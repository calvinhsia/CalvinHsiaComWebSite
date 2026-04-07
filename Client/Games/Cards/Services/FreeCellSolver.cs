using System.Diagnostics;
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

    public FreeCellSolver(FreeCellGameService gameService, Action<Func<string>>? loggerAction)
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
            _visitedStatesNumeric.Add(_game.IncrementalHashValue);
        }
        else
        {
            _visitedStates.Add(_game.GetStateHash());
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
    public List<FreeCellMove> FindMoves()
    {
        // Continuation: if the last executed move has pending sequence moves, return only the next one
        if (_moveHistory.Count > 0)
        {
            var lastMove = _moveHistory[^1];
            if (lastMove.PendingSequenceMoves is { Count: > 0 })
            {
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
            case SourceType.Tableau:
                var col = _game.Tableau[move.sourceIndex];
                if (col.Count < move.cardCount) return false;
                if (move.CardMoved != null && col[^move.cardCount] != move.CardMoved) return false;
                break;
            case SourceType.FreeCell:
                if (_game.FreeCells[move.sourceIndex] == null) return false;
                break;
            case SourceType.Foundation:
                if (_game.Foundations[move.sourceIndex].Count == 0) return false;
                break;
        }
        if (move.targetType == SourceType.FreeCell && _game.FreeCells[move.targetIndex] != null) return false;
        if (move.targetType == SourceType.Tableau && move.CardMoved != null)
        {
            var targetCol = _game.Tableau[move.targetIndex];
            if (!_game.CanPlaceOnTableau(move.CardMoved, targetCol)) return false;
        }
        if (move.targetType == SourceType.Foundation && move.CardMoved != null)
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
        if (move.sourceType == SourceType.Foundation) foundationDelta -= 2;
        if (move.targetType == SourceType.Foundation) foundationDelta += 2;

        int beforeTableau = 0;
        if (move.sourceType == SourceType.Tableau) beforeTableau += _game.GetColumnBValue(move.sourceIndex);
        if (move.targetType == SourceType.Tableau) beforeTableau += _game.GetColumnBValue(move.targetIndex);

        if (!move.ApplyMoveFast(_game))
        {
            throw new Exception($"Failed to apply {move} move for incremental score evaluation");
        }

        int afterTableau = 0;
        if (move.sourceType == SourceType.Tableau) afterTableau += _game.GetColumnBValue(move.sourceIndex);
        if (move.targetType == SourceType.Tableau) afterTableau += _game.GetColumnBValue(move.targetIndex);

        if (!move.UnApplyMove(_game))
        {
            throw new Exception($"Failed to unapply {move} move for incremental score evaluation");
        }

        return foundationDelta + (afterTableau - beforeTableau);
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
            var moves = FindMoves();
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
                                _LoggerAction?.Invoke(() => _game.dumpAllToLog($"UberBacktrack {_countNodesVisited} nodes, created {_countNodesCreated} nodes, max depth {_maxDepth}, backtracked {_numTimesBacktracked} times"));
                                while (_game.EmptyFreeCellCount < 4)
                                {
                                    currentNode = await doMoveToParentNode(currentNode);
                                    if (currentNode == null)
                                    {
                                        throw new Exception($"Failed to backtrack all the way to root during UberBacktrack at node {_countNodesVisited}.");
                                    }
                                }
                                bestMove = currentNode.ChildMoves.FirstOrDefault(m => !m.DidExecuteMove);
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
            if (bestMove.sourceType == SourceType.Foundation && bestMove.targetType == SourceType.Tableau)
            {
                _countNumberOfMovesFromFoundationToTableau++;
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
