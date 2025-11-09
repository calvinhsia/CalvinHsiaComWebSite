// Fish vs Sharks Cellular Automata Game JavaScript
console.log('[Fish] fish-game.js loading... v11 (Android delay fix)');

(function () {
    'use strict';

    let fishCanvas = null;
    let fishCtx = null;
    let fishResizeTimeout = null;
    let fishWorker = null;
    let isRunning = false;
    let animationFrameId = null;
    let renderSettings = null;

    // Delay timing tracking for Android-compatible delays
    let lastTickTime = 0;
    let currentDelayMs = 0;

    window.initFishCanvas = function (canvasId, width, height) {
        console.log(`[Fish JS] Initializing canvas: ${canvasId}`);
        fishCanvas = document.getElementById(canvasId);

        if (!fishCanvas) {
            console.error(`[Fish JS] Canvas element '${canvasId}' not found`);
            return false;
        }

        fishCanvas.width = width;
        fishCanvas.height = height;
        fishCtx = fishCanvas.getContext('2d');

        // Prevent context menu on right-click
        fishCanvas.addEventListener('contextmenu', (e) => {
            e.preventDefault();
            return false;
        });

        console.log(`[Fish JS] Canvas initialized: ${width}x${height}`);

        // Clear canvas
        fishCtx.fillStyle = '#FFFFFF';
        fishCtx.fillRect(0, 0, width, height);

        // Initialize Web Worker
        initWorker();

        return true;
    };

    function initWorker() {
        if (fishWorker) {
            fishWorker.terminate();
        }

        fishWorker = new Worker('/js/fish-worker.js');

        fishWorker.onmessage = function (e) {
            const { type, cells, fishCount, sharkCount, generation } = e.data;

            if (type === 'initialized' || type === 'generation' || type === 'updated') {
                // Render cells from worker
                if (cells && renderSettings) {
                    const cellData = new Uint8Array(cells);
                    renderCells(cellData, renderSettings);
                }

                // Notify C# component of update
                if (window.fishComponentRef && type === 'generation') {
                    window.fishComponentRef.invokeMethodAsync(
                        'OnWorkerGenerationComplete',
                        fishCount,
                        sharkCount,
                        generation
                    );
                }
            } else if (type === 'error') {
                console.error('[Fish Worker] Error:', e.data.error);
            }
        };

        fishWorker.onerror = function (error) {
            console.error('[Fish Worker] Error:', error);
        };

        console.log('[Fish JS] Worker initialized');
    }

    function renderCells(cellData, settings) {
        if (!fishCtx || !fishCanvas) return;

        const { rows, cols, cellWidth, cellHeight, useCircles, colorAgeGradient } = settings;

        // Clear canvas
        fishCtx.fillStyle = '#FFFFFF';
        fishCtx.fillRect(0, 0, fishCanvas.width, fishCanvas.height);

        let index = 0;
        for (let row = 0; row < rows; row++) {
            for (let col = 0; col < cols; col++) {
                const packed = cellData[index++];

                // Unpack: high 2 bits = type, low 6 bits = age
                const type = (packed >> 6) & 0x03;
                const age = packed & 0x3F;

                const x = col * cellWidth;
                const y = row * cellHeight;

                let color = '#FFFFFF';

                if (type === 1) {
                    // Fish = green
                    const ageAdjust = Math.min(age * colorAgeGradient, 255);
                    const greenValue = Math.max(0, 255 - ageAdjust);
                    color = `rgb(0, ${greenValue}, 0)`;
                } else if (type === 2) {
                    // Shark = red
                    const ageAdjust = Math.min(age * colorAgeGradient, 255);
                    const redValue = Math.max(0, 255 - ageAdjust);
                    color = `rgb(${redValue}, 0, 0)`;
                }

                fishCtx.fillStyle = color;

                if (useCircles && type !== 0) {
                    const centerX = x + cellWidth / 2;
                    const centerY = y + cellHeight / 2;
                    const radius = Math.min(cellWidth, cellHeight) / 2;
                    fishCtx.beginPath();
                    fishCtx.arc(centerX, centerY, radius, 0, 2 * Math.PI);
                    fishCtx.fill();
                } else {
                    fishCtx.fillRect(x, y, cellWidth, cellHeight);
                }
            }
        }
    }

    // Initialize world in worker
    window.initFishWorld = function (params) {
        console.log('[Fish JS] Initializing world in worker', params);

        // Store render settings from the world parameters
        renderSettings = {
            rows: params.rows,
            cols: params.cols,
            cellWidth: 3,  // Default values - will be updated when C# calls fishRenderFrame
            cellHeight: 3,
            useCircles: false,
            colorAgeGradient: 1
        };

        fishWorker.postMessage({
            command: 'init',
            data: params
        });
    };

    // ✅ FIX: Use requestAnimationFrame with manual delay for Android compatibility
    window.startFishSimulation = function (delayMs) {
        console.log('[Fish JS v11] Starting simulation, delay:', delayMs, 'ms (Android-compatible)');

        // Clean up any existing animation frame
        if (animationFrameId !== null) {
            cancelAnimationFrame(animationFrameId);
            animationFrameId = null;
        }

        isRunning = true;
        currentDelayMs = delayMs;
        lastTickTime = performance.now();

        function tick(currentTime) {
            if (!isRunning) return;

            const elapsed = currentTime - lastTickTime;

            // If enough time has passed (or no delay), process a generation
            if (currentDelayMs === 0 || elapsed >= currentDelayMs) {
                fishWorker.postMessage({ command: 'tick' });
                lastTickTime = currentTime;
            }

            // Always use requestAnimationFrame for reliable cross-platform timing
            animationFrameId = requestAnimationFrame(tick);
        }

        animationFrameId = requestAnimationFrame(tick);
    };

    // Stop simulation
    window.stopFishSimulation = function () {
        console.log('[Fish JS v11] Stopping simulation');
        isRunning = false;

        if (animationFrameId !== null) {
            cancelAnimationFrame(animationFrameId);
            animationFrameId = null;
        }
    };

    // Update parameters
    window.updateFishParams = function (params) {
        fishWorker.postMessage({
            command: 'updateParams',
            data: params
        });
    };

    // Add animal
    window.addFishAnimal = function (row, col, animalType) {
        fishWorker.postMessage({
            command: 'addAnimal',
            data: { row, col, animalType }
        });
    };

    // Set component reference
    window.setFishComponentRef = function (dotNetRef) {
        window.fishComponentRef = dotNetRef;
        console.log('[Fish] Component reference set');
    };

    // Get bounding client rect for touch events
    window.getBoundingClientRect = function (elementId) {
        const element = document.getElementById(elementId);
        if (!element) {
            console.error(`[Fish JS] Element '${elementId}' not found`);
            return { left: 0, top: 0, width: 0, height: 0 };
        }
        const rect = element.getBoundingClientRect();
        return {
            left: rect.left,
            top: rect.top,
            width: rect.width,
            height: rect.height
        };
    };

    // Setup canvas resize handling
    window.setupFishResize = function () {
        console.log('[Fish] Setting up resize listener');

        window.resizeFishCanvas = function (skipCallback) {
            const canvas = document.getElementById('fishCanvas');
            if (!canvas) return;

            const section = canvas.closest('.fish-canvas-section');
            const sectionWidth = section ? section.clientWidth : window.innerWidth;
            const sectionHeight = section ? section.clientHeight : window.innerHeight;

            const newWidth = sectionWidth;
            const newHeight = sectionHeight;

            const widthChanged = Math.abs(canvas.width - newWidth) > 2;
            const heightChanged = Math.abs(canvas.height - newHeight) > 2;
            if (widthChanged || heightChanged) {
                console.log('[Fish] Resizing canvas:', newWidth, 'x', newHeight);
                canvas.width = newWidth;
                canvas.height = newHeight;
                canvas.style.width = '100%';
                canvas.style.height = '100%';
                if (fishResizeTimeout) {
                    clearTimeout(fishResizeTimeout);
                }
                if (!skipCallback && window.fishComponentRef) {
                    window.fishComponentRef.invokeMethodAsync('OnCanvasResized', newWidth, newHeight);
                }
            }
        };

        window.resizeFishCanvas(false);

        window.addEventListener('resize', function () {
            window.resizeFishCanvas(false);
        });
    };

    // Get canvas dimensions
    window.getFishCanvasDimensions = function () {
        const canvas = document.getElementById('fishCanvas');
        if (!canvas) {
            return { width: 0, height: 0 };
        }
        return {
            width: canvas.width,
            height: canvas.height
        };
    };

    // Render frame (stores settings and renders if data provided)
    window.fishRenderFrame = function (cellData, rows, cols, cellWidth, cellHeight, useCircles, colorAgeGradient) {
        console.log('[Fish JS v11] fishRenderFrame called', {
            hasCanvas: !!fishCanvas,
            hasCtx: !!fishCtx,
            dataLength: cellData ? cellData.length : 0,
            rows,
            cols
        });

        // Update render settings (worker or WASM can call this)
        renderSettings = { rows, cols, cellWidth, cellHeight, useCircles, colorAgeGradient };

        // Render the provided cell data (from C# WASM or worker)
        if (cellData && cellData.length > 0) {
            renderCells(new Uint8Array(cellData), renderSettings);
        }
    };

    // Download CSV
    window.downloadCsv = function (csvContent, filename) {
        const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
        const link = document.createElement('a');
        const url = URL.createObjectURL(blob);

        link.setAttribute('href', url);
        link.setAttribute('download', filename);
        link.style.visibility = 'hidden';

        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);

        console.log(`[Fish JS] Downloaded ${filename}`);
    };

    console.log('[Fish] fish-game.js loaded successfully v11 (Android delay fix)');

    // Page Visibility API - pause when tab is hidden to save battery
    if (typeof document.hidden !== 'undefined') {
        document.addEventListener('visibilitychange', function () {
            if (document.hidden) {
                console.log('[Fish JS] Page hidden - pausing simulation');
                if (isRunning) {
                    window.stopFishSimulation();
                    // Notify C# component that we auto-paused
                    if (window.fishComponentRef) {
                        window.fishComponentRef.invokeMethodAsync('OnPageHidden');
                    }
                }
            } else {
                console.log('[Fish JS] Page visible - resuming simulation');
                // Notify C# component that page is visible again
                if (window.fishComponentRef) {
                    window.fishComponentRef.invokeMethodAsync('OnPageVisible');
                }
            }
        });
        console.log('[Fish JS] Page visibility handler registered');
    }
})();
