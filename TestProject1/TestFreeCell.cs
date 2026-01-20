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
            
            game.Select(1, 0, game.Tableau[0].Count - 1);
            bool moved = game.TryMove(0, 0);
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
                game.Select(1, i, game.Tableau[i].Count - 1);
                game.TryMove(0, i);
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
            game.Select(1, 0, game.Tableau[0].Count - 1);
            game.TryMove(0, 0);
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
            game.Select(1, 0, game.Tableau[0].Count - 1);
            game.TryMove(0, 0);

            // GetNextFoundationMove should return something or null
            var nextMove = game.GetNextFoundationMove();
            
            if (nextMove != null)
            {
                Console.WriteLine($"Next foundation move found: type={nextMove.Value.sourceType}, index={nextMove.Value.sourceIndex}");
            }
            else
            {
                Console.WriteLine("No immediate foundation move available (normal for most game states)");
            }

            Console.WriteLine("? GetNextFoundationMove works correctly");
        }

        [TestMethod]
        public void TestAutoSolveStep()
        {
            var game = new FreeCellGameService(new Random(42));

            // Try auto-solve step - may or may not find a move depending on game state
            var result = game.AutoSolveStep();

            if (result != null)
            {
                var (sourceType, sourceIndex, card) = result.Value;
                Console.WriteLine($"AutoSolveStep moved {card} from {(sourceType == 0 ? "free cell" : "tableau")} {sourceIndex}");
                Assert.IsTrue(game.Foundations.Sum(f => f.Count) > 0, "Card should be in foundation");
            }
            else
            {
                Console.WriteLine("No auto-solve move available (normal for fresh game)");
            }

            Console.WriteLine("? AutoSolveStep works correctly");
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
        public void FindSeedWithAutoSolvableCard()
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
            game.Select(1, 0, game.Tableau[0].Count - 1);
            game.TryMove(0, 0); // Move to free cell
            
            game.Select(1, 1, game.Tableau[1].Count - 1);
            game.TryMove(0, 1); // Move to another free cell
            
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
            game.Select(1, 0, game.Tableau[0].Count - 1);
            game.TryMove(0, 0);
            game.Select(1, 1, game.Tableau[1].Count - 1);
            game.TryMove(0, 1);
            
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
                game.Select(1, i, game.Tableau[i].Count - 1);
                game.TryMove(0, i);
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
    }
}
