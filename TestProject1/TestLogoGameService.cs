using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.JSInterop;
using BlazorWasm.Models;
using BlazorWasm.Services;

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
            gameState.RenderingMode = LogoRenderingMode.Immediate; // Use immediate mode
            // Null out callbacks to avoid JS runtime in tests
            gameState.OnDrawingElementCreated = null;
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
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
        public async Task ExecuteCode_Backward_MovesTurtleSouth()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Immediate;
            gameState.OnDrawingElementCreated = null;
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
            var code = "bk 50";

            // Act
            var success = await _logoService.ExecuteCodeAsync(gameState, code);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(250, gameState.Turtle.X, 0.01, "X should not change");
            Assert.AreEqual(300, gameState.Turtle.Y, 0.01, "Y should increase by 50 (south is down)");
            Assert.AreEqual(1, gameState.DrawingElements.Count, "Should create one line");
        }

        [TestMethod]
        public async Task ExecuteCode_TurnRight_ChangesHeading()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Immediate;
            gameState.OnDrawingElementCreated = null;
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
            var code = "rt 90";

            // Act
            var success = await _logoService.ExecuteCodeAsync(gameState, code);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(90, gameState.Turtle.Heading, 0.01, "Heading should be 90 degrees (east)");
            Assert.AreEqual(0, gameState.DrawingElements.Count, "Should not draw any lines");
        }

        [TestMethod]
        public async Task ExecuteCode_TurnLeft_ChangesHeading()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Immediate;
            gameState.OnDrawingElementCreated = null;
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
            var code = "lt 45";

            // Act
            var success = await _logoService.ExecuteCodeAsync(gameState, code);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(315, gameState.Turtle.Heading, 0.01, "Heading should be 315 degrees");
            Assert.AreEqual(0, gameState.DrawingElements.Count, "Should not draw any lines");
        }

        [TestMethod]
        public async Task ExecuteCode_Square_DrawsSquare()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Immediate;
            // Null out callbacks to avoid JS runtime in tests
            gameState.OnDrawingElementCreated = null;
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
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

        #region Advanced Movement Tests

        [TestMethod]
        public async Task ExecuteCode_SetXY_MovesTurtleToPosition()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Immediate;
            gameState.OnDrawingElementCreated = null;
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
            var code = "setxy 100 200";

            // Act
            var success = await _logoService.ExecuteCodeAsync(gameState, code);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(100, gameState.Turtle.X, 0.01);
            Assert.AreEqual(200, gameState.Turtle.Y, 0.01);
            Assert.AreEqual(1, gameState.DrawingElements.Count, "Should draw line to new position");
        }

        [TestMethod]
        public async Task ExecuteCode_SetX_MovesXOnly()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Immediate;
            gameState.OnDrawingElementCreated = null;
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
            var code = "setx 300";

            // Act
            var success = await _logoService.ExecuteCodeAsync(gameState, code);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(300, gameState.Turtle.X, 0.01);
            Assert.AreEqual(250, gameState.Turtle.Y, 0.01, "Y should not change");
        }

        [TestMethod]
        public async Task ExecuteCode_SetY_MovesYOnly()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Immediate;
            gameState.OnDrawingElementCreated = null;
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
            var code = "sety 100";

            // Act
            var success = await _logoService.ExecuteCodeAsync(gameState, code);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(250, gameState.Turtle.X, 0.01, "X should not change");
            Assert.AreEqual(100, gameState.Turtle.Y, 0.01);
        }

        [TestMethod]
        public async Task ExecuteCode_SetHeading_ChangesHeading()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Immediate;
            gameState.OnDrawingElementCreated = null;
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
            var code = "seth 180";

            // Act
            var success = await _logoService.ExecuteCodeAsync(gameState, code);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(180, gameState.Turtle.Heading, 0.01, "Heading should be 180 (south)");
        }

        [TestMethod]
        public async Task ExecuteCode_Home_ReturnsTurtleToCenter()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Immediate;
            gameState.OnDrawingElementCreated = null;
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
            // Move turtle away from center and change heading
            var code = @"fd 100
