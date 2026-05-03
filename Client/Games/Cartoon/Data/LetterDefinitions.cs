using BlazorWasm.Games.Cartoon.Models;

namespace BlazorWasm.Games.Cartoon.Data;

/// <summary>
/// Data-driven letter definitions for vector rendering.
/// Each letter is defined as a series of line segments relative to a center point.
/// 
/// Coordinate system:
/// - (0, 0) is the center of the letter
/// - Positive X is right, positive Y is down
/// - Standard letter size is approximately 100x100 units (-50 to +50 in each direction)
/// - Lowercase letters are scaled down (0.6x height) and positioned lower
/// </summary>
public static class LetterDefinitions
{
    /// <summary>
    /// Get the line definitions for a character.
    /// Returns null if the character is not defined.
    /// </summary>
    public static LetterDef? GetDefinition(char letter)
    {
        char upperLetter = char.ToUpper(letter);
        bool isLowerCase = char.IsLower(letter);

        if (isLowerCase && _lowercaseLetters.TryGetValue(upperLetter, out var lowerDef))
            return lowerDef;

        if (_uppercaseLetters.TryGetValue(upperLetter, out var upperDef))
            return upperDef;

        if (_digits.TryGetValue(upperLetter, out var digitDef))
            return digitDef;

        if (_punctuation.TryGetValue(upperLetter, out var puncDef))
            return puncDef;

        return null;
    }

    #region Uppercase Letters (A-Z)

