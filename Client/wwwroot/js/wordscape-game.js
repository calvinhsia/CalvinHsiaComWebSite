// WordScape Game JavaScript Functions - Enhanced with Conditional Debug Logging

// Global debug state for WordScape - can be controlled by C#
window.wordScapeDebug = {
    enabled: false
};

// ? NEW: Touch event counter for performance monitoring
window.wordScapeTouchStats = {
    totalTouchMoves: 0,
    processedTouchMoves: 0,
    lastResetTime: Date.now()
};

// Debug logging functions - only log when debug is enabled
function debugLog(message, ...args) {
    if (window.wordScapeDebug.enabled) {
        console.log(`[WordScapeDebug] ${message}`, ...args);
    }
}

// ? NEW: Performance logging
function debugTouch(message, ...args) {
    if (window.wordScapeDebug.enabled) {
        window.wordScapeTouchStats.processedTouchMoves++;
        var elapsed = Date.now() - window.wordScapeTouchStats.lastResetTime;
        if (elapsed > 1000) {
            var fps = (window.wordScapeTouchStats.processedTouchMoves / elapsed * 1000).toFixed(1);
            console.log(`[TouchStats] ${fps} touch events/sec (${window.wordScapeTouchStats.totalTouchMoves} total, ${window.wordScapeTouchStats.processedTouchMoves} processed)`);
            window.wordScapeTouchStats.totalTouchMoves = 0;
            window.wordScapeTouchStats.processedTouchMoves = 0;
            window.wordScapeTouchStats.lastResetTime = Date.now();
        }
        console.log(`[WordScapeTouch] ${message}`, ...args);
    }
}

// Function to set debug mode from C#
window.setWordScapeDebugMode = function(enabled) {
    window.wordScapeDebug.enabled = enabled;
    
    if (enabled) {
        console.log('[WordScape] Debug mode enabled - JavaScript will now log debug information');
    } else {
        console.log('[WordScape] Debug mode disabled - JavaScript logging reduced');
    }
    
    return window.wordScapeDebug;
};

// Function to convert client coordinates to SVG coordinates
window.convertClientToSVGCoordinates = function (svgElementRef, clientX, clientY) {
    try {
        // Get the actual SVG element
        let svgElement = svgElementRef;

        // Handle Blazor ElementReference
        if (!svgElement || svgElement.tagName !== 'svg') {
            svgElement = document.querySelector('.letter-wheel svg');
        }

        if (!svgElement) {
            debugError('SVG element not found for coordinate conversion');
            return [0, 0];
        }

        // ? OPTIMIZATION: Cache the rect and viewBox for better performance
        if (!svgElement._cachedRect || Date.now() - (svgElement._cacheTime || 0) > 1000) {
            svgElement._cachedRect = svgElement.getBoundingClientRect();
            svgElement._cachedViewBox = svgElement.viewBox.baseVal || { width: 600, height: 600 };
            svgElement._cacheTime = Date.now();
        }

        const rect = svgElement._cachedRect;
        const viewBox = svgElement._cachedViewBox;

        // Convert client coordinates to SVG coordinates
        const svgX = (clientX - rect.left) * (viewBox.width / rect.width);
        const svgY = (clientY - rect.top) * (viewBox.height / rect.height);

        return [svgX, svgY];
    } catch (error) {
        debugError('Error converting coordinates:', error);
        return [0, 0];
    }
};

