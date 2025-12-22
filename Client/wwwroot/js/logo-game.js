// Logo Turtle Graphics JavaScript Functions

// Global logo game state
window.logoState = {
    canvas: null,
    ctx: null,
    isInitialized: false,
    animationSpeed: 50, // milliseconds between animation frames
    debugEnabled: false // Add debug flag
};

// Enable/disable debug logging
window.setLogoDebug = function (enabled) {
    window.logoState.debugEnabled = enabled;
    console.log(`[Logo] Debug logging ${enabled ? 'enabled' : 'disabled'}`);
};

// Helper function for conditional logging
function debugLog(message, ...args) {
    if (window.logoState.debugEnabled) {
        console.log(message, ...args);
    }
}

function debugError(message, ...args) {
    if (window.logoState.debugEnabled) {
        console.error(message, ...args);
    }
}

// DEBUG: Service worker diagnostics
window.debugServiceWorker = function () {
    console.log('[Logo Debug] === SERVICE WORKER DIAGNOSTICS ===');

    if ('serviceWorker' in navigator) {
        console.log('[Logo Debug] Service Worker API available');

        navigator.serviceWorker.getRegistration().then(registration => {
            if (registration) {
                console.log('[Logo Debug] Service Worker Registration:', {
                    scope: registration.scope,
                    active: !!registration.active,
                    waiting: !!registration.waiting,
                    installing: !!registration.installing
                });

                if (registration.active) {
                    console.log('[Logo Debug] Active SW script URL:', registration.active.scriptURL);
                    console.log('[Logo Debug] Active SW state:', registration.active.state);
                }
            } else {
                console.log('[Logo Debug] No service worker registration found');
            }
        }).catch(error => {
            console.error('[Logo Debug] Error getting SW registration:', error);
        });

        // Check if we have a controller
        if (navigator.serviceWorker.controller) {
            console.log('[Logo Debug] SW Controller URL:', navigator.serviceWorker.controller.scriptURL);
        } else {
            console.log('[Logo Debug] No service worker controller');
        }

        // Check caches
        if ('caches' in window) {
            caches.keys().then(cacheNames => {
                console.log('[Logo Debug] Available caches:', cacheNames);
                return Promise.all(cacheNames.map(name =>
                    caches.open(name).then(cache =>
                        cache.keys().then(keys => ({ name, count: keys.length, keys: keys.map(k => k.url) }))
                    )
                ));
            }).then(cacheDetails => {
                console.log('[Logo Debug] Cache contents:', cacheDetails);
            });
        }
    } else {
        console.log('[Logo Debug] Service Worker API not available');
    }

    return {
        timestamp: new Date().toISOString(),
        userAgent: navigator.userAgent,
        url: window.location.href
    };
};

// DEBUG: Force refresh all resources
window.forceRefreshResources = function () {
    console.log('[Logo Debug] Forcing refresh of all resources...');

    // Clear service worker caches
    if ('caches' in window) {
        caches.keys().then(cacheNames => {
            return Promise.all(cacheNames.map(name => caches.delete(name)));
        }).then(() => {
            console.log('[Logo Debug] All caches cleared');

            // Force reload the page with no cache
            window.location.reload(true);
        });
    } else {
        // Fallback: just reload
        window.location.reload(true);
    }
};

// Initialize the Logo canvas
window.initLogoCanvas = function (canvasId, width, height) {
    try {
        debugLog('[Logo] Initializing canvas:', canvasId, width, 'x', height);

        const canvas = document.getElementById(canvasId);
        if (!canvas) {
            debugError('[Logo] Canvas not found:', canvasId);
            // Try to find it after a small delay
            setTimeout(() => {
                const retryCanvas = document.getElementById(canvasId);
                if (retryCanvas) {
                    debugLog('[Logo] Canvas found on retry');
                    return window.initLogoCanvas(canvasId, width, height);
                }
            }, 100);
            return false;
        }

        const ctx = canvas.getContext('2d');
        if (!ctx) {
            debugError('[Logo] Could not get 2D context');
            return false;
        }

        // Store references
        window.logoState.canvas = canvas;
        window.logoState.ctx = ctx;
        window.logoState.isInitialized = true;

        // Set canvas size
        canvas.width = width || 500;
        canvas.height = height || 500;

        // Ensure canvas is visible
        canvas.style.display = 'block';

        // Clear canvas with white background
        ctx.fillStyle = '#FFFFFF';
        ctx.fillRect(0, 0, canvas.width, canvas.height);

        // Set initial drawing properties
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';

        debugLog('[Logo] Canvas initialized successfully', {
            width: canvas.width,
            height: canvas.height,
            context: !!ctx
        });
        return true;
    } catch (error) {
        console.error('[Logo] Error initializing canvas:', error);
        return false;
    }
};

