using BlazorWasm.Games.Cartoon.Models;
using BlazorWasm.Services;

namespace BlazorWasm.Games.Cartoon.Services;

/// <summary>
/// Core business logic for the Cartoon animation game
/// Handles demo generation, frame interpolation, and state persistence
/// </summary>
public class CartoonService
{
    private readonly RandomService _randomService;

    public CartoonService(RandomService randomService)
    {
        _randomService = randomService;
    }

    /// <summary>
    /// Generate alphabet demo animation (A-Z with random positioning)
    /// </summary>
    public List<CartoonFrame> GenerateAlphabetDemo(double canvasWidth, double canvasHeight)
    {
        var frames = new List<CartoonFrame>();
        var rand = _randomService.GetRandom();
        int nFrames = 26; // One frame per letter A-Z
        double centerX = canvasWidth / 2;
        double centerY = canvasHeight / 2;
        double scale = Math.Min(canvasWidth, canvasHeight) * 0.4;

        for (int nFrame = 0; nFrame < nFrames; nFrame++)
        {
            var frame = new CartoonFrame();
            char letter = (char)('A' + nFrame);

            // Random position offset for animation variety
            double offsetX = centerX + (rand.NextDouble() - 0.5) * (canvasWidth * 0.2);
            double offsetY = centerY + (rand.NextDouble() - 0.5) * (canvasHeight * 0.2);
            double letterScale = scale * (0.9 + rand.NextDouble() * 0.2);

            var lines = LetterRenderer.GetLetterLines(letter, offsetX, offsetY, letterScale);

            foreach (var (start, end) in lines)
            {
                frame.Lines.Add(new CartoonLine
                {
                    Start = start,
                    End = end,
                    Thickness = 8 + rand.NextDouble() * 4,
                    Color = $"#{rand.Next(0x1000000):X6}"
                });
            }

            frames.Add(frame);
        }

        return frames;
    }

    /// <summary>
    /// Generate word-based demo animation (one word per frame)
    /// Automatically scales text to fit canvas width
    /// </summary>
    public List<CartoonFrame> GenerateWordDemo(double canvasWidth, double canvasHeight, string sentence, double thickness = 6.0)
    {
        var frames = new List<CartoonFrame>();
        var rand = _randomService.GetRandom();
        var words = sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        double baseScale = Math.Min(canvasWidth, canvasHeight) * 0.15; // Base scale for multiple characters

        foreach (var word in words)
        {
            var frame = new CartoonFrame();

            // Calculate total width needed for the word
            int charCount = word.Length;
            double charSpacing = baseScale * 1.2;
            double totalWidth = charCount * charSpacing;

            // Auto-scale if word is too wide for canvas
            double scale = baseScale;
            if (totalWidth > canvasWidth * 0.9) // Use 90% of canvas width max
            {
                scale = (canvasWidth * 0.9) / (charCount * 1.2);
                charSpacing = scale * 1.2;
                totalWidth = charCount * charSpacing;
                DebugHelper.Log($"[CartoonService] Scaled down word '{word}' from {baseScale:F1} to {scale:F1} to fit canvas", true);
            }

            double startX = (canvasWidth - totalWidth) / 2;
            double centerY = canvasHeight / 2;

            // Random color for the whole word
            string wordColor = $"#{rand.Next(0x1000000):X6}";

            for (int i = 0; i < word.Length; i++)
            {
                char c = word[i];
                double x = startX + (i + 0.5) * charSpacing;
                double y = centerY + (rand.NextDouble() - 0.5) * (canvasHeight * 0.1); // Slight vertical variation

                var lines = LetterRenderer.GetLetterLines(c, x, y, scale);

                foreach (var (start, end) in lines)
                {
                    frame.Lines.Add(new CartoonLine
                    {
                        Start = start,
                        End = end,
                        Thickness = thickness + rand.NextDouble() * (thickness * 0.5), // Use parameter with slight random variation
                        Color = wordColor
                    });
                }
            }

            frames.Add(frame);
        }

        return frames;
    }