// Function to calculate optimal grid size based on device characteristics
window.calculateOptimalGridSize = function () {
    const windowWidth = window.innerWidth;
    const windowHeight = window.innerHeight;
    const isAndroid = /Android/i.test(navigator.userAgent);

    // Cell size calculation from CSS: 28.8px + 1px gap + 2px padding + 2px border
    const cellTotalWidth = 28.8 + 1; // cell + gap
    const containerOverhead = (2 + 2) * 2; // padding + border on both sides

    // Account for Android's new full-width layout with zero padding overhead
    let layoutPaddingOverhead = 0;
    if (isAndroid) {
        // Android devices now use full-width layout with no padding overhead
        layoutPaddingOverhead = 0;
        debugLog(`Android device detected - using full-width layout with zero padding overhead`);
    } else if (windowWidth <= 768) {
        // Non-Android mobile devices still have layout padding
        layoutPaddingOverhead = (4 + 8 + 16) * 2; // game-content + wordscape + bootstrap px-4 estimated
    } else {
        layoutPaddingOverhead = (8 + 20 + 24) * 2; // desktop values
    }

    // More aggressive width utilization, especially for Android
    let availableWidth;
    if (isAndroid) {
        // Android gets nearly full viewport width since all padding is removed
        availableWidth = windowWidth * 0.995 - layoutPaddingOverhead; // 99.5% for Android
    } else if (windowWidth <= 480) {
        availableWidth = windowWidth * 0.92 - layoutPaddingOverhead; // 92% for mobile
    } else if (windowWidth <= 320) {
        availableWidth = windowWidth * 0.90 - layoutPaddingOverhead; // 90% for very small screens
    } else {
        availableWidth = windowWidth * 0.95 - layoutPaddingOverhead; // 95% for desktop
    }

    // Calculate max cells that can fit horizontally
    const maxCellsHorizontal = Math.floor((availableWidth - containerOverhead) / cellTotalWidth);

    // Allow wider grids for better screen utilization - max 18
    const optimalWidth = Math.max(8, Math.min(18, maxCellsHorizontal));

    // For square grids, use same width and height
    // But allow slightly rectangular for mobile optimization
    let optimalHeight = optimalWidth;
    if (windowWidth <= 480 && windowHeight < windowWidth) {
        // Landscape mobile - slightly reduce height
        optimalHeight = Math.max(6, optimalWidth - 2);
    }

    debugLog(`Grid size calculation:
            Window: ${windowWidth}x${windowHeight}
            Is Android: ${isAndroid}
            Layout padding overhead: ${layoutPaddingOverhead}px (Android uses full-width: ${isAndroid ? 'YES' : 'NO'})
            Available width: ${availableWidth}px
            Max cells horizontal: ${maxCellsHorizontal}
            Optimal grid: ${optimalWidth}x${optimalHeight}`);

    return { width: optimalWidth, height: optimalHeight };
};

// Function to fix Android grid positioning issues
window.fixAndroidGridPosition = function () {
    const isAndroid = /Android/i.test(navigator.userAgent);

    if (isAndroid) {
        const gridContainer = document.querySelector('.grid-container');
        const gameGrid = document.querySelector('.game-grid');
        const currentWordBar = document.querySelector('.current-word-bar');
        const gameContent = document.querySelector('.game-content');
        const wordscapeGame = document.querySelector('.wordscape-fixed-game');

        if (gridContainer && gameGrid) {
            debugLog('Making Android grid flush like current-word-bar buttons...');

            // Remove all container padding that prevents flush alignment
            if (gameContent) {
                gameContent.style.paddingLeft = '0';
                gameContent.style.paddingRight = '0';
            }

            if (wordscapeGame) {
                wordscapeGame.style.paddingLeft = '0';
                wordscapeGame.style.paddingRight = '0';
                wordscapeGame.style.marginLeft = '0';
                wordscapeGame.style.marginRight = '0';
                wordscapeGame.style.width = '100vw';
                wordscapeGame.style.maxWidth = '100vw';
            }

            // Make game-grid behave like current-word-bar (full width flexbox)
            gameGrid.style.display = 'flex';
            gameGrid.style.justifyContent = 'center';
            gameGrid.style.alignItems = 'center';
            gameGrid.style.width = '100%';
            gameGrid.style.margin = '0';
            gameGrid.style.padding = '0';

            // Make grid-container behave like the current-word-display (centered content)
            gridContainer.style.maxWidth = 'none';
            gridContainer.style.width = 'fit-content';
            gridContainer.style.margin = '0';
            gridContainer.style.padding = '1px';
            gridContainer.style.position = 'static';
            gridContainer.style.left = 'auto';
            gridContainer.style.transform = 'none';

            debugLog('Enhanced Android grid positioning fix applied - grid should now be flush like buttons');
        }
    }
};