    private static readonly Dictionary<char, LetterDef> _uppercaseLetters = new()
    {
        // A - Triangle with crossbar
        ['A'] = new(new[]
        {
            L(P(-50,  50), P(  0, -50)),    // Left diagonal
            L(P(  0, -50), P( 50,  50)),    // Right diagonal
            L(P(-25,   0), P( 25,   0))     // Cross bar
        }),

        // B - Vertical with two bumps
        ['B'] = new(new[]
        {
            L(P(-40, -50), P(-40,  50)),    // Vertical
            L(P(-40, -50), P( 20, -50)),    // Top
            L(P( 20, -50), P( 40, -25)),    // Top curve
            L(P( 40, -25), P(-40,   0)),    // To middle
            L(P(-40,   0), P( 30,   0)),    // Middle
            L(P( 30,   0), P( 40,  25)),    // Bottom curve
            L(P( 40,  25), P(-40,  50))     // To bottom
        }),

        // C - Arc opening right
        ['C'] = new(new[]
        {
            L(P( 40, -40), P(-20, -40)),    // Top
            L(P(-20, -40), P(-40, -20)),    // Top curve
            L(P(-40, -20), P(-40,  20)),    // Left
            L(P(-40,  20), P(-20,  40)),    // Bottom curve
            L(P(-20,  40), P( 40,  40))     // Bottom
        }),

        // D - Vertical with right curve
        ['D'] = new(new[]
        {
            L(P(-40, -50), P(-40,  50)),    // Vertical
            L(P(-40, -50), P( 20, -50)),    // Top
            L(P( 20, -50), P( 40, -20)),    // Top curve
            L(P( 40, -20), P( 40,  20)),    // Right
            L(P( 40,  20), P( 20,  50)),    // Bottom curve
            L(P( 20,  50), P(-40,  50))     // Bottom
        }),

        // E - Vertical with three horizontals
        ['E'] = new(new[]
        {
            L(P(-40, -50), P(-40,  50)),    // Vertical
            L(P(-40, -50), P( 40, -50)),    // Top
            L(P(-40,   0), P( 30,   0)),    // Middle
            L(P(-40,  50), P( 40,  50))     // Bottom
        }),

        // F - Vertical with two horizontals
        ['F'] = new(new[]
        {
            L(P(-40, -50), P(-40,  50)),    // Vertical
            L(P(-40, -50), P( 40, -50)),    // Top
            L(P(-40,   0), P( 30,   0))     // Middle
        }),

        // G - C with inner bar
        ['G'] = new(new[]
        {
            L(P( 40, -40), P(-20, -40)),    // Top
            L(P(-20, -40), P(-40, -20)),    // Top curve
            L(P(-40, -20), P(-40,  20)),    // Left
            L(P(-40,  20), P(-20,  40)),    // Bottom curve
            L(P(-20,  40), P( 40,  40)),    // Bottom
            L(P( 40,  40), P( 40,   0)),    // Right
            L(P( 40,   0), P( 10,   0))     // Middle bar
        }),

        // H - Two verticals with crossbar
        ['H'] = new(new[]
        {
            L(P(-40, -50), P(-40,  50)),    // Left vertical
            L(P( 40, -50), P( 40,  50)),    // Right vertical
            L(P(-40,   0), P( 40,   0))     // Cross bar
        }),

        // I - Vertical with top and bottom bars
        ['I'] = new(new[]
        {
            L(P(  0, -50), P(  0,  50)),    // Vertical
            L(P(-30, -50), P( 30, -50)),    // Top
            L(P(-30,  50), P( 30,  50))     // Bottom
        }),

        // J - Hook shape
        ['J'] = new(new[]
        {
            L(P( 30, -50), P( 30,  30)),    // Vertical
            L(P( 30,  30), P( 10,  50)),    // Bottom curve
            L(P( 10,  50), P(-20,  50)),    // Bottom
            L(P(-20,  50), P(-40,  30))     // Bottom left curve
        }),

        // K - Vertical with two diagonals
        ['K'] = new(new[]
        {
            L(P(-40, -50), P(-40,  50)),    // Vertical
            L(P( 40, -50), P(-40,   0)),    // Top diagonal
            L(P(-40,   0), P( 40,  50))     // Bottom diagonal
        }),

        // L - Vertical with bottom bar
        ['L'] = new(new[]
        {
            L(P(-40, -50), P(-40,  50)),    // Vertical
            L(P(-40,  50), P( 40,  50))     // Bottom
        }),

        // M - Four segments forming M
        ['M'] = new(new[]
        {
            L(P(-50,  50), P(-50, -50)),    // Left vertical
            L(P(-50, -50), P(  0,   0)),    // Left diagonal
            L(P(  0,   0), P( 50, -50)),    // Right diagonal
            L(P( 50, -50), P( 50,  50))     // Right vertical
        }),

        // N - Two verticals with diagonal
        ['N'] = new(new[]
        {
            L(P(-40,  50), P(-40, -50)),    // Left vertical
            L(P(-40, -50), P( 40,  50)),    // Diagonal
            L(P( 40,  50), P( 40, -50))     // Right vertical
        }),

        // O - Octagon
        ['O'] = new(new[]
        {
            L(P(-20, -40), P( 20, -40)),    // Top
            L(P( 20, -40), P( 40, -20)),    // Top right
            L(P( 40, -20), P( 40,  20)),    // Right
            L(P( 40,  20), P( 20,  40)),    // Bottom right
            L(P( 20,  40), P(-20,  40)),    // Bottom
            L(P(-20,  40), P(-40,  20)),    // Bottom left
            L(P(-40,  20), P(-40, -20)),    // Left
            L(P(-40, -20), P(-20, -40))     // Top left
        }),

        // P - Vertical with top bump
        ['P'] = new(new[]
        {
            L(P(-40, -50), P(-40,  50)),    // Vertical
            L(P(-40, -50), P( 30, -50)),    // Top
            L(P( 30, -50), P( 40, -30)),    // Top curve
            L(P( 40, -30), P( 40, -10)),    // Right top
            L(P( 40, -10), P( 30,   0)),    // Curve to middle
            L(P( 30,   0), P(-40,   0))     // Middle
        }),

        // Q - O with tail
        ['Q'] = new(new[]
        {
            L(P(-20, -40), P( 20, -40)),    // Top
            L(P( 20, -40), P( 40, -20)),    // Top right
            L(P( 40, -20), P( 40,  20)),    // Right
            L(P( 40,  20), P( 20,  40)),    // Bottom right
            L(P( 20,  40), P(-20,  40)),    // Bottom
            L(P(-20,  40), P(-40,  20)),    // Bottom left
            L(P(-40,  20), P(-40, -20)),    // Left
            L(P(-40, -20), P(-20, -40)),    // Top left
            L(P( 10,  10), P( 50,  50))     // Tail
        }),

        // R - P with leg
        ['R'] = new(new[]
        {
            L(P(-40, -50), P(-40,  50)),    // Vertical
            L(P(-40, -50), P( 30, -50)),    // Top
            L(P( 30, -50), P( 40, -30)),    // Top curve
            L(P( 40, -30), P( 40, -10)),    // Right top
            L(P( 40, -10), P( 30,   0)),    // Curve to middle
            L(P( 30,   0), P(-40,   0)),    // Middle
            L(P(  0,   0), P( 40,  50))     // Leg
        }),

        // S - Snake shape
        ['S'] = new(new[]
        {
            L(P( 40, -30), P( 20, -50)),    // Top right
            L(P( 20, -50), P(-20, -50)),    // Top
            L(P(-20, -50), P(-40, -30)),    // Top left
            L(P(-40, -30), P(-20, -10)),    // Upper curve
            L(P(-20, -10), P( 20,  10)),    // Middle diagonal
            L(P( 20,  10), P( 40,  30)),    // Lower curve
            L(P( 40,  30), P( 20,  50)),    // Bottom right
            L(P( 20,  50), P(-20,  50)),    // Bottom
            L(P(-20,  50), P(-40,  30))     // Bottom left
        }),

        // T - Top bar with vertical
        ['T'] = new(new[]
        {
            L(P(-50, -50), P( 50, -50)),    // Top
            L(P(  0, -50), P(  0,  50))     // Vertical
        }),

        // U - Horseshoe
        ['U'] = new(new[]
        {
            L(P(-40, -50), P(-40,  30)),    // Left vertical
            L(P(-40,  30), P(-20,  50)),    // Bottom left curve
            L(P(-20,  50), P( 20,  50)),    // Bottom
            L(P( 20,  50), P( 40,  30)),    // Bottom right curve
            L(P( 40,  30), P( 40, -50))     // Right vertical
        }),

        // V - Two diagonals meeting at bottom
        ['V'] = new(new[]
        {
            L(P(-50, -50), P(  0,  50)),    // Left diagonal
            L(P(  0,  50), P( 50, -50))     // Right diagonal
        }),

        // W - Four diagonals forming W
        ['W'] = new(new[]
        {
            L(P(-50, -50), P(-30,  50)),    // Left diagonal
            L(P(-30,  50), P(  0,   0)),    // Left middle
            L(P(  0,   0), P( 30,  50)),    // Right middle
            L(P( 30,  50), P( 50, -50))     // Right diagonal
        }),

        // X - Two crossing diagonals
        ['X'] = new(new[]
        {
            L(P(-40, -50), P( 40,  50)),    // Left to right
            L(P( 40, -50), P(-40,  50))     // Right to left
        }),

        // Y - Two diagonals to center with stem
        ['Y'] = new(new[]
        {
            L(P(-40, -50), P(  0,   0)),    // Left diagonal
            L(P( 40, -50), P(  0,   0)),    // Right diagonal
            L(P(  0,   0), P(  0,  50))     // Vertical stem
        }),

        // Z - Zigzag
        ['Z'] = new(new[]
        {
            L(P(-40, -50), P( 40, -50)),    // Top
            L(P( 40, -50), P(-40,  50)),    // Diagonal
            L(P(-40,  50), P( 40,  50))     // Bottom
        })
    };

