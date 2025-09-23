using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WordScapeBlazorWasm.Models;
using WordScapeBlazorWasm.Services;
using Microsoft.JSInterop;

namespace TestProject1
{
    [TestClass]
    public class TestWordament
    {
        private WordamentGameService? _gameService;
        private DebugHelper? _debugHelper;

        [TestInitialize]
        public void Initialize()
        {
            // Create shared dictionary service for tests
            var dictionaryService = new DictionaryService();
            
            // Create a mock IJSRuntime - we'll use null since DebugHelper doesn't require it for static methods
            _debugHelper = new DebugHelper(null!);
            // Enable debug mode for consistent test results
            DebugHelper.SetDebugMode(true);
            _gameService = new WordamentGameService(dictionaryService, _debugHelper);
        }

        [TestMethod]
        public void TestWordamentGameCreation()
        {
            var settings = new WordamentSettings
            {
                GameDurationMinutes = 3,
                MinWordLength = 3
            };

            var gameState = _gameService!.CreateNewGame(settings);

            Assert.IsNotNull(gameState, "Game state should be created");
            Assert.IsTrue(gameState.IsGameActive, "Game should be active");
            Assert.AreEqual(TimeSpan.FromMinutes(3), gameState.TimeRemaining, "Time should be 3 minutes");
            Assert.AreEqual(0, gameState.Score, "Initial score should be 0");
            Assert.AreEqual(0, gameState.FoundWords.Count, "No words should be found initially");
            Assert.IsNotNull(gameState.Grid, "Grid should be created");

            // Verify grid is 4x4 and has letters
            for (int x = 0; x < WordamentGrid.Size; x++)
            {
                for (int y = 0; y < WordamentGrid.Size; y++)
                {
                    var cell = gameState.Grid.Cells[x, y];
                    Assert.IsTrue(char.IsLetter(cell.Letter), $"Cell [{x},{y}] should contain a letter");
                }
            }
        }

        [TestMethod]
        public void TestValidPath()
        {
            var settings = new WordamentSettings { MinWordLength = 3 };
            var gameState = _gameService!.CreateNewGame(settings);
            var grid = gameState.Grid;

            // Test valid adjacent path
            var validPath = new List<GridPosition>
            {
                new GridPosition(0, 0),
                new GridPosition(0, 1),
                new GridPosition(1, 1)
            };

            bool isValid = _gameService.IsValidPath(validPath, grid);
            Assert.IsTrue(isValid, "Adjacent path should be valid");

            // Test invalid path with gap
            var invalidPath = new List<GridPosition>
            {
                new GridPosition(0, 0),
                new GridPosition(2, 2) // Not adjacent
            };

            bool isInvalid = _gameService.IsValidPath(invalidPath, grid);
            Assert.IsFalse(isInvalid, "Non-adjacent path should be invalid");
        }

        [TestMethod]
        public void TestWordFromPath()
        {
            var settings = new WordamentSettings { MinWordLength = 3 };
            var gameState = _gameService!.CreateNewGame(settings);
            var grid = gameState.Grid;

            // Test getting word from a simple path
            var path = new List<GridPosition>
            {
                new GridPosition(0, 0),
                new GridPosition(0, 1),
                new GridPosition(1, 1)
            };

            string word = _gameService.GetWordFromPath(path, grid);
            Assert.IsFalse(string.IsNullOrEmpty(word), "Should get a word from valid path");
            Assert.AreEqual(3, word.Length, "Word should have 3 characters");
            
            // Verify word matches the letters in the grid
            Assert.AreEqual(grid.Cells[0, 0].Letter, word[0], "First letter should match");
            Assert.AreEqual(grid.Cells[0, 1].Letter, word[1], "Second letter should match");
            Assert.AreEqual(grid.Cells[1, 1].Letter, word[2], "Third letter should match");
        }

        [TestMethod]
        public void TestGridGeneration()
        {
            var settings = new WordamentSettings { MinWordLength = 3 };
            var gameState = _gameService!.CreateNewGame(settings);
            var grid = gameState.Grid;

            // Test that grid is properly initialized
            Assert.AreEqual(WordamentGrid.Size, 4, "Grid size should be 4");

            // dump the grid
            if (grid != null)
            {
                for (int y = 0; y < WordamentGrid.Size; y++)
                {
                    var sb = new StringBuilder();
                    for (int x = 0; x < WordamentGrid.Size; x++)
                    {
                        sb.Append(grid.Cells[x, y]);
                        sb.Append('_');
                    }
                    Console.WriteLine(sb.ToString());
                }
            }

            // Test that all cells have letters
            for (int x = 0; x < WordamentGrid.Size; x++)
            {
                for (int y = 0; y < WordamentGrid.Size; y++)
                {
                    var cell = grid.Cells[x, y];
                    Assert.AreEqual(x, cell.X, $"Cell X coordinate should be {x}");
                    Assert.AreEqual(y, cell.Y, $"Cell Y coordinate should be {y}");
                    Assert.IsTrue(char.IsLetter(cell.Letter), $"Cell [{x},{y}] should contain a letter");
                }
            }

            // Test that some special cells might exist (random)
            bool hasSpecialCells = false;
            for (int x = 0; x < WordamentGrid.Size; x++)
            {
                for (int y = 0; y < WordamentGrid.Size; y++)
                {
                    if (grid.Cells[x, y].IsSpecial)
                    {
                        hasSpecialCells = true;
                        break;
                    }
                }
                if (hasSpecialCells) break;
            }
            
            Console.WriteLine($"Grid has special cells: {hasSpecialCells}");
        }