// Function to aggressively remove ALL potential sources of padding for Android
window.makeGridEdgeToEdgeAndroid = function () {
    const isAndroid = /Android/i.test(navigator.userAgent);

    if (isAndroid) {
        debugLog('Making grid flush like current-word-bar on Android...');

        // Find and remove ALL potential sources of padding sources that prevent flush positioning
        const selectors = [
            '.game-content',
            '.wordscape-fixed-game',
            'article.content',
            '.content',
            '.px-4',
            'main',
            '.page'
        ];

        selectors.forEach(selector => {
            const elements = document.querySelectorAll(selector);
            elements.forEach(element => {
                element.style.paddingLeft = '0';
                element.style.paddingRight = '0';
                element.style.marginLeft = '0';
                element.style.marginRight = '0';

                // Make containers full width like current-word-bar parent
                if (selector === '.wordscape-fixed-game') {
                    element.style.width = '100vw';
                    element.style.maxWidth = '100vw';
                }
            });
        });

        // Apply current-word-bar style flexbox layout to grid
        const gameGrid = document.querySelector('.game-grid');
        const gridContainer = document.querySelector('.grid-container');

        if (gameGrid) {
            // Make game-grid behave like current-word-bar (flexbox container)
            gameGrid.style.display = 'flex';
            gameGrid.style.justifyContent = 'center';
            gameGrid.style.alignItems = 'center';
            gameGrid.style.width = '100%';
            gameGrid.style.margin = '0';
            gameGrid.style.padding = '0';
            // Remove transform positioning
            gameGrid.style.position = 'static';
            gameGrid.style.left = 'auto';
            gameGrid.style.transform = 'none';
        }

        if (gridContainer) {
            // Make grid-container behave like current-word-display (centered flex child)
            gridContainer.style.margin = '0';
            gridContainer.style.padding = '1px';
            gridContainer.style.maxWidth = 'none';
            gridContainer.style.width = 'fit-content';
            // Remove transform positioning
            gridContainer.style.position = 'static';
            gridContainer.style.left = 'auto';
            gridContainer.style.transform = 'none';
        }

        debugLog('Android grid now uses flexbox positioning like current-word-bar');
    }
};

// Function to force Android full width by overriding CSS width constraints
window.forceAndroidFullWidth = function () {
    const isAndroid = /Android/i.test(navigator.userAgent);

    if (isAndroid) {
        debugLog('Forcing Android full width to override CSS constraints...');

        // Create a high-specificity CSS rule to override the CSS file
        const highSpecificityStyle = document.createElement('style');
        highSpecificityStyle.id = 'android-full-width-override';
        highSpecificityStyle.textContent = `
                /* HIGH SPECIFICITY Android full-width overrides */
                html body .page main article.content.game-content.px-4 {
                    width: 100vw !important;
                    max-width: 100vw !important;
                    padding-left: 0 !important;
                    padding-right: 0 !important;
                    margin-left: 0 !important;
                    margin-right: 0 !important;
                }

                html body .page main article.content .wordscape-fixed-game {
                    /* FORCE override CSS file's width: 1080px and max-width: 95vw */
                    width: 100vw !important;
                    max-width: 100vw !important;
                    min-width: 100vw !important;
                    padding-left: 0 !important;
                    padding-right: 0 !important;
                    margin-left: 0 !important;
                    margin-right: 0 !important;
                    box-sizing: border-box !important;
                }

                /* Force body and html to accommodate full width */
                html,
                html body {
                    width: 100% !important;
                    max-width: 100% !important;
                    overflow-x: hidden !important;
                    margin: 0 !important;
                    padding: 0 !important;
                }
            `;

        // Insert at the end of head for maximum specificity precedence
        document.head.appendChild(highSpecificityStyle);

        // Also apply via JavaScript for immediate effect
        const wordscapeGame = document.querySelector('.wordscape-fixed-game');
        const gameContent = document.querySelector('.game-content');

        if (wordscapeGame) {
            wordscapeGame.style.setProperty('width', '100vw', 'important');
            wordscapeGame.style.setProperty('max-width', '100vw', 'important');
            wordscapeGame.style.setProperty('min-width', '100vw', 'important');
            wordscapeGame.style.setProperty('margin-left', '0', 'important');
            wordscapeGame.style.setProperty('margin-right', '0', 'important');
            wordscapeGame.style.setProperty('padding-left', '0', 'important');
            wordscapeGame.style.setProperty('padding-right', '0', 'important');
            wordscapeGame.style.setProperty('box-sizing', 'border-box', 'important');
        }

        if (gameContent) {
            gameContent.style.setProperty('width', '100vw', 'important');
            gameContent.style.setProperty('max-width', '100vw', 'important');
            gameContent.style.setProperty('padding-left', '0', 'important');
            gameContent.style.setProperty('padding-right', '0', 'important');
            gameContent.style.setProperty('margin-left', '0', 'important');
            gameContent.style.setProperty('margin-right', '0', 'important');
        }

        debugLog('Android full width CSS override applied');
    }
};

