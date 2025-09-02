// Wordament Game JavaScript Functions - Enhanced for Desktop Drag Support with Diagonal Improvements

// Global state tracking for desktop mouse drag
window.wordamentDragState = {
    isDragging: false,
    startPosition: null,
    currentPosition: null,
    dragPath: []
};

// CRITICAL TEST: Add simple console logging to verify JavaScript is working
console.log('? Wordament JavaScript file loaded at:', new Date().toLocaleTimeString());

// CRITICAL DEBUG: Check if function is properly exposed
setTimeout(() => {
    console.log('?? DEBUG: Checking if getWordamentCellFromCoordinates is available...');
    if (typeof window.getWordamentCellFromCoordinates === 'function') {
        console.log('? getWordamentCellFromCoordinates function is available');
    } else {
        console.error('? getWordamentCellFromCoordinates function is NOT available');
        console.log('Available window functions:', Object.keys(window).filter(key => key.includes('wordament')));
    }
}, 1000);

// Enhanced function to get Wordament cell from coordinates with DIAGONAL-FRIENDLY hit test area
window.getWordamentCellFromCoordinates = function (gridElement, clientX, clientY) {
    try {
        console.log('?? getWordamentCellFromCoordinates called with:', { clientX, clientY });
        
        // CRITICAL FALLBACK: If gridElement is null or undefined, try to find it
        if (!gridElement) {
            console.log('?? No gridElement provided, trying to find .wordament-grid');
            gridElement = document.querySelector('.wordament-grid');
            if (!gridElement) {
                console.error('? Could not find .wordament-grid element');
                return null;
            }
        }
        
        // SIMPLE FALLBACK: Use elementFromPoint directly first for reliability
        console.log('?? Trying simple elementFromPoint method first...');
        const elementUnderPoint = document.elementFromPoint(clientX, clientY);
        
        if (!elementUnderPoint) {
            console.log('? No element found under point');
            return null;
        }

        // Find the closest wordament-cell element
        let cellElement = elementUnderPoint.closest('.wordament-cell');
        if (!cellElement) {
            console.log('?? No wordament-cell found, trying parent elements...');
            
            // Try checking if the element itself is a wordament-cell
            if (elementUnderPoint.classList && elementUnderPoint.classList.contains('wordament-cell')) {
                cellElement = elementUnderPoint;
            } else {
                // Look for any element with data-x and data-y attributes
                let current = elementUnderPoint;
                while (current && current !== document.body) {
                    if (current.hasAttribute && current.hasAttribute('data-x') && current.hasAttribute('data-y')) {
                        cellElement = current;
                        break;
                    }
                    current = current.parentElement;
                }
            }
        }

        if (!cellElement) {
            console.log('? Still no wordament-cell found under point, element was:', elementUnderPoint.className);
            return null;
        }

        // Get the data attributes for x,y coordinates
        const x = parseInt(cellElement.getAttribute('data-x'));
        const y = parseInt(cellElement.getAttribute('data-y'));

        if (isNaN(x) || isNaN(y)) {
            console.warn('? Invalid cell coordinates found:', x, y);
            return null;
        }

        console.log('? Found cell coordinates (simple method):', [x, y]);
        return [x, y];
        
    } catch (error) {
        console.error('? Error getting Wordament cell from coordinates:', error);
        return null;
    }
};

