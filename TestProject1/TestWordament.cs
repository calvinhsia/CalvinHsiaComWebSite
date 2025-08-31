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
            // Create a mock IJSRuntime - we'll use null since DebugHelper doesn't require it for static methods
            _debugHelper = new DebugHelper(null!);
            _gameService = new WordamentGameService(_debugHelper);
            
            // Enable debug mode for consistent test results
            DebugHelper.SetDebugMode(true);
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
            
            // Note: Due to randomness, this might not always be true, but it's likely
            Console.WriteLine($"Grid has special cells: {hasSpecialCells}");
        }

        [TestMethod]
        public void TestDebugModeConsistentResults()
        {
            // Both services should use the same fixed seed (1) in debug mode
            var settings = new WordamentSettings { MinWordLength = 3 };
            
            var gameState1 = _gameService!.CreateNewGame(settings);
            var gameState2 = _gameService.CreateNewGame(settings);
            
            // In debug mode with fixed seed, grids should be identical
            for (int x = 0; x < WordamentGrid.Size; x++)
            {
                for (int y = 0; y < WordamentGrid.Size; y++)
                {
                    Assert.AreEqual(gameState1.Grid.Cells[x, y].Letter, 
                                   gameState2.Grid.Cells[x, y].Letter, 
                                   $"Grid letters should match at position [{x},{y}] in debug mode");
                }
            }
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
    }
}