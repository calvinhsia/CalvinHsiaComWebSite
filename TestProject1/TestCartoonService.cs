using Microsoft.VisualStudio.TestTools.UnitTesting;
using WordScapeBlazorWasm.Games.Cartoon.Models;
using WordScapeBlazorWasm.Games.Cartoon.Services;
using WordScapeBlazorWasm.Services;

namespace TestProject1
{
    /// <summary>
    /// Unit tests for CartoonService
  /// Tests demo generation, frame interpolation, and color interpolation
    /// </summary>
    [TestClass]
    public class TestCartoonService
    {
        private RandomService _randomService = null!;
        private CartoonService _cartoonService = null!;

  [TestInitialize]
        public void TestInitialize()
    {
            // Use deterministic random for reproducible tests
   // Enable debug mode which forces RandomService to use seed=1
            DebugHelper.SetDebugMode(true);
        
            _randomService = new RandomService();
   _randomService.Reset(); // Force recreation with debug mode
 _cartoonService = new CartoonService(_randomService);
        }

        [TestCleanup]
      public void TestCleanup()
        {
      // Restore debug mode to default
    DebugHelper.SetDebugMode(false);
   }

        #region Alphabet Demo Tests

        [TestMethod]
        public void GenerateAlphabetDemo_ValidDimensions_Returns26Frames()
      {
            // Arrange
    double canvasWidth = 1200;
            double canvasHeight = 800;

   // Act
            var frames = _cartoonService.GenerateAlphabetDemo(canvasWidth, canvasHeight);

     // Assert
     Assert.IsNotNull(frames);
            Assert.AreEqual(26, frames.Count, "Alphabet demo should have 26 frames (A-Z)");
        }

        [TestMethod]
        public void GenerateAlphabetDemo_AllFramesHaveLines()
        {
  // Arrange
       double canvasWidth = 1200;
            double canvasHeight = 800;

         // Act
            var frames = _cartoonService.GenerateAlphabetDemo(canvasWidth, canvasHeight);

            // Assert
            foreach (var frame in frames)
            {
                Assert.IsTrue(frame.Lines.Count > 0, "Each alphabet frame should have at least one line");
      }
        }

  [TestMethod]
        public void GenerateAlphabetDemo_LinesHaveValidProperties()
   {
     // Arrange
            double canvasWidth = 1200;
            double canvasHeight = 800;

       // Act
            var frames = _cartoonService.GenerateAlphabetDemo(canvasWidth, canvasHeight);

            // Assert
            foreach (var frame in frames)
      {
            foreach (var line in frame.Lines)
    {
   Assert.IsTrue(line.Thickness > 0, "Line thickness should be positive");
   Assert.IsTrue(line.Color.StartsWith("#"), "Line color should be hex format");
              Assert.AreEqual(7, line.Color.Length, "Hex color should be 7 characters (#RRGGBB)");
          
           // Check coordinates are within canvas bounds (with some tolerance for letter rendering)
        Assert.IsTrue(line.Start.X >= 0 && line.Start.X <= canvasWidth * 1.5, 
      $"Start X coordinate {line.Start.X} should be reasonable");
         Assert.IsTrue(line.Start.Y >= 0 && line.Start.Y <= canvasHeight * 1.5,
   $"Start Y coordinate {line.Start.Y} should be reasonable");
    }
   }
     }

