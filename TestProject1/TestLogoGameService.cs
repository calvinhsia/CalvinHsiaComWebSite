using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.JSInterop;
using WordScapeBlazorWasm.Models;
using WordScapeBlazorWasm.Services;

namespace TestProject1
{
    /// <summary>
    /// Unit tests for LogoGameService
    /// Tests turtle movement, pen control, drawing commands, and control structures
    /// Note: Tests focus on C# logic - JavaScript rendering is tested separately
    /// </summary>
    [TestClass]
    public class TestLogoGameService
    {
        private LogoGameService _logoService = null!;
        private RandomService _randomService = null!;

        // Simple JSRuntime stub for testing
        private class TestJSRuntime : IJSRuntime
        {
            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            {
                return new ValueTask<TValue>(default(TValue)!);
            }

            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            {
                return new ValueTask<TValue>(default(TValue)!);
            }
        }

        [TestInitialize]
        public void TestInitialize()
        {
            // Enable debug mode for deterministic behavior
            DebugHelper.SetDebugMode(true);
            _randomService = new RandomService();
            _randomService.Reset();

            // Create Logo service with test JSRuntime
            _logoService = new LogoGameService(new TestJSRuntime());
        }

        [TestCleanup]
        public void TestCleanup()
        {
            DebugHelper.SetDebugMode(false);
        }

        #region Game State Creation Tests

        [TestMethod]
        public void CreateNewGame_InitializesTurtleAtCenter()
        {
            // Act
            var gameState = _logoService.CreateNewGame();

            // Assert
            Assert.IsNotNull(gameState);
            Assert.AreEqual(250, gameState.Turtle.X, "Turtle should start at X=250");
            Assert.AreEqual(250, gameState.Turtle.Y, "Turtle should start at Y=250");
            Assert.AreEqual(0, gameState.Turtle.Heading, "Turtle should face north (0 degrees)");
            Assert.IsTrue(gameState.Turtle.PenDown, "Pen should be down by default");
            Assert.IsTrue(gameState.Turtle.IsVisible, "Turtle should be visible by default");
        }

        [TestMethod]
        public void CreateNewGame_InitializesCanvasProperties()
        {
            // Act
            var gameState = _logoService.CreateNewGame();

            // Assert
            Assert.AreEqual(500, gameState.Canvas.Width);
            Assert.AreEqual(500, gameState.Canvas.Height);
            Assert.AreEqual("#FFFFFF", gameState.Canvas.BackgroundColor);
        }

        [TestMethod]
        public void CreateNewGame_InitializesEmptyCollections()
        {
            // Act
            var gameState = _logoService.CreateNewGame();

            // Assert
            Assert.AreEqual(0, gameState.DrawingElements.Count);
            Assert.AreEqual(0, gameState.CommandHistory.Count);
            Assert.AreEqual(0, gameState.Variables.Count);
            Assert.IsFalse(gameState.IsRunning);
        }

        #endregion

        #region Basic Movement Tests

        [TestMethod]
        public async Task ExecuteCode_Forward_MovesTurtleNorth()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Batch; // Use batch mode to avoid JS callbacks
            var code = "fd 100";

            // Act
            var success = await _logoService.ExecuteCodeAsync(gameState, code);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(250, gameState.Turtle.X, 0.01, "X should not change");
            Assert.AreEqual(150, gameState.Turtle.Y, 0.01, "Y should decrease by 100 (north is up)");
            Assert.AreEqual(1, gameState.DrawingElements.Count, "Should create one line");
        }

        [TestMethod]
        public async Task ExecuteCode_Square_DrawsSquare()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Batch;
            var code = @"repeat 4 [
  fd 100
  rt 90
]";

            // Act
            var success = await _logoService.ExecuteCodeAsync(gameState, code);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(4, gameState.DrawingElements.Count, "Should draw 4 lines");
            // Turtle should return to start
            Assert.AreEqual(250, gameState.Turtle.X, 0.01, "Should return to start X");
            Assert.AreEqual(250, gameState.Turtle.Y, 0.01, "Should return to start Y");
            Assert.AreEqual(0, gameState.Turtle.Heading, 0.01, "Should return to start heading");
        }

        #endregion

        #region Pen Control Tests

        [TestMethod]
        public async Task ExecuteCode_PenUp_StopsDrawing()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Batch;
            var code = @"pu
fd 100";

            // Act
            var success = await _logoService.ExecuteCodeAsync(gameState, code);

            // Assert
            Assert.IsTrue(success);
            Assert.IsFalse(gameState.Turtle.PenDown, "Pen should be up");
            Assert.AreEqual(0, gameState.DrawingElements.Count, "Should not draw when pen is up");
        }

        [TestMethod]
        public async Task ExecuteCode_SetPenColor_ChangesColor()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Batch;
            var code = @"setpencolor ""red""
fd 50";

            // Act
            var success = await _logoService.ExecuteCodeAsync(gameState, code);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(1, gameState.DrawingElements.Count);
            var line = gameState.DrawingElements[0];
            Assert.AreEqual("#FF0000", line.Color, "Line should be red");
        }

        #endregion

        #region Control Structure Tests

        [TestMethod]
        public async Task ExecuteCode_ForLoop_UsesVariableCorrectly()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Batch;
            var code = @"for i 1 3 [
  fd :i
]";

            // Act
            var success = await _logoService.ExecuteCodeAsync(gameState, code);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(3, gameState.DrawingElements.Count, "Should draw 3 lines");
        }

        #endregion

        #region Color Utility Tests

        [TestMethod]
        public void LogoColorUtils_IntColorToHex_ConvertsCorrectly()
        {
            // Test primary colors with consecutive integers
            Assert.AreEqual("#000000", LogoColorUtils.IntColorToHex(0), "0 should be black");
            Assert.AreEqual("#FF0000", LogoColorUtils.IntColorToHex(1), "1 should be red");
            Assert.AreEqual("#00FF00", LogoColorUtils.IntColorToHex(2), "2 should be green");
            Assert.AreEqual("#0000FF", LogoColorUtils.IntColorToHex(3), "3 should be blue");
            Assert.AreEqual("#FFFF00", LogoColorUtils.IntColorToHex(4), "4 should be yellow");
            Assert.AreEqual("#FFFFFF", LogoColorUtils.IntColorToHex(7), "7 should be white");
        }

        [TestMethod]
        public void LogoColorUtils_GetColorInt_ConvertNameCorrectly()
        {
            // Test color name to integer conversion
            Assert.AreEqual(0, LogoColorUtils.GetColorInt("black"));
            Assert.AreEqual(1, LogoColorUtils.GetColorInt("red"));
            Assert.AreEqual(2, LogoColorUtils.GetColorInt("green"));
            Assert.AreEqual(3, LogoColorUtils.GetColorInt("blue"));
            Assert.AreEqual(7, LogoColorUtils.GetColorInt("white"));
        }

        [TestMethod]
        public void GetCurrentPosition_ReturnsFormattedString()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.Turtle.X = 150;
            gameState.Turtle.Y = 200;
            gameState.Turtle.Heading = 90;

            // Act
            var position = _logoService.GetCurrentPosition(gameState);

            // Assert
            Assert.IsNotNull(position);
            Assert.IsTrue(position.Contains("150"), "Should contain X coordinate");
            Assert.IsTrue(position.Contains("200"), "Should contain Y coordinate");
            Assert.IsTrue(position.Contains("90"), "Should contain heading");
        }

        #endregion
    }
}
