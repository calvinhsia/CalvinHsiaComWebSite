using Microsoft.JSInterop;
using System.Text.RegularExpressions;
using WordScapeBlazorWasm.Models;

namespace WordScapeBlazorWasm.Services
{
    public class LogoGameService
    {
        private readonly IJSRuntime _jsRuntime;
        private bool _debugMode; // Changed from readonly to allow updates

        public LogoGameService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
            _debugMode = false; // Default to false, can be enabled via UI
        }

        private void LogDebug(string message)
        {
            if (_debugMode)
            {
                Console.WriteLine(message);
            }
        }

        public LogoGameState CreateNewGame()
        {
            return new LogoGameState
            {
                Turtle = new LogoTurtle
                {
                    X = 250,
                    Y = 250,
                    Heading = 0,
                    PenDown = true,
                    PenColor = "#000000",
                    PenWidth = 1,
                    IsVisible = true
                },
                Canvas = new LogoCanvas
                {
                    Width = 500,
                    Height = 500,
                    BackgroundColor = "#FFFFFF"
                },
                CommandHistory = new List<LogoCommand>(),
                DrawingElements = new List<LogoDrawingElement>(),
                Variables = new Dictionary<string, double>(),
                CurrentCode = "",
                IsRunning = false,
                LastError = "",
                RenderingMode = LogoRenderingMode.Immediate, // Default to immediate mode
                AnimationSpeed = 10.0
            };
        }

        public async Task<bool> ExecuteCodeAsync(LogoGameState gameState, string code)
        {
            try
            {
                LogDebug($"[Logo] ╔═══════════════════════════════════════════════════╗");
                LogDebug($"[Logo] ║  ExecuteCodeAsync STARTED                         ║");
                LogDebug($"[Logo] ╚═══════════════════════════════════════════════════╝");
                LogDebug($"[Logo] Rendering mode: {gameState.RenderingMode}");
                LogDebug($"[Logo] Code length: {code.Length} characters");
                LogDebug($"[Logo] Code preview: {code.Substring(0, Math.Min(100, code.Length))}...");

                gameState.LastError = "";
                gameState.IsRunning = true;

                // Clear the visual canvas but preserve game state (for both Immediate and Animated modes)
                gameState.DrawingElements.Clear();

                var clearOperation = new LogoCanvasOperation
                {
                    Type = LogoCanvasOperationType.Clear
                };
                gameState.OnCanvasOperation?.Invoke(clearOperation);
                LogDebug($"[Logo] ✓ Cleared canvas for {gameState.RenderingMode} mode");

                // Parse and execute the Logo code
                var commands = ParseLogoCode(code);
                LogDebug($"[Logo] ═══════════════════════════════════════════════════");
                LogDebug($"[Logo] Parsed {commands.Count} total commands");
                LogDebug($"[Logo] ═══════════════════════════════════════════════════");

                foreach (var command in commands)
                {
                    LogDebug($"[Logo] ▶ Executing: {command.Type} - {command.OriginalText}");
                    await ExecuteCommandAsync(gameState, command);
                    LogDebug($"[Logo] ✓ Completed: {command.Type}");

                    // Only add delay for animated mode - immediate mode is instant
                    if (gameState.RenderingMode == LogoRenderingMode.Animated)
                    {
                        var delay = (int)(1000.0 / gameState.AnimationSpeed);
                        await Task.Delay(Math.Max(10, delay)); // Minimum 10ms delay
                    }
                }

                gameState.IsRunning = false;

                LogDebug($"[Logo] ╔═══════════════════════════════════════════════════╗");
                LogDebug($"[Logo] ║ ExecuteCodeAsync COMPLETED                        ║");
                LogDebug($"[Logo] ╚═══════════════════════════════════════════════════╝");
                LogDebug($"[Logo] Drew {gameState.DrawingElements.Count} drawing elements");
                LogDebug($"[Logo] Turtle at ({gameState.Turtle.X:F1}, {gameState.Turtle.Y:F1}) heading {gameState.Turtle.Heading:F1}°");

                return true;
            }
            catch (Exception ex)
            {
                LogDebug($"[Logo] Execution error: {ex.Message}");
                LogDebug($"[Logo] Stack trace: {ex.StackTrace}");
                gameState.LastError = ex.Message;
                gameState.IsRunning = false;
                return false;
            }
        }

