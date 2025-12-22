// Fish vs Sharks Cellular Automata Game JavaScript
console.log('[Fish] fish-game.js loading... v13 (defensive loading)');

(function () {
    'use strict';

    let fishCanvas = null;
    let fishCtx = null;
    let fishResizeTimeout = null;
    let fishWorker = null;
    let isRunning = false;
    let animationFrameId = null;
    let renderSettings = null;
    let workerAvailable = false; // Track if worker loaded successfully

    // Delay timing tracking for Android-compatible delays
    let lastTickTime = 0;
    let currentDelayMs = 0;
    
    // Track if first resize callback has been sent (for iPad reliability)
    let firstResizeCallbackSent = false;

    window.initFishCanvas = function (canvasId, width, height) {
        try {
            console.log(`[Fish JS v13] Initializing canvas: ${canvasId}`);
            fishCanvas = document.getElementById(canvasId);

            if (!fishCanvas) {
                console.error(`[Fish JS v13] Canvas element '${canvasId}' not found`);
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

            console.log(`[Fish JS v13] Canvas initialized: ${width}x${height}`);

            // Clear canvas
            fishCtx.fillStyle = '#FFFFFF';
            fishCtx.fillRect(0, 0, width, height);

            // Initialize Web Worker (deferred and wrapped in try-catch)
            setTimeout(() => initWorker(), 0);

            return true;
        } catch (err) {
            console.error('[Fish JS v13] Error in initFishCanvas:', err);
            return false;
        }
    };

    function initWorker() {
        try {
            if (fishWorker) {
                fishWorker.terminate();
                fishWorker = null;
            }

            // Check if Workers are supported
            if (typeof Worker === 'undefined') {
                console.warn('[Fish JS v13] Web Workers not supported - will use WASM fallback');
                workerAvailable = false;
                return;
            }

            fishWorker = new Worker('/js/fish-worker.js');

            fishWorker.onmessage = function (e) {
                try {
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
                } catch (err) {
                    console.error('[Fish JS v13] Error in worker message handler:', err);
                }
            };

            fishWorker.onerror = function (error) {
                console.error('[Fish Worker] Error:', error);
                workerAvailable = false;
            };

            workerAvailable = true;
            console.log('[Fish JS v13] Worker initialized successfully');
        } catch (err) {
            console.error('[Fish JS v13] Failed to initialize Worker:', err);
            workerAvailable = false;
        }
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
        try {
            console.log('[Fish JS v13] Initializing world in worker', params);

            // Store render settings from the world parameters
            renderSettings = {
                rows: params.rows,
                cols: params.cols,
                cellWidth: 3,  // Default values - will be updated when C# calls fishRenderFrame
                cellHeight: 3,
                useCircles: false,
                colorAgeGradient: 1
            };

            if (fishWorker && workerAvailable) {
                fishWorker.postMessage({
                    command: 'init',
                    data: params
                });
            } else {
                console.warn('[Fish JS v13] Worker not available, skipping init');
            }
        } catch (err) {
            console.error('[Fish JS v13] Error in initFishWorld:', err);
        }
    };

    // ✅ FIX: Use requestAnimationFrame with manual delay for Android compatibility
    window.startFishSimulation = function (delayMs) {
        try {
            console.log('[Fish JS v13] Starting simulation, delay:', delayMs, 'ms');

            // Clean up any existing animation frame
            if (animationFrameId !== null) {
                cancelAnimationFrame(animationFrameId);
                animationFrameId = null;
            }

            if (!fishWorker || !workerAvailable) {
                console.warn('[Fish JS v13] Worker not available, cannot start simulation');
                return;
            }

            isRunning = true;
            currentDelayMs = delayMs;
            lastTickTime = performance.now();

            function tick(currentTime) {
                if (!isRunning) return;

                try {
                    const elapsed = currentTime - lastTickTime;

                    // If enough time has passed (or no delay), process a generation
                    if (currentDelayMs === 0 || elapsed >= currentDelayMs) {
                        if (fishWorker) {
                            fishWorker.postMessage({ command: 'tick' });
                        }
                        lastTickTime = currentTime;
                    }

                    // Always use requestAnimationFrame for reliable cross-platform timing
                    animationFrameId = requestAnimationFrame(tick);
                } catch (err) {
                    console.error('[Fish JS v13] Error in tick:', err);
                    isRunning = false;
                }
            }

            animationFrameId = requestAnimationFrame(tick);
        } catch (err) {
            console.error('[Fish JS v13] Error in startFishSimulation:', err);
        }
    };

    // Stop simulation
    window.stopFishSimulation = function () {
        try {
            console.log('[Fish JS v13] Stopping simulation');
            isRunning = false;

            if (animationFrameId !== null) {
                cancelAnimationFrame(animationFrameId);
                animationFrameId = null;
            }
        } catch (err) {
            console.error('[Fish JS v13] Error in stopFishSimulation:', err);
        }
    };

    // Update parameters
    window.updateFishParams = function (params) {
        try {
            if (fishWorker && workerAvailable) {
                fishWorker.postMessage({
                    command: 'updateParams',
                    data: params
                });
            }
        } catch (err) {
            console.error('[Fish JS v13] Error in updateFishParams:', err);
        }
    };

    // Add animal
    window.addFishAnimal = function (row, col, animalType) {
        try {
            if (fishWorker && workerAvailable) {
                fishWorker.postMessage({
                    command: 'addAnimal',
                    data: { row, col, animalType }
                });
            }
        } catch (err) {
            console.error('[Fish JS v13] Error in addFishAnimal:', err);
        }
    };

    // Set component reference
    window.setFishComponentRef = function (dotNetRef) {
        try {
            window.fishComponentRef = dotNetRef;
            console.log('[Fish JS v13] Component reference set');
        } catch (err) {
            console.error('[Fish JS v13] Error in setFishComponentRef:', err);
        }
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
        console.log('[Fish v12] Setting up resize listener');
        
        // Reset first resize flag when setting up
        firstResizeCallbackSent = false;

        window.resizeFishCanvas = function (skipCallback) {
            const canvas = document.getElementById('fishCanvas');
            if (!canvas) {
                console.warn('[Fish v12] Canvas not found in resizeFishCanvas');
                return;
            }

            const section = canvas.closest('.fish-canvas-section');
            const sectionWidth = section ? section.clientWidth : window.innerWidth;
            const sectionHeight = section ? section.clientHeight : window.innerHeight;

            console.log('[Fish v12] Resize check: section=', sectionWidth, 'x', sectionHeight, 
                        'canvas=', canvas.width, 'x', canvas.height,
                        'firstCallbackSent=', firstResizeCallbackSent);

            // If dimensions are 0 or too small, schedule a retry (common on iPad initial load)
            if (sectionWidth < 100 || sectionHeight < 100) {
                console.warn('[Fish v12] Section dimensions too small, retrying in 100ms...');
                setTimeout(() => window.resizeFishCanvas(skipCallback), 100);
                return;
            }

            const newWidth = sectionWidth;
            const newHeight = sectionHeight;

            const widthChanged = Math.abs(canvas.width - newWidth) > 2;
            const heightChanged = Math.abs(canvas.height - newHeight) > 2;
            
            // Always trigger callback on first resize, even if dimensions haven't changed much
            const needsCallback = !firstResizeCallbackSent || widthChanged || heightChanged;
            
            if (needsCallback) {
                console.log('[Fish v12] Resizing canvas:', newWidth, 'x', newHeight, 
                            'firstResize=', !firstResizeCallbackSent);
                canvas.width = newWidth;
                canvas.height = newHeight;
                canvas.style.width = '100%';
                canvas.style.height = '100%';
                
                if (fishResizeTimeout) {
                    clearTimeout(fishResizeTimeout);
                }
                
                if (!skipCallback && window.fishComponentRef) {
                    firstResizeCallbackSent = true;
                    console.log('[Fish v12] Invoking OnCanvasResized callback');
                    window.fishComponentRef.invokeMethodAsync('OnCanvasResized', newWidth, newHeight)
                        .then(() => console.log('[Fish v12] OnCanvasResized callback completed'))
                        .catch(err => console.error('[Fish v12] OnCanvasResized callback failed:', err));
                } else if (!skipCallback && !window.fishComponentRef) {
                    // Component ref not set yet, retry shortly
                    console.warn('[Fish v12] Component ref not set yet, retrying in 50ms...');
                    setTimeout(() => window.resizeFishCanvas(skipCallback), 50);
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
        try {
            console.log('[Fish JS v13] fishRenderFrame called', {
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
        } catch (err) {
            console.error('[Fish JS v13] Error in fishRenderFrame:', err);
        }
    };

    // Download CSV
    window.downloadCsv = function (csvContent, filename) {
        try {
            const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
            const link = document.createElement('a');
            const url = URL.createObjectURL(blob);

            link.setAttribute('href', url);
            link.setAttribute('download', filename);
            link.style.visibility = 'hidden';

            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);

            console.log(`[Fish JS v13] Downloaded ${filename}`);
        } catch (err) {
            console.error('[Fish JS v13] Error in downloadCsv:', err);
        }
    };

    console.log('[Fish] fish-game.js loaded successfully v13 (defensive loading)');

    // Page Visibility API - pause when tab is hidden to save battery
    try {
        if (typeof document !== 'undefined' && typeof document.hidden !== 'undefined') {
            document.addEventListener('visibilitychange', function () {
                try {
                    if (document.hidden) {
                        console.log('[Fish JS v13] Page hidden - pausing simulation');
                        if (isRunning) {
                            window.stopFishSimulation();
                            // Notify C# component that we auto-paused
                            if (window.fishComponentRef) {
                                window.fishComponentRef.invokeMethodAsync('OnPageHidden');
                            }
                        }
                    } else {
                        console.log('[Fish JS v13] Page visible - resuming simulation');
                        // Notify C# component that page is visible again
                        if (window.fishComponentRef) {
                            window.fishComponentRef.invokeMethodAsync('OnPageVisible');
                        }
                    }
                } catch (err) {
                    console.error('[Fish JS v13] Error in visibility handler:', err);
                }
            });
            console.log('[Fish JS v13] Page visibility handler registered');
        }
    } catch (err) {
        console.error('[Fish JS v13] Error setting up visibility handler:', err);
    }
})();
