using Microsoft.JSInterop;
using System.Text.RegularExpressions;
using WordScapeBlazorWasm.Models;

namespace WordScapeBlazorWasm.Services
{
    public class LogoGameService
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly bool _debugMode;
        
        public LogoGameService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
            _debugMode = false; // Temporarily disable for now - Set to true for debugging
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
                LogDebug($"[Logo] Executing code in {gameState.RenderingMode} mode: {code}");
                
                gameState.LastError = "";
                gameState.IsRunning = true;

                // For immediate/animated modes, clear the visual canvas but preserve game state
                if (gameState.RenderingMode != LogoRenderingMode.Batch)
                {
                    // Clear drawing elements but preserve variables and turtle position
                    gameState.DrawingElements.Clear();
                    
                    var clearOperation = new LogoCanvasOperation
                    {
                        Type = LogoCanvasOperationType.Clear
                    };
                    gameState.OnCanvasOperation?.Invoke(clearOperation);
                }

                // Parse and execute the Logo code
                var commands = ParseLogoCode(code);
                LogDebug($"[Logo] Parsed {commands.Count} commands");
                
                foreach (var command in commands)
                {
                    LogDebug($"[Logo] Executing command: {command.Type} - {command.OriginalText}");
                    await ExecuteCommandAsync(gameState, command);
                    
                    // Add delay for animated mode
                    if (gameState.RenderingMode == LogoRenderingMode.Animated)
                    {
                        var delay = (int)(1000.0 / gameState.AnimationSpeed);
                        await Task.Delay(Math.Max(10, delay)); // Minimum 10ms delay
                    }
                    else if (gameState.RenderingMode == LogoRenderingMode.Immediate)
                    {
                        // Small delay for immediate mode to allow UI to update
                        await Task.Delay(1);
                    }
                }

                gameState.IsRunning = false;
                
                LogDebug($"[Logo] Execution complete. Drew {gameState.DrawingElements.Count} drawing elements");
                LogDebug($"[Logo] Turtle position: ({gameState.Turtle.X:F1}, {gameState.Turtle.Y:F1}) heading: {gameState.Turtle.Heading:F1}°");
                