    /// <summary>
    /// Interpolate a frame between two user frames for smooth animation
    /// Uses linear interpolation for all line properties including color
    /// </summary>
    public CartoonFrame InterpolateFrame(List<CartoonFrame> userFrames, int userFrameIndex, int betweenIndex, int totalBetweenFrames)
    {
        if (userFrames.Count < 2 || betweenIndex == 0)
            return userFrames[userFrameIndex].Clone();

        var leftFrame = userFrames[userFrameIndex];
        var rightFrameIndex = (userFrameIndex + 1) % userFrames.Count;
        var rightFrame = userFrames[rightFrameIndex];

        // Handle empty frames
        if (leftFrame.Lines.Count == 0 && rightFrame.Lines.Count == 0)
            return new CartoonFrame();
        if (leftFrame.Lines.Count == 0)
            return rightFrame.Clone();
        if (rightFrame.Lines.Count == 0)
            return leftFrame.Clone();

        var nBetween = totalBetweenFrames + 1;
        double t = (double)betweenIndex / nBetween;

        var interpolatedFrame = new CartoonFrame();
        int maxLines = Math.Max(leftFrame.Lines.Count, rightFrame.Lines.Count);

        for (int i = 0; i < maxLines; i++)
        {
            // Use last line if one frame has fewer lines
            var lineLeft = i < leftFrame.Lines.Count ? leftFrame.Lines[i] : leftFrame.Lines[leftFrame.Lines.Count - 1];
            var lineRight = i < rightFrame.Lines.Count ? rightFrame.Lines[i] : rightFrame.Lines[rightFrame.Lines.Count - 1];

            interpolatedFrame.Lines.Add(new CartoonLine
            {
                Start = new Point(
                    lineLeft.Start.X + betweenIndex * (lineRight.Start.X - lineLeft.Start.X) / nBetween,
                    lineLeft.Start.Y + betweenIndex * (lineRight.Start.Y - lineLeft.Start.Y) / nBetween
                ),
                End = new Point(
                   lineLeft.End.X + betweenIndex * (lineRight.End.X - lineLeft.End.X) / nBetween,
                   lineLeft.End.Y + betweenIndex * (lineRight.End.Y - lineLeft.End.Y) / nBetween
                ),
                Thickness = lineLeft.Thickness + betweenIndex * (lineRight.Thickness - lineLeft.Thickness) / nBetween,
                Color = InterpolateColor(lineLeft.Color, lineRight.Color, t)
            });
        }

        return interpolatedFrame;
    }

    /// <summary>
    /// Interpolate between two hex colors using linear RGB interpolation
    /// </summary>
    private string InterpolateColor(string color1, string color2, double t)
    {
        // If colors are the same, no interpolation needed
        if (color1 == color2)
            return color1;

        // Parse hex colors to RGB components
        var (r1, g1, b1) = ParseHexColor(color1);
        var (r2, g2, b2) = ParseHexColor(color2);

        // Linear interpolation for each RGB component
        int r = (int)(r1 + t * (r2 - r1));
        int g = (int)(g1 + t * (g2 - g1));
        int b = (int)(b1 + t * (b2 - b1));

        // Clamp values to valid range
        r = Math.Clamp(r, 0, 255);
        g = Math.Clamp(g, 0, 255);
        b = Math.Clamp(b, 0, 255);

        // Convert back to hex
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    /// <summary>
    /// Parse a hex color string to RGB components
    /// Supports both #RRGGBB and #RGB formats
    /// </summary>
    private (int r, int g, int b) ParseHexColor(string hexColor)
    {
        // Remove # if present
        hexColor = hexColor.TrimStart('#');

        // Handle 3-character shorthand (#RGB -> #RRGGBB)
        if (hexColor.Length == 3)
        {
            hexColor = $"{hexColor[0]}{hexColor[0]}{hexColor[1]}{hexColor[1]}{hexColor[2]}{hexColor[2]}";
        }

        // Parse hex to RGB
        int r = Convert.ToInt32(hexColor.Substring(0, 2), 16);
        int g = Convert.ToInt32(hexColor.Substring(2, 2), 16);
        int b = Convert.ToInt32(hexColor.Substring(4, 2), 16);

        return (r, g, b);
    }
}
