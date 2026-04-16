using System.Diagnostics;
using Client.Games.Cards.Models;
/*
 Notes:
when a column has 1 card and there are empty freecells, move the 1 to the freecell, because an empty column is worth more
 */

namespace Client.Games.Cards.Services;

public partial class FreeCellSolver
{
    private FreeCellGameService _gameService; // current state of board including undo
    public FreeCellGameBase _game; // state of board as we manipulate it
    private List<FreeCellMove> _moveHistory = []; // so we don't repeat moves that we just did
    public int MoveHistoryCount => _moveHistory.Count;
    private HashSet<string> _visitedStates = []; // for cycle detection (string hash mode)
    // Optimization: numeric Zobrist hash set — 8 bytes per entry vs ~100+ bytes for string keys
    private HashSet<ulong> _visitedStatesNumeric = []; // for cycle detection (numeric hash mode)
    internal bool UseNumericHash = true; // flag to switch between string and numeric hashing
    public static int _nMaxNodesToVisit = 4000000;
    public static int _multipleAtWhichToUberReverse = 30000;
    public int _countVisitedNodesSinceLastUberBacktrack;
    public int _countNumberUberBacktrack = 0;
    public int _countNumberOfMovesFromFoundationToTableau = 0; // for logging / analysis purposes
    public int _countMegaMoves = 0;
    public int _countSplitMoves = 0;
    public int _countAbutMoves = 0;
    public int _countNeutralMoves = 0;
    public int _countOrderChangingMoves = 0;
    public int _countInsertUnderMoves = 0;
    public int _countBuriedFndReady = 0;
    public int _countFreeCellSeqMoves = 0;
    public int _countMaxLookAhead = 0;
    internal int? _targetClearColumn = null; // column-clearing mode: boost moves from this column
    internal int _columnClearAttemptIndex = 0; // which column-clear attempt we're on
    public static int _uberBacktrackColumnClearThreshold = 5; // uber-backtrack count after which column-clearing kicks in
    public static int _columnClearMoveThreshold = 1000; // activate column-clearing when total visited nodes exceeds this
    public static int _columnClearDepthThreshold = 2000; // activate column-clearing when max depth exceeds this
    public int _countColumnClearAttempts = 0;
    public bool _allowFoundationToTableau = true;
    public Action<Func<string>>? _LoggerAction; // avoids costly evaluation of logger messages when logging is disabled
    public int VisitedNodeCount => UseNumericHash ? _visitedStatesNumeric.Count : _visitedStates.Count;
    private bool _isEvaluatingSequenceClear = false; // recursion guard
    private List<FreeCellMove>? _pendingSequenceInitiation = null; // first moves from all discovered sequence-clears, consumed by FindMoves()

    public event Func<FreeCellMove, Task>? OnDoMove;
    public event Func<FreeCellMove, Task>? OnUndoMove;

    /// <summary>
    /// Cancellation token checked during solve loop. Set via CancelSolve().
    /// </summary>
    public CancellationToken CancellationToken { get; set; }

    /// <summary>
    /// Creates a solver for the given game state.
    /// </summary>
    /// <param name="gameService">Current game state to solve from.</param>
    /// <param name="loggerAction">Optional logging callback.</param>
    /// <param name="preVisitedStates">
    /// Optional set of Zobrist hashes representing board states the user has already visited
    /// (including undo-branch states). The solver will treat these as already-explored to avoid
    /// reverting the user's moves. The set is copied, not mutated.
    /// </param>
    /// <param name="priorMoveHistory">
    /// Optional list of moves the user has made, used to seed the solver's move history so that
    /// <c>moveWouldJustUndoPriorMove</c> correctly detects and skips immediate reversals.
    /// </param>
    public FreeCellSolver(FreeCellGameService gameService, Action<Func<string>>? loggerAction,
        HashSet<ulong>? preVisitedStates = null, List<FreeCellMove>? priorMoveHistory = null)
    {
        _gameService = gameService;
        _game = gameService.Clone();
        _LoggerAction = loggerAction;
        _game.AutoMoveToFoundationDisable = true;
        _rootTree = new FreeCellMove(cardMoved: null); // dummy root node to hold the move tree

        // Add current state to visited if not already there
        if (UseNumericHash)
        {
            _game.UseNumericHash = true;
            _game.InitIncrementalHash();
            // Seed visited states from shared page set if available
            if (preVisitedStates is { Count: > 0 })
            {
                _visitedStatesNumeric = new HashSet<ulong>(preVisitedStates);
            }
            _visitedStatesNumeric.Add(_game.IncrementalHashValue);
        }
        else
        {
            _visitedStates.Add(_game.GetStateHash());
        }

        // Seed move history for undo detection (moveWouldJustUndoPriorMove, moveWouldSwapEquivalentCard)
        if (priorMoveHistory is { Count: > 0 })
        {
            _moveHistory.AddRange(priorMoveHistory);
        }
    }

