using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WordScapeBlazorWasm.Models;
using WordScapeBlazorWasm.Services;

namespace TestProject1
{
    [TestClass]
    public class TestWordScape
    {
        [TestMethod]
        public async Task TestGridGeneration()
        {
            var random = new Random();
            for (int i = 0; i < 3; i++)
            {
                var x = random.Next();
            }
            var parms = new WordGenerationParms()
            {
                _Random = random,
                LenTargetWord = 7,
                MinSubWordLength = 3
            };
            var puz = await WordScapePuzzle.CreateNextPuzzleTask(parms);
            var grid = puz.genGrid;
            Console.WriteLine(puz.wordContainer.InitialWord);
            foreach (var x in grid!._dictPlacedWords)
            {
                Console.WriteLine($"Placed word {x.Key} at {x.Value.nX},{x.Value.nY} horiz={x.Value.IsHoriz}");
            }
            // dump the grid
            for (int y = 0; y < grid!._MaxY; y++)
            {
                var sb = new StringBuilder();
                for (int x = 0; x < grid!._MaxX; x++)
                {
                    sb.Append(grid!._chars[x, y]);
                    sb.Append('_');
                }
                Console.WriteLine(sb.ToString());
            }
        }

        [TestMethod]
        public void TestDebugModeConsistentResults()
        {
            // Enable debug mode
            DebugHelper.SetDebugMode(true);
            
            var gameService1 = new WordScapeGameService();
            var gameService2 = new WordScapeGameService();
            
            // Both services should use the same fixed seed (1) in debug mode
            var letters1 = new List<char> { 'A', 'B', 'C', 'D', 'E', 'F' };
            var letters2 = new List<char> { 'A', 'B', 'C', 'D', 'E', 'F' };
            
            var shuffled1 = gameService1.ShuffleCircleLetters(letters1);
            var shuffled2 = gameService2.ShuffleCircleLetters(letters2);
            
            // Results should be identical when using fixed seed
            Assert.AreEqual(shuffled1.Count, shuffled2.Count, "Shuffled arrays should have same length");
            for (int i = 0; i < shuffled1.Count; i++)
            {
                Assert.AreEqual(shuffled1[i], shuffled2[i], $"Letter at position {i} should match in debug mode");
            }
            
            Console.WriteLine($"Debug mode shuffle 1: {string.Join("", shuffled1)}");
            Console.WriteLine($"Debug mode shuffle 2: {string.Join("", shuffled2)}");
            
            // Disable debug mode
            DebugHelper.SetDebugMode(false);
        }

        [TestMethod]
        public void TestDebugModeToggle()
        {
            // Test that calling OnDebugModeChanged actually resets the random seed
            DebugHelper.SetDebugMode(false);
            var gameService = new WordScapeGameService();
            
            // Enable debug mode and reset
            DebugHelper.SetDebugMode(true);
            gameService.OnDebugModeChanged();
            
            var letters = new List<char> { 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H' };
            var shuffled1 = gameService.ShuffleCircleLetters(letters.ToList());
            
            // Reset again - should produce same result due to fixed seed
            gameService.OnDebugModeChanged();
            var shuffled2 = gameService.ShuffleCircleLetters(letters.ToList());
            
            // Results should be identical due to fixed seed reset
            CollectionAssert.AreEqual(shuffled1, shuffled2, "Results should be identical after debug mode reset");
            
            Console.WriteLine($"First shuffle:  {string.Join("", shuffled1)}");
            Console.WriteLine($"Second shuffle: {string.Join("", shuffled2)}");
            
            // Clean up
            DebugHelper.SetDebugMode(false);
        }
    }
}
