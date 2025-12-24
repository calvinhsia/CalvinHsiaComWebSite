// Conway's Game of Life - JavaScript support
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
    let nextCells = null;
    let rows = 0;
    let cols = 0;
    let cellSize = 4;

    // Colors
    const ALIVE_COLOR = '#00ff88';
    const DEAD_COLOR = '#1a1a2e';
    const GRID_COLOR = '#2a2a4e';

    // Performance tracking
    let generationCount = 0;
    let lastStatsTime = performance.now();
    let generationsThisSecond = 0;

    // Initialize canvas
    window.initLifeCanvas = function(canvasId, width, height) {
        canvas = document.getElementById(canvasId);
        if (!canvas) {
            console.error('[Life] Canvas not found:', canvasId);
            return;
        }

        ctx = canvas.getContext('2d', { alpha: false });
        canvasWidth = width;
        canvasHeight = height;
        canvas.width = width;
        canvas.height = height;

        console.log(`[Life v1] Canvas initialized: ${width}x${height}`);
    };

    // Set component reference for callbacks
    window.setLifeComponentRef = function(ref) {
        componentRef = ref;
        console.log('[Life v1] Component reference set');
    };

    // Setup resize handler
    window.setupLifeResize = function() {
        const resizeHandler = () => {
            if (window.resizeLifeCanvas) {
                window.resizeLifeCanvas(true);
            }
        };

        window.addEventListener('resize', debounce(resizeHandler, 250));
        console.log('[Life v1] Resize handler set up');
    };

    // Resize canvas to fit container
    window.resizeLifeCanvas = function(notifyComponent = true) {
        const section = document.querySelector('.life-canvas-section');
        if (!section || !canvas) return;

        const rect = section.getBoundingClientRect();
        const padding = 8;
        const newWidth = Math.floor(rect.width - padding);
        const newHeight = Math.floor(rect.height - padding);

        if (newWidth > 100 && newHeight > 100) {
            canvasWidth = newWidth;
            canvasHeight = newHeight;
            canvas.width = newWidth;
            canvas.height = newHeight;

            console.log(`[Life v1] Canvas resized: ${newWidth}x${newHeight}`);

            if (notifyComponent && componentRef) {
                componentRef.invokeMethodAsync('OnCanvasResized', newWidth, newHeight);
            }
        }
    };

    // Initialize the game world
    window.initLifeWorld = function(options) {
        rows = options.rows;
        cols = options.cols;
        cellSize = options.cellSize || 4;

        // Create cell arrays
        cells = new Uint8Array(rows * cols);
        nextCells = new Uint8Array(rows * cols);

        // Initialize with random cells if requested
        if (options.randomize) {
            const density = options.density || 0.25;
            for (let i = 0; i < cells.length; i++) {
                cells[i] = Math.random() < density ? 1 : 0;
            }
        }

        generationCount = 0;
        console.log(`[Life v1] World initialized: ${rows}x${cols} (${rows * cols} cells)`);

        renderFrame();
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
        console.log(`[Life v1] Pattern placed: ${pattern.name} at (${centerX}, ${centerY})`);
    };

    // Clear all cells
    window.clearLifeWorld = function() {
        if (cells) {
            cells.fill(0);
            generationCount = 0;
            renderFrame();
        }
        console.log('[Life v1] World cleared');
    };

    // Toggle cell at position
    window.toggleLifeCell = function(x, y) {
        if (!cells || x < 0 || x >= cols || y < 0 || y >= rows) return;

        const idx = y * cols + x;
        cells[idx] = cells[idx] ? 0 : 1;
        renderFrame();
    };

    // Set cell at position
    window.setLifeCell = function(x, y, alive) {
        if (!cells || x < 0 || x >= cols || y < 0 || y >= rows) return;

        const idx = y * cols + x;
        cells[idx] = alive ? 1 : 0;
    };

    // Start simulation
    window.startLifeSimulation = function(delayMs) {
        if (isRunning) return;

        isRunning = true;
        lastStatsTime = performance.now();
        generationsThisSecond = 0;

        const targetInterval = delayMs || 0;
        
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
        console.log(`[Life v1] Simulation started (delay: ${targetInterval}ms)`);
    };

    // Stop simulation
    window.stopLifeSimulation = function() {
        isRunning = false;
        if (animationId) {
            cancelAnimationFrame(animationId);
            animationId = null;
        }
        console.log('[Life v1] Simulation stopped');
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

    // Render the current state
    function renderFrame() {
        if (!ctx || !cells) return;

        // Clear canvas
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
        return { x, y };
    };

    // Render a pattern preview
    window.renderLifePatternPreview = function(previewCanvasId, pattern, scale) {
        const previewCanvas = document.getElementById(previewCanvasId);
        if (!previewCanvas || !pattern || !pattern.cells) return;

        const pctx = previewCanvas.getContext('2d');
        const patternRows = pattern.cells.length;
        const patternCols = pattern.cells[0].length;
        
        previewCanvas.width = patternCols * scale;
        previewCanvas.height = patternRows * scale;

        pctx.fillStyle = DEAD_COLOR;
        pctx.fillRect(0, 0, previewCanvas.width, previewCanvas.height);

        pctx.fillStyle = ALIVE_COLOR;
        for (let y = 0; y < patternRows; y++) {
            for (let x = 0; x < patternCols; x++) {
                if (pattern.cells[y][x]) {
                    pctx.fillRect(x * scale, y * scale, scale - 1, scale - 1);
                }
            }
        }
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

    console.log('[Life v1] life-game.js loaded');
})();