// Mobile-friendly letter wheel visibility handler
window.ensureLetterWheelVisibility = function () {
    const letterWheel = document.querySelector('.letter-wheel');
    const svg = document.querySelector('.letter-wheel svg');

    if (!letterWheel || !svg) {
        // Only retry if we're actually on the wordscape page and elements are genuinely missing
        if (window.location.pathname.includes('/wordscape')) {
            // Limit retries to prevent infinite loop
            if (!window.letterWheelRetryCount) {
                window.letterWheelRetryCount = 0;
            }

            if (window.letterWheelRetryCount < 10) {
                window.letterWheelRetryCount++;
                debugLog(`Letter wheel elements not found, will retry... (${window.letterWheelRetryCount}/10)`);
                setTimeout(window.ensureLetterWheelVisibility, 500);
            } else {
                debugLog('Letter wheel elements not found after 10 retries, stopping');
            }
        }
        return;
    }

    // Reset retry counter when elements are found
    window.letterWheelRetryCount = 0;

    const windowWidth = window.innerWidth;
    const windowHeight = window.innerHeight;

    debugLog(`Letter wheel elements found and configured for: ${windowWidth}x${windowHeight}`);
};

// Initialize WordScape-specific functionality
window.initializeWordScape = function () {
    debugLog('Initializing WordScape game...');
    
    // Only apply WordScape functionality if on the WordScape page
    if (window.location.pathname.includes('/wordscape')) {
        // Initialize address bar management for Android Edge
        if (window.addressBarManager && window.addressBarManager.init) {
            debugLog('Initializing address bar management for WordScape...');
            window.addressBarManager.init();
        }

        // Monitor window resize for letter wheel
        let resizeTimeout;
        window.addEventListener('resize', function () {
            clearTimeout(resizeTimeout);
            resizeTimeout = setTimeout(window.ensureLetterWheelVisibility, 100);
        });

        // Check on window load
        window.addEventListener('load', function () {
            setTimeout(window.ensureLetterWheelVisibility, 500);
        });

        // Apply Android-specific fixes with additional delay for address bar optimization
        setTimeout(() => {
            window.fixAndroidGridPosition();
            window.makeGridEdgeToEdgeAndroid();
            window.forceAndroidFullWidth();
            
            // If address bar manager is available, apply its optimizations too
            if (window.addressBarManager && window.addressBarManager.isAndroidEdge) {
                debugLog('Applying additional address bar optimizations for WordScape grid positioning...');
                
                // Add specific WordScape optimizations for address bar at top
                const wordscapeAddressBarStyle = document.createElement('style');
                wordscapeAddressBarStyle.id = 'wordscape-address-bar-optimization';
                wordscapeAddressBarStyle.textContent = `
                    /* WordScape-specific optimizations for address bar at top */
                    .wordscape-fixed-game {
                        /* Use full available viewport height when address bar is at top */
                        min-height: 100vh !important;
                        min-height: 100svh !important; /* Small viewport height */
                        height: 100vh !important;
                        height: 100svh !important;
                        max-height: 100vh !important;
                        max-height: 100svh !important;
                        overflow-y: auto !important;
                        padding-top: env(safe-area-inset-top, 0px) !important;
                        padding-bottom: env(safe-area-inset-bottom, 0px) !important;
                    }
                    
                    /* Use dynamic viewport units when available */
                    @supports (height: 100dvh) {
                        .wordscape-fixed-game {
                            min-height: 100dvh !important;
                            height: 100dvh !important;
                            max-height: 100dvh !important;
                        }
                    }
                    
                    /* Optimize game grid for maximum viewport usage */
                    .game-grid {
                        /* Ensure grid takes advantage of full viewport */
                        max-height: calc(40vh - env(safe-area-inset-top, 0px)) !important;
                        overflow: visible !important;
                    }
                    
                    /* Optimize letter wheel for viewport */
                    .game-wheel {
                        max-height: calc(45vh - env(safe-area-inset-bottom, 0px)) !important;
                        overflow: visible !important;
                    }
                    
                    /* Optimize found words section for remaining space */
                    .found-words-section {
                        max-height: calc(15vh - env(safe-area-inset-bottom, 0px)) !important;
                        overflow-y: auto !important;
                        flex-shrink: 1 !important;
                    }
                `;
                
                if (!document.head.querySelector('#wordscape-address-bar-optimization')) {
                    document.head.appendChild(wordscapeAddressBarStyle);
                    debugLog('WordScape address bar optimization CSS applied');
                }
            }
        }, 200);

        debugLog('WordScape initialization complete');
    }
};

