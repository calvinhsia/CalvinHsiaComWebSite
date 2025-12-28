// Minesweeper Game - JavaScript support
// v5 - Fixed missing functions that were causing test failures

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
    let touchStartPos = null;
    let touchMoved = false;
    const LONG_PRESS_DURATION = 500;
    const SCROLL_THRESHOLD = 10;

    // State persistence key
    const MINESWEEPER_STATE_KEY = 'minesweeper_game_state';

    console.log('[Minesweeper v5] minesweeper-game.js loading...');

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

    // ==================== TIMER FUNCTIONS ====================
    
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

    function startTimerFromElapsed(startTime) {
        if (timerInterval) return;
        
        elapsedTime = startTime;
        timerInterval = setInterval(() => {
            elapsedTime++;
            updateGameState();
            saveGameState();
        }, 1000);
    }

    // ==================== STATE MANAGEMENT ====================

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
            console.log(`[Minesweeper v5] Game state saved - ${status}, Time: ${elapsedTime}s`);
        } catch (ex) {
            console.error('[Minesweeper v5] Error saving game state:', ex);
        }
    }

    function loadGameState() {
        try {
            const json = localStorage.getItem(MINESWEEPER_STATE_KEY);
            if (!json) return null;

            const state = JSON.parse(json);
            console.log(`[Minesweeper v5] Game state loaded - ${state.gameStatus}, Time: ${state.elapsedTime}s`);
            return state;
        } catch (ex) {
            console.error('[Minesweeper v5] Error loading game state:', ex);
            return null;
        }
    }

    function restoreGameState(state) {
        if (!state || !state.cells || state.cells.length === 0) {
            console.log('[Minesweeper v5] No valid state to restore');
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
            
            if (state.gameStatus === 'Playing' && !gameOver) {
                isRunning = true;
                startTimerFromElapsed(elapsedTime);
            }

            console.log(`[Minesweeper v5] Game state restored - ${state.gameStatus}`);
            return true;
        } catch (ex) {
            console.error('[Minesweeper v5] Error restoring game state:', ex);
            return false;
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

    // ==================== GAME LOGIC ====================

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
        
        gridInitialized = true;
        flaggedCount = 0;
        revealedCount = 0;
        gameOver = false;
        gameWon = false;
        firstClick = true;
        elapsedTime = 0;
        isRunning = false;
        
        renderGrid();
        updateGameState();
    }

    function placeMines(excludeRow, excludeCol) {
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
            console.log('[Minesweeper v5] Game over - hit mine!');
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
            console.log(`[Minesweeper v5] You won in ${elapsedTime} seconds!`);
        }
    }

    // ==================== RENDERING ====================

    function renderGrid() {
        if (!ctx) return;
        
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
                ctx.fillStyle = gameWon ? '#00ff00' : COLORS.mine;
                ctx.font = `${Math.floor(cellSize * 0.6)}px Arial`;
                ctx.textAlign = 'center';
                ctx.textBaseline = 'middle';
                ctx.fillText('💣', x + cellSize / 2, y + cellSize / 2);
            } else if (cell.adjacentMines > 0) {
                ctx.fillStyle = COLORS.numbers[cell.adjacentMines];
                ctx.font = `bold ${Math.floor(cellSize * 0.6)}px Arial`;
                ctx.textAlign = 'center';
                ctx.textBaseline = 'middle';
                ctx.fillText(cell.adjacentMines.toString(), x + cellSize / 2, y + cellSize / 2);
            }
        }
    }

    // ==================== INPUT HANDLERS ====================

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
        
        longPressTimer = setTimeout(() => {
            if (!touchMoved) {
                longPressTriggered = true;
                if (row >= 0 && row < rows && col >= 0 && col < cols) {
                    toggleFlag(row, col);
                }
            }
        }, LONG_PRESS_DURATION);
    }

    function handleTouchEnd(e) {
        if (!touchMoved) {
            e.preventDefault();
        }
        
        if (longPressTimer) {
            clearTimeout(longPressTimer);
            longPressTimer = null;
        }
        
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
        if (touchStartPos && e.touches.length > 0) {
            const touch = e.touches[0];
            const deltaX = Math.abs(touch.clientX - touchStartPos.x);
            const deltaY = Math.abs(touch.clientY - touchStartPos.y);
            
            if (deltaX > SCROLL_THRESHOLD || deltaY > SCROLL_THRESHOLD) {
                touchMoved = true;
                if (longPressTimer) {
                    clearTimeout(longPressTimer);
                    longPressTimer = null;
                }
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

    // ==================== PUBLIC API ====================

    window.clearMinesweeperState = function() {
        try {
            localStorage.removeItem(MINESWEEPER_STATE_KEY);
            console.log('[Minesweeper v5] Game state cleared');
        } catch (ex) {
            console.error('[Minesweeper v5] Error clearing game state:', ex);
        }
    };

    window.hasMinesweeperState = function() {
        try {
            const json = localStorage.getItem(MINESWEEPER_STATE_KEY);
            return !!json;
        } catch {
            return false;
        }
    };

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
            console.error('[Minesweeper v5] Canvas not found:', canvasId);
            return;
        }

        firstResizeCallbackSent = false;
        resizeRetryCount = 0;
        isRunning = false;
        gameOver = false;
        gameWon = false;
        
        stopTimer();

        ctx = canvas.getContext('2d');

        canvas.addEventListener('click', handleClick);
        canvas.addEventListener('contextmenu', handleRightClick);
        canvas.addEventListener('touchstart', handleTouchStart, { passive: true });
        canvas.addEventListener('touchend', handleTouchEnd, { passive: false });
        canvas.addEventListener('touchmove', handleTouchMove, { passive: true });

        console.log('[Minesweeper v5] Canvas initialized');
    };

    window.setMinesweeperComponentRef = function(ref) {
        componentRef = ref;
        console.log('[Minesweeper v5] Component reference set');
        
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

        document.addEventListener('visibilitychange', () => {
            if (document.hidden && gridInitialized) {
                console.log('[Minesweeper v5] Page hidden - saving state');
                saveGameState();
            }
        });

        window.addEventListener('beforeunload', () => {
            if (gridInitialized) {
                console.log('[Minesweeper v5] Before unload - saving state');
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
        
        const isMobile = window.innerWidth <= 768;
        
        if (isMobile) {
            const easyCellSize = Math.floor((sectionWidth - 20) / 9);
            cellSize = Math.min(easyCellSize, 40);
            cellSize = Math.max(cellSize, 30);
        } else {
            const maxCellWidth = Math.floor((sectionWidth - 20) / cols);
            const maxCellHeight = Math.floor((sectionHeight - 20) / rows);
            cellSize = Math.min(maxCellWidth, maxCellHeight, 40);
            cellSize = Math.max(cellSize, 20);
        }

        canvas.width = cols * cellSize;
        canvas.height = rows * cellSize;

        if (gridInitialized) {
            renderGrid();
        }

        if (!firstResizeCallbackSent && componentRef) {
            firstResizeCallbackSent = true;
            safeInvoke('OnCanvasResized', canvas.width, canvas.height);
        }
    };

    window.initMinesweeperGame = function(difficulty, tryRestore = true) {
        console.log(`[Minesweeper v5] initMinesweeperGame called with difficulty: ${difficulty}, tryRestore: ${tryRestore}`);
        
        if (tryRestore) {
            const savedState = loadGameState();
            if (savedState && savedState.difficulty === difficulty && !savedState.gameOver) {
                if (restoreGameState(savedState)) {
                    window.resizeMinesweeperCanvas();
                    updateGameState();
                    renderGrid();
                    console.log(`[Minesweeper v5] Game restored from saved state`);
                    return;
                }
            }
        }

        const config = DIFFICULTIES[difficulty] || DIFFICULTIES.easy;
        rows = config.rows;
        cols = config.cols;
        mineCount = config.mines;
        currentDifficulty = difficulty;
        
        resetGame();
        window.resizeMinesweeperCanvas();
        
        console.log(`[Minesweeper v5] Game initialized: ${rows}x${cols}, ${mineCount} mines`);
    };

    // NEW: This function was missing - called by C# when difficulty changes or New Game is clicked
    window.newMinesweeperGame = function(difficulty) {
        console.log(`[Minesweeper v5] newMinesweeperGame called with difficulty: ${difficulty}`);
        
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
        
        console.log(`[Minesweeper v5] New game: ${rows}x${cols}, ${mineCount} mines`);
    };

    window.cleanupMinesweeper = function() {
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

    console.log('[Minesweeper v5] minesweeper-game.js loaded');
})();
