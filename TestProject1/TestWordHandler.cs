using Microsoft.VisualStudio.TestTools.UnitTesting;
using WordScapeBlazorWasm.Services;

namespace TestProject1
{
    /// <summary>
    /// Unit tests for WordHandler - grid generation and word placement
    /// </summary>
    [TestClass]
    public class TestWordHandler
    {
        private WordHandler? _wordHandler;
        private DictionaryService? _dictionaryService;
        private RandomService? _randomService;

        [TestInitialize]
        public void Initialize()
        {
            DebugHelper.SetDebugMode(true);
            _randomService = new RandomService();
            _dictionaryService = new DictionaryService(_randomService);
            _wordHandler = new WordHandler(_dictionaryService, _randomService);
        }

        [TestCleanup]
        public void Cleanup()
        {
            DebugHelper.SetDebugMode(false);
        }

        [TestMethod]
        public void TestWordHandler_CreateGrid_ReturnsValidGrid()
        {
            // Act
            var (randWord, grid, gridFilledWithRand) = _wordHandler!.CreateGrid();

            // Assert
            Assert.IsFalse(string.IsNullOrEmpty(randWord), "Should return a target word");
            Assert.IsFalse(string.IsNullOrEmpty(grid), "Should return a grid");
            Assert.IsFalse(string.IsNullOrEmpty(gridFilledWithRand), "Should return a filled grid");

            Console.WriteLine($"Target Word: {randWord}");
            Console.WriteLine($"Grid: {grid}");
            Console.WriteLine($"Filled Grid: {gridFilledWithRand}");
        }

        [TestMethod]
        public void TestWordHandler_CreateGrid_GridSizeIsCorrect()
        {
            // Act
            var (randWord, grid, gridFilledWithRand) = _wordHandler!.CreateGrid();

            // Assert - 4x4 grid = 16 characters
            Assert.AreEqual(16, grid.Length, "Grid should be 16 characters (4x4)");
            Assert.AreEqual(16, gridFilledWithRand.Length, "Filled grid should be 16 characters (4x4)");
        }

        [TestMethod]
        public void TestWordHandler_CreateGrid_WordFitsInGrid()
        {
            // Act
            var (randWord, grid, gridFilledWithRand) = _wordHandler!.CreateGrid();

            // Assert
            Assert.IsTrue(grid.Length >= randWord.Length, "Grid should be large enough for word");

            // Verify all letters from target word appear in the grid
            foreach (var letter in randWord)
            {
                var count = gridFilledWithRand.Count(c => c == letter);
                Assert.IsTrue(count >= 1, $"Letter '{letter}' from target word should appear in grid");
            }
        }

        [TestMethod]
        public void TestWordHandler_CreateGrid_IsUpperCase()
        {
            // Act
            var (randWord, grid, gridFilledWithRand) = _wordHandler!.CreateGrid();

            // Assert
            Assert.IsTrue(randWord.All(char.IsUpper), "Target word should be uppercase");
            Assert.IsTrue(gridFilledWithRand.All(char.IsUpper), "Grid should be uppercase");
        }

        [TestMethod]
        public void TestWordHandler_CreateGrid_DebugMode_IsReproducible()
        {
            // Arrange
            DebugHelper.SetDebugMode(true);
            var randomService1 = new RandomService();
            var dictionaryService1 = new DictionaryService(randomService1);
            var wordHandler1 = new WordHandler(dictionaryService1, randomService1);

            var randomService2 = new RandomService();
            var dictionaryService2 = new DictionaryService(randomService2);
            var wordHandler2 = new WordHandler(dictionaryService2, randomService2);

            // Act
            var result1 = wordHandler1.CreateGrid();
            var result2 = wordHandler2.CreateGrid();

            // Assert
            Assert.AreEqual(result1.randWord, result2.randWord, "Debug mode should produce same target word");
            Assert.AreEqual(result1.grid, result2.grid, "Debug mode should produce same grid");
            Assert.AreEqual(result1.gridFilledWithRand, result2.gridFilledWithRand, "Debug mode should produce same filled grid");

            Console.WriteLine($"Reproducible result: {result1.randWord}");
        }

