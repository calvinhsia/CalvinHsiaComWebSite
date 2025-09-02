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
    }
}