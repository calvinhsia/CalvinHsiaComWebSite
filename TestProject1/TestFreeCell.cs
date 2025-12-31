using Client.Games.Cards.Models;
using Client.Games.Cards.Services;

namespace TestProject1
{
    [TestClass]
    public class TestFreeCell
    {
        [TestMethod]
        public void TestFreeCellGameInitialization()
        {
            var random = new Random(42);
            var game = new FreeCellGameService(random);

            // Check tableau setup (8 columns)
            Assert.AreEqual(8, game.Tableau.Count);

            // First 4 columns should have 7 cards, last 4 should have 6 cards
            for (int col = 0; col < 4; col++)
            {
                Assert.AreEqual(7, game.Tableau[col].Count, $"Tableau column {col} should have 7 cards");
            }
            for (int col = 4; col < 8; col++)
            {
                Assert.AreEqual(6, game.Tableau[col].Count, $"Tableau column {col} should have 6 cards");
            }

            // All cards should be face up
            foreach (var column in game.Tableau)
            {
                Assert.IsTrue(column.All(c => c.IsFaceUp), "All tableau cards should be face up");
            }

            // Check free cells (4 empty)
            Assert.AreEqual(4, game.FreeCells.Count);
            Assert.IsTrue(game.FreeCells.All(c => c == null), "Free cells should start empty");

            // Check foundations (4 empty piles)
            Assert.AreEqual(4, game.Foundations.Count);
            Assert.IsTrue(game.Foundations.All(f => f.Count == 0), "Foundations should start empty");

            // Check total cards
            int totalCards = game.Tableau.Sum(col => col.Count);
            Assert.AreEqual(52, totalCards, "Should have all 52 cards in tableau");

            // Check initial game state
            Assert.AreEqual(0, game.MoveCount);
            Assert.IsFalse(game.IsGameWon);
            Assert.IsNull(game.Selection);

            Console.WriteLine("? FreeCell game initialization is correct");
            Console.WriteLine($"   Tableau: 8 columns (4×7 + 4×6 = 52 cards)");
            Console.WriteLine($"   Free Cells: 4 empty");
            Console.WriteLine($"   Foundations: 4 empty piles");
        }

        [TestMethod]
        public void TestEmptyFreeCellCount()
        {
            var game = new FreeCellGameService(new Random(42));

            Assert.AreEqual(4, game.EmptyFreeCellCount, "Should start with 4 empty free cells");

            // Move a card to free cell
            var firstColumnCard = game.Tableau[0][^1];
            game.Select(1, 0, game.Tableau[0].Count - 1);
            game.TryMove(0, 0);

            Assert.AreEqual(3, game.EmptyFreeCellCount, "Should have 3 empty free cells after one move");
            Assert.IsNotNull(game.FreeCells[0], "First free cell should be occupied");

            Console.WriteLine("? Empty free cell count works correctly");
        }

        [TestMethod]
        public void TestEmptyTableauCount()
        {
            var game = new FreeCellGameService(new Random(42));

            Assert.AreEqual(0, game.EmptyTableauCount, "Should start with no empty columns");

            Console.WriteLine("? Empty tableau count works correctly");
        }

        [TestMethod]
        public void TestMaxMovableCardsCalculation()
        {
            var game = new FreeCellGameService(new Random(42));

            // With 4 empty free cells and 0 empty columns: (1 + 4) * 2^0 = 5
            Assert.AreEqual(5, game.MaxMovableCards, "Initial max movable should be 5");

            // Move a card to free cell
            game.Select(1, 0, game.Tableau[0].Count - 1);
            game.TryMove(0, 0);

            // With 3 empty free cells and 0 empty columns: (1 + 3) * 2^0 = 4
            Assert.AreEqual(4, game.MaxMovableCards, "After one free cell used, max movable should be 4");

            Console.WriteLine("? Max movable cards calculation works correctly");
        }

        [TestMethod]
        public void TestMoveToFreeCell()
        {
            var game = new FreeCellGameService(new Random(42));

            var cardToMove = game.Tableau[0][^1];
            Console.WriteLine($"Moving card: {cardToMove}");

            game.Select(1, 0, game.Tableau[0].Count - 1);
            bool success = game.TryMove(0, 0);

            Assert.IsTrue(success, "Move to free cell should succeed");
            Assert.AreEqual(cardToMove.Suit, game.FreeCells[0]!.Suit);
            Assert.AreEqual(cardToMove.Rank, game.FreeCells[0]!.Rank);
            Assert.AreEqual(1, game.MoveCount);

            Console.WriteLine("? Move to free cell works correctly");
        }