        private List<LogoCommand> ParseLogoCode(string code)
        {
            LogDebug($"[Logo] === PARSING LOGO CODE ===");
            LogDebug($"[Logo] Input code: '{code}'");
            LogDebug($"[Logo] Code length: {code.Length}");

            var commands = new List<LogoCommand>();

            // First, handle multi-line repeat commands by flattening them
            var processedCode = PreprocessCode(code);
            LogDebug($"[Logo] After preprocessing: '{processedCode}'");

            var lines = processedCode.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            LogDebug($"[Logo] Split into {lines.Length} lines");

            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                var line = lines[lineIndex];
                var trimmedLine = line.Trim();
                LogDebug($"[Logo] Processing line {lineIndex + 1}: '{trimmedLine}'");

                if (string.IsNullOrEmpty(trimmedLine))
                {
                    LogDebug($"[Logo] Skipping line {lineIndex + 1} (empty)");
                    continue; // Skip empty lines
                }

                // FIXED APPROACH: Remove comment sections, preserving code
                // Strategy: Process segments between semicolons
                // - Keep segments that are inside brackets (flattened code)
                // - Keep segments that contain Logo commands
                // - Skip pure comment segments

                var segments = new List<string>();
                var currentSegment = new System.Text.StringBuilder();
                int bracketDepth = 0;

                for (int i = 0; i < trimmedLine.Length; i++)
                {
                    char c = trimmedLine[i];

                    if (c == '[')
                    {
                        bracketDepth++;
                        currentSegment.Append(c);
                    }
                    else if (c == ']')
                    {
                        bracketDepth--;
                        currentSegment.Append(c);
                    }
                    else if (c == ';' && bracketDepth == 0)
                    {
                        // End of segment - save it if non-empty
                        var seg = currentSegment.ToString().Trim();
                        if (!string.IsNullOrEmpty(seg))
                        {
                            segments.Add(seg);
                        }
                        currentSegment.Clear();
                        // Skip the semicolon and everything until next real content
                        // (comment continues to end of logical segment)
                    }
                    else
                    {
                        currentSegment.Append(c);
                    }
                }

                // Add final segment
                var finalSeg = currentSegment.ToString().Trim();
                if (!string.IsNullOrEmpty(finalSeg))
                {
                    segments.Add(finalSeg);
                }

                LogDebug($"[Logo] Split into {segments.Count} segments");
                foreach (var seg in segments)
                {
                    LogDebug($"[Logo]   Segment: '{seg}'");
                }

                // Combine segments into cleaned line
                trimmedLine = string.Join(" ", segments);
                LogDebug($"[Logo] After comment removal: '{trimmedLine}'");

                if (string.IsNullOrEmpty(trimmedLine))
                {
                    LogDebug($"[Logo] Skipping line {lineIndex + 1} (comment only after cleanup)");
                    continue;
                }

                try
                {
                    var parsedCommands = ParseLine(trimmedLine);
                    LogDebug($"[Logo] Line {lineIndex + 1} generated {parsedCommands.Count} commands");

                    foreach (var cmd in parsedCommands)
                    {
                        LogDebug($"[Logo] - Command: {cmd.Type} ({cmd.OriginalText})");
                    }

                    commands.AddRange(parsedCommands);
                }
                catch (Exception ex)
                {
                    LogDebug($"[Logo] ERROR parsing line {lineIndex + 1}: {ex.Message}");
                }
            }