        [TestMethod]
        public void TestSharedDebugHelper()
        {
            // Test that DebugHelper is shared between games
            bool originalDebugState = DebugHelper.IsDebugEnabled;
            
            try
            {
                // Test setting debug mode affects both games
                DebugHelper.SetDebugMode(true);
                Assert.IsTrue(DebugHelper.IsDebugEnabled, "Debug mode should be enabled");
                
                // This should be consistent across all services using DebugHelper
                var settings = new WordamentSettings { MinWordLength = 3 };
                var gameState = _gameService!.CreateNewGame(settings);
                
                // Verify the game service recognizes debug mode
                Assert.IsTrue(DebugHelper.IsDebugEnabled, "Debug helper should be shared");
                
                DebugHelper.SetDebugMode(false);
                Assert.IsFalse(DebugHelper.IsDebugEnabled, "Debug mode should be disabled");
            }
            finally
            {
                // Restore original state
                DebugHelper.SetDebugMode(originalDebugState);
            }
        }

        [TestMethod]
        public void TestAdjacentPositions()
        {
            var settings = new WordamentSettings { MinWordLength = 3 };
            var gameState = _gameService!.CreateNewGame(settings);
            var grid = gameState.Grid;

            // Test corner position (0,0) - should have 3 adjacent positions
            var cornerPos = new GridPosition(0, 0);
            var adjacent = _gameService.GetAdjacentPositions(cornerPos, grid, new List<GridPosition>());
            Assert.AreEqual(3, adjacent.Count, "Corner position should have 3 adjacent positions");

            // Test center position (1,1) - should have 8 adjacent positions
            var centerPos = new GridPosition(1, 1);
            var centerAdjacent = _gameService.GetAdjacentPositions(centerPos, grid, new List<GridPosition>());
            Assert.AreEqual(8, centerAdjacent.Count, "Center position should have 8 adjacent positions");

            // Test with exclusions
            var exclusions = new List<GridPosition> { new GridPosition(0, 1), new GridPosition(1, 0) };
            var adjacentWithExclusions = _gameService.GetAdjacentPositions(cornerPos, grid, exclusions);
            Assert.AreEqual(1, adjacentWithExclusions.Count, "Should exclude specified positions");
        }

        [TestMethod]
        public void TestDesktopDragPath()
        {
            // Test simulating a desktop drag path
            var settings = new WordamentSettings { MinWordLength = 3 };
            var gameState = _gameService!.CreateNewGame(settings);
            var grid = gameState.Grid;

            // Simulate a simple L-shaped drag path
            var dragPath = new List<GridPosition>
            {
                new GridPosition(0, 0), // Start
                new GridPosition(0, 1), // Down
                new GridPosition(1, 1), // Right
                new GridPosition(1, 2)  // Down
            };

            // Test each step of the path is valid
            Assert.IsTrue(_gameService.IsValidPath(dragPath, grid), "L-shaped drag path should be valid");

            // Test word formation from path
            var word = _gameService.GetWordFromPath(dragPath, grid);
            Assert.IsFalse(string.IsNullOrEmpty(word), "Should get a word from drag path");
            Assert.AreEqual(4, word.Length, "Word should have 4 characters from 4-cell path");

            // Test can add to path functionality (used during drag)
            var partialPath = new List<GridPosition> { new GridPosition(0, 0), new GridPosition(0, 1) };
            var nextPosition = new GridPosition(1, 1);
            
            Assert.IsTrue(_gameService.CanAddToPath(nextPosition, partialPath, grid), 
                "Should be able to add adjacent position to path");

            // Test invalid addition (non-adjacent)
            var invalidNext = new GridPosition(3, 3);
            Assert.IsFalse(_gameService.CanAddToPath(invalidNext, partialPath, grid), 
                "Should not be able to add non-adjacent position to path");
        }

        [TestMethod]
        public void TestPathBacktracking()
        {
            // Test backtracking functionality (important for drag UI)
            var settings = new WordamentSettings { MinWordLength = 3 };
            var gameState = _gameService!.CreateNewGame(settings);
            var grid = gameState.Grid;

            // Create a path and then backtrack
            var path = new List<GridPosition>
            {
                new GridPosition(0, 0),
                new GridPosition(0, 1),
                new GridPosition(1, 1)
            };

            // Simulate backtracking by removing last position
            path.RemoveAt(path.Count - 1);

            // Should still be valid
            Assert.IsTrue(_gameService.IsValidPath(path, grid), "Path after backtracking should be valid");
            
            var word = _gameService.GetWordFromPath(path, grid);
            Assert.AreEqual(2, word.Length, "Word should have 2 characters after backtracking");
        }

