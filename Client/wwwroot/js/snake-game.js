// Snake Game - JavaScript support
// v1 - Classic snake game

(function() {
    'use strict';

    let canvas = null;
    let ctx = null;
    let canvasWidth = 600;
    let canvasHeight = 600;
    let componentRef = null;
    let animationId = null;
    let isRunning = false;
    let lastFrameTime = 0;

    // Game state
    let snake = [];
    let food = { x: 0, y: 0 };
    let direction = { x: 1, y: 0 };
    let nextDirection = { x: 1, y: 0 };
    let gridSize = 20;
    let cols = 30;
    let rows = 30;
    let score = 0;
    let highScore = 0;
    let gameOver = false;
    let speed = 100; // ms per move

    // Colors
    const SNAKE_HEAD_COLOR = '#00ff88';
    const SNAKE_BODY_COLOR = '#00cc66';
    const FOOD_COLOR = '#ff6b6b';
    const BACKGROUND_COLOR = '#1a1a2e';
    const GRID_COLOR = '#2a2a4e';

    // Track resize
    let firstResizeCallbackSent = false;
    let resizeRetryCount = 0;
    const MAX_RESIZE_RETRIES = 20;

    console.log('[Snake v1] snake-game.js loading...');

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

    window.initSnakeCanvas = function(canvasId, width, height) {
        canvas = document.getElementById(canvasId);
        if (!canvas) {
            console.error('[Snake v1] Canvas not found:', canvasId);
            return;
        }

        firstResizeCallbackSent = false;
        resizeRetryCount = 0;
        isRunning = false;
        gameOver = false;
        
        if (animationId) {
            cancelAnimationFrame(animationId);
            animationId = null;
        }

        ctx = canvas.getContext('2d');
        canvasWidth = width;
        canvasHeight = height;
        canvas.width = width;
        canvas.height = height;

        // Setup keyboard controls
        document.addEventListener('keydown', handleKeyDown);

        console.log(`[Snake v1] Canvas initialized: ${width}x${height}`);
    };

    function handleKeyDown(e) {
        if (!isRunning || gameOver) return;

        switch(e.key) {
            case 'ArrowUp':
            case 'w':
            case 'W':
                if (direction.y !== 1) nextDirection = { x: 0, y: -1 };
                e.preventDefault();
                break;
            case 'ArrowDown':
            case 's':
            case 'S':
                if (direction.y !== -1) nextDirection = { x: 0, y: 1 };
                e.preventDefault();
                break;
            case 'ArrowLeft':
            case 'a':
            case 'A':
                if (direction.x !== 1) nextDirection = { x: -1, y: 0 };
                e.preventDefault();
                break;
            case 'ArrowRight':
            case 'd':
            case 'D':
                if (direction.x !== -1) nextDirection = { x: 1, y: 0 };
                e.preventDefault();
                break;
        }
    }

    window.setSnakeComponentRef = function(ref) {
        componentRef = ref;
        console.log('[Snake v1] Component reference set');
        
        if (!firstResizeCallbackSent) {
            setTimeout(() => {
                if (window.resizeSnakeCanvas && !firstResizeCallbackSent) {
                    window.resizeSnakeCanvas();
                }
            }, 50);
        }
    };

    window.setupSnakeResize = function() {
        firstResizeCallbackSent = false;
        resizeRetryCount = 0;

        window.addEventListener('resize', debounce(() => {
            if (window.resizeSnakeCanvas) {
                window.resizeSnakeCanvas();
            }
        }, 250));
    };

    window.resizeSnakeCanvas = function() {
        const section = document.querySelector('.snake-canvas-section');
        if (!section || !canvas) return;

        const sectionWidth = section.clientWidth;
        const sectionHeight = section.clientHeight;

        if (sectionWidth < 100 || sectionHeight < 100) {
            resizeRetryCount++;
            if (resizeRetryCount <= MAX_RESIZE_RETRIES) {
                setTimeout(() => window.resizeSnakeCanvas(), Math.min(100 * resizeRetryCount, 500));
            }
            return;
        }

        resizeRetryCount = 0;
        
        // Make it square
        const size = Math.min(sectionWidth, sectionHeight) - 8;
        const newSize = Math.floor(size);

        const sizeChanged = Math.abs(canvas.width - newSize) > 2;
        const needsCallback = !firstResizeCallbackSent || sizeChanged;

        if (needsCallback) {
            canvasWidth = newSize;
            canvasHeight = newSize;
            canvas.width = newSize;
            canvas.height = newSize;

            // Recalculate grid
            gridSize = Math.floor(newSize / cols);
            
            if (componentRef) {
                firstResizeCallbackSent = true;
                safeInvoke('OnCanvasResized', newSize, newSize);
            } else {
                setTimeout(() => window.resizeSnakeCanvas(), 50);
            }
        }
    };

    window.initSnakeWorld = function(options) {
        cols = options.cols || 30;
        rows = options.rows || 30;
        speed = options.speed || 100;
        
        gridSize = Math.floor(Math.min(canvasWidth, canvasHeight) / cols);
        
        resetGame();
        renderFrame();
        
        console.log(`[Snake v1] World initialized: ${cols}x${rows}, gridSize=${gridSize}`);
        return { cols, rows };
    };

    function resetGame() {
        // Start snake in center
        const startX = Math.floor(cols / 2);
        const startY = Math.floor(rows / 2);
        
        snake = [
            { x: startX, y: startY },
            { x: startX - 1, y: startY },
            { x: startX - 2, y: startY }
        ];
        
        direction = { x: 1, y: 0 };
        nextDirection = { x: 1, y: 0 };
        score = 0;
        gameOver = false;
        
        spawnFood();
    }

    function spawnFood() {
        let validPosition = false;
        while (!validPosition) {
            food.x = Math.floor(Math.random() * cols);
            food.y = Math.floor(Math.random() * rows);
            
            // Make sure food doesn't spawn on snake
            validPosition = !snake.some(s => s.x === food.x && s.y === food.y);
        }
    }

    window.startSnakeGame = function(speedMs) {
        if (isRunning) return;
        
        if (gameOver) {
            resetGame();
        }
        
        speed = speedMs || 100;
        isRunning = true;
        lastFrameTime = performance.now();
        
        console.log(`[Snake v1] Game started, speed: ${speed}ms`);

        function gameLoop(timestamp) {
            if (!isRunning) return;

            const elapsed = timestamp - lastFrameTime;
            
            if (elapsed >= speed) {
                lastFrameTime = timestamp;
                
                if (!gameOver) {
                    updateGame();
                }
                renderFrame();
            }

            animationId = requestAnimationFrame(gameLoop);
        }

        animationId = requestAnimationFrame(gameLoop);
    };

    window.stopSnakeGame = function() {
        isRunning = false;
        if (animationId) {
            cancelAnimationFrame(animationId);
            animationId = null;
        }
        console.log('[Snake v1] Game stopped');
    };

    window.resetSnakeGame = function() {
        isRunning = false;
        if (animationId) {
            cancelAnimationFrame(animationId);
            animationId = null;
        }
        resetGame();
        renderFrame();
        console.log('[Snake v1] Game reset');
    };

    window.setSnakeDirection = function(dx, dy) {
        if ((dx !== 0 && direction.x === 0) || (dy !== 0 && direction.y === 0)) {
            nextDirection = { x: dx, y: dy };
        }
    };

    function updateGame() {
        // Apply next direction
        direction = nextDirection;
        
        // Calculate new head position
        const head = snake[0];
        const newHead = {
            x: head.x + direction.x,
            y: head.y + direction.y
        };

        // Check wall collision
        if (newHead.x < 0 || newHead.x >= cols || newHead.y < 0 || newHead.y >= rows) {
            endGame();
            return;
        }

        // Check self collision
        if (snake.some(s => s.x === newHead.x && s.y === newHead.y)) {
            endGame();
            return;
        }

        // Move snake
        snake.unshift(newHead);

        // Check food collision
        if (newHead.x === food.x && newHead.y === food.y) {
            score += 10;
            if (score > highScore) highScore = score;
            spawnFood();
            
            if (componentRef) {
                safeInvoke('OnScoreChanged', score, highScore);
            }
        } else {
            snake.pop();
        }
    }

    function endGame() {
        gameOver = true;
        isRunning = false;
        
        if (animationId) {
            cancelAnimationFrame(animationId);
            animationId = null;
        }
        
        if (componentRef) {
            safeInvoke('OnGameOver', score, highScore);
        }
        
        console.log(`[Snake v1] Game over! Score: ${score}`);
    }

    function renderFrame() {
        if (!ctx) return;

        // Clear
        ctx.fillStyle = BACKGROUND_COLOR;
        ctx.fillRect(0, 0, canvasWidth, canvasHeight);

        // Draw grid
        ctx.strokeStyle = GRID_COLOR;
        ctx.lineWidth = 0.5;
        for (let x = 0; x <= cols; x++) {
            ctx.beginPath();
            ctx.moveTo(x * gridSize, 0);
            ctx.lineTo(x * gridSize, rows * gridSize);
            ctx.stroke();
        }
        for (let y = 0; y <= rows; y++) {
            ctx.beginPath();
            ctx.moveTo(0, y * gridSize);
            ctx.lineTo(cols * gridSize, y * gridSize);
            ctx.stroke();
        }

        // Draw food
        ctx.fillStyle = FOOD_COLOR;
        ctx.beginPath();
        ctx.arc(
            food.x * gridSize + gridSize / 2,
            food.y * gridSize + gridSize / 2,
            gridSize / 2 - 2,
            0, Math.PI * 2
        );
        ctx.fill();

        // Draw snake
        snake.forEach((segment, index) => {
            ctx.fillStyle = index === 0 ? SNAKE_HEAD_COLOR : SNAKE_BODY_COLOR;
            ctx.fillRect(
                segment.x * gridSize + 1,
                segment.y * gridSize + 1,
                gridSize - 2,
                gridSize - 2
            );
        });

        // Draw game over overlay
        if (gameOver) {
            ctx.fillStyle = 'rgba(0, 0, 0, 0.7)';
            ctx.fillRect(0, 0, canvasWidth, canvasHeight);
            
            ctx.fillStyle = '#fff';
            ctx.font = 'bold 36px Arial';
            ctx.textAlign = 'center';
            ctx.fillText('GAME OVER', canvasWidth / 2, canvasHeight / 2 - 20);
            
            ctx.font = '24px Arial';
            ctx.fillText(`Score: ${score}`, canvasWidth / 2, canvasHeight / 2 + 20);
            
            ctx.font = '16px Arial';
            ctx.fillText('Press Start to play again', canvasWidth / 2, canvasHeight / 2 + 60);
        }
    }

    window.getSnakeCanvasRect = function() {
        if (!canvas) return null;
        const rect = canvas.getBoundingClientRect();
        return { left: rect.left, top: rect.top, width: rect.width, height: rect.height };
    };

    function debounce(func, wait) {
        let timeout;
        return function(...args) {
            clearTimeout(timeout);
            timeout = setTimeout(() => func(...args), wait);
        };
    }

    // Cleanup on page unload
    window.cleanupSnake = function() {
        document.removeEventListener('keydown', handleKeyDown);
        isRunning = false;
        if (animationId) {
            cancelAnimationFrame(animationId);
            animationId = null;
        }
    };

    console.log('[Snake v1] snake-game.js loaded');
})();
