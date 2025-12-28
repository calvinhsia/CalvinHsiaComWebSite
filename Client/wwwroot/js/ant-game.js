// Langton's Ant - JavaScript support
// v1 - Initial implementation

(function() {
    'use strict';

    let canvas = null;
    let ctx = null;
    let canvasWidth = 800;
    let canvasHeight = 600;
    let componentRef = null;
    let animationId = null;
    let isRunning = false;
    let lastFrameTime = 0;

    // Grid state
    let cells = null;
    let rows = 0;
    let cols = 0;
    let cellSize = 4;

    // Ant state (can have multiple ants)
    let ants = [];

    // Direction constants: 0=North, 1=East, 2=South, 3=West
    const DIRECTIONS = [
        { dx: 0, dy: -1 },  // North
        { dx: 1, dy: 0 },   // East
        { dx: 0, dy: 1 },   // South
        { dx: -1, dy: 0 }   // West
    ];

    // Colors - support for multi-color rules
    let ruleString = 'RL'; // Classic Langton's Ant
    let colors = [];

    // Default color palette for multi-state
    const COLOR_PALETTE = [
        '#1a1a2e', // State 0 - dark (like dead)
        '#00ff88', // State 1 - green
        '#ff6b6b', // State 2 - red
        '#4ecdc4', // State 3 - teal
        '#ffe66d', // State 4 - yellow
        '#95e1d3', // State 5 - mint
        '#f38181', // State 6 - coral
        '#aa96da', // State 7 - lavender
        '#fcbad3', // State 8 - pink
        '#a8d8ea', // State 9 - light blue
        '#ff9a3c', // State 10 - orange
        '#155263'  // State 11 - dark teal
    ];

    const ANT_COLOR = '#ff0000';
    const BACKGROUND_COLOR = '#1a1a2e';

    // Performance tracking
    let stepCount = 0;
    let lastStatsTime = performance.now();
    let stepsThisSecond = 0;

    // Steps per frame for speed control
    let stepsPerFrame = 1;

    // Track if first resize callback has been sent
    let firstResizeCallbackSent = false;
    let resizeRetryCount = 0;
    const MAX_RESIZE_RETRIES = 20;

    // Wrap mode
    let wrapEdges = true;

    console.log('[Ant v1] ant-game.js loading...');

    // Helper to safely invoke C# methods
    function safeInvoke(methodName, ...args) {
        if (!componentRef) {
            console.warn(`[Ant v1] Cannot invoke ${methodName}: component ref not set`);
            return Promise.resolve();
        }
        
        return componentRef.invokeMethodAsync(methodName, ...args)
            .catch(err => {
                if (err.message && err.message.includes('disposed')) {
                    console.warn(`[Ant v1] Component was disposed, clearing ref`);
                    componentRef = null;
                    isRunning = false;
                    if (animationId !== null) {
                        cancelAnimationFrame(animationId);
                        animationId = null;
                    }
                } else {
                    console.error(`[Ant v1] Error invoking ${methodName}:`, err);
                }
            });
    }

    // Initialize canvas
    window.initAntCanvas = function(canvasId, width, height) {
        canvas = document.getElementById(canvasId);
        if (!canvas) {
            console.error('[Ant v1] Canvas not found:', canvasId);
            return;
        }

        // Reset state
        firstResizeCallbackSent = false;
        resizeRetryCount = 0;
        isRunning = false;
        if (animationId) {
            cancelAnimationFrame(animationId);
            animationId = null;
        }

        ctx = canvas.getContext('2d', { alpha: false });
        canvasWidth = width;
        canvasHeight = height;
        canvas.width = width;
        canvas.height = height;

        console.log(`[Ant v1] Canvas initialized: ${width}x${height}`);
    };

    // Set component reference for callbacks
    window.setAntComponentRef = function(ref) {
        componentRef = ref;
        console.log('[Ant v1] Component reference set');
        
        if (!firstResizeCallbackSent) {
            console.log('[Ant v1] Triggering initial resize after component ref set');
            setTimeout(() => {
                if (window.resizeAntCanvas && !firstResizeCallbackSent) {
                    window.resizeAntCanvas();
                }
            }, 50);
        }
    };

    // Setup resize handler
    window.setupAntResize = function() {
        console.log('[Ant v1] Setting up resize listener');
        
        firstResizeCallbackSent = false;
        resizeRetryCount = 0;

        window.addEventListener('resize', debounce(() => {
            if (window.resizeAntCanvas) {
                window.resizeAntCanvas();
            }
        }, 250));
        
        console.log('[Ant v1] Resize handler set up');
    };

    // Resize canvas to fit container
    window.resizeAntCanvas = function() {
        const section = document.querySelector('.ant-canvas-section');
        if (!section || !canvas) {
            console.log('[Ant v1] resizeAntCanvas: section or canvas not found');
            return;
        }

        const sectionWidth = section.clientWidth;
        const sectionHeight = section.clientHeight;

        console.log(`[Ant v1] Resize check: section=${sectionWidth}x${sectionHeight}, firstCallbackSent=${firstResizeCallbackSent}`);

        if (sectionWidth < 100 || sectionHeight < 100) {
            resizeRetryCount++;
            if (resizeRetryCount <= MAX_RESIZE_RETRIES) {
                const delay = Math.min(100 * resizeRetryCount, 500);
                console.warn(`[Ant v1] Section dimensions too small, retry ${resizeRetryCount}/${MAX_RESIZE_RETRIES} in ${delay}ms...`);
                setTimeout(() => window.resizeAntCanvas(), delay);
            } else {
                console.error('[Ant v1] Max retries reached, using fallback dimensions');
                const fallbackWidth = Math.max(window.innerWidth - 40, 300);
                const fallbackHeight = Math.max(window.innerHeight - 200, 300);
                invokeResize(fallbackWidth, fallbackHeight);
            }
            return;
        }

        resizeRetryCount = 0;
        
        const padding = 8;
        const newWidth = Math.floor(sectionWidth - padding);
        const newHeight = Math.floor(sectionHeight - padding);
        
        invokeResize(newWidth, newHeight);
    };
    
    function invokeResize(newWidth, newHeight) {
        const widthChanged = Math.abs(canvas.width - newWidth) > 2;
        const heightChanged = Math.abs(canvas.height - newHeight) > 2;
        const needsCallback = !firstResizeCallbackSent || widthChanged || heightChanged;
        
        if (needsCallback) {
            console.log(`[Ant v1] Resizing canvas: ${newWidth}x${newHeight}`);
            canvasWidth = newWidth;
            canvasHeight = newHeight;
            canvas.width = newWidth;
            canvas.height = newHeight;
            
            if (componentRef) {
                firstResizeCallbackSent = true;
                console.log('[Ant v1] Invoking OnCanvasResized callback');
                safeInvoke('OnCanvasResized', newWidth, newHeight)
                    .then(() => console.log('[Ant v1] OnCanvasResized callback completed'));
            } else {
                console.warn('[Ant v1] Component ref not set yet, retrying in 50ms...');
                setTimeout(() => window.resizeAntCanvas(), 50);
            }
        }
    }

    // Initialize the game world
    window.initAntWorld = function(options) {
        cellSize = options.cellSize || 4;
        ruleString = options.rule || 'RL';
        wrapEdges = options.wrapEdges !== false;
        stepsPerFrame = options.stepsPerFrame || 1;
        
        // Calculate rows/cols to fill the entire canvas
        rows = Math.floor(canvasHeight / cellSize);
        cols = Math.floor(canvasWidth / cellSize);

        console.log(`[Ant v1] initAntWorld: canvas=${canvasWidth}x${canvasHeight}, grid=${cols}x${rows}, rule=${ruleString}`);

        // Create cell array - each cell stores its color state (0 to ruleString.length-1)
        cells = new Uint8Array(rows * cols);

        // Setup colors based on rule length
        colors = [];
        for (let i = 0; i < ruleString.length; i++) {
            colors.push(COLOR_PALETTE[i % COLOR_PALETTE.length]);
        }

        // Initialize ant(s) at center
        ants = [];
        if (options.antCount && options.antCount > 1) {
            // Multiple ants in different positions
            for (let i = 0; i < options.antCount; i++) {
                const angle = (2 * Math.PI * i) / options.antCount;
                const radius = Math.min(rows, cols) / 4;
                ants.push({
                    x: Math.floor(cols / 2 + Math.cos(angle) * radius),
                    y: Math.floor(rows / 2 + Math.sin(angle) * radius),
                    dir: i % 4 // Different starting directions
                });
            }
        } else {
            // Single ant at center
            ants.push({
                x: Math.floor(cols / 2),
                y: Math.floor(rows / 2),
                dir: 0 // Facing North
            });
        }

        stepCount = 0;
        console.log(`[Ant v1] World initialized: ${rows}x${cols} (${rows * cols} cells), ${ants.length} ant(s), rule: ${ruleString}`);

        renderFrame();
        
        return { rows, cols };
    };

    // Clear all cells and reset ant
    window.clearAntWorld = function() {
        if (cells) {
            cells.fill(0);
            stepCount = 0;
            
            // Reset ant to center
            ants = [{
                x: Math.floor(cols / 2),
                y: Math.floor(rows / 2),
                dir: 0
            }];
            
            renderFrame();
        }
        console.log('[Ant v1] World cleared');
    };

    // Update rule string
    window.setAntRule = function(rule) {
        ruleString = rule || 'RL';
        
        // Update colors
        colors = [];
        for (let i = 0; i < ruleString.length; i++) {
            colors.push(COLOR_PALETTE[i % COLOR_PALETTE.length]);
        }
        
        console.log `[Ant v1] Rule changed to: ${ruleString}`;
    };

    // Set steps per frame
    window.setAntStepsPerFrame = function(steps) {
        stepsPerFrame = Math.max(1, steps);
        console.log(`[Ant v1] Steps per frame: ${stepsPerFrame}`);
    };

    // Set wrap mode
    window.setAntWrapMode = function(wrap) {
        wrapEdges = wrap;
        console.log(`[Ant v1] Wrap edges: ${wrapEdges}`);
    };

    // Add an ant at position
    window.addAnt = function(x, y, dir) {
        if (x >= 0 && x < cols && y >= 0 && y < rows) {
            ants.push({ x, y, dir: dir || 0 });
            renderFrame();
            console.log(`[Ant v1] Added ant at (${x}, ${y})`);
        }
    };

    // Toggle cell at position
    window.toggleAntCell = function(x, y) {
        if (!cells || x < 0 || x >= cols || y < 0 || y >= rows) return;

        const idx = y * cols + x;
        cells[idx] = (cells[idx] + 1) % ruleString.length;
        
        if (!isRunning) {
            renderFrame();
        }
    };

    // Start simulation
    window.startAntSimulation = function(delayMs) {
        if (isRunning) {
            console.log('[Ant v1] Already running, ignoring start');
            return;
        }

        isRunning = true;
        lastStatsTime = performance.now();
        stepsThisSecond = 0;

        const targetInterval = delayMs || 0;
        
        console.log(`[Ant v1] Starting simulation (delay: ${targetInterval}ms, stepsPerFrame: ${stepsPerFrame})`);

        function gameLoop(timestamp) {
            if (!isRunning) return;

            const elapsed = timestamp - lastFrameTime;
            
            if (elapsed >= targetInterval) {
                lastFrameTime = timestamp;
                
                // Do multiple steps per frame for speed
                for (let i = 0; i < stepsPerFrame; i++) {
                    doStep();
                }
                
                renderFrame();
                
                // Update stats
                stepsThisSecond += stepsPerFrame;
                const now = performance.now();
                if (now - lastStatsTime >= 500) {
                    const sps = stepsThisSecond / ((now - lastStatsTime) / 1000);
                    if (componentRef) {
                        const coloredCount = countColoredCells();
                        componentRef.invokeMethodAsync('OnStepComplete', coloredCount, stepCount, sps, ants.length);
                    }
                    stepsThisSecond = 0;
                    lastStatsTime = now;
                }
            }

            animationId = requestAnimationFrame(gameLoop);
        }

        animationId = requestAnimationFrame(gameLoop);
    };

    // Stop simulation
    window.stopAntSimulation = function() {
        isRunning = false;
        if (animationId) {
            cancelAnimationFrame(animationId);
            animationId = null;
        }
        console.log('[Ant v1] Simulation stopped');
    };

    // Step one generation
    window.stepAntGeneration = function() {
        doStep();
        renderFrame();
        
        if (componentRef) {
            const coloredCount = countColoredCells();
            componentRef.invokeMethodAsync('OnStepComplete', coloredCount, stepCount, 0, ants.length);
        }
    };

    // Perform one step for all ants
    function doStep() {
        stepCount++;

        for (let ant of ants) {
            const idx = ant.y * cols + ant.x;
            const currentState = cells[idx];
            
            // Get the rule for current state (R = turn right, L = turn left)
            const rule = ruleString[currentState] || 'R';
            
            // Turn based on rule
            if (rule === 'R' || rule === 'r') {
                ant.dir = (ant.dir + 1) % 4; // Turn right (clockwise)
            } else if (rule === 'L' || rule === 'l') {
                ant.dir = (ant.dir + 3) % 4; // Turn left (counter-clockwise)
            } else if (rule === 'U' || rule === 'u') {
                ant.dir = (ant.dir + 2) % 4; // U-turn
            } else if (rule === 'N' || rule === 'n') {
                // No turn - continue straight
            }
            
            // Flip the cell to next state
            cells[idx] = (currentState + 1) % ruleString.length;
            
            // Move forward
            const dir = DIRECTIONS[ant.dir];
            ant.x += dir.dx;
            ant.y += dir.dy;
            
            // Handle edges
            if (wrapEdges) {
                ant.x = (ant.x + cols) % cols;
                ant.y = (ant.y + rows) % rows;
            } else {
                // Bounce off edges
                if (ant.x < 0) { ant.x = 0; ant.dir = 1; }
                if (ant.x >= cols) { ant.x = cols - 1; ant.dir = 3; }
                if (ant.y < 0) { ant.y = 0; ant.dir = 2; }
                if (ant.y >= rows) { ant.y = rows - 1; ant.dir = 0; }
            }
        }
    }

    // Count non-zero cells
    function countColoredCells() {
        let count = 0;
        for (let i = 0; i < cells.length; i++) {
            if (cells[i] > 0) count++;
        }
        return count;
    }

    // Render the current state
    function renderFrame() {
        if (!ctx || !cells) return;

        // Clear entire canvas with background color
        ctx.fillStyle = BACKGROUND_COLOR;
        ctx.fillRect(0, 0, canvasWidth, canvasHeight);

        // Draw colored cells
        for (let y = 0; y < rows; y++) {
            for (let x = 0; x < cols; x++) {
                const state = cells[y * cols + x];
                if (state > 0) {
                    ctx.fillStyle = colors[state] || colors[1];
                    ctx.fillRect(x * cellSize, y * cellSize, cellSize - 1, cellSize - 1);
                }
            }
        }

        // Draw ants
        ctx.fillStyle = ANT_COLOR;
        for (let ant of ants) {
            // Draw ant as a larger square or triangle
            const ax = ant.x * cellSize;
            const ay = ant.y * cellSize;
            const size = Math.max(cellSize, 3);
            
            // Draw ant body
            ctx.fillRect(ax, ay, size, size);
            
            // Draw direction indicator (small triangle)
            ctx.fillStyle = '#ffffff';
            ctx.beginPath();
            const cx = ax + size / 2;
            const cy = ay + size / 2;
            const r = size / 3;
            
            switch (ant.dir) {
                case 0: // North
                    ctx.moveTo(cx, cy - r);
                    ctx.lineTo(cx - r/2, cy + r/2);
                    ctx.lineTo(cx + r/2, cy + r/2);
                    break;
                case 1: // East
                    ctx.moveTo(cx + r, cy);
                    ctx.lineTo(cx - r/2, cy - r/2);
                    ctx.lineTo(cx - r/2, cy + r/2);
                    break;
                case 2: // South
                    ctx.moveTo(cx, cy + r);
                    ctx.lineTo(cx - r/2, cy - r/2);
                    ctx.lineTo(cx + r/2, cy - r/2);
                    break;
                case 3: // West
                    ctx.moveTo(cx - r, cy);
                    ctx.lineTo(cx + r/2, cy - r/2);
                    ctx.lineTo(cx + r/2, cy + r/2);
                    break;
            }
            ctx.closePath();
            ctx.fill();
            ctx.fillStyle = ANT_COLOR;
        }
    }

    // Get cell coordinates from canvas position
    window.getAntCellFromPosition = function(canvasX, canvasY) {
        const x = Math.floor(canvasX / cellSize);
        const y = Math.floor(canvasY / cellSize);
        return { x: Math.min(Math.max(x, 0), cols - 1), y: Math.min(Math.max(y, 0), rows - 1) };
    };

    // Utility: debounce function
    function debounce(func, wait) {
        let timeout;
        return function executedFunction(...args) {
            const later = () => {
                clearTimeout(timeout);
                func(...args);
            };
            clearTimeout(timeout);
            timeout = setTimeout(later, wait);
        };
    }

    // Get bounding rect helper
    window.getAntCanvasRect = function() {
        if (!canvas) return null;
        const rect = canvas.getBoundingClientRect();
        return {
            left: rect.left,
            top: rect.top,
            width: rect.width,
            height: rect.height
        };
    };

    // Cleanup when leaving page
    window.cleanupAnt = function() {
        console.log('[Ant v1] Cleaning up...');
        isRunning = false;
        if (animationId) {
            cancelAnimationFrame(animationId);
            animationId = null;
        }
        componentRef = null;
    };

    console.log('[Ant v1] ant-game.js loaded');
})();
