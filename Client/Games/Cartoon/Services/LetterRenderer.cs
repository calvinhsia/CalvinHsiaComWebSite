using WordScapeBlazorWasm.Games.Cartoon.Models;

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

        // Convert to uppercase for the switch, but preserve original for spacing adjustments
        char upperLetter = char.ToUpper(letter);
        bool isLowerCase = char.IsLower(letter);

        // Adjust vertical position for lowercase letters (smaller, sitting on baseline)
        double yOffset = isLowerCase ? y + (15 * scale) : y; // Lower position for lowercase
        double heightScale = isLowerCase ? 0.6 : 1.0; // Smaller height for lowercase

        // REFACTORED: Combine s * heightScale into a single variable to reduce duplication and prevent bugs
        double scaleLowerCase = scale * heightScale; // Scale-height multiplier for lowercase letters

        switch (upperLetter)
        {
            case 'A':
                if (isLowerCase)
                {
                    // Lowercase 'a' - circular with tail
                    lines.Add((new Point(x + 30 * scale, yOffset - 20 * scaleLowerCase), new Point(x + 10 * scale, yOffset - 30 * scaleLowerCase))); // Top left
                    lines.Add((new Point(x + 10 * scale, yOffset - 30 * scaleLowerCase), new Point(x - 10 * scale, yOffset - 30 * scaleLowerCase))); // Top
                    lines.Add((new Point(x - 10 * scale, yOffset - 30 * scaleLowerCase), new Point(x - 30 * scale, yOffset - 10 * scaleLowerCase))); // Top left curve
                    lines.Add((new Point(x - 30 * scale, yOffset - 10 * scaleLowerCase), new Point(x - 30 * scale, yOffset + 10 * scaleLowerCase))); // Left
                    lines.Add((new Point(x - 30 * scale, yOffset + 10 * scaleLowerCase), new Point(x - 10 * scale, yOffset + 30 * scaleLowerCase))); // Bottom left
                    lines.Add((new Point(x - 10 * scale, yOffset + 30 * scaleLowerCase), new Point(x + 10 * scale, yOffset + 30 * scaleLowerCase))); // Bottom
                    lines.Add((new Point(x + 10 * scale, yOffset + 30 * scaleLowerCase), new Point(x + 30 * scale, yOffset + 10 * scaleLowerCase))); // Bottom right
                    lines.Add((new Point(x + 30 * scale, yOffset + 10 * scaleLowerCase), new Point(x + 30 * scale, yOffset + 30 * scaleLowerCase))); // Tail down
                }
                else
                {
                    // Uppercase 'A'
                    lines.Add((new Point(x - 50 * scale, y + 50 * scale), new Point(x, y - 50 * scale))); // Left diagonal
                    lines.Add((new Point(x, y - 50 * scale), new Point(x + 50 * scale, y + 50 * scale))); // Right diagonal
                    lines.Add((new Point(x - 25 * scale, y), new Point(x + 25 * scale, y))); // Cross bar
                }
                break;
            case 'B':
                if (isLowerCase)
                {
                    // Lowercase 'b' - tall stem with circular bowl
                    lines.Add((new Point(x - 30 * scale, y - 50 * scale), new Point(x - 30 * scale, yOffset + 30 * scaleLowerCase))); // Tall stem
                    lines.Add((new Point(x - 30 * scale, yOffset - 30 * scaleLowerCase), new Point(x + 10 * scale, yOffset - 30 * scaleLowerCase))); // Top of bowl
                    lines.Add((new Point(x + 10 * scale, yOffset - 30 * scaleLowerCase), new Point(x + 30 * scale, yOffset - 10 * scaleLowerCase))); // Top right curve
                    lines.Add((new Point(x + 30 * scale, yOffset - 10 * scaleLowerCase), new Point(x + 30 * scale, yOffset + 10 * scaleLowerCase))); // Right side
                    lines.Add((new Point(x + 30 * scale, yOffset + 10 * scaleLowerCase), new Point(x + 10 * scale, yOffset + 30 * scaleLowerCase))); // Bottom right curve
                    lines.Add((new Point(x + 10 * scale, yOffset + 30 * scaleLowerCase), new Point(x - 30 * scale, yOffset + 30 * scaleLowerCase))); // Bottom
                }
                else
                {
                    // Uppercase 'B'
                    lines.Add((new Point(x - 40 * scale, y - 50 * scale), new Point(x - 40 * scale, y + 50 * scale))); // Vertical
                    lines.Add((new Point(x - 40 * scale, y - 50 * scale), new Point(x + 20 * scale, y - 50 * scale))); // Top
                    lines.Add((new Point(x + 20 * scale, y - 50 * scale), new Point(x + 40 * scale, y - 25 * scale))); // Top curve
                    lines.Add((new Point(x + 40 * scale, y - 25 * scale), new Point(x - 40 * scale, y))); // To middle
                    lines.Add((new Point(x - 40 * scale, y), new Point(x + 30 * scale, y))); // Middle
                    lines.Add((new Point(x + 30 * scale, y), new Point(x + 40 * scale, y + 25 * scale))); // Bottom curve
                    lines.Add((new Point(x + 40 * scale, y + 25 * scale), new Point(x - 40 * scale, y + 50 * scale))); // To bottom
                }
                break;
            case 'C':
                if (isLowerCase)
                {
                    // Lowercase 'c' - simple arc
                    lines.Add((new Point(x + 30 * scale, yOffset - 20 * scaleLowerCase), new Point(x, yOffset - 30 * scaleLowerCase))); // Top
                    lines.Add((new Point(x, yOffset - 30 * scaleLowerCase), new Point(x - 30 * scale, yOffset))); // Top left curve
                    lines.Add((new Point(x - 30 * scale, yOffset), new Point(x, yOffset + 30 * scaleLowerCase))); // Bottom left curve
                    lines.Add((new Point(x, yOffset + 30 * scaleLowerCase), new Point(x + 30 * scale, yOffset + 20 * scaleLowerCase))); // Bottom
                }
                else
                {
                    // Uppercase 'C'
                    lines.Add((new Point(x + 40 * scale, y - 40 * scale), new Point(x - 20 * scale, y - 40 * scale))); // Top
                    lines.Add((new Point(x - 20 * scale, y - 40 * scale), new Point(x - 40 * scale, y - 20 * scale))); // Top curve
                    lines.Add((new Point(x - 40 * scale, y - 20 * scale), new Point(x - 40 * scale, y + 20 * scale))); // Left
                    lines.Add((new Point(x - 40 * scale, y + 20 * scale), new Point(x - 20 * scale, y + 40 * scale))); // Bottom curve
                    lines.Add((new Point(x - 20 * scale, y + 40 * scale), new Point(x + 40 * scale, y + 40 * scale))); // Bottom
                }
                break;
            case 'D':
                if (isLowerCase)
                {
                    // Lowercase 'd' - circular bowl with tall stem on right
                    lines.Add((new Point(x + 30 * scale, y - 50 * scale), new Point(x + 30 * scale, yOffset + 30 * scaleLowerCase))); // Tall stem
                    lines.Add((new Point(x - 10 * scale, yOffset - 30 * scaleLowerCase), new Point(x + 30 * scale, yOffset - 30 * scaleLowerCase))); // Top
                    lines.Add((new Point(x - 10 * scale, yOffset - 30 * scaleLowerCase), new Point(x - 30 * scale, yOffset - 10 * scaleLowerCase))); // Top left curve
                    lines.Add((new Point(x - 30 * scale, yOffset - 10 * scaleLowerCase), new Point(x - 30 * scale, yOffset + 10 * scaleLowerCase))); // Left
                    lines.Add((new Point(x - 30 * scale, yOffset + 10 * scaleLowerCase), new Point(x - 10 * scale, yOffset + 30 * scaleLowerCase))); // Bottom left curve
                    lines.Add((new Point(x - 10 * scale, yOffset + 30 * scaleLowerCase), new Point(x + 30 * scale, yOffset + 30 * scaleLowerCase))); // Bottom
                }
                else
                {
                    // Uppercase 'D'
                    lines.Add((new Point(x - 40 * scale, y - 50 * scale), new Point(x - 40 * scale, y + 50 * scale))); // Vertical
                    lines.Add((new Point(x - 40 * scale, y - 50 * scale), new Point(x + 20 * scale, y - 50 * scale))); // Top
                    lines.Add((new Point(x + 20 * scale, y - 50 * scale), new Point(x + 40 * scale, y - 20 * scale))); // Top curve
                    lines.Add((new Point(x + 40 * scale, y - 20 * scale), new Point(x + 40 * scale, y + 20 * scale))); // Right
                    lines.Add((new Point(x + 40 * scale, y + 20 * scale), new Point(x + 20 * scale, y + 50 * scale))); // Bottom curve
                    lines.Add((new Point(x + 20 * scale, y + 50 * scale), new Point(x - 40 * scale, y + 50 * scale))); // Bottom
                }
                break;
            case 'E':
                if (isLowerCase)
                {
                    // Lowercase 'e' - circular with horizontal bar
                    lines.Add((new Point(x - 30 * scale, yOffset), new Point(x + 30 * scale, yOffset))); // Horizontal bar
                    lines.Add((new Point(x + 30 * scale, yOffset), new Point(x + 20 * scale, yOffset - 30 * scaleLowerCase))); // Top right
                    lines.Add((new Point(x + 20 * scale, yOffset - 30 * scaleLowerCase), new Point(x - 10 * scale, yOffset - 30 * scaleLowerCase))); // Top
                    lines.Add((new Point(x - 10 * scale, yOffset - 30 * scaleLowerCase), new Point(x - 30 * scale, yOffset - 10 * scaleLowerCase))); // Top left
                    lines.Add((new Point(x - 30 * scale, yOffset - 10 * scaleLowerCase), new Point(x - 30 * scale, yOffset + 10 * scaleLowerCase))); // Left
                    lines.Add((new Point(x - 30 * scale, yOffset + 10 * scaleLowerCase), new Point(x - 10 * scale, yOffset + 30 * scaleLowerCase))); // Bottom left
                    lines.Add((new Point(x - 10 * scale, yOffset + 30 * scaleLowerCase), new Point(x + 20 * scale, yOffset + 30 * scaleLowerCase))); // Bottom
                    lines.Add((new Point(x + 20 * scale, yOffset + 30 * scaleLowerCase), new Point(x + 30 * scale, yOffset + 20 * scaleLowerCase))); // Bottom right
                }
                else
                {
                    // Uppercase 'E'
                    lines.Add((new Point(x - 40 * scale, y - 50 * scale), new Point(x - 40 * scale, y + 50 * scale))); // Vertical
                    lines.Add((new Point(x - 40 * scale, y - 50 * scale), new Point(x + 40 * scale, y - 50 * scale))); // Top
                    lines.Add((new Point(x - 40 * scale, y), new Point(x + 30 * scale, y))); // Middle
                    lines.Add((new Point(x - 40 * scale, y + 50 * scale), new Point(x + 40 * scale, y + 50 * scale))); // Bottom
                }
                break;
            case 'F':
                if (isLowerCase)
                {
                    // Lowercase 'f' - hook at top with crossbar
                    lines.Add((new Point(x + 10 * scale, y - 50 * scale), new Point(x - 10 * scale, y - 50 * scale))); // Top hook
                    lines.Add((new Point(x - 10 * scale, y - 50 * scale), new Point(x - 20 * scale, y - 40 * scale))); // Hook curve
                    lines.Add((new Point(x - 20 * scale, y - 40 * scale), new Point(x - 20 * scale, yOffset + 30 * scaleLowerCase))); // Stem
                    lines.Add((new Point(x - 35 * scale, yOffset - 10 * scaleLowerCase), new Point(x + 5 * scale, yOffset - 10 * scaleLowerCase))); // Crossbar
                }
                else
                {
                    // Uppercase 'F'
                    lines.Add((new Point(x - 40 * scale, y - 50 * scale), new Point(x - 40 * scale, y + 50 * scale))); // Vertical
                    lines.Add((new Point(x - 40 * scale, y - 50 * scale), new Point(x + 40 * scale, y - 50 * scale))); // Top
                    lines.Add((new Point(x - 40 * scale, y), new Point(x + 30 * scale, y))); // Middle
                }
                break;
            case 'G':
                if (isLowerCase)
                {
                    // Lowercase 'g' - circular bowl with descender
                    lines.Add((new Point(x + 30 * scale, yOffset - 30 * scaleLowerCase), new Point(x + 10 * scale, yOffset - 30 * scaleLowerCase))); // Top
                    lines.Add((new Point(x + 10 * scale, yOffset - 30 * scaleLowerCase), new Point(x - 10 * scale, yOffset - 20 * scaleLowerCase))); // Top left
                    lines.Add((new Point(x - 10 * scale, yOffset - 20 * scaleLowerCase), new Point(x - 30 * scale, yOffset))); // Left top curve
                    lines.Add((new Point(x - 30 * scale, yOffset), new Point(x - 10 * scale, yOffset + 20 * scaleLowerCase))); // Left bottom curve
                    lines.Add((new Point(x - 10 * scale, yOffset + 20 * scaleLowerCase), new Point(x + 10 * scale, yOffset + 30 * scaleLowerCase))); // Bottom
                    lines.Add((new Point(x + 10 * scale, yOffset + 30 * scaleLowerCase), new Point(x + 30 * scale, yOffset + 10 * scaleLowerCase))); // Bottom right curve
                    lines.Add((new Point(x + 30 * scale, yOffset + 10 * scaleLowerCase), new Point(x + 30 * scale, y + 40 * scale))); // Descender down
                    lines.Add((new Point(x + 30 * scale, y + 40 * scale), new Point(x + 10 * scale, y + 50 * scale))); // Descender curve
                    lines.Add((new Point(x + 10 * scale, y + 50 * scale), new Point(x - 10 * scale, y + 50 * scale))); // Descender bottom
                }
                else
                {
                    // Uppercase 'G'
                    lines.Add((new Point(x + 40 * scale, y - 40 * scale), new Point(x - 20 * scale, y - 40 * scale))); // Top
                    lines.Add((new Point(x - 20 * scale, y - 40 * scale), new Point(x - 40 * scale, y - 20 * scale))); // Top curve
                    lines.Add((new Point(x - 40 * scale, y - 20 * scale), new Point(x - 40 * scale, y + 20 * scale))); // Left
                    lines.Add((new Point(x - 40 * scale, y + 20 * scale), new Point(x - 20 * scale, y + 40 * scale))); // Bottom curve
                    lines.Add((new Point(x - 20 * scale, y + 40 * scale), new Point(x + 40 * scale, y + 40 * scale))); // Bottom
                    lines.Add((new Point(x + 40 * scale, y + 40 * scale), new Point(x + 40 * scale, y))); // Right
                    lines.Add((new Point(x + 40 * scale, y), new Point(x + 10 * scale, y))); // Middle bar
                }
                break;
            case 'H':
                if (isLowerCase)
                {
                    // Lowercase 'h' - tall stem with right hump
                    lines.Add((new Point(x - 30 * scale, y - 50 * scale), new Point(x - 30 * scale, yOffset + 30 * scaleLowerCase))); // Tall stem
                    lines.Add((new Point(x - 30 * scale, yOffset - 20 * scaleLowerCase), new Point(x, yOffset - 30 * scaleLowerCase))); // Top of hump
                    lines.Add((new Point(x, yOffset - 30 * scaleLowerCase), new Point(x + 30 * scale, yOffset - 10 * scaleLowerCase))); // Curve
                    lines.Add((new Point(x + 30 * scale, yOffset - 10 * scaleLowerCase), new Point(x + 30 * scale, yOffset + 30 * scaleLowerCase))); // Right stem
                }
                else
                {
                    // Uppercase 'H'
                    lines.Add((new Point(x - 40 * scale, y - 50 * scale), new Point(x - 40 * scale, y + 50 * scale))); // Left vertical
                    lines.Add((new Point(x + 40 * scale, y - 50 * scale), new Point(x + 40 * scale, y + 50 * scale))); // Right vertical
                    lines.Add((new Point(x - 40 * scale, y), new Point(x + 40 * scale, y))); // Cross bar
                }
                break;
            case 'I':
                if (isLowerCase)
                {
                    // Lowercase 'i' - short stem with dot
                    lines.Add((new Point(x, yOffset - 30 * scaleLowerCase), new Point(x, yOffset + 30 * scaleLowerCase))); // Stem
                    lines.Add((new Point(x - 5 * scale, y - 45 * scale), new Point(x + 5 * scale, y - 45 * scale))); // Dot top
                    lines.Add((new Point(x + 5 * scale, y - 45 * scale), new Point(x + 5 * scale, y - 55 * scale))); // Dot right
                    lines.Add((new Point(x + 5 * scale, y - 55 * scale), new Point(x - 5 * scale, y - 55 * scale))); // Dot bottom
                    lines.Add((new Point(x - 5 * scale, y - 55 * scale), new Point(x - 5 * scale, y - 45 * scale))); // Dot left
                }
                else
                {
                    // Uppercase 'I'
                    lines.Add((new Point(x, y - 50 * scale), new Point(x, y + 50 * scale))); // Vertical
                    lines.Add((new Point(x - 30 * scale, y - 50 * scale), new Point(x + 30 * scale, y - 50 * scale))); // Top
                    lines.Add((new Point(x - 30 * scale, y + 50 * scale), new Point(x + 30 * scale, y + 50 * scale))); // Bottom
                }
                break;
            case 'J':
                if (isLowerCase)
                {
                    // Lowercase 'j' - stem with descender and dot
                    lines.Add((new Point(x, yOffset - 30 * scaleLowerCase), new Point(x, y + 40 * scale))); // Stem with descender
                    lines.Add((new Point(x, y + 40 * scale), new Point(x - 10 * scale, y + 50 * scale))); // Descender curve
                    lines.Add((new Point(x - 10 * scale, y + 50 * scale), new Point(x - 20 * scale, y + 50 * scale))); // Descender bottom
                    lines.Add((new Point(x - 5 * scale, y - 45 * scale), new Point(x + 5 * scale, y - 45 * scale))); // Dot top
                    lines.Add((new Point(x + 5 * scale, y - 45 * scale), new Point(x + 5 * scale, y - 55 * scale))); // Dot right
                    lines.Add((new Point(x + 5 * scale, y - 55 * scale), new Point(x - 5 * scale, y - 55 * scale))); // Dot bottom
                    lines.Add((new Point(x - 5 * scale, y - 55 * scale), new Point(x - 5 * scale, y - 45 * scale))); // Dot left
                }
                else
                {
                    // Uppercase 'J'
                    lines.Add((new Point(x + 30 * scale, y - 50 * scale), new Point(x + 30 * scale, y + 30 * scale))); // Vertical
                    lines.Add((new Point(x + 30 * scale, y + 30 * scale), new Point(x + 10 * scale, y + 50 * scale))); // Bottom curve
                    lines.Add((new Point(x + 10 * scale, y + 50 * scale), new Point(x - 20 * scale, y + 50 * scale))); // Bottom
                    lines.Add((new Point(x - 20 * scale, y + 50 * scale), new Point(x - 40 * scale, y + 30 * scale))); // Bottom left curve
                }
                break;
            case 'K':
                if (isLowerCase)
                {
                    // Lowercase 'k' - tall stem with angled arm and leg
                    lines.Add((new Point(x - 30 * scale, y - 50 * scale), new Point(x - 30 * scale, yOffset + 30 * scaleLowerCase))); // Tall stem
                    lines.Add((new Point(x + 30 * scale, yOffset - 30 * scaleLowerCase), new Point(x - 30 * scale, yOffset))); // Top diagonal to middle
                    lines.Add((new Point(x - 30 * scale, yOffset), new Point(x + 30 * scale, yOffset + 30 * scaleLowerCase))); // Bottom diagonal from middle
                }
                else
                {
                    // Uppercase 'K'
                    lines.Add((new Point(x - 40 * scale, y - 50 * scale), new Point(x - 40 * scale, y + 50 * scale))); // Vertical
                    lines.Add((new Point(x + 40 * scale, y - 50 * scale), new Point(x - 40 * scale, y))); // Top diagonal
                    lines.Add((new Point(x - 40 * scale, y), new Point(x + 40 * scale, y + 50 * scale))); // Bottom diagonal
                }
                break;
            case 'L':
                if (isLowerCase)
                {
                    // Lowercase 'l' - simple tall stem
                    lines.Add((new Point(x, y - 50 * scale), new Point(x, yOffset + 30 * scaleLowerCase))); // Tall stem
                }
                else
                {
                    // Uppercase 'L'
                    lines.Add((new Point(x - 40 * scale, y - 50 * scale), new Point(x - 40 * scale, y + 50 * scale))); // Vertical
                    lines.Add((new Point(x - 40 * scale, y + 50 * scale), new Point(x + 40 * scale, y + 50 * scale))); // Bottom
                }
                break;
            case 'M':
                if (isLowerCase)
                {
                    // Lowercase 'm' - three humps
                    lines.Add((new Point(x - 40 * scale, yOffset + 30 * scaleLowerCase), new Point(x - 40 * scale, yOffset - 30 * scaleLowerCase))); // Left stem
                    lines.Add((new Point(x - 40 * scale, yOffset - 20 * scaleLowerCase), new Point(x - 20 * scale, yOffset - 30 * scaleLowerCase))); // First hump top
                    lines.Add((new Point(x - 20 * scale, yOffset - 30 * scaleLowerCase), new Point(x - 10 * scale, yOffset - 20 * scaleLowerCase))); // First hump curve
                    lines.Add((new Point(x - 10 * scale, yOffset - 20 * scaleLowerCase), new Point(x - 10 * scale, yOffset + 30 * scaleLowerCase))); // First middle stem
                    lines.Add((new Point(x - 10 * scale, yOffset - 20 * scaleLowerCase), new Point(x + 10 * scale, yOffset - 30 * scaleLowerCase))); // Second hump top
                    lines.Add((new Point(x + 10 * scale, yOffset - 30 * scaleLowerCase), new Point(x + 20 * scale, yOffset - 20 * scaleLowerCase))); // Second hump curve
                    lines.Add((new Point(x + 20 * scale, yOffset - 20 * scaleLowerCase), new Point(x + 20 * scale, yOffset + 30 * scaleLowerCase))); // Second middle stem
                    lines.Add((new Point(x + 20 * scale, yOffset - 20 * scaleLowerCase), new Point(x + 35 * scale, yOffset - 30 * scaleLowerCase))); // Third hump top
                    lines.Add((new Point(x + 35 * scale, yOffset - 30 * scaleLowerCase), new Point(x + 45 * scale, yOffset - 10 * scaleLowerCase))); // Third hump curve
                    lines.Add((new Point(x + 45 * scale, yOffset - 10 * scaleLowerCase), new Point(x + 45 * scale, yOffset + 30 * scaleLowerCase))); // Right stem
                }
                else
                {
                    // Uppercase 'M'
                    lines.Add((new Point(x - 50 * scale, y + 50 * scale), new Point(x - 50 * scale, y - 50 * scale))); // Left vertical
                    lines.Add((new Point(x - 50 * scale, y - 50 * scale), new Point(x, y))); // Left diagonal
                    lines.Add((new Point(x, y), new Point(x + 50 * scale, y - 50 * scale))); // Right diagonal
                    lines.Add((new Point(x + 50 * scale, y - 50 * scale), new Point(x + 50 * scale, y + 50 * scale))); // Right vertical
                }
                break;
            case 'N':
                if (isLowerCase)
                {
                    // Lowercase 'n' - hump like 'h' but shorter
                    lines.Add((new Point(x - 30 * scale, yOffset - 30 * scaleLowerCase), new Point(x - 30 * scale, yOffset + 30 * scaleLowerCase))); // Left stem
                    lines.Add((new Point(x - 30 * scale, yOffset - 20 * scaleLowerCase), new Point(x, yOffset - 30 * scaleLowerCase))); // Top of hump
                    lines.Add((new Point(x, yOffset - 30 * scaleLowerCase), new Point(x + 30 * scale, yOffset - 10 * scaleLowerCase))); // Curve
                    lines.Add((new Point(x + 30 * scale, yOffset - 10 * scaleLowerCase), new Point(x + 30 * scale, yOffset + 30 * scaleLowerCase))); // Right stem
                }
                else
                {
                    // Uppercase 'N'
                    lines.Add((new Point(x - 40 * scale, y + 50 * scale), new Point(x - 40 * scale, y - 50 * scale))); // Left vertical
                    lines.Add((new Point(x - 40 * scale, y - 50 * scale), new Point(x + 40 * scale, y + 50 * scale))); // Diagonal
                    lines.Add((new Point(x + 40 * scale, y + 50 * scale), new Point(x + 40 * scale, y - 50 * scale))); // Right vertical
                }
                break;
            case 'O':
                if (isLowerCase)
                {
                    // Lowercase 'o' - smaller circle
                    lines.Add((new Point(x - 10 * scale, yOffset - 30 * scaleLowerCase), new Point(x + 10 * scale, yOffset - 30 * scaleLowerCase))); // Top
                    lines.Add((new Point(x + 10 * scale, yOffset - 30 * scaleLowerCase), new Point(x + 30 * scale, yOffset - 10 * scaleLowerCase))); // Top right
                    lines.Add((new Point(x + 30 * scale, yOffset - 10 * scaleLowerCase), new Point(x + 30 * scale, yOffset + 10 * scaleLowerCase))); // Right
                    lines.Add((new Point(x + 30 * scale, yOffset + 10 * scaleLowerCase), new Point(x + 10 * scale, yOffset + 30 * scaleLowerCase))); // Bottom right
                    lines.Add((new Point(x + 10 * scale, yOffset + 30 * scaleLowerCase), new Point(x - 10 * scale, yOffset + 30 * scaleLowerCase))); // Bottom
                    lines.Add((new Point(x - 10 * scale, yOffset + 30 * scaleLowerCase), new Point(x - 30 * scale, yOffset + 10 * scaleLowerCase))); // Bottom left
                    lines.Add((new Point(x - 30 * scale, yOffset + 10 * scaleLowerCase), new Point(x - 30 * scale, yOffset - 10 * scaleLowerCase))); // Left
                    lines.Add((new Point(x - 30 * scale, yOffset - 10 * scaleLowerCase), new Point(x - 10 * scale, yOffset - 30 * scaleLowerCase))); // Top left
                }
                else
                {
                    // Uppercase 'O'
                    lines.Add((new Point(x - 20 * scale, y - 40 * scale), new Point(x + 20 * scale, y - 40 * scale))); // Top
                    lines.Add((new Point(x + 20 * scale, y - 40 * scale), new Point(x + 40 * scale, y - 20 * scale))); // Top right
                    lines.Add((new Point(x + 40 * scale, y - 20 * scale), new Point(x + 40 * scale, y + 20 * scale))); // Right
                    lines.Add((new Point(x + 40 * scale, y + 20 * scale), new Point(x + 20 * scale, y + 40 * scale))); // Bottom right
                    lines.Add((new Point(x + 20 * scale, y + 40 * scale), new Point(x - 20 * scale, y + 40 * scale))); // Bottom
                    lines.Add((new Point(x - 20 * scale, y + 40 * scale), new Point(x - 40 * scale, y + 20 * scale))); // Bottom left
                    lines.Add((new Point(x - 40 * scale, y + 20 * scale), new Point(x - 40 * scale, y - 20 * scale))); // Left
                    lines.Add((new Point(x - 40 * scale, y - 20 * scale), new Point(x - 20 * scale, y - 40 * scale))); // Top left
                }
                break;
            case 'P':
                if (isLowerCase)
                {
                    // Lowercase 'p' - bowl on top with descender
                    lines.Add((new Point(x - 30 * scale, yOffset - 30 * scaleLowerCase), new Point(x - 30 * scale, y + 50 * scale))); // Stem with descender
                    lines.Add((new Point(x - 30 * scale, yOffset - 30 * scaleLowerCase), new Point(x + 10 * scale, yOffset - 30 * scaleLowerCase))); // Top of bowl
                    lines.Add((new Point(x + 10 * scale, yOffset - 30 * scaleLowerCase), new Point(x + 30 * scale, yOffset - 10 * scaleLowerCase))); // Top right curve
                    lines.Add((new Point(x + 30 * scale, yOffset - 10 * scaleLowerCase), new Point(x + 30 * scale, yOffset + 10 * scaleLowerCase))); // Right side
                    lines.Add((new Point(x + 30 * scale, yOffset + 10 * scaleLowerCase), new Point(x + 10 * scale, yOffset + 30 * scaleLowerCase))); // Bottom right curve
                    lines.Add((new Point(x + 10 * scale, yOffset + 30 * scaleLowerCase), new Point(x - 30 * scale, yOffset + 30 * scaleLowerCase))); // Bottom
                }
                else
                {
                    // Uppercase 'P'
                    lines.Add((new Point(x - 40 * scale, y - 50 * scale), new Point(x - 40 * scale, y + 50 * scale))); // Vertical
                    lines.Add((new Point(x - 40 * scale, y - 50 * scale), new Point(x + 30 * scale, y - 50 * scale))); // Top
                    lines.Add((new Point(x + 30 * scale, y - 50 * scale), new Point(x + 40 * scale, y - 30 * scale))); // Top curve
                    lines.Add((new Point(x + 40 * scale, y - 30 * scale), new Point(x + 40 * scale, y - 10 * scale))); // Right top
                    lines.Add((new Point(x + 40 * scale, y - 10 * scale), new Point(x + 30 * scale, y))); // Curve to middle
                    lines.Add((new Point(x + 30 * scale, y), new Point(x - 40 * scale, y))); // Middle
                }
                break;
            case 'Q':
                if (isLowerCase)
                {
                    // Lowercase 'q' - bowl with right-side descender
                    lines.Add((new Point(x + 30 * scale, yOffset - 30 * scaleLowerCase), new Point(x + 30 * scale, y + 50 * scale))); // Right stem with descender
                    lines.Add((new Point(x - 10 * scale, yOffset - 30 * scaleLowerCase), new Point(x + 30 * scale, yOffset - 30 * scaleLowerCase))); // Top
                    lines.Add((new Point(x - 10 * scale, yOffset - 30 * scaleLowerCase), new Point(x - 30 * scale, yOffset - 10 * scaleLowerCase))); // Top left curve
                    lines.Add((new Point(x - 30 * scale, yOffset - 10 * scaleLowerCase), new Point(x - 30 * scale, yOffset + 10 * scaleLowerCase))); // Left
                    lines.Add((new Point(x - 30 * scale, yOffset + 10 * scaleLowerCase), new Point(x - 10 * scale, yOffset + 30 * scaleLowerCase))); // Bottom left curve
                    lines.Add((new Point(x - 10 * scale, yOffset + 30 * scaleLowerCase), new Point(x + 30 * scale, yOffset + 30 * scaleLowerCase))); // Bottom
                }
                else
                {
                    // Uppercase 'Q'
                    lines.Add((new Point(x - 20 * scale, y - 40 * scale), new Point(x + 20 * scale, y - 40 * scale))); // Top
                    lines.Add((new Point(x + 20 * scale, y - 40 * scale), new Point(x + 40 * scale, y - 20 * scale))); // Top right
                    lines.Add((new Point(x + 40 * scale, y - 20 * scale), new Point(x + 40 * scale, y + 20 * scale))); // Right
                    lines.Add((new Point(x + 40 * scale, y + 20 * scale), new Point(x + 20 * scale, y + 40 * scale))); // Bottom right
                    lines.Add((new Point(x + 20 * scale, y + 40 * scale), new Point(x - 20 * scale, y + 40 * scale))); // Bottom
                    lines.Add((new Point(x - 20 * scale, y + 40 * scale), new Point(x - 40 * scale, y + 20 * scale))); // Bottom left
                    lines.Add((new Point(x - 40 * scale, y + 20 * scale), new Point(x - 40 * scale, y - 20 * scale))); // Left
                    lines.Add((new Point(x - 40 * scale, y - 20 * scale), new Point(x - 20 * scale, y - 40 * scale))); // Top left
                    lines.Add((new Point(x + 10 * scale, y + 10 * scale), new Point(x + 50 * scale, y + 50 * scale))); // Tail
                }
                break;
            case 'R':
                if (isLowerCase)
                {
                    // Lowercase 'r' - short stem with small shoulder
                    lines.Add((new Point(x - 30 * scale, yOffset - 30 * scaleLowerCase), new Point(x - 30 * scale, yOffset + 30 * scaleLowerCase))); // Stem
                    lines.Add((new Point(x - 30 * scale, yOffset - 20 * scaleLowerCase), new Point(x - 10 * scale, yOffset - 30 * scaleLowerCase))); // Shoulder top
                    lines.Add((new Point(x - 10 * scale, yOffset - 30 * scaleLowerCase), new Point(x + 10 * scale, yOffset - 25 * scaleLowerCase))); // Shoulder curve
                    lines.Add((new Point(x + 10 * scale, yOffset - 25 * scaleLowerCase), new Point(x + 20 * scale, yOffset - 20 * scaleLowerCase))); // Shoulder end
                }
                else
                {
                    // Uppercase 'R'
                    lines.Add((new Point(x - 40 * scale, y - 50 * scale), new Point(x - 40 * scale, y + 50 * scale))); // Vertical
                    lines.Add((new Point(x - 40 * scale, y - 50 * scale), new Point(x + 30 * scale, y - 50 * scale))); // Top
                    lines.Add((new Point(x + 30 * scale, y - 50 * scale), new Point(x + 40 * scale, y - 30 * scale))); // Top curve
                    lines.Add((new Point(x + 40 * scale, y - 30 * scale), new Point(x + 40 * scale, y - 10 * scale))); // Right top
                    lines.Add((new Point(x + 40 * scale, y - 10 * scale), new Point(x + 30 * scale, y))); // Curve to middle
                    lines.Add((new Point(x + 30 * scale, y), new Point(x - 40 * scale, y))); // Middle
                    lines.Add((new Point(x, y), new Point(x + 40 * scale, y + 50 * scale))); // Leg
                }
                break;
            case 'S':
                if (isLowerCase)
                {
                    // Lowercase 's' - smaller S curve
                    lines.Add((new Point(x + 25 * scale, yOffset - 20 * scaleLowerCase), new Point(x + 10 * scale, yOffset - 30 * scaleLowerCase))); // Top right
                    lines.Add((new Point(x + 10 * scale, yOffset - 30 * scaleLowerCase), new Point(x - 10 * scale, yOffset - 30 * scaleLowerCase))); // Top
                    lines.Add((new Point(x - 10 * scale, yOffset - 30 * scaleLowerCase), new Point(x - 25 * scale, yOffset - 15 * scaleLowerCase))); // Top left - FIXED
                    lines.Add((new Point(x - 25 * scale, yOffset - 15 * scaleLowerCase), new Point(x - 10 * scale, yOffset))); // Upper curve - FIXED
                    lines.Add((new Point(x - 10 * scale, yOffset), new Point(x + 10 * scale, yOffset))); // Middle
                    lines.Add((new Point(x + 10 * scale, yOffset), new Point(x + 25 * scale, yOffset + 15 * scaleLowerCase))); // Lower curve
                    lines.Add((new Point(x + 25 * scale, yOffset + 15 * scaleLowerCase), new Point(x + 10 * scale, yOffset + 30 * scaleLowerCase))); // Bottom right
                    lines.Add((new Point(x + 10 * scale, yOffset + 30 * scaleLowerCase), new Point(x - 10 * scale, yOffset + 30 * scaleLowerCase))); // Bottom - FIXED
                    lines.Add((new Point(x - 10 * scale, yOffset + 30 * scaleLowerCase), new Point(x - 25 * scale, yOffset + 20 * scaleLowerCase))); // Bottom left
                }
                else
                {
                    // Uppercase 'S'
                    lines.Add((new Point(x + 40 * scale, y - 30 * scale), new Point(x + 20 * scale, y - 50 * scale))); // Top right
                    lines.Add((new Point(x + 20 * scale, y - 50 * scale), new Point(x - 20 * scale, y - 50 * scale))); // Top
                    lines.Add((new Point(x - 20 * scale, y - 50 * scale), new Point(x - 40 * scale, y - 30 * scale))); // Top left
                    lines.Add((new Point(x - 40 * scale, y - 30 * scale), new Point(x - 20 * scale, y - 10 * scale))); // Upper curve
                    lines.Add((new Point(x - 20 * scale, y - 10 * scale), new Point(x + 20 * scale, y + 10 * scale))); // Middle diagonal
                    lines.Add((new Point(x + 20 * scale, y + 10 * scale), new Point(x + 40 * scale, y + 30 * scale))); // Lower curve
                    lines.Add((new Point(x + 40 * scale, y + 30 * scale), new Point(x + 20 * scale, y + 50 * scale))); // Bottom right
                    lines.Add((new Point(x + 20 * scale, y + 50 * scale), new Point(x - 20 * scale, y + 50 * scale))); // Bottom
                    lines.Add((new Point(x - 20 * scale, y + 50 * scale), new Point(x - 40 * scale, y + 30 * scale))); // Bottom left
                }
                break;
            case 'T':
                if (isLowerCase)
                {
                    // Lowercase 't' - cross with hook at bottom
                    lines.Add((new Point(x - 10 * scale, y - 45 * scale), new Point(x - 10 * scale, yOffset + 20 * scaleLowerCase))); // Stem
                    lines.Add((new Point(x - 10 * scale, yOffset + 20 * scaleLowerCase), new Point(x, yOffset + 30 * scaleLowerCase))); // Hook curve
                    lines.Add((new Point(x, yOffset + 30 * scaleLowerCase), new Point(x + 10 * scale, yOffset + 30 * scaleLowerCase))); // Hook bottom
                    lines.Add((new Point(x - 30 * scale, yOffset - 20 * scaleLowerCase), new Point(x + 15 * scale, yOffset - 20 * scaleLowerCase))); // Crossbar
                }
                else
                {
                    // Uppercase 'T'
                    lines.Add((new Point(x - 50 * scale, y - 50 * scale), new Point(x + 50 * scale, y - 50 * scale))); // Top
                    lines.Add((new Point(x, y - 50 * scale), new Point(x, y + 50 * scale))); // Vertical
                }
                break;
            case 'U':
                if (isLowerCase)
                {
                    // Lowercase 'u' - curved bottom like 'n' inverted
                    lines.Add((new Point(x - 30 * scale, yOffset - 30 * scaleLowerCase), new Point(x - 30 * scale, yOffset + 10 * scaleLowerCase))); // Left stem
                    lines.Add((new Point(x - 30 * scale, yOffset + 10 * scaleLowerCase), new Point(x - 10 * scale, yOffset + 30 * scaleLowerCase))); // Bottom left curve
                    lines.Add((new Point(x - 10 * scale, yOffset + 30 * scaleLowerCase), new Point(x + 10 * scale, yOffset + 30 * scaleLowerCase))); // Bottom
                    lines.Add((new Point(x + 10 * scale, yOffset + 30 * scaleLowerCase), new Point(x + 30 * scale, yOffset + 10 * scaleLowerCase))); // Bottom right curve
                    lines.Add((new Point(x + 30 * scale, yOffset + 10 * scaleLowerCase), new Point(x + 30 * scale, yOffset + 30 * scaleLowerCase))); // Right stem short
                    lines.Add((new Point(x + 30 * scale, yOffset - 30 * scaleLowerCase), new Point(x + 30 * scale, yOffset + 30 * scaleLowerCase))); // Right stem full
                }
                else
                {
                    // Uppercase 'U'
                    lines.Add((new Point(x - 40 * scale, y - 50 * scale), new Point(x - 40 * scale, y + 30 * scale))); // Left vertical
                    lines.Add((new Point(x - 40 * scale, y + 30 * scale), new Point(x - 20 * scale, y + 50 * scale))); // Bottom left curve
                    lines.Add((new Point(x - 20 * scale, y + 50 * scale), new Point(x + 20 * scale, y + 50 * scale))); // Bottom
                    lines.Add((new Point(x + 20 * scale, y + 50 * scale), new Point(x + 40 * scale, y + 30 * scale))); // Bottom right curve
                    lines.Add((new Point(x + 40 * scale, y + 30 * scale), new Point(x + 40 * scale, y - 50 * scale))); // Right vertical
                }
                break;
            case 'V':
                if (isLowerCase)
                {
                    // Lowercase 'v' - smaller V
                    lines.Add((new Point(x - 30 * scale, yOffset - 30 * scaleLowerCase), new Point(x, yOffset + 30 * scaleLowerCase))); // Left diagonal
                    lines.Add((new Point(x, yOffset + 30 * scaleLowerCase), new Point(x + 30 * scale, yOffset - 30 * scaleLowerCase))); // Right diagonal
                }
                else
                {
                    // Uppercase 'V'
                    lines.Add((new Point(x - 50 * scale, y - 50 * scale), new Point(x, y + 50 * scale))); // Left diagonal
                    lines.Add((new Point(x, y + 50 * scale), new Point(x + 50 * scale, y - 50 * scale))); // Right diagonal
                }
                break;
            case 'W':
                if (isLowerCase)
                {
                    // Lowercase 'w' - smaller W
                    lines.Add((new Point(x - 40 * scale, yOffset - 30 * scaleLowerCase), new Point(x - 20 * scale, yOffset + 30 * scaleLowerCase))); // Left diagonal
                    lines.Add((new Point(x - 20 * scale, yOffset + 30 * scaleLowerCase), new Point(x, yOffset))); // Left middle
                    lines.Add((new Point(x, yOffset), new Point(x + 20 * scale, yOffset + 30 * scaleLowerCase))); // Right middle
                    lines.Add((new Point(x + 20 * scale, yOffset + 30 * scaleLowerCase), new Point(x + 40 * scale, yOffset - 30 * scaleLowerCase))); // Right diagonal
                }
                else
                {
                    // Uppercase 'W'
                    lines.Add((new Point(x - 50 * scale, y - 50 * scale), new Point(x - 30 * scale, y + 50 * scale))); // Left diagonal
                    lines.Add((new Point(x - 30 * scale, y + 50 * scale), new Point(x, y))); // Left middle
                    lines.Add((new Point(x, y), new Point(x + 30 * scale, y + 50 * scale))); // Right middle
                    lines.Add((new Point(x + 30 * scale, y + 50 * scale), new Point(x + 50 * scale, y - 50 * scale))); // Right diagonal
                }
                break;
            case 'X':
                if (isLowerCase)
                {
                    // Lowercase 'x' - smaller X
                    lines.Add((new Point(x - 30 * scale, yOffset - 30 * scaleLowerCase), new Point(x + 30 * scale, yOffset + 30 * scaleLowerCase))); // Left to right diagonal
                    lines.Add((new Point(x + 30 * scale, yOffset - 30 * scaleLowerCase), new Point(x - 30 * scale, yOffset + 30 * scaleLowerCase))); // Right to left diagonal
                }
                else
                {
                    // Uppercase 'X'
                    lines.Add((new Point(x - 40 * scale, y - 50 * scale), new Point(x + 40 * scale, y + 50 * scale))); // Left to right diagonal
                    lines.Add((new Point(x + 40 * scale, y - 50 * scale), new Point(x - 40 * scale, y + 50 * scale))); // Right to left diagonal
                }
                break;
            case 'Y':
                if (isLowerCase)
                {
                    // Lowercase 'y' - v-shape with descender
                    lines.Add((new Point(x - 30 * scale, yOffset - 30 * scaleLowerCase), new Point(x, yOffset + 10 * scaleLowerCase))); // Left diagonal to middle
                    lines.Add((new Point(x + 30 * scale, yOffset - 30 * scaleLowerCase), new Point(x, yOffset + 10 * scaleLowerCase))); // Right diagonal to middle
                    lines.Add((new Point(x, yOffset + 10 * scaleLowerCase), new Point(x - 10 * scale, y + 50 * scale))); // Descender with curve
                    lines.Add((new Point(x - 10 * scale, y + 50 * scale), new Point(x - 20 * scale, y + 50 * scale))); // Descender bottom
                }
                else
                {
                    // Uppercase 'Y'
                    lines.Add((new Point(x - 40 * scale, y - 50 * scale), new Point(x, y))); // Left diagonal
                    lines.Add((new Point(x + 40 * scale, y - 50 * scale), new Point(x, y))); // Right diagonal
                    lines.Add((new Point(x, y), new Point(x, y + 50 * scale))); // Vertical stem
                }
                break;
            case 'Z':
                if (isLowerCase)
                {
                    // Lowercase 'z' - smaller Z
                    lines.Add((new Point(x - 30 * scale, yOffset - 30 * scaleLowerCase), new Point(x + 30 * scale, yOffset - 30 * scaleLowerCase))); // Top
                    lines.Add((new Point(x + 30 * scale, yOffset - 30 * scaleLowerCase), new Point(x - 30 * scale, yOffset + 30 * scaleLowerCase))); // Diagonal
                    lines.Add((new Point(x - 30 * scale, yOffset + 30 * scaleLowerCase), new Point(x + 30 * scale, yOffset + 30 * scaleLowerCase))); // Bottom
                }
                else
                {
                    // Uppercase 'Z'
                    lines.Add((new Point(x - 40 * scale, y - 50 * scale), new Point(x + 40 * scale, y - 50 * scale))); // Top
                    lines.Add((new Point(x + 40 * scale, y - 50 * scale), new Point(x - 40 * scale, y + 50 * scale))); // Diagonal
                    lines.Add((new Point(x - 40 * scale, y + 50 * scale), new Point(x + 40 * scale, y + 50 * scale))); // Bottom
                }
                break;

            // Digits 0-9
            case '0':
                lines.Add((new Point(x - 20 * scale, y - 40 * scale), new Point(x + 20 * scale, y - 40 * scale))); // Top
                lines.Add((new Point(x + 20 * scale, y - 40 * scale), new Point(x + 40 * scale, y - 20 * scale))); // Top right
                lines.Add((new Point(x + 40 * scale, y - 20 * scale), new Point(x + 40 * scale, y + 20 * scale))); // Right
                lines.Add((new Point(x + 40 * scale, y + 20 * scale), new Point(x + 20 * scale, y + 40 * scale))); // Bottom right
                lines.Add((new Point(x + 20 * scale, y + 40 * scale), new Point(x - 20 * scale, y + 40 * scale))); // Bottom
                lines.Add((new Point(x - 20 * scale, y + 40 * scale), new Point(x - 40 * scale, y + 20 * scale))); // Bottom left
                lines.Add((new Point(x - 40 * scale, y + 20 * scale), new Point(x - 40 * scale, y - 20 * scale))); // Left
                lines.Add((new Point(x - 40 * scale, y - 20 * scale), new Point(x - 20 * scale, y - 40 * scale))); // Top left
                break;
            case '1':
                lines.Add((new Point(x - 20 * scale, y - 30 * scale), new Point(x, y - 50 * scale))); // Top diagonal
                lines.Add((new Point(x, y - 50 * scale), new Point(x, y + 50 * scale))); // Vertical
                lines.Add((new Point(x - 30 * scale, y + 50 * scale), new Point(x + 30 * scale, y + 50 * scale))); // Bottom
                break;
            case '2':
                lines.Add((new Point(x - 40 * scale, y - 30 * scale), new Point(x - 20 * scale, y - 50 * scale))); // Top left
                lines.Add((new Point(x - 20 * scale, y - 50 * scale), new Point(x + 20 * scale, y - 50 * scale))); // Top
                lines.Add((new Point(x + 20 * scale, y - 50 * scale), new Point(x + 40 * scale, y - 30 * scale))); // Top right
                lines.Add((new Point(x + 40 * scale, y - 30 * scale), new Point(x + 40 * scale, y - 10 * scale))); // Upper right
                lines.Add((new Point(x + 40 * scale, y - 10 * scale), new Point(x - 40 * scale, y + 50 * scale))); // Diagonal
                lines.Add((new Point(x - 40 * scale, y + 50 * scale), new Point(x + 40 * scale, y + 50 * scale))); // Bottom
                break;
            case '3':
                lines.Add((new Point(x - 30 * scale, y - 50 * scale), new Point(x + 30 * scale, y - 50 * scale))); // Top
                lines.Add((new Point(x + 30 * scale, y - 50 * scale), new Point(x + 40 * scale, y - 30 * scale))); // Top curve
                lines.Add((new Point(x + 40 * scale, y - 30 * scale), new Point(x + 20 * scale, y - 10 * scale))); // Upper curve
                lines.Add((new Point(x + 20 * scale, y - 10 * scale), new Point(x, y))); // Middle
                lines.Add((new Point(x, y), new Point(x + 20 * scale, y + 10 * scale))); // Middle lower
                lines.Add((new Point(x + 20 * scale, y + 10 * scale), new Point(x + 40 * scale, y + 25 * scale))); // Lower curve
                lines.Add((new Point(x + 40 * scale, y + 25 * scale), new Point(x + 30 * scale, y + 50 * scale))); // Bottom curve
                lines.Add((new Point(x + 30 * scale, y + 50 * scale), new Point(x - 30 * scale, y + 50 * scale))); // Bottom
                break;
            case '4':
                lines.Add((new Point(x + 20 * scale, y - 50 * scale), new Point(x + 20 * scale, y + 50 * scale))); // Vertical
                lines.Add((new Point(x + 20 * scale, y - 50 * scale), new Point(x - 40 * scale, y + 10 * scale))); // Diagonal
                lines.Add((new Point(x - 40 * scale, y + 10 * scale), new Point(x + 40 * scale, y + 10 * scale))); // Horizontal
                break;
            case '5':
                lines.Add((new Point(x + 40 * scale, y - 50 * scale), new Point(x - 40 * scale, y - 50 * scale))); // Top
                lines.Add((new Point(x - 40 * scale, y - 50 * scale), new Point(x - 40 * scale, y))); // Left upper
                lines.Add((new Point(x - 40 * scale, y), new Point(x + 20 * scale, y))); // Middle
                lines.Add((new Point(x + 20 * scale, y), new Point(x + 40 * scale, y + 20 * scale))); // Curve
                lines.Add((new Point(x + 40 * scale, y + 20 * scale), new Point(x + 40 * scale, y + 30 * scale))); // Lower right
                lines.Add((new Point(x + 40 * scale, y + 30 * scale), new Point(x + 20 * scale, y + 50 * scale))); // Bottom right
                lines.Add((new Point(x + 20 * scale, y + 50 * scale), new Point(x - 30 * scale, y + 50 * scale))); // Bottom
                lines.Add((new Point(x - 30 * scale, y + 50 * scale), new Point(x - 40 * scale, y + 40 * scale))); // Bottom left
                break;
            case '6':
                lines.Add((new Point(x + 30 * scale, y - 40 * scale), new Point(x + 10 * scale, y - 50 * scale))); // Top right
                lines.Add((new Point(x + 10 * scale, y - 50 * scale), new Point(x - 10 * scale, y - 50 * scale))); // Top
                lines.Add((new Point(x - 10 * scale, y - 50 * scale), new Point(x - 40 * scale, y - 20 * scale))); // Top left
                lines.Add((new Point(x - 40 * scale, y - 20 * scale), new Point(x - 40 * scale, y + 20 * scale))); // Left
                lines.Add((new Point(x - 40 * scale, y + 20 * scale), new Point(x - 20 * scale, y + 40 * scale))); // Bottom left
                lines.Add((new Point(x - 20 * scale, y + 40 * scale), new Point(x + 20 * scale, y + 40 * scale))); // Bottom
                lines.Add((new Point(x + 20 * scale, y + 40 * scale), new Point(x + 40 * scale, y + 20 * scale))); // Bottom right
                lines.Add((new Point(x + 40 * scale, y + 20 * scale), new Point(x + 40 * scale, y))); // Right
                lines.Add((new Point(x + 40 * scale, y), new Point(x + 20 * scale, y - 10 * scale))); // Upper curve
                lines.Add((new Point(x + 20 * scale, y - 10 * scale), new Point(x - 40 * scale, y - 10 * scale))); // Middle horizontal
                break;
            case '7':
                lines.Add((new Point(x - 40 * scale, y - 50 * scale), new Point(x + 40 * scale, y - 50 * scale))); // Top
                lines.Add((new Point(x + 40 * scale, y - 50 * scale), new Point(x - 10 * scale, y + 50 * scale))); // Diagonal
                break;
            case '8':
                lines.Add((new Point(x - 20 * scale, y - 50 * scale), new Point(x + 20 * scale, y - 50 * scale))); // Top
                lines.Add((new Point(x + 20 * scale, y - 50 * scale), new Point(x + 35 * scale, y - 35 * scale))); // Top right upper
                lines.Add((new Point(x + 35 * scale, y - 35 * scale), new Point(x + 30 * scale, y - 15 * scale))); // Right upper
                lines.Add((new Point(x + 30 * scale, y - 15 * scale), new Point(x, y))); // To center
                lines.Add((new Point(x, y), new Point(x + 35 * scale, y + 15 * scale))); // From center right
                lines.Add((new Point(x + 35 * scale, y + 15 * scale), new Point(x + 40 * scale, y + 30 * scale))); // Right lower
                lines.Add((new Point(x + 40 * scale, y + 30 * scale), new Point(x + 20 * scale, y + 50 * scale))); // Bottom right
                lines.Add((new Point(x + 20 * scale, y + 50 * scale), new Point(x - 20 * scale, y + 50 * scale))); // Bottom
                lines.Add((new Point(x - 20 * scale, y + 50 * scale), new Point(x - 40 * scale, y + 40 * scale))); // Bottom left
                lines.Add((new Point(x - 40 * scale, y + 40 * scale), new Point(x - 35 * scale, y + 15 * scale))); // Left lower
                lines.Add((new Point(x - 35 * scale, y + 15 * scale), new Point(x, y))); // To center left
                lines.Add((new Point(x, y), new Point(x - 30 * scale, y - 15 * scale))); // From center left
                lines.Add((new Point(x - 30 * scale, y - 15 * scale), new Point(x - 35 * scale, y - 35 * scale))); // Left upper
                lines.Add((new Point(x - 35 * scale, y - 35 * scale), new Point(x - 20 * scale, y - 50 * scale))); // Top left
                break;
            case '9':
                lines.Add((new Point(x + 40 * scale, y - 20 * scale), new Point(x + 40 * scale, y + 20 * scale))); // Right
                lines.Add((new Point(x + 40 * scale, y + 20 * scale), new Point(x + 10 * scale, y + 50 * scale))); // Bottom right
                lines.Add((new Point(x + 10 * scale, y + 50 * scale), new Point(x - 30 * scale, y + 40 * scale))); // Bottom
                lines.Add((new Point(x + 40 * scale, y - 20 * scale), new Point(x + 20 * scale, y - 40 * scale))); // Top right
                lines.Add((new Point(x + 20 * scale, y - 40 * scale), new Point(x - 20 * scale, y - 40 * scale))); // Top
                lines.Add((new Point(x - 20 * scale, y - 40 * scale), new Point(x - 40 * scale, y - 20 * scale))); // Top left
                lines.Add((new Point(x - 40 * scale, y - 20 * scale), new Point(x - 40 * scale, y))); // Left
                lines.Add((new Point(x - 40 * scale, y), new Point(x - 20 * scale, y + 10 * scale))); // Lower curve
                lines.Add((new Point(x - 20 * scale, y + 10 * scale), new Point(x + 40 * scale, y + 10 * scale))); // Middle horizontal
                break;

            // Punctuation and special characters
            case ' ': // Space - no lines, just empty
                break;
            case '.': // Period
                lines.Add((new Point(x - 5 * scale, y + 40 * scale), new Point(x + 5 * scale, y + 40 * scale))); // Top
                lines.Add((new Point(x + 5 * scale, y + 40 * scale), new Point(x + 5 * scale, y + 50 * scale))); // Right
                lines.Add((new Point(x + 5 * scale, y + 50 * scale), new Point(x - 5 * scale, y + 50 * scale))); // Bottom
                lines.Add((new Point(x - 5 * scale, y + 50 * scale), new Point(x - 5 * scale, y + 40 * scale))); // Left
                break;
            case ',': // Comma
                lines.Add((new Point(x, y + 35 * scale), new Point(x, y + 50 * scale))); // Stem
                lines.Add((new Point(x, y + 50 * scale), new Point(x - 5 * scale, y + 55 * scale))); // Tail
                break;
            case '!': // Exclamation mark
                lines.Add((new Point(x, y - 50 * scale), new Point(x, y + 20 * scale))); // Stem
                lines.Add((new Point(x - 5 * scale, y + 35 * scale), new Point(x + 5 * scale, y + 35 * scale))); // Dot top
                lines.Add((new Point(x + 5 * scale, y + 35 * scale), new Point(x + 5 * scale, y + 45 * scale))); // Dot right
                lines.Add((new Point(x + 5 * scale, y + 45 * scale), new Point(x - 5 * scale, y + 45 * scale))); // Dot bottom
                lines.Add((new Point(x - 5 * scale, y + 45 * scale), new Point(x - 5 * scale, y + 35 * scale))); // Dot left
                break;
            case '?': // Question mark
                lines.Add((new Point(x - 20 * scale, y - 40 * scale), new Point(x + 20 * scale, y - 40 * scale))); // Top
                lines.Add((new Point(x + 20 * scale, y - 40 * scale), new Point(x + 30 * scale, y - 20 * scale))); // Top right curve
                lines.Add((new Point(x + 30 * scale, y - 20 * scale), new Point(x + 10 * scale, y))); // Curve to center
                lines.Add((new Point(x + 10 * scale, y), new Point(x, y + 20 * scale))); // Stem
                lines.Add((new Point(x, y + 20 * scale), new Point(x, y + 35 * scale))); // Stem extension
                lines.Add((new Point(x - 5 * scale, y + 50 * scale), new Point(x + 5 * scale, y + 50 * scale))); // Dot top
                lines.Add((new Point(x + 5 * scale, y + 50 * scale), new Point(x + 5 * scale, y + 60 * scale))); // Dot right
                lines.Add((new Point(x + 5 * scale, y + 60 * scale), new Point(x - 5 * scale, y + 60 * scale))); // Dot bottom
                lines.Add((new Point(x - 5 * scale, y + 60 * scale), new Point(x - 5 * scale, y + 50 * scale))); // Dot left
                break;
            case ':': // Colon
                lines.Add((new Point(x - 5 * scale, y - 20 * scale), new Point(x + 5 * scale, y - 20 * scale))); // Top dot top
                lines.Add((new Point(x + 5 * scale, y - 20 * scale), new Point(x + 5 * scale, y - 10 * scale))); // Top dot right
                lines.Add((new Point(x + 5 * scale, y - 10 * scale), new Point(x - 5 * scale, y - 10 * scale))); // Top dot bottom
                lines.Add((new Point(x - 5 * scale, y - 10 * scale), new Point(x - 5 * scale, y - 20 * scale))); // Top dot left
                lines.Add((new Point(x - 5 * scale, y + 10 * scale), new Point(x + 5 * scale, y + 10 * scale))); // Bottom dot top
                lines.Add((new Point(x + 5 * scale, y + 10 * scale), new Point(x + 5 * scale, y + 20 * scale))); // Bottom dot right
                lines.Add((new Point(x + 5 * scale, y + 20 * scale), new Point(x - 5 * scale, y + 20 * scale))); // Bottom dot bottom
                lines.Add((new Point(x - 5 * scale, y + 20 * scale), new Point(x - 5 * scale, y + 10 * scale))); // Bottom dot left
                break;
            case ';': // Semicolon
                lines.Add((new Point(x - 5 * scale, y - 20 * scale), new Point(x + 5 * scale, y - 20 * scale))); // Top dot top
                lines.Add((new Point(x + 5 * scale, y - 20 * scale), new Point(x + 5 * scale, y - 10 * scale))); // Top dot right
                lines.Add((new Point(x + 5 * scale, y - 10 * scale), new Point(x - 5 * scale, y - 10 * scale))); // Top dot bottom
                lines.Add((new Point(x - 5 * scale, y - 10 * scale), new Point(x - 5 * scale, y - 20 * scale))); // Top dot left
                lines.Add((new Point(x, y + 15 * scale), new Point(x, y + 30 * scale))); // Comma stem
                lines.Add((new Point(x, y + 30 * scale), new Point(x - 5 * scale, y + 35 * scale))); // Comma tail
                break;
            case '-': // Hyphen/Minus
                lines.Add((new Point(x - 30 * scale, y), new Point(x + 30 * scale, y))); // Horizontal line
                break;
            case '_': // Underscore
                lines.Add((new Point(x - 30 * scale, y + 50 * scale), new Point(x + 30 * scale, y + 50 * scale))); // Bottom line
                break;
            case '\'': // Single quote/Apostrophe
                lines.Add((new Point(x, y - 50 * scale), new Point(x, y - 30 * scale))); // Stem
                break;
            case '"': // Double quote
                lines.Add((new Point(x - 10 * scale, y - 50 * scale), new Point(x - 10 * scale, y - 30 * scale))); // Left stem
                lines.Add((new Point(x + 10 * scale, y - 50 * scale), new Point(x + 10 * scale, y - 30 * scale))); // Right stem
                break;
            case '(': // Left parenthesis
                lines.Add((new Point(x + 20 * scale, y - 50 * scale), new Point(x - 10 * scale, y - 25 * scale))); // Top curve
                lines.Add((new Point(x - 10 * scale, y - 25 * scale), new Point(x - 20 * scale, y))); // Middle left
                lines.Add((new Point(x - 20 * scale, y), new Point(x - 10 * scale, y + 25 * scale))); // Bottom left
                lines.Add((new Point(x - 10 * scale, y + 25 * scale), new Point(x + 20 * scale, y + 50 * scale))); // Bottom curve
                break;
            case ')': // Right parenthesis
                lines.Add((new Point(x - 20 * scale, y - 50 * scale), new Point(x + 10 * scale, y - 25 * scale))); // Top curve
                lines.Add((new Point(x + 10 * scale, y - 25 * scale), new Point(x + 20 * scale, y))); // Middle right
                lines.Add((new Point(x + 20 * scale, y), new Point(x + 10 * scale, y + 25 * scale))); // Bottom right
                lines.Add((new Point(x + 10 * scale, y + 25 * scale), new Point(x - 20 * scale, y + 50 * scale))); // Bottom curve
                break;
            case '[': // Left square bracket
                lines.Add((new Point(x + 10 * scale, y - 50 * scale), new Point(x - 20 * scale, y - 50 * scale))); // Top
                lines.Add((new Point(x - 20 * scale, y - 50 * scale), new Point(x - 20 * scale, y + 50 * scale))); // Left side
                lines.Add((new Point(x - 20 * scale, y + 50 * scale), new Point(x + 10 * scale, y + 50 * scale))); // Bottom
                break;
            case ']': // Right square bracket
                lines.Add((new Point(x - 10 * scale, y - 50 * scale), new Point(x + 20 * scale, y - 50 * scale))); // Top
                lines.Add((new Point(x + 20 * scale, y - 50 * scale), new Point(x + 20 * scale, y + 50 * scale))); // Right side
                lines.Add((new Point(x + 20 * scale, y + 50 * scale), new Point(x - 10 * scale, y + 50 * scale))); // Bottom
                break;
            case '{': // Left curly brace
                lines.Add((new Point(x + 20 * scale, y - 50 * scale), new Point(x, y - 50 * scale))); // Top
                lines.Add((new Point(x, y - 50 * scale), new Point(x - 10 * scale, y - 35 * scale))); // Top curve
                lines.Add((new Point(x - 10 * scale, y - 35 * scale), new Point(x - 10 * scale, y - 10 * scale))); // Upper left
                lines.Add((new Point(x - 10 * scale, y - 10 * scale), new Point(x - 30 * scale, y))); // Middle point
                lines.Add((new Point(x - 30 * scale, y), new Point(x - 10 * scale, y + 10 * scale))); // Middle point
                lines.Add((new Point(x - 10 * scale, y + 10 * scale), new Point(x - 10 * scale, y + 35 * scale))); // Lower left
                lines.Add((new Point(x - 10 * scale, y + 35 * scale), new Point(x, y + 50 * scale))); // Bottom curve
                lines.Add((new Point(x, y + 50 * scale), new Point(x + 20 * scale, y + 50 * scale))); // Bottom
                break;
            case '}': // Right curly brace
                lines.Add((new Point(x - 20 * scale, y - 50 * scale), new Point(x, y - 50 * scale))); // Top
                lines.Add((new Point(x, y - 50 * scale), new Point(x + 10 * scale, y - 35 * scale))); // Top curve
                lines.Add((new Point(x + 10 * scale, y - 35 * scale), new Point(x + 10 * scale, y - 10 * scale))); // Upper right
                lines.Add((new Point(x + 10 * scale, y - 10 * scale), new Point(x + 30 * scale, y))); // Middle point
                lines.Add((new Point(x + 30 * scale, y), new Point(x + 10 * scale, y + 10 * scale))); // Middle point
                lines.Add((new Point(x + 10 * scale, y + 10 * scale), new Point(x + 10 * scale, y + 35 * scale))); // Lower right
                lines.Add((new Point(x + 10 * scale, y + 35 * scale), new Point(x, y + 50 * scale))); // Bottom curve
                lines.Add((new Point(x, y + 50 * scale), new Point(x - 20 * scale, y + 50 * scale))); // Bottom
                break;
            case '<': // Less than
                lines.Add((new Point(x + 30 * scale, y - 30 * scale), new Point(x - 30 * scale, y))); // Top to middle
                lines.Add((new Point(x - 30 * scale, y), new Point(x + 30 * scale, y + 30 * scale))); // Middle to bottom
                break;
            case '>': // Greater than
                lines.Add((new Point(x - 30 * scale, y - 30 * scale), new Point(x + 30 * scale, y))); // Top to middle
                lines.Add((new Point(x + 30 * scale, y), new Point(x - 30 * scale, y + 30 * scale))); // Middle to bottom
                break;
            case '+': // Plus
                lines.Add((new Point(x, y - 30 * scale), new Point(x, y + 30 * scale))); // Vertical
                lines.Add((new Point(x - 30 * scale, y), new Point(x + 30 * scale, y))); // Horizontal
                break;
            case '=': // Equals
                lines.Add((new Point(x - 30 * scale, y - 15 * scale), new Point(x + 30 * scale, y - 15 * scale))); // Top line
                lines.Add((new Point(x - 30 * scale, y + 15 * scale), new Point(x + 30 * scale, y + 15 * scale))); // Bottom line
                break;
            case '*': // Asterisk
                lines.Add((new Point(x, y - 30 * scale), new Point(x, y + 30 * scale))); // Vertical
                lines.Add((new Point(x - 25 * scale, y - 25 * scale), new Point(x + 25 * scale, y + 25 * scale))); // Diagonal \
                lines.Add((new Point(x + 25 * scale, y - 25 * scale), new Point(x - 25 * scale, y + 25 * scale))); // Diagonal /
                break;
            case '/': // Forward slash
                lines.Add((new Point(x - 20 * scale, y + 50 * scale), new Point(x + 20 * scale, y - 50 * scale))); // Diagonal
                break;
            case '\\': // Backslash
                lines.Add((new Point(x - 20 * scale, y - 50 * scale), new Point(x + 20 * scale, y + 50 * scale))); // Diagonal
                break;
            case '|': // Vertical bar/Pipe
                lines.Add((new Point(x, y - 50 * scale), new Point(x, y + 50 * scale))); // Vertical line
                break;
            case '&': // Ampersand (simplified)
                lines.Add((new Point(x + 20 * scale, y - 40 * scale), new Point(x - 20 * scale, y - 40 * scale))); // Top
                lines.Add((new Point(x - 20 * scale, y - 40 * scale), new Point(x - 35 * scale, y - 20 * scale))); // Top left
                lines.Add((new Point(x - 35 * scale, y - 20 * scale), new Point(x - 20 * scale, y))); // Mid left curve
                lines.Add((new Point(x - 20 * scale, y), new Point(x, y + 20 * scale))); // To bottom center
                lines.Add((new Point(x, y + 20 * scale), new Point(x - 20 * scale, y + 40 * scale))); // Bottom left
                lines.Add((new Point(x - 20 * scale, y + 40 * scale), new Point(x + 20 * scale, y + 40 * scale))); // Bottom
                lines.Add((new Point(x + 20 * scale, y + 40 * scale), new Point(x + 40 * scale, y + 20 * scale))); // Bottom right
                lines.Add((new Point(x, y), new Point(x + 40 * scale, y + 50 * scale))); // Tail
                break;
            case '@': // At sign (simplified)
                lines.Add((new Point(x + 20 * scale, y - 40 * scale), new Point(x - 20 * scale, y - 40 * scale))); // Top outer
                lines.Add((new Point(x - 20 * scale, y - 40 * scale), new Point(x - 40 * scale, y - 10 * scale))); // Left outer top
                lines.Add((new Point(x - 40 * scale, y - 10 * scale), new Point(x - 40 * scale, y + 10 * scale))); // Left outer
                lines.Add((new Point(x - 40 * scale, y + 10 * scale), new Point(x - 20 * scale, y + 40 * scale))); // Left outer bottom
                lines.Add((new Point(x - 20 * scale, y + 40 * scale), new Point(x + 20 * scale, y + 40 * scale))); // Bottom outer
                lines.Add((new Point(x + 20 * scale, y + 40 * scale), new Point(x + 40 * scale, y + 20 * scale))); // Right outer bottom
                lines.Add((new Point(x + 40 * scale, y + 20 * scale), new Point(x + 40 * scale, y - 30 * scale))); // Right outer
                lines.Add((new Point(x + 20 * scale, y), new Point(x + 20 * scale, y - 20 * scale))); // Inner vertical
                lines.Add((new Point(x + 20 * scale, y - 20 * scale), new Point(x, y - 20 * scale))); // Inner top
                lines.Add((new Point(x, y - 20 * scale), new Point(x - 15 * scale, y))); // Inner left
                lines.Add((new Point(x - 15 * scale, y), new Point(x, y + 20 * scale))); // Inner bottom left
                lines.Add((new Point(x, y + 20 * scale), new Point(x + 20 * scale, y + 20 * scale))); // Inner bottom
                break;
            case '#': // Hash/Number sign
                lines.Add((new Point(x - 15 * scale, y - 40 * scale), new Point(x - 25 * scale, y + 40 * scale))); // Left vertical
                lines.Add((new Point(x + 15 * scale, y - 40 * scale), new Point(x + 5 * scale, y + 40 * scale))); // Right vertical
                lines.Add((new Point(x - 40 * scale, y - 15 * scale), new Point(x + 40 * scale, y - 15 * scale))); // Top horizontal
                lines.Add((new Point(x - 40 * scale, y + 15 * scale), new Point(x + 40 * scale, y + 15 * scale))); // Bottom horizontal
                break;
            case '$': // Dollar sign (simplified S with lines)
                lines.Add((new Point(x, y - 55 * scale), new Point(x, y + 55 * scale))); // Vertical line through
                lines.Add((new Point(x + 30 * scale, y - 25 * scale), new Point(x + 15 * scale, y - 40 * scale))); // Top right
                lines.Add((new Point(x + 15 * scale, y - 40 * scale), new Point(x - 15 * scale, y - 40 * scale))); // Top
                lines.Add((new Point(x - 15 * scale, y - 40 * scale), new Point(x - 30 * scale, y - 25 * scale))); // Top left
                lines.Add((new Point(x - 30 * scale, y - 25 * scale), new Point(x - 15 * scale, y - 10 * scale))); // Upper curve
                lines.Add((new Point(x - 15 * scale, y - 10 * scale), new Point(x + 15 * scale, y + 10 * scale))); // Middle diagonal
                lines.Add((new Point(x + 15 * scale, y + 10 * scale), new Point(x + 30 * scale, y + 25 * scale))); // Lower curve
                lines.Add((new Point(x + 30 * scale, y + 25 * scale), new Point(x + 15 * scale, y + 40 * scale))); // Bottom right
                lines.Add((new Point(x + 15 * scale, y + 40 * scale), new Point(x - 15 * scale, y + 40 * scale))); // Bottom
                lines.Add((new Point(x - 15 * scale, y + 40 * scale), new Point(x - 30 * scale, y + 25 * scale))); // Bottom left
                break;
            case '%': // Percent
                lines.Add((new Point(x - 35 * scale, y - 40 * scale), new Point(x - 25 * scale, y - 40 * scale))); // Top left circle top
                lines.Add((new Point(x - 25 * scale, y - 40 * scale), new Point(x - 25 * scale, y - 30 * scale))); // Top left circle right
                lines.Add((new Point(x - 25 * scale, y - 30 * scale), new Point(x - 35 * scale, y - 30 * scale))); // Top left circle bottom
                lines.Add((new Point(x - 35 * scale, y - 30 * scale), new Point(x - 35 * scale, y - 40 * scale))); // Top left circle left
                lines.Add((new Point(x - 30 * scale, y + 50 * scale), new Point(x + 30 * scale, y - 50 * scale))); // Diagonal slash
                lines.Add((new Point(x + 25 * scale, y + 30 * scale), new Point(x + 35 * scale, y + 30 * scale))); // Bottom right circle top
                lines.Add((new Point(x + 35 * scale, y + 30 * scale), new Point(x + 35 * scale, y + 40 * scale))); // Bottom right circle right
                lines.Add((new Point(x + 35 * scale, y + 40 * scale), new Point(x + 25 * scale, y + 40 * scale))); // Bottom right circle bottom
                lines.Add((new Point(x + 25 * scale, y + 40 * scale), new Point(x + 25 * scale, y + 30 * scale))); // Bottom right circle left
                break;
            case '^': // Caret
                lines.Add((new Point(x - 30 * scale, y - 10 * scale), new Point(x, y - 40 * scale))); // Left to top
                lines.Add((new Point(x, y - 40 * scale), new Point(x + 30 * scale, y - 10 * scale))); // Top to right
                break;
            case '~': // Tilde
                lines.Add((new Point(x - 40 * scale, y - 10 * scale), new Point(x - 20 * scale, y - 25 * scale))); // Left low
                lines.Add((new Point(x - 20 * scale, y - 25 * scale), new Point(x, y - 25 * scale))); // Left high
                lines.Add((new Point(x, y - 25 * scale), new Point(x + 20 * scale, y - 10 * scale))); // Right high
                lines.Add((new Point(x + 20 * scale, y - 10 * scale), new Point(x + 40 * scale, y - 10 * scale))); // Right low
                break;
            case '`': // Backtick/Grave
                lines.Add((new Point(x - 10 * scale, y - 50 * scale), new Point(x + 10 * scale, y - 30 * scale))); // Diagonal
                break;
        }

        return lines;
    }
}