// Enhanced function to trigger all grid animations at once for better Windows compatibility
window.animateAllGridCells = function() {
    try {
        const cells = document.querySelectorAll('.grid-cell.revealed');
        debugLog(`Animating all ${cells.length} revealed cells`);
        
        if (cells.length === 0) {
            debugLog('No revealed cells found for animation');
            return 0;
        }
        
        // Force a style recalculation before starting animations
        cells.forEach(cell => {
            cell.offsetHeight; // Force reflow
        });
        
        cells.forEach((cell, index) => {
            setTimeout(() => {
                if (cell && !cell.classList.contains('celebration-flash')) {
                    debugLog(`Adding celebration-flash to cell ${index}`);
                    cell.classList.add('celebration-flash');
                    
                    // Force reflow to ensure animation starts
                    cell.offsetHeight;
                    
                    // Remove after animation with extra time buffer
                    setTimeout(() => {
                        if (cell && cell.classList.contains('celebration-flash')) {
                            debugLog(`Removing celebration-flash from cell ${index}`);
                            cell.classList.remove('celebration-flash');
                        }
                    }, 1000); // Increased from 800ms for better visibility
                }
            }, index * 50); // 50ms stagger between cells
        });
        
        return cells.length;
    } catch (error) {
        debugError('Error in animateAllGridCells:', error);
        return 0;
    }
};

// FIXED: Function to animate only the specific word cells that were just revealed
window.animateSpecificWordReveal = function(word, wordPlacement) {
    try {
        debugLog(`Animating specific word reveal for: ${word}`);
        
        // Fixed validation logic - check if wordPlacement is valid
        if (!wordPlacement || wordPlacement.startX === undefined || wordPlacement.startY === undefined) {
            debugLog('Invalid word placement data, falling back to all cells animation');
            return window.animateWordReveal(word);
        }
        
        const { startX, startY, isHorizontal, length } = wordPlacement;
        debugLog(`Word placement: (${startX}, ${startY}), horizontal: ${isHorizontal}, length: ${length}`);
        
        // Find only the cells that belong to this specific word
        const wordCells = [];
        for (let i = 0; i < length; i++) {
            const x = isHorizontal ? startX + i : startX;
            const y = isHorizontal ? startY : startY + i;
            
            // Find the grid cell at this position using a more robust selector
            let cell = document.querySelector(`.grid-cell[style*="grid-column: ${x + 1}"][style*="grid-row: ${y + 1}"]`);
            
            // Fallback: try alternative selectors if the first one doesn't work
            if (!cell) {
                // Try alternative grid positioning attributes
                cell = document.querySelector(`[data-x="${x}"][data-y="${y}"].grid-cell`);
            }
            
            if (!cell) {
                // Try finding by position in the grid layout
                const allCells = document.querySelectorAll('.grid-cell');
                const gridContainer = document.querySelector('.grid-container');
                if (gridContainer && allCells.length > 0) {
                    // Calculate grid dimensions from CSS
                    const computedStyle = window.getComputedStyle(gridContainer);
                    const gridCols = computedStyle.gridTemplateColumns ? computedStyle.gridTemplateColumns.split(' ').length : 0;
                    
                    if (gridCols > 0) {
                        const cellIndex = y * gridCols + x;
                        if (cellIndex < allCells.length) {
                            cell = allCells[cellIndex];
                        }
                    }
                }
            }
            
            if (cell && cell.classList.contains('revealed')) {
                wordCells.push({ cell, index: i });
                debugLog(`Found word cell at (${x}, ${y})`);
            } else {
                debugLog(`Could not find revealed word cell at (${x}, ${y})`);
            }
        }
        
        if (wordCells.length === 0) {
            debugLog('No word cells found for animation, using fallback');
            return window.animateWordReveal(word);
        }
        
        debugLog(`Animating ${wordCells.length} specific cells for word "${word}"`);
        
        // Animate only the cells that belong to this word with distinct animation
        wordCells.forEach(({ cell, index }) => {
            setTimeout(() => {
                if (cell && !cell.classList.contains('word-reveal-flash')) {
                    debugLog(`Adding word-reveal-flash to word cell ${index}`);
                    cell.classList.add('word-reveal-flash');
                    
                    // Force reflow to ensure animation starts
                    cell.offsetHeight;
                    
                    // Remove after animation completes
                    setTimeout(() => {
                        if (cell && cell.classList.contains('word-reveal-flash')) {
                            debugLog(`Removing word-reveal-flash from word cell ${index}`);
                            cell.classList.remove('word-reveal-flash');
                        }
                    }, 1200); // Match animation duration
                }
            }, index * 100); // Slower stagger for better visibility of the word
        });
        
        return wordCells.length;
    } catch (error) {
        debugError('Error in animateSpecificWordReveal:', error);
        // Fallback to the general animation
        return window.animateWordReveal(word);
    }
};