    /// <summary>
    /// Seeds the solver with prior moves from a deserialized game.
    /// Adds moves to the solver's history (so it won't immediately undo the user's last move)
    /// and records intermediate board states for cycle detection.
    /// Call after construction, before FindSolutionAsync/FindMoves.
    /// </summary>
    public void InitializeWithMoveHistory(List<FreeCellMove> priorMoves)
    {
        if (priorMoves.Count == 0) return;

        // Unapply all moves in reverse to collect intermediate state hashes for cycle detection
        for (int i = priorMoves.Count - 1; i >= 0; i--)
        {
            if (!priorMoves[i].UnApplyMove(_game))
            {
                _LoggerAction?.Invoke(() => $"InitializeWithMoveHistory: failed to unapply move {i}: {priorMoves[i]}");
                // Re-apply what we already unapplied to restore state
                for (int j = i + 1; j < priorMoves.Count; j++)
                    priorMoves[j].ApplyMoveFast(_game);
                return;
            }
            if (UseNumericHash)
                _visitedStatesNumeric.Add(_game.IncrementalHashValue);
            else
                _visitedStates.Add(_game.GetStateHash());
        }

        // Re-apply all moves to restore the current state
        foreach (var move in priorMoves)
        {
            move.ApplyMoveFast(_game);
        }

        // Add to solver's move history for moveWouldJustUndoPriorMove detection
        _moveHistory.AddRange(priorMoves);
    }

    /// <summary>
    /// Checks if making a move would result in a state we've already visited (cycle detection)
    /// </summary>
    private bool MoveWouldCauseCycle(FreeCellMove move)
    {
        // Optimization: use ApplyMoveFast (skips redundant TryMove validation)
        if (!move.ApplyMoveFast(_game))
        {
            throw new Exception($"Failed to apply {move} move for cycle detection");
        }

        bool wouldCauseCycle;
        if (UseNumericHash)
        {
            // Optimization: incremental hash — no full-board rescan, O(1) per move
            wouldCauseCycle = _visitedStatesNumeric.Contains(_game.IncrementalHashValue);
        }
        else
        {
            var hash = _game.GetStateHash();
            wouldCauseCycle = _visitedStates.Contains(hash);
        }

        // Try to unapply; if unapply fails that's a real problem — throw to surface it.
        if (!move.UnApplyMove(_game))
        {
            throw new Exception($"Failed to unapply {move} move for cycle detection");
        }
        return wouldCauseCycle;
    }
    public List<FreeCellMove> FindMovesUsingFindHelper()
    {
        // Continuation: if the last executed move has pending sequence moves, return only the next one
        if (_moveHistory.Count > 0)
        {
            var lastMove = _moveHistory[^1];
            if (lastMove.PendingSequenceMoves is { Count: > 0 })
            {
                if (lastMove.PendingSequenceMoves.Count > _countMaxLookAhead)
                {
                    _countMaxLookAhead = lastMove.PendingSequenceMoves.Count;
                }
                while (lastMove.PendingSequenceMoves.Count > 0)
                {
                    var next = lastMove.PendingSequenceMoves.Dequeue();
                    if (IsMoveApplicable(next) && !moveWouldJustUndoPriorMove(next) && !MoveWouldCauseCycle(next))
                    {
                        if (lastMove.PendingSequenceMoves.Count > 0)
                        {
                            next.PendingSequenceMoves = lastMove.PendingSequenceMoves;
                        }
                        lastMove.PendingSequenceMoves = null;
                        return [next];
                    }
                    lastMove.PendingSequenceMoves = null;
                    break;
                }
            }
        }

        var helper = new FindMoveHelper(this);
        var moves = helper.getMoves();

        // Initiation: getMoves() may have discovered sequence-clear opportunities across multiple columns
        if (_pendingSequenceInitiation != null)
        {
            var seqFirstMoves = _pendingSequenceInitiation;
            _pendingSequenceInitiation = null;
            seqFirstMoves.Sort((a, b) => b.mValue.CompareTo(a.mValue));
            var fallbackBase = moves.Count > 0 ? moves[^1].mValue : 0;
            foreach (var firstMove in seqFirstMoves)
            {
                if (IsMoveApplicable(firstMove) && !moveWouldJustUndoPriorMove(firstMove) && !MoveWouldCauseCycle(firstMove))
                {
                    firstMove.mValue = --fallbackBase;
                    moves.Add(firstMove);
                }
            }
        }

        return moves;
    }

