// Wordament Game JavaScript Functions - Enhanced for Desktop Drag Support

// Global state tracking for desktop mouse drag
window.wordamentDragState = {
    isDragging: false,
    startPosition: null,
    currentPosition: null,
    dragPath: []
};

// CRITICAL TEST: Add simple console logging to verify JavaScript is working
console.log('??? Wordament JavaScript file loaded at:', new Date().toLocaleTimeString());

// CRITICAL TEST: Add immediate touch debugging when file loads
window.addEventListener('DOMContentLoaded', function() {
    console.log('??? DOM Content Loaded - Setting up immediate touch debugging');
    
    // Wait for page to fully load, then set up debugging
    setTimeout(() => {
        const grid = document.querySelector('.wordament-grid');
        if (grid) {
            console.log('??? Found wordament grid, adding immediate touch debugging');
            
            grid.addEventListener('touchstart', function(e) {
                console.log('? IMMEDIATE touchstart detected on grid!');
            }, { passive: false });
            
            grid.addEventListener('touchmove', function(e) {
                console.log('? IMMEDIATE touchmove detected on grid!');
            }, { passive: false });
            
            grid.addEventListener('touchend', function(e) {
                console.log('? IMMEDIATE touchend detected on grid!');
            }, { passive: false });
        } else {
            console.log('? No wordament grid found for immediate debugging');
        }
    }, 1000);
});

// Enhanced function to get Wordament cell from coordinates with better desktop support
window.getWordamentCellFromCoordinates = function (gridElement, clientX, clientY) {
    try {
        console.log('?? getWordamentCellFromCoordinates called with:', { clientX, clientY });
        
        // Use elementFromPoint to find the cell under the coordinates
        const elementUnderPoint = document.elementFromPoint(clientX, clientY);
        console.log('Element under point:', elementUnderPoint ? elementUnderPoint.className : 'null');
        
        if (!elementUnderPoint) {
            console.log('No element found under point');
            return null;
        }

        // Find the closest wordament-cell element
        let cellElement = elementUnderPoint.closest('.wordament-cell');
        if (!cellElement) {
            console.log('No wordament-cell found under point, trying parent elements...');
            
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
            console.log('Still no wordament-cell found under point, element was:', elementUnderPoint.className);
            return null;
        }

        // Get the data attributes for x,y coordinates
        const x = parseInt(cellElement.getAttribute('data-x'));
        const y = parseInt(cellElement.getAttribute('data-y'));

        if (isNaN(x) || isNaN(y)) {
            console.warn('Invalid cell coordinates found:', x, y);
            return null;
        }

        console.log('?? Found cell coordinates:', [x, y]);
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
        console.log('??? NATIVE touchstart detected:', {
            touches: e.touches.length,
            changedTouches: e.changedTouches.length,
            target: e.target.className,
            coords: e.touches[0] ? `(${e.touches[0].clientX}, ${e.touches[0].clientY})` : 'none'
        });
    }, { passive: false });
    
    grid.addEventListener('touchmove', function(e) {
        if (touchStarted) {
            touchCount++;
            console.log(`??? NATIVE touchmove #${touchCount} detected:`, {
                touches: e.touches.length,
                changedTouches: e.changedTouches.length,
                target: e.target.className,
                coords: e.changedTouches[0] ? `(${e.changedTouches[0].clientX}, ${e.changedTouches[0].clientY})` : 'none'
            });
            
            // Test coordinate detection and direct Blazor call
            if (e.changedTouches[0]) {
                const coords = window.getWordamentCellFromCoordinates(grid, e.changedTouches[0].clientX, e.changedTouches[0].clientY);
                console.log('??? Detected cell:', coords);
                
                // CRITICAL TEST: Try calling Blazor directly from JavaScript
                if (coords && window.wordamentBlazorComponent) {
                    try {
                        window.wordamentBlazorComponent.invokeMethodAsync('TestTouchMoveFromJS', coords[0], coords[1]);
                        console.log(`??? Called Blazor TestTouchMoveFromJS with (${coords[0]}, ${coords[1]})`);
                    } catch (blazorError) {
                        console.error('??? Error calling Blazor from native JavaScript:', blazorError);
                    }
                }
            }
        }
    }, { passive: false });
    
    grid.addEventListener('touchend', function(e) {
        console.log(`??? NATIVE touchend detected after ${touchCount} move events`);
        touchStarted = false;
        touchCount = 0;
    }, { passive: false });
    
    console.log('? Native touch debugging set up complete');
};

