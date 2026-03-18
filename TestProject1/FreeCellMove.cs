using Client.Games.Cards.Services;
using Client.Games.Cards.Models;

namespace TestProject1
{
    public class FreeCellMove
    {
        /*
         * A move can be between any combination of Foundation/Freecell/Tableau
         * If tableau to tableau, source and target index are column indexes and cardCount is how many cards from the bottom of the source column
         * The Seq count for a board is the sum of all sequences that are valid (consecutive, altering red/black). Increasing overall seq count is good
         * Some moves may move a card to the foundation or freecell, which reduces seq count
         */
        public SourceType sourceType { get; set; }
        public SourceType targetType { get; set; }
        public int sourceIndex { get; set; } // column # or index of FreeCell or Foundation
        public int targetIndex { get; set; } // column # or index of FreeCell or Foundation
        public int srcColumnIndex { get; set; } // only for tableau to tableau moves
        public int cardCount { get; set; } // only for tableau to tableau moves, how many cards from the bottom of the source column

        public int Depth { get; set; } // from 0. Depth of the move in the search tree, used for debugging and logging
        public bool DidExecuteMove { get; set; } = false;
        public FreeCellMove? ParentMove { get; set; } // used for debugging and logging to trace back the sequence of moves that led to this move
        public List<FreeCellMove> ChildMoves { get; set; } = new List<FreeCellMove>(); // used for debugging and logging to trace the sequence of moves that led to this move and the moves that can be made from this move

        public int mValue { get; set; } // move value
        public int deltaBValue { get; set; }
        public Card? CardMoved { get; set; }
        public bool IsRootNode => ParentMove == null;
        public FreeCellMove(Card? cardMoved)
        {
            CardMoved = cardMoved;
        }
        public bool ApplyMove(FreeCellGameBase game)
        {
            var cardIndex = -1;
            if (sourceType == SourceType.Tableau)
            {
                cardIndex = game.Tableau[sourceIndex].Count - cardCount;
            }
            // use the TryMove method to apply this move to the game in memory
            game.Selection = new CardSelection
            {
                SourceType = sourceType,
                SourceIndex = sourceIndex,
                CardIndex = cardIndex // not used for tableau to tableau moves since we always move from the bottom of the column, and not used for freecell or foundation moves since they only have one card
            };
            var didMove = game.TryMove(targetType, targetIndex);
            return didMove;
        }
        public override string ToString() => $"{CardMoved} {sourceType}[{sourceIndex}]->{targetType}[{targetIndex}] cards:{cardCount} mVal:{mValue} {(DidExecuteMove ? "!" : "")}";

        public bool UnApplyMove(FreeCellGameBase game)
        {
            // Direct manipulation is safer than TryMove because:
            // 1. Foundation moves can't be reversed (validation rules differ)
            // 2. Tableau moves may fail due to maxMovable changing after the original move
            //    (e.g., moving to empty column reduces empty column count, so reverse has lower maxMovable)
            // 3. FreeCell state changes affect maxMovable calculations

            // Get cards to move back from target
            List<Card> cardsToMove;

            switch (targetType)
            {
                case SourceType.Foundation:
                    if (game.Foundations[targetIndex].Count == 0) return false;
                    cardsToMove = [game.Foundations[targetIndex][^1]];
                    game.Foundations[targetIndex].RemoveAt(game.Foundations[targetIndex].Count - 1);
                    break;

                case SourceType.Tableau:
                    var targetCol = game.Tableau[targetIndex];
                    if (targetCol.Count < cardCount) return false;
                    var startIdx = targetCol.Count - cardCount;
                    cardsToMove = targetCol.GetRange(startIdx, cardCount);
                    targetCol.RemoveRange(startIdx, cardCount);
                    break;

                case SourceType.FreeCell:
                    if (game.FreeCells[targetIndex] == null) return false;
                    cardsToMove = [game.FreeCells[targetIndex]!];
                    game.FreeCells[targetIndex] = null;
                    break;

                default:
                    return false;
            }

            // Put cards back to source
            switch (sourceType)
            {
                case SourceType.Tableau:
                    game.Tableau[sourceIndex].AddRange(cardsToMove);
                    break;

                case SourceType.FreeCell:
                    if (cardsToMove.Count != 1) return false;
                    game.FreeCells[sourceIndex] = cardsToMove[0];
                    break;

                case SourceType.Foundation:
                    if (cardsToMove.Count != 1) return false;
                    game.Foundations[sourceIndex].Add(cardsToMove[0]);
                    break;

                default:
                    return false;
            }

            game.MoveCount--;
            return true;
        }
    }
}
