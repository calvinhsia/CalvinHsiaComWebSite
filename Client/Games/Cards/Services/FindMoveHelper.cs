using Client.Games.Cards.Models;
/*
 Notes:
when a column has 1 card and there are empty freecells, move the 1 to the freecell, because an empty column is worth more

optimizaation near the end when # empty columns is high, we can skip some of the resequencing moves because the normal tableau to tableau moves already give us a lot of mobility, 
so we can just skip resequencing when there are 3 or more empty columns or something like that. 
We can also cap the number of empty columns we use for resequencing to 2 or something, 
to avoid combinatorial bloat from trying to resequence with 5 empty columns when the board is nearly solved and there are tons of normal moves available.
 
All 5 solver tests pass with 0 failures. Here's a summary of the changes:
Threshold: 3 empty columns (emptyColumns < 3 / EmptyTableauCount < 3)
At 3 empty columns, maxMovable = (1 + emptyFreeCells) << 3 = 8–40 cards depending on free cells. That's enough mobility that these expensive heuristics add negligible value but significant computation cost.
5 heuristics gated:
Heuristic	Location	Cost
Insert-under-sequence	FindMoveAnyFreeCellToFoundationOrTableau()	O(freeCells × cols × seqLen) + temp allocation
Split sequences	FindMoveAnyTableauToTableauOrFoundation()	O(cols² × seqLen × intCols)
Abut sequences	FindMoveAnyTableauToTableauOrFoundation()	O(cols²)
Order-changing resequence	FindAnyMoveOrderChanging(int, int, int[])	O(cols × cards) + Phase 1/2 generation
Sequence-clear mega	FindMoveAnyTableauToFreeCell()	Recursive getMoves() calls per column
Normal tableau-to-tableau moves, foundation moves, and basic freecell moves remain fully active at all times — they're cheap and always needed. */

namespace Client.Games.Cards.Services;

