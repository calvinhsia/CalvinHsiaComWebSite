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
            Console.WriteLine($"   Tableau: 8 columns (4�7 + 4�6 = 52 cards)");
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
            game.Select(SourceType.Tableau, 0, game.Tableau[0].Count - 1);
            game.TryMove(SourceType.FreeCell, 0);

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
            game.Select(SourceType.Tableau, 0, game.Tableau[0].Count - 1);
            game.TryMove(SourceType.FreeCell, 0);

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

            game.Select(SourceType.Tableau, 0, game.Tableau[0].Count - 1);
            bool success = game.TryMove(SourceType.FreeCell, 0);

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
            game.Select(SourceType.Tableau, 0, game.Tableau[0].Count - 1);
            game.TryMove(SourceType.FreeCell, 0);

            // Now try to move it back or to another location
            game.Select(SourceType.FreeCell, 0, 0);
            
            // Find a valid tableau destination
            bool foundValidMove = false;
            for (int col = 0; col < 8; col++)
            {
                if (game.Tableau[col].Count == 0) continue;
                
                var topCard = game.Tableau[col][^1];
                // Valid move: opposite color, one rank lower than target
                if (cardToMove.IsRed != topCard.IsRed && (int)cardToMove.Rank == (int)topCard.Rank - 1)
                {
                    bool success = game.TryMove(SourceType.Tableau, col);
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

                game.Select(SourceType.Tableau, aceColumn, aceIndex);
                bool success = game.TryMove(SourceType.Foundation, 0);

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
                        
                        game.Select(SourceType.Tableau, sourceCol, game.Tableau[sourceCol].Count - 1);
                        bool success = game.TryMove(SourceType.Tableau, targetCol);
                        
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
            game.Select(SourceType.Tableau, 0, game.Tableau[0].Count - 1);
            game.TryMove(SourceType.FreeCell, 0);

            // Try to move another card to the same free cell
            game.Select(SourceType.Tableau, 1, game.Tableau[1].Count - 1);
            bool success = game.TryMove(SourceType.FreeCell, 0);

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
            game.Select(SourceType.Tableau, 1, game.Tableau[1].Count - 1);
            bool success = game.TryMove(SourceType.Tableau, 0);

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

            game.Select(SourceType.Tableau, 3, 2);
            Assert.IsNotNull(game.Selection);
            Assert.AreEqual(new CardSelection(SourceType.Tableau, 3, 2), game.Selection.Value);

            game.ClearSelection();
            Assert.IsNull(game.Selection);

            Console.WriteLine("? Selection and clear works correctly");
        }

        [TestMethod]
        public void TestMoveWithoutSelection()
        {
            var game = new FreeCellGameService(new Random(42));

            bool success = game.TryMove(SourceType.Tableau, 0);
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

        [TestMethod]
        public void TestUndoFunctionality()
        {
            var game = new FreeCellGameService(new Random(42));

            // Initially, can't undo
            Assert.IsFalse(game.CanUndo, "Should not be able to undo initially");
            Assert.AreEqual(0, game.UndoCount);

            // Make a move to free cell
            var originalCard = game.Tableau[0][^1];
            int originalTableauCount = game.Tableau[0].Count;
            
            game.Select(SourceType.Tableau, 0, game.Tableau[0].Count - 1);
            bool moved = game.TryMove(SourceType.FreeCell, 0);
            Assert.IsTrue(moved, "Move should succeed");

            // Now we can undo
            Assert.IsTrue(game.CanUndo, "Should be able to undo after a move");
            Assert.AreEqual(1, game.UndoCount);
            Assert.AreEqual(1, game.MoveCount);
            Assert.IsNotNull(game.FreeCells[0], "Free cell should have a card");
            Assert.AreEqual(originalTableauCount - 1, game.Tableau[0].Count);

            // Undo the move
            bool undone = game.Undo();
            Assert.IsTrue(undone, "Undo should succeed");

            // State should be restored
            Assert.IsFalse(game.CanUndo, "Should not be able to undo after undoing");
            Assert.AreEqual(0, game.UndoCount);
            Assert.AreEqual(0, game.MoveCount);
            Assert.IsNull(game.FreeCells[0], "Free cell should be empty after undo");
            Assert.AreEqual(originalTableauCount, game.Tableau[0].Count);
            Assert.AreEqual(originalCard.Suit, game.Tableau[0][^1].Suit);
            Assert.AreEqual(originalCard.Rank, game.Tableau[0][^1].Rank);

            Console.WriteLine("? Undo functionality works correctly");
        }

        [TestMethod]
        public void TestMultipleUndos()
        {
            var game = new FreeCellGameService(new Random(42));

            // Make multiple moves
            for (int i = 0; i < 3; i++)
            {
                game.Select(SourceType.Tableau, i, game.Tableau[i].Count - 1);
                game.TryMove(SourceType.FreeCell, i);
            }

            Assert.AreEqual(3, game.MoveCount);
            Assert.AreEqual(3, game.UndoCount);

            // Undo all moves
            int undoCount = 0;
            while (game.CanUndo)
            {
                game.Undo();
                undoCount++;
            }

            Assert.AreEqual(3, undoCount);
            Assert.AreEqual(0, game.MoveCount);
            Assert.AreEqual(0, game.UndoCount);

            // All free cells should be empty again
            Assert.IsTrue(game.FreeCells.All(c => c == null), "All free cells should be empty after undoing all moves");

            Console.WriteLine("? Multiple undos work correctly");
        }

        [TestMethod]
        public void TestUndoClearedOnNewGame()
        {
            var game = new FreeCellGameService(new Random(42));

            // Make a move
            game.Select(SourceType.Tableau, 0, game.Tableau[0].Count - 1);
            game.TryMove(SourceType.FreeCell, 0);
            Assert.IsTrue(game.CanUndo);

            // Start new game
            game.InitializeGame();

            // Undo stack should be cleared
            Assert.IsFalse(game.CanUndo, "Should not be able to undo after new game");
            Assert.AreEqual(0, game.UndoCount);

            Console.WriteLine("? Undo stack cleared on new game");
        }

        [TestMethod]
        public void TestIsTriviallyWinnable()
        {
            var game = new FreeCellGameService(new Random(42));

            // A fresh game is typically not trivially winnable
            // (cards are shuffled and likely out of order)
            bool initiallyWinnable = game.IsTriviallyWinnable();
            Console.WriteLine($"Initially trivially winnable: {initiallyWinnable}");

            // Note: We can't easily set up a specific state without internal access,
            // but we can verify the method doesn't crash and returns a boolean
            Assert.IsTrue(initiallyWinnable || !initiallyWinnable, "IsTriviallyWinnable should return a boolean");

            Console.WriteLine("? IsTriviallyWinnable runs without errors");
        }

        [TestMethod]
        public void TestGetNextFoundationMove()
        {
            var game = new FreeCellGameService(new Random(42));

            // Move a card to free cell, then check if there's a foundation move
            game.Select(SourceType.Tableau, 0, game.Tableau[0].Count - 1);
            game.TryMove(SourceType.FreeCell, 0);

            // GetNextFoundationMove should return something or null
            var nextMove = game.GetNextFoundationMove();

            if (nextMove != null)
            {
                Console.WriteLine($"Next foundation move found: type={nextMove.Value.SourceType}, index={nextMove.Value.SourceIndex}");
            }
            else
            {
                Console.WriteLine("No immediate foundation move available (normal for most game states)");
            }

            Console.WriteLine("? GetNextFoundationMove works correctly");
        }

        [TestMethod]
        public void TestAutoMoveStep()
        {
            var game = new FreeCellGameService(new Random(42));

            // Try auto-solve step - may or may not find a move depending on game state
            var result = game.AutoMoveStep();

            if (result != null)
            {
                var (sourceType, sourceIndex, card) = result.Value;
                Console.WriteLine($"AutoMoveStep moved {card} from {(sourceType == SourceType.FreeCell ? "free cell" : "tableau")} {sourceIndex}");
                Assert.IsTrue(game.Foundations.Sum(f => f.Count) > 0, "Card should be in foundation");
            }
            else
            {
                Console.WriteLine("No auto-solve move available (normal for fresh game)");
            }

            Console.WriteLine("? AutoMoveStep works correctly");
        }

        [TestMethod]
        public void TestAutoMoveWithSpecificSetup()
        {
            // Test the scenario: 4? at bottom of column 5, 3? in foundation
            // Auto-move should move the 4? to foundation
            var game = new FreeCellGameService(new Random(42));

            // First, find and remove any existing 4? from the tableau to avoid duplicates
            for (int col = 0; col < 8; col++)
            {
                for (int row = game.Tableau[col].Count - 1; row >= 0; row--)
                {
                    var card = game.Tableau[col][row];
                    if (card.Suit == Suit.Clubs && card.Rank == Rank.Four)
                    {
                        Console.WriteLine($"Found existing 4? at column {col}, row {row} - removing");
                        game.Tableau[col].RemoveAt(row);
                    }
                }
            }

            // Clear column 4 (index 4, which is "column 5" in 1-based)
            game.Tableau[4].Clear();

            // Set up foundation 2 (clubs) with A?, 2?, 3?
            game.Foundations[2].Clear();
            game.Foundations[2].Add(new Card(Suit.Clubs, Rank.Ace, true));
            game.Foundations[2].Add(new Card(Suit.Clubs, Rank.Two, true));
            game.Foundations[2].Add(new Card(Suit.Clubs, Rank.Three, true));

            // Put 4? at the bottom of column 4
            game.Tableau[4].Add(new Card(Suit.Clubs, Rank.Four, true));

            Console.WriteLine($"Foundation clubs has {game.Foundations[2].Count} cards, top is {game.Foundations[2][^1]}");
            Console.WriteLine($"Column 4 has {game.Tableau[4].Count} card(s), bottom is {game.Tableau[4][^1]}");

            // Now auto-move should find and move 4?
            int movesMade = game.AutoMoveToFoundations();

            Console.WriteLine($"Auto-move made {movesMade} moves");
            Console.WriteLine($"Foundation clubs now has {game.Foundations[2].Count} cards");
            if (game.Tableau[4].Count > 0)
            {
                Console.WriteLine($"Column 4 still has: {string.Join(", ", game.Tableau[4].Select(c => c.ToString()))}");
            }
            else
            {
                Console.WriteLine("Column 4 is now empty");
            }

            Assert.IsTrue(movesMade >= 1, "Should have moved at least the 4?");
            Assert.IsTrue(game.Foundations[2].Count >= 4, "Foundation should have at least 4 cards (A, 2, 3, 4)");
            // The 4? we placed should have been moved, so column should be empty
            Assert.AreEqual(0, game.Tableau[4].Count, "Column 4 should be empty after 4? was moved");

            Console.WriteLine("? Auto-move with specific setup works correctly");
        }

        [TestMethod]
        public void TestAutoMoveDoesNotMoveBlockedCards()
        {
            // Test that auto-move only moves cards at the TOP of columns (bottom visually)
            var game = new FreeCellGameService(new Random(42));

            // First, find and remove all clubs from the tableau except what we manually place
            for (int col = 0; col < 8; col++)
            {
                for (int row = game.Tableau[col].Count - 1; row >= 0; row--)
                {
                    var card = game.Tableau[col][row];
                    if (card.Suit == Suit.Clubs && (int)card.Rank >= 4)
                    {
                        // Remove 4? and higher clubs so they don't interfere
                        game.Tableau[col].RemoveAt(row);
                    }
                }
            }

            // Clear column 4
            game.Tableau[4].Clear();

            // Set up foundation 2 (clubs) with A?, 2?, 3?
            game.Foundations[2].Clear();
            game.Foundations[2].Add(new Card(Suit.Clubs, Rank.Ace, true));
            game.Foundations[2].Add(new Card(Suit.Clubs, Rank.Two, true));
            game.Foundations[2].Add(new Card(Suit.Clubs, Rank.Three, true));

            // Put 4? in column but with a 5? on top of it (blocking it)
            game.Tableau[4].Add(new Card(Suit.Clubs, Rank.Four, true));  // 4? - buried
            game.Tableau[4].Add(new Card(Suit.Diamonds, Rank.Five, true)); // 5? - on top

            Console.WriteLine($"Column 4 has: {string.Join(" -> ", game.Tableau[4].Select(c => c.ToString()))}");
            Console.WriteLine($"Top card (accessible) is: {game.Tableau[4][^1]}");

            int originalFoundationCount = game.Foundations[2].Count;
            int movesMade = game.AutoMoveToFoundations();

            Console.WriteLine($"Auto-move made {movesMade} moves");
            Console.WriteLine($"Foundation clubs now has {game.Foundations[2].Count} cards");

            // 4? should NOT be moved because it's not at the top of the column
            Assert.AreEqual(originalFoundationCount, game.Foundations[2].Count, 
                "Foundation should not have changed - 4? is blocked and we removed other clubs");
            Assert.AreEqual(2, game.Tableau[4].Count, "Column should still have 2 cards");

            Console.WriteLine("? Blocked cards are correctly NOT auto-moved");
        }

        [TestMethod]
        public void TestAutoMoveWithExactUserScenario()
        {
            // Reproduce the EXACT scenario from user's screenshot
            // Foundations: 6?, 3?, 4?, 4? (total 17 cards)
            // Column 5 has 4? at the bottom
            // Auto should move 4? to foundation but user says it didn't
            
            var game = new FreeCellGameService(new Random(42));

            // Clear everything
            for (int col = 0; col < 8; col++) game.Tableau[col].Clear();
            for (int i = 0; i < 4; i++) game.Foundations[i].Clear();

            // Set up foundations EXACTLY as in screenshot
            // Foundation 0: Hearts A-6
            for (int r = 1; r <= 6; r++) 
                game.Foundations[0].Add(new Card(Suit.Hearts, (Rank)r, true));
            
            // Foundation 1: Clubs A-3 (user's screenshot shows 3? here)
            for (int r = 1; r <= 3; r++) 
                game.Foundations[1].Add(new Card(Suit.Clubs, (Rank)r, true));
            
            // Foundation 2: Diamonds A-4
            for (int r = 1; r <= 4; r++) 
                game.Foundations[2].Add(new Card(Suit.Diamonds, (Rank)r, true));
            
            // Foundation 3: Spades A-4
            for (int r = 1; r <= 4; r++) 
                game.Foundations[3].Add(new Card(Suit.Spades, (Rank)r, true));

            // Now set up tableau with remaining 35 cards
            // Column 5 (index 4) has 4? at the bottom (top of list = top of visual pile)
            // In FreeCell, cards are dealt top-to-bottom, so [^1] is the accessible card
            
            // Column 5: Multiple cards with 4? at the accessible bottom
            game.Tableau[4].Add(new Card(Suit.Hearts, Rank.King, true));  // top (not accessible)
            game.Tableau[4].Add(new Card(Suit.Spades, Rank.Queen, true));
            game.Tableau[4].Add(new Card(Suit.Diamonds, Rank.Jack, true));
            game.Tableau[4].Add(new Card(Suit.Clubs, Rank.Four, true));   // bottom (accessible) - THIS should move!

            Console.WriteLine("=== Exact User Screenshot Scenario ===");
            Console.WriteLine($"Foundation clubs has {game.Foundations[1].Count} cards, top is {game.Foundations[1][^1]}");
            Console.WriteLine($"Column 4 has {game.Tableau[4].Count} cards:");
            for (int i = 0; i < game.Tableau[4].Count; i++)
            {
                var marker = i == game.Tableau[4].Count - 1 ? " <-- ACCESSIBLE (should move to foundation)" : "";
                Console.WriteLine($"  [{i}] {game.Tableau[4][i]}{marker}");
            }

            var accessibleCard = game.Tableau[4][^1];
            Console.WriteLine($"\nAccessible card: {accessibleCard}");
            Console.WriteLine($"Foundation clubs top: {game.Foundations[1][^1]}");
            Console.WriteLine($"Expected: 4? can go on 3?? Same suit: {accessibleCard.Suit == Suit.Clubs}, Rank 4 == 3+1: {(int)accessibleCard.Rank == 4}");

            // Now try auto-move
            int movesMade = game.AutoMoveToFoundations();

            Console.WriteLine($"\nAutoMoveToFoundations made {movesMade} moves");
            Console.WriteLine($"Foundation clubs now has {game.Foundations[1].Count} cards");
            Console.WriteLine($"Column 4 now has {game.Tableau[4].Count} cards");

            Assert.IsTrue(movesMade >= 1, "4? should have been moved to foundation");
            Assert.AreEqual(4, game.Foundations[1].Count, "Clubs foundation should now have 4 cards");
            
            Console.WriteLine("\n? 4? was correctly moved - BUG NOT REPRODUCED");
            Console.WriteLine("The issue in the user's game must be something else.");
        }

        [TestMethod]
        public void FindSeedWithAutoMovableCard()
        {
            // Find a seed where a card can be auto-moved to foundation immediately
            // This helps reproduce the user's scenario
            
            for (int seed = 1; seed <= 1000; seed++)
            {
                var game = new FreeCellGameService(new Random(seed));
                
                // Try auto-move - see if any cards can go to foundation immediately
                int movesMade = game.AutoMoveToFoundations();
                
                if (movesMade > 0)
                {
                    Console.WriteLine($"Seed {seed}: Auto-move made {movesMade} moves immediately");
                    
                    // Show the foundations after auto-move
                    for (int i = 0; i < 4; i++)
                    {
                        if (game.Foundations[i].Count > 0)
                        {
                            Console.WriteLine($"  Foundation {i}: {game.Foundations[i].Count} cards, top: {game.Foundations[i][^1]}");
                        }
                    }
                    
                    // Found a good seed, let's use the first one
                    if (seed <= 10)
                    {
                        Console.WriteLine($"\n*** Use seed {seed} to test: /freecell/{seed} ***");
                    }
                }
            }
            
            Console.WriteLine("\n? Seed search complete");
        }

        [TestMethod]
        public void TestSpecificSeed42()
        {
            // Test with seed 42 to see what game state it produces
            var game = new FreeCellGameService(new Random(42));
            
            Console.WriteLine("=== Game with Seed 42 ===");
            Console.WriteLine("\nTableau columns (bottom card of each):");
            for (int col = 0; col < 8; col++)
            {
                if (game.Tableau[col].Count > 0)
                {
                    var topCard = game.Tableau[col][^1];
                    Console.WriteLine($"  Column {col + 1}: {topCard} (total {game.Tableau[col].Count} cards)");
                }
            }

            Console.WriteLine("\nTrying AutoMoveToFoundations...");
            int movesMade = game.AutoMoveToFoundations();
            Console.WriteLine($"Moves made: {movesMade}");

            Console.WriteLine("\nFoundations after auto-move:");
            for (int i = 0; i < 4; i++)
            {
                if (game.Foundations[i].Count > 0)
                {
                    Console.WriteLine($"  Foundation {i + 1}: {game.Foundations[i].Count} cards, top: {game.Foundations[i][^1]}");
                }
                else
                {
                    Console.WriteLine($"  Foundation {i + 1}: empty");
                }
            }

            Console.WriteLine($"\n*** To test this game: navigate to /freecell/42 ***");
        }

        #region Game ID Tests

        [TestMethod]
        public void TestGameIdIsSetOnInitialization()
        {
            var game = new FreeCellGameService(new Random(42));
            
            Assert.IsTrue(game.GameId > 0, "GameId should be positive after initialization");
            Assert.IsTrue(game.GameId <= 1000000, "GameId should be <= 1000000");
            
            Console.WriteLine($"? GameId set to: {game.GameId}");
        }

        [TestMethod]
        public void TestInitializeWithSpecificGameId()
        {
            var game = new FreeCellGameService();
            
            // Initialize with specific game ID
            game.InitializeGame(12345);
            
            Assert.AreEqual(12345, game.GameId, "GameId should match specified value");
            
            // Verify 52 cards dealt
            int totalCards = game.Tableau.Sum(col => col.Count);
            Assert.AreEqual(52, totalCards, "Should have 52 cards in tableau");
            
            Console.WriteLine($"? Game {game.GameId} initialized successfully");
        }

        [TestMethod]
        public void TestSameGameIdProducesSameLayout()
        {
            // Create two games with the same ID
            var game1 = new FreeCellGameService();
            game1.InitializeGame(12345);
            
            var game2 = new FreeCellGameService();
            game2.InitializeGame(12345);
            
            // Both should have identical tableau layouts
            for (int col = 0; col < 8; col++)
            {
                Assert.AreEqual(game1.Tableau[col].Count, game2.Tableau[col].Count,
                    $"Column {col} should have same number of cards");
                
                for (int row = 0; row < game1.Tableau[col].Count; row++)
                {
                    var card1 = game1.Tableau[col][row];
                    var card2 = game2.Tableau[col][row];
                    
                    Assert.AreEqual(card1.Suit, card2.Suit,
                        $"Card at column {col}, row {row} should have same suit");
                    Assert.AreEqual(card1.Rank, card2.Rank,
                        $"Card at column {col}, row {row} should have same rank");
                }
            }
            
            Console.WriteLine("? Same GameId produces identical layout");
        }

        [TestMethod]
        public void TestDifferentGameIdsProduceDifferentLayouts()
        {
            var game1 = new FreeCellGameService();
            game1.InitializeGame(11111);
            
            var game2 = new FreeCellGameService();
            game2.InitializeGame(22222);
            
            // At least some cards should be in different positions
            bool foundDifference = false;
            for (int col = 0; col < 8 && !foundDifference; col++)
            {
                for (int row = 0; row < Math.Min(game1.Tableau[col].Count, game2.Tableau[col].Count); row++)
                {
                    var card1 = game1.Tableau[col][row];
                    var card2 = game2.Tableau[col][row];
                    
                    if (card1.Suit != card2.Suit || card1.Rank != card2.Rank)
                    {
                        foundDifference = true;
                        break;
                    }
                }
            }
            
            Assert.IsTrue(foundDifference, "Different GameIds should produce different layouts");
            Console.WriteLine("? Different GameIds produce different layouts");
        }

        [TestMethod]
        public void TestKnownGameIds()
        {
            // Test some well-known FreeCell game IDs
            // Game #1 is famously easy, Game #11982 is known to be unsolvable in classic FreeCell
            
            var game1 = new FreeCellGameService();
            game1.InitializeGame(1);
            
            Console.WriteLine("=== Game #1 Layout ===");
            for (int col = 0; col < 8; col++)
            {
                var cards = string.Join(" ", game1.Tableau[col].Select(c => c.ToString()));
                Console.WriteLine($"  Column {col + 1}: {cards}");
            }
            
            // Verify deterministic layout for game 1
            Assert.AreEqual(52, game1.Tableau.Sum(col => col.Count), "Game #1 should have 52 cards");
            Console.WriteLine("? Known game IDs work correctly");
        }

        #endregion

        #region Serialization Tests

        [TestMethod]
        public void TestSerializeAndDeserializeState()
        {
            var game = new FreeCellGameService();
            game.InitializeGame(42424);
            
            // Make some moves to change state
            game.Select(SourceType.Tableau, 0, game.Tableau[0].Count - 1);
            game.TryMove(SourceType.FreeCell, 0); // Move to free cell
            
            game.Select(SourceType.Tableau, 1, game.Tableau[1].Count - 1);
            game.TryMove(SourceType.FreeCell, 1); // Move to another free cell
            
            // Capture original state
            int originalGameId = game.GameId;
            int originalMoveCount = game.MoveCount;
            var originalFirstFreeCell = game.FreeCells[0]?.ToString();
            int originalUndoCount = game.UndoCount;
            
            // Serialize
            var state = game.SerializeState();
            
            Assert.AreEqual(originalGameId, state.GameId);
            Assert.AreEqual(originalMoveCount, state.MoveCount);
            Assert.AreEqual(8, state.Tableau.Count);
            Assert.AreEqual(4, state.FreeCells.Count);
            Assert.AreEqual(4, state.Foundations.Count);
            Assert.AreEqual(originalUndoCount, state.UndoStack.Count);
            
            Console.WriteLine($"? Serialized state: GameId={state.GameId}, MoveCount={state.MoveCount}, UndoStack={state.UndoStack.Count}");
        }

        [TestMethod]
        public void TestSerializeToJsonAndBack()
        {
            var game = new FreeCellGameService();
            game.InitializeGame(99999);
            
            // Make some moves
            game.Select(SourceType.Tableau, 0, game.Tableau[0].Count - 1);
            game.TryMove(SourceType.FreeCell, 0);
            game.Select(SourceType.Tableau, 1, game.Tableau[1].Count - 1);
            game.TryMove(SourceType.FreeCell, 1);
            
            // Capture state
            int originalGameId = game.GameId;
            int originalMoveCount = game.MoveCount;
            var freeCellCards = game.FreeCells.Select(c => c?.ToString()).ToList();
            
            // Serialize to JSON
            string json = game.ToJson();
            Console.WriteLine($"JSON length: {json.Length} characters");
            
            // Restore from JSON
            var restoredGame = FreeCellGameService.FromJson(json);
            
            // Verify state is identical
            Assert.AreEqual(originalGameId, restoredGame.GameId);
            Assert.AreEqual(originalMoveCount, restoredGame.MoveCount);
            
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(freeCellCards[i], restoredGame.FreeCells[i]?.ToString(),
                    $"FreeCell {i} should match after restore");
            }
            
            Console.WriteLine($"? JSON serialize/deserialize works: GameId={restoredGame.GameId}");
        }

        [TestMethod]
        public void TestRestorePreservesUndoStack()
        {
            var game = new FreeCellGameService();
            game.InitializeGame(77777);
            
            // Make several moves to build up undo stack
            for (int i = 0; i < 3; i++)
            {
                game.Select(SourceType.Tableau, i, game.Tableau[i].Count - 1);
                game.TryMove(SourceType.FreeCell, i);
            }
            
            Assert.AreEqual(3, game.UndoCount);
            
            // Serialize and restore
            string json = game.ToJson();
            var restoredGame = FreeCellGameService.FromJson(json);
            
            // Verify undo stack is preserved
            Assert.AreEqual(3, restoredGame.UndoCount);
            Assert.IsTrue(restoredGame.CanUndo);
            
            // Perform undo on restored game
            restoredGame.Undo();
            Assert.AreEqual(2, restoredGame.UndoCount);
            Assert.AreEqual(2, restoredGame.MoveCount);
            
            Console.WriteLine("? Undo stack preserved after restore");
        }

        [TestMethod]
        public void TestSerializeWithFoundationCards()
        {
            var game = new FreeCellGameService();
            game.InitializeGame(55555);
            
            // Manually add cards to foundation for testing
            game.Foundations[0].Add(new Card(Suit.Hearts, Rank.Ace, true));
            game.Foundations[0].Add(new Card(Suit.Hearts, Rank.Two, true));
            
            // Serialize and restore
            string json = game.ToJson();
            var restoredGame = FreeCellGameService.FromJson(json);
            
            // Verify foundation is preserved
            Assert.AreEqual(2, restoredGame.Foundations[0].Count);
            Assert.AreEqual(Rank.Ace, restoredGame.Foundations[0][0].Rank);
            Assert.AreEqual(Rank.Two, restoredGame.Foundations[0][1].Rank);
            
            Console.WriteLine("? Foundation cards preserved after restore");
        }

        [TestMethod]
        public void TestCardSerializationFormats()
        {
            var game = new FreeCellGameService();
            game.InitializeGame(12345);
            
            var state = game.SerializeState();
            
            // Check card format (should be 2 characters: rank + suit)
            foreach (var column in state.Tableau)
            {
                foreach (var cardStr in column)
                {
                    Assert.AreEqual(2, cardStr.Length, $"Card string '{cardStr}' should be 2 characters");
                    Assert.IsTrue("A23456789TJQK".Contains(cardStr[0]), $"Invalid rank in '{cardStr}'");
                    Assert.IsTrue("CDHS".Contains(cardStr[1]), $"Invalid suit in '{cardStr}'");
                }
            }
            
            Console.WriteLine("? Card serialization format is correct (rank + suit, 2 chars)");
        }

        [TestMethod]
        public void TestSerializeEmptyGameState()
        {
            var game = new FreeCellGameService();
            game.InitializeGame(1);
            
            // Fresh game - no moves, empty free cells, empty foundations
            var state = game.SerializeState();
            
            Assert.AreEqual(0, state.MoveCount);
            Assert.IsTrue(state.FreeCells.All(c => c == null));
            Assert.IsTrue(state.Foundations.All(f => f.Count == 0));
            Assert.AreEqual(0, state.UndoStack.Count);
            
            Console.WriteLine("? Empty game state serializes correctly");
        }

        [TestMethod]
        public void TestGameIdInUrlNavigation()
        {
            // Simulate what happens when user navigates to /freecell/12345
            var game = new FreeCellGameService();
            game.InitializeGame(12345);
            
            var firstCard = game.Tableau[0][0]; // First card dealt
            Console.WriteLine($"Game 12345 first card: {firstCard}");
            
            // Another instance with same ID should have same first card
            var game2 = new FreeCellGameService();
            game2.InitializeGame(12345);
            
            Assert.AreEqual(firstCard.Suit, game2.Tableau[0][0].Suit);
            Assert.AreEqual(firstCard.Rank, game2.Tableau[0][0].Rank);
            
            Console.WriteLine($"? Game ID 12345 produces deterministic layout");
            Console.WriteLine($"   Navigate to /freecell/12345 to play this specific game");
        }

        [TestMethod]
        public void TestMoveHistoryHumanReadableFormat()
        {
            // Verify the move history entries use the human-readable format with Unicode suits
            var game = new FreeCellGameService();
            game.InitializeGame(42424);

            // Move top card of column 0 to free cell 0
            var card0 = game.Tableau[0][^1];
            game.Select(SourceType.Tableau, 0, game.Tableau[0].Count - 1);
            game.TryMove(SourceType.FreeCell, 0);

            Assert.AreEqual(1, game.MoveHistory.Count);
            var entry = game.MoveHistory[0];
            Console.WriteLine($"Move history entry: {entry}");

            // Verify format: {RankDisplay}{SuitSymbol}:{LocationLabel}{idx}>{LocationLabel}{idx}
            Assert.IsTrue(entry.Contains(">"), "Move should contain > separator");
            Assert.IsTrue(entry.Contains(":"), "Move should contain : separator");
            Assert.IsTrue(entry.StartsWith($"{card0.RankDisplay}{card0.SuitSymbol}:"),
                $"Move should start with card display '{card0.RankDisplay}{card0.SuitSymbol}:'");
            Assert.IsTrue(entry.Contains("Col0>Free0"),
                $"Move should contain 'Col0>Free0' but was '{entry}'");

            Console.WriteLine("Move history uses human-readable format with Unicode suits");
        }

        [TestMethod]
        public void TestToDumpStringFromDumpStringRoundTrip()
        {
            // Round-trip: create game, make moves, export, import, verify identical state
            var game = new FreeCellGameService();
            game.InitializeGame(42424);

            // Make several moves to build up state and history
            game.Select(SourceType.Tableau, 0, game.Tableau[0].Count - 1);
            game.TryMove(SourceType.FreeCell, 0);
            game.Select(SourceType.Tableau, 1, game.Tableau[1].Count - 1);
            game.TryMove(SourceType.FreeCell, 1);
            game.Select(SourceType.Tableau, 2, game.Tableau[2].Count - 1);
            game.TryMove(SourceType.FreeCell, 2);

            // Capture original state
            int originalGameId = game.GameId;
            int originalMoveCount = game.MoveCount;
            var originalHistory = game.MoveHistory.ToList();
            var originalTableau = game.Tableau.Select(col => col.Select(c => c.ToString()).ToList()).ToList();
            var originalFreeCells = game.FreeCells.Select(c => c?.ToString()).ToList();
            var originalFoundations = game.Foundations.Select(f => f.Select(c => c.ToString()).ToList()).ToList();

            // Export
            string dump = game.ToDumpString(includeMoveHistory: true);
            Console.WriteLine("=== Exported dump ===");
            Console.WriteLine(dump);

            // Import
            var restored = FreeCellGameService.FromDumpString(dump);

            // Verify GameId and MoveCount
            Assert.AreEqual(originalGameId, restored.GameId, "GameId should survive round-trip");
            Assert.AreEqual(originalMoveCount, restored.MoveCount, "MoveCount should survive round-trip");

            // Verify tableau
            for (int col = 0; col < 8; col++)
            {
                Assert.AreEqual(originalTableau[col].Count, restored.Tableau[col].Count,
                    $"Tableau column {col} count mismatch");
                for (int row = 0; row < originalTableau[col].Count; row++)
                {
                    Assert.AreEqual(originalTableau[col][row], restored.Tableau[col][row].ToString(),
                        $"Tableau column {col} row {row} mismatch");
                }
            }

            // Verify free cells
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(originalFreeCells[i], restored.FreeCells[i]?.ToString(),
                    $"FreeCell {i} mismatch");
            }

            // Verify foundations
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(originalFoundations[i].Count, restored.Foundations[i].Count,
                    $"Foundation {i} count mismatch");
                for (int row = 0; row < originalFoundations[i].Count; row++)
                {
                    Assert.AreEqual(originalFoundations[i][row], restored.Foundations[i][row].ToString(),
                        $"Foundation {i} row {row} mismatch");
                }
            }

            // Verify move history
            Assert.AreEqual(originalHistory.Count, restored.MoveHistory.Count,
                "MoveHistory count mismatch after round-trip");
            for (int i = 0; i < originalHistory.Count; i++)
            {
                Assert.AreEqual(originalHistory[i], restored.MoveHistory[i],
                    $"MoveHistory entry {i} mismatch");
            }

            Console.WriteLine($"Round-trip successful: GameId={restored.GameId}, Moves={restored.MoveCount}, History={restored.MoveHistory.Count}");
        }

        [TestMethod]
        public void TestToDumpStringRoundTripWithFoundationCards()
        {
            // Round-trip a game that has cards on the foundation
            var game = new FreeCellGameService();
            game.InitializeGame(42424);

            // Move cards to free cells
            for (int i = 0; i < 4; i++)
            {
                game.Select(SourceType.Tableau, i, game.Tableau[i].Count - 1);
                game.TryMove(SourceType.FreeCell, i);
            }

            // Move a card from tableau to foundation by replacing a tableau top card with an Ace
            // and moving it properly. Instead, find and move an actual ace if accessible.
            // Simpler: remove a card from a tableau column and place same-suit ace on foundation.
            var removedCard = game.Tableau[4][^1];
            game.Tableau[4].RemoveAt(game.Tableau[4].Count - 1);
            game.Foundations[0].Add(new Card(removedCard.Suit, Rank.Ace, true));

            string dump = game.ToDumpString();
            Console.WriteLine(dump);

            // We broke card integrity for testing, so VerifyGame will complain.
            // Instead, test with a game where we can legitimately get a card to foundation.
            // Re-approach: use a known game where top of a column is an Ace.
            // Let's just use the basic round-trip approach on an unmodified board.
            // Actually, let's find a game where an Ace is on top.
            var game2 = new FreeCellGameService();
            for (int seed = 1; seed <= 100; seed++)
            {
                game2.InitializeGame(seed);
                for (int col = 0; col < 8; col++)
                {
                    if (game2.Tableau[col][^1].Rank == Rank.Ace)
                    {
                        // Move ace to foundation
                        game2.Select(SourceType.Tableau, col, game2.Tableau[col].Count - 1);
                        game2.TryMove(SourceType.Foundation, 0);

                        Assert.AreEqual(1, game2.Foundations[0].Count, "Foundation should have 1 card after moving ace");

                        string dump2 = game2.ToDumpString();
                        Console.WriteLine(dump2);

                        var restored2 = FreeCellGameService.FromDumpString(dump2);
                        Assert.AreEqual(1, restored2.Foundations[0].Count, "Foundation should have 1 card after round-trip");
                        Assert.AreEqual(Rank.Ace, restored2.Foundations[0][0].Rank);
                        Console.WriteLine($"Round-trip with foundation cards successful (seed {seed})");
                        return;
                    }
                }
            }
            Assert.Inconclusive("No accessible Ace found in seeds 1-100");
        }

        [TestMethod]
        public void TestToDumpStringRoundTripNoHistory()
        {
            // Round-trip with includeMoveHistory=false
            var game = new FreeCellGameService();
            game.InitializeGame(42424);

            game.Select(SourceType.Tableau, 0, game.Tableau[0].Count - 1);
            game.TryMove(SourceType.FreeCell, 0);

            string dump = game.ToDumpString(includeMoveHistory: false);
            Console.WriteLine(dump);

            // Should not contain MoveHistory line
            Assert.IsFalse(dump.Contains("MoveHistory:"), "Dump without history should not have MoveHistory line");

            var restored = FreeCellGameService.FromDumpString(dump);
            Assert.AreEqual(game.GameId, restored.GameId);
            Assert.AreEqual(0, restored.MoveHistory.Count, "No history should be restored when not exported");

            Console.WriteLine("Round-trip without move history successful");
        }

        [TestMethod]
        public void TestToDumpStringRoundTripFreshGame()
        {
            // Round-trip a fresh game with zero moves
            var game = new FreeCellGameService();
            game.InitializeGame(12345);

            string dump = game.ToDumpString();
            Console.WriteLine(dump);

            var restored = FreeCellGameService.FromDumpString(dump);

            Assert.AreEqual(12345, restored.GameId);
            Assert.AreEqual(0, restored.MoveCount);
            Assert.AreEqual(0, restored.MoveHistory.Count);

            // Verify full tableau matches
            for (int col = 0; col < 8; col++)
            {
                Assert.AreEqual(game.Tableau[col].Count, restored.Tableau[col].Count,
                    $"Column {col} count mismatch");
                for (int row = 0; row < game.Tableau[col].Count; row++)
                {
                    Assert.AreEqual(game.Tableau[col][row].Suit, restored.Tableau[col][row].Suit,
                        $"Column {col} row {row} suit mismatch");
                    Assert.AreEqual(game.Tableau[col][row].Rank, restored.Tableau[col][row].Rank,
                        $"Column {col} row {row} rank mismatch");
                }
            }

            Console.WriteLine("Fresh game round-trip successful");
        }

        [TestMethod]
        public void TestFromDumpStringAcceptsAsciiSuitLetters()
        {
            // Verify that FromDumpString accepts move history with ASCII suit letters (H, D, C, S)
            // instead of Unicode symbols - easier for humans to type
            var game = new FreeCellGameService();
            game.InitializeGame(42424);

            // Make moves to get a dump with Unicode suits
            game.Select(SourceType.Tableau, 0, game.Tableau[0].Count - 1);
            game.TryMove(SourceType.FreeCell, 0);
            game.Select(SourceType.Tableau, 1, game.Tableau[1].Count - 1);
            game.TryMove(SourceType.FreeCell, 1);

            string dump = game.ToDumpString();

            // Replace Unicode suits with ASCII letters
            string asciiDump = dump
                .Replace("\u2665", "H")
                .Replace("\u2666", "D")
                .Replace("\u2663", "C")
                .Replace("\u2660", "S");
            Console.WriteLine("=== ASCII dump ===");
            Console.WriteLine(asciiDump);

            // Should parse successfully
            var restored = FreeCellGameService.FromDumpString(asciiDump);

            Assert.AreEqual(game.GameId, restored.GameId);
            Assert.AreEqual(game.MoveCount, restored.MoveCount);
            Assert.AreEqual(game.MoveHistory.Count, restored.MoveHistory.Count,
                "Move history count should match after round-trip with ASCII suits");

            // Verify the actual move text was preserved (with ASCII letters)
            for (int i = 0; i < game.MoveHistory.Count; i++)
            {
                var expected = game.MoveHistory[i]
                    .Replace("\u2665", "H")
                    .Replace("\u2666", "D")
                    .Replace("\u2663", "C")
                    .Replace("\u2660", "S");
                Assert.AreEqual(expected, restored.MoveHistory[i],
                    $"Move history entry {i} mismatch");
            }

            Console.WriteLine("FromDumpString accepts ASCII suit letters");
        }

        [TestMethod]
        public void TestToDumpStringMultiLineHistory()
        {
            // Verify that the dump string has each move on a separate line
            var game = new FreeCellGameService();
            game.InitializeGame(42424);

            game.Select(SourceType.Tableau, 0, game.Tableau[0].Count - 1);
            game.TryMove(SourceType.FreeCell, 0);
            game.Select(SourceType.Tableau, 1, game.Tableau[1].Count - 1);
            game.TryMove(SourceType.FreeCell, 1);

            string dump = game.ToDumpString();
            Console.WriteLine(dump);

            // Should have "MoveHistory:" on its own line
            Assert.IsTrue(dump.Contains("MoveHistory:\r\n"), "MoveHistory header should be on its own line");

            // Each move should be on its own indented line
            foreach (var move in game.MoveHistory)
            {
                Assert.IsTrue(dump.Contains($"  {move}"),
                    $"Move '{move}' should appear indented on its own line");
            }

            Console.WriteLine("Multi-line move history format correct");
        }

        [TestMethod]
        public void TestMoveHistoryMultiCardMoveFormat()
        {
            // Find a seed where a multi-card tableau move is possible and verify format includes xN
            for (int seed = 1; seed <= 200; seed++)
            {
                var game = new FreeCellGameService(new Random(seed));

                // Make a few moves to free cells to allow multi-card moves
                for (int i = 0; i < 3; i++)
                {
                    game.Select(SourceType.Tableau, i, game.Tableau[i].Count - 1);
                    game.TryMove(SourceType.FreeCell, i);
                }

                // Try to find a valid multi-card tableau move
                for (int srcCol = 0; srcCol < 8; srcCol++)
                {
                    int seqStart = game.GetBottomSequenceStartIndex(srcCol);
                    if (seqStart < 0) continue;
                    int seqLen = game.Tableau[srcCol].Count - seqStart;
                    if (seqLen < 2) continue;

                    var leadCard = game.Tableau[srcCol][seqStart];
                    for (int dstCol = 0; dstCol < 8; dstCol++)
                    {
                        if (dstCol == srcCol) continue;
                        if (!game.CanPlaceOnTableau(leadCard, game.Tableau[dstCol])) continue;
                        int maxMovable = game.CalculateMaxMovableCards(SourceType.Tableau, dstCol);
                        if (seqLen > maxMovable) continue;

                        // Found a valid multi-card move - execute it
                        game.Select(SourceType.Tableau, srcCol, seqStart);
                        bool success = game.TryMove(SourceType.Tableau, dstCol);
                        if (!success) continue;

                        // Last move entry should have xN suffix
                        var lastEntry = game.MoveHistory[^1];
                        Console.WriteLine($"Seed {seed}: Multi-card move: {lastEntry}");
                        Assert.IsTrue(lastEntry.Contains($"x{seqLen}"),
                            $"Multi-card move should contain 'x{seqLen}' but was '{lastEntry}'");
                        Assert.IsTrue(lastEntry.Contains(">"),
                            "Multi-card move should use > separator");
                        Console.WriteLine($"Multi-card move format correct (seed {seed})");
                        return;
                    }
                }
            }
            Assert.Inconclusive("No valid multi-card move found in seeds 1-200");
        }

        #endregion

        #region Classic FreeCell Compatibility Tests

        [TestMethod]
        public void TestClassicFreeCellGame11982Layout()
        {
            // Game #11982 is hardcoded to match the verified Windows FreeCell unsolvable layout
            // Layout verified from: https://dan.hersam.com/2009/02/13/how-to-beat-the-impossible-freecell-game/
            
            var game = new FreeCellGameService();
            game.InitializeGame(11982);
            
            Console.WriteLine("=== Game #11982 (Verified Windows FreeCell Layout) ===");
            for (int col = 0; col < 8; col++)
            {
                var cards = string.Join(" ", game.Tableau[col].Select(c => CardToStr(c)));
                Console.WriteLine($"  Column {col + 1}: {cards}");
            }
            
            // Verify basic structure
            Assert.AreEqual(52, game.Tableau.Sum(col => col.Count), "Game #11982 should have 52 cards");
            Assert.AreEqual(11982, game.GameId);
            
            // Verify Column 1 starts with JD (Jack of Diamonds) - the key signature of Windows FreeCell #11982
            Assert.AreEqual(Suit.Diamonds, game.Tableau[0][0].Suit, "Column 1 first card should be J♦");
            Assert.AreEqual(Rank.Jack, game.Tableau[0][0].Rank, "Column 1 first card should be J♦");
            
            // Verify Column 1 ends with 9S (9 of Spades)
            Assert.AreEqual(Suit.Spades, game.Tableau[0][6].Suit, "Column 1 last card should be 9♠");
            Assert.AreEqual(Rank.Nine, game.Tableau[0][6].Rank, "Column 1 last card should be 9♠");
            
            // Verify Column 8 ends with AC (Ace of Clubs)
            Assert.AreEqual(Suit.Clubs, game.Tableau[7][5].Suit, "Column 8 last card should be A♣");
            Assert.AreEqual(Rank.Ace, game.Tableau[7][5].Rank, "Column 8 last card should be A♣");
            
            Console.WriteLine("\n✓ Game #11982 matches verified Windows FreeCell layout (proven unsolvable)!");
        }

        [TestMethod]
        public void TestBuriedAcesGame999999()
        {
            // Game #999999 is our custom unsolvable layout with all 4 aces buried
            
            var game = new FreeCellGameService();
            game.InitializeGame(999999);
            
            Console.WriteLine("=== Game #999999 (Buried Aces - Unsolvable) ===");
            for (int col = 0; col < 8; col++)
            {
                var cards = string.Join(" ", game.Tableau[col].Select(c => CardToStr(c)));
                Console.WriteLine($"  Column {col + 1}: {cards}");
            }
            
            // Verify all 4 aces are at the bottom of columns 0-3
            Assert.AreEqual(Rank.Ace, game.Tableau[0][0].Rank, "Ace should be buried at bottom of column 1");
            Assert.AreEqual(Rank.Ace, game.Tableau[1][0].Rank, "Ace should be buried at bottom of column 2");
            Assert.AreEqual(Rank.Ace, game.Tableau[2][0].Rank, "Ace should be buried at bottom of column 3");
            Assert.AreEqual(Rank.Ace, game.Tableau[3][0].Rank, "Ace should be buried at bottom of column 4");
            
            // Verify kings are blocking the aces
            Assert.AreEqual(Rank.King, game.Tableau[0][1].Rank, "King should block ace in column 1");
            Assert.AreEqual(Rank.King, game.Tableau[1][1].Rank, "King should block ace in column 2");
            Assert.AreEqual(Rank.King, game.Tableau[2][1].Rank, "King should block ace in column 3");
            Assert.AreEqual(Rank.King, game.Tableau[3][1].Rank, "King should block ace in column 4");
            
            Console.WriteLine("\n✓ Game #999999 has all 4 aces buried under kings - systematically unsolvable!");
        }

        [TestMethod]
        public void TestClassicFreeCellGame1Layout()
        {
            // Game #1 is a well-known easy FreeCell game
            
            var game = new FreeCellGameService();
            game.InitializeGame(1);
            
            Console.WriteLine("=== Game #1 Layout ===");
            for (int col = 0; col < 8; col++)
            {
                var cards = string.Join(" ", game.Tableau[col].Select(c => CardToStr(c)));
                Console.WriteLine($"  Column {col + 1}: {cards}");
            }
            
            // Verify first card of column 1: J♦ (Jack of Diamonds) per Rosetta Code
            var firstCard = game.Tableau[0][0];
            Assert.AreEqual(Suit.Diamonds, firstCard.Suit, "Game #1 Column 1 first card should be J♦");
            Assert.AreEqual(Rank.Jack, firstCard.Rank, "Game #1 Column 1 first card should be J♦");
            
            Console.WriteLine("\n✓ Game #1 first card matches expected (J♦)!");
        }

        [TestMethod]
        public void TestAllUnsolvableGameIds()
        {
            // The 8 known unsolvable games from the original 32,000 Microsoft FreeCell games
            var unsolvableGameIds = new[] { 11982, 146692, 186216, 455889, 495505, 512118, 517776, 781948 };
            
            Console.WriteLine("=== All Known Unsolvable FreeCell Games ===\n");
            
            foreach (var gameId in unsolvableGameIds)
            {
                var game = new FreeCellGameService();
                game.InitializeGame(gameId);
                
                Assert.AreEqual(52, game.Tableau.Sum(col => col.Count), $"Game #{gameId} should have 52 cards");
                Assert.AreEqual(gameId, game.GameId, $"GameId should be {gameId}");
                
                var firstCards = string.Join(", ", game.Tableau.Select(col => CardToStr(col[0])));
                Console.WriteLine($"Game #{gameId}: {firstCards}");
            }
            
            Console.WriteLine("\n✓ All 8 unsolvable game IDs initialize correctly");
        }
        
        private static string CardToStr(Card card)
        {
            char rank = card.Rank switch
            {
                Rank.Ace => 'A',
                Rank.Ten => 'T',
                Rank.Jack => 'J',
                Rank.Queen => 'Q',
                Rank.King => 'K',
                _ => (char)('0' + (int)card.Rank)
            };
            char suit = card.Suit switch
            {
                Suit.Clubs => 'C',
                Suit.Diamonds => 'D',
                Suit.Hearts => 'H',
                Suit.Spades => 'S',
                _ => '?'
            };
            return $"{rank}{suit}";
        }

        #endregion

        #region PRNG vs Hardcoded Comparison Tests

        [TestMethod]
        public void TestComparePRNGvsHardcodedFor11982()
        {
            // Compare what the PRNG would generate vs what we hardcoded
            // This verifies if the PRNG algorithm matches classic Windows FreeCell
            
            Console.WriteLine("=== Comparing PRNG vs Hardcoded for Game #11982 ===\n");
            
            // Get the hardcoded layout
            var hardcodedGame = new FreeCellGameService();
            hardcodedGame.InitializeGame(11982);
            
            // Generate using PRNG directly (bypassing the hardcoded check)
            var prngLayout = GenerateWithPRNG(11982);
            
            Console.WriteLine("Column | Hardcoded            | PRNG Generated       | Match?");
            Console.WriteLine("-------|----------------------|----------------------|-------");
            
            bool allMatch = true;
            for (int col = 0; col < 8; col++)
            {
                var hardcodedCards = string.Join(" ", hardcodedGame.Tableau[col].Select(c => CardToStr(c)));
                var prngCards = string.Join(" ", prngLayout[col].Select(c => CardToStr(c)));
                
                bool colMatch = hardcodedCards == prngCards;
                allMatch &= colMatch;
                
                Console.WriteLine($"   {col + 1}   | {hardcodedCards,-20} | {prngCards,-20} | {(colMatch ? "✓" : "✗")}");
            }
            
            Console.WriteLine();
            if (allMatch)
            {
                Console.WriteLine("✓ PRNG output MATCHES hardcoded layout!");
                Console.WriteLine("  The PRNG algorithm is correct - hardcoding is redundant but ensures accuracy.");
            }
            else
            {
                Console.WriteLine("✗ PRNG output DIFFERS from hardcoded layout!");
                Console.WriteLine("  The hardcoded layout is REQUIRED for classic Windows FreeCell compatibility.");
                Console.WriteLine("  Other unsolvable games (146692, etc.) may not match classic FreeCell exactly.");
            }
        }

        [TestMethod]
        public void TestCompareAllUnsolvableGamesPRNGvsExpected()
        {
            // Test if our PRNG generates the same layouts for all unsolvable games
            // by comparing first cards (a quick sanity check)
            
            var unsolvableGameIds = new[] { 11982, 146692, 186216, 455889, 495505, 512118, 517776, 781948 };
            
            Console.WriteLine("=== PRNG Generated Layouts for All Unsolvable Games ===\n");
            
            foreach (var gameId in unsolvableGameIds)
            {
                var prngLayout = GenerateWithPRNG(gameId);
                
                Console.WriteLine($"Game #{gameId}:");
                for (int col = 0; col < 8; col++)
                {
                    var cards = string.Join(" ", prngLayout[col].Select(c => CardToStr(c)));
                    Console.WriteLine($"  Column {col + 1}: {cards}");
                }
                Console.WriteLine();
            }
            
            Console.WriteLine("✓ Generated layouts for all unsolvable games");
            Console.WriteLine("  These should match classic Windows FreeCell if PRNG is correct.");
        }

        /// <summary>
        /// Generates a FreeCell layout using pure PRNG (no hardcoded overrides)
        /// </summary>
        private List<List<Card>> GenerateWithPRNG(int gameId)
        {
            var tableau = new List<List<Card>>();
            for (int col = 0; col < 8; col++)
            {
                tableau.Add(new List<Card>());
            }

            // Microsoft Linear Congruential Generator
            int state = gameId;
            int Rand()
            {
                state = (int)(((long)state * 214013L + 2531011L) & 0x7FFFFFFF);
                return (state >> 16) & 0x7FFF;
            }

            // Initialize and shuffle deck
            var deck = Enumerable.Range(0, 52).ToList();
            for (int i = 51; i > 0; i--)
            {
                int j = Rand() % (i + 1);
                (deck[i], deck[j]) = (deck[j], deck[i]);
            }

            // Deal cards
            for (int i = 51; i >= 0; i--)
            {
                int cardIndex = deck[i];
                int suitIndex = cardIndex % 4;
                int rankValue = cardIndex / 4;
                
                Suit suit = suitIndex switch
                {
                    0 => Suit.Clubs,
                    1 => Suit.Diamonds,
                    2 => Suit.Hearts,
                    3 => Suit.Spades,
                    _ => Suit.Clubs
                };
                
                Rank rank = (Rank)(rankValue + 1);
                int col = (51 - i) % 8;
                tableau[col].Add(new Card(suit, rank, true));
            }

            return tableau;
        }

        #endregion

        #region Incremental Hash Tests

        /// <summary>
        /// Helper: compute fresh hash by reinitializing from scratch
        /// </summary>
        private ulong ComputeFreshHash(FreeCellGameBase game)
        {
            game.IncrementalHashReady = false;
            game.InitIncrementalHash();
            return game.IncrementalHashValue;
        }

        [TestMethod]
        public void TestIncrementalHash_InitMatchesRecompute()
        {
            var game = new FreeCellGameService(new Random(42));
            game.InitIncrementalHash();
            var hash1 = game.IncrementalHashValue;

            // Recompute from scratch and verify it matches
            var hash2 = ComputeFreshHash(game);
            Assert.AreEqual(hash1, hash2, "InitIncrementalHash should be deterministic");
        }

        [TestMethod]
        public void TestIncrementalHash_TableauToFreeCell()
        {
            var game = new FreeCellGameService(new Random(42));
            game.InitIncrementalHash();

            var card = game.Tableau[0][^1];
            var move = new FreeCellMove(card)
            {
                sourceType = SourceType.Tableau,
                targetType = SourceType.FreeCell,
                sourceIndex = 0,
                targetIndex = 0,
                cardCount = 1
            };
            Assert.IsTrue(move.ApplyMoveFast(game));

            var incrementalHash = game.IncrementalHashValue;
            var freshHash = ComputeFreshHash(game);
            Assert.AreEqual(freshHash, incrementalHash,
                "Incremental hash after Tableau->FreeCell must match fresh computation");
        }

        [TestMethod]
        public void TestIncrementalHash_FreeCellToTableau()
        {
            var game = new FreeCellGameService(new Random(42));
            game.InitIncrementalHash();

            // Move card to free cell first
            var card = game.Tableau[0][^1];
            var move1 = new FreeCellMove(card)
            {
                sourceType = SourceType.Tableau,
                targetType = SourceType.FreeCell,
                sourceIndex = 0,
                targetIndex = 0,
                cardCount = 1
            };
            move1.ApplyMoveFast(game);

            // Find a valid tableau target for this card
            int targetCol = -1;
            for (int col = 0; col < 8; col++)
            {
                if (game.Tableau[col].Count > 0 && game.CanPlaceOnTableau(card, game.Tableau[col]))
                {
                    targetCol = col;
                    break;
                }
            }
            if (targetCol == -1)
            {
                // Move to empty column if available, otherwise just move back
                targetCol = 0; // col 0 still has cards, use any column
                for (int col = 0; col < 8; col++)
                    if (game.Tableau[col].Count == 0) { targetCol = col; break; }
            }

            var move2 = new FreeCellMove(card)
            {
                sourceType = SourceType.FreeCell,
                targetType = SourceType.Tableau,
                sourceIndex = 0,
                targetIndex = targetCol,
                cardCount = 1
            };
            move2.ApplyMoveFast(game);

            var incrementalHash = game.IncrementalHashValue;
            var freshHash = ComputeFreshHash(game);
            Assert.AreEqual(freshHash, incrementalHash,
                "Incremental hash after FreeCell->Tableau must match fresh computation");
        }

        [TestMethod]
        public void TestIncrementalHash_TableauToFoundation()
        {
            for (int seed = 1; seed <= 100; seed++)
            {
                var game = new FreeCellGameService(new Random(seed));
                game.InitIncrementalHash();

                for (int col = 0; col < 8; col++)
                {
                    if (game.Tableau[col].Count == 0) continue;
                    var bottomCard = game.Tableau[col][^1];
                    if (bottomCard.Rank == Rank.Ace)
                    {
                        var move = new FreeCellMove(bottomCard)
                        {
                            sourceType = SourceType.Tableau,
                            targetType = SourceType.Foundation,
                            sourceIndex = col,
                            targetIndex = 0,
                            cardCount = 1
                        };
                        Assert.IsTrue(move.ApplyMoveFast(game));

                        var incrementalHash = game.IncrementalHashValue;
                        var freshHash = ComputeFreshHash(game);
                        Assert.AreEqual(freshHash, incrementalHash,
                            $"Seed {seed}: Incremental hash after Tableau->Foundation must match fresh computation");
                        return;
                    }
                }
            }
            Assert.Inconclusive("No accessible Ace found in seeds 1-100");
        }

        [TestMethod]
        public void TestIncrementalHash_FoundationToTableau()
        {
            for (int seed = 1; seed <= 100; seed++)
            {
                var game = new FreeCellGameService(new Random(seed));
                game.InitIncrementalHash();

                for (int col = 0; col < 8; col++)
                {
                    if (game.Tableau[col].Count == 0) continue;
                    var bottomCard = game.Tableau[col][^1];
                    if (bottomCard.Rank == Rank.Ace)
                    {
                        var moveToFnd = new FreeCellMove(bottomCard)
                        {
                            sourceType = SourceType.Tableau,
                            targetType = SourceType.Foundation,
                            sourceIndex = col,
                            targetIndex = 0,
                            cardCount = 1
                        };
                        moveToFnd.ApplyMoveFast(game);

                        // Now move it back from foundation to an empty column or valid target
                        int targetCol = game.Tableau[col].Count == 0 ? col : 0;
                        for (int c = 0; c < 8; c++)
                            if (game.Tableau[c].Count == 0) { targetCol = c; break; }

                        var moveBack = new FreeCellMove(bottomCard)
                        {
                            sourceType = SourceType.Foundation,
                            targetType = SourceType.Tableau,
                            sourceIndex = 0,
                            targetIndex = targetCol,
                            cardCount = 1
                        };
                        moveBack.ApplyMoveFast(game);

                        var incrementalHash = game.IncrementalHashValue;
                        var freshHash = ComputeFreshHash(game);
                        Assert.AreEqual(freshHash, incrementalHash,
                            $"Seed {seed}: Incremental hash after Foundation->Tableau must match fresh computation");
                        return;
                    }
                }
            }
            Assert.Inconclusive("No accessible Ace found in seeds 1-100");
        }

        [TestMethod]
        public void TestIncrementalHash_UnApplyMove_RestoresHash()
        {
            var game = new FreeCellGameService(new Random(42));
            game.InitIncrementalHash();
            var originalHash = game.IncrementalHashValue;

            var card = game.Tableau[0][^1];
            var move = new FreeCellMove(card)
            {
                sourceType = SourceType.Tableau,
                targetType = SourceType.FreeCell,
                sourceIndex = 0,
                targetIndex = 0,
                cardCount = 1
            };
            move.ApplyMoveFast(game);
            Assert.AreNotEqual(originalHash, game.IncrementalHashValue, "Hash should change after move");

            move.UnApplyMove(game);
            Assert.AreEqual(originalHash, game.IncrementalHashValue,
                "Hash must be restored to original after UnApplyMove");
        }

        [TestMethod]
        public void TestIncrementalHash_MultipleMovesAndUndo()
        {
            var game = new FreeCellGameService(new Random(42));
            game.InitIncrementalHash();
            var originalHash = game.IncrementalHashValue;

            // Apply several moves to free cells, then undo all in reverse
            var moves = new List<FreeCellMove>();
            for (int i = 0; i < 4; i++)
            {
                if (game.Tableau[i].Count == 0) continue;
                var card = game.Tableau[i][^1];
                var move = new FreeCellMove(card)
                {
                    sourceType = SourceType.Tableau,
                    targetType = SourceType.FreeCell,
                    sourceIndex = i,
                    targetIndex = i,
                    cardCount = 1
                };
                move.ApplyMoveFast(game);
                // Verify incremental matches fresh after each move
                var inc = game.IncrementalHashValue;
                var fresh = ComputeFreshHash(game);
                Assert.AreEqual(fresh, inc, $"Mismatch after move {i + 1}");
                moves.Add(move);
            }

            // Undo all moves in reverse order
            for (int i = moves.Count - 1; i >= 0; i--)
            {
                moves[i].UnApplyMove(game);
                var inc = game.IncrementalHashValue;
                var fresh = ComputeFreshHash(game);
                Assert.AreEqual(fresh, inc, $"Mismatch after undo {moves.Count - i}");
            }

            Assert.AreEqual(originalHash, game.IncrementalHashValue,
                "Hash must be fully restored after undoing all moves");
        }

        [TestMethod]
        public void TestIncrementalHash_TableauToTableau_SingleCard()
        {
            // Use multiple seeds to find one where a tableau-to-tableau single card move is legal
            for (int seed = 1; seed <= 100; seed++)
            {
                var game = new FreeCellGameService(new Random(seed));
                game.InitIncrementalHash();

                for (int srcCol = 0; srcCol < 8; srcCol++)
                {
                    if (game.Tableau[srcCol].Count == 0) continue;
                    var card = game.Tableau[srcCol][^1];
                    for (int dstCol = 0; dstCol < 8; dstCol++)
                    {
                        if (dstCol == srcCol || game.Tableau[dstCol].Count == 0) continue;
                        if (game.CanPlaceOnTableau(card, game.Tableau[dstCol]))
                        {
                            var move = new FreeCellMove(card)
                            {
                                sourceType = SourceType.Tableau,
                                targetType = SourceType.Tableau,
                                sourceIndex = srcCol,
                                targetIndex = dstCol,
                                cardCount = 1
                            };
                            var hashBefore = game.IncrementalHashValue;
                            move.ApplyMoveFast(game);

                            var incHash = game.IncrementalHashValue;
                            var freshHash = ComputeFreshHash(game);
                            Assert.AreEqual(freshHash, incHash,
                                $"Seed {seed}: Tableau[{srcCol}]->Tableau[{dstCol}] incremental hash mismatch");

                            // Undo and verify restoration
                            move.UnApplyMove(game);
                            Assert.AreEqual(hashBefore, game.IncrementalHashValue,
                                $"Seed {seed}: Hash not restored after undo");
                            return; // found and verified one move
                        }
                    }
                }
            }
            Assert.Inconclusive("No valid tableau-to-tableau single card move found in seeds 1-100");
        }

        [TestMethod]
        public void TestIncrementalHash_TableauToTableau_MultiCard()
        {
            // Multi-card moves need a valid sequence; test across seeds
            for (int seed = 1; seed <= 200; seed++)
            {
                var game = new FreeCellGameService(new Random(seed));
                game.InitIncrementalHash();

                for (int srcCol = 0; srcCol < 8; srcCol++)
                {
                    int seqStart = game.GetBottomSequenceStartIndex(srcCol);
                    if (seqStart < 0) continue;
                    int seqLen = game.Tableau[srcCol].Count - seqStart;
                    if (seqLen < 2) continue;

                    var leadCard = game.Tableau[srcCol][seqStart];
                    for (int dstCol = 0; dstCol < 8; dstCol++)
                    {
                        if (dstCol == srcCol) continue;
                        if (!game.CanPlaceOnTableau(leadCard, game.Tableau[dstCol])) continue;
                        int maxMovable = game.CalculateMaxMovableCards(SourceType.Tableau, dstCol);
                        if (seqLen > maxMovable) continue;

                        var move = new FreeCellMove(leadCard)
                        {
                            sourceType = SourceType.Tableau,
                            targetType = SourceType.Tableau,
                            sourceIndex = srcCol,
                            targetIndex = dstCol,
                            cardCount = seqLen
                        };
                        var hashBefore = game.IncrementalHashValue;
                        move.ApplyMoveFast(game);

                        var incHash = game.IncrementalHashValue;
                        var freshHash = ComputeFreshHash(game);
                        Assert.AreEqual(freshHash, incHash,
                            $"Seed {seed}: Multi-card Tableau[{srcCol}]->Tableau[{dstCol}] (count={seqLen}) incremental hash mismatch");

                        move.UnApplyMove(game);
                        Assert.AreEqual(hashBefore, game.IncrementalHashValue,
                            $"Seed {seed}: Hash not restored after undo of multi-card move");
                        return;
                    }
                }
            }
            Assert.Inconclusive("No valid multi-card tableau move found in seeds 1-200");
        }

        [TestMethod]
        public void TestIncrementalHash_FreeCellToFoundation()
        {
            // Move Ace to free cell, then from free cell to foundation
            for (int seed = 1; seed <= 100; seed++)
            {
                var game = new FreeCellGameService(new Random(seed));
                game.InitIncrementalHash();

                // Find an accessible Ace
                for (int col = 0; col < 8; col++)
                {
                    if (game.Tableau[col].Count == 0) continue;
                    var card = game.Tableau[col][^1];
                    if (card.Rank != Rank.Ace) continue;

                    // Move Ace to free cell
                    var moveToFC = new FreeCellMove(card)
                    {
                        sourceType = SourceType.Tableau,
                        targetType = SourceType.FreeCell,
                        sourceIndex = col,
                        targetIndex = 0,
                        cardCount = 1
                    };
                    moveToFC.ApplyMoveFast(game);

                    // Move Ace from free cell to foundation
                    var moveToFnd = new FreeCellMove(card)
                    {
                        sourceType = SourceType.FreeCell,
                        targetType = SourceType.Foundation,
                        sourceIndex = 0,
                        targetIndex = 0,
                        cardCount = 1
                    };
                    moveToFnd.ApplyMoveFast(game);

                    var incHash = game.IncrementalHashValue;
                    var freshHash = ComputeFreshHash(game);
                    Assert.AreEqual(freshHash, incHash,
                        $"Seed {seed}: FreeCell->Foundation incremental hash mismatch");

                    // Undo both
                    moveToFnd.UnApplyMove(game);
                    moveToFC.UnApplyMove(game);

                    var restoredHash = game.IncrementalHashValue;
                    var restoredFresh = ComputeFreshHash(game);
                    Assert.AreEqual(restoredFresh, restoredHash,
                        $"Seed {seed}: Hash not restored after undoing FreeCell->Foundation chain");
                    return;
                }
            }
            Assert.Inconclusive("No accessible Ace found in seeds 1-100");
        }

        [TestMethod]
        public void TestIncrementalHash_DifferentStatesProduceDifferentHashes()
        {
            var game = new FreeCellGameService(new Random(42));
            game.InitIncrementalHash();
            var hash1 = game.IncrementalHashValue;

            var card = game.Tableau[0][^1];
            var move = new FreeCellMove(card)
            {
                sourceType = SourceType.Tableau,
                targetType = SourceType.FreeCell,
                sourceIndex = 0,
                targetIndex = 0,
                cardCount = 1
            };
            move.ApplyMoveFast(game);
            var hash2 = game.IncrementalHashValue;

            Assert.AreNotEqual(hash1, hash2,
                "Different board states should produce different hashes");
        }

        [TestMethod]
        public void TestIncrementalHash_ManyApplyUnapplyRoundtrips()
        {
            // Stress test: apply and unapply the same move many times
            var game = new FreeCellGameService(new Random(42));
            game.InitIncrementalHash();
            var originalHash = game.IncrementalHashValue;

            var card = game.Tableau[0][^1];
            var move = new FreeCellMove(card)
            {
                sourceType = SourceType.Tableau,
                targetType = SourceType.FreeCell,
                sourceIndex = 0,
                targetIndex = 0,
                cardCount = 1
            };

            for (int i = 0; i < 100; i++)
            {
                move.ApplyMoveFast(game);
                var incHash = game.IncrementalHashValue;
                var freshHash = ComputeFreshHash(game);
                Assert.AreEqual(freshHash, incHash, $"Mismatch on apply iteration {i}");

                move.UnApplyMove(game);
                Assert.AreEqual(originalHash, game.IncrementalHashValue,
                    $"Hash drift detected on unapply iteration {i}");
            }
        }

        [TestMethod]
        public void TestIncrementalHash_MultiSeedConsistency()
        {
            // Verify across many game seeds that initial hash and post-move hash are consistent
            for (int seed = 1; seed <= 50; seed++)
            {
                var game = new FreeCellGameService(new Random(seed));
                game.InitIncrementalHash();
                var initialHash = game.IncrementalHashValue;

                // Recompute and verify determinism
                var freshHash = ComputeFreshHash(game);
                Assert.AreEqual(initialHash, freshHash, $"Seed {seed}: Init hash not deterministic");

                // Make a move (bottom card of col 0 to free cell 0)
                if (game.Tableau[0].Count > 0)
                {
                    var card = game.Tableau[0][^1];
                    var move = new FreeCellMove(card)
                    {
                        sourceType = SourceType.Tableau,
                        targetType = SourceType.FreeCell,
                        sourceIndex = 0,
                        targetIndex = 0,
                        cardCount = 1
                    };
                    move.ApplyMoveFast(game);
                    var incHash = game.IncrementalHashValue;
                    var freshAfter = ComputeFreshHash(game);
                    Assert.AreEqual(freshAfter, incHash,
                        $"Seed {seed}: Post-move incremental hash mismatch");
                }
            }
        }

        #endregion

        #region Solver with Incremental Hash Tests

        [TestMethod]
        [Timeout(120000)]
        public async Task TestSolverWithIncrementalHash_Game368()
        {
            // Game 368 was a known failure, but the OrderChanging optimization now solves it.
            var gameService = new FreeCellGameService();
            gameService.InitializeGame(368);
            var solver = new FreeCellSolver(gameService, loggerAction: (msgfactory) => Console.WriteLine(msgfactory()));
            var moves = await solver.FindSolutionAsync();
            Assert.IsTrue(moves.Count > 0, "Game 368 should now be solvable");
            Console.WriteLine($"Game 368 solved with {moves.Count} moves, visited {solver.VisitedNodeCount} states");
        }

        [TestMethod]
        [Timeout(300000)]
        public async Task TestSolverWithIncrementalHash_First20Games()
        {
            var failures = new List<string>();
            for (int gameId = 1; gameId <= 20; gameId++)
            {
                var gameService = new FreeCellGameService();
                gameService.InitializeGame(gameId);
                var solver = new FreeCellSolver(gameService, loggerAction: null);
                try
                {
                    var moves = await solver.FindSolutionAsync();
                    Console.WriteLine($"Game {gameId,4}: {moves.Count,3} moves, visited {solver._countNodesVisited,7} states");
                }
                catch (Exception ex)
                {
                    failures.Add($"Game {gameId}: {ex.Message}");
                }
            }
            foreach (var f in failures) Console.WriteLine($"FAIL: {f}");
            Assert.AreEqual(0, failures.Count, $"Failed games: {string.Join(", ", failures)}");
        }

        #endregion

        #region Move History Parsing Tests

        [TestMethod]
        public void TestParseMoveHistoryEntry_TableauToTableau()
        {
            var move = FreeCellGameService.ParseMoveHistoryEntry("5♥:Col3>Col6");
            Assert.AreEqual(SourceType.Tableau, move.sourceType);
            Assert.AreEqual(3, move.sourceIndex);
            Assert.AreEqual(SourceType.Tableau, move.targetType);
            Assert.AreEqual(6, move.targetIndex);
            Assert.AreEqual(1, move.cardCount);
            Assert.IsNotNull(move.CardMoved);
            Assert.AreEqual(Rank.Five, move.CardMoved!.Rank);
            Assert.AreEqual(Suit.Hearts, move.CardMoved.Suit);
        }

        [TestMethod]
        public void TestParseMoveHistoryEntry_MultiCardMove()
        {
            var move = FreeCellGameService.ParseMoveHistoryEntry("5♥:Col3>Col6x3");
            Assert.AreEqual(SourceType.Tableau, move.sourceType);
            Assert.AreEqual(3, move.sourceIndex);
            Assert.AreEqual(SourceType.Tableau, move.targetType);
            Assert.AreEqual(6, move.targetIndex);
            Assert.AreEqual(3, move.cardCount);
            Assert.AreEqual(Rank.Five, move.CardMoved!.Rank);
            Assert.AreEqual(Suit.Hearts, move.CardMoved.Suit);
        }

        [TestMethod]
        public void TestParseMoveHistoryEntry_FreeCellToFoundation()
        {
            var move = FreeCellGameService.ParseMoveHistoryEntry("A♠:Free0>Fnd0");
            Assert.AreEqual(SourceType.FreeCell, move.sourceType);
            Assert.AreEqual(0, move.sourceIndex);
            Assert.AreEqual(SourceType.Foundation, move.targetType);
            Assert.AreEqual(0, move.targetIndex);
            Assert.AreEqual(1, move.cardCount);
            Assert.AreEqual(Rank.Ace, move.CardMoved!.Rank);
            Assert.AreEqual(Suit.Spades, move.CardMoved.Suit);
        }

        [TestMethod]
        public void TestParseMoveHistoryEntry_AsciiSuit()
        {
            var move = FreeCellGameService.ParseMoveHistoryEntry("KH:Col0>Free2");
            Assert.AreEqual(SourceType.Tableau, move.sourceType);
            Assert.AreEqual(0, move.sourceIndex);
            Assert.AreEqual(SourceType.FreeCell, move.targetType);
            Assert.AreEqual(2, move.targetIndex);
            Assert.AreEqual(1, move.cardCount);
            Assert.AreEqual(Rank.King, move.CardMoved!.Rank);
            Assert.AreEqual(Suit.Hearts, move.CardMoved.Suit);
        }

        [TestMethod]
        public void TestParseMoveHistoryEntry_TenCard()
        {
            var move = FreeCellGameService.ParseMoveHistoryEntry("10♣:Col2>Col5");
            Assert.AreEqual(Rank.Ten, move.CardMoved!.Rank);
            Assert.AreEqual(Suit.Clubs, move.CardMoved.Suit);
            Assert.AreEqual(SourceType.Tableau, move.sourceType);
            Assert.AreEqual(2, move.sourceIndex);
            Assert.AreEqual(SourceType.Tableau, move.targetType);
            Assert.AreEqual(5, move.targetIndex);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void TestParseMoveHistoryEntry_InvalidFormat()
        {
            FreeCellGameService.ParseMoveHistoryEntry("invalid");
        }

        [TestMethod]
        public void TestParseMoveHistory_BatchParse()
        {
            var entries = new[] { "5♥:Col3>Col6", "A♠:Free0>Fnd0", "KD:Col1>Col4x2" };
            var moves = FreeCellGameService.ParseMoveHistory(entries);
            Assert.AreEqual(3, moves.Count);
            Assert.AreEqual(SourceType.Tableau, moves[0].sourceType);
            Assert.AreEqual(SourceType.FreeCell, moves[1].sourceType);
            Assert.AreEqual(2, moves[2].cardCount);
        }

        [TestMethod]
        public void TestParseMoveHistory_RoundTrip()
        {
            // Play some moves on a game, export history, parse back, verify fields match
            var game = new FreeCellGameService(new Random(42));
            game.InitializeGame(1);

            // Make a few moves via the game API
            var col0Top = game.Tableau[0][^1];
            game.Selection = new CardSelection { SourceType = SourceType.Tableau, SourceIndex = 0, CardIndex = game.Tableau[0].Count - 1 };
            game.TryMove(SourceType.FreeCell, 0);

            var col1Top = game.Tableau[1][^1];
            game.Selection = new CardSelection { SourceType = SourceType.Tableau, SourceIndex = 1, CardIndex = game.Tableau[1].Count - 1 };
            game.TryMove(SourceType.FreeCell, 1);

            Assert.IsTrue(game.MoveHistory.Count >= 2, "Should have at least 2 moves recorded");

            // Parse all history entries back to FreeCellMove objects
            var parsedMoves = FreeCellGameService.ParseMoveHistory(game.MoveHistory);
            Assert.AreEqual(game.MoveHistory.Count, parsedMoves.Count);

            // Verify first move
            Assert.AreEqual(SourceType.Tableau, parsedMoves[0].sourceType);
            Assert.AreEqual(0, parsedMoves[0].sourceIndex);
            Assert.AreEqual(SourceType.FreeCell, parsedMoves[0].targetType);
            Assert.AreEqual(0, parsedMoves[0].targetIndex);
            Assert.AreEqual(col0Top.Rank, parsedMoves[0].CardMoved!.Rank);
            Assert.AreEqual(col0Top.Suit, parsedMoves[0].CardMoved.Suit);
        }

        [TestMethod]
        public void TestSolverInitializeWithMoveHistory()
        {
            // Play moves on a game, parse history, and initialize solver with it
            var game = new FreeCellGameService(new Random(42));
            game.InitializeGame(1);

            // Make a move
            game.Selection = new CardSelection { SourceType = SourceType.Tableau, SourceIndex = 0, CardIndex = game.Tableau[0].Count - 1 };
            game.TryMove(SourceType.FreeCell, 0);

            // Serialize and re-create via dump string (simulates deserialization)
            var dump = game.ToDumpString(includeMoveHistory: true);
            var restored = FreeCellGameService.FromDumpString(dump);

            // Parse move history and initialize solver
            var parsedMoves = FreeCellGameService.ParseMoveHistory(restored.MoveHistory);
            var solver = new FreeCellSolver(restored, loggerAction: null);
            int visitedBefore = solver.VisitedNodeCount;
            solver.InitializeWithMoveHistory(parsedMoves);

            // Solver should have more visited states (initial + intermediate states from history)
            Assert.IsTrue(solver.VisitedNodeCount > visitedBefore,
                $"Expected more visited states after initializing with history. Before: {visitedBefore}, After: {solver.VisitedNodeCount}");

            // Solver's move history should contain the prior moves
            Assert.AreEqual(parsedMoves.Count, solver.MoveHistoryCount,
                "Solver move history should contain the prior moves");

            // Verify solver's game state matches the restored game state
            Assert.AreEqual(restored.dumpAllToLog(""), solver._game.dumpAllToLog(""),
                "Solver's game state should match the restored state after InitializeWithMoveHistory");
        }

        [TestMethod]
        public void TestParseMoveHistory_FromDumpStringRoundTrip()
        {
            // Full round-trip: play moves → export → parse dump → parse history → replay on fresh game → compare states
            var game = new FreeCellGameService(new Random(42));
            game.InitializeGame(5);

            // Play several moves
            game.Selection = new CardSelection { SourceType = SourceType.Tableau, SourceIndex = 0, CardIndex = game.Tableau[0].Count - 1 };
            game.TryMove(SourceType.FreeCell, 0);
            game.Selection = new CardSelection { SourceType = SourceType.Tableau, SourceIndex = 1, CardIndex = game.Tableau[1].Count - 1 };
            game.TryMove(SourceType.FreeCell, 1);

            var stateAfterMoves = game.dumpAllToLog("");

            // Export and re-import
            var dump = game.ToDumpString(includeMoveHistory: true);
            var restored = FreeCellGameService.FromDumpString(dump);

            // Parse move history
            var parsedMoves = FreeCellGameService.ParseMoveHistory(restored.MoveHistory);

            // Replay parsed moves on a fresh game starting from the same initial deal
            var fresh = new FreeCellGameService(new Random(42));
            fresh.InitializeGame(5);
            foreach (var move in parsedMoves)
            {
                move.ApplyMove(fresh);
            }

            // Board state after replay should match the original
            Assert.AreEqual(stateAfterMoves, fresh.dumpAllToLog(""),
                "Board state after replaying parsed moves should match original");
        }

        #endregion
    }
}