    /// <summary>
    /// Lightweight validity check for queued moves whose board state may have changed
    /// due to backtracking.
    /// </summary>
    private bool IsMoveApplicable(FreeCellMove move)
    {
        switch (move.sourceType)
        {
            case FreeCellArea.Tableau:
                var col = _game.Tableau[move.sourceIndex];
                if (col.Count < move.cardCount) return false;
                if (move.CardMoved != null && col[^move.cardCount] != move.CardMoved) return false;
                break;
            case FreeCellArea.FreeCell:
                if (_game.FreeCells[move.sourceIndex] == null) return false;
                break;
            case FreeCellArea.Foundation:
                if (_game.Foundations[move.sourceIndex].Count == 0) return false;
                break;
        }
        if (move.targetType == FreeCellArea.FreeCell && _game.FreeCells[move.targetIndex] != null) return false;
        if (move.targetType == FreeCellArea.Tableau && move.CardMoved != null)
        {
            var targetCol = _game.Tableau[move.targetIndex];
            if (!_game.CanPlaceOnTableau(move.CardMoved, targetCol)) return false;
        }
        if (move.targetType == FreeCellArea.Foundation && move.CardMoved != null)
        {
            if (_game.CanMoveToAnyFoundation(move.CardMoved) != move.targetIndex) return false;
        }
        return true;
    }

    private bool moveWouldJustUndoPriorMove(FreeCellMove newMove)
    {
        if (_moveHistory.Count == 0) return false;
        var lastMove = _moveHistory[^1];
        if (newMove.sourceType == lastMove.targetType &&
            newMove.targetType == lastMove.sourceType &&
            newMove.sourceIndex == lastMove.targetIndex &&
            newMove.targetIndex == lastMove.sourceIndex &&
            newMove.cardCount == lastMove.cardCount)
        {
            return true;
        }
        return false;
    }

    /// <summary>
    /// Detects when a move would swap a card with an equivalent card (same rank, same color,
    /// different suit) that was just moved. E.g., moving 4♦ to freecell then 4♥ to where 4♦ was.
    /// Such swaps are no-ops on the tableau (both cards have identical placement rules) and
    /// waste 2 moves without making progress.
    /// </summary>
    private bool moveWouldSwapEquivalentCard(FreeCellMove newMove)
    {
        if (_moveHistory.Count == 0) return false;
        if (newMove.cardCount != 1) return false;
        var lastMove = _moveHistory[^1];
        if (lastMove.cardCount != 1) return false;

        var newCard = newMove.CardMoved;
        var lastCard = lastMove.CardMoved;
        if (newCard == null || lastCard == null) return false;
        if (newCard.Suit == lastCard.Suit) return false; // same suit handled by moveWouldJustUndoPriorMove
        if (newCard.Rank != lastCard.Rank) return false;
        if (newCard.IsRed != lastCard.IsRed) return false;

        // New move targets where last card came from — this is a same-color same-rank swap
        return newMove.targetType == lastMove.sourceType &&
               newMove.targetIndex == lastMove.sourceIndex;
    }