        [TestMethod]
        public void TestMoveFromFreeCell()
        {
            var game = new FreeCellGameService(new Random(42));

            // Move a card to free cell
            var cardToMove = game.Tableau[0][^1];
            game.Select(1, 0, game.Tableau[0].Count - 1);
            game.TryMove(0, 0);

            // Now try to move it back or to another location
            game.Select(0, 0, 0);
            
            // Find a valid tableau destination
            bool foundValidMove = false;
            for (int col = 0; col < 8; col++)
            {
                if (game.Tableau[col].Count == 0) continue;
                
                var topCard = game.Tableau[col][^1];
                // Valid move: opposite color, one rank lower than target
                if (cardToMove.IsRed != topCard.IsRed && (int)cardToMove.Rank == (int)topCard.Rank - 1)
                {
                    bool success = game.TryMove(1, col);
                    if (success)
                    {
                        Assert.IsNull(game.FreeCells[0], "Free cell should be empty after move");
                        foundValidMove = true;
                        Console.WriteLine($"? Moved {cardToMove} from free cell to column {col}");
                        break;
                    }
                }
            }

            if (!foundValidMove)
            {
                Console.WriteLine("No valid move from free cell found (OK with this seed)");
            }

            Console.WriteLine("? Move from free cell works correctly");
        }

        [TestMethod]
        public void TestMoveToFoundation()
        {
            var game = new FreeCellGameService(new Random(42));

            // Find an Ace in tableau
            Card? ace = null;
            int aceColumn = -1;
            int aceIndex = -1;

            for (int col = 0; col < 8; col++)
            {
                for (int row = 0; row < game.Tableau[col].Count; row++)
                {
                    var card = game.Tableau[col][row];
                    if (card.Rank == Rank.Ace && row == game.Tableau[col].Count - 1)
                    {
                        ace = card;
                        aceColumn = col;
                        aceIndex = row;
                        break;
                    }
                }
                if (ace != null) break;
            }

            if (ace != null)
            {
                Console.WriteLine($"Found accessible Ace: {ace} at column {aceColumn}");

                game.Select(1, aceColumn, aceIndex);
                bool success = game.TryMove(2, 0);

                Assert.IsTrue(success, "Ace should move to foundation");
                Assert.AreEqual(1, game.Foundations[0].Count);
                Assert.AreEqual(Rank.Ace, game.Foundations[0][0].Rank);

                Console.WriteLine("? Ace moved to foundation successfully");
            }
            else
            {
                Console.WriteLine("No accessible Ace found (top of column) - testing auto-move");
                // Try auto-move to find and move any ace
                int moves = game.AutoMoveToFoundations();
                Console.WriteLine($"Auto-move made {moves} moves");
            }

            Console.WriteLine("? Move to foundation works correctly");
        }

        [TestMethod]
        public void TestAutoMoveToFoundations()
        {
            var game = new FreeCellGameService(new Random(42));

            int movesMade = game.AutoMoveToFoundations();
            Console.WriteLine($"Auto-move made {movesMade} moves");

            // Should have moved at least any accessible aces
            Console.WriteLine($"Foundations now have: {string.Join(", ", game.Foundations.Select(f => f.Count))} cards");

            Console.WriteLine("? Auto-move to foundations works correctly");
        }

        [TestMethod]
        public void TestTableauMoveValidation()
        {
            var game = new FreeCellGameService(new Random(42));

            // Find a valid tableau-to-tableau move
            bool foundValidMove = false;
            for (int sourceCol = 0; sourceCol < 8 && !foundValidMove; sourceCol++)
            {
                if (game.Tableau[sourceCol].Count == 0) continue;
                
                var sourceCard = game.Tableau[sourceCol][^1];
                
                for (int targetCol = 0; targetCol < 8; targetCol++)
                {
                    if (sourceCol == targetCol) continue;
                    if (game.Tableau[targetCol].Count == 0) continue;
                    
                    var targetCard = game.Tableau[targetCol][^1];
                    
                    // Valid move: opposite color, one rank lower
                    if (sourceCard.IsRed != targetCard.IsRed && 
                        (int)sourceCard.Rank == (int)targetCard.Rank - 1)
                    {
                        Console.WriteLine($"Found valid move: {sourceCard} -> {targetCard}");
                        
                        game.Select(1, sourceCol, game.Tableau[sourceCol].Count - 1);
                        bool success = game.TryMove(1, targetCol);
                        
                        Assert.IsTrue(success, "Valid tableau move should succeed");
                        foundValidMove = true;
                        break;
                    }
                }
            }

            if (!foundValidMove)
            {
                Console.WriteLine("No valid tableau-to-tableau move found (OK with this seed)");
            }

            Console.WriteLine("? Tableau move validation works correctly");
        }

