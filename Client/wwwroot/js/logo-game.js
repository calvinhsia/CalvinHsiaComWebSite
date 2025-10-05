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
window.setLogoDebug = function(enabled) {
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

// Initialize the Logo canvas
window.initLogoCanvas = function(canvasId) {
    try {
        debugLog('[Logo] Initializing canvas:', canvasId);
        
        // Wait a bit for DOM to be ready
        const canvas = document.getElementById(canvasId);
        if (!canvas) {
            debugError('[Logo] Canvas not found:', canvasId);
            // Try to find it after a small delay
            setTimeout(() => {
                const retryCanvas = document.getElementById(canvasId);
                if (retryCanvas) {
                    debugLog('[Logo] Canvas found on retry');
                    return window.initLogoCanvas(canvasId);
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

        // Set canvas size explicitly
        canvas.width = 500;
        canvas.height = 500;
        
        // Ensure canvas is visible
        canvas.style.display = 'block';
        canvas.style.border = '1px solid #ccc';

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
window.logoClearCanvas = function() {
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

// Draw the complete Logo graphics state
window.logoDrawCanvas = function(gameState) {
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
        const size = 10;

        debugLog(`[Logo] Drawing turtle at (${x}, ${y}) heading ${heading}°`);

        // Save context
        ctx.save();

        // Translate to turtle position
        ctx.translate(x, y);
        
        // Rotate to turtle heading (Logo: 0=up, clockwise)
        ctx.rotate((heading - 90) * Math.PI / 180);

        // Draw turtle shape (triangle pointing in heading direction)
        ctx.beginPath();
        ctx.moveTo(size, 0);           // Point forward
        ctx.lineTo(-size/2, -size/2);  // Back left
        ctx.lineTo(-size/2, size/2);   // Back right
        ctx.closePath();

        // Fill turtle
        ctx.fillStyle = '#00AA00';  // Green turtle
        ctx.fill();
        
        // Outline turtle
        ctx.strokeStyle = '#006600'; // Dark green outline
        ctx.lineWidth = 1;
        ctx.stroke();

        // Restore context
        ctx.restore();
        
        debugLog('[Logo] Turtle drawn successfully');
    } catch (error) {
        console.error('[Logo] Error drawing turtle:', error, turtle);
    }
}

// Animate drawing with progressive reveal
window.logoAnimateDrawing = function(gameState, speed = 50) {
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
window.logoDrawGrid = function(gridSize = 25) {
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
window.logoSaveImage = function(filename = 'logo-drawing.png') {
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
window.logoGetImageData = function() {
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
window.logoResizeCanvas = function(width, height) {
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
window.logoDebugState = function() {
    const state = {
        isInitialized: window.logoState.isInitialized,
        hasCanvas: !!window.logoState.canvas,
        hasContext: !!window.logoState.ctx,
        debugEnabled: window.logoState.debugEnabled,
        canvasElement: document.getElementById('logoCanvas')
    };
    console.log('[Logo] Debug state:', state);
    return state;
};

// Initialize Logo when DOM is loaded
document.addEventListener('DOMContentLoaded', function() {
    debugLog('[Logo] DOM loaded, ready for Logo canvas initialization');
});

// Export functions for testing
if (typeof module !== 'undefined' && module.exports) {
    module.exports = {
        initLogoCanvas: window.initLogoCanvas,
        logoClearCanvas: window.logoClearCanvas,
        logoDrawCanvas: window.logoDrawCanvas,
        logoAnimateDrawing: window.logoAnimateDrawing,
        logoDrawGrid: window.logoDrawGrid,
        logoSaveImage: window.logoSaveImage,
        logoGetImageData: window.logoGetImageData,
        logoResizeCanvas: window.logoResizeCanvas,
        logoDebugState: window.logoDebugState,
        setLogoDebug: window.setLogoDebug
    };
}