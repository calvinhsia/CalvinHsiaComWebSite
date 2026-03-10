using Client.Games.Cards.Services;
using Grpc.Net.Client.Balancer;

namespace TestProject1
{
    public class FreeCellSolver
    {
        private FreeCellGameService _gameService; // current state of board including undo
        public FreeCellGameBase _gameClone; // state of board as we manipulate it
        private List<FreeCellMove> _moveHistory = []; // so we don't repeat moves that we just did
        private HashSet<string> _visitedStates = []; // for cycle detection
        private Action<string>? _LogAction; // optional logging for debugging

        public FreeCellSolver(FreeCellGameService gameService, Action<string>? logAction = null)
        {
            _gameService = gameService;
            _gameClone = gameService.Clone();
            _LogAction = logAction;
            _gameClone.AutoMoveToFoundationDisable = true;

            // Add current state to visited if not already there
            _visitedStates.Add(_gameClone.GetStateHash());
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

        public List<FreeCellMove>? FindSolution()
        {
            var game = _gameClone;
            FreeCellMove rootTree = new FreeCellMove(cardMoved: null); // dummy root node to hold the move tree
            var currentNode = rootTree;
            var countNodesCreated = 0;
            var countNodesVisited = 0;
            var numTimesBacktracked = 0;

            while (true)
            {
                _LogAction!(game.dumpAllToLog($"Move count: {game.MoveCount} CreatedNodes:{countNodesCreated} VisitedNodes:{countNodesVisited}"));
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
                if (game.MoveCount >= 227)
                {
                    "bpt".ToString();
                }
                currentNode.ChildMoves.AddRange(moves);
                countNodesCreated += moves.Count;
                var bestMove = moves.FirstOrDefault();
                if (bestMove == null)
                {
                    if (game.IsGameWon)
                    {
                        _LogAction(game.dumpAllToLog($"Game won at move count {game.MoveCount}! Total nodes visited: {countNodesVisited}, total nodes created: {countNodesCreated}. # backtrack = {numTimesBacktracked}"));
                        break;
                    }
                    _LogAction(game.dumpAllToLog($"No moves found by solver at move count {game.MoveCount}."));
                    // we want to backtrack the position to the last move that had a score > 1 (not moving to a freecell)
                    // and use the next best move.
                    var keepBacktracking = true;
                    while (keepBacktracking)
                    {
                        currentNode.score = 0;
                        numTimesBacktracked++;
                        // we need to undo the move to backtrack the game state
                        var didUnApply = currentNode.UnApplyMove(game);
                        if (!didUnApply)
                        {
                            throw new Exception($"Failed to unapply move during backtracking: {currentNode}");
                        }
                        // remove last entry from moveHistory
                        if (_moveHistory.Count > 0)
                        {
                            _moveHistory.RemoveAt(_moveHistory.Count - 1);
                        }

                        _LogAction($"Unapplied  {game.dumpAllToLog(currentNode.ToString())}");

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
                    throw new Exception($"Solver failed {game.MoveCount} to find any moves, but game is not won. Visited {_visitedStates.Count} states. Check logs for details.");
                }
                var didit = bestMove.ApplyMove(game);
                if (!didit)
                {
                    throw new Exception($"Err applying move: {bestMove}.");
                }
                bestMove.DidExecuteMove = true;
                _moveHistory.Add(bestMove);
                currentNode = bestMove;

                // Record the new state after the move for cycle detection
                var hash = game.GetStateHash();
                if (hash == "F:_,10S,JS,KH|P:C13,D12,H12,S9|T:||||||KDQS|KS")
                {
                    "bpt".ToString();
                }
                _visitedStates.Add(hash);
                countNodesVisited++;
                var nMaxMovesToDo = 1000;
                if (_moveHistory.Count > nMaxMovesToDo)
                {
                    _LogAction(game.dumpAllToLog($"Aborting solver after {nMaxMovesToDo} moves, likely stuck in a cycle. Visited {_visitedStates.Count} states."));
                    throw new Exception($"Aborting solver after {nMaxMovesToDo} moves, likely stuck in a cycle. Check logs for details.");

                }
            }
            return _moveHistory;
        }
    }
}
