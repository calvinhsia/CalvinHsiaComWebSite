// Minesweeper Game - JavaScript support
// v3 - Added state persistence

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

    // Long press detection for mobile
    let longPressTimer = null;
    let longPressTriggered = false;
    const LONG_PRESS_DURATION = 500;

    // State persistence key
    const MINESWEEPER_STATE_KEY = 'minesweeper_game_state';

    console.log('[Minesweeper v3] minesweeper-game.js loading...');

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
            console.log(`[Minesweeper v3] Game state saved - ${status}, Time: ${elapsedTime}s`);
        } catch (ex) {
            console.error('[Minesweeper v3] Error saving game state:', ex);
        }
    }

    // Load game state from localStorage
    function loadGameState() {
        try {
            const json = localStorage.getItem(MINESWEEPER_STATE_KEY);
            if (!json) return null;

            const state = JSON.parse(json);
            console.log(`[Minesweeper v3] Game state loaded - ${state.gameStatus}, Time: ${state.elapsedTime}s`);
            return state;
        } catch (ex) {
            console.error('[Minesweeper v3] Error loading game state:', ex);
            return null;
        }
    }

    // Restore game from saved state
    function restoreGameState(state) {
        if (!state || !state.cells || state.cells.length === 0) {
            console.log('[Minesweeper v3] No valid state to restore');
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

            console.log(`[Minesweeper v3] Game state restored - ${state.gameStatus}`);
            return true;
        } catch (ex) {
            console.error('[Minesweeper v3] Error restoring game state:', ex);
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
            console.log('[Minesweeper v3] Game state cleared');
        } catch (ex) {
            console.error('[Minesweeper v3] Error clearing game state:', ex);
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
            console.error('[Minesweeper v3] Canvas not found:', canvasId);
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
        
        // Mobile touch support
        canvas.addEventListener('touchstart', handleTouchStart, { passive: false });
        canvas.addEventListener('touchend', handleTouchEnd, { passive: false });
        canvas.addEventListener('touchmove', handleTouchMove, { passive: false });

        console.log('[Minesweeper v3] Canvas initialized');
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
        e.preventDefault();
        longPressTriggered = false;
        
        const touch = e.touches[0];
        const rect = canvas.getBoundingClientRect();
        const x = touch.clientX - rect.left;
        const y = touch.clientY - rect.top;
        
        const col = Math.floor(x / cellSize);
        const row = Math.floor(y / cellSize);
        
        // Start long press timer for flagging
        longPressTimer = setTimeout(() => {
            longPressTriggered = true;
            if (row >= 0 && row < rows && col >= 0 && col < cols) {
                toggleFlag(row, col);
            }
        }, LONG_PRESS_DURATION);
    }

    function handleTouchEnd(e) {
        e.preventDefault();
        
        if (longPressTimer) {
            clearTimeout(longPressTimer);
            longPressTimer = null;
        }
        
        // If not a long press, treat as click
        if (!longPressTriggered && !gameOver) {
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
    }

    function handleTouchMove(e) {
        // Cancel long press if user moves finger
        if (longPressTimer) {
            clearTimeout(longPressTimer);
            longPressTimer = null;
        }
    }

    window.setMinesweeperComponentRef = function(ref) {
        componentRef = ref;
        console.log('[Minesweeper v3] Component reference set');
        
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
                console.log('[Minesweeper v3] Page hidden - saving state');
                saveGameState();
            }
        });

        // Save state before page unload
        window.addEventListener('beforeunload', () => {
            if (gridInitialized) {
                console.log('[Minesweeper v3] Before unload - saving state');
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
        
        // Calculate cell size to fit in viewport
        const maxCellWidth = Math.floor((sectionWidth - 20) / cols);
        const maxCellHeight = Math.floor((sectionHeight - 20) / rows);
        cellSize = Math.min(maxCellWidth, maxCellHeight, 40); // Max 40px cells
        cellSize = Math.max(cellSize, 20); // Min 20px cells

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
                    console.log(`[Minesweeper v3] Game restored from saved state`);
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
        
        console.log(`[Minesweeper v3] Game initialized: ${rows}x${cols}, ${mineCount} mines`);
    };

    window.newMinesweeperGame = function(difficulty) {
        // Clear any saved state when starting a new game
        window.clearMinesweeperState();
        
        const config = DIFFICULTIES[difficulty] || DIFFICULTIES.easy;
        rows = config.rows;
        cols = config.cols;
        mineCount = config.mines;
        currentDifficulty = difficulty;
        
        resetGame();
        window.resizeMinesweeperCanvas();
        
        updateGameState();
        
        console.log(`[Minesweeper v3] New game: ${rows}x${cols}, ${mineCount} mines`);
    };

    function resetGame() {
        stopTimer();
        
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
        
        gridInitialized = true;  // Mark grid as initialized
        flaggedCount = 0;
        revealedCount = 0;
        gameOver = false;
        gameWon = false;
        firstClick = true;
        elapsedTime = 0;
        isRunning = false;
        
        renderGrid();
    }

    function placeMines(excludeRow, excludeCol) {
        // Place mines randomly, excluding the first clicked cell and its neighbors
        let minesPlaced = 0;
        
        while (minesPlaced < mineCount) {
            const r = Math.floor(Math.random() * rows);
            const c = Math.floor(Math.random() * cols);
            
            // Don't place mine on first click or adjacent cells
            if (Math.abs(r - excludeRow) <= 1 && Math.abs(c - excludeCol) <= 1) {
                continue;
            }
            
            if (!grid[r][c].mine) {
                grid[r][c].mine = true;
                minesPlaced++;
            }
        }
        
        // Calculate adjacent mine counts
        for (let r = 0; r < rows; r++) {
            for (let c = 0; c < cols; c++) {
                if (!grid[r][c].mine) {
                    grid[r][c].adjacentMines = countAdjacentMines(r, c);
                }
            }
        }
    }

    function countAdjacentMines(row, col) {
        let count = 0;
        for (let dr = -1; dr <= 1; dr++) {
            for (let dc = -1; dc <= 1; dc++) {
                if (dr === 0 && dc === 0) continue;
                const nr = row + dr;
                const nc = col + dc;
                if (nr >= 0 && nr < rows && nc >= 0 && nc < cols && grid[nr][nc].mine) {
                    count++;
                }
            }
        }
        return count;
    }

    function revealCell(row, col) {
        const cell = grid[row][col];
        
        if (cell.state !== HIDDEN) return;
        
        // First click - place mines
        if (firstClick) {
            firstClick = false;
            placeMines(row, col);
            startTimer();
            isRunning = true;
        }
        
        cell.state = REVEALED;
        revealedCount++;
        
        // Hit a mine
        if (cell.mine) {
            gameOver = true;
            isRunning = false;
            stopTimer();
            revealAllMines();
            renderGrid();
            saveGameState();
            
            if (componentRef) {
                safeInvoke('OnGameOver', false, elapsedTime);
            }
            console.log('[Minesweeper v3] Game over - hit mine!');
            return;
        }
        
        // Auto-reveal adjacent cells if no adjacent mines
        if (cell.adjacentMines === 0) {
            for (let dr = -1; dr <= 1; dr++) {
                for (let dc = -1; dc <= 1; dc++) {
                    if (dr === 0 && dc === 0) continue;
                    const nr = row + dr;
                    const nc = col + dc;
                    if (nr >= 0 && nr < rows && nc >= 0 && nc < cols) {
                        revealCell(nr, nc);
                    }
                }
            }
        }
        
        // Check for win
        checkWin();
        
        renderGrid();
        updateGameState();
        saveGameState();
    }

    function toggleFlag(row, col) {
        const cell = grid[row][col];
        
        if (cell.state === REVEALED) return;
        
        if (cell.state === HIDDEN) {
            cell.state = FLAGGED;
            flaggedCount++;
        } else {
            cell.state = HIDDEN;
            flaggedCount--;
        }
        
        renderGrid();
        updateGameState();
        saveGameState();
    }

    function revealAllMines() {
        for (let r = 0; r < rows; r++) {
            for (let c = 0; c < cols; c++) {
                if (grid[r][c].mine) {
                    grid[r][c].state = REVEALED;
                }
            }
        }
    }

    function checkWin() {
        // Win if all non-mine cells are revealed
        const totalCells = rows * cols;
        const nonMineCells = totalCells - mineCount;
        
        if (revealedCount === nonMineCells) {
            gameOver = true;
            gameWon = true;
            isRunning = false;
            stopTimer();
            
            // Flag all remaining mines
            for (let r = 0; r < rows; r++) {
                for (let c = 0; c < cols; c++) {
                    if (grid[r][c].mine && grid[r][c].state !== FLAGGED) {
                        grid[r][c].state = FLAGGED;
                        flaggedCount++;
                    }
                }
            }
            
            saveGameState();
            
            if (componentRef) {
                safeInvoke('OnGameOver', true, elapsedTime);
            }
            console.log(`[Minesweeper v3] You won in ${elapsedTime} seconds!`);
        }
    }

    function startTimer() {
        if (timerInterval) return;
        
        elapsedTime = 0;
        timerInterval = setInterval(() => {
            elapsedTime++;
            updateGameState();
            
            // Save state every 10 seconds during gameplay
            if (elapsedTime % 10 === 0) {
                saveGameState();
            }
        }, 1000);
    }

    function stopTimer() {
        if (timerInterval) {
            clearInterval(timerInterval);
            timerInterval = null;
        }
    }

    function updateGameState() {
        if (componentRef) {
            let status = 'Ready';
            if (isRunning) status = 'Playing';
            if (gameWon) status = 'Won';
            if (gameOver && !gameWon) status = 'Lost';
            
            safeInvoke('OnGameStateChanged', mineCount - flaggedCount, elapsedTime, status);
        }
    }

    function renderGrid() {
        if (!ctx) return;
        
        // Clear canvas
        ctx.fillStyle = '#c0c0c0';
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        
        for (let r = 0; r < rows; r++) {
            for (let c = 0; c < cols; c++) {
                renderCell(r, c);
            }
        }
    }

    function renderCell(row, col) {
        const cell = grid[row][col];
        const x = col * cellSize;
        const y = row * cellSize;
        
        if (cell.state === HIDDEN || cell.state === FLAGGED) {
            // Draw 3D raised effect
            ctx.fillStyle = COLORS.hidden;
            ctx.fillRect(x, y, cellSize, cellSize);
            
            // Highlight (top-left)
            ctx.fillStyle = COLORS.hiddenHighlight;
            ctx.fillRect(x, y, cellSize, 2);
            ctx.fillRect(x, y, 2, cellSize);
            
            // Shadow (bottom-right)
            ctx.fillStyle = COLORS.hiddenBorder;
            ctx.fillRect(x + cellSize - 2, y, 2, cellSize);
            ctx.fillRect(x, y + cellSize - 2, cellSize, 2);
            
            // Draw flag
            if (cell.state === FLAGGED) {
                ctx.fillStyle = COLORS.flag;
                ctx.font = `${Math.floor(cellSize * 0.6)}px Arial`;
                ctx.textAlign = 'center';
                ctx.textBaseline = 'middle';
                ctx.fillText('🚩', x + cellSize / 2, y + cellSize / 2);
            }
        } else {
            // Revealed cell
            ctx.fillStyle = COLORS.revealed;
            ctx.fillRect(x, y, cellSize, cellSize);
            
            // Border
            ctx.strokeStyle = COLORS.revealedBorder;
            ctx.strokeRect(x, y, cellSize, cellSize);
            
            if (cell.mine) {
                // Draw mine
                ctx.fillStyle = gameWon ? '#00ff00' : COLORS.mine;
                ctx.font = `${Math.floor(cellSize * 0.6)}px Arial`;
                ctx.textAlign = 'center';
                ctx.textBaseline = 'middle';
                ctx.fillText('💣', x + cellSize / 2, y + cellSize / 2);
            } else if (cell.adjacentMines > 0) {
                // Draw number
                ctx.fillStyle = COLORS.numbers[cell.adjacentMines];
                ctx.font = `bold ${Math.floor(cellSize * 0.6)}px Arial`;
                ctx.textAlign = 'center';
                ctx.textBaseline = 'middle';
                ctx.fillText(cell.adjacentMines.toString(), x + cellSize / 2, y + cellSize / 2);
            }
        }
    }

    function debounce(func, wait) {
        let timeout;
        return function(...args) {
            clearTimeout(timeout);
            timeout = setTimeout(() => func(...args), wait);
        };
    }

    window.cleanupMinesweeper = function() {
        // Save state before cleanup
        if (gridInitialized) {
            saveGameState();
        }
        
        stopTimer();
        
        if (canvas) {
            canvas.removeEventListener('click', handleClick);
            canvas.removeEventListener('contextmenu', handleRightClick);
            canvas.removeEventListener('touchstart', handleTouchStart);
            canvas.removeEventListener('touchend', handleTouchEnd);
            canvas.removeEventListener('touchmove', handleTouchMove);
        }
        
        isRunning = false;
        componentRef = null;
    };

    // Expose for testing
    window.getMinesweeperState = function() {
        return {
            rows,
            cols,
            mineCount,
            flaggedCount,
            revealedCount,
            gameOver,
            gameWon,
            elapsedTime,
            grid: grid.map(row => row.map(cell => ({
                mine: cell.mine,
                state: cell.state,
                adjacentMines: cell.adjacentMines
            })))
        };
    };

    console.log('[Minesweeper v3] minesweeper-game.js loaded');
})();
