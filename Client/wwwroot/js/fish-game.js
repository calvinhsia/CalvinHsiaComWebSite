// Fish vs Sharks Cellular Automata Game JavaScript
console.log('[Fish] fish-game.js loading... v3');

(function () {
    'use strict';

    let fishCanvas = null;
    let fishCtx = null;
    let fishResizeTimeout = null;

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

        return true;
    };

    // NEW: Helper function to set component reference
    window.setFishComponentRef = function (dotNetRef) {
        window.fishComponentRef = dotNetRef;
        console.log('[Fish] Component reference set');
    };

    /**
         * Setup canvas resize handling with debouncing
      */
    window.setupFishResize = function () {
        console.log('[Fish] Setting up resize listener');

        // Define resize handler function
        window.resizeFishCanvas = function (skipCallback) {
            const canvas = document.getElementById('fishCanvas');
            if (!canvas) {
                console.log('[Fish] Canvas not found!');
                return;
            }

            const container = canvas.parentElement;
            const containerWidth = container.clientWidth;
            const containerHeight = container.clientHeight;

            // Fill the container completely
            const newWidth = containerWidth;
            const newHeight = containerHeight;

            // Only resize if dimensions changed significantly
            if (Math.abs(canvas.width - newWidth) > 10 || Math.abs(canvas.height - newHeight) > 10) {
                console.log('[Fish] Resizing canvas from', canvas.width, 'x', canvas.height, 'to', newWidth, 'x', newHeight);

                // Set canvas internal resolution to match container size
                canvas.width = newWidth;
                canvas.height = newHeight;

                console.log('[Fish] Resize complete. Canvas dimensions:', canvas.width, 'x', canvas.height);

                // Clear any pending timeout
                if (fishResizeTimeout) {
                    clearTimeout(fishResizeTimeout);
                }

                // Notify Blazor of the resize with actual dimensions (unless skipped for initial setup)
                if (!skipCallback && window.fishComponentRef) {
                    window.fishComponentRef.invokeMethodAsync('OnCanvasResized', newWidth, newHeight);
                }
            } else {
                console.log('[Fish] Resize skipped - dimensions unchanged (±10px threshold)');
            }
        };

        // Initial resize - skip callback since we're still initializing
        console.log('[Fish] Running initial resize (skipping callback)');
        window.resizeFishCanvas(true);

        // Add resize listener
        window.addEventListener('resize', function () {
            console.log('[Fish] Window resize event fired');
            window.resizeFishCanvas(false); // Normal resize - trigger callback
        });
        console.log('[Fish] Resize listener installed');
    };

    /**
       * Get canvas dimensions
   * @returns {object} Canvas width and height
       */
    window.getFishCanvasDimensions = function () {
        const canvas = document.getElementById('fishCanvas');
        if (!canvas) {
            console.error('[Fish] Canvas not found');
            return { width: 0, height: 0 };
        }
        return {
            width: canvas.width,
            height: canvas.height
        };
    };

    window.fishRenderFrame = function (cellData, rows, cols, cellWidth, cellHeight, useCircles, colorAgeGradient) {
        if (!fishCtx || !fishCanvas) {
            console.error('[Fish JS] Canvas not initialized');
            return;
        }

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

                let color = '#FFFFFF'; // Empty = white

                if (type === 1) {
                    // Fish = green (darken with age)
                    const ageAdjust = Math.min(age * colorAgeGradient, 255);
                    const greenValue = Math.max(0, 255 - ageAdjust);
                    color = `rgb(0, ${greenValue}, 0)`;
                } else if (type === 2) {
                    // Shark = red (darken with age)
                    const ageAdjust = Math.min(age * colorAgeGradient, 255);
                    const redValue = Math.max(0, 255 - ageAdjust);
                    color = `rgb(${redValue}, 0, 0)`;
                }

                fishCtx.fillStyle = color;

                if (useCircles && type !== 0) {
                    // Draw circle
                    const centerX = x + cellWidth / 2;
                    const centerY = y + cellHeight / 2;
                    const radius = Math.min(cellWidth, cellHeight) / 2;

                    fishCtx.beginPath();
                    fishCtx.arc(centerX, centerY, radius, 0, 2 * Math.PI);
                    fishCtx.fill();
                } else {
                    // Draw rectangle
                    fishCtx.fillRect(x, y, cellWidth, cellHeight);
                }
            }
        }
    };

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

    // Log that the script has loaded
    console.log('[Fish] fish-game.js loaded successfully');
})();
