// Wordament Game JavaScript Functions - Enhanced for Desktop Drag Support

// Global state tracking for desktop mouse drag
window.wordamentDragState = {
    isDragging: false,
    startPosition: null,
    currentPosition: null,
    dragPath: []
};

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

        console.log('? Found cell coordinates:', [x, y]);
        return [x, y];
    } catch (error) {
        console.error('? Error getting Wordament cell from coordinates:', error);
        return null;
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
                            debugger
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

// Add desktop-specific CSS for better drag feedback
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
        
        @keyframes dragPulse {
            0%, 100% { transform: scale(1); }
            50% { transform: scale(1.05); }
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
        console.log('? Desktop-specific styles added');
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
    
    // Test 6: Simulate a quick drag test
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
                preventDefault: () => {}
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
    const overallSuccess = successCount === 16 && hasMouseHandlers && hasDragState && hasVisualFunctions && hasBlazorComponent && dragTestPassed;
    console.log(`${overallSuccess ? '?' : '?'} Overall desktop drag test: ${overallSuccess ? 'PASSED' : 'FAILED'}`);
    
    return overallSuccess;
};

// Auto-initialize if on Wordament page
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', window.initializeWordament);
} else {
    window.initializeWordament();
}