// ? NEW: Function to animate word placement in the grid - shows where word was placed
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

// ? NEW: Function to animate all found words for celebration
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

    console.log('??? Enhancing desktop mouse drag for Wordament grid');

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
                
                // ? Notify Blazor component about drag start
                if (window.wordamentBlazorComponent) {
                    window.wordamentBlazorComponent.invokeMethodAsync('OnDesktopDragStart', coords[0], coords[1]);
                }
                
                // Update visual feedback
                window.updateWordamentDragVisuals();
            }
        },

        mouseMove: function(e) {
            if (!window.wordamentDragState.isDragging) return;
            
            const coords = window.getWordamentCellFromCoordinates(grid, e.clientX, e.clientY);
            if (coords) {
                const lastInPath = window.wordamentDragState.dragPath[window.wordamentDragState.dragPath.length - 1];
                if (!lastInPath || coords[0] !== lastInPath[0] || coords[1] !== lastInPath[1]) {
                    console.log('??? JavaScript: Drag moved to cell:', coords);
                    
                    // Update drag state
                    window.wordamentDragState.currentPosition = coords;
                    
                    // Check if we're backtracking
                    if (window.wordamentDragState.dragPath.length > 1) {
                        const secondLast = window.wordamentDragState.dragPath[window.wordamentDragState.dragPath.length - 2];
                        if (coords[0] === secondLast[0] && coords[1] === secondLast[1]) {
                            // Backtracking - remove last position
                            window.wordamentDragState.dragPath.pop();
                            console.log('?? JavaScript: Backtracking to:', coords);
                            
                            // ? Notify Blazor about backtrack
                            if (window.wordamentBlazorComponent) {
                                window.wordamentBlazorComponent.invokeMethodAsync('OnDesktopDragBacktrack', coords[0], coords[1]);
                            }
                        } else {
                            // Add new position to path
                            window.wordamentDragState.dragPath.push(coords);
                            console.log('?? JavaScript: Added to path:', coords);
                            
                            // ? Notify Blazor about new position
                            if (window.wordamentBlazorComponent) {
                                window.wordamentBlazorComponent.invokeMethodAsync('OnDesktopDragMove', coords[0], coords[1]);
                            }
                        }
                    } else {
                        // Add to path
                        window.wordamentDragState.dragPath.push(coords);
                        console.log('?? JavaScript: Added to path:', coords);
                        
                        // ? Notify Blazor about new position
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
            
            // ? Notify Blazor about drag end
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

    console.log('? Enhanced desktop mouse drag handlers attached with Blazor integration');
};

// Function to register Blazor component for JavaScript callbacks
window.registerWordamentBlazorComponent = function(dotNetHelper) {
    window.wordamentBlazorComponent = dotNetHelper;
    console.log('? Wordament Blazor component registered for JavaScript callbacks');
    
    // Test that the component is callable
    setTimeout(() => {
        if (window.wordamentBlazorComponent) {
            console.log('?? Testing Blazor component callback...');
            // This will be used by desktop drag events
        }
    }, 100);
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

// Add desktop-specific CSS for better drag feedback and animations
window.addWordamentDesktopStyles = function() {
    const style = document.createElement('style');
    style.textContent = `
        .wordament-grid {
            cursor: grab;
        }
        
        .wordament-grid.dragging {
            cursor: grabbing !important;
        }
        
        .wordament-grid.dragging .wordament-cell {
            cursor: grabbing !important;
        }
        
        .wordament-cell {
            transition: transform 0.1s ease, box-shadow 0.1s ease;
        }
        
        .wordament-cell:hover {
            transform: translateY(-1px);
            box-shadow: 0 4px 12px rgba(0,0,0,0.15);
        }
        
        /* Enhanced drag visual feedback */
        .wordament-cell.drag-path {
            background: #fff3cd !important;
            border: 2px solid #ffc107 !important;
            box-shadow: 0 4px 12px rgba(255, 193, 7, 0.3) !important;
            z-index: 10;
        }
        
        .wordament-cell.drag-start {
            background: #d4edda !important;
            border: 2px solid #28a745 !important;
            box-shadow: 0 4px 16px rgba(40, 167, 69, 0.4) !important;
        }
        
        .wordament-cell.drag-current {
            background: #cce5ff !important;
            border: 2px solid #007bff !important;
            box-shadow: 0 4px 16px rgba(0, 123, 255, 0.4) !important;
            animation: dragPulse 1s infinite;
        }
        
        /* ? NEW: Word placement animation */
        .wordament-cell.wordament-word-reveal {
            background: #90EE90 !important;
            color: #000 !important;
            border: 3px solid #32CD32 !important;
            box-shadow: 0 0 20px rgba(50, 205, 50, 0.6) !important;
            animation: wordamentWordReveal 1.5s ease-in-out;
            z-index: 15 !important;
        }
        
        @keyframes wordamentWordReveal {
            0% { 
                transform: scale(1); 
                background: #90EE90; 
                box-shadow: 0 0 5px rgba(50, 205, 50, 0.3);
            }
            25% { 
                transform: scale(1.15); 
                background: #98FB98; 
                box-shadow: 0 0 15px rgba(50, 205, 50, 0.6);
            }
            50% { 
                transform: scale(1.25); 
                background: #ADFF2F; 
                box-shadow: 0 0 25px rgba(173, 255, 47, 0.8);
            }
            75% { 
                transform: scale(1.15); 
                background: #98FB98; 
                box-shadow: 0 0 15px rgba(50, 205, 50, 0.6);
            }
            100% { 
                transform: scale(1); 
                background: #90EE90; 
                box-shadow: 0 0 5px rgba(50, 205, 50, 0.3);
            }
        }
        
        /* ? NEW: Path flash animation for quick feedback */
        .wordament-cell.wordament-path-flash {
            background: #FFD700 !important;
            color: #000 !important;
            border: 2px solid #FFA500 !important;
            box-shadow: 0 0 15px rgba(255, 215, 0, 0.7) !important;
            animation: wordamentPathFlash 0.8s ease-out;
            z-index: 12 !important;
        }
        
        @keyframes wordamentPathFlash {
            0% { 
                background: #FFD700; 
                transform: scale(1);
                box-shadow: 0 0 5px rgba(255, 215, 0, 0.4);
            }
            50% { 
                background: #FFA500; 
                transform: scale(1.1);
                box-shadow: 0 0 20px rgba(255, 165, 0, 0.8);
            }
            100% { 
                background: #FFD700; 
                transform: scale(1);
                box-shadow: 0 0 5px rgba(255, 215, 0, 0.4);
            }
        }
        
        /* ? NEW: Found word celebration animation */
        .found-word-item.celebration-bounce {
            animation: wordamentCelebrationBounce 1s ease-in-out;
        }
        
        @keyframes wordamentCelebrationBounce {
            0%, 100% { 
                transform: scale(1) translateY(0); 
                background: inherit;
            }
            25% { 
                transform: scale(1.05) translateY(-5px); 
                background: #e8f5e8;
            }
            50% { 
                transform: scale(1.1) translateY(-10px); 
                background: #d4edda;
                box-shadow: 0 5px 15px rgba(40, 167, 69, 0.3);
            }
            75% { 
                transform: scale(1.05) translateY(-5px); 
                background: #e8f5e8;
            }
        }
        
        @keyframes dragPulse {
            0%, 100% { transform: scale(1.05); }
            50% { transform: scale(1.1); }
        }
        
        @media (pointer: fine) {
            /* Desktop-specific enhancements */
            .wordament-cell {
                cursor: pointer;
            }
            
            .wordament-cell:active {
                transform: scale(0.95);
            }
        }
    `;
    
    if (!document.querySelector('#wordament-desktop-styles')) {
        style.id = 'wordament-desktop-styles';
        document.head.appendChild(style);
        console.log('?? Desktop-specific styles and animations added');
    }
};

// Initialize Wordament-specific functionality with enhanced desktop support
window.initializeWordament = function () {
    console.log('?? Initializing Wordament game...');
    
    // Only apply Wordament functionality if on the Wordament page
    if (window.location.pathname.includes('/wordament')) {
        // Add desktop-specific styles first
        window.addWordamentDesktopStyles();
        
        // Add enhanced touch and mouse handling for Wordament grid
        setTimeout(() => {
            const grid = document.querySelector('.wordament-grid');
            if (grid) {
                console.log('?? Setting up enhanced Wordament touch handling');
                
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
                
                // CRITICAL DEBUG: Set up native touch event debugging
                window.debugWordamentTouchEvents();
                
                console.log('? Enhanced Wordament touch and mouse handling applied');
            } else {
                console.log('?? Wordament grid not found for touch handling setup, retrying...');
                
                // Retry after a longer delay
                setTimeout(() => {
                    const retryGrid = document.querySelector('.wordament-grid');
                    if (retryGrid) {
                        console.log('?? Retry successful - setting up Wordament handling');
                        window.enhanceWordamentDesktopDrag();
                        window.addWordamentDesktopStyles();
                    } else {
                        console.log('? Retry failed - Wordament grid still not found');
                    }
                }, 1000);
            }
        }, 500); // Delay to ensure DOM is ready
        
        console.log('? Wordament initialization complete');
    }
};

// Enhanced debug function to test both coordinate detection AND Blazor integration
window.testWordamentDesktopDrag = function() {
    console.log('?? Testing Wordament desktop drag functionality');
    
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
    
    // Test 1: Coordinate detection for each cell
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
    
    console.log(`? Coordinate detection: ${successCount}/16 cells successful`);
    
    // Test 2: Check if drag handlers are attached
    const hasMouseHandlers = !!window.wordamentMouseHandlers;
    console.log(`? Mouse handlers attached: ${hasMouseHandlers}`);
    
    // Test 3: Check if drag state object exists
    const hasDragState = !!window.wordamentDragState;
    console.log(`? Drag state object: ${hasDragState}`);
    
    // Test 4: Check if visual functions exist
    const hasVisualFunctions = !!(window.updateWordamentDragVisuals && window.clearWordamentDragVisuals);
    console.log(`? Visual feedback functions: ${hasVisualFunctions}`);
    
    // Test 5: Check if Blazor component is registered
    const hasBlazorComponent = !!window.wordamentBlazorComponent;
    console.log(`? Blazor component registered: ${hasBlazorComponent}`);
    
    // Test 6: Check if animation functions exist
    const hasAnimationFunctions = !!(window.animateWordamentWordPlacement && window.animateWordamentCelebration && window.flashWordamentPath);
    console.log(`? Animation functions available: ${hasAnimationFunctions}`);
    
    // Test 7: Simulate a quick drag test
    let dragTestPassed = false;
    try {
        if (hasMouseHandlers && hasDragState) {
            // Reset drag state
            window.wordamentDragState = {
                isDragging: false,
                startPosition: null,
                currentPosition: null,
                dragPath: []
            };
            
            // Simulate drag start
            const firstCell = cells[0];
            const rect = firstCell.getBoundingClientRect();
            const mockEvent = {
                button: 0,
                clientX: rect.left + rect.width / 2,
                clientY: rect.top + rect.height / 2,
                preventDefault: () => {},
                stopPropagation: () => {}
            };
            
            window.wordamentMouseHandlers.mouseDown(mockEvent);
            
            if (window.wordamentDragState.isDragging && window.wordamentDragState.dragPath.length === 1) {
                dragTestPassed = true;
                console.log('? Drag simulation test passed');
                
                // Clean up
                window.wordamentMouseHandlers.mouseUp(mockEvent);
            } else {
                console.log('? Drag simulation test failed');
            }
        }
    } catch (error) {
        console.log('? Drag simulation error:', error);
    }
    
    // Overall test result
    const overallSuccess = successCount === 16 && hasMouseHandlers && hasDragState && hasVisualFunctions && hasBlazorComponent && hasAnimationFunctions && dragTestPassed;
    console.log(`${overallSuccess ? '?' : '?'} Overall desktop drag test: ${overallSuccess ? 'PASSED' : 'FAILED'}`);
    
    return overallSuccess;
};

// Debug function to test icon fallback display and animation functionality
window.debugWordamentIssues = function() {
    console.log('?? DEBUGGING WORDAMENT ISSUES');
    
    const issues = [];
    
    // === 1. BUTTON ICON FALLBACK DEBUGGING ===
    console.log('?? Checking button icon fallbacks...');
    
    const buttons = document.querySelectorAll('.control-button');
    console.log(`?? Found ${buttons.length} control buttons`);
    
    buttons.forEach((button, index) => {
        const iconSpan = button.querySelector('.icon-fallback');
        const textSpan = button.querySelector('.text-fallback');
        
        if (!iconSpan) {
            issues.push(`Button ${index}: Missing .icon-fallback span`);
        } else {
            const iconContent = iconSpan.textContent || iconSpan.innerText || '';
            const iconDisplay = window.getComputedStyle(iconSpan).display;
            const iconOpacity = window.getComputedStyle(iconSpan).opacity;
            const iconFontFamily = window.getComputedStyle(iconSpan).fontFamily;
            
            console.log(`Button ${index} icon:`, {
                content: iconContent,
                display: iconDisplay,
                opacity: iconOpacity,
                fontFamily: iconFontFamily
            });
            
            if (iconContent === '' || iconContent === '??') {
                issues.push(`Button ${index}: Icon displays as '??'`);
            }
        }
        
        if (!textSpan) {
            issues.push(`Button ${index}: Missing .text-fallback span`);
        } else {
            const textContent = textSpan.textContent || textSpan.innerText || '';
            const textDisplay = window.getComputedStyle(textSpan).display;
            const textOpacity = window.getComputedStyle(textSpan).opacity;
            
            console.log(`Button ${index} text:`, {
                content: textContent,
                display: textDisplay,
                opacity: textOpacity
            });
        }
    });
    
    // === 2. EMOJI SUPPORT DETECTION ===
    console.log('?? Testing emoji support...');
    
    const emojiTest = document.createElement('span');
    emojiTest.innerHTML = '??';
    emojiTest.style.fontFamily = "'Apple Color Emoji', 'Segoe UI Emoji', 'Segoe UI Symbol', 'Noto Color Emoji', sans-serif";
    document.body.appendChild(emojiTest);
    
    const emojiWidth = emojiTest.getBoundingClientRect().width;
    document.body.removeChild(emojiTest);
    
    console.log(`?? Emoji width test: ${emojiWidth}px`);
    if (emojiWidth < 10) {
        issues.push('System likely has poor emoji support');
    }
    
    // === 3. ANIMATION FUNCTIONALITY TESTING ===
    console.log('?? Testing animation functions...');
    
    const animationFunctions = [
        'animateWordamentWordPlacement',
        'flashWordamentPath', 
        'animateWordamentCelebration'
    ];
    
    animationFunctions.forEach(funcName => {
        if (typeof window[funcName] === 'function') {
            console.log(`? ${funcName} function exists`);
        } else {
            issues.push(`Missing animation function: ${funcName}`);
        }
    });
    
    // === 4. GRID CELL TESTING ===
    console.log('?? Testing grid cells...');
    
    const gridCells = document.querySelectorAll('.wordament-cell');
    console.log(`?? Found ${gridCells.length} Wordament cells`);
    
    if (gridCells.length === 0) {
        issues.push('No Wordament grid cells found');
    } else {
        let cellsWithDataAttributes = 0;
        gridCells.forEach(cell => {
            if (cell.hasAttribute('data-x') && cell.hasAttribute('data-y')) {
                cellsWithDataAttributes++;
            }
        });
        
        console.log(`?? Cells with data attributes: ${cellsWithDataAttributes}/${gridCells.length}`);
        
        if (cellsWithDataAttributes !== gridCells.length) {
            issues.push(`${gridCells.length - cellsWithDataAttributes} cells missing data-x/data-y attributes`);
        }
    }
    
    // === 5. CSS ANIMATION CLASSES ===
    console.log('?? Testing CSS animation classes...');
    
    const requiredAnimationClasses = [
        'wordament-word-reveal',
        'wordament-path-flash',
        'celebration-bounce'
    ];
    
    // Test if CSS animations are properly defined
    const testDiv = document.createElement('div');
    document.body.appendChild(testDiv);
    
    requiredAnimationClasses.forEach(className => {
        testDiv.className = className;
        const animationName = window.getComputedStyle(testDiv).animationName;
        if (animationName === 'none') {
            issues.push(`CSS animation class '${className}' has no animation defined`);
        } else {
            console.log(`? Animation class '${className}' has animation: ${animationName}`);
        }
    });
    
    document.body.removeChild(testDiv);
    
    // === SUMMARY ===
    console.log('\n?? ISSUE SUMMARY:');
    if (issues.length === 0) {
        console.log('? No issues detected!');
        return 'NO_ISSUES';
    } else {
        console.log(`? Found ${issues.length} issues:`);
        issues.forEach((issue, index) => {
            console.log(`${index + 1}. ${issue}`);
        });
        return issues;
    }
};

// Function to fix icon display issues dynamically
window.fixWordamentIconDisplay = function() {
    console.log('?? Attempting to fix icon display issues...');
    
    const buttons = document.querySelectorAll('.control-button');
    let fixedCount = 0;
    
    buttons.forEach((button, index) => {
        const iconSpan = button.querySelector('.icon-fallback');
        const textSpan = button.querySelector('.text-fallback');
        
        if (iconSpan) {
            const iconContent = iconSpan.textContent || iconSpan.innerText || '';
            
            if (iconContent === '' || iconContent === '??' || iconContent.includes('?')) {
                console.log(`?? Fixing icon in button ${index}`);
                
                // Force text fallback
                if (iconSpan) {
                    iconSpan.style.display = 'none';
                    iconSpan.style.opacity = '0';
                }
                if (textSpan) {
                    textSpan.style.display = 'inline';
                    textSpan.style.opacity = '1';
                    textSpan.style.position = 'relative';
                }
                
                fixedCount++;
            }
        }
    });
    
    console.log(`?? Fixed ${fixedCount} button icons`);
    return fixedCount;
};

// Function to test animation on actual grid
window.testWordamentAnimations = function() {
    console.log('?? Testing Wordament animations...');
    
    const gridCells = document.querySelectorAll('.wordament-cell');
    if (gridCells.length === 0) {
        console.log('? No grid cells found for animation test');
        return false;
    }
    
    // Test word placement animation
    const testPath = [
        { x: 0, y: 0 },
        { x: 1, y: 0 },
        { x: 2, y: 0 }
    ];
    
    console.log('?? Testing word placement animation...');
    if (typeof window.animateWordamentWordPlacement === 'function') {
        try {
            const result = window.animateWordamentWordPlacement('TEST', testPath);
            console.log(`? Word placement animation result: ${result}`);
        } catch (error) {
            console.log(`? Word placement animation error: ${error}`);
            return false;
        }
    }
    
    // Test path flash animation
    setTimeout(() => {
        console.log('?? Testing path flash animation...');
        if (typeof window.flashWordamentPath === 'function') {
            try {
                const result = window.flashWordamentPath(testPath);
                console.log(`? Path flash animation result: ${result}`);
            } catch (error) {
                console.log(`? Path flash animation error: ${error}`);
            }
        }
    }, 2000);
    
    return true;
};