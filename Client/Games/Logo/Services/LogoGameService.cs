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
                gameState.LastError = "";
                gameState.IsRunning = true;

                // Parse and execute the Logo code
                var commands = ParseLogoCode(code);
                
                foreach (var command in commands)
                {
                    await ExecuteCommandAsync(gameState, command);
                    
                    // Add small delay for visual effect
                    await Task.Delay(10);
                }

                gameState.IsRunning = false;
                
                // Update the canvas
                await UpdateCanvasAsync(gameState);
                
                return true;
            }
            catch (Exception ex)
            {
                gameState.LastError = ex.Message;
                gameState.IsRunning = false;
                return false;
            }
        }

        private List<LogoCommand> ParseLogoCode(string code)
        {
            var commands = new List<LogoCommand>();
            var lines = code.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith(";"))
                    continue; // Skip empty lines and comments

                var parsedCommands = ParseLine(trimmedLine);
                commands.AddRange(parsedCommands);
            }
            
            return commands;
        }

        private List<LogoCommand> ParseLine(string line)
        {
            var commands = new List<LogoCommand>();
            
            // Handle repeat command specially
            if (line.StartsWith("repeat", StringComparison.OrdinalIgnoreCase))
            {
                var repeatCommand = ParseRepeatCommand(line);
                if (repeatCommand != null)
                    commands.Add(repeatCommand);
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
            // Parse: repeat 4 [fd 100 rt 90]
            var match = Regex.Match(line, @"repeat\s+(\d+)\s*\[(.*?)\]", RegexOptions.IgnoreCase);
            if (!match.Success)
                return null;

            var count = int.Parse(match.Groups[1].Value);
            var innerCode = match.Groups[2].Value;

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
                _ => null
            };
        }

        private LogoCommand ParseMovementCommand(LogoCommandType type, string[] tokens, ref int index)
        {
            index++; // Move past the command
            if (index < tokens.Length && double.TryParse(tokens[index], out double distance))
            {
                return new LogoCommand
                {
                    Type = type,
                    Parameters = new Dictionary<string, object> { ["distance"] = distance },
                    OriginalText = $"{tokens[index - 1]} {tokens[index]}"
                };
            }
            throw new InvalidOperationException($"Invalid distance parameter for {type}");
        }

        private LogoCommand ParseTurnCommand(LogoCommandType type, string[] tokens, ref int index)
        {
            index++; // Move past the command
            if (index < tokens.Length && double.TryParse(tokens[index], out double angle))
            {
                return new LogoCommand
                {
                    Type = type,
                    Parameters = new Dictionary<string, object> { ["angle"] = angle },
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
            gameState.CommandHistory.Add(command);
            
            switch (command.Type)
            {
                case LogoCommandType.Forward:
                    await MoveForward(gameState, (double)command.Parameters["distance"]);
                    break;
                    
                case LogoCommandType.Backward:
                    await MoveBackward(gameState, (double)command.Parameters["distance"]);
                    break;
                    
                case LogoCommandType.Right:
                    TurnRight(gameState, (double)command.Parameters["angle"]);
                    break;
                    
                case LogoCommandType.Left:
                    TurnLeft(gameState, (double)command.Parameters["angle"]);
                    break;
                    
                case LogoCommandType.PenUp:
                    gameState.Turtle.PenDown = false;
                    break;
                    
                case LogoCommandType.PenDown:
                    gameState.Turtle.PenDown = true;
                    break;
                    
                case LogoCommandType.SetPenColor:
                    gameState.Turtle.PenColor = (string)command.Parameters["color"];
                    break;
                    
                case LogoCommandType.SetPenWidth:
                    gameState.Turtle.PenWidth = (double)command.Parameters["width"];
                    break;
                    
                case LogoCommandType.SetXY:
                    await MoveTo(gameState, (double)command.Parameters["x"], (double)command.Parameters["y"]);
                    break;
                    
                case LogoCommandType.SetX:
                    await MoveTo(gameState, (double)command.Parameters["x"], gameState.Turtle.Y);
                    break;
                    
                case LogoCommandType.SetY:
                    await MoveTo(gameState, gameState.Turtle.X, (double)command.Parameters["y"]);
                    break;
                    
                case LogoCommandType.SetHeading:
                    gameState.Turtle.Heading = (double)command.Parameters["heading"];
                    break;
                    
                case LogoCommandType.Home:
                    await MoveTo(gameState, 250, 250);
                    gameState.Turtle.Heading = 0;
                    break;
                    
                case LogoCommandType.ClearScreen:
                    gameState.DrawingElements.Clear();
                    await MoveTo(gameState, 250, 250);
                    gameState.Turtle.Heading = 0;
                    break;
                    
                case LogoCommandType.ShowTurtle:
                    gameState.Turtle.IsVisible = true;
                    break;
                    
                case LogoCommandType.HideTurtle:
                    gameState.Turtle.IsVisible = false;
                    break;
                    
                case LogoCommandType.Repeat:
                    await ExecuteRepeat(gameState, (int)command.Parameters["count"], (string)command.Parameters["code"]);
                    break;
                    
                case LogoCommandType.Wait:
                    var duration = (double)command.Parameters["duration"];
                    await Task.Delay((int)(duration * 100)); // duration is in tenths of seconds
                    break;
            }
        }

        private async Task ExecuteRepeat(LogoGameState gameState, int count, string code)
        {
            for (int i = 0; i < count; i++)
            {
                var commands = ParseLogoCode(code);
                foreach (var command in commands)
                {
                    await ExecuteCommandAsync(gameState, command);
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
            
            // If pen is down, add a line to the drawing
            if (turtle.PenDown)
            {
                gameState.DrawingElements.Add(new LogoDrawingElement
                {
                    Type = LogoDrawingType.Line,
                    StartX = oldX,
                    StartY = oldY,
                    EndX = turtle.X,
                    EndY = turtle.Y,
                    Color = turtle.PenColor,
                    Width = turtle.PenWidth
                });
            }
        }

        private async Task MoveBackward(LogoGameState gameState, double distance)
        {
            await MoveForward(gameState, -distance);
        }

        private void TurnRight(LogoGameState gameState, double angle)
        {
            gameState.Turtle.Heading += angle;
            gameState.Turtle.Heading = gameState.Turtle.Heading % 360;
        }

        private void TurnLeft(LogoGameState gameState, double angle)
        {
            gameState.Turtle.Heading -= angle;
            if (gameState.Turtle.Heading < 0)
                gameState.Turtle.Heading += 360;
        }

        private async Task MoveTo(LogoGameState gameState, double x, double y)
        {
            var turtle = gameState.Turtle;
            var oldX = turtle.X;
            var oldY = turtle.Y;
            
            turtle.X = x;
            turtle.Y = y;
            
            // If pen is down, draw a line
            if (turtle.PenDown)
            {
                gameState.DrawingElements.Add(new LogoDrawingElement
                {
                    Type = LogoDrawingType.Line,
                    StartX = oldX,
                    StartY = oldY,
                    EndX = turtle.X,
                    EndY = turtle.Y,
                    Color = turtle.PenColor,
                    Width = turtle.PenWidth
                });
            }
        }

        private async Task UpdateCanvasAsync(LogoGameState gameState)
        {
            try
            {
                // Call JavaScript to update the canvas
                await _jsRuntime.InvokeVoidAsync("logoDrawCanvas", gameState);
            }
            catch (Exception ex)
            {
                // Handle JS interop errors
                gameState.LastError = $"Canvas update error: {ex.Message}";
            }
        }

        public string GetCurrentPosition(LogoGameState gameState)
        {
            return $"X: {gameState.Turtle.X:F1}, Y: {gameState.Turtle.Y:F1}, Heading: {gameState.Turtle.Heading:F1}°";
        }
    }
}