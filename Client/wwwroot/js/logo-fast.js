// Fast Logo interpreter in pure JavaScript
// Executes Logo code without C# interop for maximum performance

// Global debug state for Logo - can be controlled by C#
window.logoDebug = {
    enabled: false
};

// Reference to the LogoGame component for callbacks
window.logoComponentRef = null;

// Function to set the component reference from C#
window.setLogoComponentReference = function (dotNetRef) {
    window.logoComponentRef = dotNetRef;
    debugLog('Logo component reference set');
};

// Debug logging functions - only log when debug is enabled
function debugLog(message, ...args) {
    if (window.logoDebug.enabled) {
        console.log(`[Logo-Fast] ${message}`, ...args);
    }
}

function debugError(message, ...args) {
    // Always log errors regardless of debug mode
    console.error(`[Logo-Fast] ${message}`, ...args);
}

// Function to set debug mode from C#
window.setLogoDebug = function (enabled) {
    window.logoDebug.enabled = enabled;

    if (enabled) {
        console.log('[Logo-Fast] Debug mode enabled - JavaScript will now log debug information');
    } else {
        console.log('[Logo-Fast] Debug mode disabled - JavaScript logging reduced');
    }

    return window.logoDebug;
};

window.executeLogoCodeInJS = async function (code) {
    debugLog('??????????????????????????????????????????????????');
    debugLog('Executing in pure JavaScript mode');
    debugLog('Input code:', code);

    // Reset cancellation flag
    window.logoExecutionCancelled = false;

    try {
        const canvas = document.getElementById('logoCanvas');
        if (!canvas) {
            debugError('? Canvas not found');
            return false;
        }

        debugLog('? Canvas found:', canvas.width, 'x', canvas.height);

        const ctx = canvas.getContext('2d');

        // Calculate canvas center for turtle starting position
        const centerX = canvas.width / 2;
        const centerY = canvas.height / 2;

        // Turtle state
        const turtle = {
            x: centerX,
            y: centerY,
            heading: 0,
            penDown: true,
            penColor: '#000000',
            penWidth: 1
        };

        debugLog('Initial turtle state:', JSON.stringify(turtle));
        debugLog('Canvas center:', centerX, ',', centerY);

        const variables = {};

        // Clear canvas
        ctx.clearRect(0, 0, canvas.width, canvas.height);
        debugLog('Canvas cleared');

        // Parse and execute
        debugLog('Starting parse...');
        const commands = parseLogoCode(code, variables);
        debugLog('Parsed', commands.length, 'top-level commands');

        debugLog('Starting execution...');
        const executionStart = performance.now();
        await executeCommands(commands, turtle, ctx, variables, canvas);

        // Check if execution was cancelled
        if (window.logoExecutionCancelled) {
            debugLog('? Execution cancelled by user');
            debugLog('??????????????????????????????????????????????????');
            return false;
        }

        const executionTime = performance.now() - executionStart;

        debugLog('? Execution complete in', executionTime.toFixed(2), 'ms');
        debugLog('Final turtle state:', JSON.stringify(turtle));
        debugLog('Final variables:', JSON.stringify(variables));
        debugLog('??????????????????????????????????????????????????');
        return true;
    } catch (error) {
        debugError('? Execution error:', error);
        debugError('Stack:', error.stack);
        return false;
    }
};

function parseLogoCode(code, variables) {
    debugLog('parseLogoCode - Input length:', code.length);

    // Remove comments
    const lines = code.split('\n')
        .map((line, idx) => {
            const commentIdx = line.indexOf(';');
            if (commentIdx >= 0) {
                const beforeComment = line.substring(0, commentIdx);
                const openBracket = beforeComment.indexOf('[');
                const closeBracket = line.lastIndexOf(']');

                if (openBracket >= 0 && commentIdx > openBracket && (closeBracket < 0 || commentIdx < closeBracket)) {
                    // Comment inside brackets, keep whole line
                    debugLog('Line', idx, '(comment inside brackets):', line);
                    return line;
                }
                debugLog('  Line', idx, '(removed comment):', beforeComment);
                return beforeComment;
            }
            if (line.trim().length > 0) {
                debugLog('  Line', idx, '(no comment):', line);
            }
            return line;
        })
        .filter(line => line.trim().length > 0)
        .join('\n');

    debugLog('After comment removal:', lines);
    return parseCommands(lines, variables);
}

