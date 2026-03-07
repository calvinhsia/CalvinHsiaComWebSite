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

            public FreeCellSolver(FreeCellGameService gameService, List<FreeCellMove> moveHistory)
            {
                _gameService = gameService;
                _gameClone = gameService.Clone();
                _gameClone.AutoMoveToFoundationDisable = true;
                _moveHistory = moveHistory;
            }

            public static async Task<FreeCellSolver> CreateAsync(FreeCellGameService freeCellGameService, List<FreeCellMove> moveHistory)
            {
                var solver = new FreeCellSolver(freeCellGameService, moveHistory);
                return solver;
            }
            public List<FreeCellMove> FindMoves()
            {
                var lstMoves = new List<FreeCellMove>();
                int nFreeCells = _gameClone.EmptyFreeCellCount;
                var sumSeqLenBeforeeCurrentMove = _gameClone.GetTotalSeqLengths(); // sum of all sequence lengths from each column. A good move will often increase this by creating longer sequences, a bad move will decrease it by breaking sequences up
                var maxScoreSoFar = 0;
                bool AddNewMove(FreeCellMove move)
                {
                    var didit = false;
                    if (!moveWouldJustUndoPriorMove(move))
                    {
                        if (move.score > maxScoreSoFar)
                        {
                            maxScoreSoFar = move.score;
                        }
                        lstMoves.Add(move);
                        //// now calculate the delta sequence lengths: the net change in total sequence lengths after making this move.
                        //// A positive delta is good, a negative delta is bad, but sometimes necessary to make progress.
                        //switch (move.targetType)
                        //{
                        //    case SourceType.FreeCell:
                        //        move.deltaSequenceCount = 0; // no affect on seq lengths since freecell is just a holding place
                        //        break;
                        //    case SourceType.Foundation:
                        //        move.deltaSequenceCount = 1; // 
                        //        break;
                        //    case SourceType.Tableau:
                        //        // calculate what the delta seq length is just for this column move. Decrease the source column seq length by the length of the moved sequence, and increase the dest column seq length by the length of the moved sequence, plus any new sequences created by placing the moved cards on the dest column
                        //        move.deltaSequenceCount = 2;
                        //        break;
                        //}
                        didit = true;
                    }
                    return didit;
                }
                // first see if any of the freecells can be moved to a foundation or tableau
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
                            score = 20 // arbitrary score for now
                        });
                        //return lstMoves; // prioritize moving to foundation
                    }
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
                                    score = 15 // arbitrary score for now
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
                    //Log($"Col {i} has {column.Count}, bottom seq len={seqlen}, Top= {topCard} Bot={botCard}");
                    // Check if we can move this card to a foundation
                    var foundationIdx = _gameClone.CanMoveToAnyFoundation(botCard);
                    if (foundationIdx >= 0)
                    {
                        //Log($"Can move {topCard} from column {i} to foundation");
                        AddNewMove(new FreeCellMove(botCard)
                        {
                            sourceType = SourceType.Tableau,
                            targetType = SourceType.Foundation,
                            sourceIndex = srcCol,
                            targetIndex = foundationIdx,
                            cardCount = 1,
                            score = 10 // arbitrary score for now
                        });
                        //return lstMoves;
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
                            //Log($"Can move {seqlen} cards from column {i} to column {j}");
                            AddNewMove(new FreeCellMove(topCard)
                            {
                                sourceType = SourceType.Tableau,
                                targetType = SourceType.Tableau,
                                sourceIndex = srcCol,
                                targetIndex = dstCol,
                                cardCount = seqlen,
                                score = 5 + seqlen // arbitrary scoring that favors longer moves
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

                        //var seqlen = _gameClone.GetBottomSequenceLength(i);
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
                            //Console.WriteLine($"Can move {topCard} from tableau column {i + 1} to a free cell");
                        }
                    }
                }
                return lstMoves.OrderByDescending(m => m.score).ToList();
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