// REPLACED: Function to animate ONLY the cells that were just revealed for this specific word
window.animateWordReveal = function(word) {
    try {
        debugLog(`Animating word reveal for: ${word} (fallback - should use specific animation if possible)`);
        
        // WARNING: This is a fallback function that still animates all revealed cells
        // It should only be used when word placement data is not available
        // The proper fix is to always use animateSpecificWordReveal with correct placement data
        
        // Find all revealed cells (this is the problem - it animates ALL revealed cells)
        const cells = document.querySelectorAll('.grid-cell.revealed');
        
        if (cells.length === 0) {
            debugLog('No revealed cells found for word reveal animation');
            return 0;
        }
        
        debugLog(`FALLBACK: Animating all ${cells.length} revealed cells for word "${word}" - this should be avoided`);
        
        // Animate cells with the word-reveal animation
        cells.forEach((cell, index) => {
            setTimeout(() => {
                if (cell && !cell.classList.contains('word-reveal-flash')) {
                    debugLog(`Adding word-reveal-flash to cell ${index}`);
                    cell.classList.add('word-reveal-flash');
                    
                    // Force reflow to ensure animation starts
                    cell.offsetHeight;
                    
                    // Remove after animation completes
                    setTimeout(() => {
                        if (cell && cell.classList.contains('word-reveal-flash')) {
                            debugLog(`Removing word-reveal-flash from cell ${index}`);
                            cell.classList.remove('word-reveal-flash');
                        }
                    }, 1200); // Match animation duration
                }
            }, index * 25); // Faster stagger for word reveals
        });
        
        return cells.length;
    } catch (error) {
        debugError('Error in animateWordReveal:', error);
        return 0;
    }
};

// Function to animate grid cells for celebration - Enhanced for Windows compatibility
window.addCelebrationFlash = function(cellIndex) {
    try {
        const cells = document.querySelectorAll('.grid-cell.revealed');
        debugLog(`Attempting to animate cell ${cellIndex} of ${cells.length} revealed cells`);
        
        if (cells && cellIndex < cells.length) {
            const cell = cells[cellIndex];
            debugLog(`Adding celebration-flash to cell ${cellIndex}`);
            
            // Force a reflow to ensure the animation is applied
            cell.classList.add('celebration-flash');
            cell.offsetHeight; // Trigger reflow
            
            // Remove animation class after animation completes
            setTimeout(() => {
                if (cell) {
                    debugLog(`Removing celebration-flash from cell ${cellIndex}`);
                    cell.classList.remove('celebration-flash');
                }
            }, 1000); // Increased from 800ms
        } else {
            debugLog(`Could not find cell ${cellIndex} in ${cells ? cells.length : 0} revealed cells`);
        }
    } catch (error) {
        debugError('Error in addCelebrationFlash:', error);
    }
};

// Debug function to manually test grid animations
window.testGridAnimations = function() {
    debugLog('Testing grid animations...');
    const cells = document.querySelectorAll('.grid-cell');
    debugLog(`Found ${cells.length} total grid cells`);
    
    // Add revealed class to all cells for testing
    cells.forEach(cell => {
        if (!cell.classList.contains('revealed')) {
            cell.classList.add('revealed');
            cell.textContent = 'A'; // Add sample text
        }
    });
    
    // Wait a moment then animate
    setTimeout(() => {
        window.animateAllGridCells();
    }, 100);
    
    return `Testing ${cells.length} cells`;
};

// Auto-initialize if on WordScape page - always log basic initialization
console.log('[WordScape] JavaScript file loaded at:', new Date().toLocaleTimeString());

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', window.initializeWordScape);
} else {
    window.initializeWordScape();
}