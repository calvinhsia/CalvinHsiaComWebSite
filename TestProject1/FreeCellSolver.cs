using Client.Games.Cards.Services;

namespace TestProject1
{
    public partial class FreeCellSolverTests
    {
        public class FreeCellSolver
        {
            private FreeCellGameService _gameService; // current state of board including undo
            public FreeCellGameBase _gameClone; // state of board as we manipulate it
            private List<FreeCellMove> _moveHistory; // so we don't repeat moves that we just did
            private HashSet<string> _visitedStates; // for cycle detection

            public FreeCellSolver(FreeCellGameService gameService, List<FreeCellMove> moveHistory, HashSet<string>? visitedStates = null)
            {
                _gameService = gameService;
                _gameClone = gameService.Clone();
                _gameClone.AutoMoveToFoundationDisable = true;
                _moveHistory = moveHistory;
                _visitedStates = visitedStates ?? new HashSet<string>();

                // Add current state to visited if not already there
                _visitedStates.Add(_gameClone.GetStateHash());
            }

            public static async Task<FreeCellSolver> CreateAsync(FreeCellGameService freeCellGameService, List<FreeCellMove> moveHistory, HashSet<string>? visitedStates = null)
            {
                var solver = new FreeCellSolver(freeCellGameService, moveHistory, visitedStates);
                return solver;
            }

            /// <summary>
            /// Checks if making a move would result in a state we've already visited (cycle detection)
            /// </summary>
            private bool MoveWouldCauseCycle(FreeCellMove move)
            {
                // Apply move, check hash, then unapply (much cheaper than Clone())
                // If the move cannot be applied, treat it as invalid / skip it (return true so AddNewMove doesn't include it).
                if (!move.ApplyMove(_gameClone))
                {
                    throw new Exception($"Failed to apply {move} move for cycle detection");
                }

                var hash = _gameClone.GetStateHash();
                var wouldCauseCycle = _visitedStates.Contains(hash);

                // Try to unapply; if unapply fails that's a real problem — throw to surface it.
                if (!move.UnApplyMove(_gameClone))
                {
                    throw new Exception($"Failed to unapply {move} move for cycle detection");
                }

                return wouldCauseCycle;
            }

            /// <summary>
            /// Records a state as visited (call after applying a move)
            /// </summary>
            public void RecordVisitedState()
            {
                _visitedStates.Add(_gameClone.GetStateHash());
            }

            /// <summary>
            /// Gets the visited states set (for passing to child solvers or debugging)
            /// </summary>
            public HashSet<string> VisitedStates => _visitedStates;

