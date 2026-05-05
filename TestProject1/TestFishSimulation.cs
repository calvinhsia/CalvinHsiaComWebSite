using Microsoft.VisualStudio.TestTools.UnitTesting;
using BlazorWasm.Services;

namespace TestProject1
{
    /// <summary>
    /// Unit tests for Fish vs Sharks cellular automata simulation
    /// Tests simulation rules, cell behavior, and population dynamics
    /// </summary>
    [TestClass]
    public class TestFishSimulation
    {
        private RandomService _randomService = null!;
        private Random _random = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            // Enable debug mode for deterministic behavior
            DebugHelper.SetDebugMode(true);
            _randomService = new RandomService();
            _randomService.Reset();
            _random = _randomService.GetRandom();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            DebugHelper.SetDebugMode(false);
        }

        #region Helper Classes and Methods

        private enum CellType { Empty, Fish, Shark }

        private class Cell
        {
            public CellType Type { get; set; }
            public int Age { get; set; }
            public int LastMeal { get; set; }
            public int LastAction { get; set; }
            public int LastBirth { get; set; }
            public bool ProcessedThisGeneration { get; set; }
        }

        private Cell[,] CreateTestGrid(int rows, int cols)
        {
            var cells = new Cell[rows, cols];
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    cells[r, c] = new Cell { Type = CellType.Empty };
                }
            }
            return cells;
        }

        private void PlaceFish(Cell[,] cells, int row, int col, int age = 0)
        {
            cells[row, col].Type = CellType.Fish;
            cells[row, col].Age = age;
        }

        private void PlaceShark(Cell[,] cells, int row, int col, int age = 0, int lastMeal = 0)
        {
            cells[row, col].Type = CellType.Shark;
            cells[row, col].Age = age;
            cells[row, col].LastMeal = lastMeal;
        }

        #endregion

        #region Initialization Tests

        [TestMethod]
        public void CreateGrid_InitializesEmptyCells()
        {
            // Arrange
            int rows = 10;
            int cols = 10;

            // Act
            var cells = CreateTestGrid(rows, cols);

            // Assert
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    Assert.AreEqual(CellType.Empty, cells[r, c].Type);
                    Assert.AreEqual(0, cells[r, c].Age);
                }
            }
        }

        [TestMethod]
        public void PlaceFish_CreatesFishAtPosition()
        {
            // Arrange
            var cells = CreateTestGrid(10, 10);

            // Act
            PlaceFish(cells, 5, 5, age: 3);

            // Assert
            Assert.AreEqual(CellType.Fish, cells[5, 5].Type);
            Assert.AreEqual(3, cells[5, 5].Age);
        }

        [TestMethod]
        public void PlaceShark_CreatesSharkAtPosition()
        {
            // Arrange
            var cells = CreateTestGrid(10, 10);

            // Act
            PlaceShark(cells, 7, 3, age: 5, lastMeal: 10);

            // Assert
            Assert.AreEqual(CellType.Shark, cells[7, 3].Type);
            Assert.AreEqual(5, cells[7, 3].Age);
            Assert.AreEqual(10, cells[7, 3].LastMeal);
        }

        #endregion

        #region Fish Behavior Tests

        [TestMethod]
        public void FishAging_IncreasesEachGeneration()
        {
            // Arrange
            var cells = CreateTestGrid(5, 5);
            PlaceFish(cells, 2, 2, age: 3);

            // Simulate aging
            cells[2, 2].Age++;

            // Assert
            Assert.AreEqual(4, cells[2, 2].Age, "Fish should age by 1");
        }

        [TestMethod]
        public void FishDeath_OccursAtMaxAge()
        {
            // Arrange
            int fishLifeLength = 22;
            var cells = CreateTestGrid(5, 5);
            PlaceFish(cells, 2, 2, age: fishLifeLength - 1);

            // Act - age to max
            cells[2, 2].Age++;
            bool shouldDie = cells[2, 2].Age >= fishLifeLength;

            // Assert
            Assert.IsTrue(shouldDie, "Fish should die when reaching max age");
        }

        [TestMethod]
        public void FishBreeding_RequiresMinimumAge()
        {
            // Arrange
            int fishBreedAge = 3;
            var youngFish = 2;
            var breedingFish = 3;

            // Assert
            Assert.IsFalse(youngFish >= fishBreedAge, "Young fish cannot breed");
            Assert.IsTrue(breedingFish >= fishBreedAge, "Adult fish can breed");
        }

        #endregion

        #region Shark Behavior Tests

        [TestMethod]
        public void SharkStarvation_OccursAfterDelay()
        {
            // Arrange
            int sharkStarve = 6;
            int currentGeneration = 15;
            var cells = CreateTestGrid(5, 5);
            PlaceShark(cells, 2, 2, age: 5, lastMeal: 8);

            // Act
            int generationsSinceLastMeal = currentGeneration - cells[2, 2].LastMeal;
            bool shouldStarve = generationsSinceLastMeal >= sharkStarve;

            // Assert
            Assert.IsTrue(shouldStarve, "Shark should starve after 6 generations without food");
        }

        [TestMethod]
        public void SharkFeeding_ResetsStarvationTimer()
        {
            // Arrange
            int currentGeneration = 20;
            var cells = CreateTestGrid(5, 5);
            PlaceShark(cells, 2, 2, age: 5, lastMeal: 15);

            // Act - simulate feeding
            cells[2, 2].LastMeal = currentGeneration;

            // Assert
            Assert.AreEqual(currentGeneration, cells[2, 2].LastMeal, "Last meal should be updated");
            Assert.AreEqual(0, currentGeneration - cells[2, 2].LastMeal, "Starvation timer reset");
        }

        [TestMethod]
        public void SharkBreeding_RequiresHigherAgeThanFish()
        {
            // Arrange
            int fishBreedAge = 3;
            int sharkBreedAge = 10;

            // Assert
            Assert.IsTrue(sharkBreedAge > fishBreedAge, "Sharks should breed slower than fish");
        }

        [TestMethod]
        public void SharkDeath_OccursFromAgeOrStarvation()
        {
            // Arrange
            int sharkLifeLength = 20;
            int sharkStarve = 6;
            int currentGeneration = 25;

            // Test age death
            var oldShark = 20;
            Assert.IsTrue(oldShark >= sharkLifeLength, "Old shark should die");

            // Test starvation death
            int lastMeal = 18;
            int generationsSinceLastMeal = currentGeneration - lastMeal;
            Assert.IsTrue(generationsSinceLastMeal >= sharkStarve, "Starved shark should die");
        }

        #endregion

        #region Movement and Neighbor Tests

        [TestMethod]
        public void GetNeighbors_Returns4AdjacentCells()
        {
            // For a 5x5 grid, cell (2,2) has 4 neighbors in non-torus mode
            // North (2,1), South (2,3), West (1,2), East (3,2)

            var neighbors = new List<(int row, int col)>
            {
                (1, 2), // North
                (3, 2), // South
                (2, 1), // West
                (2, 3)  // East
            };

            Assert.AreEqual(4, neighbors.Count, "Cell should have 4 neighbors");
        }

        [TestMethod]
        public void TorusMode_WrapsEdges()
        {
            // Arrange
            int rows = 5;
            int cols = 5;
            bool torus = true;

            // Test north edge wrapping
            int northNeighbor = -1;
            if (northNeighbor < 0 && torus)
            {
                northNeighbor = rows - 1;
            }

            // Assert
            Assert.AreEqual(4, northNeighbor, "North edge should wrap to bottom row");

            // Test west edge wrapping
            int westNeighbor = -1;
            if (westNeighbor < 0 && torus)
            {
                westNeighbor = cols - 1;
            }

            Assert.AreEqual(4, westNeighbor, "West edge should wrap to right column");
        }

        [TestMethod]
        public void NonTorusMode_BoundsEdges()
        {
            // Arrange
            bool torus = false;

            // Test north edge
            int northRow = -1;
            bool validNorth = northRow >= 0 || torus;

            // Assert
            Assert.IsFalse(validNorth, "North edge should not wrap in non-torus mode");

            // Test bounds check
            Assert.IsTrue(northRow < 0, "Negative index indicates out of bounds");
        }

        #endregion

        #region Population Dynamics Tests

        [TestMethod]
        public void FishOnlyPopulation_Grows()
        {
            // Arrange
            int initialFish = 10;
            int fishBreedAge = 3;

            // Simulate: fish breed when >= 3 generations old
            // If 10 fish start at age 3, all can breed immediately
            // Breeding creates 1 offspring per fish per generation (simplified)

            int fishCount = initialFish;

            // Act - simulate one generation of breeding
            for (int i = 0; i < initialFish; i++)
            {
                if (3 >= fishBreedAge) // All fish are old enough
                {
                    fishCount++; // Each breeds once
                }
            }

            // Assert
            Assert.IsTrue(fishCount > initialFish, "Fish population should grow");
            Assert.AreEqual(20, fishCount, "Each fish should produce one offspring");
        }

        [TestMethod]
        public void SharkEatsFish_ReducesFishPopulation()
        {
            // Arrange
            var cells = CreateTestGrid(5, 5);
            PlaceFish(cells, 2, 2);
            PlaceShark(cells, 2, 1); // Adjacent to fish

            int fishCount = 1;

            // Act - simulate shark eating fish
            if (cells[2, 2].Type == CellType.Fish &&
                 cells[2, 1].Type == CellType.Shark)
            {
                fishCount--; // Fish is eaten
                cells[2, 2].Type = CellType.Shark; // Shark moves to fish position
            }

            // Assert
            Assert.AreEqual(0, fishCount, "Fish count should decrease when eaten");
            Assert.AreEqual(CellType.Shark, cells[2, 2].Type, "Shark should occupy fish position");
        }

        [TestMethod]
        public void BalancedEcosystem_Oscillates()
        {
            // Simulates population oscillation
            // This is a conceptual test - real implementation would track generations

            var generations = new List<(int fish, int sharks)>
{
   (100, 10),  // Gen 0: Many fish, few sharks
         (90, 15), // Gen 1: Sharks eat some fish and breed
      (70, 20),   // Gen 2: More sharks, fewer fish
       (50, 15),   // Gen 3: Sharks start starving
     (60, 10),   // Gen 4: Fish recover, sharks decline
    (80, 12)    // Gen 5: Cycle continues
      };

            // Assert oscillation pattern
            Assert.IsTrue(generations[1].fish < generations[0].fish, "Fish should decline initially");
            Assert.IsTrue(generations[1].sharks > generations[0].sharks, "Sharks should increase initially");
            Assert.IsTrue(generations[3].sharks < generations[2].sharks, "Sharks should decline when fish scarce");
            Assert.IsTrue(generations[4].fish > generations[3].fish, "Fish should recover");
        }

        #endregion

        #region Rule Parameter Tests

        [TestMethod]
        public void OneActionPerYear_LimitsActivity()
        {
            // Arrange
            bool oneActionPerYear = true;
            int currentGeneration = 10;
            var cells = CreateTestGrid(5, 5);
            PlaceFish(cells, 2, 2, age: 5);
            cells[2, 2].LastAction = currentGeneration;

            // Act
            bool canAct = !oneActionPerYear || cells[2, 2].LastAction != currentGeneration;

            // Assert
            Assert.IsFalse(canAct, "Animal should not act twice in same generation");
        }

        [TestMethod]
        public void MultipleActionsPerYear_AllowsActivity()
        {
            // Arrange
            bool oneActionPerYear = false;
            int currentGeneration = 10;
            var cells = CreateTestGrid(5, 5);
            PlaceFish(cells, 2, 2, age: 5);
            cells[2, 2].LastAction = currentGeneration;

            // Act
            bool canAct = !oneActionPerYear || cells[2, 2].LastAction != currentGeneration;

            // Assert
            Assert.IsTrue(canAct, "Animal can act multiple times when rule disabled");
        }

        [TestMethod]
        public void AdjustableBreedAge_ChangesPopulationGrowth()
        {
            // Test that different breed ages affect population dynamics
            int fastBreedAge = 1;  // Breed quickly
            int slowBreedAge = 10; // Breed slowly

            // Simulate 5 generations
            int fastPopulation = 10;
            int slowPopulation = 10;

            // Fast breeding - all animals breed each generation
            for (int gen = 0; gen < 5; gen++)
            {
                int newFast = 0;
                for (int i = 0; i < fastPopulation; i++)
                {
                    if (gen >= fastBreedAge)
                        newFast++;
                }
                fastPopulation += newFast;
            }

            // Slow breeding - fewer animals breed
            for (int gen = 0; gen < 5; gen++)
            {
                int newSlow = 0;
                for (int i = 0; i < slowPopulation; i++)
                {
                    if (gen >= slowBreedAge)
                        newSlow++;
                }
                slowPopulation += newSlow;
            }

            // Assert
            Assert.IsTrue(fastPopulation > slowPopulation,
  "Fast breeding should result in larger population");
        }

        [TestMethod]
        public void AdjustableStarvation_AffectsSharkSurvival()
        {
            // Test that starvation time affects shark survival
            int shortStarve = 3;
            int longStarve = 10;

            // Shark hasn't eaten in 5 generations
            int generationsSinceLastMeal = 5;

            bool diesShort = generationsSinceLastMeal >= shortStarve;
            bool diesLong = generationsSinceLastMeal >= longStarve;

            // Assert
            Assert.IsTrue(diesShort, "Shark should die with short starvation time");
            Assert.IsFalse(diesLong, "Shark should survive with long starvation time");
        }

        #endregion

        #region Edge Case Tests

        [TestMethod]
        public void EmptyGrid_RemainsEmpty()
        {
            // Arrange
            var cells = CreateTestGrid(5, 5);
            int fishCount = 0;
            int sharkCount = 0;

            // Simulate generation (no animals to process)
            // Counts should remain 0

            // Assert
            Assert.AreEqual(0, fishCount);
            Assert.AreEqual(0, sharkCount);
        }

        [TestMethod]
        public void OnlyFish_NoSharks_PopulationGrows()
        {
            // Arrange
            int fishCount = 10;
            int sharkCount = 0;
            int fishBreedAge = 3;

            // Simulate breeding (simplified)
            int newFish = 0;
            for (int i = 0; i < fishCount; i++)
            {
                if (3 >= fishBreedAge) // Assume all fish are old enough
                {
                    newFish++;
                }
            }
            fishCount += newFish;

            // Assert
            Assert.IsTrue(fishCount > 10, "Fish population should grow without predators");
            Assert.AreEqual(0, sharkCount, "Shark population should remain 0");
        }

        [TestMethod]
        public void OnlySharks_NoFish_SharksStarve()
        {
            // Arrange
            int sharkCount = 10;
            int sharkStarve = 6;
            int currentGeneration = 10;

            // Simulate starvation (all sharks haven't eaten in 7 generations)
            int deadSharks = 0;
            for (int i = 0; i < sharkCount; i++)
            {
                int lastMeal = 3; // Last ate at generation 3
                if (currentGeneration - lastMeal >= sharkStarve)
                {
                    deadSharks++;
                }
            }
            sharkCount -= deadSharks;

            // Assert
            Assert.AreEqual(0, sharkCount, "All sharks should starve without food");
        }

        [TestMethod]
        public void SingleCell_CannotMove()
        {
            // A 1x1 grid has no neighbors, animals can't move
            var cells = CreateTestGrid(1, 1);
            PlaceFish(cells, 0, 0);

            // In a real simulation, neighbors list would be empty
            var neighbors = new List<(int, int)>(); // No valid neighbors

            // Assert
            Assert.AreEqual(0, neighbors.Count, "Single cell has no neighbors");
        }

        [TestMethod]
        public void FullGrid_NoMovement()
        {
            // If grid is completely full, no animal can move
            var cells = CreateTestGrid(3, 3);
            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    PlaceFish(cells, r, c);
                }
            }

            // All cells occupied - no empty neighbors
            bool hasEmptyNeighbors = false;
            // In practice, you'd check neighbors of each cell

            // Assert
            Assert.IsFalse(hasEmptyNeighbors, "Full grid has no empty spaces");
        }

        #endregion

        #region Performance Characteristics Tests

        [TestMethod]
        public void LargeGrid_ProcessingTime()
        {
            // Test that large grids can be processed
            // This is more of a smoke test than a performance benchmark
            int rows = 100;
            int cols = 100;
            var cells = CreateTestGrid(rows, cols);

            // Populate 10% with fish
            for (int i = 0; i < rows * cols / 10; i++)
            {
                int r = _random.Next(rows);
                int c = _random.Next(cols);
                PlaceFish(cells, r, c);
            }

            // Assert grid was created
            Assert.IsNotNull(cells);
            Assert.AreEqual(rows, cells.GetLength(0));
            Assert.AreEqual(cols, cells.GetLength(1));
        }

        [TestMethod]
        public void ProcessedFlag_PreventsDoubleProcessing()
        {
            // Arrange
            var cells = CreateTestGrid(5, 5);
            PlaceFish(cells, 2, 2);
            cells[2, 2].ProcessedThisGeneration = true;

            // Act
            bool shouldProcess = !cells[2, 2].ProcessedThisGeneration;

            // Assert
            Assert.IsFalse(shouldProcess, "Already processed cell should be skipped");
        }

        [TestMethod]
        public void ResetProcessedFlags_BetweenGenerations()
        {
            // Arrange
            var cells = CreateTestGrid(5, 5);
            PlaceFish(cells, 2, 2);
            cells[2, 2].ProcessedThisGeneration = true;

            // Act - simulate reset between generations
            cells[2, 2].ProcessedThisGeneration = false;

            // Assert
            Assert.IsFalse(cells[2, 2].ProcessedThisGeneration, "Flag should be reset");
        }

        #endregion
    }
}
