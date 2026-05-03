using Microsoft.VisualStudio.TestTools.UnitTesting;
using BlazorWasm.Services;

namespace TestProject1
{
    /// <summary>
    /// Unit tests for RandomService - ensures reproducible random number generation
    /// </summary>
    [TestClass]
    public class TestRandomService
    {
        [TestInitialize]
        public void Initialize()
        {
            // Ensure debug mode is off for these tests (unless testing debug mode specifically)
            DebugHelper.SetDebugMode(false);
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Reset to default state
            DebugHelper.SetDebugMode(false);
        }

        [TestMethod]
        public void TestRandomService_CreatesRandomInstance()
        {
            // Arrange & Act
            var randomService = new RandomService();
            var random = randomService.GetRandom();

            // Assert
            Assert.IsNotNull(random, "GetRandom() should return a Random instance");
        }

        [TestMethod]
        public void TestRandomService_DebugMode_UsesFixedSeed()
        {
            // Arrange
            DebugHelper.SetDebugMode(true);
            var randomService1 = new RandomService();
            var randomService2 = new RandomService();

            // Act
            var value1a = randomService1.GetRandom().Next(1000);
            var value1b = randomService1.GetRandom().Next(1000);

            var value2a = randomService2.GetRandom().Next(1000);
            var value2b = randomService2.GetRandom().Next(1000);

            // Assert
            Console.WriteLine($"Service1: {value1a}, {value1b}");
            Console.WriteLine($"Service2: {value2a}, {value2b}");

            Assert.AreEqual(value1a, value2a, "Both services should produce same first value in debug mode");
            Assert.AreEqual(value1b, value2b, "Both services should produce same second value in debug mode");
        }

        [TestMethod]
        public void TestRandomService_NonDebugMode_UsesDifferentSeeds()
        {
            // Arrange
            DebugHelper.SetDebugMode(false);
            var randomService1 = new RandomService();
            var randomService2 = new RandomService();

            // Act
            var values1 = Enumerable.Range(0, 10).Select(_ => randomService1.GetRandom().Next(1000)).ToList();
            var values2 = Enumerable.Range(0, 10).Select(_ => randomService2.GetRandom().Next(1000)).ToList();

            // Assert
            var allSame = values1.SequenceEqual(values2);
            Assert.IsFalse(allSame, "Different RandomService instances should produce different sequences in non-debug mode");
        }

        [TestMethod]
        public void TestRandomService_Reset_RecreatesRandom()
        {
            // Arrange
            DebugHelper.SetDebugMode(true);
            var randomService = new RandomService();
            var firstValue = randomService.GetRandom().Next(1000);

            // Act
            randomService.Reset();
            var secondValue = randomService.GetRandom().Next(1000);

            // Assert
            Assert.AreEqual(firstValue, secondValue, "Reset should recreate Random with same seed in debug mode");
        }

        [TestMethod]
        public void TestRandomService_GetStateDescription_ReturnsInfo()
        {
            // Arrange
            var randomService = new RandomService();
            randomService.GetRandom(); // Initialize the random

            // Act
            var description = randomService.GetStateDescription();

            // Assert
            Assert.IsNotNull(description, "GetStateDescription should return a string");
            Assert.IsTrue(description.Contains("RandomService"), "Description should contain 'RandomService'");
            Console.WriteLine($"State: {description}");
        }

        [TestMethod]
        public void TestRandomService_SwitchDebugMode_RecreatesRandom()
        {
            // Arrange
            DebugHelper.SetDebugMode(false);
            var randomService = new RandomService();
            var random1 = randomService.GetRandom();
            var value1 = random1.Next(1000);

            // Act - Switch to debug mode
            DebugHelper.SetDebugMode(true);
            randomService.Reset(); // Force recreation
            var random2 = randomService.GetRandom();
            var value2 = random2.Next(1000);

            // Act - Create new service with debug mode
            var randomService2 = new RandomService();
            var value3 = randomService2.GetRandom().Next(1000);

            // Assert
            Assert.AreEqual(value2, value3, "Both should use same fixed seed after switching to debug mode");
            Console.WriteLine($"NonDebug: {value1}, Debug After Reset: {value2}, New Debug Service: {value3}");
        }

        [TestMethod]
        public void TestRandomService_MultipleGetRandom_ReturnsSameInstance()
        {
            // Arrange
            var randomService = new RandomService();

            // Act
            var random1 = randomService.GetRandom();
            var random2 = randomService.GetRandom();

            // Assert
            Assert.AreSame(random1, random2, "Multiple calls to GetRandom() should return same Random instance");
        }

        [TestMethod]
        public void TestRandomService_DebugMode_ProducesReproducibleSequence()
        {
            // Arrange
            DebugHelper.SetDebugMode(true);

            // Act - First run
            var service1 = new RandomService();
            var sequence1 = Enumerable.Range(0, 20).Select(_ => service1.GetRandom().Next(1000)).ToList();

            // Act - Second run
            var service2 = new RandomService();
            var sequence2 = Enumerable.Range(0, 20).Select(_ => service2.GetRandom().Next(1000)).ToList();

            // Assert
            CollectionAssert.AreEqual(sequence1, sequence2, "Debug mode should produce identical sequences");

            Console.WriteLine("Sequence 1: " + string.Join(", ", sequence1.Take(10)));
            Console.WriteLine("Sequence 2: " + string.Join(", ", sequence2.Take(10)));
        }
    }
}