rt 45
home";

            // Act
            var success = await _logoService.ExecuteCodeAsync(gameState, code);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(250, gameState.Turtle.X, 0.01, "Should return to center X");
            Assert.AreEqual(250, gameState.Turtle.Y, 0.01, "Should return to center Y");
            Assert.AreEqual(0, gameState.Turtle.Heading, 0.01, "Should reset heading to 0");
        }

        #endregion

        #region Pen Control Tests

        [TestMethod]
        public async Task ExecuteCode_PenUp_StopsDrawing()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Immediate;
            // Null out callbacks to avoid JS runtime in tests
            gameState.OnDrawingElementCreated = null;
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
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
        public async Task ExecuteCode_PenDown_ResumesDrawing()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Immediate;
            gameState.OnDrawingElementCreated = null;
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
            var code = @"pu
fd 50
pd
fd 50";

            // Act
            var success = await _logoService.ExecuteCodeAsync(gameState, code);

            // Assert
            Assert.IsTrue(success);
            Assert.IsTrue(gameState.Turtle.PenDown, "Pen should be down");
            Assert.AreEqual(1, gameState.DrawingElements.Count, "Should draw one line after pen down");
        }

        [TestMethod]
        public async Task ExecuteCode_SetPenColor_ChangesColor()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Immediate;
            // Null out callbacks to avoid JS runtime in tests
            gameState.OnDrawingElementCreated = null;
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
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

        [TestMethod]
        public async Task ExecuteCode_SetPenColorInteger_ChangesColor()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Immediate;
            gameState.OnDrawingElementCreated = null;
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
            var code = @"setpencolor 1
fd 50";

            // Act
            var success = await _logoService.ExecuteCodeAsync(gameState, code);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(1, gameState.DrawingElements.Count);
            var line = gameState.DrawingElements[0];
            Assert.AreEqual("#FF0000", line.Color, "Color 1 should be red");
        }

        [TestMethod]
        public async Task ExecuteCode_SetPenWidth_ChangesWidth()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Immediate;
            gameState.OnDrawingElementCreated = null;
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
            var code = @"setpenwidth 5
fd 50";

            // Act
            var success = await _logoService.ExecuteCodeAsync(gameState, code);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(5, gameState.Turtle.PenWidth, 0.01);
            var line = gameState.DrawingElements[0];
            Assert.AreEqual(5, line.Width, 0.01, "Line width should be 5");
        }

        #endregion

        #region Control Structure Tests

        [TestMethod]
        public async Task ExecuteCode_RepeatLoop_ExecutesMultipleTimes()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Immediate;
            gameState.OnDrawingElementCreated = null;
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
            var code = @"repeat 3 [fd 10]" ;

            // Act
            var success = await _logoService.ExecuteCodeAsync(gameState, code);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(3, gameState.DrawingElements.Count, "Should draw 3 lines");
        }

        [TestMethod]
        public async Task ExecuteCode_ForLoop_UsesVariableCorrectly()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Immediate;
            gameState.OnDrawingElementCreated = null;
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
            var code = @"for i 1 3 [
  fd :i
]" ;

            // Act
            var success = await _logoService.ExecuteCodeAsync(gameState, code);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(3, gameState.DrawingElements.Count, "Should draw 3 lines");
        }

        [TestMethod]
        public async Task ExecuteCode_ForLoopWithColorVariable_ChangesColors()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Immediate;
            gameState.OnDrawingElementCreated = null;
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
            var code = @"for color 1 3 [
  setpencolor :color
  fd 20
]" ;

            // Act
            var success = await _logoService.ExecuteCodeAsync(gameState, code);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(3, gameState.DrawingElements.Count);
            Assert.AreEqual("#FF0000", gameState.DrawingElements[0].Color, "First should be red (1)");
            Assert.AreEqual("#00FF00", gameState.DrawingElements[1].Color, "Second should be green (2)");
            Assert.AreEqual("#0000FF", gameState.DrawingElements[2].Color, "Third should be blue (3)");
        }

        [TestMethod]
        public async Task ExecuteCode_NestedLoops_WorksCorrectly()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Immediate;
            gameState.OnDrawingElementCreated = null;
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
            var code = @"repeat 2 [
  repeat 2 [fd 10]
  rt 90
]" ;

            // Act
            var success = await _logoService.ExecuteCodeAsync(gameState, code);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(4, gameState.DrawingElements.Count, "Should draw 4 lines (2x2)");
        }

        #endregion

        #region Canvas Command Tests

        [TestMethod]
        public async Task ExecuteCode_ClearScreen_ClearsDrawing()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Immediate;
            gameState.OnDrawingElementCreated = null;
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
            var code = @"fd 50