    public int MoveValueDelta(FreeCellMove move, int startBValue)
    {
        if (!move.ApplyMoveFast(_game))
        {
            throw new Exception($"Failed to apply {move} move for score evaluation");
        }
        var endValue = _game.GetBValue();
        if (!move.UnApplyMove(_game))
        {
            throw new Exception($"Failed to unapply {move} move for score evaluation");
        }
        var result = endValue - startBValue;
        return result;

    }

    /// <summary>
    /// Optimization: incremental BValue delta — only rescans the 2 affected locations (source + target)
    /// instead of all 8 tableau columns + 4 foundations. ~4x cheaper than full GetBValue().
    /// </summary>
    public int MoveValueDeltaIncremental(FreeCellMove move)
    {
        int foundationDelta = 0;
        if (move.sourceType == FreeCellArea.Foundation) foundationDelta -= 2;
        if (move.targetType == FreeCellArea.Foundation) foundationDelta += 2;

        int beforeTableau = 0;
        if (move.sourceType == FreeCellArea.Tableau) beforeTableau += _game.GetColumnBValue(move.sourceIndex);
        if (move.targetType == FreeCellArea.Tableau) beforeTableau += _game.GetColumnBValue(move.targetIndex);

        if (!move.ApplyMoveFast(_game))
        {
            throw new Exception($"Failed to apply {move} move for incremental score evaluation");
        }

        int afterTableau = 0;
        if (move.sourceType == FreeCellArea.Tableau) afterTableau += _game.GetColumnBValue(move.sourceIndex);
        if (move.targetType == FreeCellArea.Tableau) afterTableau += _game.GetColumnBValue(move.targetIndex);

        if (!move.UnApplyMove(_game))
        {
            throw new Exception($"Failed to unapply {move} move for incremental score evaluation");
        }

        return foundationDelta + (afterTableau - beforeTableau);
    }

    /// <summary>
    /// Backtracks until all 4 free cells are empty (or root is reached).
    /// Returns the updated currentNode after backtracking.
    /// </summary>
    private async Task<FreeCellMove> BacktrackUntilCondition(FreeCellMove currentNode, Func<FreeCellMove, bool> condition)
    {
        while (!condition(currentNode))
        {
            if (currentNode.IsRootNode) break;
            currentNode = await doMoveToParentNode(currentNode)
                ?? throw new Exception($"Failed to backtrack until free cells clear at node {_countNodesVisited}.");
        }
        return currentNode;
    }

    /// <summary>
    /// Activates column-clearing mode: optionally backtracks to root, selects the next best column to clear,
    /// sets <see cref="_targetClearColumn"/>, and regenerates moves with column-clearing boost.
    /// Returns the updated currentNode and the best move from the regenerated list.
    /// </summary>
    /// <param name="currentNode">Current position in the search tree.</param>
    /// <param name="backtrackToRoot">If true, backtracks all the way to root before column-clearing; if false, uses current position.</param>
    private async Task<(FreeCellMove currentNode, FreeCellMove? bestMove)> ActivateColumnClearAsync(FreeCellMove currentNode, bool backtrackToRoot)
    {
        if (backtrackToRoot)
        {
            currentNode = await BacktrackUntilCondition(currentNode, n => n.IsRootNode);
        }
        else
        {
            currentNode = await BacktrackUntilCondition(currentNode, n => _game.EmptyFreeCellCount == 4);
        }
        _LoggerAction?.Invoke(() => _game.dumpAllToLog($"BackTrack for colclr"));
        var columnsToTry = FindBestColumnsToClear();
        FreeCellMove? bestMove = null;
        if (columnsToTry.Count > 0)
        {
            int idx = _columnClearAttemptIndex % columnsToTry.Count;
            _targetClearColumn = columnsToTry[idx];

            _columnClearAttemptIndex++;
            _countColumnClearAttempts++;
            var targetCol = _targetClearColumn.Value;
            _LoggerAction?.Invoke(() => $"ColumnClear attempt #{_countColumnClearAttempts}: targeting col {targetCol} ({_game.Tableau[targetCol].Count} cards, {columnsToTry.Count} candidates)");

            // Re-generate moves from root with column-clearing boost
            //currentNode.ChildMoves.Clear();
            var newMoves = FindMovesUsingFindHelper();
            foreach (var m in newMoves)
            {
                m.ParentMove = currentNode;
                m.Depth = currentNode.Depth + 1;
            }
            currentNode.ChildMoves.AddRange(newMoves);
            _countNodesCreated += newMoves.Count;
            bestMove = newMoves.FirstOrDefault();
        }

        return (currentNode, bestMove);
    }

