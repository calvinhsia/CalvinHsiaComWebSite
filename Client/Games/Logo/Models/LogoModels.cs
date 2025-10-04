using System.Text.Json.Serialization;

namespace WordScapeBlazorWasm.Models
{
    // Logo turtle graphics models for drawing and command processing
    public class LogoGameState
    {
        public LogoTurtle Turtle { get; set; } = new();
        public List<LogoCommand> CommandHistory { get; set; } = new();
        public List<LogoDrawingElement> DrawingElements { get; set; } = new();
        public string CurrentCode { get; set; } = "";
        public bool IsRunning { get; set; } = false;
        public string LastError { get; set; } = "";
        public LogoCanvas Canvas { get; set; } = new();
    }

    public class LogoTurtle
    {
        public double X { get; set; } = 250; // Start at center of 500x500 canvas
        public double Y { get; set; } = 250;
        public double Heading { get; set; } = 0; // Degrees, 0 is pointing up/north
        public bool PenDown { get; set; } = true;
        public string PenColor { get; set; } = "#000000"; // Black
        public double PenWidth { get; set; } = 1;
        public bool IsVisible { get; set; } = true;
    }

    public class LogoCanvas
    {
        public int Width { get; set; } = 500;
        public int Height { get; set; } = 500;
        public string BackgroundColor { get; set; } = "#FFFFFF"; // White
    }

    public class LogoCommand
    {
        public LogoCommandType Type { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
        public DateTime ExecutedAt { get; set; } = DateTime.Now;
        public string OriginalText { get; set; } = "";
    }

    public enum LogoCommandType
    {
        // Movement commands
        Forward,    // fd 100
        Backward,   // bk 100  
        Right,      // rt 90
        Left,       // lt 90
        
        // Pen commands
        PenUp,      // pu
        PenDown,    // pd
        
        // Drawing commands
        SetPenColor,  // setpencolor "red" or setpencolor [255 0 0]
        SetPenWidth,  // setpenwidth 5
        
        // Position commands
        SetXY,      // setxy 100 200
        SetX,       // setx 100
        SetY,       // sety 200
        SetHeading, // seth 90
        Home,       // home - go to center, heading 0
        
        // Canvas commands
        ClearScreen, // cs
        ShowTurtle,  // st
        HideTurtle,  // ht
        
        // Control structures
        Repeat,     // repeat 4 [fd 100 rt 90]
        
        // Utility
        Wait,       // wait 10 (tenth of a second)
        Comment     // ; This is a comment
    }

    public class LogoDrawingElement
    {
        public LogoDrawingType Type { get; set; }
        public double StartX { get; set; }
        public double StartY { get; set; }
        public double EndX { get; set; }
        public double EndY { get; set; }
        public string Color { get; set; } = "#000000";
        public double Width { get; set; } = 1;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public enum LogoDrawingType
    {
        Line,
        TurtlePosition
    }

    public class LogoProgram
    {
        public string Name { get; set; } = "";
        public string Code { get; set; } = "";
        public string Description { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public List<string> Tags { get; set; } = new();
    }

    // Predefined Logo programs for examples
    public static class LogoExamples
    {
        public static readonly List<LogoProgram> Programs = new()
        {
            new LogoProgram
            {
                Name = "Square",
                Code = @"; Draw a simple square
repeat 4 [
  fd 100
  rt 90
]",
                Description = "Draws a 100x100 square",
                Tags = new List<string> { "basic", "shapes", "repeat" }
            },
            
            new LogoProgram
            {
                Name = "Triangle",
                Code = @"; Draw an equilateral triangle
repeat 3 [
  fd 100
  rt 120
]",
                Description = "Draws an equilateral triangle",
                Tags = new List<string> { "basic", "shapes", "geometry" }
            },
            
            new LogoProgram
            {
                Name = "Spiral",
                Code = @"; Draw a colorful spiral
setpencolor ""red""
repeat 36 [
  fd 5
  rt 10
  fd 5
  rt 10
  fd 5
  rt 10
  fd 5
  rt 10
]",
                Description = "Creates a spiral pattern",
                Tags = new List<string> { "advanced", "patterns", "spiral" }
            },
            
            new LogoProgram
            {
                Name = "Flower",
                Code = @"; Draw a flower pattern
repeat 8 [
  repeat 4 [
    fd 50
    rt 90
  ]
  rt 45
]",
                Description = "Creates a flower-like pattern with 8 squares",
                Tags = new List<string> { "advanced", "patterns", "flowers" }
            },
            
            new LogoProgram
            {
                Name = "Star",
                Code = @"; Draw a five-pointed star
repeat 5 [
  fd 100
  rt 144
]",
                Description = "Draws a five-pointed star",
                Tags = new List<string> { "basic", "shapes", "star" }
            },
            
            new LogoProgram
            {
                Name = "House",
                Code = @"; Draw a simple house
; Draw the base
repeat 4 [
  fd 100
  rt 90
]

; Move to draw the roof
fd 100
rt 90
fd 100
lt 45

; Draw the roof
fd 70
rt 90
fd 70

; Go back to start
lt 135
fd 100
lt 90",
                Description = "Draws a simple house with a triangular roof",
                Tags = new List<string> { "intermediate", "house", "complex" }
            },
            
            new LogoProgram
            {
                Name = "Hexagon",
                Code = @"; Draw a hexagon
repeat 6 [
  fd 80
  rt 60
]",
                Description = "Draws a regular hexagon",
                Tags = new List<string> { "basic", "shapes", "geometry" }
            },
            
            new LogoProgram
            {
                Name = "Rainbow Squares",
                Code = @"; Draw rainbow colored squares
setpencolor ""red""
repeat 4 [ fd 50 rt 90 ]
rt 15

setpencolor ""orange""
repeat 4 [ fd 50 rt 90 ]
rt 15

setpencolor ""yellow""
repeat 4 [ fd 50 rt 90 ]
rt 15

setpencolor ""green""
repeat 4 [ fd 50 rt 90 ]
rt 15

setpencolor ""blue""
repeat 4 [ fd 50 rt 90 ]
rt 15

setpencolor ""purple""
repeat 4 [ fd 50 rt 90 ]",
                Description = "Draws multiple colored squares in a pattern",
                Tags = new List<string> { "intermediate", "colors", "patterns" }
            }
        };
    }
}