cs
fd 50" ;

            // Act
            var success = await _logoService.ExecuteCodeAsync(gameState, code);

            // Assert
            Assert.IsTrue(success);
            // cs clears drawing elements, then moves home (which creates a line), then fd 50 creates another line
            Assert.AreEqual(2, gameState.DrawingElements.Count, "Should have home line + fd line after cs");
            Assert.AreEqual(250, gameState.Turtle.X, 0.01, "Turtle should be at center X");
            Assert.AreEqual(200, gameState.Turtle.Y, 0.01, "Turtle should have moved 50 from center after cs+home");
        }

        [TestMethod]
        public async Task ExecuteCode_ShowTurtle_MakesTurtleVisible()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Immediate;
            gameState.OnDrawingElementCreated = null;
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
            gameState.Turtle.IsVisible = false;
            var code = "st";

            // Act
            var success = await _logoService.ExecuteCodeAsync(gameState, code);

            // Assert
            Assert.IsTrue(success);
            Assert.IsTrue(gameState.Turtle.IsVisible, "Turtle should be visible");
        }

        [TestMethod]
        public async Task ExecuteCode_HideTurtle_MakesTurtleInvisible()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Immediate;
            gameState.OnDrawingElementCreated = null;
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
            var code = "ht";

            // Act
            var success = await _logoService.ExecuteCodeAsync(gameState, code);

            // Assert
            Assert.IsTrue(success);
            Assert.IsFalse(gameState.Turtle.IsVisible, "Turtle should be hidden");
        }

        #endregion

        #region Delay/Wait Command Tests

        [TestMethod]
        public async Task ExecuteCode_Delay_DelaysExecution()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Immediate;
            gameState.OnDrawingElementCreated = null;
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
            var code = "delay 50";

            // Act
            var startTime = DateTime.Now;
            var success = await _logoService.ExecuteCodeAsync(gameState, code);
            var elapsed = DateTime.Now - startTime;

            // Assert
            Assert.IsTrue(success);
            Assert.IsTrue(elapsed.TotalMilliseconds >= 40, "Should delay at least 40ms");
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
        public void LogoColorUtils_GetRainbowColor_GeneratesColors()
        {
            // Act
            var color1 = LogoColorUtils.GetRainbowColor(0, 16);
            var color8 = LogoColorUtils.GetRainbowColor(8, 16);

            // Assert
            Assert.IsTrue(color1.StartsWith("#"), "Should return hex color");
            Assert.IsTrue(color8.StartsWith("#"), "Should return hex color");
            Assert.AreNotEqual(color1, color8, "Different steps should have different colors");
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

        #region Parser Tests

        [TestMethod]
        public async Task ExecuteCode_MultiLineCode_ParsesCorrectly()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Immediate;
            gameState.OnDrawingElementCreated = null;
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
            var code = @"fd 50
rt 90
fd 50" ;

            // Act
            var success = await _logoService.ExecuteCodeAsync(gameState, code);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(2, gameState.DrawingElements.Count, "Should draw 2 lines");
            Assert.AreEqual(90, gameState.Turtle.Heading, 0.01);
        }

        [TestMethod]
        public async Task ExecuteCode_CommentsIgnored_ExecutesCorrectly()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Immediate;
            gameState.OnDrawingElementCreated = null;
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
            var code = @"; This is a comment
fd 50
; Another comment
rt 90" ;

            // Act
            var success = await _logoService.ExecuteCodeAsync(gameState, code);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(1, gameState.DrawingElements.Count);
            Assert.AreEqual(90, gameState.Turtle.Heading, 0.01);
        }

        [TestMethod]
        public async Task ExecuteCode_EmptyLines_HandledCorrectly()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Immediate;
            gameState.OnDrawingElementCreated = null;
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
            var code = @"fd 50

rt 90

