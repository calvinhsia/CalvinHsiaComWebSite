// Fish vs Sharks Cellular Automata Game JavaScript
console.log('[Fish] fish-game.js loading... v18 (iPad diagnostics)');

(function () {
    'use strict';

    // iPad/iOS/Safari detection for debugging  
    const isIOS = /iPad|iPhone|iPod/.test(navigator.userAgent) || 
                  (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
    const isSafari = /^((?!chrome|android).)*safari/i.test(navigator.userAgent);
    
    if (isIOS || isSafari) {
        console.log('[Fish JS v18] iOS/Safari detected - device info:', {
            userAgent: navigator.userAgent.substring(0, 100),
            platform: navigator.platform,
            maxTouchPoints: navigator.maxTouchPoints
        });
    }

    let fishCanvas = null;
    let fishCtx = null;
    let fishResizeTimeout = null;
    let fishWorker = null;
    let isRunning = false;
    let animationFrameId = null;
    let renderSettings = null;
    let workerAvailable = false; // Track if worker loaded successfully
    let workerReady = false; // Track if worker has finished initializing

    // Delay timing tracking for Android-compatible delays
    let lastTickTime = 0;
    let currentDelayMs = 0;
    
    // Track if first resize callback has been sent (for iPad reliability)
    let firstResizeCallbackSent = false;
    let resizeRetryCount = 0;
    const MAX_RESIZE_RETRIES = 20;
    
    // Pending operations while waiting for worker
    let pendingWorldInit = null;
    let pendingSimulationStart = null;

    // Helper to safely invoke C# methods (handles disposed object reference)
    function safeInvoke(methodName, ...args) {
        if (!window.fishComponentRef) {
            console.warn(`[Fish JS v18] Cannot invoke ${methodName}: component ref not set`);
            return Promise.resolve();
        }
        
        return window.fishComponentRef.invokeMethodAsync(methodName, ...args)
            .catch(err => {
                if (err.message && err.message.includes('disposed')) {
                    console.warn(`[Fish JS v18] Component was disposed, clearing ref. Method: ${methodName}`);
                    window.fishComponentRef = null;
                    isRunning = false;
                    if (animationFrameId !== null) {
                        cancelAnimationFrame(animationFrameId);
                        animationFrameId = null;
                    }
                } else {
                    console.error(`[Fish JS v18] Error invoking ${methodName}:`, err);
                }
            });
    }

    // Full cleanup function for SPA navigation
    function cleanupFishState() {
        console.log('[Fish JS v18] Cleaning up Fish state');
        
        isRunning = false;
        if (animationFrameId !== null) {
            cancelAnimationFrame(animationFrameId);
            animationFrameId = null;
        }
        
        window.fishComponentRef = null;
        firstResizeCallbackSent = false;
        resizeRetryCount = 0;
        renderSettings = null;
        pendingWorldInit = null;
        pendingSimulationStart = null;
        workerReady = false;
    }

    window.initFishCanvas = function (canvasId, width, height) {
        try {
            console.log(`[Fish JS v18] Initializing canvas: ${canvasId}`);
            
            cleanupFishState();
            
            fishCanvas = document.getElementById(canvasId);

            if (!fishCanvas) {
                console.error(`[Fish JS v18] Canvas element '${canvasId}' not found`);
                return false;
            }

            fishCanvas.width = width;
            fishCanvas.height = height;
            fishCtx = fishCanvas.getContext('2d');

            fishCanvas.addEventListener('contextmenu', (e) => {
                e.preventDefault();
                return false;
            });

            console.log(`[Fish JS v18] Canvas initialized: ${width}x${height}`);

            fishCtx.fillStyle = '#FFFFFF';
            fishCtx.fillRect(0, 0, width, height);

            // Initialize Web Worker synchronously (not deferred)
            initWorker();

            return true;
        } catch (err) {
            console.error('[Fish JS v18] Error in initFishCanvas:', err);
            return false;
        }
    };

    function initWorker() {
        try {
            if (fishWorker) {
                fishWorker.terminate();
                fishWorker = null;
            }
            
            workerAvailable = false;
            workerReady = false;

            if (typeof Worker === 'undefined') {
                console.warn('[Fish JS v18] Web Workers not supported - will use WASM fallback');
                return;
            }

            fishWorker = new Worker('/js/fish-worker.js');

            fishWorker.onmessage = function (e) {
                try {
                    const { type, cells, fishCount, sharkCount, generation } = e.data;

                    if (type === 'ready') {
                        // Worker is ready to receive commands
                        console.log('[Fish JS v18] Worker signaled ready');
                        workerReady = true;
                        processPendingOperations();
                    } else if (type === 'initialized') {
                        console.log('[Fish JS v18] Worker world initialized');
                        // Render initial state
                        if (cells && renderSettings) {
                            const cellData = new Uint8Array(cells);
                            renderCells(cellData, renderSettings);
                        }
                    } else if (type === 'generation' || type === 'updated') {
                        if (cells && renderSettings) {
                            const cellData = new Uint8Array(cells);
                            renderCells(cellData, renderSettings);
                        }

                        if (type === 'generation') {
                            safeInvoke('OnWorkerGenerationComplete', fishCount, sharkCount, generation);
                        }
                    } else if (type === 'error') {
                        console.error('[Fish Worker] Error:', e.data.error);
                    }
                } catch (err) {
                    console.error('[Fish JS v18] Error in worker message handler:', err);
                }
            };

            fishWorker.onerror = function (error) {
                console.error('[Fish Worker] Error:', error);
                workerAvailable = false;
                workerReady = false;
            };

            workerAvailable = true;
            console.log('[Fish JS v18] Worker created, waiting for ready signal...');
            
            // Give the worker a chance to load, then process pending ops even without ready signal
            setTimeout(() => {
                if (!workerReady && workerAvailable) {
                    console.log('[Fish JS v18] Worker ready timeout, assuming ready');
                    workerReady = true;
                    processPendingOperations();
                }
            }, 500);
            
        } catch (err) {
            console.error('[Fish JS v18] Failed to initialize Worker:', err);
            workerAvailable = false;
            workerReady = false;
        }
    }
    
    function processPendingOperations() {
        console.log('[Fish JS v18] Processing pending operations...');
        
        if (pendingWorldInit) {
            console.log('[Fish JS v18] Executing pending world init');
            const params = pendingWorldInit;
            pendingWorldInit = null;
            
            if (fishWorker && workerAvailable) {
                fishWorker.postMessage({
                    command: 'init',
                    data: params
                });
            }
        }
        
        if (pendingSimulationStart !== null) {
            console.log('[Fish JS v18] Executing pending simulation start');
            const delayMs = pendingSimulationStart;
            pendingSimulationStart = null;
            
            startSimulationInternal(delayMs);
        }
    }

    function renderCells(cellData, settings) {
        if (!fishCtx || !fishCanvas) return;

        const { rows, cols, cellWidth, cellHeight, useCircles, colorAgeGradient } = settings;

        fishCtx.fillStyle = '#FFFFFF';
        fishCtx.fillRect(0, 0, fishCanvas.width, fishCanvas.height);

        let index = 0;
        for (let row = 0; row < rows; row++) {
            for (let col = 0; col < cols; col++) {
                const packed = cellData[index++];
                const type = (packed >> 6) & 0x03;
                const age = packed & 0x3F;

                const x = col * cellWidth;
                const y = row * cellHeight;

                let color = '#FFFFFF';

                if (type === 1) {
                    const ageAdjust = Math.min(age * colorAgeGradient, 255);
                    const greenValue = Math.max(0, 255 - ageAdjust);
                    color = `rgb(0, ${greenValue}, 0)`;
                } else if (type === 2) {
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
            console.log('[Fish JS v18] initFishWorld called', params);

            renderSettings = {
                rows: params.rows,
                cols: params.cols,
                cellWidth: 3,
                cellHeight: 3,
                useCircles: false,
                colorAgeGradient: 1
            };

            if (!workerAvailable) {
                console.warn('[Fish JS v18] Worker not available, skipping init');
                return;
            }
            
            if (!workerReady) {
                console.log('[Fish JS v18] Worker not ready, queuing world init');
                pendingWorldInit = params;
                return;
            }

            if (fishWorker) {
                fishWorker.postMessage({
                    command: 'init',
                    data: params
                });
            }
        } catch (err) {
            console.error('[Fish JS v18] Error in initFishWorld:', err);
        }
    };

    function startSimulationInternal(delayMs) {
        if (animationFrameId !== null) {
            cancelAnimationFrame(animationFrameId);
            animationFrameId = null;
        }

        if (!fishWorker || !workerAvailable) {
            console.warn('[Fish JS v18] Worker not available, cannot start simulation');
            return;
        }

        isRunning = true;
        currentDelayMs = delayMs;
        lastTickTime = performance.now();

        function tick(currentTime) {
            if (!isRunning) return;

            try {
                const elapsed = currentTime - lastTickTime;

                if (currentDelayMs === 0 || elapsed >= currentDelayMs) {
                    if (fishWorker) {
                        fishWorker.postMessage({ command: 'tick' });
                    }
                    lastTickTime = currentTime;
                }

                animationFrameId = requestAnimationFrame(tick);
            } catch (err) {
                console.error('[Fish JS v18] Error in tick:', err);
                isRunning = false;
            }
        }

        animationFrameId = requestAnimationFrame(tick);
        console.log('[Fish JS v18] Simulation started with delay:', delayMs, 'ms');
    }

    window.startFishSimulation = function (delayMs) {
        try {
            console.log('[Fish JS v18] startFishSimulation called, delay:', delayMs, 'ms');

            if (!workerAvailable) {
                console.warn('[Fish JS v18] Worker not available, cannot start simulation');
                return;
            }
            
            if (!workerReady) {
                console.log('[Fish JS v18] Worker not ready, queuing simulation start');
                pendingSimulationStart = delayMs;
                return;
            }

            startSimulationInternal(delayMs);
        } catch (err) {
            console.error('[Fish JS v18] Error in startFishSimulation:', err);
        }
    };

    window.stopFishSimulation = function () {
        try {
            console.log('[Fish JS v18] Stopping simulation');
            isRunning = false;
            pendingSimulationStart = null; // Cancel any pending start

            if (animationFrameId !== null) {
                cancelAnimationFrame(animationFrameId);
                animationFrameId = null;
            }
        } catch (err) {
            console.error('[Fish JS v18] Error in stopFishSimulation:', err);
        }
    };

    window.updateFishParams = function (params) {
        try {
            if (fishWorker && workerAvailable && workerReady) {
                fishWorker.postMessage({
                    command: 'updateParams',
                    data: params
                });
            }
        } catch (err) {
            console.error('[Fish JS v18] Error in updateFishParams:', err);
        }
    };

    window.addFishAnimal = function (row, col, animalType) {
        try {
            if (fishWorker && workerAvailable && workerReady) {
                fishWorker.postMessage({
                    command: 'addAnimal',
                    data: { row, col, animalType }
                });
            }
        } catch (err) {
            console.error('[Fish JS v18] Error in addFishAnimal:', err);
        }
    };

    window.setFishComponentRef = function (dotNetRef) {
        try {
            if (window.fishComponentRef) {
                console.log('[Fish JS v18] Clearing old component reference');
            }
            
            window.fishComponentRef = dotNetRef;
            console.log('[Fish JS v18] Component reference set');
            
            if (!firstResizeCallbackSent) {
                console.log('[Fish JS v18] Triggering initial resize after component ref set');
                setTimeout(() => {
                    if (window.resizeFishCanvas && !firstResizeCallbackSent) {
                        window.resizeFishCanvas(false);
                    }
                }, 50);
            }
        } catch (err) {
            console.error('[Fish JS v18] Error in setFishComponentRef:', err);
        }
    };

    window.getBoundingClientRect = function (elementId) {
        const element = document.getElementById(elementId);
        if (!element) {
            console.error(`[Fish JS v18] Element '${elementId}' not found`);
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

    window.setupFishResize = function () {
        console.log('[Fish v18] Setting up resize listener');
        
        firstResizeCallbackSent = false;
        resizeRetryCount = 0;

        window.resizeFishCanvas = function (skipCallback) {
            const canvas = document.getElementById('fishCanvas');
            if (!canvas) {
                console.warn('[Fish v18] Canvas not found in resizeFishCanvas');
                return;
            }

            const section = canvas.closest('.fish-canvas-section');
            const sectionWidth = section ? section.clientWidth : 0;
            const sectionHeight = section ? section.clientHeight : 0;

            console.log('[Fish v18] Resize check: section=', sectionWidth, 'x', sectionHeight, 
                        'firstCallbackSent=', firstResizeCallbackSent);

            if (sectionWidth < 100 || sectionHeight < 100) {
                resizeRetryCount++;
                if (resizeRetryCount <= MAX_RESIZE_RETRIES) {
                    const delay = Math.min(100 * resizeRetryCount, 500);
                    console.warn(`[Fish v18] Section dimensions too small, retry ${resizeRetryCount}/${MAX_RESIZE_RETRIES} in ${delay}ms...`);
                    setTimeout(() => window.resizeFishCanvas(skipCallback), delay);
                } else {
                    console.error('[Fish v18] Max retries reached, using fallback dimensions');
                    const fallbackWidth = Math.max(window.innerWidth - 40, 300);
                    const fallbackHeight = Math.max(window.innerHeight - 200, 300);
                    invokeResize(canvas, fallbackWidth, fallbackHeight, skipCallback);
                }
                return;
            }

            resizeRetryCount = 0;
            invokeResize(canvas, sectionWidth, sectionHeight, skipCallback);
        };
        
        function invokeResize(canvas, newWidth, newHeight, skipCallback) {
            const widthChanged = Math.abs(canvas.width - newWidth) > 2;
            const heightChanged = Math.abs(canvas.height - newHeight) > 2;
            const needsCallback = !firstResizeCallbackSent || widthChanged || heightChanged;
            
            if (needsCallback) {
                console.log('[Fish v18] Resizing canvas:', newWidth, 'x', newHeight);
                canvas.width = newWidth;
                canvas.height = newHeight;
                canvas.style.width = '100%';
                canvas.style.height = '100%';
                
                if (fishResizeTimeout) {
                    clearTimeout(fishResizeTimeout);
                }
                
                if (!skipCallback && window.fishComponentRef) {
                    firstResizeCallbackSent = true;
                    console.log('[Fish v18] Invoking OnCanvasResized callback');
                    safeInvoke('OnCanvasResized', newWidth, newHeight)
                        .then(() => console.log('[Fish v18] OnCanvasResized callback completed'));
                } else if (!skipCallback && !window.fishComponentRef) {
                    console.warn('[Fish v18] Component ref not set yet, retrying in 50ms...');
                    setTimeout(() => window.resizeFishCanvas(skipCallback), 50);
                }
            }
        }

        window.resizeFishCanvas(false);

        window.addEventListener('resize', function () {
            window.resizeFishCanvas(false);
        });
    };

    window.getFishCanvasDimensions = function () {
        const canvas = document.getElementById('fishCanvas');
        if (!canvas) {
            return { width: 0, height: 0 };
        }
        return { width: canvas.width, height: canvas.height };
    };

    window.fishRenderFrame = function (cellData, rows, cols, cellWidth, cellHeight, useCircles, colorAgeGradient) {
        try {
            renderSettings = { rows, cols, cellWidth, cellHeight, useCircles, colorAgeGradient };
            if (cellData && cellData.length > 0) {
                renderCells(new Uint8Array(cellData), renderSettings);
            }
        } catch (err) {
            console.error('[Fish JS v18] Error in fishRenderFrame:', err);
        }
    };

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
            console.log(`[Fish JS v18] Downloaded ${filename}`);
        } catch (err) {
            console.error('[Fish JS v18] Error in downloadCsv:', err);
        }
    };

    console.log('[Fish] fish-game.js loaded successfully v18 (wait for worker)');

    // Page Visibility API
    try {
        if (typeof document !== 'undefined' && typeof document.hidden !== 'undefined') {
            document.addEventListener('visibilitychange', function () {
                try {
                    if (document.hidden) {
                        console.log('[Fish JS v18] Page hidden - pausing simulation');
                        if (isRunning) {
                            window.stopFishSimulation();
                            safeInvoke('OnPageHidden');
                        }
                    } else {
                        console.log('[Fish JS v18] Page visible');
                        safeInvoke('OnPageVisible');
                    }
                } catch (err) {
                    console.error('[Fish JS v18] Error in visibility handler:', err);
                }
            });
            console.log('[Fish JS v18] Page visibility handler registered');
        }
    } catch (err) {
        console.error('[Fish JS v18] Error setting up visibility handler:', err);
    }
})();