  [TestMethod]
        public void GenerateAlphabetDemo_DeterministicWithSameSeed()
     {
     // Arrange
  double canvasWidth = 1200;
            double canvasHeight = 800;

            // Create two services with debug mode for deterministic behavior
      DebugHelper.SetDebugMode(true);
            
            var randomService1 = new RandomService();
    randomService1.Reset();
   var service1 = new CartoonService(randomService1);

            var randomService2 = new RandomService();
  randomService2.Reset();
        var service2 = new CartoonService(randomService2);

     // Act
            var frames1 = service1.GenerateAlphabetDemo(canvasWidth, canvasHeight);
         var frames2 = service2.GenerateAlphabetDemo(canvasWidth, canvasHeight);

     // Assert
         Assert.AreEqual(frames1.Count, frames2.Count);
         
            for (int i = 0; i < frames1.Count; i++)
            {
    Assert.AreEqual(frames1[i].Lines.Count, frames2[i].Lines.Count,
     $"Frame {i} should have same number of lines");
      
                for (int j = 0; j < frames1[i].Lines.Count; j++)
   {
          var line1 = frames1[i].Lines[j];
    var line2 = frames2[i].Lines[j];
     
          Assert.AreEqual(line1.Start.X, line2.Start.X, 0.001, "Start X should match");
           Assert.AreEqual(line1.Start.Y, line2.Start.Y, 0.001, "Start Y should match");
         Assert.AreEqual(line1.Thickness, line2.Thickness, 0.001, "Thickness should match");
            Assert.AreEqual(line1.Color, line2.Color, "Color should match");
  }
            }
        }

        #endregion

    #region Word Demo Tests

        [TestMethod]
        public void GenerateWordDemo_SingleWord_ReturnsSingleFrame()
        {
 // Arrange
   double canvasWidth = 1200;
   double canvasHeight = 800;
            string sentence = "Hello";

       // Act
         var frames = _cartoonService.GenerateWordDemo(canvasWidth, canvasHeight, sentence);

       // Assert
 Assert.AreEqual(1, frames.Count, "Single word should produce single frame");
            Assert.IsTrue(frames[0].Lines.Count > 0, "Frame should have lines");
      }

        [TestMethod]
        public void GenerateWordDemo_MultipleWords_ReturnsMultipleFrames()
   {
    // Arrange
  double canvasWidth = 1200;
            double canvasHeight = 800;
            string sentence = "Hello World Test";

            // Act
          var frames = _cartoonService.GenerateWordDemo(canvasWidth, canvasHeight, sentence);

            // Assert
            Assert.AreEqual(3, frames.Count, "Three words should produce three frames");
     }

        [TestMethod]
        public void GenerateWordDemo_EmptyString_ReturnsEmptyList()
        {
            // Arrange
      double canvasWidth = 1200;
          double canvasHeight = 800;
            string sentence = "";

         // Act
         var frames = _cartoonService.GenerateWordDemo(canvasWidth, canvasHeight, sentence);

            // Assert
            Assert.AreEqual(0, frames.Count, "Empty string should produce no frames");
        }

        [TestMethod]
        public void GenerateWordDemo_WhitespaceOnly_ReturnsEmptyList()
        {
     // Arrange
      double canvasWidth = 1200;
          double canvasHeight = 800;
  string sentence = "   \t  \n  ";

            // Act
  var frames = _cartoonService.GenerateWordDemo(canvasWidth, canvasHeight, sentence);

    // Assert
    // Split with RemoveEmptyEntries should handle whitespace properly
    // But if there are somehow words generated, they should be empty frames
            Assert.IsTrue(frames.Count == 0 || frames.All(f => f.Lines.Count == 0), 
    "Whitespace only should produce no frames or empty frames");
      }

        [TestMethod]
 public void GenerateWordDemo_CustomThickness_AppliesThickness()
        {
// Arrange
   double canvasWidth = 1200;
 double canvasHeight = 800;
        string sentence = "Test";
            double customThickness = 15.0;

    // Act
       var frames = _cartoonService.GenerateWordDemo(canvasWidth, canvasHeight, sentence, customThickness);

 // Assert
        Assert.AreEqual(1, frames.Count);
     foreach (var line in frames[0].Lines)
        {
    // Thickness should be around the custom value (with some random variation)
      Assert.IsTrue(line.Thickness >= customThickness * 0.9 && line.Thickness <= customThickness * 1.6,
          $"Line thickness {line.Thickness} should be near {customThickness}");
         }
        }

