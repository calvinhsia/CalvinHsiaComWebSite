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

        // NEW: Rendering mode configuration
        [JsonPropertyName("renderingMode")]
        public LogoRenderingMode RenderingMode { get; set; } = LogoRenderingMode.Immediate;

        // NEW: Callback for immediate rendering - not serialized
        [JsonIgnore]
        public Action<LogoDrawingElement>? OnDrawingElementCreated { get; set; }
        
        // NEW: Callback for turtle position updates - not serialized
        [JsonIgnore]
        public Action<LogoTurtle>? OnTurtlePositionChanged { get; set; }
        
        // NEW: Callback for canvas updates (clear, etc.) - not serialized
        [JsonIgnore]
        public Action<LogoCanvasOperation>? OnCanvasOperation { get; set; }

        // NEW: Animation speed for animated mode (commands per second)
        [JsonPropertyName("animationSpeed")]
        public double AnimationSpeed { get; set; } = 10.0;
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

        // NEW: Clone method for position tracking
        public LogoTurtle Clone()
        {
            return new LogoTurtle
            {
                X = this.X,
                Y = this.Y,
                Heading = this.Heading,
                PenDown = this.PenDown,
                PenColor = this.PenColor,
                PenWidth = this.PenWidth,
                IsVisible = this.IsVisible
            };
        }
    }

    public class LogoCanvas
    {
        [JsonPropertyName("width")]
        public int Width { get; set; } = 500;  // Default, will be updated by resize
        
        [JsonPropertyName("height")]
        public int Height { get; set; } = 500;  // Default, will be updated by resize
        
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

        // NEW: Execution delay for animation timing
        [JsonPropertyName("executionDelay")]
        public int ExecutionDelayMs { get; set; } = 0;
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
        SetPenColor,  // setpencolor "red" or setpencolor [255 0 0] or setpencolor :colorvar
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
        Delay,      // delay 1000 (delay N milliseconds)
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

        // NEW: Unique identifier for tracking
        [JsonPropertyName("id")]
        public Guid Id { get; set; } = Guid.NewGuid();
    }

    public enum LogoDrawingType
    {
        Line,
        TurtlePosition
    }

    // NEW: Rendering mode enumeration
    public enum LogoRenderingMode
    {
        Immediate,  // Render each element as it's created with callbacks
        Animated    // Render with delays for smooth animation
    }

    // NEW: Canvas operation types for immediate updates
    public enum LogoCanvasOperationType
    {
        Clear,
        SetBackgroundColor,
        ShowTurtle,
        HideTurtle
    }

    // NEW: Canvas operation model
    public class LogoCanvasOperation
    {
        [JsonPropertyName("type")]
        public LogoCanvasOperationType Type { get; set; }
        
        [JsonPropertyName("parameters")]
        public Dictionary<string, object> Parameters { get; set; } = new();
        
        [JsonPropertyName("executedAt")]
        public DateTime ExecutedAt { get; set; } = DateTime.Now;

        // NEW: Unique identifier
        [JsonPropertyName("id")]
        public Guid Id { get; set; } = Guid.NewGuid();
    }

    public class LogoProgram
    {
        public string Name { get; set; } = "";
        public string Code { get; set; } = "";
        public string Description { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public List<string> Tags { get; set; } = new();
    }

    // NEW: Color utilities for integer-based color system
    public static class LogoColorUtils
    {
        // Predefined colors as integers (0-15 for basic Logo colors)
        // Using consecutive integers for logical color progression
        public static readonly Dictionary<string, int> ColorNameToInt = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "black", 0 },
            { "red", 1 },        // #FF0000
            { "green", 2 },      // #00FF00  
            { "blue", 3 },       // #0000FF
            { "yellow", 4 },     // #FFFF00 (red + green)
            { "magenta", 5 },    // #FF00FF (red + blue)
            { "cyan", 6 },       // #00FFFF (green + blue)
            { "white", 7 },      // #FFFFFF (all colors)
            { "gray", 8 },       // #808080
            { "grey", 8 },       // same as gray
            { "orange", 9 },     // #FFA500
            { "purple", 10 },    // #800080
            { "pink", 11 },      // #FFC0CB
            { "brown", 12 },     // #A52A2A
            { "lightblue", 13 }, // #87CEEB
            { "lightgreen", 14 },// #90EE90
            { "lightyellow", 15 }// #FFFFE0
        };

        // Convert integer color (0-15) to hex color
        // Using consecutive integers for logical color progression
        public static string IntColorToHex(int colorInt)
        {
            // Ensure color is in valid range
            colorInt = Math.Max(0, Math.Min(15, colorInt));
            
            return colorInt switch
            {
                0 => "#000000",  // black
                1 => "#FF0000",  // red
                2 => "#00FF00",  // green
                3 => "#0000FF",  // blue
                4 => "#FFFF00",  // yellow (red + green)
                5 => "#FF00FF",  // magenta (red + blue)
                6 => "#00FFFF",  // cyan (green + blue)
                7 => "#FFFFFF",  // white (all colors)
                8 => "#808080",  // gray
                9 => "#FFA500",  // orange
                10 => "#800080", // purple
                11 => "#FFC0CB", // pink
                12 => "#A52A2A", // brown
                13 => "#87CEEB", // lightblue
                14 => "#90EE90", // lightgreen
                15 => "#FFFFE0", // lightyellow
                _ => "#000000"   // default to black
            };
        }

        // Convert color name to integer
        public static int? GetColorInt(string colorName)
        {
            if (ColorNameToInt.TryGetValue(colorName, out int colorInt))
            {
                return colorInt;
            }
            return null;
        }

        // Convert HSV values to create rainbow colors for animation
        public static string HsvToHex(double hue, double saturation = 1.0, double value = 1.0)
        {
            // Normalize hue to 0-360 range
            hue = hue % 360;
            if (hue < 0) hue += 360;

            double c = value * saturation;
            double x = c * (1 - Math.Abs((hue / 60) % 2 - 1));
            double m = value - c;

            double r, g, b;

            if (hue < 60)
            {
                r = c; g = x; b = 0;
            }
            else if (hue < 120)
            {
                r = x; g = c; b = 0;
            }
            else if (hue < 180)
            {
                r = 0; g = c; b = x;
            }
            else if (hue < 240)
            {
                r = 0; g = x; b = c;
            }
            else if (hue < 300)
            {
                r = x; g = 0; b = c;
            }
            else
            {
                r = c; g = 0; b = x;
            }

            int red = (int)Math.Round((r + m) * 255);
            int green = (int)Math.Round((g + m) * 255);
            int blue = (int)Math.Round((b + m) * 255);

            return $"#{red:X2}{green:X2}{blue:X2}";
        }

        // Create a rainbow color based on a step value (useful for loops)
        public static string GetRainbowColor(int step, int totalSteps = 16)
        {
            double hue = (360.0 * step) / totalSteps;
            return HsvToHex(hue);
        }
    }
}