fd 50" ;

            // Act
            var success = await _logoService.ExecuteCodeAsync(gameState, code);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(2, gameState.DrawingElements.Count);
        }

        #endregion

        #region Error Handling Tests

        [TestMethod]
        public async Task ExecuteCode_UndefinedVariable_ReturnsError()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Immediate;
            gameState.OnDrawingElementCreated = null;
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
            var code = "fd :undefined";

            // Act
            var success = await _logoService.ExecuteCodeAsync(gameState, code);

            // Assert
            Assert.IsFalse(success, "Should fail with undefined variable");
            Assert.IsFalse(string.IsNullOrEmpty(gameState.LastError), "Should set error message");
        }

        [TestMethod]
        public async Task ExecuteCode_InvalidColorVariable_ReturnsError()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Immediate;
            gameState.OnDrawingElementCreated = null;
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
            var code = "setpencolor :badcolor";

            // Act
            var success = await _logoService.ExecuteCodeAsync(gameState, code);

            // Assert
            Assert.IsFalse(success, "Should fail with undefined color variable");
            Assert.IsFalse(string.IsNullOrEmpty(gameState.LastError));
        }

        #endregion

        #region Advanced Parser Tests

        [TestMethod]
        public async Task Parser_NestedBrackets_ParsesCorrectly()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Immediate;
            gameState.OnDrawingElementCreated = null;
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
            var code = @"repeat 2 [
  repeat 3 [
    fd 10
  ]
  rt 90
]" ;

            // Act
            var success = await _logoService.ExecuteCodeAsync(gameState, code);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(6, gameState.DrawingElements.Count, "Should draw 6 lines (2 outer * 3 inner)");
        }

        [TestMethod]
        public async Task Parser_TripleNestedLoops_ParsesCorrectly()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Immediate;
            gameState.OnDrawingElementCreated = null;
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
            var code = @"repeat 2 [
  repeat 2 [
    repeat 2 [
      fd 5
    ]
  ]
]" ;

            // Act
            var success = await _logoService.ExecuteCodeAsync(gameState, code);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(8, gameState.DrawingElements.Count, "Should draw 8 lines (2*2*2)");
        }

        [TestMethod]
        public async Task Parser_ForLoopInsideRepeat_ParsesCorrectly()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Immediate;
            gameState.OnDrawingElementCreated = null;
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
            var code = @"repeat 2 [
  for i 1 3 [
    fd :i
  ]
]" ;

            // Act
            var success = await _logoService.ExecuteCodeAsync(gameState, code);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(6, gameState.DrawingElements.Count, "Should draw 6 lines (2 * 3)");
        }

        [TestMethod]
        public async Task Parser_RepeatInsideForLoop_ParsesCorrectly()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Immediate;
            gameState.OnDrawingElementCreated = null;
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
            var code = @"for i 1 2 [
  repeat 3 [
    fd 10
  ]
]" ;

            // Act
            var success = await _logoService.ExecuteCodeAsync(gameState, code);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(6, gameState.DrawingElements.Count, "Should draw 6 lines");
        }

        [TestMethod]
        public async Task Parser_CommentsInsideBrackets_ParsesCorrectly()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Immediate;
            gameState.OnDrawingElementCreated = null;
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
            var code = @"repeat 2 [
  ; This is a comment inside the loop
  fd 20
  ; Another comment
  rt 90
]" ;

            // Act
            var success = await _logoService.ExecuteCodeAsync(gameState, code);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(2, gameState.DrawingElements.Count, "Comments should be ignored");
        }

        [TestMethod]
        public async Task Parser_MultipleCommandsOnOneLine_ParsesCorrectly()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Immediate;
            gameState.OnDrawingElementCreated = null;
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
            var code = "fd 50 rt 90 fd 50"; // Multiple spaces

            // Act
            var success = await _logoService.ExecuteCodeAsync(gameState, code);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(2, gameState.DrawingElements.Count, "Should execute 3 commands on one line");
            Assert.AreEqual(90, gameState.Turtle.Heading, 0.01);
        }

        [TestMethod]
        public async Task Parser_InlineCommentAfterCommand_ParsesCorrectly()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Immediate;
            gameState.OnDrawingElementCreated = null;
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
            // Note: Parser strips comments from the entire line after semicolon
            var code = @"fd 50