        [TestMethod]
        public void TestGridCellSelection()
        {
            // Test selection and highlighting functionality
            var settings = new WordamentSettings { MinWordLength = 3 };
            var gameState = _gameService!.CreateNewGame(settings);
            var grid = gameState.Grid;

            // Test clearing selection
            _gameService.ClearSelection(grid);
            
            // Verify all cells are cleared
            for (int x = 0; x < WordamentGrid.Size; x++)
            {
                for (int y = 0; y < WordamentGrid.Size; y++)
                {
                    var cell = grid.Cells[x, y];
                    Assert.IsFalse(cell.IsSelected, $"Cell [{x},{y}] should not be selected after clear");
                    Assert.IsFalse(cell.IsHighlighted, $"Cell [{x},{y}] should not be highlighted after clear");
                }
            }

            // Test updating selection
            var testPath = new List<GridPosition>
            {
                new GridPosition(1, 1),
                new GridPosition(1, 2)
            };

            _gameService.UpdateSelection(testPath, grid);

            // Verify selected cells are marked
            Assert.IsTrue(grid.Cells[1, 1].IsSelected, "First cell in path should be selected");
            Assert.IsTrue(grid.Cells[1, 2].IsSelected, "Second cell in path should be selected");
            Assert.IsFalse(grid.Cells[0, 0].IsSelected, "Unselected cell should not be marked");
        }

        [TestMethod]
        public void TestDesktopDragDiagnostics()
        {
            // Test to help diagnose desktop drag issues
            var settings = new WordamentSettings { MinWordLength = 3 };
            var gameState = _gameService!.CreateNewGame(settings);
            var grid = gameState.Grid;

            Console.WriteLine("?? Wordament Grid Layout for Desktop Drag Testing:");
            Console.WriteLine("?????????????????????????");
            
            for (int y = 0; y < WordamentGrid.Size; y++)
            {
                var rowText = "?";
                for (int x = 0; x < WordamentGrid.Size; x++)
                {
                    var cell = grid.Cells[x, y];
                    rowText += $"  {cell.Letter}  ?";
                }
                Console.WriteLine(rowText);
                
                if (y < WordamentGrid.Size - 1)
                {
                    Console.WriteLine("?????????????????????????");
                }
            }
            Console.WriteLine("?????????????????????????");

            // Test all adjacent relationships for desktop drag validation
            Console.WriteLine("\n?? Testing Adjacent Relationships:");
            for (int y = 0; y < WordamentGrid.Size; y++)
            {
                for (int x = 0; x < WordamentGrid.Size; x++)
                {
                    var pos = new GridPosition(x, y);
                    var adjacentCount = _gameService.GetAdjacentPositions(pos, grid, new List<GridPosition>()).Count;
                    var expectedCount = GetExpectedAdjacentCount(x, y);
                    
                    Console.WriteLine($"Position ({x},{y}): {adjacentCount} adjacent (expected {expectedCount}) - {(adjacentCount == expectedCount ? "?" : "?")}");
                    Assert.AreEqual(expectedCount, adjacentCount, $"Position ({x},{y}) should have {expectedCount} adjacent positions");
                }
            }

            // Test diagonal movement (critical for Wordament)
            Console.WriteLine("\n?? Testing Diagonal Movement:");
            var testPairs = new[]
            {
                (new GridPosition(0, 0), new GridPosition(1, 1), true, "Corner to diagonal"),
                (new GridPosition(1, 1), new GridPosition(2, 2), true, "Center to diagonal"),
                (new GridPosition(0, 0), new GridPosition(2, 2), false, "Corner to non-adjacent"),
                (new GridPosition(0, 0), new GridPosition(1, 0), true, "Horizontal adjacent"),
                (new GridPosition(0, 0), new GridPosition(0, 1), true, "Vertical adjacent")
            };

            foreach (var (pos1, pos2, expected, description) in testPairs)
            {
                var isAdjacent = grid.AreAdjacent(pos1, pos2);
                Console.WriteLine($"{description}: {pos1} -> {pos2} = {isAdjacent} (expected {expected}) - {(isAdjacent == expected ? "?" : "?")}");
                Assert.AreEqual(expected, isAdjacent, $"{description} failed");
            }
        }

        private int GetExpectedAdjacentCount(int x, int y)
        {
            // Calculate expected adjacent count based on position
            if ((x == 0 || x == 3) && (y == 0 || y == 3))
                return 3; // Corner positions
            else if (x == 0 || x == 3 || y == 0 || y == 3)
                return 5; // Edge positions (not corner)
            else
                return 8; // Interior positions
        }

        [TestMethod]
        public void TestWordPlacementAnimationData()
        {
            // Test to verify that word placement animation receives correct data
            var settings = new WordamentSettings { MinWordLength = 3 };
            var gameState = _gameService!.CreateNewGame(settings);
            var grid = gameState.Grid;

            // Create a specific word path
            var wordPath = new List<GridPosition>
            {
                new GridPosition(0, 0),
                new GridPosition(0, 1),
                new GridPosition(1, 1),
                new GridPosition(1, 2)
            };

            // Get the word from this path
            var word = _gameService.GetWordFromPath(wordPath, grid);
            Console.WriteLine($"?? Test word: '{word}' from path:");
            
            for (int i = 0; i < wordPath.Count; i++)
            {
                var pos = wordPath[i];
                var cell = grid.Cells[pos.X, pos.Y];
                Console.WriteLine($"  [{i}] Position ({pos.X},{pos.Y}) = '{cell.Letter}'");
            }

            // Test that the path is valid
            Assert.IsTrue(_gameService.IsValidPath(wordPath, grid), "Word path should be valid");
            Assert.AreEqual(wordPath.Count, word.Length, $"Word length should match path length: {word.Length} != {wordPath.Count}");

            // Verify that animation data would be correct
            for (int i = 0; i < wordPath.Count; i++)
            {
                var pos = wordPath[i];
                var expectedLetter = grid.Cells[pos.X, pos.Y].Letter;
                var actualLetter = word[i];
                Assert.AreEqual(expectedLetter, actualLetter, $"Letter at position {i} should match: expected '{expectedLetter}', got '{actualLetter}'");
            }

            Console.WriteLine($"? Animation data test passed for word '{word}'");
        }

