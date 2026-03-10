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
        private Action<string>? _LogAction; // optional logging for debugging

        public FreeCellSolver(FreeCellGameService gameService, Action<string>? logAction = null)
        {
            _gameService = gameService;
            _game = gameService.Clone();
            _LogAction = logAction;
            _game.AutoMoveToFoundationDisable = true;

            // Add current state to visited if not already there
            _visitedStates.Add(_game.GetStateHash());
        }

        public static async Task<FreeCellSolver> CreateAsync(FreeCellGameService freeCellGameService, Action<string>? logAction = null)
        {
            var solver = new FreeCellSolver(freeCellGameService, logAction);
            return solver;
        }

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

        /// <summary>
        /// Records a state as visited (call after applying a move)
        /// </summary>
        public void RecordVisitedState()
        {
            _visitedStates.Add(_game.GetStateHash());
        }

        /// <summary>
        /// Gets the visited states set (for passing to child solvers or debugging)
        /// </summary>
        public HashSet<string> VisitedStates => _visitedStates;

        public List<FreeCellMove> FindMoves()
        {
            var lstMoves = new List<FreeCellMove>();
            int nFreeCells = _game.EmptyFreeCellCount;
            var sumSeqLenBeforeeCurrentMove = _game.GetTotalSeqLengths(); // sum of all sequence lengths from each column. A good move will often increase this by creating longer sequences, a bad move will decrease it by breaking sequences up
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
                for (int i = 0; i < _game.Foundations.Count; i++)
                {
                    var foundation = _game.Foundations[i];
                    if (foundation.Count > 0)
                    {
                        var card = _game.Foundations[i][^1];
                        if (card != null)
                        {
                            for (int iCol = 0; iCol < _game.Tableau.Count - 1; iCol++)
                            {
                                if (_game.CanPlaceOnTableau(card, _game.Tableau[iCol]))
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
                        score = 100 // arbitrary score for now
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
                                score = 80 // arbitrary score for now
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
                        score = 100 // arbitrary score for now
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
                            score = 50 + seqlen * 10 // arbitrary scoring that favors longer moves
                        });
                    }
                }
            }
            // now see if can move to free cell
            if (nFreeCells > 0 && maxScoreSoFar < 2) // don't move to freecell if we can move to foundation or to tableau
            {
                for (int i = 0; i < _game.Tableau.Count; i++)
                {
                    if (_game.Tableau[i].Count == 0) continue;

                    // Check if we can move this card to a free cell
                    {
                        AddNewMove(new FreeCellMove(_game.Tableau[i][^1])
                        {
                            sourceType = SourceType.Tableau,
                            targetType = SourceType.FreeCell,
                            sourceIndex = i,
                            targetIndex = _game.FindAnyFreeCell(),
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

        public List<FreeCellMove>? FindSolution()
        {
            FreeCellMove rootTree = new FreeCellMove(cardMoved: null); // dummy root node to hold the move tree
            var currentNode = rootTree;
            var countNodesCreated = 0;
            var countNodesVisited = 0;
            var numTimesBacktracked = 0;

            while (true)
            {
                _LogAction!(_game.dumpAllToLog($"Move count: {_game.MoveCount} CreatedNodes:{countNodesCreated} VisitedNodes:{countNodesVisited}"));
                var moves = FindMoves();
                void dumpMoves()
                {
                    foreach (var move in moves)
                    {
                        move.ParentMove = currentNode;
                        move.Depth = currentNode.Depth + 1;
                        _LogAction(move.ToString());
                    }
                }
                dumpMoves();
                if (_game.MoveCount >= 227)
                {
                    "bpt".ToString();
                }
                currentNode.ChildMoves.AddRange(moves);
                countNodesCreated += moves.Count;
                var bestMove = moves.FirstOrDefault();
                if (bestMove == null)
                {
                    if (_game.IsGameWon)
                    {
                        _LogAction(_game.dumpAllToLog($"Game won at move count {_game.MoveCount}! Total nodes visited: {countNodesVisited}, total nodes created: {countNodesCreated}. # backtrack = {numTimesBacktracked}"));
                        break;
                    }
                    _LogAction(_game.dumpAllToLog($"No moves found by solver at move count {_game.MoveCount}."));
                    // we want to backtrack the position to the last move that had a score > 1 (not moving to a freecell)
                    // and use the next best move.
                    var keepBacktracking = true;
                    while (keepBacktracking)
                    {
                        currentNode.score = 0;
                        numTimesBacktracked++;
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

                        _LogAction($"Unapplied  {_game.dumpAllToLog(currentNode.ToString())}");

                        currentNode = currentNode.ParentMove;
                        if (currentNode != null)
                        {
                            if (currentNode.IsRootNode)
                            {
                                _LogAction($"Backtracked all the way to root node, no solution found");
                                break;
                            }
                            // now find the first childmove that we haven't done yet and execute it
                            bestMove = currentNode.ChildMoves.FirstOrDefault(m => !m.DidExecuteMove);
                            if (bestMove == null)
                            {
                                _LogAction($"Backtracking to move with score {currentNode.score} at depth {currentNode.Depth}, no more best moves, so we need to backtrack further");
                                keepBacktracking = true;
                            }
                            else
                            {
                                _LogAction($"Found next best move at depth {currentNode.Depth}: {bestMove}, score={bestMove.score}, so executing it");
                                keepBacktracking = false;
                            }
                        }
                        else
                        {
                            _LogAction($"no moves found backtracking all the way to rootnode");
                            break; // 
                        }
                    }
                }
                if (bestMove == null)
                {
                    throw new Exception($"Solver failed {_game.MoveCount} to find any moves, but game is not won. Visited {_visitedStates.Count} states. Check logs for details.");
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
                countNodesVisited++;
                var nMaxMovesToDo = 1000;
                if (_moveHistory.Count > nMaxMovesToDo)
                {
                    _LogAction(_game.dumpAllToLog($"Aborting solver after {nMaxMovesToDo} moves, likely stuck in a cycle. Visited {_visitedStates.Count} states."));
                    throw new Exception($"Aborting solver after {nMaxMovesToDo} moves, likely stuck in a cycle. Check logs for details.");

                }
            }
            return _moveHistory;
        }
    }
}
