// Fast Logo interpreter in pure JavaScript
// Executes Logo code without C# interop for maximum performance

// Global debug state for Logo - can be controlled by C#
window.logoDebug = {
    enabled: false
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

window.executeLogoCodeInJS = function (code) {
    debugLog('?????????????????????????????????????????????');
    debugLog('Executing in pure JavaScript mode');
    debugLog('Input code:', code);

    try {
        const canvas = document.getElementById('logoCanvas');
        if (!canvas) {
            debugError('? Canvas not found');
            return false;
        }

        debugLog('? Canvas found:', canvas.width, 'x', canvas.height);

        const ctx = canvas.getContext('2d');

        // Turtle state
        const turtle = {
            x: 250,
            y: 250,
            heading: 0,
            penDown: true,
            penColor: '#000000',
            penWidth: 1
        };

        debugLog('Initial turtle state:', JSON.stringify(turtle));

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
        executeCommands(commands, turtle, ctx, variables);
        const executionTime = performance.now() - executionStart;

        debugLog('? Execution complete in', executionTime.toFixed(2), 'ms');
        debugLog('Final turtle state:', JSON.stringify(turtle));
        debugLog('Final variables:', JSON.stringify(variables));
        debugLog('?????????????????????????????????????????????');
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
            } else {
                // Has brackets - extract until matching ]
                while (depth > 0 && i < tokens.length) {
                    const t = tokens[i++];
                    if (t === '[') depth++;
                    else if (t === ']') depth--;
                    if (depth > 0) blockTokens.push(t); // Don't include the closing ]
                }
            }

            const cmd = {
                type: 'for',
                variable: varName,
                start: start,
                end: end,
                code: blockTokens.join(' ')
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
            } else {
                // Has brackets - extract until matching ]
                while (depth > 0 && i < tokens.length) {
                    const t = tokens[i++];
                    if (t === '[') depth++;
                    else if (t === ']') depth--;
                    if (depth > 0) blockTokens.push(t); // Don't include the closing ]
                }
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
        else if (token === 'cs' || token === 'clearscreen') {
            debugLog('  ? Parsed CS command');
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
        else if (token === '[' || token === ']') {
            // Skip loose brackets - they're just delimiters
            debugLog('  ?? Skipping bracket:', token);
        }
        else {
            debugLog('?? Unknown token, skipping:', token);
        }

        i++;
    }

    debugLog('parseCommands - Returning', commands.length, 'commands');
    return commands;
}

function tokenize(code) {
    // Simple tokenizer - split by whitespace but respect brackets
    return code.match(/\S+/g) || [];
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

function executeCommands(commands, turtle, ctx, variables) {
    debugLog('executeCommands -', commands.length, 'commands');
    for (let i = 0; i < commands.length; i++) {
        const cmd = commands[i];
        debugLog('  Executing command', i, ':', cmd.type);
        executeCommand(cmd, turtle, ctx, variables);
    }
}

function executeCommand(cmd, turtle, ctx, variables) {
    switch (cmd.type) {
        case 'for':
            debugLog('    FOR loop:', cmd.variable, 'from', cmd.start, 'to', cmd.end);
            for (let i = cmd.start; i <= cmd.end; i++) {
                variables[cmd.variable] = i;
                debugLog('      Loop iteration', i);
                const loopCommands = parseCommands(cmd.code, variables);
                debugLog('      Loop parsed', loopCommands.length, 'commands');
                executeCommands(loopCommands, turtle, ctx, variables);
            }
            delete variables[cmd.variable];
            debugLog('    FOR loop complete');
            break;

        case 'repeat':
            debugLog('    REPEAT', cmd.count, 'times');
            for (let i = 0; i < cmd.count; i++) {
                debugLog('      Repeat iteration', i + 1, 'of', cmd.count);
                const repeatCommands = parseCommands(cmd.code, variables);
                executeCommands(repeatCommands, turtle, ctx, variables);
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

        case 'cs':
            debugLog('    CS (clear screen)');
            ctx.clearRect(0, 0, ctx.canvas.width, ctx.canvas.height);
            turtle.x = 250;
            turtle.y = 250;
            turtle.heading = 0;
            break;

        case 'home':
            debugLog('    HOME');
            turtle.x = 250;
            turtle.y = 250;
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
        debugLog('      ??? Drew line:', ctx.strokeStyle, 'width', ctx.lineWidth);
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
    return colors[Math.max(0, Math.min(15, colorInt))] || '#000000';
}

// Basic initialization log - always shown
console.log('[Logo-Fast] ? Fast JavaScript Logo engine loaded');