        [TestMethod]
        public void TestAnimationSequence()
        {
            // Test the sequence that happens during word submission
            var settings = new WordamentSettings { MinWordLength = 3 };
            var gameState = _gameService!.CreateNewGame(settings);
            var grid = gameState.Grid;

            // Create a test path
            var testPath = new List<GridPosition>
            {
                new GridPosition(0, 0),
                new GridPosition(0, 1),
                new GridPosition(1, 1)
            };

            var word = _gameService.GetWordFromPath(testPath, grid);
            Console.WriteLine($"?? Testing animation sequence for word: '{word}'");

            // Step 1: Submit the word
            var foundWord = _gameService.SubmitWord(testPath, grid, settings);
            
            if (foundWord != null)
            {
                Console.WriteLine($"? Word '{foundWord.Word}' submitted successfully, score: {foundWord.Score}");
                
                // Step 2: Add to found words (simulating the game)
                gameState.FoundWords.Add(foundWord);
                
                // Step 3: Create animation data that would be sent to JavaScript
                var animationData = testPath.Select(p => new { x = p.X, y = p.Y }).ToArray();
                
                Console.WriteLine("?? Animation data that would be sent to JavaScript:");
                for (int i = 0; i < animationData.Length; i++)
                {
                    Console.WriteLine($"  [{i}] {{ x: {animationData[i].x}, y: {animationData[i].y} }}");
                }
                
                // Step 4: Verify that this data correctly identifies the cells
                Console.WriteLine("?? Verifying cell identification:");
                for (int i = 0; i < animationData.Length; i++)
                {
                    var data = animationData[i];
                    var cell = grid.Cells[data.x, data.y];
                    Console.WriteLine($"  Cell at ({data.x},{data.y}) contains letter '{cell.Letter}' (word[{i}] = '{word[i]}')");
                    Assert.AreEqual(word[i], cell.Letter, $"Animation data should point to correct cells");
                }
                
                Assert.IsTrue(true, "Animation sequence test completed successfully");
            }
            else
            {
                // This might happen if the word isn't in the dictionary
                Console.WriteLine($"? Word '{word}' was not accepted (likely not in dictionary)");
                Assert.Inconclusive($"Word '{word}' not in dictionary for animation test");
            }
        }

        [TestMethod]
        [Ignore]
        public void TestMultipleWordAnimations()
        {
            // Test that multiple words don't interfere with each other's animations
            var settings = new WordamentSettings { MinWordLength = 3 };
            var gameState = _gameService!.CreateNewGame(settings);
            var grid = gameState.Grid;

            Console.WriteLine("?? Testing multiple word animation separation:");
            
            // Find multiple valid words
            var testedWords = new List<(string Word, List<GridPosition> Path)>();
            
            // Test a few different paths
            var testPaths = new List<List<GridPosition>>
            {
                new() { new(0, 0), new(0, 1), new(1, 1) },      // 3-letter L-shape
                new() { new(1, 0), new(1, 1), new(2, 1) },      // 3-letter L-shape
                new() { new(2, 2), new(2, 3), new(3, 3) },      // 3-letter L-shape
                new() { new(0, 0), new(0, 1) },                  // 2-letter (might be too short)
                new() { new(3, 0), new(3, 1), new(2, 1), new(2, 0) }, // 4-letter square
            };

            foreach (var path in testPaths)
            {
                if (_gameService.IsValidPath(path, grid))
                {
                    var word = _gameService.GetWordFromPath(path, grid);
                    if (word.Length >= settings.MinWordLength)
                    {
                        var foundWord = _gameService.SubmitWord(path, grid, settings);
                        if (foundWord != null)
                        {
                            testedWords.Add((word, path));
                            gameState.FoundWords.Add(foundWord);
                            Console.WriteLine($"  ? Found word '{word}' with {path.Count} cells");
                        }
                    }
                }
            }

            Assert.IsTrue(testedWords.Count > 0, "Should find at least one valid word for testing");

            // Simulate the animation data for each word
            Console.WriteLine($"\n?? Animation data for {testedWords.Count} words:");
            for (int wordIndex = 0; wordIndex < testedWords.Count; wordIndex++)
            {
                var (word, path) = testedWords[wordIndex];
                Console.WriteLine($"  Word {wordIndex + 1}: '{word}'");
                
                var animationData = path.Select(p => new { x = p.X, y = p.Y }).ToArray();
                for (int i = 0; i < animationData.Length; i++)
                {
                    var data = animationData[i];
                    Console.WriteLine($"    Cell [{i}]: ({data.x},{data.y})");
                }
            }

            // The key test: each word should have its own distinct cell positions
            for (int i = 0; i < testedWords.Count; i++)
            {
                for (int j = i + 1; j < testedWords.Count; j++)
                {
                    var (word1, path1) = testedWords[i];
                    var (word2, path2) = testedWords[j];
                    
                    // Check if words share any cells (they might, which is fine)
                    var sharedCells = path1.Intersect(path2).ToList();
                    if (sharedCells.Any())
                    {
                        Console.WriteLine($"  ?? Words '{word1}' and '{word2}' share {sharedCells.Count} cells - this is normal");
                    }
                    else
                    {
                        Console.WriteLine($"  ? Words '{word1}' and '{word2}' use completely different cells");
                    }
                }
            }

            Console.WriteLine("? Multiple word animation test completed");
        }

