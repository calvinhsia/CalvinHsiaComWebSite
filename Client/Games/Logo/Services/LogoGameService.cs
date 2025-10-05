using Microsoft.JSInterop;
using System.Text.RegularExpressions;
using WordScapeBlazorWasm.Models;

namespace WordScapeBlazorWasm.Services
{
    public class LogoGameService
    {
        private readonly IJSRuntime _jsRuntime;
        
        public LogoGameService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
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
                CurrentCode = "",
                IsRunning = false,
                LastError = ""
            };
        }

        public async Task<bool> ExecuteCodeAsync(LogoGameState gameState, string code)
        {
            try
            {
                Console.WriteLine($"[Logo] Executing code: {code}");
                
                gameState.LastError = "";
                gameState.IsRunning = true;

                // Parse and execute the Logo code
                var commands = ParseLogoCode(code);
                Console.WriteLine($"[Logo] Parsed {commands.Count} commands");
                
                foreach (var command in commands)
                {
                    Console.WriteLine($"[Logo] Executing command: {command.Type} - {command.OriginalText}");
                    await ExecuteCommandAsync(gameState, command);
                    
                    // Add small delay for visual effect
                    await Task.Delay(10);
                }

                gameState.IsRunning = false;
                
                Console.WriteLine($"[Logo] Execution complete. Drew {gameState.DrawingElements.Count} drawing elements");
                Console.WriteLine($"[Logo] Turtle position: ({gameState.Turtle.X:F1}, {gameState.Turtle.Y:F1}) heading: {gameState.Turtle.Heading:F1}°");
                
                // Update the canvas
                await UpdateCanvasAsync(gameState);
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Logo] Execution error: {ex.Message}");
                Console.WriteLine($"[Logo] Stack trace: {ex.StackTrace}");
                gameState.LastError = ex.Message;
                gameState.IsRunning = false;
                return false;
            }
        }

        private List<LogoCommand> ParseLogoCode(string code)
        {
            Console.WriteLine($"[Logo] === PARSING LOGO CODE ===");
            Console.WriteLine($"[Logo] Input code: '{code}'");
            Console.WriteLine($"[Logo] Code length: {code.Length}");
            
            var commands = new List<LogoCommand>();
            
            // First, handle multi-line repeat commands by flattening them
            var processedCode = PreprocessCode(code);
            Console.WriteLine($"[Logo] After preprocessing: '{processedCode}'");
            
            var lines = processedCode.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Console.WriteLine($"[Logo] Split into {lines.Length} lines");
            
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                var line = lines[lineIndex];
                var trimmedLine = line.Trim();
                Console.WriteLine($"[Logo] Processing line {lineIndex + 1}: '{trimmedLine}'");
                
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith(";"))
                {
                    Console.WriteLine($"[Logo] Skipping line {lineIndex + 1} (empty or comment)");
                    continue; // Skip empty lines and comments
                }

                try
                {
                    var parsedCommands = ParseLine(trimmedLine);
                    Console.WriteLine($"[Logo] Line {lineIndex + 1} generated {parsedCommands.Count} commands");
                    
                    foreach (var cmd in parsedCommands)
                    {
                        Console.WriteLine($"[Logo] - Command: {cmd.Type} ({cmd.OriginalText})");
                    }
                    
                    commands.AddRange(parsedCommands);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Logo] ERROR parsing line {lineIndex + 1}: {ex.Message}");
                }
            }
            
            Console.WriteLine($"[Logo] === PARSING COMPLETE ===");
            Console.WriteLine($"[Logo] Total commands parsed: {commands.Count}");
            return commands;
        }

        private string PreprocessCode(string code)
        {
            // Convert multi-line blocks to single line
            var result = code;
            
            // Handle repeat blocks: "repeat N [\n  cmd1\n  cmd2\n]" to "repeat N [cmd1 cmd2]"
            var repeatPattern = @"repeat\s+(\d+)\s*\[\s*\n(.*?)\n\s*\]";
            var repeatMatches = Regex.Matches(result, repeatPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            
            foreach (Match match in repeatMatches.Cast<Match>().Reverse()) // Process in reverse to maintain indices
            {
                var count = match.Groups[1].Value;
                var innerCode = match.Groups[2].Value;
                
                // Flatten the inner code by removing newlines and extra whitespace
                var flattened = Regex.Replace(innerCode, @"\s+", " ").Trim();
                
                var replacement = $"repeat {count} [{flattened}]";
                result = result.Substring(0, match.Index) + replacement + result.Substring(match.Index + match.Length);
                
                Console.WriteLine($"[Logo] Flattened repeat block: '{replacement}'");
            }
            
            // Handle for blocks: "for i 1 50 [\n  cmd1\n  cmd2\n]" to "for i 1 50 [cmd1 cmd2]"
            var forPattern = @"for\s+(\w+)\s+(\d+)\s+(\d+)\s*\[\s*\n(.*?)\n\s*\]";
            var forMatches = Regex.Matches(result, forPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            
            foreach (Match match in forMatches.Cast<Match>().Reverse()) // Process in reverse to maintain indices
            {
                var variable = match.Groups[1].Value;
                var start = match.Groups[2].Value;
                var end = match.Groups[3].Value;
                var innerCode = match.Groups[4].Value;
                
                // Flatten the inner code by removing newlines and extra whitespace
                var flattened = Regex.Replace(innerCode, @"\s+", " ").Trim();
                
                var replacement = $"for {variable} {start} {end} [{flattened}]";
                result = result.Substring(0, match.Index) + replacement + result.Substring(match.Index + match.Length);
                
                Console.WriteLine($"[Logo] Flattened for block: '{replacement}'");
            }
            
            return result;
        }

        private List<LogoCommand> ParseLine(string line)
        {
            var commands = new List<LogoCommand>();
            
            // Handle repeat command specially
            if (line.StartsWith("repeat", StringComparison.OrdinalIgnoreCase))
            {
                var repeatCommand = ParseRepeatCommand(line);
                if (repeatCommand != null)
                {
                    Console.WriteLine($"[Logo] Parsed repeat command: count={repeatCommand.Parameters["count"]}, code={repeatCommand.Parameters["code"]}");
                    commands.Add(repeatCommand);
                }
                return commands;
            }
            
            // Handle for command specially
            if (line.StartsWith("for", StringComparison.OrdinalIgnoreCase))
            {
                var forCommand = ParseForCommandFromLine(line);
                if (forCommand != null)
                {
                    Console.WriteLine($"[Logo] Parsed for command: {forCommand.Parameters["variable"]} from {forCommand.Parameters["start"]} to {forCommand.Parameters["end"]}");
                    commands.Add(forCommand);
                }
                return commands;
            }

            // Split line into tokens
            var tokens = Regex.Split(line, @"\s+").Where(t => !string.IsNullOrEmpty(t)).ToArray();
            
            for (int i = 0; i < tokens.Length; i++)
            {
                var command = ParseToken(tokens, ref i);
                if (command != null)
                    commands.Add(command);
            }
            
            return commands;
        }

        private LogoCommand? ParseForCommandFromLine(string line)
        {
            // Parse: for i 1 50 [fd :i rt 91]
            var match = Regex.Match(line, @"for\s+(\w+)\s+(\d+)\s+(\d+)\s*\[(.*?)\]", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!match.Success)
            {
                Console.WriteLine($"[Logo] For regex did not match: '{line}'");
                return null;
            }

            var variable = match.Groups[1].Value;
            var start = double.Parse(match.Groups[2].Value);
            var end = double.Parse(match.Groups[3].Value);
            var code = match.Groups[4].Value.Trim();
            
            Console.WriteLine($"[Logo] Parsed for: variable={variable}, start={start}, end={end}, code='{code}'");

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

        private LogoCommand? ParseRepeatCommand(string line)
        {
            Console.WriteLine($"[Logo] Parsing repeat command: '{line}'");
            
            // Parse: repeat 4 [fd 100 rt 90]
            // Handle both single line and multi-line format
            var match = Regex.Match(line, @"repeat\s+(\d+)\s*\[(.*?)\]", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!match.Success)
            {
                Console.WriteLine($"[Logo] Repeat regex did not match");
                return null;
            }

            var count = int.Parse(match.Groups[1].Value);
            var innerCode = match.Groups[2].Value.Trim();
            
            Console.WriteLine($"[Logo] Parsed repeat: count={count}, innerCode='{innerCode}'");

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
                "wait" => ParseWaitCommand(tokens, ref index),
                "for" => ParseForCommand(tokens, ref index),
                _ => null
            };
        }

        private LogoCommand? ParseForCommand(string[] tokens, ref int index)
        {
            // Parse: for i 1 100 [fd :i rt 91]
            index++; // Move past 'for'
            
            if (index + 3 >= tokens.Length)
                return null;
                
            var variable = tokens[index++];  // variable name
            var startValue = tokens[index++]; // start value
            var endValue = tokens[index++];   // end value
            
            // Find the bracket content
            var bracketStart = Array.IndexOf(tokens, "[", index);
            var bracketEnd = Array.LastIndexOf(tokens, "]");
            
            if (bracketStart == -1 || bracketEnd == -1 || bracketEnd <= bracketStart)
                return null;
                
            var codeTokens = tokens.Skip(bracketStart + 1).Take(bracketEnd - bracketStart - 1);
            var code = string.Join(" ", codeTokens);
            
            index = bracketEnd + 1; // Move past the closing bracket
            
            return new LogoCommand
            {
                Type = LogoCommandType.For,
                Parameters = new Dictionary<string, object>
                {
                    ["variable"] = variable,
                    ["start"] = double.Parse(startValue),
                    ["end"] = double.Parse(endValue),
                    ["code"] = code
                },
                OriginalText = string.Join(" ", tokens.Skip(index - (bracketEnd - index + 4)).Take(bracketEnd - index + 4))
            };
        }

        private double EvaluateExpression(string expression, LogoGameState gameState)
        {
            // Handle variable references like :i
            if (expression.StartsWith(":"))
            {
                var varName = expression.Substring(1);
                if (gameState.Variables.ContainsKey(varName))
                {
                    return gameState.Variables[varName];
                }
                throw new InvalidOperationException($"Variable '{varName}' not defined");
            }
            
            // Handle numeric values
            if (double.TryParse(expression, out double value))
            {
                return value;
            }
            
            throw new InvalidOperationException($"Invalid expression: {expression}");
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
                string color;
                
                // Remove quotes if present
                if (colorToken.StartsWith("\"") && colorToken.EndsWith("\""))
                {
                    color = colorToken.Trim('"');
                }
                else
                {
                    color = colorToken;
                }
                
                // Convert common color names to hex
                color = ConvertColorNameToHex(color);
                
                return new LogoCommand
                {
                    Type = LogoCommandType.SetPenColor,
                    Parameters = new Dictionary<string, object> { ["color"] = color },
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
            if (index + 1 < tokens.Length && 
                double.TryParse(tokens[index], out double x) &&
                double.TryParse(tokens[index + 1], out double y))
            {
                var command = new LogoCommand
                {
                    Type = LogoCommandType.SetXY,
                    Parameters = new Dictionary<string, object> { ["x"] = x, ["y"] = y },
                    OriginalText = $"setxy {tokens[index]} {tokens[index + 1]}"
                };
                index++; // Skip the y parameter
                return command;
            }
            throw new InvalidOperationException("Invalid x,y parameters for setxy");
        }

        private LogoCommand ParseSetXCommand(string[] tokens, ref int index)
        {
            index++; // Move past setx
            if (index < tokens.Length && double.TryParse(tokens[index], out double x))
            {
                return new LogoCommand
                {
                    Type = LogoCommandType.SetX,
                    Parameters = new Dictionary<string, object> { ["x"] = x },
                    OriginalText = $"setx {tokens[index]}"
                };
            }
            throw new InvalidOperationException("Invalid x parameter for setx");
        }

        private LogoCommand ParseSetYCommand(string[] tokens, ref int index)
        {
            index++; // Move past sety
            if (index < tokens.Length && double.TryParse(tokens[index], out double y))
            {
                return new LogoCommand
                {
                    Type = LogoCommandType.SetY,
                    Parameters = new Dictionary<string, object> { ["y"] = y },
                    OriginalText = $"sety {tokens[index]}"
                };
            }
            throw new InvalidOperationException("Invalid y parameter for sety");
        }

        private LogoCommand ParseSetHeadingCommand(string[] tokens, ref int index)
        {
            index++; // Move past seth/setheading
            if (index < tokens.Length && double.TryParse(tokens[index], out double heading))
            {
                return new LogoCommand
                {
                    Type = LogoCommandType.SetHeading,
                    Parameters = new Dictionary<string, object> { ["heading"] = heading },
                    OriginalText = $"seth {tokens[index]}"
                };
            }
            throw new InvalidOperationException("Invalid heading parameter for seth");
        }

        private LogoCommand ParseWaitCommand(string[] tokens, ref int index)
        {
            index++; // Move past wait
            if (index < tokens.Length && double.TryParse(tokens[index], out double duration))
            {
                return new LogoCommand
                {
                    Type = LogoCommandType.Wait,
                    Parameters = new Dictionary<string, object> { ["duration"] = duration },
                    OriginalText = $"wait {tokens[index]}"
                };
            }
            throw new InvalidOperationException("Invalid duration parameter for wait");
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
                _ => colorName.StartsWith("#") ? colorName : "#000000"
            };
        }

        private async Task ExecuteCommandAsync(LogoGameState gameState, LogoCommand command)
        {
            Console.WriteLine($"[Logo] === EXECUTING COMMAND ===");
            Console.WriteLine($"[Logo] Command type: {command.Type}");
            Console.WriteLine($"[Logo] Original text: '{command.OriginalText}'");
            Console.WriteLine($"[Logo] Parameters: {string.Join(", ", command.Parameters.Select(p => $"{p.Key}={p.Value}"))}");
            
            gameState.CommandHistory.Add(command);
            
            try
            {
                switch (command.Type)
                {
                    case LogoCommandType.Forward:
                        var forwardDistance = EvaluateExpression(command.Parameters["distance"].ToString(), gameState);
                        Console.WriteLine($"[Logo] Executing Forward with distance: {forwardDistance}");
                        await MoveForward(gameState, forwardDistance);
                        break;
                        
                    case LogoCommandType.Backward:
                        var backwardDistance = EvaluateExpression(command.Parameters["distance"].ToString(), gameState);
                        Console.WriteLine($"[Logo] Executing Backward with distance: {backwardDistance}");
                        await MoveBackward(gameState, backwardDistance);
                        break;
                        
                    case LogoCommandType.Right:
                        var rightAngle = EvaluateExpression(command.Parameters["angle"].ToString(), gameState);
                        Console.WriteLine($"[Logo] Executing Right with angle: {rightAngle}");
                        TurnRight(gameState, rightAngle);
                        break;
                        
                    case LogoCommandType.Left:
                        var leftAngle = EvaluateExpression(command.Parameters["angle"].ToString(), gameState);
                        Console.WriteLine($"[Logo] Executing Left with angle: {leftAngle}");
                        TurnLeft(gameState, leftAngle);
                        break;
                        
                    case LogoCommandType.PenUp:
                        Console.WriteLine("[Logo] Executing PenUp");
                        gameState.Turtle.PenDown = false;
                        break;
                        
                    case LogoCommandType.PenDown:
                        Console.WriteLine("[Logo] Executing PenDown");
                        gameState.Turtle.PenDown = true;
                        break;
                        
                    case LogoCommandType.SetPenColor:
                        Console.WriteLine($"[Logo] Executing SetPenColor: {command.Parameters["color"]}");
                        gameState.Turtle.PenColor = (string)command.Parameters["color"];
                        break;
                        
                    case LogoCommandType.SetPenWidth:
                        Console.WriteLine($"[Logo] Executing SetPenWidth: {command.Parameters["width"]}");
                        gameState.Turtle.PenWidth = (double)command.Parameters["width"];
                        break;
                        
                    case LogoCommandType.SetXY:
                        Console.WriteLine($"[Logo] Executing SetXY: ({command.Parameters["x"]}, {command.Parameters["y"]})");
                        await MoveTo(gameState, (double)command.Parameters["x"], (double)command.Parameters["y"]);
                        break;
                        
                    case LogoCommandType.SetX:
                        Console.WriteLine($"[Logo] Executing SetX: {command.Parameters["x"]}");
                        await MoveTo(gameState, (double)command.Parameters["x"], gameState.Turtle.Y);
                        break;
                        
                    case LogoCommandType.SetY:
                        Console.WriteLine($"[Logo] Executing SetY: {command.Parameters["y"]}");
                        await MoveTo(gameState, gameState.Turtle.X, (double)command.Parameters["y"]);
                        break;
                        
                    case LogoCommandType.SetHeading:
                        Console.WriteLine($"[Logo] Executing SetHeading: {command.Parameters["heading"]}");
                        gameState.Turtle.Heading = (double)command.Parameters["heading"];
                        break;
                        
                    case LogoCommandType.Home:
                        Console.WriteLine("[Logo] Executing Home");
                        await MoveTo(gameState, 250, 250);
                        gameState.Turtle.Heading = 0;
                        break;
                        
                    case LogoCommandType.ClearScreen:
                        Console.WriteLine("[Logo] Executing ClearScreen");
                        gameState.DrawingElements.Clear();
                        await MoveTo(gameState, 250, 250);
                        gameState.Turtle.Heading = 0;
                        break;
                        
                    case LogoCommandType.ShowTurtle:
                        Console.WriteLine("[Logo] Executing ShowTurtle");
                        gameState.Turtle.IsVisible = true;
                        break;
                        
                    case LogoCommandType.HideTurtle:
                        Console.WriteLine("[Logo] Executing HideTurtle");
                        gameState.Turtle.IsVisible = false;
                        break;
                        
                    case LogoCommandType.Repeat:
                        var count = (int)command.Parameters["count"];
                        var code = (string)command.Parameters["code"];
                        Console.WriteLine($"[Logo] Executing Repeat {count} times: '{code}'");
                        await ExecuteRepeat(gameState, count, code);
                        break;
                        
                    case LogoCommandType.For:
                        var variable = (string)command.Parameters["variable"];
                        var start = (double)command.Parameters["start"];
                        var end = (double)command.Parameters["end"];
                        var forCode = (string)command.Parameters["code"];
                        Console.WriteLine($"[Logo] Executing For {variable} from {start} to {end}: '{forCode}'");
                        await ExecuteFor(gameState, variable, start, end, forCode);
                        break;
                        
                    case LogoCommandType.Wait:
                        var duration = (double)command.Parameters["duration"];
                        Console.WriteLine($"[Logo] Executing Wait: {duration}");
                        await Task.Delay((int)(duration * 100)); // duration is in tenths of seconds
                        break;
                        
                    default:
                        Console.WriteLine($"[Logo] WARNING: Unknown command type: {command.Type}");
                        break;
                }
                
                Console.WriteLine($"[Logo] Command {command.Type} executed successfully");
                Console.WriteLine($"[Logo] Drawing elements count now: {gameState.DrawingElements.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Logo] ERROR executing command {command.Type}: {ex.Message}");
                Console.WriteLine($"[Logo] Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        private async Task ExecuteRepeat(LogoGameState gameState, int count, string code)
        {
            Console.WriteLine($"[Logo] Executing repeat {count} times: {code}");
            
            for (int i = 0; i < count; i++)
            {
                Console.WriteLine($"[Logo] Repeat iteration {i + 1}/{count}");
                var commands = ParseLogoCode(code);
                foreach (var command in commands)
                {
                    await ExecuteCommandAsync(gameState, command);
                }
            }
        }

        private async Task ExecuteFor(LogoGameState gameState, string variable, double start, double end, string code)
        {
            Console.WriteLine($"[Logo] Executing for loop: {variable} from {start} to {end}");
            
            for (double i = start; i <= end; i++)
            {
                // Set the variable value
                gameState.Variables[variable] = i;
                Console.WriteLine($"[Logo] For iteration: {variable} = {i}");
                
                // Execute the code with the current variable value
                var commands = ParseLogoCode(code);
                foreach (var command in commands)
                {
                    await ExecuteCommandAsync(gameState, command);
                }
            }
            
            // Clean up the variable after the loop
            gameState.Variables.Remove(variable);
        }

        private async Task MoveForward(LogoGameState gameState, double distance)
        {
            var turtle = gameState.Turtle;
            var oldX = turtle.X;
            var oldY = turtle.Y;
            
            // Convert heading to radians (Logo: 0=up/north, clockwise)
            var radians = (turtle.Heading - 90) * Math.PI / 180;
            
            turtle.X += distance * Math.Cos(radians);
            turtle.Y += distance * Math.Sin(radians);
            
            Console.WriteLine($"[Logo] Moving from ({oldX:F1}, {oldY:F1}) to ({turtle.X:F1}, {turtle.Y:F1}), heading: {turtle.Heading:F1}°, pen: {(turtle.PenDown ? "down" : "up")}");
            
            // If pen is down, add a line to the drawing
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
                Console.WriteLine($"[Logo] Added line element: ({oldX:F1}, {oldY:F1}) to ({turtle.X:F1}, {turtle.Y:F1}), color: {turtle.PenColor}");
            }
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
            Console.WriteLine($"[Logo] Turned right {angle}°: {oldHeading:F1}° -> {gameState.Turtle.Heading:F1}°");
        }

        private void TurnLeft(LogoGameState gameState, double angle)
        {
            var oldHeading = gameState.Turtle.Heading;
            gameState.Turtle.Heading -= angle;
            if (gameState.Turtle.Heading < 0)
                gameState.Turtle.Heading += 360;
            Console.WriteLine($"[Logo] Turned left {angle}°: {oldHeading:F1}° -> {gameState.Turtle.Heading:F1}°");
        }

        private async Task MoveTo(LogoGameState gameState, double x, double y)
        {
            var turtle = gameState.Turtle;
            var oldX = turtle.X;
            var oldY = turtle.Y;
            
            turtle.X = x;
            turtle.Y = y;
            
            Console.WriteLine($"[Logo] MoveTo from ({oldX:F1}, {oldY:F1}) to ({x:F1}, {y:F1}), pen: {(turtle.PenDown ? "down" : "up")}");
            
            // If pen is down, draw a line
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
                Console.WriteLine($"[Logo] Added MoveTo line element: ({oldX:F1}, {oldY:F1}) to ({x:F1}, {y:F1}), color: {turtle.PenColor}");
            }
        }

        private async Task UpdateCanvasAsync(LogoGameState gameState)
        {
            try
            {
                Console.WriteLine($"[Logo] Updating canvas with {gameState.DrawingElements.Count} drawing elements");
                
                // Call JavaScript to update the canvas
                await _jsRuntime.InvokeVoidAsync("logoDrawCanvas", gameState);
                
                Console.WriteLine("[Logo] Canvas update completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Logo] Canvas update error: {ex.Message}");
                // Handle JS interop errors
                gameState.LastError = $"Canvas update error: {ex.Message}";
            }
        }

        public string GetCurrentPosition(LogoGameState gameState)
        {
            return $"X: {gameState.Turtle.X:F1}, Y: {gameState.Turtle.Y:F1}, Heading: {gameState.Turtle.Heading:F1}°";
        }

        private LogoGameState CloneGameState(LogoGameState original)
        {
            var clone = new LogoGameState
            {
                Turtle = new LogoTurtle
                {
                    X = original.Turtle.X,
                    Y = original.Turtle.Y,
                    Heading = original.Turtle.Heading,
                    PenDown = original.Turtle.PenDown,
                    PenColor = original.Turtle.PenColor,
                    PenWidth = original.Turtle.PenWidth,
                    IsVisible = original.Turtle.IsVisible
                },
                Canvas = new LogoCanvas
                {
                    Width = original.Canvas.Width,
                    Height = original.Canvas.Height,
                    BackgroundColor = original.Canvas.BackgroundColor
                },
                CommandHistory = new List<LogoCommand>(original.CommandHistory),
                DrawingElements = new List<LogoDrawingElement>(original.DrawingElements),
                CurrentCode = original.CurrentCode,
                IsRunning = original.IsRunning,
                LastError = original.LastError,
                Variables = new Dictionary<string, double>(original.Variables) // Clone variables
            };
            return clone;
        }

        private void RestoreGameState(LogoGameState target, LogoGameState source)
        {
            target.Turtle.X = source.Turtle.X;
            target.Turtle.Y = source.Turtle.Y;
            target.Turtle.Heading = source.Turtle.Heading;
            target.Turtle.PenDown = source.Turtle.PenDown;
            target.Turtle.PenColor = source.Turtle.PenColor;
            target.Turtle.PenWidth = source.Turtle.PenWidth;
            target.Turtle.IsVisible = source.Turtle.IsVisible;
            
            target.Canvas.Width = source.Canvas.Width;
            target.Canvas.Height = source.Canvas.Height;
            target.Canvas.BackgroundColor = source.Canvas.BackgroundColor;
            
            target.CommandHistory = new List<LogoCommand>(source.CommandHistory);
            target.DrawingElements = new List<LogoDrawingElement>(source.DrawingElements);
            target.CurrentCode = source.CurrentCode;
            target.IsRunning = source.IsRunning;
            target.LastError = source.LastError;
            
            // Restore variables
            foreach (var key in source.Variables.Keys)
            {
                target.Variables[key] = source.Variables[key];
            }
        }
    }
}