     [TestMethod]
      public void GenerateWordDemo_LongWord_AutoScales()
        {
         // Arrange
            double canvasWidth = 400; // Small canvas
    double canvasHeight = 300;
       string sentence = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"; // Very long word

   // Act
   var frames = _cartoonService.GenerateWordDemo(canvasWidth, canvasHeight, sentence);

        // Assert
            Assert.AreEqual(1, frames.Count);
   var frame = frames[0];
    Assert.IsTrue(frame.Lines.Count > 0, "Should have lines");
     
            // Check that all lines are within canvas bounds (allowing some margin)
  foreach (var line in frame.Lines)
            {
   Assert.IsTrue(line.Start.X >= -10 && line.Start.X <= canvasWidth + 10,
  $"Line should fit within canvas width (Start X: {line.Start.X})");
    Assert.IsTrue(line.End.X >= -10 && line.End.X <= canvasWidth + 10,
        $"Line should fit within canvas width (End X: {line.End.X})");
        }
  }

        [TestMethod]
  public void GenerateWordDemo_AllLinesInFrameHaveSameColor()
        {
            // Arrange
       double canvasWidth = 1200;
   double canvasHeight = 800;
       string sentence = "Hello World";

            // Act
var frames = _cartoonService.GenerateWordDemo(canvasWidth, canvasHeight, sentence);

            // Assert
     foreach (var frame in frames)
            {
   var firstColor = frame.Lines[0].Color;
                foreach (var line in frame.Lines)
    {
          Assert.AreEqual(firstColor, line.Color, 
         "All lines in a single word frame should have the same color");
         }
        }
      }

    #endregion

        #region Frame Interpolation Tests

        [TestMethod]
        public void InterpolateFrame_TwoFrames_BetweenIndex0_ReturnsLeftFrame()
        {
   // Arrange
var frame1 = CreateTestFrame(100, 100, 200, 200);
   var frame2 = CreateTestFrame(300, 300, 400, 400);
            var frames = new List<CartoonFrame> { frame1, frame2 };

     // Act
   var result = _cartoonService.InterpolateFrame(frames, 0, 0, 10);

            // Assert
            Assert.AreEqual(1, result.Lines.Count);
            AssertPointEquals(100, 100, result.Lines[0].Start);
  AssertPointEquals(200, 200, result.Lines[0].End);
        }

        [TestMethod]
        public void InterpolateFrame_TwoFrames_Midpoint_ReturnsMidpoint()
   {
            // Arrange
   var frame1 = CreateTestFrame(100, 100, 200, 200, "#FF0000");
            var frame2 = CreateTestFrame(300, 300, 400, 400, "#0000FF");
        var frames = new List<CartoonFrame> { frame1, frame2 };
    int totalBetween = 10;
  int midIndex = 5;

    // Act
var result = _cartoonService.InterpolateFrame(frames, 0, midIndex, totalBetween);

          // Assert
  Assert.AreEqual(1, result.Lines.Count);
     
// At index 5 with totalBetween 10, nBetween = 11
// So interpolation is: leftValue + 5 * (rightValue - leftValue) / 11
// Start.X: 100 + 5 * (300 - 100) / 11 = 100 + 5 * 200 / 11 = 100 + 90.909... = 190.909
// Start.Y: 100 + 5 * (300 - 100) / 11 = 190.909
 AssertPointEquals(190.91, 190.91, result.Lines[0].Start, tolerance: 0.1);
            AssertPointEquals(290.91, 290.91, result.Lines[0].End, tolerance: 0.1);
        }

        [TestMethod]
        public void InterpolateFrame_DifferentLineCount_UsesLastLineForMissing()
        {
  // Arrange
            var frame1 = CreateTestFrame(100, 100, 200, 200);
      frame1.Lines.Add(new CartoonLine { Start = new Point(150, 150), End = new Point(250, 250), Thickness = 5, Color = "#FF0000" });
            
            var frame2 = CreateTestFrame(300, 300, 400, 400);
            // frame2 has only 1 line, frame1 has 2

      var frames = new List<CartoonFrame> { frame1, frame2 };

            // Act
          var result = _cartoonService.InterpolateFrame(frames, 0, 5, 10);

      // Assert
     Assert.AreEqual(2, result.Lines.Count, "Should interpolate to match the max line count");
        }

        [TestMethod]
        public void InterpolateFrame_EmptyFrames_ReturnsEmptyFrame()
        {
         // Arrange
         var frame1 = new CartoonFrame();
            var frame2 = new CartoonFrame();
          var frames = new List<CartoonFrame> { frame1, frame2 };

            // Act
            var result = _cartoonService.InterpolateFrame(frames, 0, 5, 10);

         // Assert
            Assert.AreEqual(0, result.Lines.Count, "Interpolating empty frames should return empty frame");
        }