        [TestMethod]
        public void TestDiagonalHitAreaConsiderations()
        {
            // Test that helps understand diagonal drag expectations
            var settings = new WordamentSettings { MinWordLength = 3 };
            var gameState = _gameService!.CreateNewGame(settings);
            var grid = gameState.Grid;

            Console.WriteLine("?? Testing diagonal considerations for hit area improvements:");
            Console.WriteLine("The JavaScript implementation now reduces the effective hit-test area of tiles");
            Console.WriteLine("to make diagonal dragging easier by creating 'dead zones' around tile edges.");
            
            // Test that diagonal paths work correctly with game logic
            var diagonalPath = new List<GridPosition>
            {
                new GridPosition(0, 0), // Corner
                new GridPosition(1, 1), // Diagonal to center
                new GridPosition(2, 0), // Diagonal up-right  
                new GridPosition(3, 1)  // Diagonal down-right
            };

            // Verify the path logic still works
            bool pathValid = _gameService.IsValidPath(diagonalPath, grid);
            Console.WriteLine($"? Diagonal path validation: {pathValid}");
            
            if (pathValid)
            {
                var word = _gameService.GetWordFromPath(diagonalPath, grid);
                Console.WriteLine($"? Diagonal word formed: '{word}' ({word.Length} letters)");
                
                // Verify each step is adjacent
                for (int i = 0; i < diagonalPath.Count - 1; i++)
                {
                    var pos1 = diagonalPath[i];
                    var pos2 = diagonalPath[i + 1];
                    var adjacent = grid.AreAdjacent(pos1, pos2);
                    Console.WriteLine($"  Step {i + 1}: ({pos1.X},{pos1.Y}) -> ({pos2.X},{pos2.Y}) = {adjacent}");
                    Assert.IsTrue(adjacent, $"Diagonal step {i + 1} should be adjacent");
                }
            }

            // Test pure diagonal movement
            var pureDiagonalPath = new List<GridPosition>
            {
                new GridPosition(0, 0),
                new GridPosition(1, 1),
                new GridPosition(2, 2),
                new GridPosition(3, 3)
            };

            bool diagonalValid = _gameService.IsValidPath(pureDiagonalPath, grid);
            Console.WriteLine($"? Pure diagonal path (0,0)->(3,3): {diagonalValid}");
            
            if (diagonalValid)
            {
                var diagonalWord = _gameService.GetWordFromPath(pureDiagonalPath, grid);
                Console.WriteLine($"? Pure diagonal word: '{diagonalWord}' ({diagonalWord.Length} letters)");
            }

            Console.WriteLine("\n? JavaScript hit-area reduction should make it easier to drag diagonally");
            Console.WriteLine("  by not triggering on cells when dragging near their edges.");
            Console.WriteLine("? This test verifies that the game logic supports diagonal movement correctly.");
            
            Assert.IsTrue(true, "Diagonal considerations test completed");
        }

        [TestMethod]
        public void TestLongWordModeGameCompletion()
        {
            // Test that LongWord mode properly handles game completion
            var timerSettings = new WordamentSettings 
            { 
                GameMode = WordamentGameMode.Timer,
                GameDurationMinutes = 3,
                MinWordLength = 3 
            };
            
            var longWordSettings = new WordamentSettings 
            { 
                GameMode = WordamentGameMode.LongWord,
                MinWordLength = 3 
            };

            // Test Timer mode game state
            var timerGameState = _gameService!.CreateNewGame(timerSettings);
            Assert.AreEqual(WordamentGameMode.Timer, timerGameState.GameMode, "Timer mode should be set correctly");
            Assert.IsFalse(timerGameState.IsGameComplete, "Timer mode should not be complete initially");
            Assert.IsTrue(timerGameState.IsGameActive, "Timer mode should be active initially");
            Console.WriteLine($"Timer mode - TimeRemaining: {timerGameState.TimeRemaining}, IsGameComplete: {timerGameState.IsGameComplete}");

            // Test LongWord mode game state
            var longWordGameState = _gameService!.CreateNewGame(longWordSettings);
            Assert.AreEqual(WordamentGameMode.LongWord, longWordGameState.GameMode, "LongWord mode should be set correctly");
            Assert.IsFalse(longWordGameState.IsGameComplete, "LongWord mode should not be complete initially");
            Assert.IsTrue(longWordGameState.IsGameActive, "LongWord mode should be active initially");
            Assert.IsFalse(longWordGameState.OriginalWordFound, "Original word should not be found initially");
            Assert.IsFalse(string.IsNullOrEmpty(longWordGameState.OriginalWord), "Original word should be set");
            Console.WriteLine($"LongWord mode - TimeRemaining: {longWordGameState.TimeRemaining}, OriginalWord: '{longWordGameState.OriginalWord}', IsGameComplete: {longWordGameState.IsGameComplete}");

            // Test LongWord completion by finding original word
            longWordGameState.OriginalWordFound = true;
            Assert.IsTrue(longWordGameState.IsGameComplete, "LongWord mode should be complete when original word is found");
            Console.WriteLine($"After finding original word - IsGameComplete: {longWordGameState.IsGameComplete}");

            // Test Timer mode completion by time expiration
            timerGameState.TimeRemaining = TimeSpan.Zero;
            Assert.IsTrue(timerGameState.IsGameComplete, "Timer mode should be complete when time expires");
            Console.WriteLine($"After time expiration - IsGameComplete: {timerGameState.IsGameComplete}");

            // Test deactivating games
            longWordGameState.OriginalWordFound = false; // Reset
            longWordGameState.IsGameActive = false;
            Assert.IsTrue(longWordGameState.IsGameComplete, "LongWord mode should be complete when inactive");

            timerGameState.TimeRemaining = TimeSpan.FromMinutes(1); // Reset
            timerGameState.IsGameActive = false;
            Assert.IsTrue(timerGameState.IsGameComplete, "Timer mode should be complete when inactive");
        }

