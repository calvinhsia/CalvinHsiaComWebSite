// Mandelbrot Set Explorer - JavaScript support
// v1 - Initial implementation with zoom and pan

(function() {
    'use strict';

    let canvas = null;
    let ctx = null;
    let canvasWidth = 800;
    let canvasHeight = 600;
    let componentRef = null;
    let isRendering = false;

    // Mandelbrot parameters
    let centerX = -0.5;
    let centerY = 0;
    let zoom = 1;
    let maxIterations = 100;

    // Color palette
    let colorPalette = [];

    // Track if first resize callback has been sent
    let firstResizeCallbackSent = false;
    let resizeRetryCount = 0;
    const MAX_RESIZE_RETRIES = 20;

    // For progressive rendering
    let imageData = null;
    let renderStartTime = 0;

    console.log('[Mandelbrot v1] mandelbrot-game.js loading...');

    // Generate color palette
    function generatePalette(numColors) {
        colorPalette = [];
        for (let i = 0; i < numColors; i++) {
            const t = i / numColors;
            // Create a nice gradient
            const r = Math.floor(9 * (1 - t) * t * t * t * 255);
            const g = Math.floor(15 * (1 - t) * (1 - t) * t * t * 255);
            const b = Math.floor(8.5 * (1 - t) * (1 - t) * (1 - t) * t * 255);
            colorPalette.push({ r, g, b });
        }
        // Black for points in the set
        colorPalette.push({ r: 0, g: 0, b: 0 });
    }

    // Helper to safely invoke C# methods
    function safeInvoke(methodName, ...args) {
        if (!componentRef) {
            console.warn(`[Mandelbrot v1] Cannot invoke ${methodName}: component ref not set`);
            return Promise.resolve();
        }
        
        return componentRef.invokeMethodAsync(methodName, ...args)
            .catch(err => {
                if (err.message && err.message.includes('disposed')) {
                    console.warn(`[Mandelbrot v1] Component was disposed, clearing ref`);
                    componentRef = null;
                    isRendering = false;
                } else {
                    console.error(`[Mandelbrot v1] Error invoking ${methodName}:`, err);
                }
            });
    }

    // Initialize canvas
    window.initMandelbrotCanvas = function(canvasId, width, height) {
        canvas = document.getElementById(canvasId);
        if (!canvas) {
            console.error('[Mandelbrot v1] Canvas not found:', canvasId);
            return;
        }

        firstResizeCallbackSent = false;
        resizeRetryCount = 0;
        isRendering = false;

        ctx = canvas.getContext('2d');
        canvasWidth = width;
        canvasHeight = height;
        canvas.width = width;
        canvas.height = height;

        generatePalette(256);

        console.log(`[Mandelbrot v1] Canvas initialized: ${width}x${height}`);
    };

    // Set component reference
    window.setMandelbrotComponentRef = function(ref) {
        componentRef = ref;
        console.log('[Mandelbrot v1] Component reference set');
        
        if (!firstResizeCallbackSent) {
            setTimeout(() => {
                if (window.resizeMandelbrotCanvas && !firstResizeCallbackSent) {
                    window.resizeMandelbrotCanvas();
                }
            }, 50);
        }
    };

    // Setup resize handler
    window.setupMandelbrotResize = function() {
        console.log('[Mandelbrot v1] Setting up resize listener');
        
        firstResizeCallbackSent = false;
        resizeRetryCount = 0;

        window.addEventListener('resize', debounce(() => {
            if (window.resizeMandelbrotCanvas) {
                window.resizeMandelbrotCanvas();
            }
        }, 250));
    };

    // Resize canvas
    window.resizeMandelbrotCanvas = function() {
        const section = document.querySelector('.mandelbrot-canvas-section');
        if (!section || !canvas) return;

        const sectionWidth = section.clientWidth;
        const sectionHeight = section.clientHeight;

        if (sectionWidth < 100 || sectionHeight < 100) {
            resizeRetryCount++;
            if (resizeRetryCount <= MAX_RESIZE_RETRIES) {
                setTimeout(() => window.resizeMandelbrotCanvas(), Math.min(100 * resizeRetryCount, 500));
            }
            return;
        }

        resizeRetryCount = 0;
        const padding = 8;
        const newWidth = Math.floor(sectionWidth - padding);
        const newHeight = Math.floor(sectionHeight - padding);

        const widthChanged = Math.abs(canvas.width - newWidth) > 2;
        const heightChanged = Math.abs(canvas.height - newHeight) > 2;
        const needsCallback = !firstResizeCallbackSent || widthChanged || heightChanged;

        if (needsCallback) {
            canvasWidth = newWidth;
            canvasHeight = newHeight;
            canvas.width = newWidth;
            canvas.height = newHeight;
            
            if (componentRef) {
                firstResizeCallbackSent = true;
                safeInvoke('OnCanvasResized', newWidth, newHeight);
            } else {
                setTimeout(() => window.resizeMandelbrotCanvas(), 50);
            }
        }
    };

    // Set Mandelbrot parameters
    window.setMandelbrotParams = function(params) {
        centerX = params.centerX;
        centerY = params.centerY;
        zoom = params.zoom;
        maxIterations = params.maxIterations;
        console.log(`[Mandelbrot v1] Params: center=(${centerX}, ${centerY}), zoom=${zoom}, maxIter=${maxIterations}`);
    };

    // Render the Mandelbrot set
    window.renderMandelbrot = function() {
        if (!ctx || !canvas) return;
        if (isRendering) {
            console.log('[Mandelbrot v1] Already rendering, skipping');
            return;
        }

        isRendering = true;
        renderStartTime = performance.now();

        const width = canvas.width;
        const height = canvas.height;
        
        imageData = ctx.createImageData(width, height);
        const data = imageData.data;

        // Calculate bounds
        const aspectRatio = width / height;
        const viewHeight = 4 / zoom;
        const viewWidth = viewHeight * aspectRatio;
        const minX = centerX - viewWidth / 2;
        const maxX = centerX + viewWidth / 2;
        const minY = centerY - viewHeight / 2;
        const maxY = centerY + viewHeight / 2;

        // Use requestAnimationFrame for progressive rendering
        let currentRow = 0;
        const rowsPerFrame = Math.max(1, Math.floor(height / 60)); // Aim for ~60 frames

        function renderRows() {
            const endRow = Math.min(currentRow + rowsPerFrame, height);

            for (let py = currentRow; py < endRow; py++) {
                const y0 = minY + (py / height) * (maxY - minY);

                for (let px = 0; px < width; px++) {
                    const x0 = minX + (px / width) * (maxX - minX);

                    let x = 0;
                    let y = 0;
                    let iteration = 0;

                    // Mandelbrot iteration
                    while (x * x + y * y <= 4 && iteration < maxIterations) {
                        const xTemp = x * x - y * y + x0;
                        y = 2 * x * y + y0;
                        x = xTemp;
                        iteration++;
                    }

                    // Color based on iteration count
                    const idx = (py * width + px) * 4;
                    
                    if (iteration === maxIterations) {
                        // Point is in the set - black
                        data[idx] = 0;
                        data[idx + 1] = 0;
                        data[idx + 2] = 0;
                        data[idx + 3] = 255;
                    } else {
                        // Smooth coloring
                        const colorIdx = iteration % colorPalette.length;
                        const color = colorPalette[colorIdx];
                        data[idx] = color.r;
                        data[idx + 1] = color.g;
                        data[idx + 2] = color.b;
                        data[idx + 3] = 255;
                    }
                }
            }

            currentRow = endRow;

            // Draw current progress
            ctx.putImageData(imageData, 0, 0);

            if (currentRow < height) {
                requestAnimationFrame(renderRows);
            } else {
                // Rendering complete
                isRendering = false;
                const elapsed = performance.now() - renderStartTime;
                console.log(`[Mandelbrot v1] Render complete in ${elapsed.toFixed(0)}ms`);
                
                if (componentRef) {
                    safeInvoke('OnRenderComplete', elapsed, width * height);
                }
            }
        }

        requestAnimationFrame(renderRows);
    };

    // Zoom in at a specific point
    window.mandelbrotZoomAt = function(canvasX, canvasY, zoomFactor) {
        const width = canvas.width;
        const height = canvas.height;
        
        // Calculate current view bounds
        const aspectRatio = width / height;
        const viewHeight = 4 / zoom;
        const viewWidth = viewHeight * aspectRatio;
        const minX = centerX - viewWidth / 2;
        const minY = centerY - viewHeight / 2;

        // Calculate new center based on click position
        const clickX = minX + (canvasX / width) * viewWidth;
        const clickY = minY + (canvasY / height) * viewHeight;

        // Update center and zoom
        centerX = clickX;
        centerY = clickY;
        zoom *= zoomFactor;

        console.log(`[Mandelbrot v1] Zoom at (${clickX.toFixed(6)}, ${clickY.toFixed(6)}), new zoom: ${zoom.toFixed(2)}`);

        // Notify C# of new parameters
        if (componentRef) {
            safeInvoke('OnParamsChanged', centerX, centerY, zoom);
        }

        // Re-render
        window.renderMandelbrot();
    };

    // Reset to default view
    window.resetMandelbrot = function() {
        centerX = -0.5;
        centerY = 0;
        zoom = 1;
        
        if (componentRef) {
            safeInvoke('OnParamsChanged', centerX, centerY, zoom);
        }
        
        window.renderMandelbrot();
        console.log('[Mandelbrot v1] Reset to default view');
    };

    // Get coordinates from canvas position
    window.getMandelbrotCoords = function(canvasX, canvasY) {
        const width = canvas.width;
        const height = canvas.height;
        
        const aspectRatio = width / height;
        const viewHeight = 4 / zoom;
        const viewWidth = viewHeight * aspectRatio;
        const minX = centerX - viewWidth / 2;
        const minY = centerY - viewHeight / 2;

        const realX = minX + (canvasX / width) * viewWidth;
        const realY = minY + (canvasY / height) * viewHeight;

        return { x: realX, y: realY };
    };

    // Utility: debounce
    function debounce(func, wait) {
        let timeout;
        return function executedFunction(...args) {
            clearTimeout(timeout);
            timeout = setTimeout(() => func(...args), wait);
        };
    }

    // Get canvas rect
    window.getMandelbrotCanvasRect = function() {
        if (!canvas) return null;
        const rect = canvas.getBoundingClientRect();
        return { left: rect.left, top: rect.top, width: rect.width, height: rect.height };
    };

    // Cleanup when leaving page
    window.cleanupMandelbrot = function() {
        console.log('[Mandelbrot v1] Cleaning up...');
        isRendering = false;
        componentRef = null;
    };

    console.log('[Mandelbrot v1] mandelbrot-game.js loaded');
})();