function parseCommands(code, variables) {
    debugLog('parseCommands - Input:', code);
    const commands = [];
    const tokens = tokenize(code);
    debugLog('Tokens:', tokens);
    let i = 0;

    while (i < tokens.length) {
        const token = tokens[i].toLowerCase();
        debugLog('  Processing token', i, ':', token);

        if (token === 'for') {
            // for variable start end [code]
            const varName = tokens[++i];
            const start = parseValue(tokens[++i], variables);
            const end = parseValue(tokens[++i], variables);

            debugLog('    FOR command details:');
            debugLog('      Variable:', varName);
            debugLog('      Start:', start);
            debugLog('      End:', end);
            debugLog('      Next token index:', i + 1, 'token:', tokens[i + 1]);

            // Skip the opening bracket if present
            if (tokens[i + 1] === '[') {
                i++;
                debugLog('      Found opening bracket at index', i);
            }
            i++; // move past '[' or first token  
            const blockTokens = [];
            let depth = 1;
            let foundOpenBracket = (tokens[i - 1] === '[');

            debugLog('      Found open bracket:', foundOpenBracket);
            debugLog('      Starting to extract block tokens from index', i);

            if (!foundOpenBracket) {
                // No brackets - single line code, take everything until end of line
                while (i < tokens.length) {
                    debugLog('        Adding token (no bracket):', tokens[i]);
                    blockTokens.push(tokens[i++]);
                }
                i--; // Adjust because while loop will increment
            } else {
                // Has brackets - extract until matching ]
                while (depth > 0 && i < tokens.length) {
                    const t = tokens[i++];
                    debugLog('        Token at', i - 1, ':', t, '(depth:', depth, ')');
                    if (t === '[') {
                        depth++;
                        debugLog('          Increased depth to', depth);
                    }
                    else if (t === ']') {
                        depth--;
                        debugLog('          Decreased depth to', depth);
                    }
                    if (depth > 0) {
                        blockTokens.push(t); // Don't include the closing ]
                        debugLog('          Added to blockTokens');
                    }
                }
                i--; // After loop, i is one past ], adjust so while loop's i++ positions correctly
            }

            const code = blockTokens.join(' ');
            debugLog('      Extracted block tokens:', blockTokens.length, 'tokens');
            debugLog('      Block code:', code);
            debugLog('      Current i after extraction:', i, '(will become', i + 1, 'after while loop increment)');

            const cmd = {
                type: 'for',
                variable: varName,
                start: start,
                end: end,
                code: code
            };
            debugLog('  ? Parsed FOR command:', JSON.stringify(cmd));
            commands.push(cmd);
        }
        else if (token === 'repeat') {
            const count = parseValue(tokens[++i], variables);

            // Skip the opening bracket if present
            if (tokens[i + 1] === '[') {
                i++;
            }
            i++; // move past '[' or first token
            const blockTokens = [];
            let depth = 1;
            let foundOpenBracket = (tokens[i - 1] === '[');

            if (!foundOpenBracket) {
                // No brackets - single line code, take everything until end of line
                while (i < tokens.length) {
                    blockTokens.push(tokens[i++]);
                }
                i--; // Adjust because while loop will increment
            } else {
                // Has brackets - extract until matching ]
                while (depth > 0 && i < tokens.length) {
                    const t = tokens[i++];
                    if (t === '[') depth++;
                    else if (t === ']') depth--;
                    if (depth > 0) blockTokens.push(t); // Don't include the closing ]
                }
                i--; // After loop, i is one past ], adjust so while loop's i++ positions correctly
            }

            const cmd = {
                type: 'repeat',
                count: count,
                code: blockTokens.join(' ')
            };
            debugLog('  ? Parsed REPEAT command:', JSON.stringify(cmd));
            commands.push(cmd);
        }
        else if (token === 'fd' || token === 'forward') {
            const cmd = { type: 'fd', distance: tokens[++i] };
            debugLog('  ? Parsed FD command:', JSON.stringify(cmd));
            commands.push(cmd);
        }
        else if (token === 'bk' || token === 'backward') {
            const cmd = { type: 'bk', distance: tokens[++i] };
            debugLog('  ? Parsed BK command:', JSON.stringify(cmd));
            commands.push(cmd);
        }
        else if (token === 'rt' || token === 'right') {
            const cmd = { type: 'rt', angle: tokens[++i] };
            debugLog('  ? Parsed RT command:', JSON.stringify(cmd));
            commands.push(cmd);
        }
        else if (token === 'lt' || token === 'left') {
            const cmd = { type: 'lt', angle: tokens[++i] };
            debugLog('  ? Parsed LT command:', JSON.stringify(cmd));
            commands.push(cmd);
        }
        else if (token === 'setpencolor') {
            const cmd = { type: 'setpencolor', color: tokens[++i] };
            debugLog('  ? Parsed SETPENCOLOR command:', JSON.stringify(cmd));
            commands.push(cmd);
        }
        else if (token === 'setpenwidth') {
            const cmd = { type: 'setpenwidth', width: tokens[++i] };
            debugLog('  ? Parsed SETPENWIDTH command:', JSON.stringify(cmd));
            commands.push(cmd);
        }
        else if (token === 'setxy') {
            const cmd = { type: 'setxy', x: tokens[++i], y: tokens[++i] };
            debugLog('  ? Parsed SETXY command:', JSON.stringify(cmd));
            commands.push(cmd);
        }
        else if (token === 'setx') {
            const cmd = { type: 'setx', x: tokens[++i] };
            debugLog('  ? Parsed SETX command:', JSON.stringify(cmd));
            commands.push(cmd);
        }
        else if (token === 'sety') {
            const cmd = { type: 'sety', y: tokens[++i] };
            debugLog('  ? Parsed SETY command:', JSON.stringify(cmd));
            commands.push(cmd);
        }
        else if (token === 'seth' || token === 'setheading') {
            const cmd = { type: 'seth', heading: tokens[++i] };
            debugLog('  ? Parsed SETH command:', JSON.stringify(cmd));
            commands.push(cmd);
        }
        else if (token === 'st' || token === 'showturtle') {
            debugLog('  ? Parsed ST command');
            commands.push({ type: 'st' });
        }
        else if (token === 'ht' || token === 'hideturtle') {
            debugLog('  ? Parsed HT command');
            commands.push({ type: 'ht' });
        }
        else if (token === 'cs' || token === 'clearscreen') {
            debugLog('  ? Parsed CS command - CLEAR SCREEN DETECTED');
            commands.push({ type: 'cs' });
        }
        else if (token === 'home') {
            debugLog('  ? Parsed HOME command');
            commands.push({ type: 'home' });
        }
        else if (token === 'pu' || token === 'penup') {
            debugLog('  ? Parsed PU command');
            commands.push({ type: 'pu' });
        }
        else if (token === 'pd' || token === 'pendown') {
            debugLog('  ? Parsed PD command');
            commands.push({ type: 'pd' });
        }
        else if (token === 'delay') {
            const cmd = { type: 'delay', milliseconds: tokens[++i] };
            debugLog('  ? Parsed DELAY command:', JSON.stringify(cmd));
            commands.push(cmd);
        }
        else if (token === 'showstatus') {
            // showstatus can take a string literal, variable, or number
            i++; // Move to the parameter
            let param = tokens[i];
            
            // Handle case where colon and variable name are separate tokens (e.g., ": angle")
            if (param === ':' && i + 1 < tokens.length) {
                param = ':' + tokens[++i]; // Combine : with next token
            }
            
            const cmd = { type: 'showstatus', message: param };
            debugLog('  ? Parsed SHOWSTATUS command:', JSON.stringify(cmd));
            commands.push(cmd);
        }
        else if (token === 'let') {
            // let varname value
            // value can be a literal number, variable reference, or expression
            const varName = tokens[++i]; // No quotes needed with 'let'
            i++; // Move to value expression
            
            // Collect tokens for the value expression (could be simple value or expression like :colorstep + 1)
            const valueTokens = [];
            
            // Check if next token starts an expression
            if (i < tokens.length) {
                valueTokens.push(tokens[i]);
                
                // Check for arithmetic operators (peek ahead for + - * /)
                while (i + 1 < tokens.length && ['+', '-', '*', '/'].includes(tokens[i + 1])) {
                    valueTokens.push(tokens[++i]); // operator
                    if (i + 1 < tokens.length) {
                        valueTokens.push(tokens[++i]); // operand
                    }
                }
            }
            
            const cmd = { type: 'let', varName: varName, valueTokens: valueTokens };
            debugLog('  ? Parsed LET command:', JSON.stringify(cmd));
            commands.push(cmd);
        }
        else if (token === '[' || token === ']') {
            // Skip loose brackets - they're just delimiters
            debugLog('  ? Skipping bracket:', token);
        }
        else {
            debugLog('? Unknown token, skipping:', token);
        }

        i++;
    }

    debugLog('parseCommands - Returning', commands.length, 'commands');
    return commands;
}

