using Client.Games.Cards.Services;
using Client.Games.Cards.Models;

namespace TestProject1
{
    public partial class FreeCellSolverTests
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

            public int score { get; set; }
            public int deltaSequenceCount { get; set; }
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
            public override string ToString() => $"{CardMoved} {sourceType}[{sourceIndex}] -> {targetType}[{targetIndex}] (cards: {cardCount}, score: {score})";

            public bool UnApplyMove(FreeCellGameBase game)
            {
                var cardIndex = -1;
                if (targetType == SourceType.Tableau)
                {
                    cardIndex = game.Tableau[targetIndex].Count - cardCount;
                }
                // use the TryMove method to unapply this move to the game in memory by reversing the source and target
                game.Selection = new CardSelection
                {
                    SourceType = targetType,
                    SourceIndex = targetIndex,
                    CardIndex = cardIndex // not used for tableau to tableau moves since we always move from the bottom of the column, and not used for freecell or foundation moves since they only have one card
                };
                var didMove = game.TryMove(sourceType, sourceIndex);
                if (didMove)
                {
                    game.MoveCount -= 2; // since TryMove increments the move count, we need to decrement it by 2 to account for the move and unmove
                }
                return didMove;

            }
        }
    }
}