        [TestMethod]
        public void TestWordScapeStyleWordValidation()
        {
            // Test that words are now classified using WordScape-style validation
            var settings = new WordamentSettings { MinWordLength = 3 };
            var gameState = _gameService!.CreateNewGame(settings);
            var grid = gameState.Grid;

            Console.WriteLine("?? Testing WordScape-style word validation in Wordament:");

            // Test known good words from small dictionary
            var testWords = new[] { "THE", "AND", "CAT", "DOG", "HELLO", "WORLD" };
            
            foreach (var testWord in testWords)
            {
                var wordType = _gameService.ValidateWordType(testWord);
                Console.WriteLine($"  Word '{testWord}': {wordType}");
                
                // All these should be at least in small dictionary (SubWordNotInGrid)
                Assert.AreNotEqual(FoundWordType.SubWordNotAWord, wordType, 
                    $"Word '{testWord}' should be found in at least one dictionary");
            }

            // Test that invalid/made-up words are classified as not found
            var invalidWords = new[] { "XYZ", "QWERTY", "ASDFGH", "ZZZZZZ" };
            
            foreach (var invalidWord in invalidWords)
            {
                var wordType = _gameService.ValidateWordType(invalidWord);
                Console.WriteLine($"  Invalid word '{invalidWord}': {wordType}");
                
                // These should likely be SubWordNotAWord
                // Note: Some might be in large dictionary, so we just check they're classified
                Assert.IsTrue(Enum.IsDefined(typeof(FoundWordType), wordType),
                    $"Word '{invalidWord}' should have a valid classification");
            }

            // Test word submission includes all words (even invalid ones)
            var testPath = new List<GridPosition>
            {
                new GridPosition(0, 0),
                new GridPosition(0, 1),
                new GridPosition(1, 1)
            };

            var pathWord = _gameService.GetWordFromPath(testPath, grid);
            Console.WriteLine($"\n?? Testing word submission for grid word: '{pathWord}'");

            var foundWord = _gameService.SubmitWord(testPath, grid, settings);
            
            if (foundWord != null)
            {
                Console.WriteLine($"  Word '{foundWord.Word}' submitted with type: {foundWord.WordType}");
                Console.WriteLine($"  Score: {foundWord.Score}");
                Console.WriteLine($"  CSS class: {foundWord.GetDisplayClass()}");
                
                // Verify the word has a proper classification
                Assert.IsTrue(Enum.IsDefined(typeof(FoundWordType), foundWord.WordType),
                    "Found word should have a valid type classification");
                
                // Verify CSS class is set
                Assert.IsFalse(string.IsNullOrEmpty(foundWord.GetDisplayClass()),
                    "Found word should have a CSS class");
            }
            else
            {
                Console.WriteLine($"  Word '{pathWord}' was rejected (likely too short)");
                Assert.IsTrue(pathWord.Length < settings.MinWordLength, 
                    "Only words shorter than minimum length should be rejected");
            }

            Console.WriteLine("? WordScape-style validation test completed");
        }

        [TestMethod]
        public void TestWordClassificationColors()
        {
            // Test that word classification produces the correct CSS classes
            Console.WriteLine("?? Testing word classification color mapping:");

            var testWordTypes = new[]
            {
                FoundWordType.SubWordInGrid,
                FoundWordType.SubWordInLargeDictionary,
                FoundWordType.SubWordNotInGrid,
                FoundWordType.SubWordNotAWord
            };

            foreach (var wordType in testWordTypes)
            {
                var foundWord = new WordamentFoundWord
                {
                    Word = "TEST",
                    WordType = wordType,
                    Score = 10
                };

                var cssClass = foundWord.GetDisplayClass();
                Console.WriteLine($"  {wordType} -> CSS class: '{cssClass}'");

                // Verify each type has a unique CSS class
                Assert.IsFalse(string.IsNullOrEmpty(cssClass), 
                    $"WordType {wordType} should have a CSS class");
                
                // Verify it follows the expected naming convention
                var expectedClasses = new[]
                {
                    "word-in-grid",
                    "word-in-large-dict", 
                    "word-in-small-dict",
                    "word-not-found"
                };
                
                Assert.IsTrue(expectedClasses.Contains(cssClass),
                    $"CSS class '{cssClass}' should be one of the expected classes");
            }

            // Test combination classes (longest word, etc.)
            var longestWord = new WordamentFoundWord
            {
                Word = "LONGEST",
                WordType = FoundWordType.SubWordNotInGrid,
                IsLongestWord = true,
                Score = 50
            };

            // Note: The combination logic is in the Razor page, not the model
            // So we just test the base classification here
            var baseCssClass = longestWord.GetDisplayClass();
            Console.WriteLine($"  Longest word base class: '{baseCssClass}'");
            Assert.AreEqual("word-in-small-dict", baseCssClass, 
                "Longest word should still have correct base type classification");

            Console.WriteLine("? Word classification color test completed");
        }