// Clear the entire canvas
window.logoClearCanvas = function () {
    if (!window.logoState.isInitialized) {
        debugError('[Logo] Canvas not initialized for clear operation');
        return false;
    }

    try {
        const ctx = window.logoState.ctx;
        const canvas = window.logoState.canvas;

        // Clear with white background
        ctx.fillStyle = '#FFFFFF';
        ctx.fillRect(0, 0, canvas.width, canvas.height);

        debugLog('[Logo] Canvas cleared');
        return true;
    } catch (error) {
        console.error('[Logo] Error clearing canvas:', error);
        return false;
    }
};

// NEW: Draw a single drawing element immediately (for immediate/animated rendering)
window.logoDrawElement = function (element) {
    if (!window.logoState.isInitialized) {
        debugError('[Logo] Canvas not initialized for element drawing');
        return false;
    }

    try {
        debugLog('[Logo] Drawing single element:', element);

        const ctx = window.logoState.ctx;

        // Validate element structure
        if (!element) {
            debugError('[Logo] Element is null or undefined');
            return false;
        }

        // Draw based on element type
        if (element.type === 0) { // LogoDrawingType.Line = 0
            drawLine(ctx, element);
            debugLog('[Logo] Single line element drawn');
            return true;
        } else {
            debugLog('[Logo] Unknown element type:', element.type);
            return false;
        }
    } catch (error) {
        console.error('[Logo] Error drawing single element:', error);
        return false;
    }
};

// NEW: Update turtle position/display (for immediate/animated rendering)
window.logoUpdateTurtle = function (turtle) {
    if (!window.logoState.isInitialized) {
        debugError('[Logo] Canvas not initialized for turtle update');
        return false;
    }

    try {
        debugLog('[Logo] Updating turtle:', turtle);

        const ctx = window.logoState.ctx;
        const canvas = window.logoState.canvas;

        // Only draw the turtle if it's visible
        if (turtle && turtle.isVisible) {
            drawTurtle(ctx, turtle);
            debugLog('[Logo] Turtle updated and drawn');
            return true;
        } else {
            debugLog('[Logo] Turtle is hidden, not drawing');
            return true;
        }
    } catch (error) {
        console.error('[Logo] Error updating turtle:', error);
        return false;
    }
};

// NEW: Execute canvas operations (for immediate/animated rendering)
window.logoExecuteCanvasOperation = function (operation) {
    if (!window.logoState.isInitialized) {
        debugError('[Logo] Canvas not initialized for canvas operation');
        return false;
    }

    try {
        debugLog('[Logo] Executing canvas operation:', operation);

        if (!operation) {
            debugError('[Logo] Operation is null or undefined');
            return false;
        }

        switch (operation.type) {
            case 0: // Clear
                return window.logoClearCanvas();

            case 1: // SetBackgroundColor
                const ctx = window.logoState.ctx;
                const canvas = window.logoState.canvas;
                const color = operation.parameters?.color || '#FFFFFF';
                ctx.fillStyle = color;
                ctx.fillRect(0, 0, canvas.width, canvas.height);
                debugLog('[Logo] Background color set to:', color);
                return true;

            case 2: // ShowTurtle
            case 3: // HideTurtle
                // These are handled by the turtle visibility property
                // No immediate action needed here
                debugLog('[Logo] Turtle visibility operation processed');
                return true;

            default:
                debugLog('[Logo] Unknown canvas operation type:', operation.type);
                return false;
        }
    } catch (error) {
        console.error('[Logo] Error executing canvas operation:', error);
        return false;
    }
};

