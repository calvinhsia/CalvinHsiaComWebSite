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
            var grid = puz?.genGrid;
            Console.WriteLine(puz?.wordContainer?.InitialWord);
            if (grid?._dictPlacedWords != null)
            {
                foreach (var x in grid._dictPlacedWords)
                {
                    Console.WriteLine($"Placed word {x.Key} at {x.Value.nX},{x.Value.nY} horiz={x.Value.IsHoriz}");
                }
            }
            // dump the grid
            if (grid != null)
            {
                for (int y = 0; y < grid._MaxY; y++)
                {
                    var sb = new StringBuilder();
                    for (int x = 0; x < grid._MaxX; x++)
                    {
                        sb.Append(grid._chars[x, y]);
                        sb.Append('_');
                    }
                    Console.WriteLine(sb.ToString());
                }
            }
        }

        [TestMethod]
        public void TestSharedDictionaryService()
        {
            // Test that the DictionaryService is properly registered and shared
            var dictionaryService = new DictionaryService();
            
            // Test that both dictionary instances are created lazily
            Assert.IsNotNull(dictionaryService.SmallDictionary, "Small dictionary should be available");
            Assert.IsNotNull(dictionaryService.LargeDictionary, "Large dictionary should be available");
            
            // Test word validation
            var testWord = "TEST";
            var isValidSmall = dictionaryService.IsWord(testWord, DictionaryLib.DictionaryType.Small);
            var isValidLarge = dictionaryService.IsWord(testWord, DictionaryLib.DictionaryType.Large);
            
            Console.WriteLine($"Word '{testWord}' - Small Dict: {isValidSmall}, Large Dict: {isValidLarge}");
            
            // Test that the same instance is returned on subsequent calls
            var smallDict1 = dictionaryService.SmallDictionary;
            var smallDict2 = dictionaryService.SmallDictionary;
            Assert.AreSame(smallDict1, smallDict2, "Should return same instance");
        }
    }
}