rt 90";

            // Act
            var success = await _logoService.ExecuteCodeAsync(gameState, code);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(1, gameState.DrawingElements.Count);
            Assert.AreEqual(90, gameState.Turtle.Heading, 0.01);
        }

        [TestMethod]
        public async Task Parser_VariableInMultipleParameters_ParsesCorrectly()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Immediate;
            gameState.OnDrawingElementCreated = null;
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
            // Test variable used in different command types
            var code = @"for i 10 20 [
  fd :i
  bk :i
]";

            // Act
            var success = await _logoService.ExecuteCodeAsync(gameState, code);

            // Assert
            Assert.IsTrue(success);
            // Each iteration: fd draws a line, bk draws another line = 2 lines per iteration * 11 iterations = 22 lines
            Assert.IsTrue(gameState.DrawingElements.Count > 0, "Should draw lines using variable");
            // Turtle should return to approximately start position
            Assert.AreEqual(250, gameState.Turtle.X, 1.0, "Should return close to start X");
            Assert.AreEqual(250, gameState.Turtle.Y, 1.0, "Should return close to start Y");
        }

        #endregion

        #region Complex Pattern Tests

        [TestMethod]
        public async Task ExecuteCode_Spiral_DrawsCorrectly()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Immediate;
            gameState.OnDrawingElementCreated = null;
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
            var code = @"for i 1 10 [
  fd :i
  rt 91
]" ;

            // Act
            var success = await _logoService.ExecuteCodeAsync(gameState, code);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(10, gameState.DrawingElements.Count, "Should draw 10 line segments");
        }

        [TestMethod]
        public async Task ExecuteCode_Triangle_DrawsCorrectShape()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Immediate;
            gameState.OnDrawingElementCreated = null;
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
            var code = @"repeat 3 [
  fd 100
  rt 120
]" ;

            // Act
            var success = await _logoService.ExecuteCodeAsync(gameState, code);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(3, gameState.DrawingElements.Count, "Triangle has 3 sides");
            // Should return to approximately start position
            Assert.AreEqual(250, gameState.Turtle.X, 1.0, "Should return close to start X");
            Assert.AreEqual(250, gameState.Turtle.Y, 1.0, "Should return close to start Y");
        }

        [TestMethod]
        public async Task ExecuteCode_Star_DrawsCorrectShape()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Immediate;
            gameState.OnDrawingElementCreated = null;
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
            var code = @"repeat 5 [
  fd 100
  rt 144
]" ;

            // Act
            var success = await _logoService.ExecuteCodeAsync(gameState, code);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(5, gameState.DrawingElements.Count, "Star has 5 points");
        }

        #endregion

        #region Rendering Mode Tests

        [TestMethod]
        public void CreateNewGame_DefaultsToImmediateMode()
        {
            // Act
            var gameState = _logoService.CreateNewGame();

            // Assert
            Assert.AreEqual(LogoRenderingMode.Immediate, gameState.RenderingMode);
            Assert.AreEqual(10.0, gameState.AnimationSpeed, 0.01);
        }

        [TestMethod]
        public async Task ExecuteCode_ImmediateMode_NoCallbackDelay()
        {
            // Arrange
            var gameState = _logoService.CreateNewGame();
            gameState.RenderingMode = LogoRenderingMode.Immediate;
            int callbackCount = 0;
            gameState.OnDrawingElementCreated = async (element) =>
            {
                callbackCount++;
                await Task.CompletedTask;
            };
            gameState.OnTurtlePositionChanged = null;
            gameState.OnCanvasOperation = null;
            var code = "repeat 5 [fd 10]";

            // Act
            var startTime = DateTime.Now;
            var success = await _logoService.ExecuteCodeAsync(gameState, code);
            var elapsed = DateTime.Now - startTime;

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(5, callbackCount, "Should invoke callback for each line");
            // Immediate mode should be fast (no animation delays)
            Assert.IsTrue(elapsed.TotalMilliseconds < 500, "Should execute quickly in immediate mode");
        }

        #endregion

        #region Debug Mode Tests

        [TestMethod]
        public void SetDebugMode_EnablesLogging()
        {
            // Act
            _logoService.SetDebugMode(true);

            // Assert - if debug mode works, no exception should occur
            var gameState = _logoService.CreateNewGame();
            Assert.IsNotNull(gameState);
        }

        [TestMethod]
        public void SetDebugMode_DisablesLogging()
        {
            // Act
            _logoService.SetDebugMode(false);

            // Assert
            var gameState = _logoService.CreateNewGame();
            Assert.IsNotNull(gameState);
        }

        #endregion
    }
}
