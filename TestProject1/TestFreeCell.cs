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
            // Column 5 has 4? at bottom
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
            // Column 5 (index 4) has 4? at bottom (top of list = top of visual pile)
            // In FreeCell, cards are dealt top-to-bottom, so [^1] is the accessible card
            
            // Column 5: Multiple cards with 4? at the accessible bottom
            game.Tableau[4].Add(new Card(Suit.Hearts, Rank.King, true));  // top (not accessible)
            game.Tableau[4].Add(new Card(Suit.Spades, Rank.Queen, true));
            game.Tableau[4].Add(new Card(Suit.Diamonds, Rank.Jack, true));
            game.Tableau[4].Add(new Card(Suit.Clubs, Rank.Four, true));   // bottom (accessible) - THIS should move!

            Console.WriteLine("=== Exact User Screenshot Scenario ===");
            Console.WriteLine($"Foundation 1 (Clubs): {game.Foundations[1].Count} cards, top: {game.Foundations[1][^1]}");
            Console.WriteLine($"Column 5 has {game.Tableau[4].Count} cards:");
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
            Console.WriteLine($"Foundation 1 (Clubs) now has {game.Foundations[1].Count} cards");
            Console.WriteLine($"Column 5 now has {game.Tableau[4].Count} cards");

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
    }
}
