// Cartoon Drawing Game JavaScript
// Handles canvas drawing operations for the Cartoon page

let cartoonCanvas = null;
let cartoonCtx = null;

/**
 * Initialize the cartoon canvas
 * @param {string} canvasId - ID of the canvas element
 */
window.initCartoonCanvas = function (canvasId) {
    console.log('[Cartoon] Initializing canvas:', canvasId);
    
    cartoonCanvas = document.getElementById(canvasId);
    if (!cartoonCanvas) {
        console.error('[Cartoon] Canvas element not found:', canvasId);
        return false;
    }

    cartoonCtx = cartoonCanvas.getContext('2d');
    if (!cartoonCtx) {
        console.error('[Cartoon] Could not get 2D context');
        return false;
    }

    // Set canvas background to white
    cartoonCtx.fillStyle = 'white';
    cartoonCtx.fillRect(0, 0, cartoonCanvas.width, cartoonCanvas.height);
    
    // Set default drawing properties
    cartoonCtx.lineCap = 'round';
    cartoonCtx.lineJoin = 'round';
    
    console.log('[Cartoon] Canvas initialized successfully');
    return true;
};

/**
 * Draw a line on the canvas
 * @param {number} x1 - Start X coordinate
 * @param {number} y1 - Start Y coordinate
 * @param {number} x2 - End X coordinate
 * @param {number} y2 - End Y coordinate
 * @param {number} thickness - Line thickness
 * @param {string} color - Line color
 */
window.cartoonDrawLine = function (x1, y1, x2, y2, thickness, color) {
    if (!cartoonCtx) {
        console.error('[Cartoon] Context not initialized');
        return;
    }

    cartoonCtx.save();
    cartoonCtx.strokeStyle = color;
    cartoonCtx.lineWidth = thickness;
    
    cartoonCtx.beginPath();
    cartoonCtx.moveTo(x1, y1);
    cartoonCtx.lineTo(x2, y2);
    cartoonCtx.stroke();
    
    cartoonCtx.restore();
};

/**
 * Draw a preview line (for draw mode feedback)
 * @param {number} x1 - Start X coordinate
 * @param {number} y1 - Start Y coordinate
 * @param {number} x2 - End X coordinate
 * @param {number} y2 - End Y coordinate
 * @param {number} thickness - Line thickness
 * @param {string} color - Line color
 */
window.cartoonDrawPreviewLine = function (x1, y1, x2, y2, thickness, color) {
    if (!cartoonCtx || !cartoonCanvas) return;

    // Clear and redraw everything with preview
    const imageData = cartoonCtx.getImageData(0, 0, cartoonCanvas.width, cartoonCanvas.height);
    
    // We need to redraw from scratch, so let's just draw the preview on top
    // This is simplified - in production you'd want to cache the base image
    cartoonCtx.save();
    cartoonCtx.strokeStyle = color;
    cartoonCtx.lineWidth = thickness;
    cartoonCtx.globalAlpha = 0.5; // Semi-transparent preview
    
    cartoonCtx.beginPath();
    cartoonCtx.moveTo(x1, y1);
    cartoonCtx.lineTo(x2, y2);
    cartoonCtx.stroke();
    
    cartoonCtx.restore();
};

/**
 * Clear the canvas
 */
window.cartoonClearCanvas = function () {
    if (!cartoonCtx || !cartoonCanvas) {
        console.error('[Cartoon] Canvas not initialized');
        return;
    }

    // Clear with white background
    cartoonCtx.fillStyle = 'white';
    cartoonCtx.fillRect(0, 0, cartoonCanvas.width, cartoonCanvas.height);
};

/**
 * Update a thumbnail canvas for a frame
 * @param {number} frameIndex - Index of the frame
 * @param {Array} lines - Array of line objects
 */
window.cartoonUpdateThumbnail = function (frameIndex, lines) {
    const thumbnailCanvas = document.getElementById(`thumbnail_${frameIndex}`);
    if (!thumbnailCanvas) {
        console.log('[Cartoon] Thumbnail canvas not found for frame', frameIndex);
        return;
    }

    const ctx = thumbnailCanvas.getContext('2d');
    if (!ctx) return;

    // Clear thumbnail
    ctx.fillStyle = 'white';
    ctx.fillRect(0, 0, thumbnailCanvas.width, thumbnailCanvas.height);

    // Calculate scale factor
    const scaleX = thumbnailCanvas.width / 800;
    const scaleY = thumbnailCanvas.height / 600;

    // Draw all lines scaled down
    ctx.save();
    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';

    lines.forEach(line => {
        ctx.strokeStyle = line.color;
        ctx.lineWidth = line.thickness * Math.min(scaleX, scaleY);
        
        ctx.beginPath();
        ctx.moveTo(line.x1 * scaleX, line.y1 * scaleY);
        ctx.lineTo(line.x2 * scaleX, line.y2 * scaleY);
        ctx.stroke();
    });

    ctx.restore();
};

/**
 * Save the current canvas as an image
 * @param {string} filename - Filename for the download
 */
window.cartoonSaveImage = function (filename) {
    if (!cartoonCanvas) {
        console.error('[Cartoon] Canvas not initialized');
        return;
    }

    try {
        const dataUrl = cartoonCanvas.toDataURL('image/png');
        const link = document.createElement('a');
        link.download = filename || `cartoon-${Date.now()}.png`;
        link.href = dataUrl;
        link.click();
        console.log('[Cartoon] Image saved:', link.download);
    } catch (error) {
        console.error('[Cartoon] Error saving image:', error);
    }
};

/**
 * Export animation as a series of images or GIF
 * @param {Array} frames - Array of frame data
 * @param {string} format - 'zip' or 'gif'
 */
window.cartoonExportAnimation = function (frames, format) {
    console.log('[Cartoon] Exporting animation:', format, frames.length, 'frames');
    // This would require additional libraries for GIF creation
    // For now, we'll just export individual frames
    
    if (format === 'frames') {
        frames.forEach((frame, index) => {
            // Render each frame and download
            cartoonClearCanvas();
            frame.lines.forEach(line => {
                cartoonDrawLine(line.x1, line.y1, line.x2, line.y2, line.thickness, line.color);
            });
            cartoonSaveImage(`cartoon-frame-${index + 1}.png`);
        });
    }
};

// Log that the script has loaded
console.log('[Cartoon] cartoon-game.js loaded successfully');