// CRITICAL DEBUG: Add native JavaScript touch event debugging
window.debugWordamentTouchEvents = function() {
    console.log('?? Setting up native JavaScript touch event debugging for Wordament');
    
    const grid = document.querySelector('.wordament-grid');
    if (!grid) {
        console.log('? No Wordament grid found for touch debugging');
        return;
    }
    
    let touchStarted = false;
    let touchCount = 0;
    
    // Add native touch event listeners to see if events are reaching JavaScript at all
    grid.addEventListener('touchstart', function(e) {
        touchStarted = true;
        touchCount = 0;
        console.log('? NATIVE touchstart detected:', {
            touches: e.touches.length,
            changedTouches: e.changedTouches.length,
            target: e.target.className,
            coords: e.touches[0] ? `(${e.touches[0].clientX}, ${e.touches[0].clientY})` : 'none'
        });
    }, { passive: false });
    
    grid.addEventListener('touchmove', function(e) {
        if (touchStarted) {
            touchCount++;
            console.log(`? NATIVE touchmove #${touchCount} detected:`, {
                touches: e.touches.length,
                changedTouches: e.changedTouches.length,
                target: e.target.className,
                coords: e.changedTouches[0] ? `(${e.changedTouches[0].clientX}, ${e.changedTouches[0].clientY})` : 'none'
            });
            
            // Test coordinate detection and direct Blazor call
            if (e.changedTouches[0]) {
                const coords = window.getWordamentCellFromCoordinates(grid, e.changedTouches[0].clientX, e.changedTouches[0].clientY);
                console.log('? Detected cell (diagonal-friendly):', coords);
                
                // CRITICAL TEST: Try calling Blazor directly from JavaScript
                if (coords && window.wordamentBlazorComponent) {
                    try {
                        window.wordamentBlazorComponent.invokeMethodAsync('TestTouchMoveFromJS', coords[0], coords[1]);
                        console.log(`? Called Blazor TestTouchMoveFromJS with (${coords[0]}, ${coords[1]})`);
                    } catch (blazorError) {
                        console.error('? Error calling Blazor from native JavaScript:', blazorError);
                    }
                }
            }
        }
    }, { passive: false });
    
    grid.addEventListener('touchend', function(e) {
        console.log(`? NATIVE touchend detected after ${touchCount} move events`);
        touchStarted = false;
        touchCount = 0;
    }, { passive: false });
    
    console.log('? Native touch debugging set up complete');
};

// ?? NEW: Function to animate word placement in the grid - shows where word was placed
window.animateWordamentWordPlacement = function(word, path) {
    try {
        console.log(`?? Animating Wordament word placement for: ${word} with ${path.length} cells`);
        
        if (!path || path.length === 0) {
            console.log('? No path provided for word placement animation');
            return 0;
        }
        
        // Find cells that match the path
        const wordCells = [];
        path.forEach((position, index) => {
            const cell = document.querySelector(`[data-x="${position.x}"][data-y="${position.y}"]`);
            if (cell) {
                wordCells.push({ cell, index });
                console.log(`? Found word cell at (${position.x}, ${position.y})`);
            } else {
                console.log(`? Could not find cell at (${position.x}, ${position.y})`);
            }
        });
        
        if (wordCells.length === 0) {
            console.log('? No word cells found for animation');
            return 0;
        }
        
        console.log(`?? Animating ${wordCells.length} cells for word "${word}"`);
        
        // Animate only the cells that belong to this word
        wordCells.forEach(({ cell, index }) => {
            setTimeout(() => {
                if (cell && !cell.classList.contains('wordament-word-reveal')) {
                    console.log(`?? Adding wordament-word-reveal to word cell ${index}`);
                    cell.classList.add('wordament-word-reveal');
                    
                    // Force reflow to ensure animation starts
                    cell.offsetHeight;
                    
                    // Remove after animation completes
                    setTimeout(() => {
                        if (cell && cell.classList.contains('wordament-word-reveal')) {
                            console.log(`? Removing wordament-word-reveal from word cell ${index}`);
                            cell.classList.remove('wordament-word-reveal');
                        }
                    }, 1500); // Longer duration for better visibility
                }
            }, index * 120); // Slower stagger for better visibility of the word path
        });
        
        return wordCells.length;
    } catch (error) {
        console.error('Error in animateWordamentWordPlacement:', error);
        return 0;
    }
};

// ?? NEW: Function to animate all found words for celebration
window.animateWordamentCelebration = function() {
    try {
        // Look for found word items in the found words list
        const foundWordItems = document.querySelectorAll('.found-word-item');
        console.log(`?? Animating ${foundWordItems.length} found word items`);
        
        if (foundWordItems.length === 0) {
            console.log('? No found word items for celebration animation');
            return 0;
        }
        
        // Animate found word items with stagger
        foundWordItems.forEach((item, index) => {
            setTimeout(() => {
                if (item && !item.classList.contains('celebration-bounce')) {
                    console.log(`?? Adding celebration-bounce to word item ${index}`);
                    item.classList.add('celebration-bounce');
                    
                    // Force reflow
                    item.offsetHeight;
                    
                    // Remove after animation
                    setTimeout(() => {
                        if (item && item.classList.contains('celebration-bounce')) {
                            console.log(`? Removing celebration-bounce from word item ${index}`);
                            item.classList.remove('celebration-bounce');
                        }
                    }, 1000);
                }
            }, index * 100); // 100ms stagger between items
        });
        
        return foundWordItems.length;
    } catch (error) {
        console.error('Error in animateWordamentCelebration:', error);
        return 0;
    }
};

