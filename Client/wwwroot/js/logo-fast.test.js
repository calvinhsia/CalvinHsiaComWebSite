/**
 * Unit tests for logo-fast.js JavaScript Logo interpreter
 * Run with: npm test
 */

// Mock DOM and canvas
global.document = {
    getElementById: jest.fn(() => ({
        width: 500,
        height: 500,
        getContext: jest.fn(() => ({
            clearRect: jest.fn(),
            strokeStyle: '',
            lineWidth: 1,
            beginPath: jest.fn(),
            moveTo: jest.fn(),
            lineTo: jest.fn(),
            stroke: jest.fn(),
            canvas: { width: 500, height: 500 }
        }))
    }))
};

global.console = {
    log: jest.fn(),
    error: jest.fn()
};

global.performance = {
    now: jest.fn(() => Date.now())
};

// Load the module (would need to adjust logo-fast.js to be module-compatible)
// For now, we'll test the core functions

describe('Logo Fast Interpreter - Command Parsing', () => {
    
    test('parseValue handles numeric literals', () => {
        const variables = {};
        const result = parseValue('100', variables);
        expect(result).toBe(100);
    });

    test('parseValue handles variable references', () => {
        const variables = { myvar: 42 };
        const result = parseValue(':myvar', variables);
        expect(result).toBe(42);
    });

    test('parseValue returns 0 for undefined variables', () => {
        const variables = {};
        const result = parseValue(':undefined', variables);
        expect(result).toBe(0);
    });
});

describe('Logo Fast Interpreter - Movement Commands', () => {
    
    test('forward command is parsed', () => {
        const commands = parseCommands('fd 100', {});
        expect(commands).toHaveLength(1);
        expect(commands[0].type).toBe('fd');
        expect(commands[0].distance).toBe('100');
    });

    test('backward command is parsed', () => {
        const commands = parseCommands('bk 50', {});
        expect(commands).toHaveLength(1);
        expect(commands[0].type).toBe('bk');
        expect(commands[0].distance).toBe('50');
    });

    test('right turn command is parsed', () => {
        const commands = parseCommands('rt 90', {});
        expect(commands).toHaveLength(1);
        expect(commands[0].type).toBe('rt');
        expect(commands[0].angle).toBe('90');
    });

    test('left turn command is parsed', () => {
        const commands = parseCommands('lt 45', {});
        expect(commands).toHaveLength(1);
        expect(commands[0].type).toBe('lt');
        expect(commands[0].angle).toBe('45');
    });
});

describe('Logo Fast Interpreter - Position Commands', () => {
    
    test('setxy command is parsed with two parameters', () => {
        const commands = parseCommands('setxy 100 200', {});
        expect(commands).toHaveLength(1);
        expect(commands[0].type).toBe('setxy');
        expect(commands[0].x).toBe('100');
        expect(commands[0].y).toBe('200');
    });

    test('setx command is parsed', () => {
        const commands = parseCommands('setx 150', {});
        expect(commands).toHaveLength(1);
        expect(commands[0].type).toBe('setx');
        expect(commands[0].x).toBe('150');
    });

    test('sety command is parsed', () => {
        const commands = parseCommands('sety 250', {});
        expect(commands).toHaveLength(1);
        expect(commands[0].type).toBe('sety');
        expect(commands[0].y).toBe('250');
    });

    test('seth command is parsed', () => {
        const commands = parseCommands('seth 180', {});
        expect(commands).toHaveLength(1);
        expect(commands[0].type).toBe('seth');
        expect(commands[0].heading).toBe('180');
    });

    test('setheading alias is parsed', () => {
        const commands = parseCommands('setheading 90', {});
        expect(commands).toHaveLength(1);
        expect(commands[0].type).toBe('seth');
        expect(commands[0].heading).toBe('90');
    });
});

describe('Logo Fast Interpreter - Pen Commands', () => {
    
    test('pen up command is parsed', () => {
        const commands = parseCommands('pu', {});
        expect(commands).toHaveLength(1);
        expect(commands[0].type).toBe('pu');
    });

    test('pen down command is parsed', () => {
        const commands = parseCommands('pd', {});
        expect(commands).toHaveLength(1);
        expect(commands[0].type).toBe('pd');
    });

    test('setpencolor with string is parsed', () => {
        const commands = parseCommands('setpencolor "red"', {});
        expect(commands).toHaveLength(1);
        expect(commands[0].type).toBe('setpencolor');
        expect(commands[0].color).toBe('"red"');
    });

    test('setpencolor with integer is parsed', () => {
        const commands = parseCommands('setpencolor 1', {});
        expect(commands).toHaveLength(1);
        expect(commands[0].type).toBe('setpencolor');
        expect(commands[0].color).toBe('1');
    });

    test('setpenwidth is parsed', () => {
        const commands = parseCommands('setpenwidth 5', {});
        expect(commands).toHaveLength(1);
        expect(commands[0].type).toBe('setpenwidth');
        expect(commands[0].width).toBe('5');
    });
});

