using WordScapeBlazorWasm.Games.Cartoon.Models;
using WordScapeBlazorWasm.Games.Cartoon.Data;

namespace WordScapeBlazorWasm.Games.Cartoon.Services;

/// <summary>
/// Service responsible for rendering letters as line-based vector graphics
/// Supports uppercase, lowercase, digits, and punctuation characters
/// </summary>
public class LetterRenderer
{
    /// <summary>
    /// Get the vector lines needed to draw a character at the specified position and scale
    /// </summary>
    /// <param name="letter">The character to render</param>
    /// <param name="x">X-coordinate of the letter's center point</param>
    /// <param name="y">Y-coordinate of the letter's center point</param>
    /// <param name="scaleFactor">Scaling factor (100.0 = normal size)</param>
    /// <returns>List of line segments (start point, end point) to draw the letter</returns>
    public static List<(Point start, Point end)> GetLetterLines(char letter, double x, double y, double scaleFactor)
    {
        var lines = new List<(Point, Point)>();
        double scale = scaleFactor / 100.0; // Normalize scale

        // Get the letter definition from the data-driven system
        var letterDef = LetterDefinitions.GetDefinition(letter);

        if (letterDef == null)
        {
            // Character not defined - return empty list
            return lines;
        }

        // Adjust vertical position and height for lowercase letters
        bool isLowerCase = char.IsLower(letter);
        double yOffset = isLowerCase ? y + (15 * scale) : y; // Lower position for lowercase
        double heightScale = isLowerCase ? 0.6 : 1.0; // Smaller height for lowercase
        double scaleLowerCase = scale * heightScale; // Combined scale for lowercase

        // Transform each line segment from definition coordinates to screen coordinates
        foreach (var segment in letterDef.Lines)
        {
            // For lowercase, use scaleLowerCase for Y coordinates to apply height scaling
            var startPoint = new Point(
                x + (segment.X1 * scale),
                yOffset + (segment.Y1 * scaleLowerCase)
            );

            var endPoint = new Point(
                x + (segment.X2 * scale),
                yOffset + (segment.Y2 * scaleLowerCase)
            );

            lines.Add((startPoint, endPoint));
        }

        return lines;
    }
}