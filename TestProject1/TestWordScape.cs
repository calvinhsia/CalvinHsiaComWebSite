using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BlazorWasm.Models;
using BlazorWasm.Services;

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

        /// <summary>
        /// 🔍 TEST: Verify centralized RandomService is used throughout the application
        /// This test verifies that all components use the same Random instance from RandomService
        /// </summary>
        [TestMethod]
        public void TestCentralizedRandomServiceUsage()
        {
            Console.WriteLine("=== Testing Centralized RandomService Usage ===");
            Console.WriteLine();

            // Enable debug mode for reproducible results
            DebugHelper.SetDebugMode(true);
            Console.WriteLine("✅ Debug mode enabled (fixed seed = 1)");
            Console.WriteLine();

            // Create centralized services
            var randomService = new RandomService();
            var dictionaryService = new DictionaryService(randomService);
            var gameService = new WordScapeGameService(dictionaryService, randomService);

            Console.WriteLine("📊 Service State:");
            Console.WriteLine($"   {randomService.GetStateDescription()}");
            Console.WriteLine();

            // Get the Random instance from RandomService
            var centralizedRandom = randomService.GetRandom();
            var centralizedRandomId = centralizedRandom.GetHashCode().ToString("X8");
            Console.WriteLine($"🎲 Centralized Random ID: {centralizedRandomId}");
            Console.WriteLine();

            // Test 1: Create WordContainer using centralized Random
            Console.WriteLine("TEST 1: Create WordContainer with centralized Random");
            var wordContainer = new WordContainer
            {
                InitialWord = "SOMETHING",
                subwords = new List<string> { "SOME", "THING", "METH", "HOME" }
            };
            Console.WriteLine($"   ✅ WordContainer created");
            Console.WriteLine();

            // Test 2: Create GenGrid using GameService (should use centralized Random)
            Console.WriteLine("TEST 2: Create GenGrid via GameService.CreateGenGrid()");
            var genGrid1 = gameService.CreateGenGrid(15, 15, wordContainer);
            var genGrid1RandomId = genGrid1._random.GetHashCode().ToString("X8");
            Console.WriteLine($"   GenGrid Random ID: {genGrid1RandomId}");
            Console.WriteLine($"   Match centralized? {(genGrid1RandomId == centralizedRandomId ? "✅ YES" : "❌ NO")}");
            Console.WriteLine();

            // Test 3: Create GenGrid directly (passing centralized Random)
            Console.WriteLine("TEST 3: Create GenGrid directly with centralized Random");
            var genGrid2 = new GenGrid(15, 15, wordContainer, centralizedRandom);
            var genGrid2RandomId = genGrid2._random.GetHashCode().ToString("X8");
            Console.WriteLine($"   GenGrid Random ID: {genGrid2RandomId}");
            Console.WriteLine($"   Match centralized? {(genGrid2RandomId == centralizedRandomId ? "✅ YES" : "❌ NO")}");
            Console.WriteLine();

            // Test 4: Verify GameService uses centralized Random internally
            Console.WriteLine("TEST 4: GameService internal Random usage");
            var gameServiceState = randomService.GetStateDescription();
            Console.WriteLine($"   {gameServiceState}");
            Console.WriteLine();

            // Test 5: Create a new Random(1) and verify it's DIFFERENT
            Console.WriteLine("TEST 5: Create separate Random(1) for comparison");
            var separateRandom = new Random(1);
            var separateRandomId = separateRandom.GetHashCode().ToString("X8");
            Console.WriteLine($"   Separate Random(1) ID: {separateRandomId}");
            Console.WriteLine($"   Different from centralized? {(separateRandomId != centralizedRandomId ? "✅ YES (as expected)" : "❌ NO (PROBLEM!)")}");
            Console.WriteLine();

            // Final verification
            Console.WriteLine("=== FINAL VERIFICATION ===");
            Console.WriteLine();

            bool allMatch = (genGrid1RandomId == centralizedRandomId) &&
                           (genGrid2RandomId == centralizedRandomId);

            if (allMatch)
            {
                Console.WriteLine("✅ SUCCESS: All components use the centralized Random instance!");
                Console.WriteLine($"   All Random IDs match: {centralizedRandomId}");
            }
            else
            {
                Console.WriteLine("❌ FAILURE: Not all components use centralized Random!");
                Console.WriteLine($"   Centralized Random ID: {centralizedRandomId}");
                Console.WriteLine($"   GenGrid1 Random ID:     {genGrid1RandomId} {(genGrid1RandomId == centralizedRandomId ? "✅" : "❌")}");
                Console.WriteLine($"   GenGrid2 Random ID:     {genGrid2RandomId} {(genGrid2RandomId == centralizedRandomId ? "✅" : "❌")}");
            }

            Console.WriteLine();
            Console.WriteLine("💡 TIP: Look for log messages starting with 🎲 to trace Random usage");

            // Assert that all use the same Random instance
            Assert.AreEqual(centralizedRandomId, genGrid1RandomId,
                "GenGrid created via GameService.CreateGenGrid() should use centralized Random");
            Assert.AreEqual(centralizedRandomId, genGrid2RandomId,
                "GenGrid created directly should use centralized Random when passed explicitly");
        }

        [TestMethod]
        public void TestSharedDictionaryService()
        {
            // Test that the DictionaryService is properly registered and shared
            var randomService = new RandomService();
            var dictionaryService = new DictionaryService(randomService);

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
            var randomService = new RandomService();
            var dictionaryService = new DictionaryService(randomService);

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
            var randomService = new RandomService();
            var dictionaryService = new DictionaryService(randomService);
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

        /// <summary>
        /// Quick start: Creates an interactive HTML test page
        /// Run this test and it will open a browser with a test interface
        /// </summary>
        [TestMethod]
        [TestCategory("Manual")]
        public void QuickStart_InteractiveHtmlTest()
        {
            Console.WriteLine("=== Quick Start Interactive Test ===");
            Console.WriteLine();
            Console.WriteLine("This test creates a standalone HTML file for experimenting with your Blazor app.");
            Console.WriteLine();

            // Create a simple HTML test page
            var htmlContent = @"<!DOCTYPE html>
<html>
<head>
    <title>Blazor WASM Quick Test</title>
    <style>
        body { 
            font-family: Arial, sans-serif; 
            max-width: 1200px; 
            margin: 20px auto; 
            padding: 20px; 
            background: #f5f5f5;
        }
        .container {
            background: white;
            padding: 30px;
            border-radius: 8px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }
        h1 { color: #333; }
        .grid {
            display: grid;
            grid-template-columns: repeat(4, 100px);
            gap: 10px;
            margin: 20px 0;
        }
        .cell {
            width: 100px;
            height: 100px;
            background: #4CAF50;
            color: white;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 32px;
            border-radius: 8px;
            cursor: pointer;
            transition: all 0.2s;
        }
        .cell:hover {
            transform: scale(1.1);
            background: #45a049;
        }
        .cell.selected {
            background: #ff9800;
        }
        .output {
            margin: 20px 0;
            padding: 15px;
            background: #e3f2fd;
            border-left: 4px solid #2196F3;
            border-radius: 4px;
        }
        button {
            padding: 12px 24px;
            margin: 5px;
            background: #2196F3;
            color: white;
            border: none;
            border-radius: 4px;
            cursor: pointer;
            font-size: 16px;
        }
        button:hover {
            background: #1976D2;
        }
    </style>
</head>
<body>
    <div class='container'>
        <h1>🧪 Blazor WASM Quick Test</h1>
        <p>Click on the cells to select them. Experiment with the code below!</p>
        
        <div class='grid' id='grid'></div>
        
        <div>
            <button onclick='clearSelection()'>Clear Selection</button>
            <button onclick='randomSelect()'>Random Select</button>
            <button onclick='console.log(selectedWord)'>Log Word</button>
        </div>
        
        <div class='output'>
            <strong>Selected Word:</strong> <span id='word'>(none)</span><br>
            <strong>Length:</strong> <span id='length'>0</span>
        </div>
        
        <details style='margin-top: 20px;'>
            <summary style='cursor: pointer; font-weight: bold;'>📝 How to experiment</summary>
            <ul>
                <li>Open browser DevTools (F12) to see console output</li>
                <li>Edit this HTML file to change the layout and styling</li>
                <li>Modify the JavaScript below to test different interactions</li>
                <li>Refresh the browser to see your changes</li>
            </ul>
        </details>
    </div>

    <script>
        // Sample letters for the grid
        const letters = ['W','O','R','D','S','C','A','P','E','T','E','S','T','X','Y','Z'];
        let selectedCells = [];
        let selectedWord = '';
        
        // Initialize grid
        const grid = document.getElementById('grid');
        letters.forEach((letter, idx) => {
            const cell = document.createElement('div');
            cell.className = 'cell';
            cell.textContent = letter;
            cell.dataset.index = idx;
            cell.dataset.letter = letter;
            cell.onclick = () => toggleCell(cell);
            grid.appendChild(cell);
        });
        
        function toggleCell(cell) {
            const index = parseInt(cell.dataset.index);
            
            if (cell.classList.contains('selected')) {
                cell.classList.remove('selected');
                selectedCells = selectedCells.filter(i => i !== index);
            } else {
                cell.classList.add('selected');
                selectedCells.push(index);
            }
            
            updateWord();
            console.log('Cell toggled:', cell.dataset.letter);
        }
        
        function updateWord() {
            selectedWord = selectedCells
                .map(idx => letters[idx])
                .join('');
            
            document.getElementById('word').textContent = selectedWord || '(none)';
            document.getElementById('length').textContent = selectedWord.length;
        }
        
        function clearSelection() {
            document.querySelectorAll('.cell').forEach(cell => {
                cell.classList.remove('selected');
            });
            selectedCells = [];
            updateWord();
            console.log('Selection cleared');
        }
        
        function randomSelect() {
            clearSelection();
            const count = Math.floor(Math.random() * 5) + 3;
            const cells = Array.from(document.querySelectorAll('.cell'));
            
            for (let i = 0; i < count; i++) {
                const randomCell = cells[Math.floor(Math.random() * cells.length)];
                if (!randomCell.classList.contains('selected')) {
                    toggleCell(randomCell);
                }
            }
            
            console.log('Random selection:', selectedWord);
        }
        
        console.log('Test page loaded! Try clicking on cells.');
        console.log('Modify this file to experiment with HTML, CSS, and JavaScript!');
    </script>
</body>
</html>";

            var outputPath = Path.Combine(
                Path.GetDirectoryName(typeof(TestWordScape).Assembly.Location)!,
                "quick-test.html"
            );

            File.WriteAllText(outputPath, htmlContent);

            Console.WriteLine($"✅ Test file created: {outputPath}");
            Console.WriteLine();
            Console.WriteLine("What you can do:");
            Console.WriteLine("  1. Open the file in your browser");
            Console.WriteLine("  2. Click on cells to select letters");
            Console.WriteLine("  3. Edit the HTML file to experiment with:");
            Console.WriteLine("     - CSS styles (colors, sizes, animations)");
            Console.WriteLine("     - HTML layout (grid size, button placement)");
            Console.WriteLine("     - JavaScript interactions (drag & drop, word validation)");
            Console.WriteLine("  4. Refresh browser to see your changes");
            Console.WriteLine();
            Console.WriteLine("Opening in browser...");

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = outputPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not auto-open: {ex.Message}");
                Console.WriteLine($"Manually open: {outputPath}");
            }
        }
    }
}