// Draw the complete Logo graphics state
window.logoDrawCanvas = function (gameState) {
    debugLog('[Logo] logoDrawCanvas called with:', gameState);

    if (!window.logoState) {
        debugError('[Logo] window.logoState is undefined');
        return false;
    }

    debugLog('[Logo] logoState.isInitialized:', window.logoState.isInitialized);
    debugLog('[Logo] logoState.canvas:', !!window.logoState.canvas);
    debugLog('[Logo] logoState.ctx:', !!window.logoState.ctx);

    if (!window.logoState.isInitialized) {
        console.error('[Logo] Canvas not initialized');
        return false;
    }

    try {
        debugLog('[Logo] Drawing canvas with gameState:', gameState);

        const ctx = window.logoState.ctx;
        const canvas = window.logoState.canvas;

        // Validate gameState structure
        if (!gameState) {
            console.error('[Logo] gameState is null or undefined');
            return false;
        }

        debugLog('[Logo] gameState type:', typeof gameState);
        debugLog('[Logo] gameState keys:', Object.keys(gameState));

        // Ensure required properties exist
        if (!gameState.canvas) {
            debugLog('[Logo] gameState.canvas is missing, using defaults');
            gameState.canvas = { backgroundColor: '#FFFFFF' };
        }

        if (!gameState.drawingElements) {
            debugLog('[Logo] gameState.drawingElements is missing, using empty array');
            gameState.drawingElements = [];
        }

        if (!gameState.turtle) {
            debugLog('[Logo] gameState.turtle is missing, using defaults');
            gameState.turtle = { x: 250, y: 250, heading: 0, isVisible: true };
        }

        debugLog(`[Logo] About to draw ${gameState.drawingElements.length} drawing elements`);

        // Clear canvas
        const backgroundColor = gameState.canvas.backgroundColor || '#FFFFFF';
        ctx.fillStyle = backgroundColor;
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        debugLog('[Logo] Canvas cleared with background:', backgroundColor);

        // Draw all drawing elements (only log in debug mode)
        if (window.logoState.debugEnabled) {
            console.log(`[Logo] Drawing ${gameState.drawingElements.length} elements`);
        }

        gameState.drawingElements.forEach((element, index) => {
            debugLog(`[Logo] Drawing element ${index}:`, element);
            if (element.type === 0) { // LogoDrawingType.Line = 0
                drawLine(ctx, element);
            }
        });

        // Draw turtle if visible
        if (gameState.turtle && gameState.turtle.isVisible) {
            debugLog('[Logo] Drawing turtle at:', gameState.turtle);
            drawTurtle(ctx, gameState.turtle);
        } else {
            debugLog('[Logo] Turtle is hidden or missing, not drawing');
        }

        debugLog(`[Logo] Canvas drawing complete - Drew ${gameState.drawingElements.length} elements`);
        return true;
    } catch (error) {
        console.error('[Logo] Error drawing canvas:', error);
        console.error('[Logo] Error stack:', error.stack);
        return false;
    }
};

// Draw a line element
function drawLine(ctx, element) {
    try {
        debugLog('[Logo] Drawing line:', element);

        // Validate line coordinates
        if (typeof element.startX !== 'number' || typeof element.startY !== 'number' ||
            typeof element.endX !== 'number' || typeof element.endY !== 'number') {
            debugError('[Logo] Invalid line coordinates:', element);
            return;
        }

        ctx.beginPath();
        ctx.moveTo(element.startX, element.startY);
        ctx.lineTo(element.endX, element.endY);
        ctx.strokeStyle = element.color || '#000000';
        ctx.lineWidth = element.width || 1;
        ctx.stroke();

        debugLog(`[Logo] Line drawn from (${element.startX}, ${element.startY}) to (${element.endX}, ${element.endY})`);
    } catch (error) {
        console.error('[Logo] Error drawing line:', error, element);
    }
}