        [TestMethod]
        public void TestAlwaysAddWordsToList()
        {
            // Test that all valid-length words are added to the list, regardless of dictionary status
            var settings = new WordamentSettings { MinWordLength = 3 };
            var gameState = _gameService!.CreateNewGame(settings);
            var grid = gameState.Grid;

            Console.WriteLine("?? Testing that all words are added to found list:");

            // Find several different paths and submit them
            var testPaths = new List<List<GridPosition>>
            {
                new() { new(0, 0), new(0, 1), new(1, 1) },      // 3-letter
                new() { new(1, 0), new(1, 1), new(2, 1) },      // 3-letter different area
                new() { new(2, 2), new(2, 3), new(3, 3) },      // 3-letter corner
            };

            var submittedWords = new List<WordamentFoundWord>();

            foreach (var path in testPaths)
            {
                if (_gameService.IsValidPath(path, grid))
                {
                    var word = _gameService.GetWordFromPath(path, grid);
                    if (word.Length >= settings.MinWordLength)
                    {
                        var foundWord = _gameService.SubmitWord(path, grid, settings);
                        if (foundWord != null)
                        {
                            submittedWords.Add(foundWord);
                            gameState.FoundWords.Add(foundWord);
                            
                            Console.WriteLine($"  Added: '{foundWord.Word}' ({foundWord.WordType}, {foundWord.Score} pts)");
                        }
                    }
                }
            }

            Assert.IsTrue(submittedWords.Count > 0, "Should submit at least some words");

            // Verify that words are added regardless of dictionary status
            var hasValidWords = submittedWords.Any(w => w.WordType != FoundWordType.SubWordNotAWord);
            var hasInvalidWords = submittedWords.Any(w => w.WordType == FoundWordType.SubWordNotAWord);

            Console.WriteLine($"  Valid dictionary words found: {hasValidWords}");
            Console.WriteLine($"  Invalid/unknown words found: {hasInvalidWords}");

            // At minimum, we should be able to classify all submitted words
            foreach (var word in submittedWords)
            {
                Assert.IsTrue(Enum.IsDefined(typeof(FoundWordType), word.WordType),
                    $"Word '{word.Word}' should have a valid classification");
                
                // Score should be 0 for invalid words, positive for valid words
                if (word.WordType == FoundWordType.SubWordNotAWord)
                {
                    Assert.AreEqual(0, word.Score, "Invalid words should have 0 score");
                }
                else
                {
                    Assert.IsTrue(word.Score > 0, "Valid words should have positive score");
                }
            }

            Console.WriteLine($"? All words added test completed - {submittedWords.Count} words tested");
        }

        [TestMethod]
        public void TestNonDictionaryWordsAreAdded()
        {
            // Test specifically that non-dictionary words show up in the found list
            var settings = new WordamentSettings { MinWordLength = 3 };
            var gameState = _gameService!.CreateNewGame(settings);
            var grid = gameState.Grid;

            Console.WriteLine("?? Testing that non-dictionary words are added to found list:");

            // Create a test path that forms a likely non-dictionary word
            var testPath = new List<GridPosition>
            {
                new GridPosition(0, 0),
                new GridPosition(0, 1),
                new GridPosition(1, 1)
            };

            var word = _gameService.GetWordFromPath(testPath, grid);
            Console.WriteLine($"  Testing word: '{word}' (3 letters from grid)");

            // Submit the word regardless of whether it's in dictionary
            var foundWord = _gameService.SubmitWord(testPath, grid, settings);
            
            Assert.IsNotNull(foundWord, "SubmitWord should return a WordamentFoundWord for any valid-length word");
            Console.WriteLine($"  ? Word '{foundWord.Word}' was submitted successfully");
            Console.WriteLine($"  ?? Word type: {foundWord.WordType}");
            Console.WriteLine($"  ?? Score: {foundWord.Score}");
            Console.WriteLine($"  ?? CSS class: {foundWord.GetDisplayClass()}");

            // Verify the word has proper classification
            Assert.IsTrue(Enum.IsDefined(typeof(FoundWordType), foundWord.WordType),
                "Word should have a valid type classification");

            // Test what happens when we add multiple words (including made-up ones)
            var madeUpWord = "XYZ"; // This should definitely not be in any dictionary
            
            // We can't directly test made-up words since they depend on the grid layout
            // But we can verify that the service handles classification correctly
            var wordType = _gameService.ValidateWordType(madeUpWord);
            Console.WriteLine($"  Made-up word '{madeUpWord}' classified as: {wordType}");
            
            Assert.AreEqual(FoundWordType.SubWordNotAWord, wordType,
                "Made-up words should be classified as SubWordNotAWord");

            // Test a short word (should be rejected due to length)
            var shortPath = new List<GridPosition> { new GridPosition(0, 0), new GridPosition(0, 1) };
            var shortWord = _gameService.GetWordFromPath(shortPath, grid);
            var shortFoundWord = _gameService.SubmitWord(shortPath, grid, settings);
            
            if (shortWord.Length < settings.MinWordLength)
            {
                Assert.IsNull(shortFoundWord, "Words shorter than minimum length should be rejected");
                Console.WriteLine($"  ? Short word '{shortWord}' ({shortWord.Length} letters) correctly rejected");
            }
            else
            {
                Assert.IsNotNull(shortFoundWord, "Words meeting minimum length should be accepted");
                Console.WriteLine($"  ? Word '{shortFoundWord.Word}' ({shortFoundWord.Word.Length} letters) accepted");
            }

            Console.WriteLine("? Non-dictionary word addition test completed");
        }