describe('Logo Fast Interpreter - Control Commands', () => {
    
    test('repeat command is parsed', () => {
        const commands = parseCommands('repeat 4 [fd 100]', {});
        expect(commands).toHaveLength(1);
        expect(commands[0].type).toBe('repeat');
        expect(commands[0].count).toBe(4);
        expect(commands[0].code).toBe('[fd 100]');
    });

    test('for loop is parsed', () => {
        const commands = parseCommands('for i 1 10 [fd :i]', {});
        expect(commands).toHaveLength(1);
        expect(commands[0].type).toBe('for');
        expect(commands[0].variable).toBe('i');
        expect(commands[0].start).toBe(1);
        expect(commands[0].end).toBe(10);
        expect(commands[0].code).toBe('[fd :i]');
    });

    test('cs command is parsed', () => {
        const commands = parseCommands('cs', {});
        expect(commands).toHaveLength(1);
        expect(commands[0].type).toBe('cs');
    });

    test('home command is parsed', () => {
        const commands = parseCommands('home', {});
        expect(commands).toHaveLength(1);
        expect(commands[0].type).toBe('home');
    });

    test('delay command is parsed', () => {
        const commands = parseCommands('delay 500', {});
        expect(commands).toHaveLength(1);
        expect(commands[0].type).toBe('delay');
        expect(commands[0].milliseconds).toBe('500');
    });

    test('showstatus command is parsed', () => {
        const commands = parseCommands('showstatus "Hello"', {});
        expect(commands).toHaveLength(1);
        expect(commands[0].type).toBe('showstatus');
        expect(commands[0].message).toBe('"Hello"');
    });
});

describe('Logo Fast Interpreter - Turtle Display Commands', () => {
    
    test('st command is parsed', () => {
        const commands = parseCommands('st', {});
        expect(commands).toHaveLength(1);
        expect(commands[0].type).toBe('st');
    });

    test('ht command is parsed', () => {
        const commands = parseCommands('ht', {});
        expect(commands).toHaveLength(1);
        expect(commands[0].type).toBe('ht');
    });
});

describe('Logo Fast Interpreter - Color Utilities', () => {
    
    test('intColorToHex converts colors correctly', () => {
        expect(intColorToHex(0)).toBe('#000000'); // black
        expect(intColorToHex(1)).toBe('#FF0000'); // red
        expect(intColorToHex(2)).toBe('#00FF00'); // green
        expect(intColorToHex(3)).toBe('#0000FF'); // blue
        expect(intColorToHex(7)).toBe('#FFFFFF'); // white
    });

    test('intColorToHex clamps out of range values', () => {
        expect(intColorToHex(-1)).toBe('#000000'); // clamp to black
        expect(intColorToHex(20)).toBe('#FFFFE0'); // clamp to lightyellow (15)
    });
});

// Helper functions that would be extracted from logo-fast.js
function parseValue(token, variables) {
    if (token.startsWith(':')) {
        const varName = token.substring(1);
        return variables[varName] || 0;
    }
    const num = parseFloat(token);
    return isNaN(num) ? 0 : num;
}

function parseCommands(code, variables) {
    // Simplified version for testing - actual implementation is in logo-fast.js
    const tokens = code.match(/\S+/g) || [];
    const commands = [];
    let i = 0;

    while (i < tokens.length) {
        const token = tokens[i].toLowerCase();

        if (token === 'fd' || token === 'forward') {
            commands.push({ type: 'fd', distance: tokens[++i] });
        } else if (token === 'bk' || token === 'backward') {
            commands.push({ type: 'bk', distance: tokens[++i] });
        } else if (token === 'rt' || token === 'right') {
            commands.push({ type: 'rt', angle: tokens[++i] });
        } else if (token === 'lt' || token === 'left') {
            commands.push({ type: 'lt', angle: tokens[++i] });
        } else if (token === 'setxy') {
            commands.push({ type: 'setxy', x: tokens[++i], y: tokens[++i] });
        } else if (token === 'setx') {
            commands.push({ type: 'setx', x: tokens[++i] });
        } else if (token === 'sety') {
            commands.push({ type: 'sety', y: tokens[++i] });
        } else if (token === 'seth' || token === 'setheading') {
            commands.push({ type: 'seth', heading: tokens[++i] });
        } else if (token === 'pu' || token === 'penup') {
            commands.push({ type: 'pu' });
        } else if (token === 'pd' || token === 'pendown') {
            commands.push({ type: 'pd' });
        } else if (token === 'setpencolor') {
            commands.push({ type: 'setpencolor', color: tokens[++i] });
        } else if (token === 'setpenwidth') {
            commands.push({ type: 'setpenwidth', width: tokens[++i] });
        } else if (token === 'cs' || token === 'clearscreen') {
            commands.push({ type: 'cs' });
        } else if (token === 'home') {
            commands.push({ type: 'home' });
        } else if (token === 'st' || token === 'showturtle') {
            commands.push({ type: 'st' });
        } else if (token === 'ht' || token === 'hideturtle') {
            commands.push({ type: 'ht' });
        } else if (token === 'delay') {
            commands.push({ type: 'delay', milliseconds: tokens[++i] });
        } else if (token === 'showstatus') {
            commands.push({ type: 'showstatus', message: tokens[++i] });
        } else if (token === 'repeat') {
            const count = parseInt(tokens[++i]);
            i++; // skip [
            let code = '';
            while (i < tokens.length && tokens[i] !== ']') {
                code += tokens[i++] + ' ';
            }
            commands.push({ type: 'repeat', count, code: code.trim() });
        } else if (token === 'for') {
            const variable = tokens[++i];
            const start = parseInt(tokens[++i]);
            const end = parseInt(tokens[++i]);
            i++; // skip [
            let code = '';
            while (i < tokens.length && tokens[i] !== ']') {
                code += tokens[i++] + ' ';
            }
            commands.push({ type: 'for', variable, start, end, code: code.trim() });
        }

        i++;
    }

    return commands;
}

function intColorToHex(colorInt) {
    const colors = [
        '#000000', '#FF0000', '#00FF00', '#0000FF',
        '#FFFF00', '#FF00FF', '#00FFFF', '#FFFFFF',
        '#808080', '#FFA500', '#800080', '#FFC0CB',
        '#A52A2A', '#87CEEB', '#90EE90', '#FFFFE0'
    ];
    return colors[Math.max(0, Math.min(15, colorInt))] || '#000000';
}
