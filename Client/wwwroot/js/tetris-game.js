// Tetris Game - JavaScript support
// v1 - Classic Tetris implementation

(function() {
    'use strict';

    let canvas = null;
    let ctx = null;
    let nextCanvas = null;
    let nextCtx = null;
    let canvasWidth = 300;
    let canvasHeight = 600;
    let componentRef = null;
    let animationId = null;
    let isRunning = false;
    let lastFrameTime = 0;
    let lastDropTime = 0;

    // Game constants
    const COLS = 10;
    const ROWS = 20;
    const BLOCK_SIZE = 30;

    // Game state
    let board = [];
    let currentPiece = null;
    let nextPiece = null;
    let score = 0;
    let level = 1;
    let lines = 0;
    let gameOver = false;
    let dropInterval = 1000; // ms between drops

    // Tetromino shapes
    const PIECES = {
        I: { shape: [[1,1,1,1]], color: '#00f0f0' },
        O: { shape: [[1,1],[1,1]], color: '#f0f000' },
        T: { shape: [[0,1,0],[1,1,1]], color: '#a000f0' },
        S: { shape: [[0,1,1],[1,1,0]], color: '#00f000' },
        Z: { shape: [[1,1,0],[0,1,1]], color: '#f00000' },
        J: { shape: [[1,0,0],[1,1,1]], color: '#0000f0' },
        L: { shape: [[0,0,1],[1,1,1]], color: '#f0a000' }
    };
    const PIECE_NAMES = Object.keys(PIECES);

    // Colors
    const BACKGROUND_COLOR = '#1a1a2e';
    const GRID_COLOR = '#2a2a4e';

    // Track resize
    let firstResizeCallbackSent = false;

    console.log('[Tetris v1] tetris-game.js loading...');

    function safeInvoke(methodName, ...args) {
        if (!componentRef) return Promise.resolve();
        
        return componentRef.invokeMethodAsync(methodName, ...args)
            .catch(err => {
                if (err.message && err.message.includes('disposed')) {
                    componentRef = null;
                    isRunning = false;
                    if (animationId !== null) {
                        cancelAnimationFrame(animationId);
                        animationId = null;
                    }
                }
            });
    }

    window.initTetrisCanvas = function(canvasId, nextCanvasId) {
        canvas = document.getElementById(canvasId);
        nextCanvas = document.getElementById(nextCanvasId);
        
        if (!canvas) {
            console.error('[Tetris v1] Canvas not found:', canvasId);
            return;
        }

        firstResizeCallbackSent = false;
        isRunning = false;
        gameOver = false;
        
        if (animationId) {
            cancelAnimationFrame(animationId);
            animationId = null;
        }

        canvasWidth = COLS * BLOCK_SIZE;
        canvasHeight = ROWS * BLOCK_SIZE;
        canvas.width = canvasWidth;
        canvas.height = canvasHeight;
        ctx = canvas.getContext('2d');

        if (nextCanvas) {
            nextCanvas.width = 4 * BLOCK_SIZE;
            nextCanvas.height = 4 * BLOCK_SIZE;
            nextCtx = nextCanvas.getContext('2d');
        }

        // Setup keyboard controls
        document.addEventListener('keydown', handleKeyDown);

        console.log(`[Tetris v1] Canvas initialized: ${canvasWidth}x${canvasHeight}`);
    };

    function handleKeyDown(e) {
        if (!isRunning || gameOver) return;

        switch(e.key) {
            case 'ArrowLeft':
            case 'a':
            case 'A':
                movePiece(-1, 0);
                e.preventDefault();
                break;
            case 'ArrowRight':
            case 'd':
            case 'D':
                movePiece(1, 0);
                e.preventDefault();
                break;
            case 'ArrowDown':
            case 's':
            case 'S':
                movePiece(0, 1);
                e.preventDefault();
                break;
            case 'ArrowUp':
            case 'w':
            case 'W':
                rotatePiece();
                e.preventDefault();
                break;
            case ' ':
                hardDrop();
                e.preventDefault();
                break;
        }
    }

    window.setTetrisComponentRef = function(ref) {
        componentRef = ref;
        console.log('[Tetris v1] Component reference set');
        
        if (!firstResizeCallbackSent) {
            firstResizeCallbackSent = true;
            initGame();
        }
    };

    function initGame() {
        // Create empty board
        board = [];
        for (let r = 0; r < ROWS; r++) {
            board.push(new Array(COLS).fill(null));
        }
        
        score = 0;
        level = 1;
        lines = 0;
        gameOver = false;
        dropInterval = 1000;
        
        nextPiece = createPiece();
        spawnPiece();
        renderFrame();
        
        if (componentRef) {
            safeInvoke('OnStatsChanged', score, level, lines);
        }
        
        console.log('[Tetris v1] Game initialized');
    }

    function createPiece() {
        const name = PIECE_NAMES[Math.floor(Math.random() * PIECE_NAMES.length)];
        const piece = PIECES[name];
        return {
            shape: piece.shape.map(row => [...row]),
            color: piece.color,
            x: Math.floor(COLS / 2) - Math.ceil(piece.shape[0].length / 2),
            y: 0
        };
    }

    function spawnPiece() {
        currentPiece = nextPiece;
        currentPiece.x = Math.floor(COLS / 2) - Math.ceil(currentPiece.shape[0].length / 2);
        currentPiece.y = 0;
        nextPiece = createPiece();
        
        // Check if game over
        if (!isValidPosition(currentPiece, 0, 0)) {
            endGame();
        }
    }

    function isValidPosition(piece, offsetX, offsetY) {
        for (let r = 0; r < piece.shape.length; r++) {
            for (let c = 0; c < piece.shape[r].length; c++) {
                if (piece.shape[r][c]) {
                    const newX = piece.x + c + offsetX;
                    const newY = piece.y + r + offsetY;
                    
                    if (newX < 0 || newX >= COLS || newY >= ROWS) {
                        return false;
                    }
                    if (newY >= 0 && board[newY][newX]) {
                        return false;
                    }
                }
            }
        }
        return true;
    }

    function movePiece(dx, dy) {
        if (isValidPosition(currentPiece, dx, dy)) {
            currentPiece.x += dx;
            currentPiece.y += dy;
            renderFrame();
            return true;
        }
        return false;
    }

    function rotatePiece() {
        const rotated = currentPiece.shape[0].map((_, i) =>
            currentPiece.shape.map(row => row[i]).reverse()
        );
        
        const original = currentPiece.shape;
        currentPiece.shape = rotated;
        
        // Wall kick - try offsets if rotation fails
        const kicks = [0, -1, 1, -2, 2];
        for (const kick of kicks) {
            if (isValidPosition(currentPiece, kick, 0)) {
                currentPiece.x += kick;
                renderFrame();
                return;
            }
        }
        
        // Revert if no valid position
        currentPiece.shape = original;
    }

    function hardDrop() {
        while (movePiece(0, 1)) {
            score += 2;
        }
        lockPiece();
    }

    function lockPiece() {
        // Add piece to board
        for (let r = 0; r < currentPiece.shape.length; r++) {
            for (let c = 0; c < currentPiece.shape[r].length; c++) {
                if (currentPiece.shape[r][c]) {
                    const boardY = currentPiece.y + r;
                    const boardX = currentPiece.x + c;
                    if (boardY >= 0) {
                        board[boardY][boardX] = currentPiece.color;
                    }
                }
            }
        }
        
        // Check for completed lines
        let linesCleared = 0;
        for (let r = ROWS - 1; r >= 0; r--) {
            if (board[r].every(cell => cell !== null)) {
                board.splice(r, 1);
                board.unshift(new Array(COLS).fill(null));
                linesCleared++;
                r++; // Check same row again
            }
        }
        
        // Update score
        if (linesCleared > 0) {
            const lineScores = [0, 100, 300, 500, 800];
            score += lineScores[linesCleared] * level;
            lines += linesCleared;
            
            // Level up every 10 lines
            level = Math.floor(lines / 10) + 1;
            dropInterval = Math.max(100, 1000 - (level - 1) * 100);
            
            if (componentRef) {
                safeInvoke('OnStatsChanged', score, level, lines);
            }
        }
        
        spawnPiece();
    }

    function endGame() {
        gameOver = true;
        isRunning = false;
        
        if (animationId) {
            cancelAnimationFrame(animationId);
            animationId = null;
        }
        
        if (componentRef) {
            safeInvoke('OnGameOver', score, level, lines);
        }
        
        console.log(`[Tetris v1] Game over! Score: ${score}, Level: ${level}, Lines: ${lines}`);
    }

    window.startTetrisGame = function() {
        if (isRunning) return;
        
        if (gameOver) {
            initGame();
        }
        
        isRunning = true;
        lastFrameTime = performance.now();
        lastDropTime = lastFrameTime;
        
        console.log('[Tetris v1] Game started');

        function gameLoop(timestamp) {
            if (!isRunning) return;

            // Auto-drop
            if (timestamp - lastDropTime >= dropInterval) {
                if (!movePiece(0, 1)) {
                    lockPiece();
                }
                lastDropTime = timestamp;
            }

            renderFrame();
            animationId = requestAnimationFrame(gameLoop);
        }

        animationId = requestAnimationFrame(gameLoop);
    };

    window.stopTetrisGame = function() {
        isRunning = false;
        if (animationId) {
            cancelAnimationFrame(animationId);
            animationId = null;
        }
        console.log('[Tetris v1] Game paused');
    };

    window.resetTetrisGame = function() {
        isRunning = false;
        if (animationId) {
            cancelAnimationFrame(animationId);
            animationId = null;
        }
        initGame();
        console.log('[Tetris v1] Game reset');
    };

    window.tetrisMoveLeft = function() { movePiece(-1, 0); };
    window.tetrisMoveRight = function() { movePiece(1, 0); };
    window.tetrisMoveDown = function() { movePiece(0, 1); };
    window.tetrisRotate = function() { rotatePiece(); };
    window.tetrisHardDrop = function() { hardDrop(); };

    function renderFrame() {
        if (!ctx) return;

        // Clear
        ctx.fillStyle = BACKGROUND_COLOR;
        ctx.fillRect(0, 0, canvasWidth, canvasHeight);

        // Draw grid
        ctx.strokeStyle = GRID_COLOR;
        ctx.lineWidth = 0.5;
        for (let x = 0; x <= COLS; x++) {
            ctx.beginPath();
            ctx.moveTo(x * BLOCK_SIZE, 0);
            ctx.lineTo(x * BLOCK_SIZE, ROWS * BLOCK_SIZE);
            ctx.stroke();
        }
        for (let y = 0; y <= ROWS; y++) {
            ctx.beginPath();
            ctx.moveTo(0, y * BLOCK_SIZE);
            ctx.lineTo(COLS * BLOCK_SIZE, y * BLOCK_SIZE);
            ctx.stroke();
        }

        // Draw board
        for (let r = 0; r < ROWS; r++) {
            for (let c = 0; c < COLS; c++) {
                if (board[r][c]) {
                    drawBlock(ctx, c, r, board[r][c]);
                }
            }
        }

        // Draw current piece
        if (currentPiece && !gameOver) {
            // Draw ghost piece
            let ghostY = currentPiece.y;
            while (isValidPosition(currentPiece, 0, ghostY - currentPiece.y + 1)) {
                ghostY++;
            }
            
            ctx.globalAlpha = 0.3;
            for (let r = 0; r < currentPiece.shape.length; r++) {
                for (let c = 0; c < currentPiece.shape[r].length; c++) {
                    if (currentPiece.shape[r][c]) {
                        drawBlock(ctx, currentPiece.x + c, ghostY + r, currentPiece.color);
                    }
                }
            }
            ctx.globalAlpha = 1;

            // Draw actual piece
            for (let r = 0; r < currentPiece.shape.length; r++) {
                for (let c = 0; c < currentPiece.shape[r].length; c++) {
                    if (currentPiece.shape[r][c]) {
                        drawBlock(ctx, currentPiece.x + c, currentPiece.y + r, currentPiece.color);
                    }
                }
            }
        }

        // Draw next piece preview
        if (nextCtx && nextPiece) {
            nextCtx.fillStyle = BACKGROUND_COLOR;
            nextCtx.fillRect(0, 0, nextCanvas.width, nextCanvas.height);
            
            const offsetX = (4 - nextPiece.shape[0].length) / 2;
            const offsetY = (4 - nextPiece.shape.length) / 2;
            
            for (let r = 0; r < nextPiece.shape.length; r++) {
                for (let c = 0; c < nextPiece.shape[r].length; c++) {
                    if (nextPiece.shape[r][c]) {
                        drawBlock(nextCtx, offsetX + c, offsetY + r, nextPiece.color);
                    }
                }
            }
        }

        // Draw game over overlay
        if (gameOver) {
            ctx.fillStyle = 'rgba(0, 0, 0, 0.7)';
            ctx.fillRect(0, 0, canvasWidth, canvasHeight);
            
            ctx.fillStyle = '#fff';
            ctx.font = 'bold 24px Arial';
            ctx.textAlign = 'center';
            ctx.fillText('GAME OVER', canvasWidth / 2, canvasHeight / 2 - 20);
            
            ctx.font = '16px Arial';
            ctx.fillText(`Score: ${score}`, canvasWidth / 2, canvasHeight / 2 + 10);
            ctx.fillText(`Lines: ${lines}`, canvasWidth / 2, canvasHeight / 2 + 35);
        }
    }

    function drawBlock(context, x, y, color) {
        const padding = 1;
        context.fillStyle = color;
        context.fillRect(
            x * BLOCK_SIZE + padding,
            y * BLOCK_SIZE + padding,
            BLOCK_SIZE - padding * 2,
            BLOCK_SIZE - padding * 2
        );
        
        // Highlight
        context.fillStyle = 'rgba(255, 255, 255, 0.3)';
        context.fillRect(
            x * BLOCK_SIZE + padding,
            y * BLOCK_SIZE + padding,
            BLOCK_SIZE - padding * 2,
            3
        );
    }

    window.cleanupTetris = function() {
        document.removeEventListener('keydown', handleKeyDown);
        isRunning = false;
        if (animationId) {
            cancelAnimationFrame(animationId);
            animationId = null;
        }
    };

    console.log('[Tetris v1] tetris-game.js loaded');
})();