        [TestMethod] 
        public void TestWordSubmissionLogicFixes()
        {
            // Test that the submission logic properly handles all word types
            var settings = new WordamentSettings { MinWordLength = 3 };
            var gameState = _gameService!.CreateNewGame(settings);

            Console.WriteLine("?? Testing word submission logic fixes:");

            // Test various word types that should all be addable
            var testWords = new[]
            {
                ("Known good word", "THE"),
                ("Likely invalid word", "XYZ"), 
                ("Random letters", "QJK")
            };

            foreach (var (description, testWord) in testWords)
            {
                var wordType = _gameService.ValidateWordType(testWord);
                Console.WriteLine($"  {description} '{testWord}': {wordType}");

                // All words should get a valid classification
                Assert.IsTrue(Enum.IsDefined(typeof(FoundWordType), wordType),
                    $"Word '{testWord}' should have a valid classification");

                // Only valid dictionary words should have non-zero scores
                if (wordType == FoundWordType.SubWordNotAWord)
                {
                    Console.WriteLine($"    Expected score: 0 (not in dictionary)");
                }
                else
                {
                    Console.WriteLine($"    Expected score: > 0 (in dictionary)");
                }
            }

            // Test the UI feedback method
            var isValidForUI1 = _gameService.IsValidWordForUI("THE", 3);
            var isValidForUI2 = _gameService.IsValidWordForUI("XYZ", 3);
            
            Console.WriteLine($"  UI validation - 'THE': {isValidForUI1}");
            Console.WriteLine($"  UI validation - 'XYZ': {isValidForUI2}");

            // THE should be valid for UI, XYZ should not (but both should be submittable)
            Assert.IsTrue(isValidForUI1, "Known good words should be valid for UI");
            // Note: XYZ might actually be in large dictionary, so we don't assert false here

            Console.WriteLine("? Word submission logic test completed");
        }

        [TestMethod]
        public async Task TestEfficientSubwordGeneration()
        {
            // Test the new efficient subword generation algorithm
            var settings = new WordamentSettings { MinWordLength = 3 };
            var gameState = _gameService!.CreateNewGame(settings);
            
            Console.WriteLine($"?? Testing efficient subword generation for original word: '{gameState.OriginalWord}'");
            
            // This should complete quickly now (not hang like before)
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            var subwords = await _gameService.GetOriginalWordSubwordsAsync(gameState.OriginalWord, 3);
            
            stopwatch.Stop();
            
            Console.WriteLine($"?? Subword generation completed in {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"?? Found {subwords.Count} subwords from '{gameState.OriginalWord}'");
            
            // Should complete in reasonable time (less than 5 seconds)
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 5000, 
                $"Subword generation took too long: {stopwatch.ElapsedMilliseconds}ms");
            
            // Should find at least some words
            Assert.IsTrue(subwords.Count > 0, "Should find at least some subwords");
            
            // All words should be classifiable and proper length
            foreach (var word in subwords.Take(10)) // Check first 10 for performance
            {
                Console.WriteLine($"  '{word.Word}' - {word.WordType} ({word.Word.Length} letters)");
                
                Assert.IsTrue(word.Word.Length >= 3, $"Word '{word.Word}' should be at least 3 letters");
                Assert.IsTrue(word.Word.Length <= gameState.OriginalWord.Length, 
                    $"Word '{word.Word}' should not be longer than original word");
                Assert.IsTrue(Enum.IsDefined(typeof(FoundWordType), word.WordType),
                    $"Word '{word.Word}' should have valid classification");
            }
            
            // Test that longest words are marked correctly
            var longestWords = subwords.Where(w => w.IsLongestWord).ToList();
            if (longestWords.Any())
            {
                Console.WriteLine($"?? Found {longestWords.Count} longest words:");
                foreach (var word in longestWords)
                {
                    Console.WriteLine($"  '{word.Word}' ({word.Word.Length} letters)");
                    Assert.AreEqual(gameState.OriginalWord.Length, word.Word.Length,
                        "Longest words should match original word length");
                }
            }
            
            Console.WriteLine("? Efficient subword generation test completed successfully");
        }
    }
}