    #endregion

    #region Lowercase Letters

    private static readonly Dictionary<char, LetterDef> _lowercaseLetters = new()
    {
        // a - Small rounded letter
        ['A'] = new(new[]
        {
            L(P( 30, -20), P( 10, -30)),    // Top left
            L(P( 10, -30), P(-10, -30)),    // Top
            L(P(-10, -30), P(-30, -10)),    // Top left curve
            L(P(-30, -10), P(-30,  10)),    // Left
            L(P(-30,  10), P(-10,  30)),    // Bottom left
            L(P(-10,  30), P( 10,  30)),    // Bottom
            L(P( 10,  30), P( 30,  10)),    // Bottom right
            L(P( 30,  10), P( 30,  30))     // Tail down
        }),

        // b - Tall stem with bowl
        ['B'] = new(new[]
        {
            L(P(-30, -50), P(-30,  30)),    // Tall stem
            L(P(-30, -30), P( 10, -30)),    // Top of bowl
            L(P( 10, -30), P( 30, -10)),    // Top right curve
            L(P( 30, -10), P( 30,  10)),    // Right side
            L(P( 30,  10), P( 10,  30)),    // Bottom right curve
            L(P( 10,  30), P(-30,  30))     // Bottom
        }),

        // c - Small arc
        ['C'] = new(new[]
        {
            L(P( 30, -20), P(  0, -30)),    // Top
            L(P(  0, -30), P(-30,   0)),    // Top left curve
            L(P(-30,   0), P(  0,  30)),    // Bottom left curve
            L(P(  0,  30), P( 30,  20))     // Bottom
        }),

        // d - Bowl with tall stem
        ['D'] = new(new[]
        {
            L(P( 30, -50), P( 30,  30)),    // Tall stem
            L(P(-10, -30), P( 30, -30)),    // Top
            L(P(-10, -30), P(-30, -10)),    // Top left curve
            L(P(-30, -10), P(-30,  10)),    // Left
            L(P(-30,  10), P(-10,  30)),    // Bottom left curve
            L(P(-10,  30), P( 30,  30))     // Bottom
        }),

        // e - Rounded with bar
        ['E'] = new(new[]
        {
            L(P(-30,   0), P( 30,   0)),    // Horizontal bar
            L(P( 30,   0), P( 20, -30)),    // Top right
            L(P( 20, -30), P(-10, -30)),    // Top
            L(P(-10, -30), P(-30, -10)),    // Top left
            L(P(-30, -10), P(-30,  10)),    // Left
            L(P(-30,  10), P(-10,  30)),    // Bottom left
            L(P(-10,  30), P( 20,  30)),    // Bottom
            L(P( 20,  30), P( 30,  20))     // Bottom right
        }),

        // f - Hooked stem with crossbar
        ['F'] = new(new[]
        {
            L(P( 10, -50), P(-10, -50)),    // Top hook
            L(P(-10, -50), P(-20, -40)),    // Hook curve
            L(P(-20, -40), P(-20,  30)),    // Stem
            L(P(-35, -10), P(  5, -10))     // Crossbar
        }),

        // g - Bowl with descender
        ['G'] = new(new[]
        {
            L(P( 30, -30), P( 10, -30)),    // Top
            L(P( 10, -30), P(-10, -20)),    // Top left
            L(P(-10, -20), P(-30,   0)),    // Left top curve
            L(P(-30,   0), P(-10,  20)),    // Left bottom curve
            L(P(-10,  20), P( 10,  30)),    // Bottom
            L(P( 10,  30), P( 30,  10)),    // Bottom right curve
            L(P( 30,  10), P( 30,  40)),    // Descender down
            L(P( 30,  40), P( 10,  50)),    // Descender curve
            L(P( 10,  50), P(-10,  50))     // Descender bottom
        }),

        // h - Tall stem with hump
        ['H'] = new(new[]
        {
            L(P(-30, -50), P(-30,  30)),    // Tall stem
            L(P(-30, -20), P(  0, -30)),    // Top of hump
            L(P(  0, -30), P( 30, -10)),    // Curve
            L(P( 30, -10), P( 30,  30))     // Right stem
        }),

        // i - Stem with dot
        ['I'] = new(new[]
        {
            L(P(  0, -30), P(  0,  30)),    // Stem
            L(P( -5, -45), P(  5, -45)),    // Dot top
            L(P(  5, -45), P(  5, -55)),    // Dot right
            L(P(  5, -55), P( -5, -55)),    // Dot bottom
            L(P( -5, -55), P( -5, -45))     // Dot left
        }),

        // j - Stem with descender and dot
        ['J'] = new(new[]
        {
            L(P(  0, -30), P(  0,  40)),    // Stem with descender
            L(P(  0,  40), P(-10,  50)),    // Descender curve
            L(P(-10,  50), P(-20,  50)),    // Descender bottom
            L(P( -5, -45), P(  5, -45)),    // Dot top
            L(P(  5, -45), P(  5, -55)),    // Dot right
            L(P(  5, -55), P( -5, -55)),    // Dot bottom
            L(P( -5, -55), P( -5, -45))     // Dot left
        }),

        // k - Tall stem with two diagonals
        ['K'] = new(new[]
        {
            L(P(-30, -50), P(-30,  30)),    // Tall stem
            L(P( 30, -30), P(-30,   0)),    // Top diagonal to middle
            L(P(-30,   0), P( 30,  30))     // Bottom diagonal from middle
        }),

        // l - Simple tall stem
        ['L'] = new(new[]
        {
            L(P(  0, -50), P(  0,  30))     // Tall stem
        }),

        // m - Two humps (corrected to have 3 stems, not 4)
        ['M'] = new(new[]
        {
            L(P(-40,  30), P(-40, -30)),    // Left stem
            L(P(-40, -20), P(-20, -30)),    // First hump top
            L(P(-20, -30), P(-10, -20)),    // First hump curve
            L(P(-10, -20), P(-10,  30)),    // First middle stem (down from hump)
            L(P(-10, -20), P( 10, -30)),    // Second hump top
            L(P( 10, -30), P( 20, -20)),    // Second hump curve
            L(P( 20, -20), P( 20,  30))     // Right stem (down from hump - removed duplicate)
        }),

        // n - One hump
        ['N'] = new(new[]
        {
            L(P(-30, -30), P(-30,  30)),    // Left stem
            L(P(-30, -20), P(  0, -30)),    // Top of hump
            L(P(  0, -30), P( 30, -10)),    // Curve
            L(P( 30, -10), P( 30,  30))     // Right stem
        }),

        // o - Small oval
        ['O'] = new(new[]
        {
            L(P(-10, -30), P( 10, -30)),    // Top
            L(P( 10, -30), P( 30, -10)),    // Top right
            L(P( 30, -10), P( 30,  10)),    // Right
            L(P( 30,  10), P( 10,  30)),    // Bottom right
            L(P( 10,  30), P(-10,  30)),    // Bottom
            L(P(-10,  30), P(-30,  10)),    // Bottom left
            L(P(-30,  10), P(-30, -10)),    // Left
            L(P(-30, -10), P(-10, -30))     // Top left
        }),

        // p - Bowl with descender
        ['P'] = new(new[]
        {
            L(P(-30, -30), P(-30,  50)),    // Stem with descender
            L(P(-30, -30), P( 10, -30)),    // Top of bowl
            L(P( 10, -30), P( 30, -10)),    // Top right curve
            L(P( 30, -10), P( 30,  10)),    // Right side
            L(P( 30,  10), P( 10,  30)),    // Bottom right curve
            L(P( 10,  30), P(-30,  30))     // Bottom
        }),

        // q - Bowl with right descender
        ['Q'] = new(new[]
        {
            L(P( 30, -30), P( 30,  50)),    // Right stem with descender
            L(P(-10, -30), P( 30, -30)),    // Top
            L(P(-10, -30), P(-30, -10)),    // Top left curve
            L(P(-30, -10), P(-30,  10)),    // Left
            L(P(-30,  10), P(-10,  30)),    // Bottom left curve
            L(P(-10,  30), P( 30,  30))     // Bottom
        }),

        // r - Short stem with shoulder
        ['R'] = new(new[]
        {
            L(P(-30, -30), P(-30,  30)),    // Stem
            L(P(-30, -20), P(-10, -30)),    // Shoulder top
            L(P(-10, -30), P( 10, -25)),    // Shoulder curve
            L(P( 10, -25), P( 20, -20))     // Shoulder end
        }),

        // s - Small snake
        ['S'] = new(new[]
        {
            L(P( 25, -20), P( 10, -30)),    // Top right
            L(P( 10, -30), P(-10, -30)),    // Top
            L(P(-10, -30), P(-25, -15)),    // Top left
            L(P(-25, -15), P(-10,   0)),    // Upper curve
            L(P(-10,   0), P( 10,   0)),    // Middle
            L(P( 10,   0), P( 25,  15)),    // Lower curve
            L(P( 25,  15), P( 10,  30)),    // Bottom right
            L(P( 10,  30), P(-10,  30)),    // Bottom
            L(P(-10,  30), P(-25,  20))     // Bottom left
        }),

        // t - Hooked stem with crossbar
        ['T'] = new(new[]
        {
            L(P(-10, -45), P(-10,  20)),    // Stem
            L(P(-10,  20), P(  0,  30)),    // Hook curve
            L(P(  0,  30), P( 10,  30)),    // Hook bottom
            L(P(-30, -20), P( 15, -20))     // Crossbar
        }),

        // u - Horseshoe with stems
        ['U'] = new(new[]
        {
            L(P(-30, -30), P(-30,  10)),    // Left stem
            L(P(-30,  10), P(-10,  30)),    // Bottom left curve
            L(P(-10,  30), P( 10,  30)),    // Bottom
            L(P( 10,  30), P( 30,  10)),    // Bottom right curve
            L(P( 30,  10), P( 30,  30)),    // Right stem short
            L(P( 30, -30), P( 30,  30))     // Right stem full
        }),

        // v - Two diagonals
        ['V'] = new(new[]
        {
            L(P(-30, -30), P(  0,  30)),    // Left diagonal
            L(P(  0,  30), P( 30, -30))     // Right diagonal
        }),

        // w - Four diagonals
        ['W'] = new(new[]
        {
            L(P(-40, -30), P(-20,  30)),    // Left diagonal
            L(P(-20,  30), P(  0,   0)),    // Left middle
            L(P(  0,   0), P( 20,  30)),    // Right middle
            L(P( 20,  30), P( 40, -30))     // Right diagonal
        }),

        // x - Crossing diagonals
        ['X'] = new(new[]
        {
            L(P(-30, -30), P( 30,  30)),    // Left to right diagonal
            L(P( 30, -30), P(-30,  30))     // Right to left diagonal
        }),

        // y - V with descender
        ['Y'] = new(new[]
        {
            L(P(-30, -30), P(  0,  10)),    // Left diagonal to middle
            L(P( 30, -30), P(  0,  10)),    // Right diagonal to middle
            L(P(  0,  10), P(-10,  50)),    // Descender with curve
            L(P(-10,  50), P(-20,  50))     // Descender bottom
        }),

        // z - Small zigzag
        ['Z'] = new(new[]
        {
            L(P(-30, -30), P( 30, -30)),    // Top
            L(P( 30, -30), P(-30,  30)),    // Diagonal
            L(P(-30,  30), P( 30,  30))     // Bottom
        })
    };

