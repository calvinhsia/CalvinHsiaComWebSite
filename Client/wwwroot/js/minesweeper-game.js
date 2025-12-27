// Minesweeper Game - JavaScript support
// v4 - Fixed mobile scrolling for larger grids (passive touch listeners)

(function() {
    'use strict';

    let canvas = null;
    let ctx = null;
    let componentRef = null;
    let isRunning = false;
    let timerInterval = null;
    let gridInitialized = false;  // Track if grid has been initialized

    // Game configuration
    const DIFFICULTIES = {
        easy: { rows: 9, cols: 9, mines: 10 },
        medium: { rows: 16, cols: 16, mines: 40 },
        hard: { rows: 16, cols: 30, mines: 99 }
    };

    // Game state
    let grid = [];
    let rows = 9;
    let cols = 9;
    let mineCount = 10;
    let cellSize = 30;
    let flaggedCount = 0;
    let revealedCount = 0;
    let gameOver = false;
    let gameWon = false;
    let firstClick = true;
    let elapsedTime = 0;
    let currentDifficulty = 'easy';

    // Cell states
    const HIDDEN = 0;
    const REVEALED = 1;
    const FLAGGED = 2;

    // Colors
    const COLORS = {
        hidden: '#c0c0c0',
        hiddenBorder: '#808080',
        hiddenHighlight: '#ffffff',
        revealed: '#d0d0d0',
        revealedBorder: '#808080',
        mine: '#ff0000',
        flag: '#ff0000',
        numbers: ['', '#0000ff', '#008000', '#ff0000', '#000080', '#800000', '#008080', '#000000', '#808080']
    };

    // Track resize
    let firstResizeCallbackSent = false;
    let resizeRetryCount = 0;
    const MAX_RESIZE_RETRIES = 20;

    // Long press detection for mobile - enhanced for scroll support
    let longPressTimer = null;
    let longPressTriggered = false;
    let touchStartPos = null;  // Track touch start position
    let touchMoved = false;     // Track if finger moved (scrolling)
    const LONG_PRESS_DURATION = 500;
    const SCROLL_THRESHOLD = 10;  // Pixels of movement to consider it a scroll

    // State persistence key
    const MINESWEEPER_STATE_KEY = 'minesweeper_game_state';

    console.log('[Minesweeper v4] minesweeper-game.js loading...');

    function safeInvoke(methodName, ...args) {
        if (!componentRef) return Promise.resolve();
        
        return componentRef.invokeMethodAsync(methodName, ...args)
            .catch(err => {
                if (err.message && err.message.includes('disposed')) {
                    componentRef = null;
                    isRunning = false;
                    stopTimer();
                }
            });
    }

    // Save game state to localStorage
    function saveGameState() {
        if (!gridInitialized || !grid.length) return;
        
        try {
            const cells = [];
            for (let r = 0; r < rows; r++) {
                for (let c = 0; c < cols; c++) {
                    if (grid[r] && grid[r][c]) {
                        cells.push({
                            row: r,
                            col: c,
                            isMine: grid[r][c].mine,
                            state: grid[r][c].state,
                            adjacentMines: grid[r][c].adjacentMines
                        });
                    }
                }
            }

            let status = 'Ready';
            if (isRunning) status = 'Playing';
            if (gameWon) status = 'Won';
            if (gameOver && !gameWon) status = 'Lost';

            const state = {
                difficulty: currentDifficulty,
                rows: rows,
                cols: cols,
                mineCount: mineCount,
                flaggedCount: flaggedCount,
                revealedCount: revealedCount,
                elapsedTime: elapsedTime,
                gameStatus: status,
                gameOver: gameOver,
                gameWon: gameWon,
                firstClick: firstClick,
                cells: cells,
                lastSaved: new Date().toISOString()
            };

            localStorage.setItem(MINESWEEPER_STATE_KEY, JSON.stringify(state));
            console.log(`[Minesweeper v4] Game state saved - ${status}, Time: ${elapsedTime}s`);
        } catch (ex) {
            console.error('[Minesweeper v4] Error saving game state:', ex);
        }
    }

    // Load game state from localStorage
    function loadGameState() {
        try {
            const json = localStorage.getItem(MINESWEEPER_STATE_KEY);
            if (!json) return null;

            const state = JSON.parse(json);
            console.log(`[Minesweeper v4] Game state loaded - ${state.gameStatus}, Time: ${state.elapsedTime}s`);
            return state;
        } catch (ex) {
            console.error('[Minesweeper v4] Error loading game state:', ex);
            return null;
        }
    }

    // Restore game from saved state
    function restoreGameState(state) {
        if (!state || !state.cells || state.cells.length === 0) {
            console.log('[Minesweeper v4] No valid state to restore');
            return false;
        }

        try {
            stopTimer();
            
            currentDifficulty = state.difficulty || 'easy';
            rows = state.rows || 9;
            cols = state.cols || 9;
            mineCount = state.mineCount || 10;
            flaggedCount = state.flaggedCount || 0;
            revealedCount = state.revealedCount || 0;
            elapsedTime = state.elapsedTime || 0;
            gameOver = state.gameOver || false;
            gameWon = state.gameWon || false;
            firstClick = state.firstClick !== undefined ? state.firstClick : true;

            // Rebuild grid from saved cells
            grid = [];
            for (let r = 0; r < rows; r++) {
                grid[r] = [];
                for (let c = 0; c < cols; c++) {
                    grid[r][c] = {
                        mine: false,
                        state: HIDDEN,
                        adjacentMines: 0
                    };
                }
            }

            // Restore cell data
            for (const cell of state.cells) {
                if (cell.row >= 0 && cell.row < rows && cell.col >= 0 && cell.col < cols) {
                    grid[cell.row][cell.col] = {
                        mine: cell.isMine,
                        state: cell.state,
                        adjacentMines: cell.adjacentMines
                    };
                }
            }

            gridInitialized = true;
            
            // Resume timer if game was in progress
            if (state.gameStatus === 'Playing' && !gameOver) {
                isRunning = true;
                startTimerFromElapsed(elapsedTime);
            }

            console.log(`[Minesweeper v4] Game state restored - ${state.gameStatus}`);
            return true;
        } catch (ex) {
            console.error('[Minesweeper v4] Error restoring game state:', ex);
            return false;
        }
    }

    // Start timer from a specific elapsed time
    function startTimerFromElapsed(startTime) {
        if (timerInterval) return;
        
        elapsedTime = startTime;
        timerInterval = setInterval(() => {
            elapsedTime++;
            updateGameState();
            saveGameState(); // Save periodically
        }, 1000);
    }

    // Clear saved state
    window.clearMinesweeperState = function() {
        try {
            localStorage.removeItem(MINESWEEPER_STATE_KEY);
            console.log('[Minesweeper v4] Game state cleared');
        } catch (ex) {
            console.error('[Minesweeper v4] Error clearing game state:', ex);
        }
    };

    // Check if there's a saved state
    window.hasMinesweeperState = function() {
        try {
            const json = localStorage.getItem(MINESWEEPER_STATE_KEY);
            return !!json;
        } catch {
            return false;
        }
    };

    // Get saved difficulty
    window.getMinesweeperSavedDifficulty = function() {
        try {
            const state = loadGameState();
            return state?.difficulty || 'easy';
        } catch {
            return 'easy';
        }
    };

    window.initMinesweeperCanvas = function(canvasId) {
        canvas = document.getElementById(canvasId);
        if (!canvas) {
            console.error('[Minesweeper v4] Canvas not found:', canvasId);
            return;
        }

        firstResizeCallbackSent = false;
        resizeRetryCount = 0;
        isRunning = false;
        gameOver = false;
        gameWon = false;
        
        stopTimer();

        ctx = canvas.getContext('2d');

        // Setup event handlers
        canvas.addEventListener('click', handleClick);
        canvas.addEventListener('contextmenu', handleRightClick);
        
        // Mobile touch support - use passive: true for touchmove to allow scrolling
        canvas.addEventListener('touchstart', handleTouchStart, { passive: true });
        canvas.addEventListener('touchend', handleTouchEnd, { passive: false });  // Need non-passive for preventDefault on tap
        canvas.addEventListener('touchmove', handleTouchMove, { passive: true });  // CRITICAL: passive allows scrolling

        console.log('[Minesweeper v4] Canvas initialized with scroll-friendly touch handling');
    };

    function handleClick(e) {
        if (gameOver) return;
        
        const rect = canvas.getBoundingClientRect();
        const x = e.clientX - rect.left;
        const y = e.clientY - rect.top;
        
        const col = Math.floor(x / cellSize);
        const row = Math.floor(y / cellSize);
        
        if (row >= 0 && row < rows && col >= 0 && col < cols) {
            revealCell(row, col);
        }
    }

    function handleRightClick(e) {
        e.preventDefault();
        if (gameOver) return;
        
        const rect = canvas.getBoundingClientRect();
        const x = e.clientX - rect.left;
        const y = e.clientY - rect.top;
        
        const col = Math.floor(x / cellSize);
        const row = Math.floor(y / cellSize);
        
        if (row >= 0 && row < rows && col >= 0 && col < cols) {
            toggleFlag(row, col);
        }
    }

    function handleTouchStart(e) {
        // DON'T preventDefault here - allow scroll to start
        longPressTriggered = false;
        touchMoved = false;
        
        const touch = e.touches[0];
        touchStartPos = {
            x: touch.clientX,
            y: touch.clientY
        };
        
        const rect = canvas.getBoundingClientRect();
        const x = touch.clientX - rect.left;
        const y = touch.clientY - rect.top;
        
        const col = Math.floor(x / cellSize);
        const row = Math.floor(y / cellSize);
        
        // Start long press timer for flagging
        longPressTimer = setTimeout(() => {
            // Only trigger long press if finger hasn't moved
            if (!touchMoved) {
                longPressTriggered = true;
                if (row >= 0 && row < rows && col >= 0 && col < cols) {
                    toggleFlag(row, col);
                }
            }
        }, LONG_PRESS_DURATION);
    }

    function handleTouchEnd(e) {
        // Only preventDefault if we're actually interacting with the game (not scrolling)
        if (!touchMoved) {
            e.preventDefault();
        }
        
        if (longPressTimer) {
            clearTimeout(longPressTimer);
            longPressTimer = null;
        }
        
        // If not a long press and finger didn't move (not scrolling), treat as click
        if (!longPressTriggered && !touchMoved && !gameOver) {
            const touch = e.changedTouches[0];
            const rect = canvas.getBoundingClientRect();
            const x = touch.clientX - rect.left;
            const y = touch.clientY - rect.top;
            
            const col = Math.floor(x / cellSize);
            const row = Math.floor(y / cellSize);
            
            if (row >= 0 && row < rows && col >= 0 && col < cols) {
                revealCell(row, col);
            }
        }
        
        touchStartPos = null;
        touchMoved = false;
    }

    function handleTouchMove(e) {
        // Check if finger moved beyond threshold (user is scrolling)
        if (touchStartPos && e.touches.length > 0) {
            const touch = e.touches[0];
            const deltaX = Math.abs(touch.clientX - touchStartPos.x);
            const deltaY = Math.abs(touch.clientY - touchStartPos.y);
            
            if (deltaX > SCROLL_THRESHOLD || deltaY > SCROLL_THRESHOLD) {
                touchMoved = true;
                // Cancel long press if user is scrolling
                if (longPressTimer) {
                    clearTimeout(longPressTimer);
                    longPressTimer = null;
                }
            }
        }
        
        // DON'T preventDefault - let the scroll happen
    }

    window.setMinesweeperComponentRef = function(ref) {
        componentRef = ref;
        console.log('[Minesweeper v4] Component reference set');
        
        if (!firstResizeCallbackSent) {
            setTimeout(() => {
                if (window.resizeMinesweeperCanvas && !firstResizeCallbackSent) {
                    window.resizeMinesweeperCanvas();
                }
            }, 50);
        }
    };

    window.setupMinesweeperResize = function() {
        firstResizeCallbackSent = false;
        resizeRetryCount = 0;

        window.addEventListener('resize', debounce(() => {
            if (window.resizeMinesweeperCanvas) {
                window.resizeMinesweeperCanvas();
            }
        }, 250));

        // Save state on visibility change (when user switches tabs/apps)
        document.addEventListener('visibilitychange', () => {
            if (document.hidden && gridInitialized) {
                console.log('[Minesweeper v4] Page hidden - saving state');
                saveGameState();
            }
        });

        // Save state before page unload
        window.addEventListener('beforeunload', () => {
            if (gridInitialized) {
                console.log('[Minesweeper v4] Before unload - saving state');
                saveGameState();
            }
        });
    };

    window.resizeMinesweeperCanvas = function() {
        const section = document.querySelector('.minesweeper-canvas-section');
        if (!section || !canvas) return;

        const sectionWidth = section.clientWidth;
        const sectionHeight = section.clientHeight;

        if (sectionWidth < 100 || sectionHeight < 100) {
            resizeRetryCount++;
            if (resizeRetryCount <= MAX_RESIZE_RETRIES) {
                setTimeout(() => window.resizeMinesweeperCanvas(), Math.min(100 * resizeRetryCount, 500));
            }
            return;
        }

        resizeRetryCount = 0;
        
        // Use fixed cell size for consistent touch targets on mobile
        // Calculate based on viewport width to ensure cells are always tappable
        const isMobile = window.innerWidth <= 768;
        
        if (isMobile) {
            // On mobile: use fixed cell size (same as easy mode) for all difficulties
            // This ensures cells are always large enough to tap
            // Easy: 9 cols, so cell size = (width - 20) / 9
            const easyCellSize = Math.floor((sectionWidth - 20) / 9);
            cellSize = Math.min(easyCellSize, 40); // Max 40px
            cellSize = Math.max(cellSize, 30); // Min 30px on mobile for touch targets
        } else {
            // On desktop: fit cells to viewport, but with reasonable limits
            const maxCellWidth = Math.floor((sectionWidth - 20) / cols);
            const maxCellHeight = Math.floor((sectionHeight - 20) / rows);
            cellSize = Math.min(maxCellWidth, maxCellHeight, 40); // Max 40px cells
            cellSize = Math.max(cellSize, 20); // Min 20px cells
        }

        canvas.width = cols * cellSize;
        canvas.height = rows * cellSize;

        // Only render if grid is initialized
        if (gridInitialized) {
            renderGrid();
        }

        if (!firstResizeCallbackSent && componentRef) {
            firstResizeCallbackSent = true;
            safeInvoke('OnCanvasResized', canvas.width, canvas.height);
        }
    };

    window.initMinesweeperGame = function(difficulty, tryRestore = true) {
        // Try to restore saved state first
        if (tryRestore) {
            const savedState = loadGameState();
            if (savedState && savedState.difficulty === difficulty && !savedState.gameOver) {
                if (restoreGameState(savedState)) {
                    window.resizeMinesweeperCanvas();
                    updateGameState();
                    renderGrid();
                    console.log(`[Minesweeper v4] Game restored from saved state`);
                    return;
                }
            }
        }

        // Start fresh game
        const config = DIFFICULTIES[difficulty] || DIFFICULTIES.easy;
        rows = config.rows;
        cols = config.cols;
        mineCount = config.mines;
        currentDifficulty = difficulty;
        
        resetGame();
        window.resizeMinesweeperCanvas();
        
        console.log(`[Minesweeper v4] Game initialized: ${rows}x${cols}, ${mineCount} mines`);
    };
})();