            public List<FreeCellMove> FindMoves()
            {
                var lstMoves = new List<FreeCellMove>();
                int nFreeCells = _gameClone.EmptyFreeCellCount;
                var sumSeqLenBeforeeCurrentMove = _gameClone.GetTotalSeqLengths(); // sum of all sequence lengths from each column. A good move will often increase this by creating longer sequences, a bad move will decrease it by breaking sequences up
                var maxScoreSoFar = 0;
                bool AddNewMove(FreeCellMove move)
                {
                    var didit = false;
                    if (!moveWouldJustUndoPriorMove(move) && !MoveWouldCauseCycle(move))
                    {
                        if (move.score > maxScoreSoFar)
                        {
                            maxScoreSoFar = move.score;
                        }
                        lstMoves.Add(move);
                        didit = true;
                    }
                    return didit;
                }
                var allowFoundationMovesToTableau = false;
                if (allowFoundationMovesToTableau)
                {
                    // see if any foundation cells can be added to tableau
                    for (int i = 0; i < _gameClone.Foundations.Count; i++)
                    {
                        var foundation = _gameClone.Foundations[i];
                        if (foundation.Count > 0)
                        {
                            var card = _gameClone.Foundations[i][^1];
                            if (card != null)
                            {
                                for (int iCol = 0; iCol < _gameClone.Tableau.Count - 1; iCol++)
                                {
                                    if (_gameClone.CanPlaceOnTableau(card, _gameClone.Tableau[iCol]))
                                    {
                                        // just because we Can place, it from Foundation to tableau, doesn't mean we Want to. Only do so if it would increase the seq total.
                                        // todo: Check if once done, there are any moves that would increase the seq total.
                                        // for now, we'll add with mediocre score

                                        AddNewMove(new FreeCellMove(card)
                                        {
                                            sourceType = SourceType.Foundation,
                                            targetType = SourceType.Tableau,
                                            sourceIndex = i,
                                            targetIndex = iCol,      // <-- FIX: set targetIndex to destination column
                                            cardCount = 1,
                                            score = 5
                                        });

                                    }
                                }
                            }
                        }
                    }
                }
                //  see if any of the freecells can be moved to a foundation or tableau
                for (int i = 0; i < _gameClone.FreeCells.Count; i++)
                {
                    var freecellCard = _gameClone.FreeCells[i];
                    if (freecellCard == null) continue;
                    // Check if we can move this card to a foundation
                    var foundationIndex = _gameClone.CanMoveToAnyFoundation(freecellCard);
                    if (foundationIndex >= 0)
                    {
                        AddNewMove(new FreeCellMove(freecellCard)
                        {
                            sourceType = SourceType.FreeCell,
                            targetType = SourceType.Foundation,
                            sourceIndex = i,
                            targetIndex = foundationIndex,
                            cardCount = 1,
                            score = 100 // arbitrary score for now
                        });
                        //return lstMoves; // prioritize moving to foundation
                    }
                    // now see if freecell to tableau
                    for (var dstCol = 0; dstCol < _gameClone.Tableau.Count; dstCol++)
                    {
                        if (_gameClone.CanMoveFreeCellToTableau(i, dstCol))
                        {
                            // if the dest column is empty, don't do it: no gain in moving free card to empty column
                            if (_gameClone.Tableau[dstCol].Count > 0)
                            {
                                AddNewMove(new FreeCellMove(freecellCard)
                                {
                                    sourceType = SourceType.FreeCell,
                                    targetType = SourceType.Tableau,
                                    sourceIndex = i,
                                    targetIndex = dstCol,
                                    cardCount = 1,
                                    score = 80 // arbitrary score for now
                                });
                            }
                        }
                    }
                }
                // check for tableau to tableau moves, and tableau to foundation or freecell moves. Prioritize moving to foundation, then tableau to tableau, then tableau to freecell
                for (int srcCol = 0; srcCol < _gameClone.Tableau.Count; srcCol++)
                {
                    var column = _gameClone.Tableau[srcCol];
                    if (column.Count == 0) continue;
                    var seqlen = _gameClone.GetBottomSequenceLength(srcCol);
                    var topCard = column[^seqlen];
                    var botCard = column[^1];
                    // Check if we can move this card to a foundation
                    var foundationIdx = _gameClone.CanMoveToAnyFoundation(botCard);
                    if (foundationIdx >= 0)
                    {
                        AddNewMove(new FreeCellMove(botCard)
                        {
                            sourceType = SourceType.Tableau,
                            targetType = SourceType.Foundation,
                            sourceIndex = srcCol,
                            targetIndex = foundationIdx,
                            cardCount = 1,
                            score = 100 // arbitrary score for now
                        });
                    }
                    for (var dstCol = 0; dstCol < _gameClone.Tableau.Count; dstCol++)
                    {
                        if (srcCol == dstCol) continue;
                        // if the destination column is empty, and the seqlen is the entire column, don't do anything. Moving an entire column is a no-op
                        if (_gameClone.Tableau[dstCol].Count == 0 && seqlen == column.Count)
                        {
                            continue;
                        }
                        int maxMovable = _gameClone.CalculateMaxMovableCards(SourceType.Tableau, dstCol);
                        if (seqlen > maxMovable)
                        {
                            continue;
                        }
                        if (_gameClone.CanMoveTableauToTableau(srcCol, dstCol, seqlen))
                        {
                            AddNewMove(new FreeCellMove(topCard)
                            {
                                sourceType = SourceType.Tableau,
                                targetType = SourceType.Tableau,
                                sourceIndex = srcCol,
                                targetIndex = dstCol,
                                cardCount = seqlen,
                                score = 50 + seqlen * 10 // arbitrary scoring that favors longer moves
                            });
                        }
                    }
                }
                // now see if can move to free cell
                if (nFreeCells > 0 && maxScoreSoFar < 2) // don't move to freecell if we can move to foundation or to tableau
                {
                    for (int i = 0; i < _gameClone.Tableau.Count; i++)
                    {
                        if (_gameClone.Tableau[i].Count == 0) continue;

                        // Check if we can move this card to a free cell
                        {
                            AddNewMove(new FreeCellMove(_gameClone.Tableau[i][^1])
                            {
                                sourceType = SourceType.Tableau,
                                targetType = SourceType.FreeCell,
                                sourceIndex = i,
                                targetIndex = _gameClone.FindAnyFreeCell(),
                                cardCount = 1,
                                score = 1 // arbitrary score for now
                            });
                        }
                    }
                }
                var maxScore = lstMoves.Count > 0 ? lstMoves.Max(m => m.score) : 0;
                if (maxScore > 5)
                {
                    lstMoves = lstMoves.Where(m => m.score >= maxScore - 5).OrderByDescending(m => m.score).ToList(); // if we have any good moves, only keep the good moves);
                }
                else
                {
                    lstMoves.OrderByDescending(m => m.score).ToList();
                }
                return lstMoves;
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
        }
    }
}
