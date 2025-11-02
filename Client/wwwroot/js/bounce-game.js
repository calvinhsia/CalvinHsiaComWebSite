// Bounce Game JavaScript Functions
console.log('[Bounce] bounce-game.js loading... v3');

(function () {
    'use strict';

    let bounceCanvas = null;
    let bounceContext = null;
    let bounceResizeTimeout = null;

    // Initialize bounce canvas
    window.initBounceCanvas = function (canvasId) {
        console.log('[Bounce] Initializing canvas:', canvasId);

        bounceCanvas = document.getElementById(canvasId);
        if (!bounceCanvas) {
            console.error('[Bounce] Canvas not found:', canvasId);
            return false;
        }

        bounceContext = bounceCanvas.getContext('2d');
        if (!bounceContext) {
            console.error('[Bounce] Could not get 2D context');
            return false;
        }

        // Store context globally for rendering
        window.bounceContext = bounceContext;
        window.bounceCanvas = bounceCanvas;

        console.log('[Bounce] Canvas initialized successfully');
        return true;
    };

    // NEW: Helper function to set component reference
    window.setBounceComponentRef = function (dotNetRef) {
        window.bounceComponentRef = dotNetRef;
        console.log('[Bounce] Component reference set');
    };

    /**
     * Setup canvas resize handling with debouncing
     */
    window.setupBounceResize = function () {
        console.log('[Bounce] Setting up resize listener');

        // Define resize handler function
        window.resizeBounceCanvas = function (skipCallback) {
            const canvas = document.getElementById('bounceCanvas');
            if (!canvas) {
                console.log('[Bounce] Canvas not found!');
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
                console.log('[Bounce] Resizing canvas from', canvas.width, 'x', canvas.height, 'to', newWidth, 'x', newHeight);

                // Set canvas internal resolution to match container size
                canvas.width = newWidth;
                canvas.height = newHeight;

                console.log('[Bounce] Resize complete. Canvas dimensions:', canvas.width, 'x', canvas.height);

                // Clear any pending timeout
                if (bounceResizeTimeout) {
                    clearTimeout(bounceResizeTimeout);
                }

                // Notify Blazor of the resize with actual dimensions (unless skipped for initial setup)
                if (!skipCallback && window.bounceComponentRef) {
                    window.bounceComponentRef.invokeMethodAsync('OnCanvasResized', newWidth, newHeight);
                }
            } else {
                console.log('[Bounce] Resize skipped - dimensions unchanged (±10px threshold)');
            }
        };

        // Initial resize - skip callback since we're still initializing
        console.log('[Bounce] Running initial resize (skipping callback)');
        window.resizeBounceCanvas(true);

        // Add resize listener
        window.addEventListener('resize', function () {
            console.log('[Bounce] Window resize event fired');
            window.resizeBounceCanvas(false); // Normal resize - trigger callback
        });
        console.log('[Bounce] Resize listener installed');
    };

    /**
     * Get canvas dimensions
 * @returns {object} Canvas width and height
     */
    window.getBounceCanvasDimensions = function () {
        const canvas = document.getElementById('bounceCanvas');
        if (!canvas) {
            console.error('[Bounce] Canvas not found');
            return { width: 0, height: 0 };
        }
        return {
            width: canvas.width,
            height: canvas.height
        };
    };

    // Render frame with bouncing balls
    window.bounceRenderFrame = function (balls) {
        if (!bounceContext || !bounceCanvas) {
            console.error('[Bounce] Canvas not initialized');
            return;
        }

        const ctx = bounceContext;
        const canvas = bounceCanvas;

        // Clear canvas with black background
        ctx.fillStyle = '#000';
        ctx.fillRect(0, 0, canvas.width, canvas.height);

        // Render balls
        if (balls && balls.length > 0) {
            balls.forEach((ball) => {
                if (ball && typeof ball.x === 'number' && typeof ball.y === 'number') {
                    ctx.beginPath();
                    ctx.arc(ball.x, ball.y, ball.radius, 0, Math.PI * 2);
                    ctx.fillStyle = ball.color || '#FFFFFF';
                    ctx.fill();

                    // Add a subtle glow effect
                    ctx.strokeStyle = ball.color || '#FFFFFF';
                    ctx.lineWidth = 2;
                    ctx.stroke();
                }
            });
        }
    };

    // Auto-initialize if on Bounce page
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () {
            if (window.location.pathname.includes('/bounce')) {
                console.log('[Bounce] Page loaded, waiting for canvas...');
            }
        });
    } else {
        if (window.location.pathname.includes('/bounce')) {
            console.log('[Bounce] Page already loaded');
        }
    }

    // Log that the script has loaded
    console.log('[Bounce] bounce-game.js loaded successfully');
})();
