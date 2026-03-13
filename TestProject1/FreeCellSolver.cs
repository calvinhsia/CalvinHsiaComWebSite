using Client.Games.Cards.Services;
using Grpc.Net.Client.Balancer;

namespace TestProject1
{
    public class FreeCellSolver
    {
        private FreeCellGameService _gameService; // current state of board including undo
        public FreeCellGameBase _game; // state of board as we manipulate it
        private List<FreeCellMove> _moveHistory = []; // so we don't repeat moves that we just did
        private HashSet<string> _visitedStates = []; // for cycle detection

        private Action<Func<string>>? _LoggerAction; // avoids costly evaluation of logger messages when logging is disabled

        public FreeCellSolver(FreeCellGameService gameService, Action<Func<string>>? loggerAction)
        {
            _gameService = gameService;
            _game = gameService.Clone();
            _LoggerAction = loggerAction;
            _game.AutoMoveToFoundationDisable = true;

            // Add current state to visited if not already there
            _visitedStates.Add(_game.GetStateHash());
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
            // Apply move, check hash, then unapply (much cheaper than Clone())
            // If the move cannot be applied, treat it as invalid / skip it (return true so AddNewMove doesn't include it).
            if (!move.ApplyMove(_game))
            {
                throw new Exception($"Failed to apply {move} move for cycle detection");
            }

            var hash = _game.GetStateHash();
            var wouldCauseCycle = _visitedStates.Contains(hash);

            // Try to unapply; if unapply fails that's a real problem — throw to surface it.
            if (!move.UnApplyMove(_game))
            {
                throw new Exception($"Failed to unapply {move} move for cycle detection");
            }
            return wouldCauseCycle;
        }