    /// <summary>
    /// Ranks columns by how beneficial and feasible it would be to clear them.
    /// Scores consider foundation-ready cards, chain-foundation-ready cards, cards placeable
    /// on other tableau columns, column length, and available temp slots.
    /// Returns column indices sorted by score descending.
    /// </summary>
    private List<int> FindBestColumnsToClear()
    {
        var scores = new List<(int colIndex, int score)>();
        int emptyFreeCells = _game.EmptyFreeCellCount;
        int emptyColumns = _game.EmptyTableauCount;
        int availableTemp = emptyFreeCells + emptyColumns;

        for (int col = 0; col < _game.Tableau.Count; col++)
        {
            var column = _game.Tableau[col];
            if (column.Count == 0) continue;

            int immediateReady = 0;
            int placeableOnTableau = 0;
            for (int i = 0; i < column.Count; i++)
            {
                var card = column[i];
                if (_game.CanMoveToAnyFoundation(card) >= 0)
                {
                    immediateReady++;
                    continue;
                }
                // Non-foundation-ready: check if placeable on another non-empty tableau column
                for (int otherCol = 0; otherCol < _game.Tableau.Count; otherCol++)
                {
                    if (otherCol == col) continue;
                    var otherColumn = _game.Tableau[otherCol];
                    if (otherColumn.Count > 0 && _game.CanPlaceOnTableau(card, otherColumn))
                    {
                        placeableOnTableau++;
                        break;
                    }
                }
            }

            int chainReady = CountChainFoundationReadyFullColumn(column);
            int nonChainReady = column.Count - chainReady;

            // Benefit: foundation-ready cards go directly, chain-ready cards follow
            // Cost: non-chain-ready cards must be parked; small bonus for those placeable on other columns
            int score = immediateReady * 40 + chainReady * 20 - nonChainReady * 15 + placeableOnTableau * 5;

            // Bonus if column can be fully cleared with available temp slots
            if (nonChainReady <= availableTemp)
                score += 50;

            // Bonus for shorter columns (easier to clear)
            score += Math.Max(0, 8 - column.Count) * 5;

            scores.Add((col, score));
        }

        scores.Sort((a, b) => b.score.CompareTo(a.score));
        var result = new List<int>(scores.Count);
        foreach (var (colIndex, _) in scores)
            result.Add(colIndex);
        return result;
    }

    /// <summary>
    /// Count how many cards in a column can chain to foundation (including all cards).
    /// Simulates foundation state: iteratively marks cards whose rank == simulated top + 1.
    /// </summary>
    private int CountChainFoundationReadyFullColumn(List<Card> column)
    {
        Span<int> simTopRank = stackalloc int[4];
        for (int s = 0; s < 4; s++)
            simTopRank[s] = _game.GetFoundationTopRank((Suit)s);

        int count = 0;
        bool changed = true;
        Span<bool> marked = stackalloc bool[column.Count];
        while (changed)
        {
            changed = false;
            for (int i = 0; i < column.Count; i++)
            {
                if (marked[i]) continue;
                var card = column[i];
                int suitIdx = (int)card.Suit;
                if ((int)card.Rank == simTopRank[suitIdx] + 1)
                {
                    marked[i] = true;
                    simTopRank[suitIdx] = (int)card.Rank;
                    count++;
                    changed = true;
                }
            }
        }
        return count;
    }