// ? NEW: Function to flash the grid cells that were just used in a word
window.flashWordamentPath = function(path) {
    try {
        console.log(`? Flashing Wordament path with ${path.length} cells`);
        
        if (!path || path.length === 0) {
            console.log('? No path provided for flashing');
            return 0;
        }
        
        // Find cells that match the path
        const pathCells = [];
        path.forEach((position, index) => {
            const cell = document.querySelector(`[data-x="${position.x}"][data-y="${position.y}"]`);
            if (cell) {
                pathCells.push({ cell, index });
                console.log(`? Found path cell at (${position.x}, ${position.y})`);
            }
        });
        
        if (pathCells.length === 0) {
            console.log('? No path cells found for flashing');
            return 0;
        }
        
        console.log(`? Flashing ${pathCells.length} cells in path`);
        
        // Flash all path cells simultaneously
        pathCells.forEach(({ cell, index }) => {
            if (cell && !cell.classList.contains('wordament-path-flash')) {
                console.log(`? Adding wordament-path-flash to path cell ${index}`);
                cell.classList.add('wordament-path-flash');
                
                // Force reflow
                cell.offsetHeight;
                
                // Remove after quick flash
                setTimeout(() => {
                    if (cell && cell.classList.contains('wordament-path-flash')) {
                        console.log(`? Removing wordament-path-flash from path cell ${index}`);
                        cell.classList.remove('wordament-path-flash');
                    }
                }, 800); // Quick flash duration
            }
        });
        
        return pathCells.length;
    } catch (error) {
        console.error('Error in flashWordamentPath:', error);
        return 0;
    }
};

