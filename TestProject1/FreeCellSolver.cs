using Client.Games.Cards.Services;
using System.Diagnostics;
/*
 Notes:
when a column has 1 card and there are empty freecells, move the 1 to the freecell, because an empty column is worth more
 */

namespace TestProject1
{
    public class FreeCellSolver
    {
        private FreeCellGameService _gameService; // current state of board including undo
        public FreeCellGameBase _game; // state of board as we manipulate it
        private List<FreeCellMove> _moveHistory = []; // so we don't repeat moves that we just did
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
        public bool _allowFoundationToTableau = true;
        private Action<Func<string>>? _LoggerAction; // avoids costly evaluation of logger messages when logging is disabled
        public int VisitedNodeCount => UseNumericHash ? _visitedStatesNumeric.Count : _visitedStates.Count;
        private bool _isEvaluatingSequenceClear = false; // recursion guard
        private List<FreeCellMove>? _pendingSequenceInitiation = null; // first moves from all discovered sequence-clears, consumed by FindMoves()

        public event Func<FreeCellMove, Task>? OnDoMove;
        public event Func<FreeCellMove, Task>? OnUndoMove;


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
                _visitedStatesNumeric.Add(_game.GetStateHashNumeric());
            }
            else
            {
                _visitedStates.Add(_game.GetStateHash());
            }
        }

        //public static async Task<FreeCellSolver> CreateAsync(FreeCellGameService freeCellGameService, Action<Func<string>>? loggerAction = null)
        //{
        //    var solver = new FreeCellSolver(freeCellGameService, loggerAction);
        //    return solver;
        //}

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
                // Optimization: numeric hash — no string alloc, cheaper Contains check
                var hash = _game.GetStateHashNumeric();
                wouldCauseCycle = _visitedStatesNumeric.Contains(hash);
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

                }
            }
            public void FindMoveAnyTableauToTableauOrFoundation()
            {
                // Optimization: hoist EmptyFreeCellCount/EmptyTableauCount out of loop — they're identical across all columns
                int tableauColCount = _game.Tableau.Count;
                int emptyFreeCells = _game.EmptyFreeCellCount;
                int emptyColumns = _game.EmptyTableauCount;
                Span<int> maxMovablePerCol = stackalloc int[tableauColCount];
                for (int c = 0; c < tableauColCount; c++)
                {
                    int ec = (_game.Tableau[c].Count == 0) ? Math.Max(0, emptyColumns - 1) : emptyColumns;
                    maxMovablePerCol[c] = (1 + emptyFreeCells) << ec;
                }

                // Optimization: reuse a single list across source column iterations to avoid per-column allocation
                var tableauMoves = new List<FreeCellMove>();
                var allTableauToTableauMoves = new List<FreeCellMove>();
                for (int srcCol = 0; srcCol < tableauColCount; srcCol++)
                {
                    var column = _game.Tableau[srcCol];
                    if (column.Count == 0) continue; // empty column, nothing to move
                    var seqlen = _game.GetBottomSequenceLength(srcCol);
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
                            tableauMoves.Add(new FreeCellMove(topCard)
                            {
                                sourceType = SourceType.Tableau,
                                targetType = SourceType.Tableau,
                                sourceIndex = srcCol,
                                targetIndex = dstCol,
                                cardCount = seqlen,
                                mValue = 50 + seqlen * 10 // arbitrary scoring that favors longer moves
                            });
                        }
                        else
                        {
                            /* Consider breaking up sequences when there are empty columns/freecells that would allow moving part of the sequence and creating new moves. 
                             * Example of a position where breaking up a sequence would be good: j
                             id 599526:
                                 Depth:57 CreatedNodes:189 VisitedNodes:88
                             FreeCells:  Q♠             Foundations:  5♣  2♠  4♥  4♦ BValue: 58
                                  8♦      K♣  K♥  K♠  K♦ 10♦
                                  7♠      Q♥  Q♣  Q♦  J♠  9♣
                                  6♥      J♣  J♥      9♦ 10♣
                                         10♥          5♥  8♠
                                          9♠          3♠  7♦
                                          8♥          J♦  6♣
                                          7♣         10♠  5♦
                                          6♦          9♥  4♠
                                          5♠          8♣    
                                                      7♥    
                                                      6♠    

                            move  Q♠ FreeCell[0]->Tableau[0] cards:1 mVal:80  from FreeCell to Tableau empty column: Yields  J♦ Tableau[6]->Tableau[0] cards:6 mVal:120 
                             J♦ Tableau[6]->Tableau[0] cards:6 mVal:80 
                             8♠ Tableau[7]->Tableau[0] cards:5 mVal:70 
                             Q♠ FreeCell[0]->Tableau[0] cards:1 mVal:50 

                             */
                        }
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
                BoostChainMoves(allTableauToTableauMoves);
            }
            public void FindMoveAnyFoundationToTableau()
            {
                /* fails with lots of empty columns: combinatorics explode. There are simple moves from Tableau to Foundations here that are skipped.
         FreeCells:  K♣          K♦ Foundations:  5♦  5♠  3♣  2♥ BValue: 53 
      K♥  K♠      Q♣         10♠  6♦
      Q♠  Q♦      J♦          9♥  Q♥
      J♥  J♣                  8♣  J♠
     10♣ 10♥                  7♥ 10♦
      9♦  9♣                  6♠  9♠
      8♠  8♥                  5♥  8♦
      7♦  7♣                  4♣  7♠
      6♣  6♥                  3♥    
          5♣                        
          4♥                        

                 */

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
                                    // todo: Check if once done, there are any moves that would increase the seq total.
                                    // for now, we'll add with mediocre score
                                    var move = new FreeCellMove(topCard)
                                    {
                                        sourceType = SourceType.Foundation,
                                        targetType = SourceType.Tableau,
                                        sourceIndex = i,
                                        targetIndex = iCol,      // <-- FIX: set targetIndex to destination column
                                        cardCount = 1,
                                        mValue = 50 // make this score really high: when doing so results in goodmove
                                    };
                                    var goodMove = MoveEffectOnBoard(move);
                                    if (goodMove != null) // only add the move if it results in a positive change to the board (e.g. creates new moves, increases BValue, etc.)
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
                move.ApplyMove(_game);
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
            public void FindMoveAnyTableauToFreeCell()
            {
                // now see if can move to free cell
                int nFreeCells = _game.EmptyFreeCellCount;
                if (nFreeCells == 0)
                {
                    return;
                }
                if (_maxmValueSoFar < 2) // don't move to freecell if we already have a move from tableau to foundation or to tableau
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
                            continue; // The column starts with KQJ...moving any cards from this column to a freecell is worthless points because it doesn't free up any locked cards, so we skip it when looking for moves from tableau to freecell.
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
                            for (int i = 0; i < column.Count - 1; i++) // skip last card (it's being moved to freecell)
                            {
                                if (_game.CanMoveToAnyFoundation(column[i]) >= 0)
                                {
                                    score += (i + 1); // i=0 (top, most buried) gets +1; closer to bottom gets more
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
                            move.PendingSequenceMoves = new Queue<FreeCellMove>([followUp]);
                        }
                        AddNewMove(move);
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
                                    // (net zero freecell cost — big boost)
                                    if (goodMove != null && goodMove.ApplyMoveFast(_game))
                                    {
                                        // topOfSeq is the last card moved to freecells (highest rank in the sequence)
                                        var topOfSeqCard = tempMoves[^1].CardMoved!;
                                        var colAfter = _game.Tableau[iCol];
                                        if (colAfter.Count == 0 || _game.CanPlaceOnTableau(topOfSeqCard, colAfter))
                                        {
                                            // Sequence can go back: create reverse moves (topOfSeq first, botOfSeq last)
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
                                    // Big boost when reversible (net zero freecell cost), smaller otherwise
                                    var boostValue = reverseMoves != null ? 200 + 10 * seqlen : 10 * seqlen;
                                    foreach (var tm in tempMoves)
                                    {
                                        tm.mValue = boostValue;
                                    }

                                    if (reverseMoves != null && megaboostCarrier != null)
                                    {
                                        // Chain this reversible megaboost onto the existing carrier's queue
                                        // (ALL tempMoves since carrier is for a different column)
                                        foreach (var tm in tempMoves)
                                            megaboostCarrier.PendingSequenceMoves!.Enqueue(tm);
                                        goodMove.mValue = boostValue;
                                        megaboostCarrier.PendingSequenceMoves.Enqueue(goodMove);
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
                                        // Build the pending queue: remaining clear moves + (if reversible) enabled move + reverse moves
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
                                        // Try to boost the existing freecell move for this column already in _lstMoves
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
                                        else
                                        {
                                            //// Fallback: no existing move in _lstMoves (e.g. rejected by cycle/undo check)
                                            //var firstMove = tempMoves[0];
                                            //if (queue.Count > 0)
                                            //{
                                            //    firstMove.PendingSequenceMoves = queue;
                                            //}
                                            //(_solver._pendingSequenceInitiation ??= []).Add(firstMove);
                                            //_solver._LoggerAction?.Invoke(() => $"Sequence-clear: col {iCol} seqlen={seqlen} -> fallback pending enables {goodMove}, reversible={reverseMoves != null}, queue: {queue.Count + 1} moves");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
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
            /// Chain detection for inter-column moves. When move X targets column B and move Y
            /// sources from column B, Y should happen first (to free space before X fills it).
            /// Boost scales with free cell scarcity since tight resources make ordering critical.
            /// Example: move 2 from A→B and move 2 from B→C both exist. B→C must go first so
            /// the cards it removes from B aren't buried under the cards A→B adds.
            /// </summary>
            private void BoostChainMoves(List<FreeCellMove> tableauMoves)
            {
                if (tableauMoves.Count < 2) return;

                int freeSlots = _game.EmptyFreeCellCount + _game.EmptyTableauCount;
                // More boost when fewer free slots: 0 slots → 30, 4+ slots → 10
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
                            // Chain: X moves TO targetCol, Y moves FROM targetCol
                            // Y should go first to free space → boost Y
                            int boost = baseBoost + moveX.cardCount * 5;
                            moveY.mValue += boost;
                            _solver._LoggerAction?.Invoke(() =>
                                $"Chain boost +{boost}: {moveY} should precede {moveX} (freeSlots={freeSlots})");
                        }
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
                    FindMoveAnyTableauToFreeCell();
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
                        move.mValue += delta * 10; // weight BValue improvement into scoring
                    }
                }
                // Optimization: in-place sort descending by mValue instead of LINQ OrderByDescending().ToList()
                _lstMoves.Sort((a, b) => b.mValue.CompareTo(a.mValue));
                return _lstMoves;
            }
        }
        public List<FreeCellMove> FindMoves()
        {
            // Continuation: if the last executed move has pending sequence moves, return only the next one
            // (continuation steps are mechanical — alternatives at the initiation node handle backtracking)
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
                            // Transfer remaining queue so the chain continues when this move is executed
                            if (lastMove.PendingSequenceMoves.Count > 0)
                            {
                                next.PendingSequenceMoves = lastMove.PendingSequenceMoves;
                            }
                            lastMove.PendingSequenceMoves = null;
                            return [next];
                        }
                        // Move is invalid — remaining moves depend on it, so abandon the sequence
                        lastMove.PendingSequenceMoves = null;
                        break;
                    }
                }
            }

            var helper = new FindMoveHelper(this);
            var moves = helper.getMoves();

            // Initiation: getMoves() may have discovered sequence-clear opportunities across multiple columns
            // Merge all discovered first moves into results alongside normal alternatives
            if (_pendingSequenceInitiation != null)
            {
                var seqFirstMoves = _pendingSequenceInitiation;
                _pendingSequenceInitiation = null;
                // Sort by original boosted score to preserve relative quality ordering
                seqFirstMoves.Sort((a, b) => b.mValue.CompareTo(a.mValue));
                var fallbackBase = moves.Count > 0 ? moves[^1].mValue : 0;
                foreach (var firstMove in seqFirstMoves)
                {
                    // Validate before adding (cycle detection + undo check)
                    if (IsMoveApplicable(firstMove) && !moveWouldJustUndoPriorMove(firstMove) && !MoveWouldCauseCycle(firstMove))
                    {
                        // Score below normal moves so they're tried as fallbacks by the backtracker
                        // when normal moves fail, rather than explored first (which can poison the visited-states set)
                        firstMove.mValue = --fallbackBase;
                        moves.Add(firstMove);
                    }
                }
            }

            return moves;
        }

        /// <summary>
        /// Lightweight validity check for queued moves whose board state may have changed
        /// due to backtracking. Prevents crashes in MoveWouldCauseCycle/ApplyMoveFast.
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
            // Validate tableau target placement (important for combined megaboost queues
            // where earlier moves may have changed the target column)
            if (move.targetType == SourceType.Tableau && move.CardMoved != null)
            {
                var targetCol = _game.Tableau[move.targetIndex];
                if (!_game.CanPlaceOnTableau(move.CardMoved, targetCol)) return false;
            }
            return true;
        }

        private bool moveWouldJustUndoPriorMove(FreeCellMove newMove)
        {
            if (_moveHistory.Count == 0) return false;
            var lastMove = _moveHistory[^1];
            // if the new move is the exact opposite of the last move, then it would just undo it
            if (newMove.sourceType == lastMove.targetType &&
                newMove.targetType == lastMove.sourceType &&
                newMove.sourceIndex == lastMove.targetIndex &&
                newMove.targetIndex == lastMove.sourceIndex &&
                //newMove.CardMoved == lastMove.CardMoved &&
                newMove.cardCount == lastMove.cardCount)
            {
                return true;
            }
            return false;
        }

        public int MoveValueDelta(FreeCellMove move, int startBValue)
        {
            // Optimization: use ApplyMoveFast to skip redundant validation
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
            // Foundation delta is trivially computed (each card in foundation contributes 2 to BValue)
            int foundationDelta = 0;
            if (move.sourceType == SourceType.Foundation) foundationDelta -= 2;
            if (move.targetType == SourceType.Foundation) foundationDelta += 2;

            // Snapshot BValue contributions of affected tableau columns before move
            int beforeTableau = 0;
            if (move.sourceType == SourceType.Tableau) beforeTableau += _game.GetColumnBValue(move.sourceIndex);
            if (move.targetType == SourceType.Tableau) beforeTableau += _game.GetColumnBValue(move.targetIndex);

            if (!move.ApplyMoveFast(_game))
            {
                throw new Exception($"Failed to apply {move} move for incremental score evaluation");
            }

            // Snapshot BValue contributions of affected tableau columns after move
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
                var indentation = _LoggerAction != null ? (doIndent ? new string(' ', currentNode.Depth) : string.Empty) : string.Empty;
                _LoggerAction?.Invoke(() => _game.dumpAllToLog($"Depth:{_game.MoveCount} CreatedNodes:{_countNodesCreated} VisitedNodes:{_countNodesVisited}", indentation));
                var moves = FindMoves();
                foreach (var move in moves)
                {
                    move.ParentMove = currentNode;
                    move.Depth = currentNode.Depth + 1;
                    _LoggerAction?.Invoke(() => indentation + move.ToString());
                }
                if (_countNodesVisited == 3208)
                {
                    "bpt".ToString();
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
                    // we want to backtrack the position to the last move that had a score > 1 (not moving to a freecell)
                    // and use the next best move.
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
                            // now find the first childmove that we haven't done yet and execute it
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
                                keepBacktracking = false; // don't need to go back further yet, we found an unexplored move at the current level, so we'll try that first before backtracking more
                            }
                        }
                        else
                        {
                            _LoggerAction?.Invoke(() => $"{indentation}no moves found backtracking all the way to rootnode");
                            break; // 
                        }
                    }
                }
                if (bestMove == null)
                {
                    throw new Exception($"Solver failed {_game.MoveCount} to find any moves, but game is not won. Visited {(UseNumericHash ? _visitedStatesNumeric.Count : _visitedStates.Count)} states. MaxDepth = {_maxDepth}");
                }
                if (OnDoMove != null) await OnDoMove.Invoke(bestMove);
                // Optimization: use ApplyMoveFast in the main solve loop (move already validated by getMoves)
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
                    _visitedStatesNumeric.Add(_game.GetStateHashNumeric());
                }
                else
                {
                    var hash = _game.GetStateHash();
                    if (hash == "F:_,QD,7C,KC|P:_,H2,C1,S3|T:2C6S7HKDJHQCJDTC|4S8C|5H5DTSADTH9S8H7S6D5S4H3C2D|6H3D|9H4CTDQS|JC8D3H9C8S7D|JS|QH6C9DKSKH5C4D")
                    {
                        "bpt".ToString();
                    }
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
            // we need to undo the move to backtrack the game state
            var didUnApply = currentNode.UnApplyMove(_game);
            if (!didUnApply)
            {
                throw new Exception($"Failed to unapply move during backtracking: {currentNode}");
            }
            if (OnUndoMove != null) await OnUndoMove.Invoke(currentNode);
            // remove last entry from moveHistory
            if (_moveHistory.Count > 0)
            {
                _moveHistory.RemoveAt(_moveHistory.Count - 1);
            }

            return currentNode.ParentMove;
        }
    }
}