// Draw the turtle
function drawTurtle(ctx, turtle) {
    try {
        // Validate turtle properties
        if (typeof turtle.x !== 'number' || typeof turtle.y !== 'number' || typeof turtle.heading !== 'number') {
            debugError('[Logo] Invalid turtle properties:', turtle);
            return;
        }

        const x = turtle.x;
        const y = turtle.y;
        const heading = turtle.heading;
        const scale = 0.8; // Scale factor for the turtle (SVG is 40x40, we'll scale it down a bit)

        debugLog(`[Logo] Drawing turtle at (${x}, ${y}) heading ${heading}°`);

        // Save context
        ctx.save();

        // Translate to turtle position
        ctx.translate(x, y);

        // Rotate to turtle heading (Logo: 0=up, clockwise)
        // Note: SVG turtle naturally points up, so we rotate from there
        ctx.rotate(heading * Math.PI / 180);

        // Scale the turtle
        ctx.scale(scale, scale);

        // Center the turtle (SVG is 40x40 with turtle centered at 20,20)
        ctx.translate(-20, -20);

        // Draw turtle based on SVG from header
        // The SVG turtle is much more detailed than the simple triangle

        // Turtle tail (bottom)
        ctx.fillStyle = '#2c3e50';
        ctx.beginPath();
        ctx.ellipse(20, 32, 2, 3, 0, 0, Math.PI * 2);
        ctx.fill();

        // Turtle legs
        ctx.fillStyle = '#2c3e50';
        // Back left leg
        ctx.beginPath();
        ctx.ellipse(12, 28, 3, 2, 0, 0, Math.PI * 2);
        ctx.fill();
        // Back right leg
        ctx.beginPath();
        ctx.ellipse(28, 28, 3, 2, 0, 0, Math.PI * 2);
        ctx.fill();
        // Front left leg
        ctx.beginPath();
        ctx.ellipse(14, 16, 2, 3, 0, 0, Math.PI * 2);
        ctx.fill();
        // Front right leg
        ctx.beginPath();
        ctx.ellipse(26, 16, 2, 3, 0, 0, Math.PI * 2);
        ctx.fill();

        // Turtle shell (main body)
        ctx.fillStyle = '#27ae60';
        ctx.beginPath();
        ctx.ellipse(20, 22, 12, 10, 0, 0, Math.PI * 2);
        ctx.fill();

        // Shell pattern (darker green detail)
        ctx.fillStyle = '#229954';
        ctx.beginPath();
        ctx.moveTo(15, 20);
        ctx.quadraticCurveTo(20, 18, 25, 20);
        ctx.quadraticCurveTo(20, 24, 15, 20);
        ctx.fill();

        // Turtle head (front)
        ctx.fillStyle = '#2ecc71';
        ctx.beginPath();
        ctx.arc(20, 12, 6, 0, Math.PI * 2);
        ctx.fill();

        // Eyes
        ctx.fillStyle = 'black';
        // Left eye
        ctx.beginPath();
        ctx.arc(18, 10, 1, 0, Math.PI * 2);
        ctx.fill();
        // Right eye
        ctx.beginPath();
        ctx.arc(22, 10, 1, 0, Math.PI * 2);
        ctx.fill();

        // Restore context
        ctx.restore();

        debugLog('[Logo] Turtle drawn successfully');
    } catch (error) {
        console.error('[Logo] Error drawing turtle:', error, turtle);
    }
}