function tokenize(code) {
    // Tokenizer that properly separates brackets from other characters
    // Split on whitespace, but also split brackets into separate tokens
    const tokens = [];
    const rawTokens = code.match(/\S+/g) || [];

    for (const token of rawTokens) {
        // Check if token contains brackets
        if (token.includes('[') || token.includes(']')) {
            // Split brackets into separate tokens
            let current = '';
            for (let i = 0; i < token.length; i++) {
                const char = token[i];
                if (char === '[' || char === ']') {
                    // Push accumulated characters
                    if (current.length > 0) {
                        tokens.push(current);
                        current = '';
                    }
                    // Push bracket as separate token
                    tokens.push(char);
                } else {
                    current += char;
                }
            }
            // Push any remaining characters
            if (current.length > 0) {
                tokens.push(current);
            }
        } else {
            // No brackets, just push the token as-is
            tokens.push(token);
        }
    }

    return tokens;
}

function parseValue(token, variables) {
    if (token.startsWith(':')) {
        const varName = token.substring(1);
        const value = variables[varName] || 0;
        debugLog('    Variable', varName, '=', value);
        return value;
    }
    const num = parseFloat(token);
    const result = isNaN(num) ? 0 : num;
    debugLog('    Literal value:', token, '=', result);
    return result;
}