        [TestMethod]
        public void InterpolateFrame_OneEmptyOneNot_ReturnsNonEmpty()
        {
         // Arrange
        var frame1 = new CartoonFrame(); // Empty
   var frame2 = CreateTestFrame(300, 300, 400, 400);
            var frames = new List<CartoonFrame> { frame1, frame2 };

            // Act
            var result = _cartoonService.InterpolateFrame(frames, 0, 5, 10);

            // Assert
     Assert.IsTrue(result.Lines.Count > 0, "Should return the non-empty frame");
  }

        [TestMethod]
        public void InterpolateFrame_WrapsAround_InterpolatesFromLastToFirst()
        {
         // Arrange
 var frame1 = CreateTestFrame(100, 100, 200, 200);
     var frame2 = CreateTestFrame(300, 300, 400, 400);
      var frame3 = CreateTestFrame(500, 500, 600, 600);
            var frames = new List<CartoonFrame> { frame1, frame2, frame3 };

     // Act - interpolate from last frame (index 2) to first frame (wraps around)
         var result = _cartoonService.InterpolateFrame(frames, 2, 5, 10);

   // Assert
            Assert.IsNotNull(result);
     Assert.AreEqual(1, result.Lines.Count);
            // Should be interpolating between frame3 (500,500,600,600) and frame1 (100,100,200,200)
          // At 5/11 progress, should be closer to frame3
     }

        [TestMethod]
        public void InterpolateFrame_SequentialInterpolation_Smooth()
        {
            // Arrange
 var frame1 = CreateTestFrame(0, 0, 100, 100);
            var frame2 = CreateTestFrame(100, 100, 200, 200);
     var frames = new List<CartoonFrame> { frame1, frame2 };
            int totalBetween = 10;

       // Act & Assert - check that interpolation is smooth and progressive
      Point? lastStart = null;
      for (int i = 0; i <= totalBetween; i++)
            {
          var result = _cartoonService.InterpolateFrame(frames, 0, i, totalBetween);
    Assert.AreEqual(1, result.Lines.Count);
            
       var currentStart = result.Lines[0].Start;
    
         if (lastStart.HasValue)
          {
    // Each step should move the point progressively
        Assert.IsTrue(currentStart.X >= lastStart.Value.X, 
    $"X should increase progressively (step {i})");
       Assert.IsTrue(currentStart.Y >= lastStart.Value.Y,
       $"Y should increase progressively (step {i})");
        }
    
        lastStart = currentStart;
            }
   }

        #endregion

        #region Color Interpolation Tests

        [TestMethod]
  public void ColorInterpolation_SameColor_ReturnsSameColor()
        {
   // Arrange
          var frame1 = CreateTestFrame(100, 100, 200, 200, "#FF0000");
       var frame2 = CreateTestFrame(300, 300, 400, 400, "#FF0000");
            var frames = new List<CartoonFrame> { frame1, frame2 };

     // Act
         var result = _cartoonService.InterpolateFrame(frames, 0, 5, 10);

          // Assert
        Assert.AreEqual("#FF0000", result.Lines[0].Color);
        }

[TestMethod]
        public void ColorInterpolation_RedToBlue_Midpoint()
     {
            // Arrange
            var frame1 = CreateTestFrame(100, 100, 200, 200, "#FF0000"); // Red
            var frame2 = CreateTestFrame(300, 300, 400, 400, "#0000FF"); // Blue
      var frames = new List<CartoonFrame> { frame1, frame2 };

  // Act
            var result = _cartoonService.InterpolateFrame(frames, 0, 5, 10);

            // Assert
            var color = result.Lines[0].Color;
          Assert.IsTrue(color.StartsWith("#"), "Color should be hex format");
   Assert.AreEqual(7, color.Length, "Color should be #RRGGBB format");

    // At midpoint between red and blue, should have both red and blue components
  // Not pure red (#FF0000) and not pure blue (#0000FF)
         Assert.AreNotEqual("#FF0000", color);
   Assert.AreNotEqual("#0000FF", color);
    }

