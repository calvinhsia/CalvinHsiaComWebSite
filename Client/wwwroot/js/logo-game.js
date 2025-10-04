// Logo Turtle Graphics JavaScript Functions

// Global logo game state
window.logoState = {
    canvas: null,
    ctx: null,
    isInitialized: false,
    animationSpeed: 50 // milliseconds between animation frames
};

// Initialize the Logo canvas
window.initLogoCanvas = function(canvasId) {
    try {
        console.log('[Logo] Initializing canvas:', canvasId);
        
        const canvas = document.getElementById(canvasId);
        if (!canvas) {
            console.error('[Logo] Canvas not found:', canvasId);
            return false;
        }

        const ctx = canvas.getContext('2d');
        if (!ctx) {
            console.error('[Logo] Could not get 2D context');
            return false;
        }

        // Store references
        window.logoState.canvas = canvas;
        window.logoState.ctx = ctx;
        window.logoState.isInitialized = true;

        // Set canvas size
        canvas.width = 500;
        canvas.height = 500;

        // Clear canvas with white background
        ctx.fillStyle = '#FFFFFF';
        ctx.fillRect(0, 0, canvas.width, canvas.height);

        // Set initial drawing properties
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';

        console.log('[Logo] Canvas initialized successfully');
        return true;
    } catch (error) {
        console.error('[Logo] Error initializing canvas:', error);
        return false;
    }
};

// Clear the entire canvas
window.logoClearCanvas = function() {
    if (!window.logoState.isInitialized) return false;
    
    try {
        const ctx = window.logoState.ctx;
        const canvas = window.logoState.canvas;
        
        // Clear with white background
        ctx.fillStyle = '#FFFFFF';
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        
        console.log('[Logo] Canvas cleared');
        return true;
    } catch (error) {
        console.error('[Logo] Error clearing canvas:', error);
        return false;
    }
};

// Draw the complete Logo graphics state
window.logoDrawCanvas = function(gameState) {
    if (!window.logoState.isInitialized) {
        console.error('[Logo] Canvas not initialized');
        return false;
    }

    try {
        const ctx = window.logoState.ctx;
        const canvas = window.logoState.canvas;
        
        // Clear canvas
        ctx.fillStyle = gameState.canvas.backgroundColor || '#FFFFFF';
        ctx.fillRect(0, 0, canvas.width, canvas.height);

        // Draw all drawing elements
        gameState.drawingElements.forEach(element => {
            if (element.type === 0) { // LogoDrawingType.Line = 0
                drawLine(ctx, element);
            }
        });

        // Draw turtle if visible
        if (gameState.turtle.isVisible) {
            drawTurtle(ctx, gameState.turtle);
        }

        console.log(`[Logo] Drew ${gameState.drawingElements.length} elements`);
        return true;
    } catch (error) {
        console.error('[Logo] Error drawing canvas:', error);
        return false;
    }
};

// Draw a line element
function drawLine(ctx, element) {
    try {
        ctx.beginPath();
        ctx.moveTo(element.startX, element.startY);
        ctx.lineTo(element.endX, element.endY);
        ctx.strokeStyle = element.color || '#000000';
        ctx.lineWidth = element.width || 1;
        ctx.stroke();
    } catch (error) {
        console.error('[Logo] Error drawing line:', error);
    }
}

// Draw the turtle
function drawTurtle(ctx, turtle) {
    try {
        const x = turtle.x;
        const y = turtle.y;
        const heading = turtle.heading;
        const size = 10;

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
    } catch (error) {
        console.error('[Logo] Error drawing turtle:', error);
    }
}

// Animate drawing with progressive reveal
window.logoAnimateDrawing = function(gameState, speed = 50) {
    if (!window.logoState.isInitialized) return false;

    try {
        const ctx = window.logoState.ctx;
        const canvas = window.logoState.canvas;
        const elements = gameState.drawingElements;
        
        // Clear canvas
        ctx.fillStyle = gameState.canvas.backgroundColor || '#FFFFFF';
        ctx.fillRect(0, 0, canvas.width, canvas.height);

        // Animate elements one by one
        let elementIndex = 0;
        const animateNext = () => {
            if (elementIndex < elements.length) {
                const element = elements[elementIndex];
                if (element.type === 0) { // LogoDrawingType.Line
                    drawLine(ctx, element);
                }
                elementIndex++;
                setTimeout(animateNext, speed);
            } else {
                // Animation complete, draw turtle
                if (gameState.turtle.isVisible) {
                    drawTurtle(ctx, gameState.turtle);
                }
                console.log('[Logo] Animation complete');
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
    if (!window.logoState.isInitialized) return false;

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
        console.log('[Logo] Grid drawn');
        return true;
    } catch (error) {
        console.error('[Logo] Error drawing grid:', error);
        return false;
    }
};

// Save canvas as image
window.logoSaveImage = function(filename = 'logo-drawing.png') {
    if (!window.logoState.isInitialized) return false;

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
        
        console.log('[Logo] Image saved:', filename);
        return true;
    } catch (error) {
        console.error('[Logo] Error saving image:', error);
        return false;
    }
};

// Get canvas as base64 image data
window.logoGetImageData = function() {
    if (!window.logoState.isInitialized) return null;

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
    if (!window.logoState.isInitialized) return false;

    try {
        const canvas = window.logoState.canvas;
        canvas.width = width;
        canvas.height = height;
        
        // Clear with white background
        const ctx = window.logoState.ctx;
        ctx.fillStyle = '#FFFFFF';
        ctx.fillRect(0, 0, width, height);
        
        console.log(`[Logo] Canvas resized to ${width}x${height}`);
        return true;
    } catch (error) {
        console.error('[Logo] Error resizing canvas:', error);
        return false;
    }
};

// Initialize Logo when DOM is loaded
document.addEventListener('DOMContentLoaded', function() {
    console.log('[Logo] DOM loaded, ready for Logo canvas initialization');
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
        logoResizeCanvas: window.logoResizeCanvas
    };
}