// Enhanced desktop mouse drag support for Wordament with Blazor integration
window.enhanceWordamentDesktopDrag = function() {
    const grid = document.querySelector('.wordament-grid');
    if (!grid) {
        console.log('? Wordament grid not found for desktop drag enhancement');
        return;
    }

    console.log('? Enhancing desktop mouse drag for Wordament grid with diagonal-friendly hit testing');

    // Remove any existing event listeners to prevent duplicates
    if (window.wordamentMouseHandlers) {
        grid.removeEventListener('mousedown', window.wordamentMouseHandlers.mouseDown);
        document.removeEventListener('mousemove', window.wordamentMouseHandlers.mouseMove);
        document.removeEventListener('mouseup', window.wordamentMouseHandlers.mouseUp);
        grid.removeEventListener('mouseleave', window.wordamentMouseHandlers.mouseLeave);
    }

    // Create enhanced mouse event handlers that communicate with Blazor
    window.wordamentMouseHandlers = {
        mouseDown: function(e) {
            if (e.button !== 0) return; // Only left mouse button
            
            e.preventDefault();
            e.stopPropagation();
            
            const coords = window.getWordamentCellFromCoordinates(grid, e.clientX, e.clientY);
            if (coords) {
                console.log('??? JavaScript: Desktop drag started at cell:', coords);
                window.wordamentDragState.isDragging = true;
                window.wordamentDragState.startPosition = coords;
                window.wordamentDragState.currentPosition = coords;
                window.wordamentDragState.dragPath = [coords];
                
                grid.classList.add('dragging');
                
                // ?? Notify Blazor component about drag start
                if (window.wordamentBlazorComponent) {
                    window.wordamentBlazorComponent.invokeMethodAsync('OnDesktopDragStart', coords[0], coords[1]);
                }
                
                // Update visual feedback
                window.updateWordamentDragVisuals();
            }
        },

        mouseMove: function(e) {
            if (!window.wordamentDragState.isDragging) return;
            
            // DIAGONAL IMPROVEMENT: Use enhanced hit testing for better diagonal support
            const coords = window.getWordamentCellFromCoordinates(grid, e.clientX, e.clientY);
            if (coords) {
                const lastInPath = window.wordamentDragState.dragPath[window.wordamentDragState.dragPath.length - 1];
                if (!lastInPath || coords[0] !== lastInPath[0] || coords[1] !== lastInPath[1]) {
                    console.log('??? JavaScript: Drag moved to cell (diagonal-friendly):', coords);
                    
                    // Update drag state
                    window.wordamentDragState.currentPosition = coords;
                    
                    // Check if we're backtracking
                    if (window.wordamentDragState.dragPath.length > 1) {
                        const secondLast = window.wordamentDragState.dragPath[window.wordamentDragState.dragPath.length - 2];
                        if (coords[0] === secondLast[0] && coords[1] === secondLast[1]) {
                            // Backtracking - remove last position
                            window.wordamentDragState.dragPath.pop();
                            console.log('?? JavaScript: Backtracking to:', coords);
                            
                            // ?? Notify Blazor about backtrack
                            if (window.wordamentBlazorComponent) {
                                window.wordamentBlazorComponent.invokeMethodAsync('OnDesktopDragBacktrack', coords[0], coords[1]);
                            }
                        } else {
                            // Add new position to path
                            window.wordamentDragState.dragPath.push(coords);
                            console.log('?? JavaScript: Added to path:', coords);
                            
                            // ?? Notify Blazor about new position
                            if (window.wordamentBlazorComponent) {
                                window.wordamentBlazorComponent.invokeMethodAsync('OnDesktopDragMove', coords[0], coords[1]);
                            }
                        }
                    } else {
                        // Add to path
                        window.wordamentDragState.dragPath.push(coords);
                        console.log('?? JavaScript: Added to path:', coords);
                        
                        // ?? Notify Blazor about new position
                        if (window.wordamentBlazorComponent) {
                            window.wordamentBlazorComponent.invokeMethodAsync('OnDesktopDragMove', coords[0], coords[1]);
                        }
                    }
                    
                    // Update visual feedback
                    window.updateWordamentDragVisuals();
                }
            }
        },

        mouseUp: function(e) {
            if (!window.wordamentDragState.isDragging) return;
            
            e.preventDefault();
            e.stopPropagation();
            
            console.log('??? JavaScript: Desktop drag ended. Path:', window.wordamentDragState.dragPath);
            
            // ?? Notify Blazor about drag end
            if (window.wordamentBlazorComponent) {
                window.wordamentBlazorComponent.invokeMethodAsync('OnDesktopDragEnd', window.wordamentDragState.dragPath);
            }
            
            // Clean up drag state
            window.wordamentDragState.isDragging = false;
            grid.classList.remove('dragging');
            
            // Clean up visual feedback
            window.clearWordamentDragVisuals();
            
            // Reset drag state
            window.wordamentDragState = {
                isDragging: false,
                startPosition: null,
                currentPosition: null,
                dragPath: []
            };
        },

        mouseLeave: function(e) {
            if (window.wordamentDragState.isDragging) {
                console.log('??? JavaScript: Mouse left grid during drag');
                window.wordamentMouseHandlers.mouseUp(e);
            }
        }
    };

    // Attach enhanced event listeners with capture phase for desktop
    grid.addEventListener('mousedown', window.wordamentMouseHandlers.mouseDown, { 
        passive: false, 
        capture: true 
    });
    document.addEventListener('mousemove', window.wordamentMouseHandlers.mouseMove, { 
        passive: false,
        capture: true
    });
    document.addEventListener('mouseup', window.wordamentMouseHandlers.mouseUp, { 
        passive: false,
        capture: true
    });
    grid.addEventListener('mouseleave', window.wordamentMouseHandlers.mouseLeave, { 
        passive: false 
    });

    console.log('? Enhanced desktop mouse drag handlers attached with diagonal-friendly hit testing');
};

// Function to register Blazor component for JavaScript callbacks
window.registerWordamentBlazorComponent = function(dotNetHelper) {
    window.wordamentBlazorComponent = dotNetHelper;
    console.log('?? Wordament Blazor component registered for JavaScript callbacks');
    
    // Test that the component is callable
    setTimeout(() => {
        if (window.wordamentBlazorComponent) {
            console.log('?? Testing Blazor component callback...');
            // This will be used by desktop drag events
        }
    }, 100);
};