async function executeCommands(commands, turtle, ctx, variables, canvas) {
    debugLog('executeCommands -', commands.length, 'commands');
    for (let i = 0; i < commands.length; i++) {
        // Check for cancellation before each command
        if (window.logoExecutionCancelled) {
            debugLog('? Execution cancelled - stopping command loop');
            return;
        }

        const cmd = commands[i];
        debugLog('  Executing command', i, ':', cmd.type);
        await executeCommand(cmd, turtle, ctx, variables, canvas);
    }
}

async function executeCommand(cmd, turtle, ctx, variables, canvas) {
    // Check for cancellation at start of each command
    if (window.logoExecutionCancelled) {
        debugLog('? Execution cancelled - skipping command');
        return;
    }

    switch (cmd.type) {
        case 'for':
            debugLog('    FOR loop:', cmd.variable, 'from', cmd.start, 'to', cmd.end);
            for (let i = cmd.start; i <= cmd.end; i++) {
                // Check cancellation in loop
                if (window.logoExecutionCancelled) {
                    debugLog('? Execution cancelled - stopping FOR loop');
                    return;
                }

                variables[cmd.variable] = i;
                debugLog('      Loop iteration', i);
                const loopCommands = parseCommands(cmd.code, variables);
                debugLog('      Loop parsed', loopCommands.length, 'commands');
                await executeCommands(loopCommands, turtle, ctx, variables, canvas);
            }
            delete variables[cmd.variable];
            debugLog('    FOR loop complete');
            break;

        case 'repeat':
            debugLog('    REPEAT', cmd.count, 'times');
            for (let i = 0; i < cmd.count; i++) {
                // Check cancellation in loop
                if (window.logoExecutionCancelled) {
                    debugLog('? Execution cancelled - stopping REPEAT loop');
                    return;
                }

                debugLog('      Repeat iteration', i + 1, 'of', cmd.count);
                const repeatCommands = parseCommands(cmd.code, variables);
                await executeCommands(repeatCommands, turtle, ctx, variables, canvas);
            }
            debugLog('    REPEAT complete');
            break;

        case 'fd':
            const fdDist = parseValue(cmd.distance, variables);
            debugLog('    FD', fdDist);
            forward(turtle, ctx, fdDist);
            break;

        case 'bk':
            const bkDist = parseValue(cmd.distance, variables);
            debugLog('    BK', bkDist);
            forward(turtle, ctx, -bkDist);
            break;

        case 'rt':
            const rtAngle = parseValue(cmd.angle, variables);
            debugLog('    RT', rtAngle, '(before:', turtle.heading, ')');
            turtle.heading += rtAngle;
            turtle.heading = turtle.heading % 360;
            debugLog('    RT result:', turtle.heading);
            break;

        case 'lt':
            const ltAngle = parseValue(cmd.angle, variables);
            debugLog('    LT', ltAngle, '(before:', turtle.heading, ')');
            turtle.heading -= ltAngle;
            if (turtle.heading < 0) turtle.heading += 360;
            debugLog('    LT result:', turtle.heading);
            break;

        case 'setpencolor':
            const colorValue = parseValue(cmd.color, variables);
            if (Number.isInteger(colorValue)) {
                turtle.penColor = intColorToHex(Math.floor(colorValue));
                debugLog('    SETPENCOLOR (int)', colorValue, '?', turtle.penColor);
            } else {
                turtle.penColor = cmd.color.replace(/["']/g, '');
                debugLog('    SETPENCOLOR (name)', turtle.penColor);
            }
            break;

        case 'setpenwidth':
            turtle.penWidth = parseValue(cmd.width, variables);
            debugLog('    SETPENWIDTH', turtle.penWidth);
            break;

        case 'setxy':
            const newX = parseValue(cmd.x, variables);
            const newY = parseValue(cmd.y, variables);
            debugLog('    SETXY', newX, newY, '(pen', turtle.penDown ? 'DOWN' : 'UP', ')');
            const oldX = turtle.x;
            const oldY = turtle.y;
            turtle.x = newX;
            turtle.y = newY;
            if (turtle.penDown) {
                ctx.strokeStyle = turtle.penColor;
                ctx.lineWidth = turtle.penWidth;
                ctx.beginPath();
                ctx.moveTo(oldX, oldY);
                ctx.lineTo(turtle.x, turtle.y);
                ctx.stroke();
                debugLog('      Drew line during setxy from', oldX, oldY, 'to', turtle.x, turtle.y);
            }
            break;

        case 'setx':
            const setX = parseValue(cmd.x, variables);
            debugLog('    SETX', setX, '(pen', turtle.penDown ? 'DOWN' : 'UP', ')');
            const oldSetX = turtle.x;
            turtle.x = setX;
            if (turtle.penDown) {
                ctx.strokeStyle = turtle.penColor;
                ctx.lineWidth = turtle.penWidth;
                ctx.beginPath();
                ctx.moveTo(oldSetX, turtle.y);
                ctx.lineTo(turtle.x, turtle.y);
                ctx.stroke();
            }
            break;

        case 'sety':
            const setY = parseValue(cmd.y, variables);
            debugLog('    SETY', setY, '(pen', turtle.penDown ? 'DOWN' : 'UP', ')');
            const oldSetY = turtle.y;
            turtle.y = setY;
            if (turtle.penDown) {
                ctx.strokeStyle = turtle.penColor;
                ctx.lineWidth = turtle.penWidth;
                ctx.beginPath();
                ctx.moveTo(turtle.x, oldSetY);
                ctx.lineTo(turtle.x, turtle.y);
                ctx.stroke();
            }
            break;

        case 'seth':
            const newHeading = parseValue(cmd.heading, variables);
            debugLog('    SETH', newHeading, '(before:', turtle.heading, ')');
            turtle.heading = newHeading % 360;
            if (turtle.heading < 0) turtle.heading += 360;
            debugLog('    SETH result:', turtle.heading);
            break;

        case 'st':
            debugLog('    ST (show turtle) - not implemented in JS fast mode');
            // Could implement turtle drawing here if desired
            break;

        case 'ht':
            debugLog('    HT (hide turtle) - not implemented in JS fast mode');
            // Could implement turtle hiding here if desired
            break;

        case 'cs':
            debugLog('    CS (clear screen) - EXECUTING NOW');
            debugLog('    Canvas dimensions:', ctx.canvas.width, 'x', ctx.canvas.height);
            debugLog('    Canvas state before clear - checking if has content...');
            ctx.clearRect(0, 0, ctx.canvas.width, ctx.canvas.height);
            debugLog('    ? Canvas cleared via clearRect()');
            // Reset turtle to center of canvas
            turtle.x = canvas.width / 2;
            turtle.y = canvas.height / 2;
            turtle.heading = 0;
            debugLog('    Turtle reset to home:', turtle.x, turtle.y, 'heading:', turtle.heading);
            debugLog('    Adding 16ms delay for browser repaint...');
            // Add small delay to allow browser to repaint after clear
            await new Promise(resolve => setTimeout(resolve, 16)); // ~60fps
            debugLog('    ? CS complete - canvas should now be clear and visible');
            break;

        case 'home':
            debugLog('    HOME');
            // Reset turtle to center of canvas
            turtle.x = canvas.width / 2;
            turtle.y = canvas.height / 2;
            turtle.heading = 0;
            break;

        case 'pu':
            debugLog('    PU (pen up)');
            turtle.penDown = false;
            break;

        case 'pd':
            debugLog('    PD (pen down)');
            turtle.penDown = true;
            break;

        case 'delay':
            const ms = parseValue(cmd.milliseconds, variables);
            debugLog('    DELAY', ms, 'ms - waiting...');
            await new Promise(resolve => setTimeout(resolve, ms));
            debugLog('    DELAY complete');
            break;

        case 'showstatus':
            let statusMessage;
            
            // Check if it's a string literal (quoted)
            if (cmd.message.startsWith('"') || cmd.message.startsWith("'")) {
                statusMessage = cmd.message.replace(/["']/g, '');
            } else if (cmd.message.startsWith(':')) {
                // Variable reference
                const varName = cmd.message.substring(1);
                const value = variables[varName];
                statusMessage = value !== undefined ? String(value) : '';
            } else {
                // Try to parse as number
                const numValue = parseFloat(cmd.message);
                statusMessage = isNaN(numValue) ? cmd.message : String(numValue);
            }
            
            debugLog('    SHOWSTATUS:', statusMessage);
            
            // Call back to C# to update status
            if (window.logoComponentRef) {
                try {
                    await window.logoComponentRef.invokeMethodAsync('ShowStatusFromLogo', statusMessage);
                } catch (error) {
                    debugError('Failed to call ShowStatusFromLogo:', error);
                }
            }
            break;

        case 'let':
            // Evaluate the expression in valueTokens
            let value = 0;
            
            if (cmd.valueTokens.length === 1) {
                // Simple value
                value = parseValue(cmd.valueTokens[0], variables);
            } else if (cmd.valueTokens.length >= 3) {
                // Expression like :colorstep + 1
                const leftOperand = parseValue(cmd.valueTokens[0], variables);
                const operator = cmd.valueTokens[1];
                const rightOperand = parseValue(cmd.valueTokens[2], variables);
                
                switch (operator) {
                    case '+':
                        value = leftOperand + rightOperand;
                        break;
                    case '-':
                        value = leftOperand - rightOperand;
                        break;
                    case '*':
                        value = leftOperand * rightOperand;
                        break;
                    case '/':
                        value = rightOperand !== 0 ? leftOperand / rightOperand : 0;
                        break;
                    default:
                        value = leftOperand;
                }
                
                // Handle additional operations if present
                for (let i = 3; i < cmd.valueTokens.length; i += 2) {
                    if (i + 1 < cmd.valueTokens.length) {
                        const nextOp = cmd.valueTokens[i];
                        const nextOperand = parseValue(cmd.valueTokens[i + 1], variables);
                        
                        switch (nextOp) {
                            case '+': value += nextOperand; break;
                            case '-': value -= nextOperand; break;
                            case '*': value *= nextOperand; break;
                            case '/': value = nextOperand !== 0 ? value / nextOperand : value; break;
                        }
                    }
                }
            }
            
            variables[cmd.varName] = value;
            debugLog('    LET', cmd.varName, '=', value);
            debugLog('    Current variables:', JSON.stringify(variables));
            break;

        default:
            debugLog('    ?? Unknown command type:', cmd.type);
    }
}

function forward(turtle, ctx, distance) {
    const oldX = turtle.x;
    const oldY = turtle.y;

    const radians = (turtle.heading - 90) * Math.PI / 180;
    turtle.x += distance * Math.cos(radians);
    turtle.y += distance * Math.sin(radians);

    debugLog('      FORWARD', distance, ':',
        'from (', oldX.toFixed(1), ',', oldY.toFixed(1), ')',
        'to (', turtle.x.toFixed(1), ',', turtle.y.toFixed(1), ')',
        'heading', turtle.heading.toFixed(1), '°',
        'pen', turtle.penDown ? 'DOWN' : 'UP');

    if (turtle.penDown) {
        ctx.strokeStyle = turtle.penColor;
        ctx.lineWidth = turtle.penWidth;
        ctx.beginPath();
        ctx.moveTo(oldX, oldY);
        ctx.lineTo(turtle.x, turtle.y);
        ctx.stroke();
        debugLog('      ?? Drew line:', ctx.strokeStyle, 'width', ctx.lineWidth);
    }
}

function intColorToHex(colorInt) {
    const colors = [
        '#000000', // 0 black
        '#FF0000', // 1 red
        '#00FF00', // 2 green
        '#0000FF', // 3 blue
        '#FFFF00', // 4 yellow
        '#FF00FF', // 5 magenta
        '#00FFFF', // 6 cyan
        '#FFFFFF', // 7 white
        '#808080', // 8 gray
        '#FFA500', // 9 orange
        '#800080', // 10 purple
        '#FFC0CB', // 11 pink
        '#A52A2A', // 12 brown
        '#87CEEB', // 13 lightblue
        '#90EE90', // 14 lightgreen
        '#FFFFE0'  // 15 lightyellow
    ];
    // Use modulo to cycle through all available colors
    const index = Math.abs(Math.floor(colorInt)) % colors.length;
    return colors[index];
}

// Basic initialization log - always shown
console.log('[Logo-Fast] ? Fast JavaScript Logo engine loaded');