    #endregion

    #region Digits (0-9)

    private static readonly Dictionary<char, LetterDef> _digits = new()
    {
        // 0 - Oval shape
        ['0'] = new(new[]
        {
            L(P(-20, -40), P( 20, -40)),    // Top
            L(P( 20, -40), P( 40, -20)),    // Top right
            L(P( 40, -20), P( 40,  20)),    // Right
            L(P( 40,  20), P( 20,  40)),    // Bottom right
            L(P( 20,  40), P(-20,  40)),    // Bottom
            L(P(-20,  40), P(-40,  20)),    // Bottom left
            L(P(-40,  20), P(-40, -20)),    // Left
            L(P(-40, -20), P(-20, -40))     // Top left
        }),

        // 1 - Vertical with diagonal top
        ['1'] = new(new[]
        {
            L(P(-20, -30), P(  0, -50)),    // Top diagonal
            L(P(  0, -50), P(  0,  50)),    // Vertical
            L(P(-30,  50), P( 30,  50))     // Bottom
        }),

        // 2 - Top arc with diagonal bottom
        ['2'] = new(new[]
        {
            L(P(-40, -30), P(-20, -50)),    // Top left
            L(P(-20, -50), P( 20, -50)),    // Top
            L(P( 20, -50), P( 40, -30)),    // Top right
            L(P( 40, -30), P( 40, -10)),    // Upper right
            L(P( 40, -10), P(-40,  50)),    // Diagonal
            L(P(-40,  50), P( 40,  50))     // Bottom
        }),

        // 3 - Two bumps on right
        ['3'] = new(new[]
        {
            L(P(-30, -50), P( 30, -50)),    // Top
            L(P( 30, -50), P( 40, -30)),    // Top curve
            L(P( 40, -30), P( 20, -10)),    // Upper curve
            L(P( 20, -10), P(  0,   0)),    // Middle
            L(P(  0,   0), P( 20,  10)),    // Middle lower
            L(P( 20,  10), P( 40,  25)),    // Lower curve
            L(P( 40,  25), P( 30,  50)),    // Bottom curve
            L(P( 30,  50), P(-30,  50))     // Bottom
        }),

        // 4 - Angled top with vertical
        ['4'] = new(new[]
        {
            L(P( 20, -50), P( 20,  50)),    // Vertical
            L(P( 20, -50), P(-40,  10)),    // Diagonal
            L(P(-40,  10), P( 40,  10))     // Horizontal
        }),

        // 5 - Rotated S shape
        ['5'] = new(new[]
        {
            L(P( 40, -50), P(-40, -50)),    // Top
            L(P(-40, -50), P(-40,   0)),    // Left upper
            L(P(-40,   0), P( 20,   0)),    // Middle
            L(P( 20,   0), P( 40,  20)),    // Curve
            L(P( 40,  20), P( 40,  30)),    // Lower right
            L(P( 40,  30), P( 20,  50)),    // Bottom right
            L(P( 20,  50), P(-30,  50)),    // Bottom
            L(P(-30,  50), P(-40,  40))     // Bottom left
        }),

        // 6 - Loop at bottom
        ['6'] = new(new[]
        {
            L(P( 30, -40), P( 10, -50)),    // Top right
            L(P( 10, -50), P(-10, -50)),    // Top
            L(P(-10, -50), P(-40, -20)),    // Top left
            L(P(-40, -20), P(-40,  20)),    // Left
            L(P(-40,  20), P(-20,  40)),    // Bottom left
            L(P(-20,  40), P( 20,  40)),    // Bottom
            L(P( 20,  40), P( 40,  20)),    // Bottom right
            L(P( 40,  20), P( 40,   0)),    // Right
            L(P( 40,   0), P( 20, -10)),    // Upper curve
            L(P( 20, -10), P(-40, -10))     // Middle horizontal
        }),

        // 7 - Top bar with diagonal
        ['7'] = new(new[]
        {
            L(P(-40, -50), P( 40, -50)),    // Top
            L(P( 40, -50), P(-10,  50))     // Diagonal
        }),

        // 8 - Two loops stacked
        ['8'] = new(new[]
        {
            L(P(-20, -50), P( 20, -50)),    // Top
            L(P( 20, -50), P( 35, -35)),    // Top right upper
            L(P( 35, -35), P( 30, -15)),    // Right upper
            L(P( 30, -15), P(  0,   0)),    // To center
            L(P(  0,   0), P( 35,  15)),    // From center right
            L(P( 35,  15), P( 40,  30)),    // Right lower
            L(P( 40,  30), P( 20,  50)),    // Bottom right
            L(P( 20,  50), P(-20,  50)),    // Bottom
            L(P(-20,  50), P(-40,  40)),    // Bottom left
            L(P(-40,  40), P(-35,  15)),    // Left lower
            L(P(-35,  15), P(  0,   0)),    // To center left
            L(P(  0,   0), P(-30, -15)),    // From center left
            L(P(-30, -15), P(-35, -35)),    // Left upper
            L(P(-35, -35), P(-20, -50))     // Top left
        }),

        // 9 - Loop at top
        ['9'] = new(new[]
        {
            L(P( 40, -20), P( 40,  20)),    // Right
            L(P( 40,  20), P( 10,  50)),    // Bottom right
            L(P( 10,  50), P(-30,  40)),    // Bottom
            L(P( 40, -20), P( 20, -40)),    // Top right
            L(P( 20, -40), P(-20, -40)),    // Top
            L(P(-20, -40), P(-40, -20)),    // Top left
            L(P(-40, -20), P(-40,   0)),    // Left
            L(P(-40,   0), P(-20,  10)),    // Lower curve
            L(P(-20,  10), P( 40,  10))     // Middle horizontal
        })
    };