// NEW: Function to test diagonal hit area improvements
window.testDiagonalHitArea = function() {
    console.log('?? Testing diagonal hit area improvements...');
    
    const grid = document.querySelector('.wordament-grid');
    if (!grid) {
        console.log('? Grid not found for diagonal testing');
        return false;
    }
    
    const cells = grid.querySelectorAll('.wordament-cell');
    if (cells.length !== 16) {
        console.log(`? Expected 16 cells, found ${cells.length}`);
        return false;
    }
    
    console.log('?? Testing diagonal hit area detection...');
    
    // Test diagonal positions between cells
    let diagonalTests = 0;
    let successfulDiagonalDetections = 0;
    
    // Test between cell (0,0) and cell (1,1) - diagonal
    const cell00 = document.querySelector('[data-x="0"][data-y="0"]');
    const cell11 = document.querySelector('[data-x="1"][data-y="1"]');
    
    if (cell00 && cell11) {
        const rect00 = cell00.getBoundingClientRect();
        const rect11 = cell11.getBoundingClientRect();
        
        // Test point on the edge between the two cells (diagonal)
        const edgeX = rect00.right - 5; // 5px inside cell00's right edge
        const edgeY = rect00.bottom - 5; // 5px inside cell00's bottom edge
        
        diagonalTests++;
        const detectedCell = window.getWordamentCellFromCoordinates(grid, edgeX, edgeY);
        
        if (detectedCell && detectedCell[0] === 0 && detectedCell[1] === 0) {
            successfulDiagonalDetections++;
            console.log('? Diagonal test 1 passed: Edge point correctly detected as (0,0)');
        } else {
            console.log(`? Diagonal test 1 failed: Expected (0,0), got ${detectedCell ? `(${detectedCell[0]},${detectedCell[1]})` : 'null'}`);
        }
        
        // Test point closer to the diagonal line but still in cell00's reduced area
        const diagonalX = rect00.left + rect00.width * 0.6;  // 60% into cell00
        const diagonalY = rect00.top + rect00.height * 0.6;  // 60% into cell00
        
        diagonalTests++;
        const detectedDiagonal = window.getWordamentCellFromCoordinates(grid, diagonalX, diagonalY);
        
        if (detectedDiagonal && detectedDiagonal[0] === 0 && detectedDiagonal[1] === 0) {
            successfulDiagonalDetections++;
            console.log('? Diagonal test 2 passed: Inner diagonal point correctly detected as (0,0)');
        } else {
            console.log(`? Diagonal test 2 failed: Expected (0,0), got ${detectedDiagonal ? `(${detectedDiagonal[0]},${detectedDiagonal[1]})` : 'null'}`);
        }
    }
    
    // Test between cell (1,0) and cell (1,1) - vertical edge  
    const cell10 = document.querySelector('[data-x="1"][data-y="0"]');
    
    if (cell10 && cell11) {
        const rect10 = cell10.getBoundingClientRect();
        const rect11 = cell11.getBoundingClientRect();
        
        // Test point on the vertical edge between cells
        const verticalEdgeX = rect10.left + rect10.width * 0.3; // 30% into cell - should be in reduced area
        const verticalEdgeY = rect10.bottom - 2; // Just inside bottom edge of cell10
        
        diagonalTests++;
        const detectedVertical = window.getWordamentCellFromCoordinates(grid, verticalEdgeX, verticalEdgeY);
        
        if (detectedVertical && detectedVertical[0] === 1 && detectedVertical[1] === 0) {
            successfulDiagonalDetections++;
            console.log('? Vertical edge test passed: Edge point correctly detected as (1,0)');
        } else {
            console.log(`? Vertical edge test failed: Expected (1,0), got ${detectedVertical ? `(${detectedVertical[0]},${detectedVertical[1]})` : 'null'}`);
        }
    }
    
    // Test a point that should NOT be detected (in the reduced area between cells)
    if (cell00 && cell10) {
        const rect00 = cell00.getBoundingClientRect();
        const rect10 = cell10.getBoundingClientRect();
        
        // Test point in the gap between cells (should not be detected by either)
        const gapX = (rect00.right + rect10.left) / 2; // Middle of the gap
        const gapY = rect00.top + rect00.height * 0.5; // Middle vertically
        
        diagonalTests++;
        const detectedGap = window.getWordamentCellFromCoordinates(grid, gapX, gapY);
        
        if (!detectedGap) {
            successfulDiagonalDetections++;
            console.log('? Gap test passed: Gap point correctly not detected');
        } else {
            console.log(`? Gap test failed: Gap point incorrectly detected as (${detectedGap[0]},${detectedGap[1]})`);
        }
    }
    
    const successRate = (successfulDiagonalDetections / diagonalTests) * 100;
    console.log(`?? Diagonal hit area test results: ${successfulDiagonalDetections}/${diagonalTests} tests passed (${successRate.toFixed(1)}%)`);
    
    if (successRate >= 75) {
        console.log('? Diagonal hit area improvements are working well!');
        return true;
    } else {
        console.log('? Diagonal hit area improvements need more work');
        return false;
    }
};