                // Update the canvas only for batch mode (immediate/animated handle their own updates)
                if (gameState.RenderingMode == LogoRenderingMode.Batch)
                {
                    await UpdateCanvasAsync(gameState);
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Logo] Execution error: {ex.Message}");
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
                
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith(";"))
                {
                    LogDebug($"[Logo] Skipping line {lineIndex + 1} (empty or comment)");
                    continue; // Skip empty lines and comments
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
                
                LogDebug($"[Logo] Flattened repeat block: '{replacement}'");
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
                
                LogDebug($"[Logo] Flattened for block: '{replacement}'");
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
                    LogDebug($"[Logo] Parsed repeat command: count={repeatCommand.Parameters["count"]}, code={repeatCommand.Parameters["code"]}");
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
                    LogDebug($"[Logo] Parsed for command: {forCommand.Parameters["variable"]} from {forCommand.Parameters["start"]} to {forCommand.Parameters["end"]}");
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

        private LogoCommand? ParseRepeatCommand(string line)
        {
            LogDebug($"[Logo] Parsing repeat command: '{line}'");
            
            // Parse: repeat 4 [fd 100 rt 90]
            var match = Regex.Match(line, @"repeat\s+(\d+)\s*\[(.*?)\]", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!match.Success)
            {
                LogDebug($"[Logo] Repeat regex did not match");
                return null;
            }

            var count = int.Parse(match.Groups[1].Value);
            var innerCode = match.Groups[2].Value.Trim();
            
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
            var match = Regex.Match(line, @"for\s+(\w+)\s+(\d+)\s+(\d+)\s*\[(.*?)\]", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!match.Success)
            {
                LogDebug($"[Logo] For regex did not match: '{line}'");
                return null;
            }

            var variable = match.Groups[1].Value;
            var start = double.Parse(match.Groups[2].Value);
            var end = double.Parse(match.Groups[3].Value);
            var code = match.Groups[4].Value.Trim();
            
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
                "wait" => ParseWaitCommand(tokens, ref index),
                _ => null
            };
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
                "cyan" => "#00FFFF",
                "magenta" => "#FF00FF",
                _ => colorName.StartsWith("#") ? colorName : "#000000"
            };
        }

        private string EvaluateColorExpression(string colorExpression, LogoGameState gameState)
        {
            // Handle variable references like :colorvar
            if (colorExpression.StartsWith(":"))
            {
                var varName = colorExpression.Substring(1);
                if (gameState.Variables.ContainsKey(varName))
                {
                    var value = gameState.Variables[varName];
                    // Treat variable value as integer color
                    return LogoColorUtils.IntColorToHex((int)value);
                }
                throw new InvalidOperationException($"Color variable '{varName}' not defined");
            }
            
            // Handle direct integer values
            if (int.TryParse(colorExpression, out int colorInt))
            {
                return LogoColorUtils.IntColorToHex(colorInt);
            }
            
            // Handle quoted string colors
            var cleanColor = colorExpression.StartsWith("\"") && colorExpression.EndsWith("\"") 
                ? colorExpression.Trim('"') 
                : colorExpression;
            
            // Try to convert color name to integer first
            var colorNameInt = LogoColorUtils.GetColorInt(cleanColor);
            if (colorNameInt.HasValue)
            {
                return LogoColorUtils.IntColorToHex(colorNameInt.Value);
            }
            
            // Fall back to traditional color name conversion
            return ConvertColorNameToHex(cleanColor);
        }

        private double EvaluateExpression(string expression, LogoGameState gameState)
        {
            LogDebug($"[Logo] Evaluating expression: '{expression}'");
            LogDebug($"[Logo] Available variables: {string.Join(", ", gameState.Variables.Select(v => $"{v.Key}={v.Value}"))}");
            
            // Handle variable references like :i
            if (expression.StartsWith(":"))
            {
                var varName = expression.Substring(1);
                LogDebug($"[Logo] Looking for variable: '{varName}'");
                
                if (gameState.Variables.ContainsKey(varName))
                {
                    var varValue = gameState.Variables[varName];
                    LogDebug($"[Logo] Found variable {varName} = {varValue}");
                    return varValue;
                }
                
                LogDebug($"[Logo] ERROR: Variable '{varName}' not found in variables dictionary");
                throw new InvalidOperationException($"Variable '{varName}' not defined");
            }
            
            // Handle numeric values
            if (double.TryParse(expression, out double numericValue))
            {
                LogDebug($"[Logo] Parsed numeric value: {numericValue}");
                return numericValue;
            }
            
            LogDebug($"[Logo] ERROR: Could not evaluate expression '{expression}'");
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
                        // Notify turtle position change for immediate modes
                        if (gameState.RenderingMode != LogoRenderingMode.Batch)
                        {
                            gameState.OnTurtlePositionChanged?.Invoke(gameState.Turtle.Clone());
                        }
                        break;
                        
                    case LogoCommandType.Left:
                        var leftAngle = EvaluateExpression(command.Parameters["angle"].ToString(), gameState);
                        LogDebug($"[Logo] Executing Left with angle: {leftAngle}");
                        TurnLeft(gameState, leftAngle);
                        // Notify turtle position change for immediate modes
                        if (gameState.RenderingMode != LogoRenderingMode.Batch)
                        {
                            gameState.OnTurtlePositionChanged?.Invoke(gameState.Turtle.Clone());
                        }
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
                        LogDebug($"[Logo] Executing SetXY: ({command.Parameters["x"]}, {command.Parameters["y"]})");
                        await MoveTo(gameState, (double)command.Parameters["x"], (double)command.Parameters["y"]);
                        break;
                        
                    case LogoCommandType.SetX:
                        LogDebug($"[Logo] Executing SetX: {command.Parameters["x"]}");
                        await MoveTo(gameState, (double)command.Parameters["x"], gameState.Turtle.Y);
                        break;
                        
                    case LogoCommandType.SetY:
                        LogDebug($"[Logo] Executing SetY: {command.Parameters["y"]}");
                        await MoveTo(gameState, gameState.Turtle.X, (double)command.Parameters["y"]);
                        break;
                        
                    case LogoCommandType.SetHeading:
                        LogDebug($"[Logo] Executing SetHeading: {command.Parameters["heading"]}");
                        gameState.Turtle.Heading = (double)command.Parameters["heading"];
                        // Notify turtle position change for immediate modes
                        if (gameState.RenderingMode != LogoRenderingMode.Batch)
                        {
                            gameState.OnTurtlePositionChanged?.Invoke(gameState.Turtle.Clone());
                        }
                        break;
                        
                    case LogoCommandType.Home:
                        LogDebug("[Logo] Executing Home");
                        await MoveTo(gameState, 250, 250);
                        gameState.Turtle.Heading = 0;
                        break;
                        
                    case LogoCommandType.ClearScreen:
                        LogDebug("[Logo] Executing ClearScreen");
                        gameState.DrawingElements.Clear();
                        
                        // For immediate modes, notify canvas operation
                        if (gameState.RenderingMode != LogoRenderingMode.Batch)
                        {
                            var clearOperation = new LogoCanvasOperation
                            {
                                Type = LogoCanvasOperationType.Clear
                            };
                            gameState.OnCanvasOperation?.Invoke(clearOperation);
                        }
                        
                        await MoveTo(gameState, 250, 250);
                        gameState.Turtle.Heading = 0;
                        break;
                        
                    case LogoCommandType.ShowTurtle:
                        LogDebug("[Logo] Executing ShowTurtle");
                        gameState.Turtle.IsVisible = true;
                        
                        // For immediate modes, notify canvas operation
                        if (gameState.RenderingMode != LogoRenderingMode.Batch)
                        {
                            var showOperation = new LogoCanvasOperation
                            {
                                Type = LogoCanvasOperationType.ShowTurtle
                            };
                            gameState.OnCanvasOperation?.Invoke(showOperation);
                        }
                        break;
                        
                    case LogoCommandType.HideTurtle:
                        LogDebug("[Logo] Executing HideTurtle");
                        gameState.Turtle.IsVisible = false;
                        
                        // For immediate modes, notify canvas operation
                        if (gameState.RenderingMode != LogoRenderingMode.Batch)
                        {
                            var hideOperation = new LogoCanvasOperation
                            {
                                Type = LogoCanvasOperationType.HideTurtle
                            };
                            gameState.OnCanvasOperation?.Invoke(hideOperation);
                        }
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
                        
                    case LogoCommandType.Wait:
                        var duration = (double)command.Parameters["duration"];
                        LogDebug($"[Logo] Executing Wait: {duration}");
                        await Task.Delay((int)(duration * 100)); // duration is in tenths of seconds
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
                Console.WriteLine($"[Logo] ERROR executing command {command.Type}: {ex.Message}");
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
            LogDebug($"[Logo] Executing for loop: {variable} from {start} to {end}");
            LogDebug($"[Logo] For loop code: '{code}'");
            
            try
            {
                for (double i = start; i <= end; i++)
                {
                    // Set the variable value
                    gameState.Variables[variable] = i;
                    LogDebug($"[Logo] For iteration: {variable} = {i}");
                    LogDebug($"[Logo] Current variables: {string.Join(", ", gameState.Variables.Select(v => $"{v.Key}={v.Value}"))}");
                    
                    // Execute the code with the current variable value
                    var commands = ParseLogoCode(code);
                    LogDebug($"[Logo] For iteration parsed {commands.Count} commands");
                    
                    foreach (var command in commands)
                    {
                        LogDebug($"[Logo] For iteration executing: {command.Type} - {command.OriginalText}");
                        await ExecuteCommandAsync(gameState, command);
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[Logo] ERROR in ExecuteFor: {ex.Message}");
                Console.WriteLine($"[Logo] ERROR in ExecuteFor: {ex.Message}");
                throw;
            }
            finally
            {
                // Clean up the variable after the loop
                if (gameState.Variables.ContainsKey(variable))
                {
                    gameState.Variables.Remove(variable);
                    LogDebug($"[Logo] Cleaned up variable: {variable}");
                }
            }
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
            
            LogDebug($"[Logo] Moving from ({oldX:F1}, {oldY:F1}) to ({turtle.X:F1}, {turtle.Y:F1}), heading: {turtle.Heading:F1}°");
            
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
                LogDebug($"[Logo] Added line element: ({oldX:F1}, {oldY:F1}) to ({turtle.X:F1}, {turtle.Y:F1}), color: {turtle.PenColor}");
                
                // For immediate/animated modes, notify drawing element created
                if (gameState.RenderingMode != LogoRenderingMode.Batch)
                {
                    gameState.OnDrawingElementCreated?.Invoke(line);
                }
            }
            
            // Notify turtle position change for immediate modes
            if (gameState.RenderingMode != LogoRenderingMode.Batch)
            {
                gameState.OnTurtlePositionChanged?.Invoke(turtle.Clone());
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
                LogDebug($"[Logo] Added MoveTo line element: ({oldX:F1}, {oldY:F1}) to ({x:F1}, {y:F1}), color: {turtle.PenColor}");
                
                // For immediate/animated modes, notify drawing element created
                if (gameState.RenderingMode != LogoRenderingMode.Batch)
                {
                    gameState.OnDrawingElementCreated?.Invoke(line);
                }
            }
            
            // Notify turtle position change for immediate modes
            if (gameState.RenderingMode != LogoRenderingMode.Batch)
            {
                gameState.OnTurtlePositionChanged?.Invoke(turtle.Clone());
            }
        }

        private async Task UpdateCanvasAsync(LogoGameState gameState)
        {
            try
            {
                LogDebug($"[Logo] Updating canvas with {gameState.DrawingElements.Count} drawing elements");
                
                // Call JavaScript to update the canvas
                await _jsRuntime.InvokeVoidAsync("logoDrawCanvas", gameState);
                
                LogDebug("[Logo] Canvas update completed successfully");
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

        // Method to toggle debug mode at runtime
        public void SetDebugMode(bool enabled)
        {
            // This would require making _debugMode non-readonly and adding logic to change it
            // For now, change the const _debugMode = false at the top to enable/disable
        }
    }
}