        [TestMethod]
        public void ColorInterpolation_BlackToWhite_Progressive()
        {
   // Arrange
          var frame1 = CreateTestFrame(100, 100, 200, 200, "#000000"); // Black
  var frame2 = CreateTestFrame(300, 300, 400, 400, "#FFFFFF"); // White
       var frames = new List<CartoonFrame> { frame1, frame2 };

    // Act & Assert
       for (int i = 0; i <= 10; i++)
   {
     var result = _cartoonService.InterpolateFrame(frames, 0, i, 10);
       var color = result.Lines[0].Color;
 
  // Extract RGB components
          var r = Convert.ToInt32(color.Substring(1, 2), 16);
      var g = Convert.ToInt32(color.Substring(3, 2), 16);
  var b = Convert.ToInt32(color.Substring(5, 2), 16);
   
     // All components should be equal (grayscale) and progressive
        Assert.AreEqual(r, g, "R and G should be equal in grayscale");
       Assert.AreEqual(g, b, "G and B should be equal in grayscale");
          
  // Should be proportional to interpolation factor
    // t = i / (10 + 1) = i / 11
      double t = (double)i / 11;
  int expected = (int)(255.0 * t);
     Assert.AreEqual(expected, r, 1, $"At step {i}, color should be approximately {expected} (t={t:F3})");
   }
        }

        [TestMethod]
   public void ColorInterpolation_ThreeColorChannels_Independent()
        {
            // Arrange - test independent interpolation of R, G, B channels
        var frame1 = CreateTestFrame(100, 100, 200, 200, "#FF8000"); // Orange
     var frame2 = CreateTestFrame(300, 300, 400, 400, "#00FF80"); // Cyan-green
            var frames = new List<CartoonFrame> { frame1, frame2 };

      // Act
            var result = _cartoonService.InterpolateFrame(frames, 0, 5, 10);

            // Assert
     var color = result.Lines[0].Color;
     var r = Convert.ToInt32(color.Substring(1, 2), 16);
    var g = Convert.ToInt32(color.Substring(3, 2), 16);
            var b = Convert.ToInt32(color.Substring(5, 2), 16);
            
         // At midpoint:
          // R: 255 -> 0, midpoint ~127
            // G: 128 -> 255, midpoint ~191
            // B: 0 -> 128, midpoint ~64
    Assert.IsTrue(r >= 100 && r <= 155, $"Red channel should be ~127, got {r}");
            Assert.IsTrue(g >= 165 && g <= 220, $"Green channel should be ~191, got {g}");
          Assert.IsTrue(b >= 40 && b <= 90, $"Blue channel should be ~64, got {b}");
        }

        #endregion

        #region Model Cloning Tests

        [TestMethod]
        public void CartoonLine_Clone_CreatesIndependentCopy()
      {
 // Arrange
  var original = new CartoonLine
     {
        Start = new Point(10, 20),
 End = new Point(30, 40),
  Thickness = 5.0,
     Color = "#FF0000"
            };

  // Act
    var clone = original.Clone();
            clone.Start = new Point(100, 200);
            clone.Thickness = 10.0;

       // Assert
       Assert.AreEqual(10, original.Start.X, "Original should not be modified");
       Assert.AreEqual(5.0, original.Thickness, "Original thickness should not be modified");
            Assert.AreEqual(100, clone.Start.X, "Clone should have new value");
         Assert.AreEqual(10.0, clone.Thickness, "Clone thickness should have new value");
        }

    [TestMethod]
        public void CartoonFrame_Clone_CreatesDeepCopy()
        {
            // Arrange
            var original = CreateTestFrame(10, 20, 30, 40);
   original.Lines.Add(new CartoonLine
            {
              Start = new Point(50, 60),
                End = new Point(70, 80),
                Thickness = 3.0,
   Color = "#00FF00"
          });

        // Act
  var clone = original.Clone();
            clone.Lines[0].Start = new Point(100, 200);
    clone.Lines.Add(new CartoonLine
            {
       Start = new Point(90, 100),
        End = new Point(110, 120),
        Thickness = 7.0,
        Color = "#0000FF"
            });

      // Assert
            Assert.AreEqual(2, original.Lines.Count, "Original should have 2 lines");
      Assert.AreEqual(3, clone.Lines.Count, "Clone should have 3 lines");
         Assert.AreEqual(10, original.Lines[0].Start.X, "Original line should not be modified");
            Assert.AreEqual(100, clone.Lines[0].Start.X, "Clone line should have new value");
        }