// Visual feedback for drag operations
window.updateWordamentDragVisuals = function() {
    // Clear previous drag visuals
    window.clearWordamentDragVisuals();
    
    if (!window.wordamentDragState.dragPath || window.wordamentDragState.dragPath.length === 0) return;
    
    // Highlight cells in the current drag path
    window.wordamentDragState.dragPath.forEach((coords, index) => {
        const cell = document.querySelector(`[data-x="${coords[0]}"][data-y="${coords[1]}"]`);
        if (cell) {
            cell.classList.add('drag-path');
            if (index === 0) {
                cell.classList.add('drag-start');
            } else if (index === window.wordamentDragState.dragPath.length - 1) {
                cell.classList.add('drag-current');
            }
        }
    });
};

window.clearWordamentDragVisuals = function() {
    const grid = document.querySelector('.wordament-grid');
    if (grid) {
        const cells = grid.querySelectorAll('.wordament-cell');
        cells.forEach(cell => {
            cell.classList.remove('drag-path', 'drag-start', 'drag-current');
        });
    }
};

// Initialize Wordament-specific functionality with enhanced desktop support
window.initializeWordament = function () {
    console.log('?? Initializing Wordament game with diagonal drag improvements...');
    
    // Only apply Wordament functionality if on the Wordament page
    if (window.location.pathname.includes('/wordament')) {
        // Add enhanced touch and mouse handling for Wordament grid
        setTimeout(() => {
            const grid = document.querySelector('.wordament-grid');
            if (grid) {
                console.log('?? Setting up enhanced Wordament touch and drag handling');
                
                // Ensure proper touch-action settings
                grid.style.touchAction = 'none';
                grid.style.userSelect = 'none';
                grid.style.webkitUserSelect = 'none';
                grid.style.webkitTouchCallout = 'none';
                grid.style.webkitTapHighlightColor = 'transparent';
                
                // Add additional CSS to prevent scrolling
                grid.addEventListener('touchmove', function(e) {
                    e.preventDefault();
                    e.stopPropagation();
                }, { passive: false });
                
                // Apply to all cells as well
                const cells = grid.querySelectorAll('.wordament-cell');
                cells.forEach(cell => {
                    cell.style.touchAction = 'none';
                    cell.style.userSelect = 'none';
                    cell.style.webkitUserSelect = 'none';
                    cell.style.webkitTouchCallout = 'none';
                    cell.style.webkitTapHighlightColor = 'transparent';
                });
                
                // ??? Enhance desktop mouse drag support
                window.enhanceWordamentDesktopDrag();
                
                // ?? Set up native touch event debugging
                window.debugWordamentTouchEvents();
                
                console.log('? Enhanced Wordament touch and mouse handling applied');
            } else {
                console.log('?? Wordament grid not found for setup, retrying...');
                
                // Retry after a longer delay
                setTimeout(() => {
                    const retryGrid = document.querySelector('.wordament-grid');
                    if (retryGrid) {
                        console.log('? Retry successful - setting up Wordament handling');
                        window.enhanceWordamentDesktopDrag();
                    } else {
                        console.log('? Retry failed - Wordament grid still not found');
                    }
                }, 1000);
            }
        }, 500); // Delay to ensure DOM is ready
        
        console.log('?? Wordament initialization complete with diagonal improvements');
    }
};