        public List<FreeCellMove> FindMoves()
        {
            var lstMoves = new List<FreeCellMove>();
            int nFreeCells = _game.EmptyFreeCellCount;
            var sumSeqLenBeforeeCurrentMove = _game.GetBValue(); // sum of all sequence lengths from each column. A good move will often increase this by creating longer sequences, a bad move will decrease it by breaking sequences up
            var maxScoreSoFar = 0;
            bool AddNewMove(FreeCellMove move)
            {
                var didit = false;
                if (!MoveWouldCauseCycle(move))
                {
                    if (move.mValue > maxScoreSoFar)
                    {
                        maxScoreSoFar = move.mValue;
                    }
                    lstMoves.Add(move);
                    didit = true;
                }
                return didit;
            }
            var allowFoundationMovesToTableau = false;
            /* fails with lots of empty columns: combinatorics explode. Therea re simple mvoes from Tableau to Foundations here that are skipped.
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

            if (allowFoundationMovesToTableau)
            {
                // see if any foundation cells can be added to tableau
                for (int i = 0; i < _game.Foundations.Count; i++)
                {
                    var foundation = _game.Foundations[i];
                    if (foundation.Count > 0)
                    {
                        var card = _game.Foundations[i][^1];
                        if (card != null && (int)card.Rank > 2) // don't try an ace or 2
                        {
                            for (int iCol = 0; iCol < _game.Tableau.Count - 1; iCol++) // see if the foundation card can be added to a non-empty column
                            {
                                if (_game.Tableau[iCol].Count == 0)
                                {
                                    continue;
                                }
                                if (_game.CanPlaceOnTableau(card, _game.Tableau[iCol]))
                                {
                                    // just because we Can place, it from Foundation to tableau, doesn't mean we want to. Only do so if it would increase the seq total.
                                    // todo: Check if once done, there are any moves that would increase the seq total.
                                    // for now, we'll add with mediocre score

                                    AddNewMove(new FreeCellMove(card)
                                    {
                                        sourceType = SourceType.Foundation,
                                        targetType = SourceType.Tableau,
                                        sourceIndex = i,
                                        targetIndex = iCol,      // <-- FIX: set targetIndex to destination column
                                        cardCount = 1,
                                        mValue = 5
                                    });

                                }
                            }
                        }
                    }
                }
            }
            //  see if any of the freecells can be moved to a foundation or tableau
            for (int i = 0; i < _game.FreeCells.Count; i++)
            {
                var freecellCard = _game.FreeCells[i];
                if (freecellCard == null) continue;
                // Check if we can move this card to a foundation
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
                    //return lstMoves; // prioritize moving to foundation
                }
                // now see if freecell to tableau
                for (var dstCol = 0; dstCol < _game.Tableau.Count; dstCol++)
                {
                    if (_game.CanMoveFreeCellToTableau(i, dstCol))
                    {
                        // if the dest column is empty, don't do it: no gain in moving free card to empty column
                        if (_game.Tableau[dstCol].Count > 0)
                        {
                            AddNewMove(new FreeCellMove(freecellCard)
                            {
                                sourceType = SourceType.FreeCell,
                                targetType = SourceType.Tableau,
                                sourceIndex = i,
                                targetIndex = dstCol,
                                cardCount = 1,
                                mValue = 80 // arbitrary score for now
                            });
                        }
                    }
                }
            }
            // check for tableau to tableau moves, and tableau to foundation or freecell moves. Prioritize moving to foundation, then tableau to tableau, then tableau to freecell
            for (int srcCol = 0; srcCol < _game.Tableau.Count; srcCol++)
            {
                var column = _game.Tableau[srcCol];
                if (column.Count == 0) continue;
                var seqlen = _game.GetBottomSequenceLength(srcCol);
                var topCard = column[^seqlen];
                var botCard = column[^1];
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
                for (var dstCol = 0; dstCol < _game.Tableau.Count; dstCol++)
                {
                    if (srcCol == dstCol) continue;
                    // if the destination column is empty, and the seqlen is the entire column, don't do anything. Moving an entire column is a no-op
                    if (_game.Tableau[dstCol].Count == 0 && seqlen == column.Count)
                    {
                        continue;
                    }
                    int maxMovable = _game.CalculateMaxMovableCards(SourceType.Tableau, dstCol);
                    if (seqlen > maxMovable)
                    {
                        continue;
                    }
                    if (_game.CanMoveTableauToTableau(srcCol, dstCol, seqlen))
                    {
                        AddNewMove(new FreeCellMove(topCard)
                        {
                            sourceType = SourceType.Tableau,
                            targetType = SourceType.Tableau,
                            sourceIndex = srcCol,
                            targetIndex = dstCol,
                            cardCount = seqlen,
                            mValue = 50 + seqlen * 10 // arbitrary scoring that favors longer moves
                        });
                    }
                }
            }
            // now see if can move to free cell
            if (nFreeCells > 0 && maxScoreSoFar < 2) // don't move to freecell if we can move to foundation or to tableau
            {
                for (int iCol = 0; iCol < _game.Tableau.Count; iCol++)
                {
                    var column = _game.Tableau[iCol];
                    if (column.Count == 0) continue;
                    {
                        // we'll start the scoring at 1. If there are cards that can be placed on foundation (initially aces) then add score for each.
                        // The higher the index, the higher the score. The last in the column gets the highest
                        // If the column count is 1, more points because an empty column is worth more than an empty freecell.
                        // if there are 2 or 3 of a kind, add more
                        var score = 1;
                        if (column.Count == 1)
                        {
                            score += 4;
                        }
                        else
                        {
                            var nMaxToLookat = 3;
                            for (int idx = column.Count - 2; idx >= 0 && nMaxToLookat > 0; idx--, nMaxToLookat--)
                            {
                                if (_game.CanMoveToAnyFoundation(column[idx]) >= 0)
                                {
                                    if (idx == column.Count - 2)   
                                    {
                                        score += 10; // moving a card that can go to foundation is good,
                                    }
                                    score += idx + 1; // the higher the index, the higher the score
                                }
                            }
                        }
                        AddNewMove(new FreeCellMove(_game.Tableau[iCol][^1])
                        {
                            sourceType = SourceType.Tableau,
                            targetType = SourceType.FreeCell,
                            sourceIndex = iCol,
                            targetIndex = _game.FindAnyFreeCell(),
                            cardCount = 1,
                            mValue = score // use the calculated score
                        });
                    }
                }
            }
            var maxScore = lstMoves.Count > 0 ? lstMoves.Max(m => m.mValue) : 0;
            if (maxScore > 50000)
            {
                lstMoves = lstMoves.Where(m => m.mValue >= maxScore - 5).OrderByDescending(m => m.mValue).ToList(); // if we have any good moves, only keep the good moves);
            }
            else
            {
                lstMoves.OrderByDescending(m => m.mValue).ToList();
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

        public int MoveValueDelta(FreeCellMove move, int startBValue)
        {
            // apply the move, check score, then unapply
            if (!move.ApplyMove(_game))
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

        public int _countNodesCreated = 0;
        public int _countNodesVisited = 0;
        public int _numTimesBacktracked = 0;
        public List<FreeCellMove> FindSolution()
        {
            FreeCellMove rootTree = new FreeCellMove(cardMoved: null); // dummy root node to hold the move tree
            var currentNode = rootTree;

            while (true)
            {
                _LoggerAction?.Invoke(() => _game.dumpAllToLog($"Move count: {_game.MoveCount} CreatedNodes:{_countNodesCreated} VisitedNodes:{_countNodesVisited}"));
                var moves = FindMoves();
                foreach (var move in moves)
                {
                    move.ParentMove = currentNode;
                    move.Depth = currentNode.Depth + 1;
                    _LoggerAction?.Invoke(() => move.ToString());
                }
                if (_game.MoveCount >= 227)
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
                        _LoggerAction?.Invoke(() => _game.dumpAllToLog($"Game won at move count {_game.MoveCount}! Total nodes visited: {_countNodesVisited}, total nodes created: {_countNodesCreated}. # backtrack = {_numTimesBacktracked}"));
                        break;
                    }
                    _LoggerAction?.Invoke(() => _game.dumpAllToLog($"No moves found by solver at move count {_game.MoveCount}."));
                    // we want to backtrack the position to the last move that had a score > 1 (not moving to a freecell)
                    // and use the next best move.
                    var keepBacktracking = true;
                    while (keepBacktracking)
                    {
                        currentNode.mValue = 0;
                        _numTimesBacktracked++;
                        // we need to undo the move to backtrack the game state
                        var didUnApply = currentNode.UnApplyMove(_game);
                        if (!didUnApply)
                        {
                            throw new Exception($"Failed to unapply move during backtracking: {currentNode}");
                        }
                        // remove last entry from moveHistory
                        if (_moveHistory.Count > 0)
                        {
                            _moveHistory.RemoveAt(_moveHistory.Count - 1);
                        }

                        _LoggerAction?.Invoke(() => $"Unapplied  {_game.dumpAllToLog(currentNode.ToString())}");

                        currentNode = currentNode.ParentMove;
                        if (currentNode != null)
                        {
                            if (currentNode.IsRootNode)
                            {
                                _LoggerAction?.Invoke(() => "Backtracked all the way to root node, no solution found");
                                break;
                            }
                            // now find the first childmove that we haven't done yet and execute it
                            bestMove = currentNode.ChildMoves.FirstOrDefault(m => !m.DidExecuteMove);
                            if (bestMove == null)
                            {
                                _LoggerAction?.Invoke(() => $"Backtracking to move with score {currentNode.mValue} at depth {currentNode.Depth}, no more best moves, so we need to backtrack further");
                                keepBacktracking = true;
                            }
                            else
                            {
                                _LoggerAction?.Invoke(() => $"Found next best move at depth {currentNode.Depth}: {bestMove}, score={bestMove.mValue}, so executing it");
                                keepBacktracking = false;
                            }
                        }
                        else
                        {
                            _LoggerAction?.Invoke(() => "no moves found backtracking all the way to rootnode");
                            break; // 
                        }
                    }
                }
                if (bestMove == null)
                {
                    throw new Exception($"Solver failed {_game.MoveCount} to find any moves, but game is not won. Visited {_visitedStates.Count} states.");
                }
                var didit = bestMove.ApplyMove(_game);
                if (!didit)
                {
                    throw new Exception($"Err applying move: {bestMove}.");
                }
                bestMove.DidExecuteMove = true;
                _moveHistory.Add(bestMove);
                currentNode = bestMove;

                // Record the new state after the move for cycle detection
                var hash = _game.GetStateHash();
                if (hash == "F:_,10S,JS,KH|P:C13,D12,H12,S9|T:||||||KDQS|KS")
                {
                    "bpt".ToString();
                }
                _visitedStates.Add(hash);
                _countNodesVisited++;
                var nMaxMovesToDo = 5000;
                if (_moveHistory.Count > nMaxMovesToDo)
                {
                    _LoggerAction?.Invoke(() => _game.dumpAllToLog($"Aborting solver after {nMaxMovesToDo} moves, likely stuck in a cycle. Visited {_visitedStates.Count} states."));
                    throw new Exception($"Aborting solver after {nMaxMovesToDo} moves, likely stuck in a cycle. Check logs for details.");

                }
            }
            return _moveHistory;
        }
    }
}
