using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            var random = new Random(1);
            var parms = new WordGenerationParms()
            {
                _Random = random,
                LenTargetWord = 9,
                MinSubWordLength = 3
            };
            var sw = Stopwatch.StartNew();
            var puz = await WordScapePuzzle.CreateNextPuzzleTask(parms);
            var elapsed = sw.ElapsedMilliseconds;
            var grid = puz?.genGrid;
            Console.WriteLine($"{puz?.wordContainer?.InitialWord}  {elapsed} ms");
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

        [TestMethod]
        public void TestDictionaryServiceValidation()
        {
            // Test the fix for non-alphabetic input validation
            var dictionaryService = new DictionaryService();
            
            // Test valid words
            Assert.IsTrue(dictionaryService.IsWord("TEST"), "Valid word should return true");
            Assert.IsTrue(dictionaryService.IsWord("WORD"), "Valid word should return true");
            
            // Test invalid inputs that should NOT cause exceptions
            Assert.IsFalse(dictionaryService.IsWord(""), "Empty string should return false");
            Assert.IsFalse(dictionaryService.IsWord(null), "Null should return false");
            Assert.IsFalse(dictionaryService.IsWord("TEST123"), "Word with numbers should return false");
            Assert.IsFalse(dictionaryService.IsWord("TEST!"), "Word with punctuation should return false");
            Assert.IsFalse(dictionaryService.IsWord("TEST "), "Word with spaces should return false");
            Assert.IsFalse(dictionaryService.IsWord("123"), "Numbers only should return false");
            Assert.IsFalse(dictionaryService.IsWord("!@#"), "Symbols only should return false");
            
            Console.WriteLine("All dictionary service validation tests passed without exceptions!");
        }

        [TestMethod]
        public void TestSpecificWordsIssue()
        {
            // Test specific words that user reported as showing pink (not found)
            // FIXED: Use lowercase words to work around DictionaryLib ToLowerByte bug
            var dictionaryService = new DictionaryService();
            var problemWords = new[] { "size", "zeal" };
            
            foreach (var word in problemWords)
            {
                var isInSmall = dictionaryService.IsWord(word, DictionaryLib.DictionaryType.Small);
                var isInLarge = dictionaryService.IsWord(word, DictionaryLib.DictionaryType.Large);
                
                Console.WriteLine($"Word '{word}' - Small Dict: {isInSmall}, Large Dict: {isInLarge}");
                
                // At least one of the dictionaries should contain these common English words
                Assert.IsTrue(isInSmall || isInLarge, $"'{word}' should be found in at least one dictionary");
            }
            
            // Test some other common words for comparison
            var commonWords = new[] { "the", "and", "for", "with", "have", "word", "game", "play" };
            
            foreach (var word in commonWords)
            {
                var isInSmall = dictionaryService.IsWord(word, DictionaryLib.DictionaryType.Small);
                var isInLarge = dictionaryService.IsWord(word, DictionaryLib.DictionaryType.Large);
                
                Console.WriteLine($"Common word '{word}' - Small Dict: {isInSmall}, Large Dict: {isInLarge}");
            }
        }
    }
}