    #endregion

    #region Punctuation and Special Characters

    private static readonly Dictionary<char, LetterDef> _punctuation = new()
    {
        // Space - no lines
        [' '] = new(Array.Empty<LineSegment>()),

        // . - Period (small square dot)
        ['.'] = new(new[]
        {
            L(P( -5,  40), P(  5,  40)),    // Top
            L(P(  5,  40), P(  5,  50)),    // Right
            L(P(  5,  50), P( -5,  50)),    // Bottom
            L(P( -5,  50), P( -5,  40))     // Left
        }),

        // , - Comma (dot with tail)
        [','] = new(new[]
        {
            L(P(  0,  35), P(  0,  50)),    // Stem
            L(P(  0,  50), P( -5,  55))     // Tail
        }),

        // ! - Exclamation mark
        ['!'] = new(new[]
        {
            L(P(  0, -50), P(  0,  20)),    // Stem
            L(P( -5,  35), P(  5,  35)),    // Dot top
            L(P(  5,  35), P(  5,  45)),    // Dot right
            L(P(  5,  45), P( -5,  45)),    // Dot bottom
            L(P( -5,  45), P( -5,  35))     // Dot left
        }),

        // ? - Question mark
        ['?'] = new(new[]
        {
            L(P(-20, -40), P( 20, -40)),    // Top
            L(P( 20, -40), P( 30, -20)),    // Top right curve
            L(P( 30, -20), P( 10,   0)),    // Curve to center
            L(P( 10,   0), P(  0,  20)),    // Stem
            L(P(  0,  20), P(  0,  35)),    // Stem extension
            L(P( -5,  50), P(  5,  50)),    // Dot top
            L(P(  5,  50), P(  5,  60)),    // Dot right
            L(P(  5,  60), P( -5,  60)),    // Dot bottom
            L(P( -5,  60), P( -5,  50))     // Dot left
        }),

        // : - Colon (two dots)
        [':'] = new(new[]
        {
            L(P( -5, -20), P(  5, -20)),    // Top dot top
            L(P(  5, -20), P(  5, -10)),    // Top dot right
            L(P(  5, -10), P( -5, -10)),    // Top dot bottom
            L(P( -5, -10), P( -5, -20)),    // Top dot left
            L(P( -5,  10), P(  5,  10)),    // Bottom dot top
            L(P(  5,  10), P(  5,  20)),    // Bottom dot right
            L(P(  5,  20), P( -5,  20)),    // Bottom dot bottom
            L(P( -5,  20), P( -5,  10))     // Bottom dot left
        }),

        // ; - Semicolon (dot with comma)
        [';'] = new(new[]
        {
            L(P( -5, -20), P(  5, -20)),    // Top dot top
            L(P(  5, -20), P(  5, -10)),    // Top dot right
            L(P(  5, -10), P( -5, -10)),    // Top dot bottom
            L(P( -5, -10), P( -5, -20)),    // Top dot left
            L(P(  0,  15), P(  0,  30)),    // Comma stem
            L(P(  0,  30), P( -5,  35))     // Comma tail
        }),

        // - - Hyphen/minus
        ['-'] = new(new[]
        {
            L(P(-30,   0), P( 30,   0))     // Horizontal line
        }),

        // _ - Underscore
        ['_'] = new(new[]
        {
            L(P(-30,  50), P( 30,  50))     // Bottom line
        }),

        // ' - Single quote
        ['\''] = new(new[]
        {
            L(P(  0, -50), P(  0, -30))     // Stem
        }),

        // " - Double quote
        ['"'] = new(new[]
        {
            L(P(-10, -50), P(-10, -30)),    // Left stem
            L(P( 10, -50), P( 10, -30))     // Right stem
        }),

        // ( - Left parenthesis
        ['('] = new(new[]
        {
            L(P( 20, -50), P(-10, -25)),    // Top curve
            L(P(-10, -25), P(-20,   0)),    // Middle left
            L(P(-20,   0), P(-10,  25)),    // Bottom left
            L(P(-10,  25), P( 20,  50))     // Bottom curve
        }),

        // ) - Right parenthesis
        [')'] = new(new[]
        {
            L(P(-20, -50), P( 10, -25)),    // Top curve
            L(P( 10, -25), P( 20,   0)),    // Middle right
            L(P( 20,   0), P( 10,  25)),    // Bottom right
            L(P( 10,  25), P(-20,  50))     // Bottom curve
        }),

        // [ - Left bracket
        ['['] = new(new[]
        {
            L(P( 10, -50), P(-20, -50)),    // Top
            L(P(-20, -50), P(-20,  50)),    // Left side
            L(P(-20,  50), P( 10,  50))     // Bottom
        }),

        // ] - Right bracket
        [']'] = new(new[]
        {
            L(P(-10, -50), P( 20, -50)),    // Top
            L(P( 20, -50), P( 20,  50)),    // Right side
            L(P( 20,  50), P(-10,  50))     // Bottom
        }),

        // { - Left brace
        ['{'] = new(new[]
        {
            L(P( 20, -50), P(  0, -50)),    // Top
            L(P(  0, -50), P(-10, -35)),    // Top curve
            L(P(-10, -35), P(-10, -10)),    // Upper left
            L(P(-10, -10), P(-30,   0)),    // Middle point
            L(P(-30,   0), P(-10,  10)),    // Middle point
            L(P(-10,  10), P(-10,  35)),    // Lower left
            L(P(-10,  35), P(  0,  50)),    // Bottom curve
            L(P(  0,  50), P( 20,  50))     // Bottom
        }),

        // } - Right brace
        ['}'] = new(new[]
        {
            L(P(-20, -50), P(  0, -50)),    // Top
            L(P(  0, -50), P( 10, -35)),    // Top curve
            L(P( 10, -35), P( 10, -10)),    // Upper right
            L(P( 10, -10), P( 30,   0)),    // Middle point
            L(P( 30,   0), P( 10,  10)),    // Middle point
            L(P( 10,  10), P( 10,  35)),    // Lower right
            L(P( 10,  35), P(  0,  50)),    // Bottom curve
            L(P(  0,  50), P(-20,  50))     // Bottom
        }),

        // < - Less than
        ['<'] = new(new[]
        {
            L(P( 30, -30), P(-30,   0)),    // Top to middle
            L(P(-30,   0), P( 30,  30))     // Middle to bottom
        }),

        // > - Greater than
        ['>'] = new(new[]
        {
            L(P(-30, -30), P( 30,   0)),    // Top to middle
            L(P( 30,   0), P(-30,  30))     // Middle to bottom
        }),

        // + - Plus
        ['+'] = new(new[]
        {
            L(P(  0, -30), P(  0,  30)),    // Vertical
            L(P(-30,   0), P( 30,   0))     // Horizontal
        }),

        // = - Equals
        ['='] = new(new[]
        {
            L(P(-30, -15), P( 30, -15)),    // Top line
            L(P(-30,  15), P( 30,  15))     // Bottom line
        }),

        // * - Asterisk
        ['*'] = new(new[]
        {
            L(P(  0, -30), P(  0,  30)),    // Vertical
            L(P(-25, -25), P( 25,  25)),    // Diagonal \
            L(P( 25, -25), P(-25,  25))     // Diagonal /
        }),

        // / - Forward slash
        ['/'] = new(new[]
        {
            L(P(-20,  50), P( 20, -50))     // Diagonal
        }),

        // \ - Backslash
        ['\\'] = new(new[]
        {
            L(P(-20, -50), P( 20,  50))     // Diagonal
        }),

        // | - Vertical bar
        ['|'] = new(new[]
        {
            L(P(  0, -50), P(  0,  50))     // Vertical line
        }),

        // & - Ampersand
        ['&'] = new(new[]
        {
            L(P( 20, -40), P(-20, -40)),    // Top
            L(P(-20, -40), P(-35, -20)),    // Top left
            L(P(-35, -20), P(-20,   0)),    // Mid left curve
            L(P(-20,   0), P(  0,  20)),    // To bottom center
            L(P(  0,  20), P(-20,  40)),    // Bottom left
            L(P(-20,  40), P( 20,  40)),    // Bottom
            L(P( 20,  40), P( 40,  20)),    // Bottom right
            L(P(  0,   0), P( 40,  50))     // Tail
        }),

        // @ - At sign
        ['@'] = new(new[]
        {
            L(P( 20, -40), P(-20, -40)),    // Top outer
            L(P(-20, -40), P(-40, -10)),    // Left outer top
            L(P(-40, -10), P(-40,  10)),    // Left outer
            L(P(-40,  10), P(-20,  40)),    // Left outer bottom
            L(P(-20,  40), P( 20,  40)),    // Bottom outer
            L(P( 20,  40), P( 40,  20)),    // Right outer bottom
            L(P( 40,  20), P( 40, -30)),    // Right outer
            L(P( 20,   0), P( 20, -20)),    // Inner vertical
            L(P( 20, -20), P(  0, -20)),    // Inner top
            L(P(  0, -20), P(-15,   0)),    // Inner left
            L(P(-15,   0), P(  0,  20)),    // Inner bottom left
            L(P(  0,  20), P( 20,  20))     // Inner bottom
        }),

        // # - Hash/pound
        ['#'] = new(new[]
        {
            L(P(-15, -40), P(-25,  40)),    // Left vertical
            L(P( 15, -40), P(  5,  40)),    // Right vertical
            L(P(-40, -15), P( 40, -15)),    // Top horizontal
            L(P(-40,  15), P( 40,  15))     // Bottom horizontal
        }),

        // $ - Dollar sign
        ['$'] = new(new[]
        {
            L(P(  0, -55), P(  0,  55)),    // Vertical line through
            L(P( 30, -25), P( 15, -40)),    // Top right
            L(P( 15, -40), P(-15, -40)),    // Top
            L(P(-15, -40), P(-30, -25)),    // Top left
            L(P(-30, -25), P(-15, -10)),    // Upper curve
            L(P(-15, -10), P( 15,  10)),    // Middle diagonal
            L(P( 15,  10), P( 30,  25)),    // Lower curve
            L(P( 30,  25), P( 15,  40)),    // Bottom right
            L(P( 15,  40), P(-15,  40)),    // Bottom
            L(P(-15,  40), P(-30,  25))     // Bottom left
        }),

        // % - Percent
        ['%'] = new(new[]
        {
            L(P(-35, -40), P(-25, -40)),    // Top left circle top
            L(P(-25, -40), P(-25, -30)),    // Top left circle right
            L(P(-25, -30), P(-35, -30)),    // Top left circle bottom
            L(P(-35, -30), P(-35, -40)),    // Top left circle left
            L(P(-30,  50), P( 30, -50)),    // Diagonal slash
            L(P( 25,  30), P( 35,  30)),    // Bottom right circle top
            L(P( 35,  30), P( 35,  40)),    // Bottom right circle right
            L(P( 35,  40), P( 25,  40)),    // Bottom right circle bottom
            L(P( 25,  40), P( 25,  30))     // Bottom right circle left
        }),

        // ^ - Caret
        ['^'] = new(new[]
        {
            L(P(-30, -10), P(  0, -40)),    // Left to top
            L(P(  0, -40), P( 30, -10))     // Top to right
        }),

        // ~ - Tilde
        ['~'] = new(new[]
        {
            L(P(-40, -10), P(-20, -25)),    // Left low
            L(P(-20, -25), P(  0, -25)),    // Left high
            L(P(  0, -25), P( 20, -10)),    // Right high
            L(P( 20, -10), P( 40, -10))     // Right low
        }),

        // ` - Backtick
        ['`'] = new(new[]
        {
            L(P(-10, -50), P( 10, -30))     // Diagonal
        })
    };

    #endregion

    // Helper methods to create points and line segments
    private static Point P(double x, double y) => new() { X = x, Y = y };

    private static LineSegment L(Point start, Point end) => new()
    {
        X1 = start.X,
        Y1 = start.Y,
        X2 = end.X,
        Y2 = end.Y
    };
}

/// <summary>
/// Represents a letter's vector definition
/// </summary>
public record LetterDef(LineSegment[] Lines);

/// <summary>
/// Represents a line segment in a letter's vector definition
/// </summary>
public record LineSegment
{
    public double X1 { get; init; }
    public double Y1 { get; init; }
    public double X2 { get; init; }
    public double Y2 { get; init; }
}