public partial class FreeCellSolver
{
    /// <summary>
    /// Given a board position, find all "reasonable" moves to consider for the next step. These are moves that
    /// do not immediately undo a previous move, do not swap equivalent cards, and do not cause cycles.
    /// </summary>
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
                if (!_solver.moveWouldSwapEquivalentCard(move))
                {
                    if (!_solver.MoveWouldCauseCycle(move))
                    {
                        if (move.mValue > _maxmValueSoFar)
                        {
                            _maxmValueSoFar = move.mValue;
                        }
                        if (move.CardMoved?.Rank == Rank.Ace &&move.sourceType == FreeCellArea.Tableau && move.targetType == FreeCellArea.FreeCell)
                        {
                            _solver._countGenPurpose++;
                        }
                        _lstMoves.Add(move);
                        didit = true;
                    }
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
                            sourceType = FreeCellArea.FreeCell,
                            targetType = FreeCellArea.Foundation,
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
                                sourceType = FreeCellArea.FreeCell,
                                targetType = FreeCellArea.Tableau,
                                sourceIndex = i,
                                targetIndex = dstCol,
                                cardCount = 1,
                                mValue = 80
                            };
                        }
                    }
                    else if (_game.CanMoveFreeCellToTableau(i, dstCol))
                    {
                        // Favor destinations with longer bottom sequences — extending a longer run is more valuable
                        var dstSeqLen = _game.GetBottomSequenceLength(dstCol);
                        AddNewMove(new FreeCellMove(freecellCard)
                        {
                            sourceType = FreeCellArea.FreeCell,
                            targetType = FreeCellArea.Tableau,
                            sourceIndex = i,
                            targetIndex = dstCol,
                            cardCount = 1,
                            mValue = 100 + dstSeqLen * 5
                        });
                        canMoveToNonEmptyTableau = true;
                    }
                }
                // Skip the empty-column move when the card already fits on a non-empty column —
                // placing on a non-empty column preserves the valuable empty column and avoids
                // an extra move later. Only evaluate the empty-column route when no non-empty
                // destination exists (the card's only tableau option is an empty column).
                if (deferredEmptyColMove != null && !canMoveToNonEmptyTableau && !_allowOnlyTableauPositiveMoves)
                {
                    AddNewMove(deferredEmptyColMove);
                }

                // Insert-under-sequence: move a column's bottom sequence to temp storage,
                // place this freecell card underneath, then restore the sequence on top.
                // Extends the sorted run by 1 and frees a free cell.
                // Skip when 3+ empty columns: high mobility makes this expensive heuristic unnecessary.
                if (!_allowOnlyTableauPositiveMoves && _game.EmptyTableauCount < 3)
                {
                    FindInsertUnderSeqMoves(i, freecellCard);
                }

            }

            // Heuristic: detect sequences among freecell cards.
            // When multiple freecell cards form a descending alternating-color chain,
            // placing the head card on a tableau column enables the rest to follow,
            // clearing multiple free cells in one logical move sequence.
            if (!_allowOnlyTableauPositiveMoves)
            {
                FindFreeCellSeqMoves();
            }
        }
        private void FindFreeCellSeqMoves()
        {
            var fcCards = new List<(int fcIndex, Card card)>();
            for (int fi = 0; fi < _game.FreeCells.Count; fi++)
            {
                if (_game.FreeCells[fi] != null)
                    fcCards.Add((fi, _game.FreeCells[fi]!));
            }

            if (fcCards.Count >= 2)
            {
                // Build successor map: for each freecell card, find another freecell card
                // that can go on top of it (rank-1, opposite color)
                var successorFcIndex = new Dictionary<int, int>(); // fcIndex -> successor's fcIndex
                var hasPredecessor = new HashSet<int>();

                for (int a = 0; a < fcCards.Count; a++)
                {
                    var (idxA, cardA) = fcCards[a];
                    for (int b = 0; b < fcCards.Count; b++)
                    {
                        if (a == b) continue;
                        var (idxB, cardB) = fcCards[b];
                        // cardB goes on top of cardA: rank-1, opposite color
                        if ((int)cardB.Rank == (int)cardA.Rank - 1 && cardB.IsRed != cardA.IsRed)
                        {
                            successorFcIndex[idxA] = idxB;
                            hasPredecessor.Add(idxB);
                            break;
                        }
                    }
                }

                // Find chain heads: freecell cards with a successor but no predecessor
                foreach (var (fcIndex, card) in fcCards)
                {
                    if (hasPredecessor.Contains(fcIndex)) continue;
                    if (!successorFcIndex.ContainsKey(fcIndex)) continue;

                    // Build the chain starting from this head
                    var chain = new List<(int fcIndex, Card card)>();
                    int cur = fcIndex;
                    while (true)
                    {
                        chain.Add((cur, _game.FreeCells[cur]!));
                        if (!successorFcIndex.TryGetValue(cur, out var next)) break;
                        cur = next;
                    }

                    if (chain.Count < 2) continue;

                    var headCard = chain[0].card;
                    int chainMVal = 80 + chain.Count * 30;
                    FreeCellMove? deferredEmptyColMove = null;
                    bool foundNonEmptyDest = false;

                    for (int dstCol = 0; dstCol < _game.Tableau.Count; dstCol++)
                    {
                        var colDest = _game.Tableau[dstCol];
                        if (colDest.Count == 0)
                        {
                            if (deferredEmptyColMove == null)
                            {
                                var queue = new Queue<FreeCellMove>();
                                for (int c = 1; c < chain.Count; c++)
                                {
                                    queue.Enqueue(new FreeCellMove(chain[c].card)
                                    {
                                        sourceType = FreeCellArea.FreeCell,
                                        targetType = FreeCellArea.Tableau,
                                        sourceIndex = chain[c].fcIndex,
                                        targetIndex = dstCol,
                                        cardCount = 1,
                                        mValue = chainMVal
                                    });
                                }
                                deferredEmptyColMove = new FreeCellMove(headCard)
                                {
                                    sourceType = FreeCellArea.FreeCell,
                                    targetType = FreeCellArea.Tableau,
                                    sourceIndex = chain[0].fcIndex,
                                    targetIndex = dstCol,
                                    cardCount = 1,
                                    mValue = chainMVal,
                                    PendingSequenceMoves = queue
                                };
                            }
                            continue;
                        }
                        if (_game.CanPlaceOnTableau(headCard, colDest))
                        {
                            var queue = new Queue<FreeCellMove>();
                            for (int c = 1; c < chain.Count; c++)
                            {
                                queue.Enqueue(new FreeCellMove(chain[c].card)
                                {
                                    sourceType = FreeCellArea.FreeCell,
                                    targetType = FreeCellArea.Tableau,
                                    sourceIndex = chain[c].fcIndex,
                                    targetIndex = dstCol,
                                    cardCount = 1,
                                    mValue = chainMVal
                                });
                            }
                            AddNewMove(new FreeCellMove(headCard)
                            {
                                sourceType = FreeCellArea.FreeCell,
                                targetType = FreeCellArea.Tableau,
                                sourceIndex = chain[0].fcIndex,
                                targetIndex = dstCol,
                                cardCount = 1,
                                mValue = chainMVal,
                                PendingSequenceMoves = queue
                            });
                            foundNonEmptyDest = true;
                        }
                    }

                    // Prefer non-empty columns to preserve valuable empty columns;
                    // only use empty-column route when no non-empty destination exists.
                    if (deferredEmptyColMove != null && !foundNonEmptyDest)
                    {
                        AddNewMove(deferredEmptyColMove);
                    }

                    if (foundNonEmptyDest || deferredEmptyColMove != null)
                    {
                        _solver._countFreeCellSeqMoves++;
                        _solver._LoggerAction?.Invoke(() =>
                            $"FreeCellSeq: {chain.Count} cards ({string.Join(",", chain.Select(c => c.card))}) chained placement");
                    }
                }
            }
        }
        private void FindInsertUnderSeqMoves(int freeCellIndex, Card freecellCard)
        {
            // Need enough temp slots (free cells + empty columns) to hold the sequence
            int availableTemp = _game.EmptyFreeCellCount + _game.EmptyTableauCount;
            if (availableTemp <= 0) return;

            for (var dstCol = 0; dstCol < _game.Tableau.Count; dstCol++)
            {
                var colDest = _game.Tableau[dstCol];
                if (colDest.Count < 2) continue; // empty or if single card, just do normal freecell-to-tableau move instead of insert-under-sequence
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
                var cardLocations = new Dictionary<Card, (FreeCellArea type, int index)>(seqLen);
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
                        cardLocations[card] = (FreeCellArea.FreeCell, fcSlot);
                        allMoves.Add(new FreeCellMove(card)
                        {
                            sourceType = FreeCellArea.Tableau,
                            targetType = FreeCellArea.FreeCell,
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
                        cardLocations[card] = (FreeCellArea.Tableau, emptyCol);
                        allMoves.Add(new FreeCellMove(card)
                        {
                            sourceType = FreeCellArea.Tableau,
                            targetType = FreeCellArea.Tableau,
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
                    sourceType = FreeCellArea.FreeCell,
                    targetType = FreeCellArea.Tableau,
                    sourceIndex = freeCellIndex,
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
                        targetType = FreeCellArea.Tableau,
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
                _solver._countInsertUnderMoves++;
                _solver._LoggerAction?.Invoke(() =>
                    $"InsertUnderSeq: freecell[{freeCellIndex}] {freecellCard} -> col {dstCol} under seqLen={seqLen}, {insertQueue.Count} queued moves");
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
                            sourceType = FreeCellArea.Tableau,
                            targetType = FreeCellArea.Foundation,
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
                // let's see if there's a move entirely within this column, just by reordering the bottom cards. E.G. if the column ends with 7685 and there are 4 free cells we can reorder them in place. This is a very high value move because it's free.
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

                    /*This causes 2 additional failures 
Game	TimeMs	Moves	Nodes	Visit	BTrack	Uber	Fnd=>Tabl	Mega	Split	Abut	Neut	Order	InsertUnder	BurFndRdy	Stat
617	3,780	0	249,756	240,017	232,090	8	11,348	0	38	3,440	409	70	26	18	Solver failed 6 to find any moves; but game is not won. Visited 204423 states. MaxDepth = 3136
850	8,443	0	510,564	420,038	368,398	14	1,319	2	111	6,953	2,944	52	2,810	4	Solver failed 11 to find any moves; but game is not won. Visited 338134 states. MaxDepth = 13468
                     */
                    //if (seqlen > maxMovablePerCol[dstCol])
                    //{
                    //    // Full sequence too large — try moving a smaller sub-sequence
                    //    for (int tryLen = maxMovablePerCol[dstCol]; tryLen >= 1; tryLen--)
                    //    {
                    //        if (_game.CanMoveTableauToTableau(srcCol, dstCol, tryLen))
                    //        {
                    //            var moveTopCard = column[^tryLen];
                    //            var mVal = 50 + tryLen * 10;
                    //            var penalty = Math.Min(GetContinuationBlockedPenalty(botCard, srcCol, dstCol, tryLen), tryLen * 10);
                    //            mVal -= penalty;
                    //            mVal -= 20; // penalize breaking a sorted sequence
                    //            tableauMoves.Add(new FreeCellMove(moveTopCard)
                    //            {
                    //                sourceType = SourceType.Tableau,
                    //                targetType = SourceType.Tableau,
                    //                sourceIndex = srcCol,
                    //                targetIndex = dstCol,
                    //                cardCount = tryLen,
                    //                mValue = mVal
                    //            });
                    //            break; // found a valid sub-sequence move for this destination
                    //        }
                    //    }
                    //    continue;
                    //}
                    if (_game.CanMoveTableauToTableau(srcCol, dstCol, seqlen))
                    {
                        var mVal = 80 + seqlen * 10; // arbitrary scoring that favors longer moves
                        // Penalize when continuation cards (rank-1, opposite color) are buried, especially in the destination column
                        var penalty = Math.Min(GetContinuationBlockedPenalty(botCard, srcCol, dstCol, seqlen), seqlen * 10);
                        mVal -= penalty;
                        tableauMoves.Add(new FreeCellMove(topCard)
                        {
                            sourceType = FreeCellArea.Tableau,
                            targetType = FreeCellArea.Tableau,
                            sourceIndex = srcCol,
                            targetIndex = dstCol,
                            cardCount = seqlen,
                            mValue = mVal
                        });
                    }
                    else if (seqlen > 1 && numCardsInDestCol > 0)
                    {
                        // Full sequence can't be placed — try moving a sub-sequence (e.g., 10-9 from J-10-9 onto Q-J).
                        // This leaves the top card(s) behind, which may be more mobile as a lone card.
                        for (int tryLen = seqlen - 1; tryLen >= 1; tryLen--)
                        {
                            if (tryLen > maxMovablePerCol[dstCol])
                                continue;
                            if (_game.CanMoveTableauToTableau(srcCol, dstCol, tryLen))
                            {
                                var moveTopCard = column[^tryLen];
                                var mVal = 80 + tryLen * 10;
                                var subBotCard = column[^1];
                                var penalty = Math.Min(GetContinuationBlockedPenalty(subBotCard, srcCol, dstCol, tryLen), tryLen * 10);
                                mVal -= penalty;
                                mVal -= 15; // penalize splitting a sorted sequence
                                tableauMoves.Add(new FreeCellMove(moveTopCard)
                                {
                                    sourceType = FreeCellArea.Tableau,
                                    targetType = FreeCellArea.Tableau,
                                    sourceIndex = srcCol,
                                    targetIndex = dstCol,
                                    cardCount = tryLen,
                                    mValue = mVal
                                });
                                break; // use the largest fitting sub-sequence for this destination
                            }
                        }
                    }
                }
                // Split sequences: when seqlen > maxMovable for some destination, try splitting via intermediate column
                // Skip when 3+ empty columns: maxMovable is already large enough to move most sequences directly.
                if (seqlen > 3 && emptyColumns < 3)
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
            if (_maxmValueSoFar < 100 && emptyColumns < 3)
            {
                FindAbutSequenceMoves(seqLens, maxMovablePerCol, tableauColCount, allTableauToTableauMoves);
            }
            if (emptyColumns < 2)
            {
                FindAnyMoveOrderChanging(emptyFreeCells, emptyColumns, seqLens);
            }
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
                int alreadyAccessibleFoundation = 0;
                int seqStart = column.Count - existingSeqLen; // first index of existing bottom sequence
                for (int i = 0; i < cardsToResequence; i++)
                {
                    var card = column[startIdx + i];
                    if (_game.CanMoveToAnyFoundation(card) >= 0)
                    {
                        foundationCount++;
                        if (startIdx + i >= seqStart)
                            alreadyAccessibleFoundation++;
                    }
                    else
                        remaining.Add(card);
                }

                // Effective benefit: only foundation cards that are NOT already accessible count
                int effectiveFoundationCount = foundationCount - alreadyAccessibleFoundation;

                // Skip if no improvement: no new foundation cards exposed AND remaining won't extend the sequence
                if (effectiveFoundationCount == 0 && cardsToResequence <= existingSeqLen) continue;

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

                    // With no new foundation benefit, remaining must actually extend the existing sequence
                    if (effectiveFoundationCount == 0 && remaining.Count <= existingSeqLen) continue;
                }

                // In probe mode (MoveEffectOnBoard), emit a lightweight marker move so the caller
                // knows a resequencing opportunity exists, but skip the expensive Phase 1/Phase 2
                // multi-step move generation. This replaces the old blanket
                // `_allowOnlyTableauPositiveMoves` early-return that blocked discovery entirely.
                if (_allowOnlyTableauPositiveMoves)
                {
                    // Skip if bottom card is foundation-ready — it should go to foundation, not freecell
                    if (_game.CanMoveToAnyFoundation(column[^1]) >= 0) continue;
                    int fcTarget = -1;
                    for (int f = 0; f < _game.FreeCells.Count; f++)
                    {
                        if (_game.FreeCells[f] == null) { fcTarget = f; break; }
                    }
                    if (fcTarget < 0) continue;
                    int probeVal = 200 + foundationCount * 100 + Math.Max(0, remaining.Count - existingSeqLen) * 20;
                    AddNewMove(new FreeCellMove(column[^1])
                    {
                        sourceType = FreeCellArea.Tableau,
                        targetType = FreeCellArea.FreeCell,
                        sourceIndex = iCol,
                        targetIndex = fcTarget,
                        cardCount = 1,
                        mValue = probeVal
                    });
                    continue;
                }

                // Generate Phase 1: move all window cards to temp storage (from bottom up).
                // Use free cells first (less valuable than empty columns), overflow to empty columns.
                var allMoves = new List<FreeCellMove>(cardsToResequence + remaining.Count);
                var cardLocations = new Dictionary<Card, (FreeCellArea type, int index)>(cardsToResequence);
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
                        cardLocations[card] = (FreeCellArea.FreeCell, fcIdx);
                        allMoves.Add(new FreeCellMove(card)
                        {
                            sourceType = FreeCellArea.Tableau,
                            targetType = FreeCellArea.FreeCell,
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
                        cardLocations[card] = (FreeCellArea.Tableau, emptyColIdx);
                        allMoves.Add(new FreeCellMove(card)
                        {
                            sourceType = FreeCellArea.Tableau,
                            targetType = FreeCellArea.Tableau,
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
                        targetType = FreeCellArea.Tableau,
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
                        // Anti-cycle: skip cards that have already been moved Foundation→Tableau
                        // multiple times in the current search path — these are semantic cycles
                        // where the card bounces F→T→F→T without real progress.
                        _solver._foundationToTableauCardCount.TryGetValue(topCard, out var ftCount);
                        if (ftCount >= 2)
                        {
                            _solver._LoggerAction?.Invoke(() => $"Skipping F→T for {topCard}: already moved F→T {ftCount} times in current path");
                            continue;
                        }
                        FreeCellMove? deferredEmptyColMove = null;
                        FreeCellMove? deferredEmptyColGoodMove = null;
                        var canMoveToNonEmptyTableau = false;
                        for (int iCol = 0; iCol < _game.Tableau.Count; iCol++) // see if the foundation card can be added to a column even if empty
                        {
                            var column = _game.Tableau[iCol];
                            if (_game.CanPlaceOnTableau(topCard, column))
                            {
                                if (column.Count == 0) // empty column?
                                {
                                    if (deferredEmptyColMove == null) // only keep the first empty-column candidate (all empty cols are equivalent)
                                    {
                                        deferredEmptyColMove = new FreeCellMove(topCard)
                                        {
                                            sourceType = FreeCellArea.Foundation,
                                            targetType = FreeCellArea.Tableau,
                                            sourceIndex = i,
                                            targetIndex = iCol,
                                            cardCount = 1,
                                            mValue = 50
                                        };
                                        deferredEmptyColGoodMove = MoveEffectOnBoard(deferredEmptyColMove);
                                    }
                                    continue;
                                }
                                // just because we can place it from Foundation to tableau, doesn't mean we want to. Only do so if it would increase the seq total.
                                var move = new FreeCellMove(topCard)
                                {
                                    sourceType = FreeCellArea.Foundation,
                                    targetType = FreeCellArea.Tableau,
                                    sourceIndex = i,
                                    targetIndex = iCol,
                                    cardCount = 1,
                                    mValue = 50
                                };
                                var goodMove = MoveEffectOnBoard(move);
                                if (goodMove != null) // only add the move if it results in a positive change to the board
                                {
                                    _solver._LoggerAction?.Invoke(() => $"move {move} from Foundation to Tableau: Yields {goodMove}");
                                    move.PendingSequenceMoves = new Queue<FreeCellMove>([goodMove]);
                                    //move.mValue += goodMove.mValue;
                                    var didAdd = AddNewMove(move);
                                    canMoveToNonEmptyTableau = true;
                                }
                            }
                        }
                        // Only try the empty-column route when no non-empty destination was found
                        if (deferredEmptyColMove != null && !canMoveToNonEmptyTableau && deferredEmptyColGoodMove != null)
                        {
                            _solver._LoggerAction?.Invoke(() => $"move {deferredEmptyColMove} from Foundation to Tableau empty column: Yields {deferredEmptyColGoodMove}");
                            deferredEmptyColMove.PendingSequenceMoves = new Queue<FreeCellMove>([deferredEmptyColGoodMove]);
                            AddNewMove(deferredEmptyColMove);
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
            var movedCard = move.CardMoved;
            var result = moves.FirstOrDefault(m =>
            {
                if (!((m.targetType == FreeCellArea.Tableau && m.targetIndex == col) ||
                      (m.sourceType == FreeCellArea.Tableau && m.sourceIndex == col)))
                    return false;
                // Reject equivalent-card swaps: if the follow-up places a same-rank same-color
                // card back onto the column we just moved from, it's a no-op swap.
                if (movedCard != null && m.CardMoved != null &&
                    m.CardMoved.Rank == movedCard.Rank &&
                    m.CardMoved.IsRed == movedCard.IsRed &&
                    m.CardMoved.Suit != movedCard.Suit &&
                    m.targetType == move.sourceType &&
                    m.targetIndex == move.sourceIndex)
                    return false;
                return true;
            });
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
            if (_maxmValueSoFar <= 70) // don't move to freecell if we already have a good foundation or tableau move
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
                        sourceType = FreeCellArea.Tableau,
                        targetType = FreeCellArea.FreeCell,
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
                        if (followUp.sourceType == FreeCellArea.Tableau && followUp.sourceIndex == iCol)
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
                        else if (followUp.targetType == FreeCellArea.Tableau && followUp.targetIndex == iCol)
                        {
                            var newBottom = followUp.sourceType == FreeCellArea.Tableau
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
                                sourceType = FreeCellArea.FreeCell,
                                targetType = FreeCellArea.Tableau,
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
                    //if (move.mValue >= 50)
                    {
                        AddNewMove(move);
                        numMovesFound++;
                    }
                }
            }
            if (_maxmValueSoFar < 3 && !_solver._isEvaluatingSequenceClear
                && _solver._pendingSequenceInitiation == null
                && _game.EmptyTableauCount < 3) // still no good move — see if clearing a bottom sequence into freecells enables a positive follow-up; skip when 3+ empty columns (high mobility)
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
                                    sourceType = FreeCellArea.Tableau,
                                    targetType = FreeCellArea.FreeCell,
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
                                        (m.sourceType == FreeCellArea.Tableau && m.sourceIndex == iCol) ||
                                        (m.targetType == FreeCellArea.Tableau && m.targetIndex == iCol));
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
                                                sourceType = FreeCellArea.FreeCell,
                                                targetType = FreeCellArea.Tableau,
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
                                        m.sourceType == FreeCellArea.Tableau &&
                                        m.targetType == FreeCellArea.FreeCell &&
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
            // Targeted pass: when good moves already exist (_maxmValueSoFar >= 2), still consider
            // T→FC for columns with 2+ buried foundation-ready cards — these high-value columns
            // should not be gated out just because a routine T-T move was found.
            // Gating uses immediate readiness (CanMoveToAnyFoundation) to avoid qualifying too
            // many columns. Scoring uses chain-foundation-ready counting which also credits cards
            // that become ready through chaining (e.g., 3♥ after 2♥ goes).
            // Low base mValue ensures these moves are explored AFTER normal T-T/foundation moves,
            // preventing unproductive search paths (e.g., game 71 regression with high mValue).
            if (nFreeCells > 0 && _maxmValueSoFar >= 2)
            {
                int availableTemp = (nFreeCells - 1) + _game.EmptyTableauCount;
                for (int iCol = 0; iCol < _game.Tableau.Count; iCol++)
                {
                    var column = _game.Tableau[iCol];
                    if (column.Count < 3) continue; // need at least 2 buried cards + bottom card
                    if (_lazyGetColumnLockCounts.Value[iCol] == column.Count) continue;
                    // Skip if we already generated a T→FC move for this column
                    if (_lstMoves.Any(m => m.sourceType == FreeCellArea.Tableau && m.targetType == FreeCellArea.FreeCell && m.sourceIndex == iCol))
                        continue;
                    // Gate: require at least 1 immediately-ready card AND 2+ chain-foundation-ready cards.
                    // Pure immediate gating (>= 2) is too strict: e.g. [2♣, 3♣, Q♣] with A♣ on foundation
                    // has only 1 immediately ready (2♣), but 2 chain-ready (2♣ then 3♣).
                    // When only 1 immediately ready, require that most buried cards ARE chain-ready
                    // (trueBlockers <= 1). This avoids qualifying deep columns where chain-ready cards
                    // are buried under many non-productive cards.
                    int immediateReadyCount = 0;
                    int blockers = 0;
                    for (int qi = column.Count - 2; qi >= 0 && blockers <= availableTemp; qi--)
                    {
                        if (_game.CanMoveToAnyFoundation(column[qi]) >= 0)
                            immediateReadyCount++;
                        else
                            blockers++;
                    }
                    if (immediateReadyCount < 1) continue; // need at least 1 to start a chain
                    int chainReadyCount = CountChainFoundationReady(column);
                    if (chainReadyCount < 2) continue; // need 2+ total chain-ready
                    if (immediateReadyCount < 2)
                    {
                        // Count truly non-productive buried cards (not chain-ready at all)
                        int trueBlockers = (column.Count - 1) - chainReadyCount;
                        if (trueBlockers > 1) continue;
                    }
                    if (chainReadyCount > availableTemp + 2) continue; // too many to realistically execute
                    // Low base score: explore after normal T-T/foundation moves.
                    // Only follow-up bonuses (concrete enabling moves) boost priority.
                    var score = 1;
                    var seqLen = _game.GetBottomSequenceLength(iCol);
                    if (seqLen > 1) score -= seqLen;
                    var move = new FreeCellMove(column[^1])
                    {
                        sourceType = FreeCellArea.Tableau,
                        targetType = FreeCellArea.FreeCell,
                        sourceIndex = iCol,
                        targetIndex = _game.FindAnyFreeCell(),
                        cardCount = 1,
                        mValue = score
                    };
                    _solver._countBuriedFndReady++;
                    // Check if moving to freecell enables a positive follow-up in the source column
                    var followUp = MoveEffectOnBoard(move, iCol);
                    if (followUp != null)
                    {
                        move.mValue += followUp.mValue;
                        move.PendingSequenceMoves = new Queue<FreeCellMove>([followUp]);
                    }
                    AddNewMove(move);
                    numMovesFound++;
                }
            }
            return numMovesFound;
        }

        /// <summary>
        /// Count how many buried cards in a column can chain to foundation.
        /// Simulates foundation state: iteratively marks cards whose rank == simulated top + 1
        /// for their suit, regardless of order in the column. Excludes the bottom card (index ^1).
        /// </summary>
        private int CountChainFoundationReady(List<Card> column)
        {
            // Copy current foundation top ranks (per suit)
            Span<int> simTopRank = stackalloc int[4];
            for (int s = 0; s < 4; s++)
                simTopRank[s] = _game.GetFoundationTopRank((Suit)s);

            int count = 0;
            bool changed = true;
            Span<bool> marked = stackalloc bool[column.Count];
            while (changed)
            {
                changed = false;
                for (int i = 0; i < column.Count - 1; i++) // exclude bottom card
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

        /// <summary>
        /// Score chain-foundation-ready cards in a column for the targeted pass.
        /// Same chain simulation as CountChainFoundationReady, but returns a score
        /// with 10 base per chain-ready card + position bonus (deeper = higher bonus).
        /// </summary>
        private int ScoreChainFoundationReady(List<Card> column)
        {
            Span<int> simTopRank = stackalloc int[4];
            for (int s = 0; s < 4; s++)
                simTopRank[s] = _game.GetFoundationTopRank((Suit)s);

            int score = 0;
            bool changed = true;
            Span<bool> marked = stackalloc bool[column.Count];
            while (changed)
            {
                changed = false;
                for (int i = 0; i < column.Count - 1; i++)
                {
                    if (marked[i]) continue;
                    var card = column[i];
                    int suitIdx = (int)card.Suit;
                    if ((int)card.Rank == simTopRank[suitIdx] + 1)
                    {
                        marked[i] = true;
                        simTopRank[suitIdx] = (int)card.Rank;
                        score += 10 + (i + 1); // 10 base per chain-ready card + position bonus
                        changed = true;
                    }
                }
            }
            return score;
        }

        /// <summary>
        /// Pruning: Check for safe auto-foundation moves
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
                        sourceType = FreeCellArea.FreeCell,
                        targetType = FreeCellArea.Foundation,
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
                        sourceType = FreeCellArea.Tableau,
                        targetType = FreeCellArea.Foundation,
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
            if (contRank < 2) return 0; // Ace at bottom — nearly done, no meaningful continuation needed

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
                            // Continuation card buried in destination — gets seqlen cards deeper after the move
                            penalty += (buryDepth + seqlen) * 5;
                        }
                        else if (col == srcCol)
                        {
                            // Continuation card in source — becomes more accessible after we remove seqlen cards
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
                            sourceType = FreeCellArea.Tableau,
                            targetType = FreeCellArea.Tableau,
                            sourceIndex = srcCol,
                            targetIndex = intCol,
                            cardCount = botCount,
                            mValue = 80 + seqlen * 10
                        };
                        var move2 = new FreeCellMove(topOfSeq)
                        {
                            sourceType = FreeCellArea.Tableau,
                            targetType = FreeCellArea.Tableau,
                            sourceIndex = srcCol,
                            targetIndex = dstCol,
                            cardCount = topCount,
                            mValue = 80 + seqlen * 10
                        };
                        var move3 = new FreeCellMove(splitCard)
                        {
                            sourceType = FreeCellArea.Tableau,
                            targetType = FreeCellArea.Tableau,
                            sourceIndex = intCol,
                            targetIndex = dstCol,
                            cardCount = botCount,
                            mValue = 80 + seqlen * 10
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
                //                 if (seqlen >= nonLockedCards) continue; // entire non-locked portion is the sequence — nothing above to unlock, abutting is a no-op

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
                        sourceType = FreeCellArea.Tableau,
                        targetType = FreeCellArea.Tableau,
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
            if (!_allowOnlyTableauPositiveMoves && _solver._allowFoundationToTableau)
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
            // Column-clearing mode: boost moves that evacuate the target column
            if (_solver._targetClearColumn is int targetCol)
            {
                foreach (var move in _lstMoves)
                {
                    if (move.sourceType == FreeCellArea.Tableau && move.sourceIndex == targetCol)
                    {
                        move.mValue += 500;
                    }
                }
            }
            _lstMoves.Sort((a, b) => b.mValue.CompareTo(a.mValue));
            return _lstMoves;
        }
    }
}