            LogDebug($"[Logo] === PARSING COMPLETE ===");
            LogDebug($"[Logo] Total commands parsed: {commands.Count}");
            return commands;
        }

        private string PreprocessCode(string code)
        {
            LogDebug($"[Logo] ════ PreprocessCode STARTED ════");
            LogDebug($"[Logo] Input code:\n{code}");

            // Convert multi-line blocks to single line
            var result = code;

            // Handle for blocks: "for i 1 50 [\n  cmd1\n  cmd2\n]" to "for i 1 50 [cmd1 cmd2]"
            // Use a more robust pattern that handles nested brackets
            var forPattern = @"for\s+(\w+)\s+(\d+)\s+(\d+)\s*\[";
            var matches = new List<(int start, int end, string replacement)>();

            var forMatches = Regex.Matches(result, forPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);

            foreach (Match match in forMatches)
            {
                LogDebug($"[Logo] Found 'for' at position {match.Index}: {match.Value}");

                var variable = match.Groups[1].Value;
                var start = match.Groups[2].Value;
                var end = match.Groups[3].Value;

                // Find the matching closing bracket by counting bracket depth
                int startPos = match.Index + match.Length; // Position after the opening '['
                int bracketDepth = 1;
                int endPos = startPos;

                for (int i = startPos; i < result.Length && bracketDepth > 0; i++)
                {
                    if (result[i] == '[')
                        bracketDepth++;
                    else if (result[i] == ']')
                        bracketDepth--;

                    if (bracketDepth == 0)
                    {
                        endPos = i;
                        break;
                    }
                }

                if (bracketDepth == 0)
                {
                    var innerCode = result.Substring(startPos, endPos - startPos);
                    LogDebug($"[Logo] Inner code:\n{innerCode}");

                    // Flatten the inner code by removing newlines and extra whitespace
                    var flattened = Regex.Replace(innerCode, @"\s+", " ").Trim();
                    LogDebug($"[Logo] Flattened: '{flattened}'");

                    var replacement = $"for {variable} {start} {end} [{flattened}]";

                    // CRITICAL FIX: Check if this 'for' is inside another match's range
                    // If so, skip it (it's a nested loop that will be handled by the outer loop's flattening)
                    bool isNested = false;
                    foreach (var existing in matches)
                    {
                        if (match.Index > existing.start && match.Index < existing.end)
                        {
                            isNested = true;
                            LogDebug($"[Logo] Skipping nested 'for' at position {match.Index} (inside range {existing.start}-{existing.end})");
                            break;
                        }
                    }

                    if (!isNested)
                    {
                        // Store for replacement (we'll do this in reverse order to maintain indices)
                        matches.Add((match.Index, endPos + 1, replacement));
                        LogDebug($"[Logo] Added replacement for position {match.Index}-{endPos + 1}");
                    }
                }
            }

            // Apply replacements in reverse order to maintain indices
            foreach (var (start, end, replacement) in matches.OrderByDescending(m => m.start))
            {
                LogDebug($"[Logo] Replacing position {start}-{end} with: '{replacement}'");
                result = result.Substring(0, start) + replacement + result.Substring(end);
            }

            // Handle repeat blocks: "repeat N [\n  cmd1\n  cmd2\n]" to "repeat N [cmd1 cmd2]"
            var repeatPattern = @"repeat\s+(\d+)\s*\[";
            var repeatMatches = new List<(int start, int end, string replacement)>();

            var repeatMatchList = Regex.Matches(result, repeatPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);

            foreach (Match match in repeatMatchList)
            {
                LogDebug($"[Logo] Found 'repeat' at position {match.Index}: {match.Value}");

                var count = match.Groups[1].Value;

                // Find the matching closing bracket by counting bracket depth
                int startPos = match.Index + match.Length; // Position after the opening '['
                int bracketDepth = 1;
                int endPos = startPos;

                for (int i = startPos; i < result.Length && bracketDepth > 0; i++)
                {
                    if (result[i] == '[')
                        bracketDepth++;
                    else if (result[i] == ']')
                        bracketDepth--;

                    if (bracketDepth == 0)
                    {
                        endPos = i;
                        break;
                    }
                }

                if (bracketDepth == 0)
                {
                    var innerCode = result.Substring(startPos, endPos - startPos);

                    // Flatten the inner code by removing newlines and extra whitespace
                    var flattened = Regex.Replace(innerCode, @"\s+", " ").Trim();

                    var replacement = $"repeat {count} [{flattened}]";


                    // Check if this 'repeat' is inside another match's range
                    bool isNested = false;
                    foreach (var existing in repeatMatches)
                    {
                        if (match.Index > existing.start && match.Index < existing.end)
                        {
                            isNested = true;
                            LogDebug($"[Logo] Skipping nested 'repeat' at position {match.Index}");
                            break;
                        }
                    }

                    if (!isNested)
                    {
                        // Store for replacement
                        repeatMatches.Add((match.Index, endPos + 1, replacement));
                    }
                }
            }

            // Apply replacements in reverse order to maintain indices
            foreach (var (start, end, replacement) in repeatMatches.OrderByDescending(m => m.start))
            {
                LogDebug($"[Logo] Replacing repeat position {start}-{end} with: '{replacement}'");
                result = result.Substring(0, start) + replacement + result.Substring(end);
            }

            LogDebug($"[Logo] ════ PreprocessCode COMPLETED ════");
            LogDebug($"[Logo] Output code:\n{result}");

            return result;
        }

        private List<LogoCommand> ParseLine(string line)
        {
            var commands = new List<LogoCommand>();
            LogDebug($"[Logo] ════════════════════════════════════════");
            LogDebug($"[Logo] ParseLine INPUT: '{line}'");

            // Handle repeat command specially
            if (line.StartsWith("repeat", StringComparison.OrdinalIgnoreCase))
            {
                LogDebug($"[Logo] → Detected REPEAT command");
                var repeatCommand = ParseRepeatCommand(line);
                if (repeatCommand != null)
                {
                    LogDebug($"[Logo] Parsed repeat command: count={repeatCommand.Parameters["count"]}, code={repeatCommand.Parameters["code"]}");
                    commands.Add(repeatCommand);
                    LogDebug($"[Logo] ✓ Parsed repeat: {repeatCommand.OriginalText}");
                }
                return commands;
            }

            // Handle for command specially - MUST check before tokenizing to handle nested brackets
            if (line.StartsWith("for", StringComparison.OrdinalIgnoreCase))
            {
                LogDebug($"[Logo] → Detected FOR command at START");
                var forCommand = ParseForCommandFromLine(line);
                if (forCommand != null)
                {
                    LogDebug($"[Logo] Parsed for command: {forCommand.Parameters["variable"]} from {forCommand.Parameters["start"]} to {forCommand.Parameters["end"]}");
                    commands.Add(forCommand);
                    LogDebug($"[Logo] ✓ Parsed for: {forCommand.OriginalText}");
                }
                return commands;
            }

            // NEW: Check if line CONTAINS a for loop (not just starts with it)
            // This handles cases like: "setpencolor :color for i 1 8 [ fd :i rt 91 ]"
            var forPattern = @"\bfor\s+(\w+)\s+(\d+)\s+(\d+)\s*\[";
            if (Regex.IsMatch(line, forPattern, RegexOptions.IgnoreCase))
            {
                LogDebug($"[Logo] → Detected FOR command in MIDDLE of line");
                // Find where the 'for' command starts
                var forMatch = Regex.Match(line, forPattern, RegexOptions.IgnoreCase);
                if (forMatch.Success && forMatch.Index > 0)
                {
                    // There are commands before the 'for' loop
                    var beforeFor = line.Substring(0, forMatch.Index).Trim();
                    var fromFor = line.Substring(forMatch.Index).Trim();

                    LogDebug($"[Logo] Split into:");
                    LogDebug($"[Logo]   BEFORE FOR: '{beforeFor}'");
                    LogDebug($"[Logo]   FROM FOR: '{fromFor}'");

                    // Parse commands before the for loop
                    var beforeTokens = Regex.Split(beforeFor, @"\s+").Where(t => !string.IsNullOrEmpty(t)).ToArray();
                    LogDebug($"[Logo] Parsing {beforeTokens.Length} tokens before for loop");
                    for (int i = 0; i < beforeTokens.Length; i++)
                    {
                        var command = ParseToken(beforeTokens, ref i);
                        if (command != null)
                        {
                            commands.Add(command);
                            LogDebug($"[Logo] ✓ Added command: {command.Type} - {command.OriginalText}");
                        }
                    }

                    // Parse the for loop
                    LogDebug($"[Logo] Now parsing for loop portion");
                    var forCommand = ParseForCommandFromLine(fromFor);
                    if (forCommand != null)
                    {
                        LogDebug($"[Logo] Parsed for command: {forCommand.Parameters["variable"]} from {forCommand.Parameters["start"]} to {forCommand.Parameters["end"]}");
                        commands.Add(forCommand);
                        LogDebug($"[Logo] ✓ Parsed for: {forCommand.OriginalText}");
                    }

                    LogDebug($"[Logo] ParseLine OUTPUT: {commands.Count} commands total");
                    return commands;
                }
            }

            // Split line into tokens (only if no special commands found)
            LogDebug($"[Logo] → Parsing as REGULAR tokens");
            var tokens = Regex.Split(line, @"\s+").Where(t => !string.IsNullOrEmpty(t)).ToArray();
            LogDebug($"[Logo] Split into {tokens.Length} tokens: {string.Join(", ", tokens)}");

            for (int i = 0; i < tokens.Length; i++)
            {
                var command = ParseToken(tokens, ref i);
                if (command != null)
                {
                    commands.Add(command);
                    LogDebug($"[Logo] ✓ Added command: {command.Type} - {command.OriginalText}");
                }
            }

            LogDebug($"[Logo] ParseLine OUTPUT: {commands.Count} commands total");
            LogDebug($"[Logo] ════════════════════════════════════════");
            return commands;
        }

        private LogoCommand? ParseRepeatCommand(string line)
        {
            LogDebug($"[Logo] Parsing repeat command: '{line}'");

            // Parse: repeat 4 [fd 100 rt 90]
            // Need to handle nested brackets properly
            var match = Regex.Match(line, @"^repeat\s+(\d+)\s*\[", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                LogDebug($"[Logo] Repeat regex did not match");
                return null;
            }

            var count = int.Parse(match.Groups[1].Value);

            // Find the matching closing bracket by counting bracket depth
            int startPos = match.Index + match.Length; // Position after the opening '['
            int bracketDepth = 1;
            int endPos = startPos;

            for (int i = startPos; i < line.Length && bracketDepth > 0; i++)
            {
                if (line[i] == '[')
                    bracketDepth++;
                else if (line[i] == ']')
                    bracketDepth--;

                if (bracketDepth == 0)
                {
                    endPos = i;
                    break;
                }
            }

            if (bracketDepth != 0)
            {
                LogDebug($"[Logo] Unmatched brackets in repeat command: '{line}'");
                return null;
            }

            var innerCode = line.Substring(startPos, endPos - startPos).Trim();

            LogDebug($"[Logo] Parsed repeat: count={count}, innerCode='{innerCode}'");

            return new LogoCommand
            {
                Type = LogoCommandType.Repeat,
                Parameters = new Dictionary<string, object>
                {
                    ["count"] = count,
                    ["code"] = innerCode
                },
                OriginalText = line
            };
        }

        private LogoCommand? ParseForCommandFromLine(string line)
        {
            // Parse: for i 1 50 [fd :i rt 91]
            // Need to handle nested brackets properly
            var match = Regex.Match(line, @"^for\s+(\w+)\s+(\d+)\s+(\d+)\s*\[", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                LogDebug($"[Logo] For regex did not match: '{line}'");
                return null;
            }

            var variable = match.Groups[1].Value;
            var start = double.Parse(match.Groups[2].Value);
            var end = double.Parse(match.Groups[3].Value);

            // Find the matching closing bracket by counting bracket depth
            int startPos = match.Index + match.Length; // Position after the opening '['
            int bracketDepth = 1;
            int endPos = startPos;

            for (int i = startPos; i < line.Length && bracketDepth > 0; i++)
            {
                if (line[i] == '[')
                    bracketDepth++;
                else if (line[i] == ']')
                    bracketDepth--;

                if (bracketDepth == 0)
                {
                    endPos = i;
                    break;
                }
            }

            if (bracketDepth != 0)
            {
                LogDebug($"[Logo] Unmatched brackets in for command: '{line}'");
                return null;
            }

            var code = line.Substring(startPos, endPos - startPos).Trim();

            LogDebug($"[Logo] Parsed for: variable={variable}, start={start}, end={end}, code='{code}'");

            return new LogoCommand
            {
                Type = LogoCommandType.For,
                Parameters = new Dictionary<string, object>
                {
                    ["variable"] = variable,
                    ["start"] = start,
                    ["end"] = end,
                    ["code"] = code
                },
                OriginalText = line
            };
        }

        private LogoCommand? ParseToken(string[] tokens, ref int index)
        {
            if (index >= tokens.Length)
                return null;

            var token = tokens[index].ToLowerInvariant();

            return token switch
            {
                "fd" or "forward" => ParseMovementCommand(LogoCommandType.Forward, tokens, ref index),
                "bk" or "backward" => ParseMovementCommand(LogoCommandType.Backward, tokens, ref index),
                "rt" or "right" => ParseTurnCommand(LogoCommandType.Right, tokens, ref index),
                "lt" or "left" => ParseTurnCommand(LogoCommandType.Left, tokens, ref index),
                "pu" or "penup" => new LogoCommand { Type = LogoCommandType.PenUp, OriginalText = token },
                "pd" or "pendown" => new LogoCommand { Type = LogoCommandType.PenDown, OriginalText = token },
                "cs" or "clearscreen" => new LogoCommand { Type = LogoCommandType.ClearScreen, OriginalText = token },
                "st" or "showturtle" => new LogoCommand { Type = LogoCommandType.ShowTurtle, OriginalText = token },
                "ht" or "hideturtle" => new LogoCommand { Type = LogoCommandType.HideTurtle, OriginalText = token },
                "home" => new LogoCommand { Type = LogoCommandType.Home, OriginalText = token },
                "setpencolor" => ParseColorCommand(tokens, ref index),
                "setpenwidth" => ParseWidthCommand(tokens, ref index),
                "setxy" => ParseSetXYCommand(tokens, ref index),
                "setx" => ParseSetXCommand(tokens, ref index),
                "sety" => ParseSetYCommand(tokens, ref index),
                "seth" or "setheading" => ParseSetHeadingCommand(tokens, ref index),
                "delay" => ParseDelayCommand(tokens, ref index),
                _ => null
            };
        }

        private LogoCommand ParseDelayCommand(string[] tokens, ref int index)
        {
            index++; // Move past delay
            if (index < tokens.Length)
            {
                var timeToken = tokens[index];

                return new LogoCommand
                {
                    Type = LogoCommandType.Delay,
                    Parameters = new Dictionary<string, object> { ["milliseconds"] = timeToken },
                    OriginalText = $"delay {tokens[index]}"
                };
            }
            throw new InvalidOperationException("Invalid milliseconds parameter for delay");
        }

        private LogoCommand ParseMovementCommand(LogoCommandType type, string[] tokens, ref int index)
        {
            index++; // Move past the command
            if (index < tokens.Length)
            {
                var distanceToken = tokens[index];
                // Store the token as-is, we'll evaluate it during execution
                return new LogoCommand
                {
                    Type = type,
                    Parameters = new Dictionary<string, object> { ["distance"] = distanceToken },
                    OriginalText = $"{tokens[index - 1]} {tokens[index]}"
                };
            }
            throw new InvalidOperationException($"Invalid distance parameter for {type}");
        }

        private LogoCommand ParseTurnCommand(LogoCommandType type, string[] tokens, ref int index)
        {
            index++; // Move past the command
            if (index < tokens.Length)
            {
                var angleToken = tokens[index];
                // Store the token as-is, we'll evaluate it during execution
                return new LogoCommand
                {
                    Type = type,
                    Parameters = new Dictionary<string, object> { ["angle"] = angleToken },
                    OriginalText = $"{tokens[index - 1]} {tokens[index]}"
                };
            }
            throw new InvalidOperationException($"Invalid angle parameter for {type}");
        }

        private LogoCommand ParseColorCommand(string[] tokens, ref int index)
        {
            index++; // Move past setpencolor
            if (index < tokens.Length)
            {
                var colorToken = tokens[index];

                // Store the token as-is, we'll evaluate it during execution
                // This handles variables like :colorvar, integer values, and string colors
                return new LogoCommand
                {
                    Type = LogoCommandType.SetPenColor,
                    Parameters = new Dictionary<string, object> { ["color"] = colorToken },
                    OriginalText = $"setpencolor {tokens[index]}"
                };
            }
            throw new InvalidOperationException("Invalid color parameter for setpencolor");
        }

        private LogoCommand ParseWidthCommand(string[] tokens, ref int index)
        {
            index++; // Move past setpenwidth
            if (index < tokens.Length && double.TryParse(tokens[index], out double width))
            {
                return new LogoCommand
                {
                    Type = LogoCommandType.SetPenWidth,
                    Parameters = new Dictionary<string, object> { ["width"] = width },
                    OriginalText = $"setpenwidth {tokens[index]}"
                };
            }
            throw new InvalidOperationException("Invalid width parameter for setpenwidth");
        }

        private LogoCommand ParseSetXYCommand(string[] tokens, ref int index)
        {
            index++; // Move past setxy
            if (index + 1 < tokens.Length)
            {
                var xToken = tokens[index];
                var yToken = tokens[index + 1];
                index++; // Consume the second parameter

                return new LogoCommand
                {
                    Type = LogoCommandType.SetXY,
                    Parameters = new Dictionary<string, object> { ["x"] = xToken, ["y"] = yToken },
                };
            }
            throw new InvalidOperationException("Invalid parameters for setxy");
        }

        private LogoCommand ParseSetXCommand(string[] tokens, ref int index)
        {
            index++; // Move past setx
            if (index < tokens.Length)
            {
                var xToken = tokens[index];

                // Store the token as-is, we'll evaluate it during execution
                return new LogoCommand
                {
                    Type = LogoCommandType.SetX,
                    Parameters = new Dictionary<string, object> { ["x"] = xToken },
                    OriginalText = $"setx {tokens[index]}"
                };
            }
            throw new InvalidOperationException("Invalid x parameter for setx");
        }

        private LogoCommand ParseSetYCommand(string[] tokens, ref int index)
        {
            index++; // Move past sety
            if (index < tokens.Length)
            {
                var yToken = tokens[index];

                // Store the token as-is, we'll evaluate it during execution
                return new LogoCommand
                {
                    Type = LogoCommandType.SetY,
                    Parameters = new Dictionary<string, object> { ["y"] = yToken },
                    OriginalText = $"sety {tokens[index]}"
                };
            }
            throw new InvalidOperationException("Invalid y parameter for sety");
        }

        private LogoCommand ParseSetHeadingCommand(string[] tokens, ref int index)
        {
            index++; // Move past seth or setheading
            if (index < tokens.Length)
            {
                var headingToken = tokens[index];

                return new LogoCommand
                {
                    Type = LogoCommandType.SetHeading,
                    Parameters = new Dictionary<string, object> { ["heading"] = headingToken },
                    OriginalText = $"{tokens[index - 1]} {tokens[index]}"
                };
            }
            throw new InvalidOperationException("Invalid heading parameter for seth/setheading");
        }

        private string ConvertColorNameToHex(string colorName)
        {
            return colorName.ToLowerInvariant() switch
            {
                "red" => "#FF0000",
                "green" => "#00FF00",
                "blue" => "#0000FF",
                "yellow" => "#FFFF00",
                "orange" => "#FFA500",
                "purple" => "#800080",
                "pink" => "#FFC0CB",
                "brown" => "#A52A2A",
                "black" => "#000000",
                "white" => "#FFFFFF",
                "gray" or "grey" => "#808080",
                "cyan" => "#00FFFF",
                "magenta" => "#FF00FF",
                _ => colorName.StartsWith("#") ? colorName : "#000000"
            };
        }

        private string EvaluateColorExpression(string colorExpression, LogoGameState gameState)
        {
            if (colorExpression.StartsWith(":"))
            {
                var varName = colorExpression.Substring(1);
                if (gameState.Variables.ContainsKey(varName))
                {
                    var value = gameState.Variables[varName];
                    return LogoColorUtils.IntColorToHex((int)value);
                }
                throw new InvalidOperationException($"Color variable '{varName}' not defined");
            }

            if (int.TryParse(colorExpression, out int colorInt))
            {
                return LogoColorUtils.IntColorToHex(colorInt);
            }

            var cleanColor = colorExpression.StartsWith("\"") && colorExpression.EndsWith("\"")
                ? colorExpression.Trim('"')
                : colorExpression;

            var colorNameInt = LogoColorUtils.GetColorInt(cleanColor);
            if (colorNameInt.HasValue)
            {
                return LogoColorUtils.IntColorToHex(colorNameInt.Value);
            }

            return ConvertColorNameToHex(cleanColor);
        }

        private double EvaluateExpression(string expression, LogoGameState gameState)
        {
            LogDebug($"[Logo] ▶ EvaluateExpression: '{expression}'");
            LogDebug($"[Logo]   Available variables: {string.Join(", ", gameState.Variables.Select(v => $"{v.Key}={v.Value}"))}");

            if (expression.StartsWith(":"))
            {
                var varName = expression.Substring(1);
                LogDebug($"[Logo]   Looking for variable: '{varName}'");

                if (gameState.Variables.ContainsKey(varName))
                {
                    var varValue = gameState.Variables[varName];
                    LogDebug($"[Logo]   ✓ Found variable '{varName}' = {varValue}");
                    return varValue;
                }

                LogDebug($"[Logo]   ❌ ERROR: Variable '{varName}' NOT FOUND!");
                LogDebug($"[Logo]   Variables in dictionary: {string.Join(", ", gameState.Variables.Keys)}");
                throw new InvalidOperationException($"Variable '{varName}' not defined");
            }

            if (double.TryParse(expression, out double numericValue))
            {
                LogDebug($"[Logo]   ✓ Parsed numeric value: {numericValue}");
                return numericValue;
            }

            LogDebug($"[Logo]   ❌ ERROR: Could not evaluate expression '{expression}'");
            throw new InvalidOperationException($"Invalid expression: {expression}");
        }

        private async Task ExecuteCommandAsync(LogoGameState gameState, LogoCommand command)
        {
            LogDebug($"[Logo] === EXECUTING COMMAND ===");
            LogDebug($"[Logo] Command type: {command.Type}");
            LogDebug($"[Logo] Original text: '{command.OriginalText}'");
            LogDebug($"[Logo] Parameters: {string.Join(", ", command.Parameters.Select(p => $"{p.Key}={p.Value}"))}");
            
            gameState.CommandHistory.Add(command);

            try
            {
                switch (command.Type)
                {
                    case LogoCommandType.Forward:
                        var forwardDistance = EvaluateExpression(command.Parameters["distance"].ToString(), gameState);
                        LogDebug($"[Logo] Executing Forward with distance: {forwardDistance}");
                        await MoveForward(gameState, forwardDistance);
                        break;

                    case LogoCommandType.Backward:
                        var backwardDistance = EvaluateExpression(command.Parameters["distance"].ToString(), gameState);
                        LogDebug($"[Logo] Executing Backward with distance: {backwardDistance}");
                        await MoveBackward(gameState, backwardDistance);
                        break;

                    case LogoCommandType.Right:
                        var rightAngle = EvaluateExpression(command.Parameters["angle"].ToString(), gameState);
                        LogDebug($"[Logo] Executing Right with angle: {rightAngle}");
                        TurnRight(gameState, rightAngle);
                        gameState.OnTurtlePositionChanged?.Invoke(gameState.Turtle.Clone());
                        break;

                    case LogoCommandType.Left:
                        var leftAngle = EvaluateExpression(command.Parameters["angle"].ToString(), gameState);
                        LogDebug($"[Logo] Executing Left with angle: {leftAngle}");
                        TurnLeft(gameState, leftAngle);
                        gameState.OnTurtlePositionChanged?.Invoke(gameState.Turtle.Clone());
                        break;

                    case LogoCommandType.PenUp:
                        LogDebug("[Logo] Executing PenUp");
                        gameState.Turtle.PenDown = false;
                        break;

                    case LogoCommandType.PenDown:
                        LogDebug("[Logo] Executing PenDown");
                        gameState.Turtle.PenDown = true;
                        break;

                    case LogoCommandType.SetPenColor:
                        var colorExpression = command.Parameters["color"].ToString();
                        var evaluatedColor = EvaluateColorExpression(colorExpression, gameState);
                        LogDebug($"[Logo] Executing SetPenColor: {colorExpression} -> {evaluatedColor}");
                        gameState.Turtle.PenColor = evaluatedColor;
                        break;

                    case LogoCommandType.SetPenWidth:
                        LogDebug($"[Logo] Executing SetPenWidth: {command.Parameters["width"]}");
                        gameState.Turtle.PenWidth = (double)command.Parameters["width"];
                        break;

                    case LogoCommandType.SetXY:
                        var xExpr = command.Parameters["x"].ToString();
                        var yExpr = command.Parameters["y"].ToString();
                        var x = EvaluateExpression(xExpr, gameState);
                        var y = EvaluateExpression(yExpr, gameState);
                        LogDebug($"[Logo] Executing SetXY: ({x}, {y})");
                        await MoveTo(gameState, x, y);
                        break;

                    case LogoCommandType.SetX:
                        var xSetExpr = command.Parameters["x"].ToString();
                        var xSet = EvaluateExpression(xSetExpr, gameState);
                        LogDebug($"[Logo] Executing SetX: {xSet}");
                        await MoveTo(gameState, xSet, gameState.Turtle.Y);
                        break;

                    case LogoCommandType.SetY:
                        var ySetExpr = command.Parameters["y"].ToString();
                        var ySet = EvaluateExpression(ySetExpr, gameState);
                        LogDebug($"[Logo] Executing SetY: {ySet}");
                        await MoveTo(gameState, gameState.Turtle.X, ySet);
                        break;

                    case LogoCommandType.SetHeading:
                        var headingExpr = command.Parameters["heading"].ToString();
                        var heading = EvaluateExpression(headingExpr, gameState);
                        LogDebug($"[Logo] Executing SetHeading: {heading}");
                        gameState.Turtle.Heading = heading;
                        gameState.OnTurtlePositionChanged?.Invoke(gameState.Turtle.Clone());
                        break;

                    case LogoCommandType.Home:
                        LogDebug("[Logo] Executing Home");
                        await MoveTo(gameState, 250, 250);
                        gameState.Turtle.Heading = 0;
                        break;

                    case LogoCommandType.ClearScreen:
                        LogDebug("[Logo] Executing ClearScreen");
                        gameState.DrawingElements.Clear();
                        var clearOperation = new LogoCanvasOperation
                        {
                            Type = LogoCanvasOperationType.Clear
                        };
                        gameState.OnCanvasOperation?.Invoke(clearOperation);
                        await MoveTo(gameState, 250, 250);
                        gameState.Turtle.Heading = 0;
                        break;

                    case LogoCommandType.ShowTurtle:
                        LogDebug("[Logo] Executing ShowTurtle");
                        gameState.Turtle.IsVisible = true;
                        var showOperation = new LogoCanvasOperation
                        {
                            Type = LogoCanvasOperationType.ShowTurtle
                        };
                        gameState.OnCanvasOperation?.Invoke(showOperation);
                        break;

                    case LogoCommandType.HideTurtle:
                        LogDebug("[Logo] Executing HideTurtle");
                        gameState.Turtle.IsVisible = false;
                        var hideOperation = new LogoCanvasOperation
                        {
                            Type = LogoCanvasOperationType.HideTurtle
                        };
                        gameState.OnCanvasOperation?.Invoke(hideOperation);
                        break;

                    case LogoCommandType.Repeat:
                        var count = (int)command.Parameters["count"];
                        var code = (string)command.Parameters["code"];
                        LogDebug($"[Logo] Executing Repeat {count} times: '{code}'");
                        await ExecuteRepeat(gameState, count, code);
                        break;

                    case LogoCommandType.For:
                        var variable = (string)command.Parameters["variable"];
                        var start = (double)command.Parameters["start"];
                        var end = (double)command.Parameters["end"];
                        var forCode = (string)command.Parameters["code"];
                        LogDebug($"[Logo] Executing For {variable} from {start} to {end}: '{forCode}'");
                        await ExecuteFor(gameState, variable, start, end, forCode);
                        break;

                    default:
                        LogDebug($"[Logo] WARNING: Unknown command type: {command.Type}");
                        break;
                }

                LogDebug($"[Logo] Command {command.Type} executed successfully");
                LogDebug($"[Logo] Drawing elements count now: {gameState.DrawingElements.Count}");
            }
            catch (Exception ex)
            {
                LogDebug($"[Logo] ERROR executing command {command.Type}: {ex.Message}");
                LogDebug($"[Logo] Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        private async Task ExecuteRepeat(LogoGameState gameState, int count, string code)
        {
            LogDebug($"[Logo] Executing repeat {count} times: {code}");

            for (int i = 0; i < count; i++)
            {
                LogDebug($"[Logo] Repeat iteration {i + 1}/{count}");
                var commands = ParseLogoCode(code);
                foreach (var command in commands)
                {
                    await ExecuteCommandAsync(gameState, command);
                }
            }
        }

        private async Task ExecuteFor(LogoGameState gameState, string variable, double start, double end, string code)
        {
            LogDebug($"[Logo] ═══════════════════════════════════════════════════");
            LogDebug($"[Logo] ExecuteFor STARTED: {variable} from {start} to {end}");
            LogDebug($"[Logo] For loop code: '{code}'");
            LogDebug($"[Logo] Variables BEFORE loop: {string.Join(", ", gameState.Variables.Select(v => $"{v.Key}={v.Value}"))}");

            bool hadPreviousValue = gameState.Variables.ContainsKey(variable);
            double? previousValue = hadPreviousValue ? gameState.Variables[variable] : null;

            LogDebug($"[Logo] Variable '{variable}' - hadPreviousValue: {hadPreviousValue}, previousValue: {previousValue}");

            try
            {
                for (double i = start; i <= end; i++)
                {
                    LogDebug($"[Logo] ─────────────────────────────────────────────────");
                    LogDebug($"[Logo] For iteration START: {variable} = {i}");

                    gameState.Variables[variable] = i;
                    LogDebug($"[Logo] Set variable {variable} = {i}");
                    LogDebug($"[Logo] All variables AFTER set: {string.Join(", ", gameState.Variables.Select(v => $"{v.Key}={v.Value}"))}");

                    var commands = ParseLogoCode(code);
                    LogDebug($"[Logo] Parsed {commands.Count} commands for iteration");

                    foreach (var command in commands)
                    {
                        LogDebug($"[Logo] Executing command: {command.Type} - {command.OriginalText}");
                        LogDebug($"[Logo] Variables BEFORE command: {string.Join(", ", gameState.Variables.Select(v => $"{v.Key}={v.Value}"))}");

                        await ExecuteCommandAsync(gameState, command);

                        LogDebug($"[Logo] Variables AFTER command: {string.Join(", ", gameState.Variables.Select(v => $"{v.Key}={v.Value}"))}");
                    }

                    LogDebug($"[Logo] For iteration END: {variable} = {i}");
                    LogDebug($"[Logo] Variables at iteration end: {string.Join(", ", gameState.Variables.Select(v => $"{v.Key}={v.Value}"))}");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[Logo] ❌ ERROR in ExecuteFor: {ex.Message}");
                LogDebug($"[Logo] Variables at error: {string.Join(", ", gameState.Variables.Select(v => $"{v.Key}={v.Value}"))}");
                LogDebug($"[Logo] Stack trace: {ex.StackTrace}");
                throw;
            }
            finally
            {
                LogDebug($"[Logo] ─────────────────────────────────────────────────");
                LogDebug($"[Logo] ExecuteFor FINALLY block");
                LogDebug($"[Logo] Variables BEFORE cleanup: {string.Join(", ", gameState.Variables.Select(v => $"{v.Key}={v.Value}"))}");
                LogDebug($"[Logo] Cleanup decision for '{variable}': hadPreviousValue={hadPreviousValue}, previousValue={previousValue}");

                if (hadPreviousValue && previousValue.HasValue)
                {
                    gameState.Variables[variable] = previousValue.Value;
                    LogDebug($"[Logo] ✓ RESTORED variable '{variable}' to previous value: {previousValue.Value}");
                }
                else
                {
                    if (gameState.Variables.ContainsKey(variable))
                    {
                        gameState.Variables.Remove(variable);
                        LogDebug($"[Logo] ✓ REMOVED variable '{variable}' (did not exist before loop)");
                    }
                    else
                    {
                        LogDebug($"[Logo] ⚠ Variable '{variable}' not found in dictionary for removal");
                    }
                }

                LogDebug($"[Logo] Variables AFTER cleanup: {string.Join(", ", gameState.Variables.Select(v => $"{v.Key}={v.Value}"))}");
                LogDebug($"[Logo] ExecuteFor COMPLETED: {variable}");
                LogDebug($"[Logo] ═══════════════════════════════════════════════════");
            }
        }

        private async Task MoveForward(LogoGameState gameState, double distance)
        {
            var turtle = gameState.Turtle;
            var oldX = turtle.X;
            var oldY = turtle.Y;

            var radians = (turtle.Heading - 90) * Math.PI / 180;

            turtle.X += distance * Math.Cos(radians);
            turtle.Y += distance * Math.Sin(radians);

            LogDebug($"[Logo] Moving from ({oldX:F1}, {oldY:F1}) to ({turtle.X:F1}, {turtle.Y:F1}), heading: {turtle.Heading:F1}°");

            if (turtle.PenDown)
            {
                var line = new LogoDrawingElement
                {
                    Type = LogoDrawingType.Line,
                    StartX = oldX,
                    StartY = oldY,
                    EndX = turtle.X,
                    EndY = turtle.Y,
                    Color = turtle.PenColor,
                    Width = turtle.PenWidth
                };
                gameState.DrawingElements.Add(line);
                LogDebug($"[Logo] Added line element: ({oldX:F1}, {oldY:F1}) to ({turtle.X:F1}, {turtle.Y:F1}), color: {turtle.PenColor}");

                gameState.OnDrawingElementCreated?.Invoke(line);
            }

            gameState.OnTurtlePositionChanged?.Invoke(turtle.Clone());
        }

        private async Task MoveBackward(LogoGameState gameState, double distance)
        {
            await MoveForward(gameState, -distance);
        }

        private void TurnRight(LogoGameState gameState, double angle)
        {
            var oldHeading = gameState.Turtle.Heading;
            gameState.Turtle.Heading += angle;
            gameState.Turtle.Heading = gameState.Turtle.Heading % 360;
            LogDebug($"[Logo] Turned right {angle}°: {oldHeading:F1}° -> {gameState.Turtle.Heading:F1}°");
        }

        private void TurnLeft(LogoGameState gameState, double angle)
        {
            var oldHeading = gameState.Turtle.Heading;
            gameState.Turtle.Heading -= angle;
            if (gameState.Turtle.Heading < 0)
                gameState.Turtle.Heading += 360;
            LogDebug($"[Logo] Turned left {angle}°: {oldHeading:F1}° -> {gameState.Turtle.Heading:F1}°");
        }

        private async Task MoveTo(LogoGameState gameState, double x, double y)
        {
            var turtle = gameState.Turtle;
            var oldX = turtle.X;
            var oldY = turtle.Y;

            turtle.X = x;
            turtle.Y = y;

            LogDebug($"[Logo] MoveTo from ({oldX:F1}, {oldY:F1}) to ({x:F1}, {y:F1}), pen: {(turtle.PenDown ? "down" : "up")}");

            if (turtle.PenDown)
            {
                var line = new LogoDrawingElement
                {
                    Type = LogoDrawingType.Line,
                    StartX = oldX,
                    StartY = oldY,
                    EndX = turtle.X,
                    EndY = turtle.Y,
                    Color = turtle.PenColor,
                    Width = turtle.PenWidth
                };
                gameState.DrawingElements.Add(line);
                LogDebug($"[Logo] Added MoveTo line element: ({oldX:F1}, {oldY:F1}) to ({x:F1}, {y:F1}), color: {turtle.PenColor}");

                gameState.OnDrawingElementCreated?.Invoke(line);
            }

            gameState.OnTurtlePositionChanged?.Invoke(turtle.Clone());
        }

        private async Task UpdateCanvasAsync(LogoGameState gameState)
        {
            try
            {
                LogDebug($"[Logo] Updating canvas with {gameState.DrawingElements.Count} drawing elements");
                await _jsRuntime.InvokeVoidAsync("logoDrawCanvas", gameState);
                LogDebug("[Logo] Canvas update completed successfully");
            }
            catch (Exception ex)
            {
                LogDebug($"[Logo] Canvas update error: {ex.Message}");
                gameState.LastError = $"Canvas update error: {ex.Message}";
            }
        }

        public string GetCurrentPosition(LogoGameState gameState)
        {
            return $"X: {gameState.Turtle.X:F1}, Y: {gameState.Turtle.Y:F1}, Heading: {gameState.Turtle.Heading:F1}°";
        }

        public void SetDebugMode(bool enabled)
        {
            _debugMode = enabled;
            LogDebug($"[Logo] Debug mode set to: {enabled}");
        }
    }
}