// Animate drawing with progressive reveal
window.logoAnimateDrawing = function (gameState, speed = 50) {
    if (!window.logoState.isInitialized) {
        debugError('[Logo] Canvas not initialized for animation');
        return false;
    }

    try {
        debugLog('[Logo] Starting animation with speed:', speed);

        const ctx = window.logoState.ctx;
        const canvas = window.logoState.canvas;
        const elements = gameState.drawingElements || [];

        // Clear canvas
        const backgroundColor = gameState.canvas?.backgroundColor || '#FFFFFF';
        ctx.fillStyle = backgroundColor;
        ctx.fillRect(0, 0, canvas.width, canvas.height);

        // Animate elements one by one
        let elementIndex = 0;
        const animateNext = () => {
            if (elementIndex < elements.length) {
                const element = elements[elementIndex];
                debugLog(`[Logo] Animating element ${elementIndex}:`, element);
                if (element.type === 0) { // LogoDrawingType.Line
                    drawLine(ctx, element);
                }
                elementIndex++;
                setTimeout(animateNext, speed);
            } else {
                // Animation complete, draw turtle
                if (gameState.turtle && gameState.turtle.isVisible) {
                    drawTurtle(ctx, gameState.turtle);
                }
                debugLog('[Logo] Animation complete');
            }
        };

        animateNext();
        return true;
    } catch (error) {
        console.error('[Logo] Error animating drawing:', error);
        return false;
    }
};

// Draw a grid on the canvas for reference
window.logoDrawGrid = function (gridSize = 25) {
    if (!window.logoState.isInitialized) {
        debugError('[Logo] Canvas not initialized for grid drawing');
        return false;
    }

    try {
        const ctx = window.logoState.ctx;
        const canvas = window.logoState.canvas;

        ctx.save();
        ctx.strokeStyle = '#E0E0E0'; // Light gray
        ctx.lineWidth = 0.5;

        // Vertical lines
        for (let x = 0; x <= canvas.width; x += gridSize) {
            ctx.beginPath();
            ctx.moveTo(x, 0);
            ctx.lineTo(x, canvas.height);
            ctx.stroke();
        }

        // Horizontal lines
        for (let y = 0; y <= canvas.height; y += gridSize) {
            ctx.beginPath();
            ctx.moveTo(0, y);
            ctx.lineTo(canvas.width, y);
            ctx.stroke();
        }

        // Draw center point
        const centerX = canvas.width / 2;
        const centerY = canvas.height / 2;

        ctx.fillStyle = '#FF0000'; // Red center point
        ctx.beginPath();
        ctx.arc(centerX, centerY, 3, 0, 2 * Math.PI);
        ctx.fill();

        ctx.restore();
        debugLog('[Logo] Grid drawn with size:', gridSize);
        return true;
    } catch (error) {
        console.error('[Logo] Error drawing grid:', error);
        return false;
    }
};

// Save canvas as image
window.logoSaveImage = function (filename = 'logo-drawing.png') {
    if (!window.logoState.isInitialized) {
        debugError('[Logo] Canvas not initialized for save operation');
        return false;
    }

    try {
        const canvas = window.logoState.canvas;

        // Create download link
        const link = document.createElement('a');
        link.download = filename;
        link.href = canvas.toDataURL('image/png');

        // Trigger download
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);

        debugLog('[Logo] Image saved:', filename);
        return true;
    } catch (error) {
        console.error('[Logo] Error saving image:', error);
        return false;
    }
};

// Get canvas as base64 image data
window.logoGetImageData = function () {
    if (!window.logoState.isInitialized) {
        debugError('[Logo] Canvas not initialized for image data operation');
        return null;
    }

    try {
        const canvas = window.logoState.canvas;
        return canvas.toDataURL('image/png');
    } catch (error) {
        console.error('[Logo] Error getting image data:', error);
        return null;
    }
};

// Resize canvas
window.logoResizeCanvas = function (width, height) {
    if (!window.logoState.isInitialized) {
        debugError('[Logo] Canvas not initialized for resize operation');
        return false;
    }

    try {
        const canvas = window.logoState.canvas;
        canvas.width = width;
        canvas.height = height;

        // Clear with white background
        const ctx = window.logoState.ctx;
        ctx.fillStyle = '#FFFFFF';
        ctx.fillRect(0, 0, width, height);

        debugLog(`[Logo] Canvas resized to ${width}x${height}`);
        return true;
    } catch (error) {
        console.error('[Logo] Error resizing canvas:', error);
        return false;
    }
};