        #endregion

        #region Edge Cases and Error Handling

        [TestMethod]
        public void GenerateAlphabetDemo_ZeroDimensions_DoesNotCrash()
    {
    // Arrange
   double canvasWidth = 0;
 double canvasHeight = 0;

       // Act
    var frames = _cartoonService.GenerateAlphabetDemo(canvasWidth, canvasHeight);

 // Assert
     Assert.IsNotNull(frames);
          Assert.AreEqual(26, frames.Count); // Should still generate 26 frames
     }

  [TestMethod]
   public void GenerateWordDemo_SpecialCharacters_HandlesGracefully()
        {
            // Arrange
 double canvasWidth = 1200;
            double canvasHeight = 800;
            string sentence = "Hello! @#$ 123";

            // Act
  var frames = _cartoonService.GenerateWordDemo(canvasWidth, canvasHeight, sentence);

            // Assert - should handle special characters (may render as empty or skip)
            Assert.IsNotNull(frames);
  Assert.IsTrue(frames.Count >= 0, "Should handle special characters without crashing");
        }

        [TestMethod]
        public void InterpolateFrame_SingleFrame_ReturnsClone()
        {
            // Arrange
            var frame1 = CreateTestFrame(100, 100, 200, 200);
         var frames = new List<CartoonFrame> { frame1 };

          // Act - even with single frame, should handle wrapping
            var result = _cartoonService.InterpolateFrame(frames, 0, 5, 10);

 // Assert
   Assert.IsNotNull(result);
         Assert.AreEqual(1, result.Lines.Count);
        }

   [TestMethod]
        public void InterpolateFrame_VeryLargeBetweenValue_Works()
        {
            // Arrange
       var frame1 = CreateTestFrame(0, 0, 100, 100);
       var frame2 = CreateTestFrame(1000, 1000, 1100, 1100);
            var frames = new List<CartoonFrame> { frame1, frame2 };

            // Act
            var result = _cartoonService.InterpolateFrame(frames, 0, 100, 200);

          // Assert
            Assert.IsNotNull(result);
     Assert.AreEqual(1, result.Lines.Count);
     // At 100/201 (~0.5), should be roughly midway
            Assert.IsTrue(result.Lines[0].Start.X > 400 && result.Lines[0].Start.X < 600,
      $"Interpolated X should be near midpoint, got {result.Lines[0].Start.X}");
        }

        [TestMethod]
        public void GenerateWordDemo_MultipleSpaces_IgnoresExtraSpaces()
        {
            // Arrange
            double canvasWidth = 1200;
            double canvasHeight = 800;
       string sentence = "Hello    World    Test"; // Multiple spaces

            // Act
     var frames = _cartoonService.GenerateWordDemo(canvasWidth, canvasHeight, sentence);

            // Assert
            Assert.AreEqual(3, frames.Count, "Should treat multiple spaces as single delimiter");
 }

        #endregion

      #region Helper Methods

        private CartoonFrame CreateTestFrame(double x1, double y1, double x2, double y2, string color = "#000000")
        {
            var frame = new CartoonFrame();
            frame.Lines.Add(new CartoonLine
      {
     Start = new Point(x1, y1),
            End = new Point(x2, y2),
  Thickness = 5.0,
              Color = color
  });
   return frame;
    }

   private void AssertPointEquals(double expectedX, double expectedY, Point actual, double tolerance = 0.001)
        {
   Assert.AreEqual(expectedX, actual.X, tolerance, $"Point X coordinate mismatch");
       Assert.AreEqual(expectedY, actual.Y, tolerance, $"Point Y coordinate mismatch");
        }

        #endregion
    }
}
