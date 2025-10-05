using System.Text.Json.Serialization;

namespace WordScapeBlazorWasm.Models
{
    // Logo turtle graphics models for drawing and command processing
    public class LogoGameState
    {
        [JsonPropertyName("turtle")]
        public LogoTurtle Turtle { get; set; } = new();
        
        [JsonPropertyName("commandHistory")]
        public List<LogoCommand> CommandHistory { get; set; } = new();
        
        [JsonPropertyName("drawingElements")]
        public List<LogoDrawingElement> DrawingElements { get; set; } = new();
        
        [JsonPropertyName("variables")]
        public Dictionary<string, double> Variables { get; set; } = new();
        
        [JsonPropertyName("currentCode")]
        public string CurrentCode { get; set; } = "";
        
        [JsonPropertyName("isRunning")]
        public bool IsRunning { get; set; } = false;
        
        [JsonPropertyName("lastError")]
        public string LastError { get; set; } = "";
        
        [JsonPropertyName("canvas")]
        public LogoCanvas Canvas { get; set; } = new();
    }

    public class LogoTurtle
    {
        [JsonPropertyName("x")]
        public double X { get; set; } = 250; // Start at center of 500x500 canvas
        
        [JsonPropertyName("y")]
        public double Y { get; set; } = 250;
        
        [JsonPropertyName("heading")]
        public double Heading { get; set; } = 0; // Degrees, 0 is pointing up/north
        
        [JsonPropertyName("penDown")]
        public bool PenDown { get; set; } = true;
        
        [JsonPropertyName("penColor")]
        public string PenColor { get; set; } = "#000000"; // Black
        
        [JsonPropertyName("penWidth")]
        public double PenWidth { get; set; } = 1;
        
        [JsonPropertyName("isVisible")]
        public bool IsVisible { get; set; } = true;
    }

    public class LogoCanvas
    {
        [JsonPropertyName("width")]
        public int Width { get; set; } = 500;
        
        [JsonPropertyName("height")]
        public int Height { get; set; } = 500;
        
        [JsonPropertyName("backgroundColor")]
        public string BackgroundColor { get; set; } = "#FFFFFF"; // White
    }

    public class LogoCommand
    {
        [JsonPropertyName("type")]
        public LogoCommandType Type { get; set; }
        
        [JsonPropertyName("parameters")]
        public Dictionary<string, object> Parameters { get; set; } = new();
        
        [JsonPropertyName("executedAt")]
        public DateTime ExecutedAt { get; set; } = DateTime.Now;
        
        [JsonPropertyName("originalText")]
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
        Comment,    // ; This is a comment

        // Variables and control structures
        SetVariable, // set variable value
        For            // for loop
    }

    public class LogoDrawingElement
    {
        [JsonPropertyName("type")]
        public LogoDrawingType Type { get; set; }
        
        [JsonPropertyName("startX")]
        public double StartX { get; set; }
        
        [JsonPropertyName("startY")]
        public double StartY { get; set; }
        
        [JsonPropertyName("endX")]
        public double EndX { get; set; }
        
        [JsonPropertyName("endY")]
        public double EndY { get; set; }
        
        [JsonPropertyName("color")]
        public string Color { get; set; } = "#000000";
        
        [JsonPropertyName("width")]
        public double Width { get; set; } = 1;
        
        [JsonPropertyName("createdAt")]
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
}