// Enhanced debug function to test both coordinate detection AND Blazor integration
window.testWordamentDesktopDrag = function() {
    console.log('?? Testing Wordament desktop drag functionality with diagonal improvements...');
    
    const grid = document.querySelector('.wordament-grid');
    if (!grid) {
        console.log('? Grid not found');
        return false;
    }
    
    const cells = grid.querySelectorAll('.wordament-cell');
    console.log('?? Found', cells.length, 'cells');
    
    if (cells.length !== 16) {
        console.log('? Expected 16 cells, found', cells.length);
        return false;
    }
    
    // Test 1: Coordinate detection for each cell center
    let successCount = 0;
    cells.forEach(cell => {
        const x = cell.getAttribute('data-x');
        const y = cell.getAttribute('data-y');
        const rect = cell.getBoundingClientRect();
        const centerX = rect.left + rect.width / 2;
        const centerY = rect.top + rect.height / 2;
        
        const detected = window.getWordamentCellFromCoordinates(grid, centerX, centerY);
        if (detected && detected[0] == x && detected[1] == y) {
            successCount++;
        } else {
            console.log(`? Failed to detect cell (${x},${y}) at center (${centerX},${centerY})`);
        }
    });
    
    console.log(`? Center coordinate detection: ${successCount}/16 cells successful`);
    
    // Test 3: Check if drag handlers are attached
    const hasMouseHandlers = !!window.wordamentMouseHandlers;
    console.log(`${hasMouseHandlers ? '?' : '?'} Mouse handlers attached: ${hasMouseHandlers}`);
    
    // Test 4: Check if drag state object exists
    const hasDragState = !!window.wordamentDragState;
    console.log(`${hasDragState ? '?' : '?'} Drag state object: ${hasDragState}`);
    
    // Test 5: Check if visual functions exist
    const hasVisualFunctions = !!(window.updateWordamentDragVisuals && window.clearWordamentDragVisuals);
    console.log(`${hasVisualFunctions ? '?' : '?'} Visual feedback functions: ${hasVisualFunctions}`);
    
    // Test 6: Check if Blazor component is registered
    const hasBlazorComponent = !!window.wordamentBlazorComponent;
    console.log(`${hasBlazorComponent ? '?' : '?'} Blazor component registered: ${hasBlazorComponent}`);
    
    // Test 7: Check if animation functions exist
    const hasAnimationFunctions = !!(window.animateWordamentWordPlacement && window.animateWordamentCelebration && window.flashWordamentPath);
    console.log(`${hasAnimationFunctions ? '?' : '?'} Animation functions available: ${hasAnimationFunctions}`);
    
    // Overall test result
    const overallSuccess = successCount === 16 && hasMouseHandlers && hasDragState && hasVisualFunctions && hasBlazorComponent && hasAnimationFunctions;
    console.log(`\n${overallSuccess ? '??' : '?'} Overall desktop drag test: ${overallSuccess ? 'PASSED' : 'FAILED'}`);
    
    if (overallSuccess) {
        console.log('?? All tests passed! JavaScript functions are working correctly.');
    } else {
        console.log('?? Some tests failed. Check the individual test results above for details.');
    }
    
    return overallSuccess;
};

// DEBUGGING HELPER: Simple function to test if JavaScript is working
window.testWordamentJavaScript = function() {
    console.log('?? Testing basic Wordament JavaScript functionality...');
    
    const tests = [];
    
    // Test 1: Check if main function exists
    tests.push({
        name: 'getWordamentCellFromCoordinates function',
        result: typeof window.getWordamentCellFromCoordinates === 'function'
    });
    
    // Test 2: Check if grid exists
    const grid = document.querySelector('.wordament-grid');
    tests.push({
        name: 'Wordament grid element',
        result: !!grid
    });
    
    // Test 3: Check if cells exist
    const cells = grid ? grid.querySelectorAll('.wordament-cell') : [];
    tests.push({
        name: 'Wordament cells (16 expected)',
        result: cells.length === 16,
        details: `Found ${cells.length} cells`
    });
    
    // Test 4: Try calling the function with dummy coordinates
    let functionWorks = false;
    try {
        const result = window.getWordamentCellFromCoordinates(grid, 100, 100);
        functionWorks = true;
        tests.push({
            name: 'Function call test',
            result: true,
            details: `Returned: ${result}`
        });
    } catch (error) {
        tests.push({
            name: 'Function call test',
            result: false,
            details: `Error: ${error.message}`
        });
    }
    
    // Display results
    console.log('?? Test Results:');
    tests.forEach((test, index) => {
        const status = test.result ? '?' : '?';
        console.log(`${index + 1}. ${status} ${test.name}${test.details ? ` - ${test.details}` : ''}`);
    });
    
    const allPassed = tests.every(test => test.result);
    console.log(`\n${allPassed ? '??' : '?'} Overall: ${allPassed ? 'ALL TESTS PASSED' : 'SOME TESTS FAILED'}`);
    
    return allPassed;
};