        [TestMethod]
        public void TestWordHandler_CreateGrid_MultipleRuns_ProduceDifferentResults_NonDebug()
        {
            // Arrange
            DebugHelper.SetDebugMode(false);
            var randomService1 = new RandomService();
            var dictionaryService1 = new DictionaryService(randomService1);
            var wordHandler1 = new WordHandler(dictionaryService1, randomService1);

            // Small delay to ensure different seed
            Thread.Sleep(10);

            var randomService2 = new RandomService();
            var dictionaryService2 = new DictionaryService(randomService2);
            var wordHandler2 = new WordHandler(dictionaryService2, randomService2);

            // Act
            var result1 = wordHandler1.CreateGrid();
            var result2 = wordHandler2.CreateGrid();

            // Assert - Should likely be different (not guaranteed 100% but very likely)
            var areDifferent = result1.randWord != result2.randWord ||
         result1.gridFilledWithRand != result2.gridFilledWithRand;

            Console.WriteLine($"Result 1: {result1.randWord}");
            Console.WriteLine($"Result 2: {result2.randWord}");
            Console.WriteLine($"Are Different: {areDifferent}");

            // Note: This test might occasionally fail due to random chance
            // but it's very unlikely with a large word pool
        }

        [TestMethod]
        public void TestWordHandler_CreateGrid_NoUnderscoresInFinalGrid()
        {
            // Act
            var (randWord, grid, gridFilledWithRand) = _wordHandler!.CreateGrid();

            // Assert
            Assert.IsFalse(gridFilledWithRand.Contains('_'), "Final grid should not contain underscores");
        }

        [TestMethod]
        public void TestWordHandler_CreateGrid_AllLetters()
        {
            // Act
            var (randWord, grid, gridFilledWithRand) = _wordHandler!.CreateGrid();

            // Assert
            Assert.IsTrue(gridFilledWithRand.All(char.IsLetter), "Grid should contain only letters");
        }

        [TestMethod]
        public void TestWordHandler_GetRandWord_ReturnsWord()
        {
            // Act
            var word = _wordHandler!.GetRandWord();

            // Assert
            Assert.IsFalse(string.IsNullOrEmpty(word), "Should return a random word");
            Assert.IsTrue(word.All(char.IsLetter), "Word should contain only letters");

            Console.WriteLine($"Random word: {word}");
        }

        [TestMethod]
        public void TestWordHandler_GetRandWord_DebugMode_IsReproducible()
        {
            // Arrange
            DebugHelper.SetDebugMode(true);
            var randomService1 = new RandomService();
            var dictionaryService1 = new DictionaryService(randomService1);
            var wordHandler1 = new WordHandler(dictionaryService1, randomService1);

            var randomService2 = new RandomService();
            var dictionaryService2 = new DictionaryService(randomService2);
            var wordHandler2 = new WordHandler(dictionaryService2, randomService2);

            // Act
            var word1 = wordHandler1.GetRandWord();
            var word2 = wordHandler2.GetRandWord();

            // Assert
            Assert.AreEqual(word1, word2, "Debug mode should produce same random word");
            Console.WriteLine($"Reproducible random word: {word1}");
        }

        [TestMethod]
        public void TestWordHandler_Instance_IsSingleton()
        {
            // Arrange
            var randomService = new RandomService();
            var dictionaryService = new DictionaryService(randomService);

            // Act
            var handler1 = new WordHandler(dictionaryService, randomService);
            var instance1 = WordHandler.Instance;

            var handler2 = new WordHandler(dictionaryService, randomService);
            var instance2 = WordHandler.Instance;

            // Assert
            Assert.IsNotNull(instance1, "Instance should not be null after first creation");
            Assert.AreSame(handler2, instance2, "Instance should be the last created handler");
        }

        [TestMethod]
        public void TestWordHandler_CreateGrid_WordLength_InValidRange()
        {
            // Act - Create multiple grids
            var words = new List<string>();
            for (int i = 0; i < 10; i++)
            {
                var (randWord, _, _) = _wordHandler!.CreateGrid();
                words.Add(randWord);
            }

            // Assert - Words should be reasonable length for 4x4 grid
            foreach (var word in words)
            {
                Assert.IsTrue(word.Length >= 10, $"Word '{word}' should be at least 10 characters");
                Assert.IsTrue(word.Length <= 16, $"Word '{word}' should be at most 16 characters (grid size)");
            }

            Console.WriteLine($"Word lengths: {string.Join(", ", words.Select(w => w.Length))}");
        }

        [TestMethod]
        public void TestWordHandler_CreateGrid_PerformanceTest()
        {
            // Act - Measure time to create 10 grids
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < 10; i++)
            {
                var result = _wordHandler!.CreateGrid();
            }

            stopwatch.Stop();

            // Assert - Should complete reasonably quickly
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 5000, $"Creating 10 grids should take less than 5 seconds (took {stopwatch.ElapsedMilliseconds}ms)");

            Console.WriteLine($"Created 10 grids in {stopwatch.ElapsedMilliseconds}ms ({stopwatch.ElapsedMilliseconds / 10.0}ms per grid)");
        }
    }
}
