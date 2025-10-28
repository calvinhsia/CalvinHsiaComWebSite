namespace WordScapeBlazorWasm.Games.Cartoon.Models;

/// <summary>
/// Point structure for coordinate handling
/// </summary>
public record struct Point(double X, double Y);

/// <summary>
/// Drawing modes for the cartoon editor
/// </summary>
public enum DrawMode
{
    /// <summary>Click-to-click line drawing</summary>
    Draw,
    /// <summary>Click and drag to draw</summary>
    Drag
}

/// <summary>
/// Demo animation types
/// </summary>
public enum DemoType
{
    Alphabet,
    Credits,
    History
}

/// <summary>
/// Represents a single line in a cartoon frame
/// </summary>
public class CartoonLine
{
    public Point Start { get; set; }
    public Point End { get; set; }
    public double Thickness { get; set; }
    public string Color { get; set; } = "#000000";

    public CartoonLine Clone()
    {
        return new CartoonLine
        {
            Start = this.Start,
            End = this.End,
            Thickness = this.Thickness,
            Color = this.Color
        };
    }
}

/// <summary>
/// Represents a single frame in the cartoon animation
/// </summary>
public class CartoonFrame
{
    public List<CartoonLine> Lines { get; set; } = new();

    public CartoonFrame Clone()
    {
        var frame = new CartoonFrame();
        foreach (var line in Lines)
            frame.Lines.Add(line.Clone());
        return frame;
    }
}

/// <summary>
/// Persistent state for saving/loading cartoon projects
/// </summary>
[Serializable]
public class CartoonPersistentState
{
    public List<CartoonFrame> Frames { get; set; } = new();
    public int CurrentFrameIndex { get; set; }
    public int BetweenFrames { get; set; } = 20;
    public double PenThickness { get; set; } = 2.0;
    public string PenColor { get; set; } = "#000000";
    public DrawMode CurrentMode { get; set; } = DrawMode.Drag;
    public int FrameDelay { get; set; } = 100;
    public DemoType CurrentDemoType { get; set; } = DemoType.Alphabet;
    public DateTime LastSaved { get; set; } = DateTime.Now;
}