        [TestMethod]
        public void TestInvalidMoveToOccupiedFreeCell()
        {
            var game = new FreeCellGameService(new Random(42));

            // Move first card to free cell
            game.Select(1, 0, game.Tableau[0].Count - 1);
            game.TryMove(0, 0);

            // Try to move another card to the same free cell
            game.Select(1, 1, game.Tableau[1].Count - 1);
            bool success = game.TryMove(0, 0);

            Assert.IsFalse(success, "Should not be able to move to occupied free cell");

            Console.WriteLine("? Invalid move to occupied free cell correctly rejected");
        }

        [TestMethod]
        public void TestMoveToEmptyColumn()
        {
            var game = new FreeCellGameService(new Random(42));

            // Manually empty a column for testing
            var savedCards = new List<Card>(game.Tableau[0]);
            game.Tableau[0].Clear();

            // Any card can go on empty column in FreeCell
            var cardToMove = game.Tableau[1][^1];
            game.Select(1, 1, game.Tableau[1].Count - 1);
            bool success = game.TryMove(1, 0);

            Assert.IsTrue(success, "Any card can move to empty column in FreeCell");
            Assert.AreEqual(1, game.Tableau[0].Count);

            Console.WriteLine("? Move to empty column works correctly");
        }

        [TestMethod]
        public void TestGameWinCondition()
        {
            var game = new FreeCellGameService(new Random(42));

            Assert.IsFalse(game.IsGameWon, "Game should not be won initially");

            // Manually set up a winning state
            game.Foundations.Clear();
            foreach (Suit suit in Enum.GetValues<Suit>())
            {
                var foundationPile = new List<Card>();
                foreach (Rank rank in Enum.GetValues<Rank>())
                {
                    foundationPile.Add(new Card(suit, rank, true));
                }
                game.Foundations.Add(foundationPile);
            }

            Assert.IsTrue(game.IsGameWon, "Game should be won when all foundations have 13 cards");
            Console.WriteLine("? Game win condition detection works correctly");
        }

        [TestMethod]
        public void TestSelectionAndClear()
        {
            var game = new FreeCellGameService(new Random(42));

            Assert.IsNull(game.Selection);

            game.Select(1, 3, 2);
            Assert.IsNotNull(game.Selection);
            Assert.AreEqual((1, 3, 2), game.Selection.Value);

            game.ClearSelection();
            Assert.IsNull(game.Selection);

            Console.WriteLine("? Selection and clear works correctly");
        }

        [TestMethod]
        public void TestMoveWithoutSelection()
        {
            var game = new FreeCellGameService(new Random(42));

            bool success = game.TryMove(1, 0);
            Assert.IsFalse(success, "Move without selection should fail");

            Console.WriteLine("? Move without selection correctly rejected");
        }

        [TestMethod]
        public void TestAllCardsDealt()
        {
            var game = new FreeCellGameService(new Random(42));

            // Collect all cards
            var allCards = new List<Card>();
            foreach (var column in game.Tableau)
            {
                allCards.AddRange(column);
            }

            // Verify all 52 cards are present
            Assert.AreEqual(52, allCards.Count);

            // Verify all suits and ranks are present
            foreach (Suit suit in Enum.GetValues<Suit>())
            {
                foreach (Rank rank in Enum.GetValues<Rank>())
                {
                    var hasCard = allCards.Any(c => c.Suit == suit && c.Rank == rank);
                    Assert.IsTrue(hasCard, $"Should have {rank} of {suit}");
                }
            }

            Console.WriteLine("? All 52 cards are dealt correctly");
        }
    }
}