    public int _countNodesCreated = 0;
    public int _countNodesVisited = 0;
    public int _numTimesBacktracked = 0;
    public int _maxDepth = 0;
    public FreeCellMove _rootTree;
    public async Task<List<FreeCellMove>> FindSolutionAsync()
    {
        _rootTree = new FreeCellMove(cardMoved: null); // dummy root node to hold the move tree
        var currentNode = _rootTree;
        var doIndent = false;
        while (true)
        {
            CancellationToken.ThrowIfCancellationRequested();
            if (_game.IsGameWon)
            {
                _LoggerAction?.Invoke(() => _game.dumpAllToLog($"Game won at move count {_game.MoveCount}! Total nodes visited: {_countNodesVisited}, total nodes created: {_countNodesCreated}. # backtrack = {_numTimesBacktracked}"));
                break;
            }
            var indentation = _LoggerAction != null ? (doIndent ? new string(' ', currentNode.Depth) : string.Empty) : string.Empty;
            _LoggerAction?.Invoke(() => _game.dumpAllToLog($"Depth:{_game.MoveCount} CreatedNodes:{_countNodesCreated} VisitedNodes:{_countNodesVisited}", indentation));
            var moves = FindMovesUsingFindHelper();
            foreach (var move in moves)
            {
                move.ParentMove = currentNode;
                move.Depth = currentNode.Depth + 1;
                _LoggerAction?.Invoke(() => indentation + move.ToString());
            }
            currentNode.ChildMoves.AddRange(moves);
            _countNodesCreated += moves.Count;
            var bestMove = moves.FirstOrDefault();
            if (bestMove == null)
            {
                if (_game.IsGameWon)
                {
                    _LoggerAction?.Invoke(() => _game.dumpAllToLog($"{indentation}Game won at move count {_game.MoveCount}! Total nodes visited: {_countNodesVisited}, total nodes created: {_countNodesCreated}. # backtrack = {_numTimesBacktracked}"));
                    break;
                }
                _LoggerAction?.Invoke(() => _game.dumpAllToLog($"No moves found by solver at move count {_game.MoveCount}.", indentation));
                var keepBacktracking = true;
                while (keepBacktracking)
                {
                    currentNode.mValue = 0;
                    _numTimesBacktracked++;
                    if (_LoggerAction != null) indentation = (doIndent ? new string(' ', currentNode.Depth) : string.Empty);
                    _LoggerAction?.Invoke(() => $"{indentation}Unapplied  {_game.dumpAllToLog(currentNode.ToString(), indentation)}");
                    currentNode = await doMoveToParentNode(currentNode);
                    if (currentNode != null)
                    {
                        if (_LoggerAction != null) indentation = (doIndent ? new string(' ', currentNode.Depth) : string.Empty);
                        bestMove = currentNode.ChildMoves.FirstOrDefault(m => !m.DidExecuteMove);
                        if (bestMove == null)
                        {
                            if (currentNode.IsRootNode)
                            {
                                _LoggerAction?.Invoke(() => $"{indentation}Exhausted all moves from root node, no solution found");
                                break;
                            }
                            _LoggerAction?.Invoke(() => $"{indentation}Backtracking to move with score {currentNode.mValue} at depth {currentNode.Depth}, no more best moves, so we need to backtrack further");
                            keepBacktracking = true;
                            if (_countVisitedNodesSinceLastUberBacktrack > _multipleAtWhichToUberReverse)
                            {
                                _countVisitedNodesSinceLastUberBacktrack = 0;
                                _countNumberUberBacktrack++;
                                _LoggerAction?.Invoke(() => _game.dumpAllToLog($"UberBacktrack #{_countNumberUberBacktrack}: {_countNodesVisited} nodes, created {_countNodesCreated} nodes, max depth {_maxDepth}, backtracked {_numTimesBacktracked} times"));
                                if (_countNumberUberBacktrack >= _uberBacktrackColumnClearThreshold)
                                {
                                    (currentNode, bestMove) = await ActivateColumnClearAsync(currentNode, backtrackToRoot: true);
                                }
                                else
                                {
                                    // Standard uber-backtrack: back up until 4 free cells are empty
                                    currentNode = await BacktrackUntilCondition(currentNode, n => _game.EmptyFreeCellCount == 4);
                                    bestMove = currentNode.ChildMoves.FirstOrDefault(m => !m.DidExecuteMove);
                                    // If uber-backtrack reached root with no unexecuted children,
                                    // regenerate moves (cycle detection may now prune differently)
                                    if (bestMove == null && currentNode.IsRootNode)
                                    {
                                        currentNode.ChildMoves.Clear();
                                        var newMoves = FindMovesUsingFindHelper();
                                        foreach (var m in newMoves)
                                        {
                                            m.ParentMove = currentNode;
                                            m.Depth = currentNode.Depth + 1;
                                        }
                                        currentNode.ChildMoves.AddRange(newMoves);
                                        _countNodesCreated += newMoves.Count;
                                        bestMove = newMoves.FirstOrDefault();
                                    }
                                }
                                keepBacktracking = false;
                            }
                            else if (_moveHistory.Count > _columnClearMoveThreshold || _maxDepth > _columnClearDepthThreshold)
                            {
                                // Independent column-clear: node/depth thresholds exceeded
                                // without triggering an uber-backtrack — back up until 4 free cells
                                // are empty first, then activate column-clearing from that position
                                _LoggerAction?.Invoke(() => $"ColumnClear (independent): nodes={_countNodesVisited}, maxDepth={_maxDepth}");
                                (currentNode, bestMove) = await ActivateColumnClearAsync(currentNode, backtrackToRoot: true);
                                keepBacktracking = false;
                            }
                        }
                        else
                        {
                            _LoggerAction?.Invoke(() => $"{indentation}Found next best move at depth {currentNode.Depth}: {bestMove}, score={bestMove.mValue}, so executing it");
                            keepBacktracking = false;
                        }
                    }
                    else
                    {
                        _LoggerAction?.Invoke(() => $"{indentation}no moves found backtracking all the way to rootnode");
                        break;
                    }
                }
            }
            if (bestMove == null)
            {
                throw new Exception($"Solver failed {_game.MoveCount} to find any moves, but game is not won. Visited {(UseNumericHash ? _visitedStatesNumeric.Count : _visitedStates.Count)} states. MaxDepth = {_maxDepth}");
            }
            if (OnDoMove != null) await OnDoMove.Invoke(bestMove);
            var didit = bestMove.ApplyMoveFast(_game);
            if (!didit)
            {
                throw new Exception($"Err applying move: {bestMove}.");
            }
            bestMove.DidExecuteMove = true;
            _moveHistory.Add(bestMove);
            currentNode = bestMove;
            if (bestMove.Depth > _maxDepth)
            {
                _maxDepth = bestMove.Depth;
            }
            if (bestMove.sourceType == FreeCellArea.Foundation && bestMove.targetType == FreeCellArea.Tableau)
            {
                _countNumberOfMovesFromFoundationToTableau++;
            }

            // Clear column-clearing boost once the target column is empty (goal achieved)
            if (_targetClearColumn is int tc && _game.Tableau[tc].Count == 0)
            {
                _LoggerAction?.Invoke(() => $"ColumnClear: target col {tc} is now empty, clearing boost");
                _targetClearColumn = null;
            }

            // Record the new state after the move for cycle detection
            if (UseNumericHash)
            {
                _visitedStatesNumeric.Add(_game.IncrementalHashValue);
            }
            else
            {
                var hash = _game.GetStateHash();
                _visitedStates.Add(hash);
            }
            _countNodesVisited++;
            _countVisitedNodesSinceLastUberBacktrack++;
            if (_countNodesVisited == _nMaxNodesToVisit)
            {
                _LoggerAction?.Invoke(() => _game.dumpAllToLog($"Aborting solver after {_nMaxNodesToVisit} moves, likely stuck in a cycle. Visited {(UseNumericHash ? _visitedStatesNumeric.Count : _visitedStates.Count)} states. MaxDepth = {_maxDepth} "));
                throw new Exception($"Aborting solver after {_nMaxNodesToVisit} nodes, MaxDepth{_maxDepth}  likely stuck in a cycle. Check logs for details.");

            }
        }
        return _moveHistory;
    }

    private async Task<FreeCellMove?> doMoveToParentNode(FreeCellMove currentNode)
    {
        var didUnApply = currentNode.UnApplyMove(_game);
        if (!didUnApply)
        {
            throw new Exception($"Failed to unapply move during backtracking: {currentNode}");
        }
        if (OnUndoMove != null) await OnUndoMove.Invoke(currentNode);
        if (_moveHistory.Count > 0)
        {
            _moveHistory.RemoveAt(_moveHistory.Count - 1);
        }

        return currentNode.ParentMove;
    }
}
