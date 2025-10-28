// Bounce Game JavaScript Functions

// Initialize bounce canvas
window.initBounceCanvas = function (canvasId) {
    const canvas = document.getElementById(canvasId);
    if (!canvas) {
        console.error('[Bounce] Canvas not found:', canvasId);
        return;
    }

    const ctx = canvas.getContext('2d');
    if (!ctx) {
        console.error('[Bounce] Could not get 2D context');
        return;
    }

    // Set canvas size
    canvas.width = 1200;
    canvas.height = 800;

    // Store context globally for rendering
    window.bounceContext = ctx;
    window.bounceCanvas = canvas;
};

// Render frame with bouncing balls
window.bounceRenderFrame = function (balls) {
    if (!window.bounceContext || !window.bounceCanvas) {
        console.error('[Bounce] Canvas not initialized');
        return;
    }

    const ctx = window.bounceContext;
    const canvas = window.bounceCanvas;

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