// Debug function to check state
window.logoDebugState = function () {
    const state = {
        isInitialized: window.logoState.isInitialized,
        hasCanvas: !!window.logoState.canvas,
        hasContext: !!window.logoState.ctx,
        debugEnabled: window.logoState.debugEnabled,
        canvasElement: document.getElementById('logoCanvas'),
        serviceWorkerDiagnostics: window.debugServiceWorker?.()
    };
    console.log('[Logo] Debug state:', state);
    return state;
};

// Setup canvas resize handling (similar to Fish game)
window.setupLogoResize = function () {
    console.log('[Logo] Setting up resize listener');

    window.resizeLogoCanvas = function (skipCallback) {
        const canvas = document.getElementById('logoCanvas');
        if (!canvas) return;

        const section = canvas.closest('.logo-canvas-section');
        if (!section) return;

        const sectionWidth = section.clientWidth;
        const sectionHeight = section.clientHeight;

        // Calculate size to fill the section while maintaining aspect ratio
        const aspectRatio = 1; // Square canvas like before, but now responsive
        let newWidth = sectionWidth;
        let newHeight = sectionHeight;

        // Maintain square aspect ratio - use the smaller dimension
        const size = Math.min(newWidth, newHeight);
        newWidth = size;
        newHeight = size;

        const widthChanged = Math.abs(canvas.width - newWidth) > 2;
        const heightChanged = Math.abs(canvas.height - newHeight) > 2;
        
        if (widthChanged || heightChanged) {
            console.log('[Logo] Resizing canvas:', newWidth, 'x', newHeight);
            
            // Save current drawing
            const tempCanvas = document.createElement('canvas');
            tempCanvas.width = canvas.width;
            tempCanvas.height = canvas.height;
            const tempCtx = tempCanvas.getContext('2d');
            tempCtx.drawImage(canvas, 0, 0);

            canvas.width = newWidth;
            canvas.height = newHeight;
            canvas.style.width = newWidth + 'px';
            canvas.style.height = newHeight + 'px';

            // Restore drawing scaled to new size
            const ctx = canvas.getContext('2d');
            ctx.fillStyle = '#FFFFFF';
            ctx.fillRect(0, 0, canvas.width, canvas.height);
            ctx.drawImage(tempCanvas, 0, 0, tempCanvas.width, tempCanvas.height,
                0, 0, canvas.width, canvas.height);

            // Re-apply drawing properties
            ctx.lineCap = 'round';
            ctx.lineJoin = 'round';

            if (!skipCallback && window.logoComponentRef) {
                window.logoComponentRef.invokeMethodAsync('OnCanvasResized', newWidth, newHeight);
            }
        }
    };

    window.resizeLogoCanvas(false);

    window.addEventListener('resize', function () {
        window.resizeLogoCanvas(false);
    });
};

// Set component reference
window.setLogoComponentRef = function (dotNetRef) {
    window.logoComponentRef = dotNetRef;
    console.log('[Logo] Component reference set');
};

// Initialize Logo when DOM is loaded
document.addEventListener('DOMContentLoaded', function () {
    debugLog('[Logo] DOM loaded, ready for Logo canvas initialization');
});

// Export functions for testing
if (typeof module !== 'undefined' && module.exports) {
    module.exports = {
        initLogoCanvas: window.initLogoCanvas,
        logoClearCanvas: window.logoClearCanvas,
        logoDrawCanvas: window.logoDrawCanvas,
        logoDrawElement: window.logoDrawElement,
        logoUpdateTurtle: window.logoUpdateTurtle,
        logoExecuteCanvasOperation: window.logoExecuteCanvasOperation,
        logoAnimateDrawing: window.logoAnimateDrawing,
        logoDrawGrid: window.logoDrawGrid,
        logoSaveImage: window.logoSaveImage,
        logoGetImageData: window.logoGetImageData,
        logoResizeCanvas: window.logoResizeCanvas,
        logoDebugState: window.logoDebugState,
        setLogoDebug: window.setLogoDebug,
        debugServiceWorker: window.debugServiceWorker,
        forceRefreshResources: window.forceRefreshResources
    };
}