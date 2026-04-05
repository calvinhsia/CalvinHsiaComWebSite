using Client.Games.Cards.Models;
using System.Diagnostics;
/*
 Notes:
when a column has 1 card and there are empty freecells, move the 1 to the freecell, because an empty column is worth more
 */

namespace Client.Games.Cards.Services;

public class FreeCellSolver
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
    public int _countInertUnderMoves = 0;
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
    private class FindMoveHelper
    {
        int _maxmValueSoFar;
        public List<FreeCellMove> _lstMoves;
        FreeCellSolver _solver;
        private bool _allowOnlyTableauPositiveMoves;

        private FreeCellGameBase _game => _solver._game;
        private Lazy<int[]> _lazyGetColumnLockCounts;

        public FindMoveHelper(FreeCellSolver solver, bool allowOnlyTableauPositiveMoves = false)
        {
            _lstMoves = [];
            _solver = solver;
            this._allowOnlyTableauPositiveMoves = allowOnlyTableauPositiveMoves;
            _maxmValueSoFar = 0;
            _lazyGetColumnLockCounts = new Lazy<int[]>(() => _game.GetColumnLockCounts());
        }
        bool AddNewMove(FreeCellMove move)
        {
            var didit = false;
            if (!_solver.moveWouldJustUndoPriorMove(move))
            {
                if (!_solver.MoveWouldCauseCycle(move))
                {
                    if (move.mValue > _maxmValueSoFar)
                    {
                        _maxmValueSoFar = move.mValue;
                    }
                    _lstMoves.Add(move);
                    didit = true;
                }
            }
            return didit;
        }
        void FindMoveAnyFreeCellToFoundationOrTableau()
        {
            for (int i = 0; i < _game.FreeCells.Count; i++)
            {
                var freecellCard = _game.FreeCells[i];
                if (freecellCard == null) continue;
                // Check if we can move this card to a foundation
                if (!_allowOnlyTableauPositiveMoves) // don't look for moves to foundation when looking for moves from foundation to tableau, we're desperate, so we already found no tableau to foundation moves so don't find the move that would undo our test move
                {
                    var foundationIndex = _game.CanMoveToAnyFoundation(freecellCard);
                    if (foundationIndex >= 0)
                    {
                        AddNewMove(new FreeCellMove(freecellCard)
                        {
                            sourceType = SourceType.FreeCell,
                            targetType = SourceType.Foundation,
                            sourceIndex = i,
                            targetIndex = foundationIndex,
                            cardCount = 1,
                            mValue = 100 // arbitrary score for now
                        });
                    }
                }
                // now see if freecell to tableau
                // Optimization: check Count==0 first to skip CanMoveFreeCellToTableau for empty columns
                // (any card can always be placed on an empty column, so the call is redundant)
                FreeCellMove? deferredEmptyColMove = null;
                var canMoveToNonEmptyTableau = false;
                for (var dstCol = 0; dstCol < _game.Tableau.Count; dstCol++)
                {
                    var columnDest = _game.Tableau[dstCol];
                    if (columnDest.Count == 0) // empty column — always a valid destination, no need to call CanMoveFreeCellToTableau
                    {
                        if (deferredEmptyColMove == null) // only keep the first empty-column candidate (all empty cols are equivalent)
                        {
                            deferredEmptyColMove = new FreeCellMove(freecellCard)
                            {
                                sourceType = SourceType.FreeCell,
                                targetType = SourceType.Tableau,
                                sourceIndex = i,
                                targetIndex = dstCol,
                                cardCount = 1,
                                mValue = 80
                            };
                        }
                    }
                    else if (_game.CanMoveFreeCellToTableau(i, dstCol))
                    {
                        AddNewMove(new FreeCellMove(freecellCard)
                        {
                            sourceType = SourceType.FreeCell,
                            targetType = SourceType.Tableau,
                            sourceIndex = i,
                            targetIndex = dstCol,
                            cardCount = 1,
                            mValue = 80
                        });
                        canMoveToNonEmptyTableau = true;
                    }
                }
                // Always evaluate the empty-column move via MoveEffectOnBoard — even when non-empty
                // destinations exist. Pruning it entirely causes regressions: in some game states the
                // empty-column route (place card, then enable a large multi-card move) is the only
                // viable path, and removing it from the tree starves the backtracker of alternatives.
                if (deferredEmptyColMove != null)
                {
                    var goodMove = MoveEffectOnBoard(deferredEmptyColMove);
                    if (goodMove != null)
                    {
                        _solver._LoggerAction?.Invoke(() => $"move {deferredEmptyColMove} from FreeCell to Tableau empty column: Yields {goodMove}");
                        deferredEmptyColMove.mValue += 100;
                        deferredEmptyColMove.PendingSequenceMoves = new Queue<FreeCellMove>([goodMove]);
                        AddNewMove(deferredEmptyColMove);
                    }
                }

                // Insert-under-sequence: move a column's bottom sequence to temp storage,
                // place this freecell card underneath, then restore the sequence on top.
                // Extends the sorted run by 1 and frees a free cell.
                if (!_allowOnlyTableauPositiveMoves)
                {
                    // Need enough temp slots (free cells + empty columns) to hold the sequence
                    int availableTemp = _game.EmptyFreeCellCount + _game.EmptyTableauCount;
                    if (availableTemp > 0)
                    {
                        for (var dstCol = 0; dstCol < _game.Tableau.Count; dstCol++)
                        {
                            var colDest = _game.Tableau[dstCol];
                            if (colDest.Count == 0) continue;
                            var seqLen = _game.GetBottomSequenceLength(dstCol);
                            if (seqLen < 1) continue;
                            if (seqLen > availableTemp) continue;

                            var topOfSeq = colDest[colDest.Count - seqLen];
                            // freecellCard must connect directly above the sequence
                            if ((int)freecellCard.Rank != (int)topOfSeq.Rank + 1) continue;
                            if (freecellCard.IsRed == topOfSeq.IsRed) continue;

                            // freecellCard must also fit on the card above the sequence (or column is entirely the sequence)
                            if (seqLen < colDest.Count)
                            {
                                var cardAbove = colDest[colDest.Count - seqLen - 1];
                                if ((int)freecellCard.Rank != (int)cardAbove.Rank - 1) continue;
                                if (freecellCard.IsRed == cardAbove.IsRed) continue;
                            }


                            // Capture sequence cards in bottom-to-top order
                            var seqCards = new Card[seqLen];
                            for (int s = 0; s < seqLen; s++)
                                seqCards[s] = colDest[colDest.Count - 1 - s];

                            var allMoves = new List<FreeCellMove>(seqLen * 2 + 1);
                            var cardLocations = new Dictionary<Card, (SourceType type, int index)>(seqLen);
                            int fcSlot = 0;
                            int emptyCol = 0;
                            bool allAllocated = true;
                            int mVal = 150 + seqLen * 10;

                            // Phase 1: move sequence cards to temp storage (bottom card first)
                            for (int s = 0; s < seqLen; s++)
                            {
                                var card = seqCards[s];
                                while (fcSlot < _game.FreeCells.Count && _game.FreeCells[fcSlot] != null)
                                    fcSlot++;
                                if (fcSlot < _game.FreeCells.Count)
                                {
                                    cardLocations[card] = (SourceType.FreeCell, fcSlot);
                                    allMoves.Add(new FreeCellMove(card)
                                    {
                                        sourceType = SourceType.Tableau,
                                        targetType = SourceType.FreeCell,
                                        sourceIndex = dstCol,
                                        targetIndex = fcSlot,
                                        cardCount = 1,
                                        mValue = mVal
                                    });
                                    fcSlot++;
                                }
                                else
                                {
                                    while (emptyCol < _game.Tableau.Count && (_game.Tableau[emptyCol].Count != 0 || emptyCol == dstCol))
                                        emptyCol++;
                                    if (emptyCol >= _game.Tableau.Count) { allAllocated = false; break; }
                                    cardLocations[card] = (SourceType.Tableau, emptyCol);
                                    allMoves.Add(new FreeCellMove(card)
                                    {
                                        sourceType = SourceType.Tableau,
                                        targetType = SourceType.Tableau,
                                        sourceIndex = dstCol,
                                        targetIndex = emptyCol,
                                        cardCount = 1,
                                        mValue = mVal
                                    });
                                    emptyCol++;
                                }
                            }
                            if (!allAllocated) continue;

                            // Phase 2: move freecell card onto the column
                            allMoves.Add(new FreeCellMove(freecellCard)
                            {
                                sourceType = SourceType.FreeCell,
                                targetType = SourceType.Tableau,
                                sourceIndex = i,
                                targetIndex = dstCol,
                                cardCount = 1,
                                mValue = mVal
                            });

                            // Phase 3: restore sequence cards back onto the column (top of sequence first)
                            for (int s = seqLen - 1; s >= 0; s--)
                            {
                                var card = seqCards[s];
                                var (srcType, srcIdx) = cardLocations[card];
                                allMoves.Add(new FreeCellMove(card)
                                {
                                    sourceType = srcType,
                                    targetType = SourceType.Tableau,
                                    sourceIndex = srcIdx,
                                    targetIndex = dstCol,
                                    cardCount = 1,
                                    mValue = mVal
                                });
                            }

                            // First move is the carrier, rest are PendingSequenceMoves
                            var firstInsertMove = allMoves[0];
                            var insertQueue = new Queue<FreeCellMove>();
                            for (int m = 1; m < allMoves.Count; m++)
                                insertQueue.Enqueue(allMoves[m]);
                            firstInsertMove.PendingSequenceMoves = insertQueue;
                            AddNewMove(firstInsertMove);
                            _solver._countInertUnderMoves++;
                            _solver._LoggerAction?.Invoke(() =>
                                $"InsertUnderSeq: freecell[{i}] {freecellCard} -> col {dstCol} under seqLen={seqLen}, {insertQueue.Count} queued moves");
                        }
                    }
                }

            }
        }
        public void FindMoveAnyTableauToTableauOrFoundation()
        {
            // Optimization: hoist EmptyFreeCellCount/EmptyTableauCount out of loop — they're identical across all columns
            int tableauColCount = _game.Tableau.Count;
            int emptyFreeCells = _game.EmptyFreeCellCount;
            int emptyColumns = _game.EmptyTableauCount;
            // Precompute bottom sequence lengths for all columns (needed by split/combine logic)
            var seqLens = new int[tableauColCount];
            Span<int> maxMovablePerCol = stackalloc int[tableauColCount];
            for (int c = 0; c < tableauColCount; c++)
            {
                int ec = (_game.Tableau[c].Count == 0) ? Math.Max(0, emptyColumns - 1) : emptyColumns;
                maxMovablePerCol[c] = (1 + emptyFreeCells) << ec;
                seqLens[c] = _game.Tableau[c].Count > 0 ? _game.GetBottomSequenceLength(c) : 0;
            }

            // Optimization: reuse a single list across source column iterations to avoid per-column allocation
            var tableauMoves = new List<FreeCellMove>();
            var allTableauToTableauMoves = new List<FreeCellMove>();
            for (int srcCol = 0; srcCol < tableauColCount; srcCol++)
            {
                var column = _game.Tableau[srcCol];
                if (column.Count == 0) continue; // empty column, nothing to move
                var seqlen = seqLens[srcCol];
                var topCard = column[^seqlen];
                var botCard = column[^1];
                //if (!_allowOnlyTableauPositiveMoves)
                {
                    // Check if we can move this card to a foundation
                    var foundationIdx = _game.CanMoveToAnyFoundation(botCard);
                    if (foundationIdx >= 0)
                    {
                        AddNewMove(new FreeCellMove(botCard)
                        {
                            sourceType = SourceType.Tableau,
                            targetType = SourceType.Foundation,
                            sourceIndex = srcCol,
                            targetIndex = foundationIdx,
                            cardCount = 1,
                            mValue = 100 // arbitrary score for now
                        });
                    }
                }
                var lockCount = _lazyGetColumnLockCounts.Value[srcCol];
                if (column.Count == lockCount)
                {
                    continue; // The column consists entirely of a seq starting with a K, like K, or KQJ... moving any cards from this column to another column is worthless points because it doesn't free up any locked cards, so we skip it when looking for positive moves from tableau to tableau. We may still want to move from this column to foundation though, so we don't skip it entirely.
                }
                var didCheckEmptyDstCol = false;
                tableauMoves.Clear();
                for (var dstCol = 0; dstCol < tableauColCount; dstCol++)
                {
                    if (srcCol == dstCol) continue;
                    // if the destination column is empty, and the seqlen is the entire column, don't do anything. Moving an entire column is a no-op
                    var numCardsInDestCol = _game.Tableau[dstCol].Count;
                    if (numCardsInDestCol == 0)
                    {
                        if (seqlen == column.Count)
                            continue; // whole-column to empty is a no-op
                        if (didCheckEmptyDstCol)
                            continue; // Pruning: only try one empty dest column per source (all empty cols are equivalent)
                        didCheckEmptyDstCol = true;
                    }
                    if (seqlen > maxMovablePerCol[dstCol])
                    {
                        continue;
                    }
                    if (_game.CanMoveTableauToTableau(srcCol, dstCol, seqlen))
                    {
                        var mVal = 50 + seqlen * 10; // arbitrary scoring that favors longer moves
                        // Penalize when continuation cards (rank-1, opposite color) are buried, especially in the destination column
                        var penalty = Math.Min(GetContinuationBlockedPenalty(botCard, srcCol, dstCol, seqlen), seqlen * 10);
                        mVal -= penalty;
                        tableauMoves.Add(new FreeCellMove(topCard)
                        {
                            sourceType = SourceType.Tableau,
                            targetType = SourceType.Tableau,
                            sourceIndex = srcCol,
                            targetIndex = dstCol,
                            cardCount = seqlen,
                            mValue = mVal
                        });
                    }
                }
                // Split sequences: when seqlen > maxMovable for some destination, try splitting via intermediate column
                if (seqlen > 3)
                {
                    FindSplitSequenceMoves(srcCol, seqlen, emptyFreeCells, emptyColumns, maxMovablePerCol, tableauColCount, tableauMoves);
                }
                // Only differentiate when multiple destinations compete for the same source.
                // Optimization: use incremental BValue delta — only rescan the 2 affected columns instead of all 8
                if (tableauMoves.Count > 1)
                {
                    foreach (var move in tableauMoves)
                    {
                        var delta = _solver.MoveValueDeltaIncremental(move);
                        move.mValue += delta * 10; // differentiate destinations by BValue impact
                    }
                }
                foreach (var move in tableauMoves)
                {
                    if (AddNewMove(move))
                    {
                        allTableauToTableauMoves.Add(move);
                    }
                }
            }
            // Abutting: move a suffix of one column's sequence onto another column's sequence
            // to create a longer combined sequence. Only try when no high-scoring moves found yet.
            // Save _maxmValueSoFar — speculative moves (abut, resequence) should not gate
            // freecell exploration where foundation chains (e.g. J→FC exposing A→Foundation) may hide.
            var maxValueBeforeSpeculative = _maxmValueSoFar;
            if (_maxmValueSoFar < 50)
            {
                FindAbutSequenceMoves(seqLens, maxMovablePerCol, tableauColCount, allTableauToTableauMoves);
            }
            FindAnyMoveOrderChanging(emptyFreeCells, emptyColumns, seqLens);
            _maxmValueSoFar = maxValueBeforeSpeculative;
            BoostChainMoves(allTableauToTableauMoves);
        }
        /// <summary>
        /// For each non-empty column: look at the bottom N cards below the locked K-sequence.
        /// Filter out cards that can go to foundation right now (they stay parked and the
        /// solver moves them to foundation on the next iteration). The remaining cards must form a
        /// valid descending alternating-color sequence to be placed back on the column.
        /// </summary>
        public void FindAnyMoveOrderChanging(int emptyFreeCells, int emptyColumns, int[] seqLens)
        {
            // Cap empty columns used for resequencing: when the board is nearly solved
            // (many empty columns), the solver already has massive mobility via normal
            // tableau-to-tableau moves (maxMovable doubles per empty column), so resequencing
            // is wasted work and combinatorial bloat. Use at most 2 empty columns.
            int usableEmptyColumns = Math.Min(emptyColumns, 2);
            int totalTempSlots = emptyFreeCells + usableEmptyColumns;
            if (totalTempSlots < 2) return; // need at least 2 temp slots to reorder anything
            if (_allowOnlyTableauPositiveMoves) return;

            for (var iCol = 0; iCol < _game.Tableau.Count; iCol++)
            {
                var column = _game.Tableau[iCol];
                if (column.Count < 2) continue;
                var lockCount = _lazyGetColumnLockCounts.Value[iCol];
                var cardsBelow = column.Count - lockCount;
                if (cardsBelow < 2) continue;

                var existingSeqLen = seqLens[iCol]; // reuse precomputed value from parent
                // Expand window: use free cells + capped empty columns as temp storage
                var cardsToResequence = Math.Min(cardsBelow, emptyFreeCells + usableEmptyColumns);
                if (cardsToResequence < 2) continue;

                var startIdx = column.Count - cardsToResequence;

                // Collect the bottom cardsToResequence non-locked cards, filtering out
                // any that can go to foundation right now (they'll stay parked
                // and the solver moves them to foundation on the next iteration).
                var remaining = new List<Card>(cardsToResequence);
                int foundationCount = 0;
                for (int i = 0; i < cardsToResequence; i++)
                {
                    var card = column[startIdx + i];
                    if (_game.CanMoveToAnyFoundation(card) >= 0)
                        foundationCount++;
                    else
                        remaining.Add(card);
                }

                // Skip if no improvement: no foundation cards found AND remaining won't extend the sequence
                if (foundationCount == 0 && cardsToResequence <= existingSeqLen) continue;

                // Validate remaining cards form a valid descending alternating-color sequence
                if (remaining.Count > 0)
                {
                    remaining.Sort((a, b) => ((int)b.Rank).CompareTo((int)a.Rank));

                    bool valid = true;
                    for (int i = 1; i < remaining.Count; i++)
                    {
                        if ((int)remaining[i].Rank != (int)remaining[i - 1].Rank - 1)
                        { valid = false; break; }
                    }
                    if (!valid) continue;

                    for (int i = 1; i < remaining.Count; i++)
                    {
                        if (remaining[i].IsRed == remaining[i - 1].IsRed)
                        { valid = false; break; }
                    }
                    if (!valid) continue;

                    // Check the top card of the remaining sequence fits on the card above the window
                    var topCard = remaining[0];
                    if (startIdx > 0)
                    {
                        var cardAbove = column[startIdx - 1];
                        if (topCard.IsRed == cardAbove.IsRed || (int)topCard.Rank != (int)cardAbove.Rank - 1)
                            continue;
                    }

                    // With no foundation benefit, remaining must actually extend the existing sequence
                    if (foundationCount == 0 && remaining.Count <= existingSeqLen) continue;
                }

                // Generate Phase 1: move all window cards to temp storage (from bottom up).
                // Use free cells first (less valuable than empty columns), overflow to empty columns.
                var allMoves = new List<FreeCellMove>(cardsToResequence + remaining.Count);
                var cardLocations = new Dictionary<Card, (SourceType type, int index)>(cardsToResequence);
                int fcIdx = 0;
                int emptyColIdx = 0;
                bool allAllocated = true;
                int mVal = 200 + foundationCount * 100 + Math.Max(0, remaining.Count - existingSeqLen) * 20;
                for (int i = 0; i < cardsToResequence; i++)
                {
                    var card = column[startIdx + cardsToResequence - 1 - i]; // remove from bottom up
                    // Try free cells first
                    while (fcIdx < _game.FreeCells.Count && _game.FreeCells[fcIdx] != null)
                        fcIdx++;
                    if (fcIdx < _game.FreeCells.Count)
                    {
                        cardLocations[card] = (SourceType.FreeCell, fcIdx);
                        allMoves.Add(new FreeCellMove(card)
                        {
                            sourceType = SourceType.Tableau,
                            targetType = SourceType.FreeCell,
                            sourceIndex = iCol,
                            targetIndex = fcIdx,
                            cardCount = 1,
                            mValue = mVal
                        });
                        fcIdx++;
                    }
                    else
                    {
                        // Overflow to empty columns
                        while (emptyColIdx < _game.Tableau.Count && (_game.Tableau[emptyColIdx].Count != 0 || emptyColIdx == iCol))
                            emptyColIdx++;
                        if (emptyColIdx >= _game.Tableau.Count) { allAllocated = false; break; }
                        cardLocations[card] = (SourceType.Tableau, emptyColIdx);
                        allMoves.Add(new FreeCellMove(card)
                        {
                            sourceType = SourceType.Tableau,
                            targetType = SourceType.Tableau,
                            sourceIndex = iCol,
                            targetIndex = emptyColIdx,
                            cardCount = 1,
                            mValue = mVal
                        });
                        emptyColIdx++;
                    }
                }
                if (!allAllocated) continue;

                // Generate Phase 2: remaining cards back to column in sorted order (highest rank first).
                // Foundation-ready cards stay wherever parked — the solver moves them next iteration.
                foreach (var card in remaining)
                {
                    var (srcType, srcIdx) = cardLocations[card];
                    allMoves.Add(new FreeCellMove(card)
                    {
                        sourceType = srcType,
                        targetType = SourceType.Tableau,
                        sourceIndex = srcIdx,
                        targetIndex = iCol,
                        cardCount = 1,
                        mValue = mVal
                    });
                }

                // First move is the carrier, rest are queued as PendingSequenceMoves
                var firstMove = allMoves[0];
                var queue = new Queue<FreeCellMove>();
                for (int m = 1; m < allMoves.Count; m++)
                    queue.Enqueue(allMoves[m]);
                firstMove.PendingSequenceMoves = queue;
                AddNewMove(firstMove);
                _solver._countOrderChangingMoves++;
                _solver._LoggerAction?.Invoke(() =>
                    $"OrderChanging: col {iCol} resequence {cardsToResequence} cards (seq {existingSeqLen}->{remaining.Count}), {foundationCount} foundation-ready stay parked, {queue.Count} queued moves, freeCells={emptyFreeCells} emptyCols={usableEmptyColumns}");
            }
        }
        public void FindMoveAnyFoundationToTableau()
        {
            // see if any foundation cells can be added to tableau
            for (int i = 0; i < _game.Foundations.Count; i++)
            {
                var foundation = _game.Foundations[i];
                if (foundation.Count > 0)
                {
                    var topCard = _game.Foundations[i][^1];
                    if (topCard != null && (int)topCard.Rank > 2) // don't try an ace or 2
                    {
                        var didCheckEmptyColumn = false;
                        for (int iCol = 0; iCol < _game.Tableau.Count - 1; iCol++) // see if the foundation card can be added to a column even if empty
                        {
                            var column = _game.Tableau[iCol];
                            if (_game.CanPlaceOnTableau(topCard, column))
                            {
                                if (column.Count == 0) // empty column?
                                {
                                    if (didCheckEmptyColumn)
                                        continue;
                                    didCheckEmptyColumn = true;
                                }
                                // just because we can place it from Foundation to tableau, doesn't mean we want to. Only do so if it would increase the seq total.
                                var move = new FreeCellMove(topCard)
                                {
                                    sourceType = SourceType.Foundation,
                                    targetType = SourceType.Tableau,
                                    sourceIndex = i,
                                    targetIndex = iCol,
                                    cardCount = 1,
                                    mValue = 50 // make this score really high: when doing so results in goodmove
                                };
                                var goodMove = MoveEffectOnBoard(move);
                                if (goodMove != null) // only add the move if it results in a positive change to the board
                                {
                                    _solver._LoggerAction?.Invoke(() => $"move {move} from Foundation to Tableau: Yields {goodMove}");
                                    move.PendingSequenceMoves = new Queue<FreeCellMove>([goodMove]);
                                    var didAdd = AddNewMove(move);
                                }
                            }
                        }
                    }
                }
            }
        }
        FreeCellMove? MoveEffectOnBoard(FreeCellMove move, int? columnOfInterest = null)
        {
            move.ApplyMoveFast(_game);
            var helper = new FindMoveHelper(_solver, allowOnlyTableauPositiveMoves: true);
            var moves = helper.getMoves();
            move.UnApplyMove(_game);

            // Only return moves that involve the column of interest —
            // defaults to targetIndex (for moves placing a card on a column),
            // but callers can pass sourceIndex (e.g. tableau→freecell exposes a new card in the source column).
            var col = columnOfInterest ?? move.targetIndex;
            var result = moves.FirstOrDefault(m =>
                (m.targetType == SourceType.Tableau && m.targetIndex == col) ||
                (m.sourceType == SourceType.Tableau && m.sourceIndex == col));
            return result;
        }
        public int FindMoveAnyTableauToFreeCell()
        {
            var numMovesFound = 0;
            // now see if can move to free cell
            int nFreeCells = _game.EmptyFreeCellCount;
            if (nFreeCells == 0)
            {
                return numMovesFound;
            }
            if (_maxmValueSoFar < 2) // don't move to freecell if we already have a good foundation or tableau move
            {
                for (int iCol = 0; iCol < _game.Tableau.Count; iCol++)
                {
                    var column = _game.Tableau[iCol];
                    if (column.Count == 0)
                    {
                        continue;
                    }
                    if (column.Count > 1 && _lazyGetColumnLockCounts.Value[iCol] == column.Count) // if a single card, it may be moved to a free cell.
                    {
                        continue; // The column starts with KQJ...moving any cards from this column to a freecell is worthless points because it doesn't free up any locked cards
                    }
                    // If the column count is 1, more points because an empty column is worth more than an empty freecell.
                    var score = 1;
                    if (column.Count == 1)
                    {
                        score += 4;
                    }
                    else
                    {
                        // Penalize breaking a sequence — if the bottom card is part of a sorted run, moving it to freecell is destructive
                        var seqLen = _game.GetBottomSequenceLength(iCol);
                        if (seqLen > 1)
                        {
                            score -= seqLen; // longer sequence broken = bigger penalty
                        }
                        // Boost for buried foundation-ready cards — closer to bottom = higher score (easier to uncover)
                        int[]? rankCounts = column.Count >= 4 ? new int[14] : null;
                        for (int i = 0; i < column.Count - 1; i++) // skip last card (it's being moved to freecell)
                        {
                            var card = column[i];
                            if (_game.CanMoveToAnyFoundation(card) >= 0)
                            {
                                score += (i + 1); // i=0 (top, most buried) gets +1; closer to bottom gets more
                            }
                            if (rankCounts != null)
                            {
                                rankCounts[(int)card.Rank]++;
                            }
                        }
                        // Big boost when remaining cards have 3 or 4 of the same rank
                        if (rankCounts != null)
                        {
                            for (int r = 1; r <= 13; r++)
                            {
                                switch (rankCounts[r])
                                {
                                    case 2:
                                        score += 4;
                                        break;
                                    case 3:
                                        score += 15;
                                        break;
                                    case 4:
                                        score += 20;
                                        break;
                                }
                            }
                        }
                    }
                    var move = new FreeCellMove(column[^1])
                    {
                        sourceType = SourceType.Tableau,
                        targetType = SourceType.FreeCell,
                        sourceIndex = iCol,
                        targetIndex = _game.FindAnyFreeCell(),
                        cardCount = 1,
                        mValue = score
                    };
                    // Check if moving this card to freecell enables a positive follow-up in the source column
                    var followUp = MoveEffectOnBoard(move, iCol);
                    if (followUp != null)
                    {
                        move.mValue += followUp.mValue;
                        var cardX = move.CardMoved!;
                        bool isFreecellNeutral;
                        if (followUp.sourceType == SourceType.Tableau && followUp.sourceIndex == iCol)
                        {
                            int remaining = column.Count - 1 - followUp.cardCount;
                            if (remaining <= 0)
                            {
                                isFreecellNeutral = true; // column empty — any card can go back
                            }
                            else
                            {
                                var newBottom = column[remaining - 1];
                                isFreecellNeutral = cardX.IsRed != newBottom.IsRed && (int)cardX.Rank == (int)newBottom.Rank - 1;
                            }
                        }
                        else if (followUp.targetType == SourceType.Tableau && followUp.targetIndex == iCol)
                        {
                            var newBottom = followUp.sourceType == SourceType.Tableau
                                ? _game.Tableau[followUp.sourceIndex][^1]
                                : followUp.CardMoved!;
                            isFreecellNeutral = cardX.IsRed != newBottom.IsRed && (int)cardX.Rank == (int)newBottom.Rank - 1;
                        }
                        else
                        {
                            isFreecellNeutral = false;
                        }
                        if (isFreecellNeutral)
                        {
                            var reverseMove = new FreeCellMove(move.CardMoved!)
                            {
                                sourceType = SourceType.FreeCell,
                                targetType = SourceType.Tableau,
                                sourceIndex = move.targetIndex,
                                targetIndex = iCol,
                                cardCount = 1,
                                mValue = 100
                            };
                            move.mValue += 100; // big boost for freecell-neutral chain
                            move.PendingSequenceMoves = new Queue<FreeCellMove>([followUp, reverseMove]);
                            _solver._LoggerAction?.Invoke(() => $"Freecell-neutral: col {iCol} card {move.CardMoved} -> freecell, {followUp}, then back to col {iCol}");
                            _solver._countNeutralMoves++;
                        }
                        else
                        {
                            move.PendingSequenceMoves = new Queue<FreeCellMove>([followUp]);
                        }
                    }
                    AddNewMove(move);
                    numMovesFound++;
                }
            }
            if (_maxmValueSoFar < 3 && !_solver._isEvaluatingSequenceClear
                && _solver._pendingSequenceInitiation == null) // still no good move — see if clearing a bottom sequence into freecells enables a positive follow-up
            {
                var numFreeCells = _game.EmptyFreeCellCount;
                if (numFreeCells > 1) // for a seq move, need > 1 free cell
                {
                    FreeCellMove? megaboostCarrier = null;
                    for (var iCol = 0; iCol < _game.Tableau.Count; iCol++)
                    {
                        var column = _game.Tableau[iCol];
                        if (column.Count == 0) continue;
                        if (column.Count <= numFreeCells) continue; // can already clear entire column trivially
                        if (_lazyGetColumnLockCounts.Value[iCol] == column.Count) continue; // fully locked K-sequence
                        var seqlen = _game.GetBottomSequenceLength(iCol);
                        if (seqlen > 1 && seqlen <= numFreeCells)
                        {
                            // Temporarily move the entire bottom sequence into freecells
                            var tempMoves = new List<FreeCellMove>(seqlen);
                            var freeCellIdx = 0;
                            bool allApplied = true;
                            for (int s = 0; s < seqlen; s++)
                            {
                                while (freeCellIdx < _game.FreeCells.Count && _game.FreeCells[freeCellIdx] != null)
                                    freeCellIdx++;
                                if (freeCellIdx >= _game.FreeCells.Count) { allApplied = false; break; }

                                var card = column[^1]; // bottom card shifts as we remove
                                var tempMove = new FreeCellMove(card)
                                {
                                    sourceType = SourceType.Tableau,
                                    targetType = SourceType.FreeCell,
                                    sourceIndex = iCol,
                                    targetIndex = freeCellIdx,
                                    cardCount = 1,
                                    mValue = 0
                                };
                                if (!tempMove.ApplyMoveFast(_game))
                                {
                                    allApplied = false;
                                    break;
                                }
                                tempMoves.Add(tempMove);
                                freeCellIdx++;
                            }

                            FreeCellMove? goodMove = null;
                            List<FreeCellMove>? reverseMoves = null;
                            if (allApplied)
                            {
                                // Check if exposing the cards underneath creates positive tableau moves
                                _solver._isEvaluatingSequenceClear = true;
                                try
                                {
                                    var innerHelper = new FindMoveHelper(_solver, allowOnlyTableauPositiveMoves: true);
                                    var followUpMoves = innerHelper.getMoves();
                                    goodMove = followUpMoves.FirstOrDefault(m =>
                                        (m.sourceType == SourceType.Tableau && m.sourceIndex == iCol) ||
                                        (m.targetType == SourceType.Tableau && m.targetIndex == iCol));
                                }
                                finally
                                {
                                    _solver._isEvaluatingSequenceClear = false;
                                }
                                // Check if the cleared sequence can be put back after the enabled move
                                if (goodMove != null && goodMove.ApplyMoveFast(_game))
                                {
                                    var topOfSeqCard = tempMoves[^1].CardMoved!;
                                    var colAfter = _game.Tableau[iCol];
                                    if (colAfter.Count == 0 || _game.CanPlaceOnTableau(topOfSeqCard, colAfter))
                                    {
                                        reverseMoves = new List<FreeCellMove>(seqlen);
                                        for (int r = tempMoves.Count - 1; r >= 0; r--)
                                        {
                                            var tm = tempMoves[r];
                                            reverseMoves.Add(new FreeCellMove(tm.CardMoved!)
                                            {
                                                sourceType = SourceType.FreeCell,
                                                targetType = SourceType.Tableau,
                                                sourceIndex = tm.targetIndex,
                                                targetIndex = iCol,
                                                cardCount = 1,
                                                mValue = 0
                                            });
                                        }
                                    }
                                    goodMove.UnApplyMove(_game);
                                }
                            }

                            // Undo all temporary moves in reverse order
                            for (int s = tempMoves.Count - 1; s >= 0; s--)
                            {
                                tempMoves[s].UnApplyMove(_game);
                            }

                            if (goodMove != null)
                            {
                                var boostValue = reverseMoves != null ? 200 + 10 * seqlen : 10 * seqlen;
                                foreach (var tm in tempMoves)
                                {
                                    tm.mValue = boostValue;
                                }

                                if (reverseMoves != null && megaboostCarrier != null)
                                {
                                    foreach (var tm in tempMoves)
                                        megaboostCarrier.PendingSequenceMoves!.Enqueue(tm);
                                    goodMove.mValue = boostValue;
                                    megaboostCarrier.PendingSequenceMoves!.Enqueue(goodMove);
                                    foreach (var rm in reverseMoves)
                                    {
                                        rm.mValue = boostValue;
                                        megaboostCarrier.PendingSequenceMoves.Enqueue(rm);
                                    }
                                    megaboostCarrier.mValue += boostValue;
                                    if (megaboostCarrier.mValue > _maxmValueSoFar)
                                    {
                                        _maxmValueSoFar = megaboostCarrier.mValue;
                                    }
                                    _solver._countMegaMoves++;
                                    _solver._LoggerAction?.Invoke(() => $"Sequence-clear: col {iCol} seqlen={seqlen} -> chained onto carrier {megaboostCarrier} enables {goodMove}, queue: {megaboostCarrier.PendingSequenceMoves.Count}");
                                }
                                else
                                {
                                    var queue = new Queue<FreeCellMove>();
                                    for (int q = 1; q < tempMoves.Count; q++)
                                        queue.Enqueue(tempMoves[q]);
                                    if (reverseMoves != null)
                                    {
                                        goodMove.mValue = boostValue;
                                        queue.Enqueue(goodMove);
                                        foreach (var rm in reverseMoves)
                                        {
                                            rm.mValue = boostValue;
                                            queue.Enqueue(rm);
                                        }
                                    }
                                    var existingMove = _lstMoves.FirstOrDefault(m =>
                                        m.sourceType == SourceType.Tableau &&
                                        m.targetType == SourceType.FreeCell &&
                                        m.sourceIndex == iCol);
                                    if (existingMove != null)
                                    {
                                        existingMove.mValue += boostValue;
                                        if (existingMove.mValue > _maxmValueSoFar)
                                        {
                                            _maxmValueSoFar = existingMove.mValue;
                                        }
                                        if (queue.Count > 0)
                                        {
                                            existingMove.PendingSequenceMoves = queue;
                                        }
                                        if (reverseMoves != null)
                                        {
                                            megaboostCarrier = existingMove;
                                        }
                                        _solver._countMegaMoves++;
                                        _solver._LoggerAction?.Invoke(() => $"Sequence-clear: col {iCol} seqlen={seqlen} -> boosted existing move {existingMove} enables {goodMove}, reversible={reverseMoves != null}, queue: {queue.Count}");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return numMovesFound;
        }

        /// <summary>
        /// Pruning: Check for safe auto-foundation moves — cards that can ALWAYS go to foundation
        /// because both opposite-color cards of (rank-1) are already in foundation.
        /// When found, these moves dominate all alternatives and we return only them.
        /// </summary>
        private bool FindSafeAutoFoundationMoves()
        {
            var safeMovesFound = false;
            // Check freecells
            for (int i = 0; i < _game.FreeCells.Count; i++)
            {
                var card = _game.FreeCells[i];
                if (card == null) continue;
                var foundationIdx = _game.CanMoveToAnyFoundation(card);
                if (foundationIdx >= 0 && _game.IsSafeToMoveToFoundation(card))
                {
                    _lstMoves.Clear();
                    _lstMoves.Add(new FreeCellMove(card)
                    {
                        sourceType = SourceType.FreeCell,
                        targetType = SourceType.Foundation,
                        sourceIndex = i,
                        targetIndex = foundationIdx,
                        cardCount = 1,
                        mValue = 100000 // forced move — dominates everything
                    });
                    return true;
                }
            }
            // Check tableau bottoms
            for (int col = 0; col < _game.Tableau.Count; col++)
            {
                var column = _game.Tableau[col];
                if (column.Count == 0) continue;
                var card = column[^1];
                var foundationIdx = _game.CanMoveToAnyFoundation(card);
                if (foundationIdx >= 0 && _game.IsSafeToMoveToFoundation(card))
                {
                    _lstMoves.Clear();
                    _lstMoves.Add(new FreeCellMove(card)
                    {
                        sourceType = SourceType.Tableau,
                        targetType = SourceType.Foundation,
                        sourceIndex = col,
                        targetIndex = foundationIdx,
                        cardCount = 1,
                        mValue = 100000
                    });
                    return true;
                }
            }
            return safeMovesFound;
        }

        /// <summary>
        /// Penalizes T-to-T moves where the continuation cards (cards that would extend the
        /// sequence on the destination: rank-1, opposite color to the bottom of the moved sequence)
        /// are buried, especially in the destination column where they become harder to access.
        /// </summary>
        private int GetContinuationBlockedPenalty(Card botCard, int srcCol, int dstCol, int seqlen)
        {
            int contRank = (int)botCard.Rank - 1;
            if (contRank < 2) return 0; // Ace at bottom â€” nearly done, no meaningful continuation needed

            bool contIsRed = !botCard.IsRed;
            int penalty = 0;

            for (int col = 0; col < _game.Tableau.Count; col++)
            {
                var column = _game.Tableau[col];
                for (int i = 0; i < column.Count; i++)
                {
                    var card = column[i];
                    if ((int)card.Rank == contRank && card.IsRed == contIsRed)
                    {
                        int buryDepth = column.Count - 1 - i; // 0 = accessible bottom
                        if (col == dstCol && buryDepth > 0)
                        {
                            // Continuation card buried in destination â€” gets seqlen cards deeper after the move
                            penalty += (buryDepth + seqlen) * 5;
                        }
                        else if (col == srcCol)
                        {
                            // Continuation card in source â€” becomes more accessible after we remove seqlen cards
                            int postMoveBury = Math.Max(0, buryDepth - seqlen);
                            if (postMoveBury > 0)
                                penalty += postMoveBury * 2;
                        }
                        else if (buryDepth > 0)
                        {
                            penalty += buryDepth * 2;
                        }
                    }
                }
            }
            return penalty;
        }

        /// <summary>
        /// Chain detection for inter-column moves.
        /// </summary>
        private void BoostChainMoves(List<FreeCellMove> tableauMoves)
        {
            if (tableauMoves.Count < 2) return;

            int freeSlots = _game.EmptyFreeCellCount + _game.EmptyTableauCount;
            int baseBoost = Math.Max(10, 30 - freeSlots * 5);

            for (int xi = 0; xi < tableauMoves.Count; xi++)
            {
                var moveX = tableauMoves[xi];
                int targetCol = moveX.targetIndex;

                for (int yi = 0; yi < tableauMoves.Count; yi++)
                {
                    if (yi == xi) continue;
                    var moveY = tableauMoves[yi];
                    if (moveY.sourceIndex == targetCol)
                    {
                        int boost = baseBoost + moveX.cardCount * 5;
                        moveY.mValue += boost;
                        _solver._LoggerAction?.Invoke(() =>
                            $"Chain boost +{boost}: {moveY} should precede {moveX} (freeSlots={freeSlots})");
                    }
                }
            }
        }

        /// <summary>
        /// Split a sequence that's too long to move directly by routing part through an intermediate column.
        /// </summary>
        private void FindSplitSequenceMoves(int srcCol, int seqlen, int emptyFreeCells, int emptyColumns,
            Span<int> maxMovablePerCol, int tableauColCount, List<FreeCellMove> tableauMoves)
        {
            if (_allowOnlyTableauPositiveMoves) return;
            var column = _game.Tableau[srcCol];
            var topOfSeq = column[^seqlen];

            bool didCheckEmptyDstCol = false;
            for (int dstCol = 0; dstCol < tableauColCount; dstCol++)
            {
                if (dstCol == srcCol) continue;
                var dstColumn = _game.Tableau[dstCol];

                // Only try splitting when the full sequence can't move directly
                if (seqlen <= maxMovablePerCol[dstCol]) continue;

                // Full sequence must fit on dstCol placement-wise
                if (dstColumn.Count == 0)
                {
                    if (seqlen == column.Count) continue; // whole-column to empty is no-op
                    if (didCheckEmptyDstCol) continue;
                    didCheckEmptyDstCol = true;
                }
                else if (!_game.CanPlaceOnTableau(topOfSeq, dstColumn))
                {
                    continue;
                }

                bool foundSplit = false;
                for (int topCount = seqlen - 1; topCount >= 1 && !foundSplit; topCount--)
                {
                    int botCount = seqlen - topCount;
                    var splitCard = column[^botCount];

                    bool didCheckEmptyIntCol = false;
                    for (int intCol = 0; intCol < tableauColCount && !foundSplit; intCol++)
                    {
                        if (intCol == srcCol || intCol == dstCol) continue;
                        var intColumn = _game.Tableau[intCol];

                        if (intColumn.Count == 0)
                        {
                            if (didCheckEmptyIntCol) continue;
                            didCheckEmptyIntCol = true;
                        }

                        if (botCount > maxMovablePerCol[intCol]) continue;
                        if (intColumn.Count > 0 && !_game.CanPlaceOnTableau(splitCard, intColumn)) continue;

                        int adjEmptyColsMove2 = emptyColumns - (intColumn.Count == 0 ? 1 : 0);
                        int ecMove2 = (dstColumn.Count == 0) ? Math.Max(0, adjEmptyColsMove2 - 1) : adjEmptyColsMove2;
                        int maxMovableMove2 = (1 + emptyFreeCells) << Math.Max(0, ecMove2);
                        if (topCount > maxMovableMove2) continue;

                        int adjEmptyCols = emptyColumns;
                        if (intColumn.Count == 0) adjEmptyCols--;
                        if (dstColumn.Count == 0) adjEmptyCols--;
                        if (column.Count == seqlen) adjEmptyCols++;
                        adjEmptyCols = Math.Max(0, adjEmptyCols);
                        int maxMovableFinal = (1 + emptyFreeCells) << adjEmptyCols;
                        if (botCount > maxMovableFinal) continue;

                        var move1 = new FreeCellMove(splitCard)
                        {
                            sourceType = SourceType.Tableau,
                            targetType = SourceType.Tableau,
                            sourceIndex = srcCol,
                            targetIndex = intCol,
                            cardCount = botCount,
                            mValue = 50 + seqlen * 10
                        };
                        var move2 = new FreeCellMove(topOfSeq)
                        {
                            sourceType = SourceType.Tableau,
                            targetType = SourceType.Tableau,
                            sourceIndex = srcCol,
                            targetIndex = dstCol,
                            cardCount = topCount,
                            mValue = 50 + seqlen * 10
                        };
                        var move3 = new FreeCellMove(splitCard)
                        {
                            sourceType = SourceType.Tableau,
                            targetType = SourceType.Tableau,
                            sourceIndex = intCol,
                            targetIndex = dstCol,
                            cardCount = botCount,
                            mValue = 50 + seqlen * 10
                        };
                        move1.PendingSequenceMoves = new Queue<FreeCellMove>([move2, move3]);
                        tableauMoves.Add(move1);
                        _solver._LoggerAction?.Invoke(() =>
                            $"Split seq: col {srcCol} seqlen={seqlen} split {botCount}+{topCount} via col {intCol} to col {dstCol}");
                        foundSplit = true;
                        _solver._countSplitMoves++;
                    }
                }
            }
        }

        /// <summary>
        /// Sequence abutting: move a suffix of one column's bottom sequence onto another column's
        /// existing sequence, leaving a shorter, more mobile remainder.
        /// </summary>
        private void FindAbutSequenceMoves(int[] seqLens, Span<int> maxMovablePerCol,
            int tableauColCount, List<FreeCellMove> allTableauToTableauMoves)
        {
            if (_allowOnlyTableauPositiveMoves) return;

            for (int srcCol = 0; srcCol < tableauColCount; srcCol++)
            {
                var seqlen = seqLens[srcCol];
                if (seqlen < 2) continue;
                var column = _game.Tableau[srcCol];
                var lockCount = _lazyGetColumnLockCounts.Value[srcCol];
                var nonLockedCards = column.Count - lockCount;
                if (seqlen >= nonLockedCards) continue; // entire non-locked portion is the sequence — nothing above to unlock, abutting is a no-op
                var topOfSeq = column[^seqlen];
                var topRank = (int)topOfSeq.Rank;

                for (int dstCol = 0; dstCol < tableauColCount; dstCol++)
                {
                    if (dstCol == srcCol) continue;
                    var dstColumn = _game.Tableau[dstCol];
                    if (dstColumn.Count == 0) continue;
                    int dstSeqLen = seqLens[dstCol];
                    if (dstSeqLen < 1) continue;

                    var dstBotCard = dstColumn[^1];
                    int neededRank = (int)dstBotCard.Rank - 1;
                    if (neededRank < 1) continue;

                    int k = topRank - neededRank;
                    if (k < 1 || k >= seqlen) continue;

                    int moveCount = seqlen - k;
                    if (moveCount <= k) continue;
                    if (moveCount > nonLockedCards) continue;
                    var splitCard = column[^moveCount];

                    if (splitCard.IsRed == dstBotCard.IsRed) continue;

                    if (moveCount > maxMovablePerCol[dstCol]) continue;

                    int combinedLen = dstSeqLen + moveCount;
                    int remainderLen = k;
                    // Keep base score below the <50 gating threshold — let MoveValueDeltaIncremental
                    // in getMoves() differentiate truly beneficial abuts from neutral shuffles.
                    int mValue = 10 + combinedLen * 2 + moveCount * 2;
                    if (remainderLen == 1)
                        mValue += 10;

                    var move = new FreeCellMove(splitCard)
                    {
                        sourceType = SourceType.Tableau,
                        targetType = SourceType.Tableau,
                        sourceIndex = srcCol,
                        targetIndex = dstCol,
                        cardCount = moveCount,
                        mValue = mValue
                    };
                    if (AddNewMove(move))
                    {
                        allTableauToTableauMoves.Add(move);
                    }
                    _solver._LoggerAction?.Invoke(() =>
                        $"Abut seq: col {srcCol} seqlen={seqlen} -> move {moveCount} cards to col {dstCol} (dstSeqLen={dstSeqLen}), combined={combinedLen}, remainder={remainderLen}");
                    _solver._countAbutMoves++;
                }
            }
        }

        internal List<FreeCellMove> getMoves()
        {
            // Pruning: safe auto-foundation moves are always optimal — skip everything else
            if (!_allowOnlyTableauPositiveMoves && FindSafeAutoFoundationMoves())
            {
                return _lstMoves;
            }
            FindMoveAnyFreeCellToFoundationOrTableau();
            FindMoveAnyTableauToTableauOrFoundation();
            if (!_allowOnlyTableauPositiveMoves)
            {
                var numMovesFound = FindMoveAnyTableauToFreeCell();
            }
            if (!_allowOnlyTableauPositiveMoves && _lstMoves.Count == 0 && _solver._allowFoundationToTableau)
            {
                FindMoveAnyFoundationToTableau();
            }
            // Pruning: use incremental BValue delta to improve move ordering for non-forced moves
            if (_lstMoves.Count > 1)
            {
                for (int i = 0; i < _lstMoves.Count; i++)
                {
                    var move = _lstMoves[i];
                    var delta = _solver.MoveValueDeltaIncremental(move);
                    move.deltaBValue = delta;
                    move.mValue += delta * 10;
                }
            }
            _lstMoves.Sort((a, b) => b.mValue.CompareTo(a.mValue));
            return _lstMoves;
        }
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
