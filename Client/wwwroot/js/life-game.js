// Conway's Game of Life - JavaScript support
// v4 - Fixed auto-start by matching Fish pattern

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
    let nextCells = null;
    let rows = 0;
    let cols = 0;
    let cellSize = 4;

    // Colors
    const ALIVE_COLOR = '#00ff88';
    const DEAD_COLOR = '#1a1a2e';

    // Performance tracking
    let generationCount = 0;
    let lastStatsTime = performance.now();
    let generationsThisSecond = 0;

    // Track if first resize callback has been sent (for auto-start)
    let firstResizeCallbackSent = false;
    let resizeRetryCount = 0;
    const MAX_RESIZE_RETRIES = 20;

    console.log('[Life v4] life-game.js loading...');

    // Helper to safely invoke C# methods
    function safeInvoke(methodName, ...args) {
        if (!componentRef) {
            console.warn(`[Life v4] Cannot invoke ${methodName}: component ref not set`);
            return Promise.resolve();
        }
        
        return componentRef.invokeMethodAsync(methodName, ...args)
            .catch(err => {
                if (err.message && err.message.includes('disposed')) {
                    console.warn(`[Life v4] Component was disposed, clearing ref`);
                    componentRef = null;
                    isRunning = false;
                    if (animationId !== null) {
                        cancelAnimationFrame(animationId);
                        animationId = null;
                    }
                } else {
                    console.error(`[Life v4] Error invoking ${methodName}:`, err);
                }
            });
    }

    // Initialize canvas
    window.initLifeCanvas = function(canvasId, width, height) {
        canvas = document.getElementById(canvasId);
        if (!canvas) {
            console.error('[Life v4] Canvas not found:', canvasId);
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

        console.log(`[Life v4] Canvas initialized: ${width}x${height}`);
    };

    // Set component reference for callbacks
    window.setLifeComponentRef = function(ref) {
        componentRef = ref;
        console.log('[Life v4] Component reference set');
        
        // Trigger resize if not yet done (matches Fish pattern)
        if (!firstResizeCallbackSent) {
            console.log('[Life v4] Triggering initial resize after component ref set');
            setTimeout(() => {
                if (window.resizeLifeCanvas && !firstResizeCallbackSent) {
                    window.resizeLifeCanvas();
                }
            }, 50);
        }
    };

    // Setup resize handler
    window.setupLifeResize = function() {
        console.log('[Life v4] Setting up resize listener');
        
        firstResizeCallbackSent = false;
        resizeRetryCount = 0;

        window.addEventListener('resize', debounce(() => {
            if (window.resizeLifeCanvas) {
                window.resizeLifeCanvas();
            }
        }, 250));
        
        console.log('[Life v4] Resize handler set up');
    };

    // Resize canvas to fit container - always notifies component for first resize
    window.resizeLifeCanvas = function() {
        const section = document.querySelector('.life-canvas-section');
        if (!section || !canvas) {
            console.log('[Life v4] resizeLifeCanvas: section or canvas not found');
            return;
        }

        const sectionWidth = section.clientWidth;
        const sectionHeight = section.clientHeight;

        console.log(`[Life v4] Resize check: section=${sectionWidth}x${sectionHeight}, firstCallbackSent=${firstResizeCallbackSent}`);

        // Retry if section too small (layout not ready)
        if (sectionWidth < 100 || sectionHeight < 100) {
            resizeRetryCount++;
            if (resizeRetryCount <= MAX_RESIZE_RETRIES) {
                const delay = Math.min(100 * resizeRetryCount, 500);
                console.warn(`[Life v4] Section dimensions too small, retry ${resizeRetryCount}/${MAX_RESIZE_RETRIES} in ${delay}ms...`);
                setTimeout(() => window.resizeLifeCanvas(), delay);
            } else {
                console.error('[Life v4] Max retries reached, using fallback dimensions');
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
            console.log(`[Life v4] Resizing canvas: ${newWidth}x${newHeight}`);
            canvasWidth = newWidth;
            canvasHeight = newHeight;
            canvas.width = newWidth;
            canvas.height = newHeight;
            
            if (componentRef) {
                firstResizeCallbackSent = true;
                console.log('[Life v4] Invoking OnCanvasResized callback');
                safeInvoke('OnCanvasResized', newWidth, newHeight)
                    .then(() => console.log('[Life v4] OnCanvasResized callback completed'));
            } else {
                console.warn('[Life v4] Component ref not set yet, retrying in 50ms...');
                setTimeout(() => window.resizeLifeCanvas(), 50);
            }
        }
    }

    // Initialize the game world
    window.initLifeWorld = function(options) {
        cellSize = options.cellSize || 4;
        
        // Calculate rows/cols to fill the entire canvas
        rows = Math.floor(canvasHeight / cellSize);
        cols = Math.floor(canvasWidth / cellSize);

        console.log(`[Life v4] initLifeWorld: canvas=${canvasWidth}x${canvasHeight}, grid=${cols}x${rows}, cellSize=${cellSize}`);

        // Create cell arrays
        cells = new Uint8Array(rows * cols);
        nextCells = new Uint8Array(rows * cols);

        // Initialize with random cells if requested
        if (options.randomize) {
            const density = options.density || 0.25;
            let aliveCount = 0;
            for (let i = 0; i < cells.length; i++) {
                if (Math.random() < density) {
                    cells[i] = 1;
                    aliveCount++;
                }
            }
            console.log(`[Life v4] Randomized with density ${(density * 100).toFixed(0)}%: ${aliveCount} alive cells`);
        }

        generationCount = 0;
        console.log(`[Life v4] World initialized: ${rows}x${cols} (${rows * cols} cells)`);

        renderFrame();
        
        // Return actual dimensions for C# to know
        return { rows, cols };
    };

    // Place a pattern at specified location
    window.placeLifePattern = function(pattern, centerX, centerY) {
        if (!cells || !pattern || !pattern.cells) return;

        const patternRows = pattern.cells.length;
        const patternCols = pattern.cells[0].length;
        const startX = centerX - Math.floor(patternCols / 2);
        const startY = centerY - Math.floor(patternRows / 2);

        for (let py = 0; py < patternRows; py++) {
            for (let px = 0; px < patternCols; px++) {
                const x = startX + px;
                const y = startY + py;

                if (x >= 0 && x < cols && y >= 0 && y < rows) {
                    const idx = y * cols + x;
                    cells[idx] = pattern.cells[py][px] ? 1 : 0;
                }
            }
        }

        renderFrame();
        console.log(`[Life v4] Pattern placed: ${pattern.name} at (${centerX}, ${centerY})`);
    };

    // Clear all cells
    window.clearLifeWorld = function() {
        if (cells) {
            cells.fill(0);
            generationCount = 0;
            renderFrame();
        }
        console.log('[Life v4] World cleared');
    };

    // Toggle cell at position - works while running
    window.toggleLifeCell = function(x, y) {
        if (!cells || x < 0 || x >= cols || y < 0 || y >= rows) return;

        const idx = y * cols + x;
        cells[idx] = cells[idx] ? 0 : 1;
        
        // Only render immediately if not running (running will render on next frame)
        if (!isRunning) {
            renderFrame();
        }
    };

    // Set cell at position - works while running
    window.setLifeCell = function(x, y, alive) {
        if (!cells || x < 0 || x >= cols || y < 0 || y >= rows) return;

        const idx = y * cols + x;
        cells[idx] = alive ? 1 : 0;
        
        // Only render immediately if not running (running will render on next frame)
        if (!isRunning) {
            renderFrame();
        }
    };

    // Start simulation
    window.startLifeSimulation = function(delayMs) {
        if (isRunning) {
            console.log('[Life v4] Already running, ignoring start');
            return;
        }

        isRunning = true;
        lastStatsTime = performance.now();
        generationsThisSecond = 0;

        const targetInterval = delayMs || 0;
        
        console.log(`[Life v4] Starting simulation (delay: ${targetInterval}ms)`);

        function gameLoop(timestamp) {
            if (!isRunning) return;

            const elapsed = timestamp - lastFrameTime;
            
            if (elapsed >= targetInterval) {
                lastFrameTime = timestamp;
                doGeneration();
                renderFrame();
                
                // Update stats
                generationsThisSecond++;
                const now = performance.now();
                if (now - lastStatsTime >= 500) {
                    const gps = generationsThisSecond / ((now - lastStatsTime) / 1000);
                    if (componentRef) {
                        const aliveCount = countAliveCells();
                        componentRef.invokeMethodAsync('OnGenerationComplete', aliveCount, generationCount, gps);
                    }
                    generationsThisSecond = 0;
                    lastStatsTime = now;
                }
            }

            animationId = requestAnimationFrame(gameLoop);
        }

        animationId = requestAnimationFrame(gameLoop);
    };

    // Stop simulation
    window.stopLifeSimulation = function() {
        isRunning = false;
        if (animationId) {
            cancelAnimationFrame(animationId);
            animationId = null;
        }
        console.log('[Life v4] Simulation stopped');
    };

    // Step one generation
    window.stepLifeGeneration = function() {
        doGeneration();
        renderFrame();
        
        if (componentRef) {
            const aliveCount = countAliveCells();
            componentRef.invokeMethodAsync('OnGenerationComplete', aliveCount, generationCount, 0);
        }
    };

    // Perform one generation using Game of Life rules
    function doGeneration() {
        generationCount++;

        for (let y = 0; y < rows; y++) {
            for (let x = 0; x < cols; x++) {
                const idx = y * cols + x;
                const neighbors = countNeighbors(x, y);
                const alive = cells[idx];

                // Conway's Game of Life rules:
                // 1. Any live cell with 2 or 3 neighbors survives
                // 2. Any dead cell with exactly 3 neighbors becomes alive
                // 3. All other cells die or stay dead
                if (alive) {
                    nextCells[idx] = (neighbors === 2 || neighbors === 3) ? 1 : 0;
                } else {
                    nextCells[idx] = (neighbors === 3) ? 1 : 0;
                }
            }
        }

        // Swap buffers
        const temp = cells;
        cells = nextCells;
        nextCells = temp;
    }

    // Count live neighbors (with wrapping for torus topology)
    function countNeighbors(x, y) {
        let count = 0;

        for (let dy = -1; dy <= 1; dy++) {
            for (let dx = -1; dx <= 1; dx++) {
                if (dx === 0 && dy === 0) continue;

                // Wrap around (torus topology)
                const nx = (x + dx + cols) % cols;
                const ny = (y + dy + rows) % rows;
                
                count += cells[ny * cols + nx];
            }
        }

        return count;
    }

    // Count total alive cells
    function countAliveCells() {
        let count = 0;
        for (let i = 0; i < cells.length; i++) {
            count += cells[i];
        }
        return count;
    }

    // Render the current state - fills entire canvas
    function renderFrame() {
        if (!ctx || !cells) return;

        // Clear entire canvas with dead color
        ctx.fillStyle = DEAD_COLOR;
        ctx.fillRect(0, 0, canvasWidth, canvasHeight);

        // Draw alive cells
        ctx.fillStyle = ALIVE_COLOR;
        
        for (let y = 0; y < rows; y++) {
            for (let x = 0; x < cols; x++) {
                if (cells[y * cols + x]) {
                    ctx.fillRect(x * cellSize, y * cellSize, cellSize - 1, cellSize - 1);
                }
            }
        }
    }

    // Get cell coordinates from canvas position
    window.getLifeCellFromPosition = function(canvasX, canvasY) {
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
    window.getLifeCanvasRect = function() {
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
    window.cleanupLife = function() {
        console.log('[Life v4] Cleaning up...');
        isRunning = false;
        if (animationId) {
            cancelAnimationFrame(animationId);
            animationId = null;
        }
        componentRef = null;
    };

    console.log('[Life v4] life-game.js loaded');
})();
