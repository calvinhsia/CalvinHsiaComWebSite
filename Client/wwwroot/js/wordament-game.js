// Wordament Game JavaScript Functions - Enhanced for Desktop Drag Support with Conditional Debug Logging

// Global debug state - can be controlled by C#
window.wordamentDebug = {
    enabled: false,
    touchEnabled: false
};

// Debug logging functions - only log when debug is enabled
function debugLog(message, ...args) {
    if (window.wordamentDebug.enabled) {
        console.log(`[WordamentDebug] ${message}`, ...args);
    }
}

function debugLogTouch(message, ...args) {
    if (window.wordamentDebug.enabled && window.wordamentDebug.touchEnabled) {
        console.log(`[WordamentTouch] ${message}`, ...args);
    }
}

function debugError(message, ...args) {
    // Always log errors regardless of debug mode
    console.error(`[WordamentError] ${message}`, ...args);
}

function debugWarn(message, ...args) {
    // Always log warnings regardless of debug mode
    console.warn(`[WordamentWarn] ${message}`, ...args);
}

// Function to set debug mode from C#
window.setWordamentDebugMode = function(enabled, touchEnabled = true) {
    window.wordamentDebug.enabled = enabled;
    window.wordamentDebug.touchEnabled = touchEnabled;
    
    if (enabled) {
        console.log('[Wordament] Debug mode enabled - JavaScript will now log debug information');
    } else {
        console.log('[Wordament] Debug mode disabled - JavaScript logging reduced');
    }
    
    return window.wordamentDebug;
};

// Global state tracking for desktop mouse drag
window.wordamentDragState = {
    isDragging: false,
    startPosition: null,
    currentPosition: null,
    dragPath: []
};

// Basic initialization log - always shown
console.log('[Wordament] JavaScript file loaded at:', new Date().toLocaleTimeString());

// Debug check - only log details if debug is enabled
setTimeout(() => {
    debugLog('Checking if getWordamentCellFromCoordinates is available...');
    if (typeof window.getWordamentCellFromCoordinates === 'function') {
        debugLog('getWordamentCellFromCoordinates function is available');
    } else {
        debugError('getWordamentCellFromCoordinates function is NOT available');
        debugLog('Available window functions:', Object.keys(window).filter(key => key.includes('wordament')));
    }
}, 1000);

// Enhanced function to get Wordament cell from coordinates with DIAGONAL-FRIENDLY hit test area
window.getWordamentCellFromCoordinates = function (gridElement, clientX, clientY) {
    try {
        debugLog('getWordamentCellFromCoordinates called with:', { clientX, clientY });
        
        // CRITICAL FALLBACK: If gridElement is null or undefined, try to find it
        if (!gridElement) {
            debugLog('No gridElement provided, trying to find .wordament-grid');
            gridElement = document.querySelector('.wordament-grid');
            if (!gridElement) {
                debugError('Could not find .wordament-grid element');
                return null;
            }
        }
        
        // First, use elementFromPoint to find what element is under the coordinates
        debugLog('Trying elementFromPoint method...');
        const elementUnderPoint = document.elementFromPoint(clientX, clientY);
        
        if (!elementUnderPoint) {
            debugLog('No element found under point');
            return null;
        }

        // Find the closest wordament-cell element
        let cellElement = elementUnderPoint.closest('.wordament-cell');
        if (!cellElement) {
            debugLog('No wordament-cell found, trying parent elements...');
            
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
            debugLog('Still no wordament-cell found under point, element was:', elementUnderPoint.className);
            return null;
        }

        // Get the data attributes for x,y coordinates
        const x = parseInt(cellElement.getAttribute('data-x'));
        const y = parseInt(cellElement.getAttribute('data-y'));

        if (isNaN(x) || isNaN(y)) {
            debugWarn('Invalid cell coordinates found:', x, y);
            return null;
        }

        // DIAGONAL IMPROVEMENT: Check if the coordinates fall within the reduced hit-test area
        // This is inspired by the VB.NET implementation line 282 that makes the mouse move area smaller
        const rect = cellElement.getBoundingClientRect();
        
        // Calculate relative position within the cell (0.0 to 1.0)
        const relativeX = (clientX - rect.left) / rect.width;
        const relativeY = (clientY - rect.top) / rect.height;
        
        // IMPROVED: Better touch detection - check for ongoing touch events or recent touch
        const isTouchEvent = (
            // Check if we have active touch tracking
            window.isTouchActive ||
            // Check if there are active touches
            ('touches' in window && window.touches && window.touches.length > 0) ||
            // Check if this is being called from touch event context
            (typeof TouchEvent !== 'undefined' && window.event instanceof TouchEvent) ||
            // Check if we're in a touch environment and not actively mouse dragging
            ('ontouchstart' in window && !window.wordamentDragState.isDragging) ||
            // Check for recent touch activity
            (window.lastTouchTime && (Date.now() - window.lastTouchTime) < 1000)
        );
        
        // Use different margins for touch vs mouse
        const hitAreaMargin = isTouchEvent ? 0.10 : 0.25; // 10% margin for touch (even more lenient), 25% for mouse
        const hitAreaMin = hitAreaMargin;
        const hitAreaMax = 1.0 - hitAreaMargin;
        
        // Check if the click/drag is within the reduced hit area
        const withinHitArea = (relativeX >= hitAreaMin && relativeX <= hitAreaMax && 
                              relativeY >= hitAreaMin && relativeY <= hitAreaMax);
        
        if (withinHitArea) {
            debugLog(`Cell (${x},${y}) hit within ${isTouchEvent ? 'touch-friendly' : 'mouse'} area - relative pos: (${relativeX.toFixed(2)}, ${relativeY.toFixed(2)})`);
            return [x, y];
        } else {
            // Point is in the edge area - more likely to be intended for diagonal movement
            debugLog(`Point (${clientX},${clientY}) in edge area of cell (${x},${y}) - relative pos: (${relativeX.toFixed(2)}, ${relativeY.toFixed(2)}) - ignoring for ${isTouchEvent ? 'touch' : 'mouse'} diagonal friendliness`);
            return null;
        }
        
    } catch (error) {
        debugError('Error getting Wordament cell from coordinates:', error);
        return null;
    }
};

// CRITICAL: Add the missing Blazor callback methods that the Razor component expects
window.wordamentBlazorCallbacks = {
    // Mouse event callbacks
    OnDesktopDragStart: function(x, y) {
        debugLog(`JavaScript: OnDesktopDragStart called with (${x}, ${y})`);
        if (window.wordamentBlazorComponent) {
            try {
                window.wordamentBlazorComponent.invokeMethodAsync('OnDesktopDragStart', x, y);
            } catch (error) {
                debugError('Error calling OnDesktopDragStart:', error);
            }
        } else {
            debugError('Blazor component not registered for OnDesktopDragStart');
        }
    },
    
    OnDesktopDragMove: function(x, y) {
        debugLog(`JavaScript: OnDesktopDragMove called with (${x}, ${y})`);
        if (window.wordamentBlazorComponent) {
            try {
                window.wordamentBlazorComponent.invokeMethodAsync('OnDesktopDragMove', x, y);
            } catch (error) {
                debugError('Error calling OnDesktopDragMove:', error);
            }
        } else {
            debugError('Blazor component not registered for OnDesktopDragMove');
        }
    },
    
    OnDesktopDragEnd: function(path) {
        debugLog(`JavaScript: OnDesktopDragEnd called with path:`, path);
        if (window.wordamentBlazorComponent) {
            try {
                // Convert path to format expected by Blazor (array of arrays)
                const pathArray = path.map(coords => [coords[0], coords[1]]);
                window.wordamentBlazorComponent.invokeMethodAsync('OnDesktopDragEnd', pathArray);
            } catch (error) {
                debugError('Error calling OnDesktopDragEnd:', error);
            }
        } else {
            debugError('Blazor component not registered for OnDesktopDragEnd');
        }
    },
    
    OnDesktopDragBacktrack: function(x, y) {
        debugLog(`JavaScript: OnDesktopDragBacktrack called with (${x}, ${y})`);
        if (window.wordamentBlazorComponent) {
            try {
                window.wordamentBlazorComponent.invokeMethodAsync('OnDesktopDragBacktrack', x, y);
            } catch (error) {
                debugError('Error calling OnDesktopDragBacktrack:', error);
            }
        } else {
            debugError('Blazor component not registered for OnDesktopDragBacktrack');
        }
    },

    // Touch event callbacks (use separate methods for cleaner separation)
    OnTouchDragStart: function(x, y) {
        debugLogTouch(`JavaScript: OnTouchDragStart called with (${x}, ${y})`);
        if (window.wordamentBlazorComponent) {
            try {
                window.wordamentBlazorComponent.invokeMethodAsync('OnTouchDragStart', x, y);
            } catch (error) {
                debugError('Error calling OnTouchDragStart:', error);
            }
        } else {
            debugError('Blazor component not registered for OnTouchDragStart');
        }
    },
    
    OnTouchDragMove: function(x, y) {
        debugLogTouch(`JavaScript: OnTouchDragMove called with (${x}, ${y})`);
        if (window.wordamentBlazorComponent) {
            try {
                window.wordamentBlazorComponent.invokeMethodAsync('OnTouchDragMove', x, y);
            } catch (error) {
                debugError('Error calling OnTouchDragMove:', error);
            }
        } else {
            debugError('Blazor component not registered for OnTouchDragMove');
        }
    },
    
    OnTouchDragEnd: function(path) {
        debugLogTouch(`JavaScript: OnTouchDragEnd called with path:`, path);
        if (window.wordamentBlazorComponent) {
            try {
                // Convert path to format expected by Blazor (array of arrays)
                const pathArray = path.map(coords => [coords[0], coords[1]]);
                window.wordamentBlazorComponent.invokeMethodAsync('OnTouchDragEnd', pathArray);
            } catch (error) {
                debugError('Error calling OnTouchDragEnd:', error);
            }
        } else {
            debugError('Blazor component not registered for OnTouchDragEnd');
        }
    },
    
    OnTouchDragBacktrack: function(x, y) {
        debugLogTouch(`JavaScript: OnTouchDragBacktrack called with (${x}, ${y})`);
        if (window.wordamentBlazorComponent) {
            try {
                window.wordamentBlazorComponent.invokeMethodAsync('OnTouchDragBacktrack', x, y);
            } catch (error) {
                debugError('Error calling OnTouchDragBacktrack:', error);
            }
        } else {
            debugError('Blazor component not registered for OnTouchDragBacktrack');
        }
    }
};

// CRITICAL DEBUG: Add native JavaScript touch event debugging
window.debugWordamentTouchEvents = function() {
    debugLog('Setting up native JavaScript touch event debugging for Wordament');
    
    const grid = document.querySelector('.wordament-grid');
    if (!grid) {
        debugLog('No Wordament grid found for touch debugging');
        return;
    }
    
    let touchStarted = false;
    let touchCount = 0;
    
    // Track touch activity globally for better touch detection
    window.lastTouchTime = 0;
    window.isTouchActive = false;
    
    // Add native touch event listeners to see if events are reaching JavaScript at all
    grid.addEventListener('touchstart', function(e) {
        touchStarted = true;
        touchCount = 0;
        window.lastTouchTime = Date.now();
        window.isTouchActive = true;
        
        debugLogTouch('NATIVE touchstart detected:', {
            touches: e.touches.length,
            changedTouches: e.changedTouches.length,
            target: e.target.className,
            coords: e.touches[0] ? `(${e.touches[0].clientX}, ${e.touches[0].clientY})` : 'none'
        });
    }, { passive: false });
    
    grid.addEventListener('touchmove', function(e) {
        if (touchStarted) {
            touchCount++;
            window.lastTouchTime = Date.now();
            window.isTouchActive = true;
            
            debugLogTouch(`NATIVE touchmove #${touchCount} detected:`, {
                touches: e.touches.length,
                changedTouches: e.changedTouches.length,
                target: e.target.className,
                coords: e.changedTouches[0] ? `(${e.changedTouches[0].clientX}, ${e.changedTouches[0].clientY})` : 'none'
            });
            
            // Test coordinate detection - but don't call Blazor directly to avoid errors
            if (e.changedTouches[0]) {
                const coords = window.getWordamentCellFromCoordinates(grid, e.changedTouches[0].clientX, e.changedTouches[0].clientY);
                debugLogTouch('Detected cell (diagonal-friendly):', coords);
            }
        }
    }, { passive: false });
    
    grid.addEventListener('touchend', function(e) {
        debugLogTouch(`NATIVE touchend detected after ${touchCount} move events`);
        touchStarted = false;
        touchCount = 0;
        window.isTouchActive = false;
        // Keep lastTouchTime for a bit to help with detection
    }, { passive: false });
    
    // Clear touch tracking after a delay
    setInterval(() => {
        if (window.lastTouchTime && (Date.now() - window.lastTouchTime) > 2000) {
            window.isTouchActive = false;
        }
    }, 1000);
    
    debugLog('Native touch debugging set up complete');
};

// NEW: Function to animate word placement in the grid - shows where word was placed
window.animateWordamentWordPlacement = function(word, path) {
    try {
        debugLog(`Animating Wordament word placement for: ${word} with ${path.length} cells`);
        
        if (!path || path.length === 0) {
            debugLog('No path provided for word placement animation');
            return 0;
        }
        
        // Find cells that match the path
        const wordCells = [];
        path.forEach((position, index) => {
            const cell = document.querySelector(`[data-x="${position.x}"][data-y="${position.y}"]`);
            if (cell) {
                wordCells.push({ cell, index });
                debugLog(`Found word cell at (${position.x}, ${position.y})`);
            } else {
                debugLog(`Could not find cell at (${position.x}, ${position.y})`);
            }
        });
        
        if (wordCells.length === 0) {
            debugLog('No word cells found for animation');
            return 0;
        }
        
        debugLog(`Animating ${wordCells.length} cells for word "${word}"`);
        
        // Animate only the cells that belong to this word
        wordCells.forEach(({ cell, index }) => {
            setTimeout(() => {
                if (cell && !cell.classList.contains('wordament-word-reveal')) {
                    debugLog(`Adding wordament-word-reveal to word cell ${index}`);
                    cell.classList.add('wordament-word-reveal');
                    
                    // Force reflow to ensure animation starts
                    cell.offsetHeight;
                    
                    // Remove after animation completes
                    setTimeout(() => {
                        if (cell && cell.classList.contains('wordament-word-reveal')) {
                            debugLog(`Removing wordament-word-reveal from word cell ${index}`);
                            cell.classList.remove('wordament-word-reveal');
                        }
                    }, 1500); // Longer duration for better visibility
                }
            }, index * 120); // Slower stagger for better visibility of the word path
        });
        
        return wordCells.length;
    } catch (error) {
        debugError('Error in animateWordamentWordPlacement:', error);
        return 0;
    }
};

// NEW: Function to animate all found words for celebration
window.animateWordamentCelebration = function() {
    try {
        // Look for found word items in the found words list
        const foundWordItems = document.querySelectorAll('.found-word-item');
        debugLog(`Animating ${foundWordItems.length} found word items`);
        
        if (foundWordItems.length === 0) {
            debugLog('No found word items for celebration animation');
            return 0;
        }
        
        // Animate found word items with stagger
        foundWordItems.forEach((item, index) => {
            setTimeout(() => {
                if (item && !item.classList.contains('celebration-bounce')) {
                    debugLog(`Adding celebration-bounce to word item ${index}`);
                    item.classList.add('celebration-bounce');
                    
                    // Force reflow
                    item.offsetHeight;
                    
                    // Remove after animation
                    setTimeout(() => {
                        if (item && item.classList.contains('celebration-bounce')) {
                            debugLog(`Removing celebration-bounce from word item ${index}`);
                            item.classList.remove('celebration-bounce');
                        }
                    }, 1000);
                }
            }, index * 100); // 100ms stagger between items
        });
        
        return foundWordItems.length;
    } catch (error) {
        debugError('Error in animateWordamentCelebration:', error);
        return 0;
    }
};

// NEW: Function to flash the grid cells that were just used in a word
window.flashWordamentPath = function(path) {
    try {
        debugLog(`Flashing Wordament path with ${path.length} cells`);
        
        if (!path || path.length === 0) {
            debugLog('No path provided for flashing');
            return 0;
        }
        
        // Find cells that match the path
        const pathCells = [];
        path.forEach((position, index) => {
            const cell = document.querySelector(`[data-x="${position.x}"][data-y="${position.y}"]`);
            if (cell) {
                pathCells.push({ cell, index });
                debugLog(`Found path cell at (${position.x}, ${position.y})`);
            }
        });
        
        if (pathCells.length === 0) {
            debugLog('No path cells found for flashing');
            return 0;
        }
        
        debugLog(`Flashing ${pathCells.length} cells in path`);
        
        // Flash all path cells simultaneously
        pathCells.forEach(({ cell, index }) => {
            if (cell && !cell.classList.contains('wordament-path-flash')) {
                debugLog(`Adding wordament-path-flash to path cell ${index}`);
                cell.classList.add('wordament-path-flash');
                
                // Force reflow
                cell.offsetHeight;
                
                // Remove after quick flash
                setTimeout(() => {
                    if (cell && cell.classList.contains('wordament-path-flash')) {
                        debugLog(`Removing wordament-path-flash from path cell ${index}`);
                        cell.classList.remove('wordament-path-flash');
                    }
                }, 800); // Quick flash duration
            }
        });
        
        return pathCells.length;
    } catch (error) {
        debugError('Error in flashWordamentPath:', error);
        return 0;
    }
};

// Enhanced desktop mouse drag support for Wordament with Blazor integration
window.enhanceWordamentDesktopDrag = function() {
    const grid = document.querySelector('.wordament-grid');
    if (!grid) {
        debugLog('Wordament grid not found for desktop drag enhancement');
        return;
    }

    debugLog('Enhancing desktop mouse drag for Wordament grid with diagonal-friendly hit testing');

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
            
            debugLog('Mouse down at:', e.clientX, e.clientY);
            
            const coords = window.getWordamentCellFromCoordinates(grid, e.clientX, e.clientY);
            if (coords) {
                debugLog('JavaScript: Desktop drag started at cell:', coords);
                window.wordamentDragState.isDragging = true;
                window.wordamentDragState.startPosition = coords;
                window.wordamentDragState.currentPosition = coords;
                window.wordamentDragState.dragPath = [coords];
                
                grid.classList.add('dragging');
                
                // Notify Blazor component about drag start
                window.wordamentBlazorCallbacks.OnDesktopDragStart(coords[0], coords[1]);
                
                // Update visual feedback
                window.updateWordamentDragVisuals();
            } else {
                debugLog('No cell detected at mouse down position');
            }
        },

        mouseMove: function(e) {
            if (!window.wordamentDragState.isDragging) return;
            
            // DIAGONAL IMPROVEMENT: Use enhanced hit testing for better diagonal support
            const coords = window.getWordamentCellFromCoordinates(grid, e.clientX, e.clientY);
            if (coords) {
                const lastInPath = window.wordamentDragState.dragPath[window.wordamentDragState.dragPath.length - 1];
                if (!lastInPath || coords[0] !== lastInPath[0] || coords[1] !== lastInPath[1]) {
                    debugLog('JavaScript: Drag moved to cell (diagonal-friendly):', coords);
                    
                    // Update drag state
                    window.wordamentDragState.currentPosition = coords;
                    
                    // Check if we're backtracking
                    if (window.wordamentDragState.dragPath.length > 1) {
                        const secondLast = window.wordamentDragState.dragPath[window.wordamentDragState.dragPath.length - 2];
                        if (coords[0] === secondLast[0] && coords[1] === secondLast[1]) {
                            // Backtracking - remove last position
                            window.wordamentDragState.dragPath.pop();
                            debugLog('JavaScript: Backtracking to:', coords);
                            
                            // Notify Blazor about backtrack
                            window.wordamentBlazorCallbacks.OnDesktopDragBacktrack(coords[0], coords[1]);
                        } else {
                            // Add new position to path
                            window.wordamentDragState.dragPath.push(coords);
                            debugLog('JavaScript: Added to path:', coords);
                            
                            // Notify Blazor about new position
                            window.wordamentBlazorCallbacks.OnDesktopDragMove(coords[0], coords[1]);
                        }
                    } else {
                        // Add to path
                        window.wordamentDragState.dragPath.push(coords);
                        debugLog('JavaScript: Added to path:', coords);
                        
                        // Notify Blazor about new position
                        window.wordamentBlazorCallbacks.OnDesktopDragMove(coords[0], coords[1]);
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
            
            debugLog('JavaScript: Desktop drag ended. Path:', window.wordamentDragState.dragPath);
            
            // Notify Blazor about drag end
            window.wordamentBlazorCallbacks.OnDesktopDragEnd(window.wordamentDragState.dragPath);
            
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
                debugLog('JavaScript: Mouse left grid during drag');
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

    debugLog('Enhanced desktop mouse drag handlers attached with diagonal-friendly hit testing');
};

// NEW: Enhanced touch drag support for Wordament with Blazor integration
window.enhanceWordamentTouchDrag = function() {
    const grid = document.querySelector('.wordament-grid');
    if (!grid) {
        debugLog('Wordament grid not found for touch drag enhancement');
        return;
    }

    debugLog('Enhancing touch drag for Wordament grid');

    // Remove any existing touch event listeners to prevent duplicates
    if (window.wordamentTouchHandlers) {
        grid.removeEventListener('touchstart', window.wordamentTouchHandlers.touchStart);
        grid.removeEventListener('touchmove', window.wordamentTouchHandlers.touchMove);
        grid.removeEventListener('touchend', window.wordamentTouchHandlers.touchEnd);
    }

    // Create enhanced touch event handlers that communicate with Blazor
    window.wordamentTouchHandlers = {
        touchStart: function(e) {
            e.preventDefault();
            e.stopPropagation();
            
            if (e.touches.length > 0) {
                const touch = e.touches[0];
                debugLogTouch('Touch start at:', touch.clientX, touch.clientY);
                
                const coords = window.getWordamentCellFromCoordinates(grid, touch.clientX, touch.clientY);
                if (coords) {
                    debugLogTouch('JavaScript: Touch drag started at cell:', coords);
                    window.wordamentDragState.isDragging = true;
                    window.wordamentDragState.startPosition = coords;
                    window.wordamentDragState.currentPosition = coords;
                    window.wordamentDragState.dragPath = [coords];
                    
                    grid.classList.add('dragging');
                    
                    // Notify Blazor component about touch drag start
                    window.wordamentBlazorCallbacks.OnTouchDragStart(coords[0], coords[1]);
                    
                    // Update visual feedback
                    window.updateWordamentDragVisuals();
                } else {
                    debugLogTouch('No cell detected at touch start position');
                }
            }
        },

        touchMove: function(e) {
            if (!window.wordamentDragState.isDragging) return;
            
            e.preventDefault();
            e.stopPropagation();
            
            if (e.touches.length > 0) {
                const touch = e.touches[0];
                
                const coords = window.getWordamentCellFromCoordinates(grid, touch.clientX, touch.clientY);
                if (coords) {
                    const lastInPath = window.wordamentDragState.dragPath[window.wordamentDragState.dragPath.length - 1];
                    if (!lastInPath || coords[0] !== lastInPath[0] || coords[1] !== lastInPath[1]) {
                        debugLogTouch('JavaScript: Touch moved to cell (touch-friendly):', coords);
                        
                        // Update drag state
                        window.wordamentDragState.currentPosition = coords;
                        
                        // Check if we're backtracking
                        if (window.wordamentDragState.dragPath.length > 1) {
                            const secondLast = window.wordamentDragState.dragPath[window.wordamentDragState.dragPath.length - 2];
                            if (coords[0] === secondLast[0] && coords[1] === secondLast[1]) {
                                // Backtracking - remove last position
                                window.wordamentDragState.dragPath.pop();
                                debugLogTouch('JavaScript: Touch backtracking to:', coords);
                        
                                // Notify Blazor about backtrack
                                window.wordamentBlazorCallbacks.OnTouchDragBacktrack(coords[0], coords[1]);
                            } else {
                                // Add new position to path
                                window.wordamentDragState.dragPath.push(coords);
                                debugLogTouch('JavaScript: Touch added to path:', coords);
                        
                                // Notify Blazor about new position
                                window.wordamentBlazorCallbacks.OnTouchDragMove(coords[0], coords[1]);
                            }
                        } else {
                            // Add to path
                            window.wordamentDragState.dragPath.push(coords);
                            debugLogTouch('JavaScript: Touch added to path:', coords);
                        
                            // Notify Blazor about new position
                            window.wordamentBlazorCallbacks.OnTouchDragMove(coords[0], coords[1]);
                        }
                        
                        // Update visual feedback
                        window.updateWordamentDragVisuals();
                    }
                }
            }
        },

        touchEnd: function(e) {
            if (!window.wordamentDragState.isDragging) return;
            
            e.preventDefault();
            e.stopPropagation();
            
            debugLogTouch('JavaScript: Touch drag ended. Path:', window.wordamentDragState.dragPath);
            
            // Notify Blazor about touch drag end
            window.wordamentBlazorCallbacks.OnTouchDragEnd(window.wordamentDragState.dragPath);
            
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
        }
    };

    // Attach enhanced touch event listeners
    grid.addEventListener('touchstart', window.wordamentTouchHandlers.touchStart, { 
        passive: false 
    });
    grid.addEventListener('touchmove', window.wordamentTouchHandlers.touchMove, { 
        passive: false 
    });
    grid.addEventListener('touchend', window.wordamentTouchHandlers.touchEnd, { 
        passive: false 
    });

    debugLog('Enhanced touch drag handlers attached');
};

// Function to register Blazor component for JavaScript callbacks
window.registerWordamentBlazorComponent = function(dotNetHelper) {
    window.wordamentBlazorComponent = dotNetHelper;
    debugLog('Wordament Blazor component registered for JavaScript callbacks');
    
    // Test that the component is callable
    setTimeout(() => {
        if (window.wordamentBlazorComponent) {
            debugLog('Testing Blazor component callback availability...');
            try {
                // Test if we can call a method (this might fail if method doesn't exist, but that's ok)
                debugLog('Blazor component is ready for JavaScript callbacks');
                
                // Initialize mouse handling now that Blazor is ready
                if (window.location.pathname.includes('/wordament')) {
                    setTimeout(() => {
                        const grid = document.querySelector('.wordament-grid');
                        if (grid && !window.wordamentMouseHandlers) {
                            debugLog('Setting up mouse handlers now that Blazor is registered...');
                            window.enhanceWordamentDesktopDrag();
                        }
                    }, 100);
                }
            } catch (error) {
                debugError('Error testing Blazor component:', error);
            }
        } else {
            debugError('Blazor component registration failed - component is null');
        }
    }, 100);
};

// NEW: Function to test diagonal hit area improvements
window.testDiagonalHitArea = function() {
    debugLog('Testing diagonal hit area improvements...');
    
    const grid = document.querySelector('.wordament-grid');
    if (!grid) {
        debugLog('Grid not found for diagonal testing');
        return false;
    }
    
    const cells = grid.querySelectorAll('.wordament-cell');
    if (cells.length !== 16) {
        debugLog(`Expected 16 cells, found ${cells.length}`);
        return false;
    }
    
    debugLog('Testing diagonal hit area detection with reduced effective area...');
    
    let diagonalTests = 0;
    let successfulDiagonalDetections = 0;
    
    // Test cell (0,0) with various positions
    const cell00 = document.querySelector('[data-x="0"][data-y="0"]');
    
    if (cell00) {
        const rect = cell00.getBoundingClientRect();
        
        // Test 1: Center of cell should always be detected
        const centerX = rect.left + rect.width * 0.5;
        const centerY = rect.top + rect.height * 0.5;
        
        diagonalTests++;
        const centerDetection = window.getWordamentCellFromCoordinates(grid, centerX, centerY);
        
        if (centerDetection && centerDetection[0] === 0 && centerDetection[1] === 0) {
            successfulDiagonalDetections++;
            debugLog('Center test passed: Cell center correctly detected as (0,0)');
        } else {
            debugLog(`Center test failed: Expected (0,0), got ${centerDetection ? `(${centerDetection[0]},${centerDetection[1]})` : 'null'}`);
        }
        
        // Test 2: Point within reduced area (40% into cell) should be detected
        const innerX = rect.left + rect.width * 0.4;  // 40% into cell - within hit area
        const innerY = rect.top + rect.height * 0.4;  // 40% into cell - within hit area
        
        diagonalTests++;
        const innerDetection = window.getWordamentCellFromCoordinates(grid, innerX, innerY);
        
        if (innerDetection && innerDetection[0] === 0 && innerDetection[1] === 0) {
            successfulDiagonalDetections++;
            debugLog('Inner area test passed: Inner point correctly detected as (0,0)');
        } else {
            debugLog(`Inner area test failed: Expected (0,0), got ${innerDetection ? `(${innerDetection[0]},${innerDetection[1]})` : 'null'}`);
        }
        
        // Test 3: Point near edge (10% into cell) should NOT be detected (in dead zone)
        const edgeX = rect.left + rect.width * 0.1;  // 10% into cell - should be in dead zone
        const edgeY = rect.top + rect.height * 0.1;  // 10% into cell - should be in dead zone
        
        diagonalTests++;
        const edgeDetection = window.getWordamentCellFromCoordinates(grid, edgeX, edgeY);
        
        if (!edgeDetection) {
            successfulDiagonalDetections++;
            debugLog('Edge test passed: Edge point correctly NOT detected (in dead zone)');
        } else {
            debugLog(`Edge test failed: Expected null, got (${edgeDetection[0]},${edgeDetection[1]}) - dead zone not working`);
        }
        
        // Test 4: Point very near edge (5% into cell) should NOT be detected
        const veryEdgeX = rect.left + rect.width * 0.05;  // 5% into cell - definitely in dead zone
        const veryEdgeY = rect.top + rect.height * 0.05;  // 5% into cell - definitely in dead zone
        
        diagonalTests++;
        const veryEdgeDetection = window.getWordamentCellFromCoordinates(grid, veryEdgeX, veryEdgeY);
        
        if (!veryEdgeDetection) {
            successfulDiagonalDetections++;
            debugLog('Very edge test passed: Very edge point correctly NOT detected (in dead zone)');
        } else {
            debugLog(`Very edge test failed: Expected null, got (${veryEdgeDetection[0]},${veryEdgeDetection[1]}) - dead zone not working`);
        }
        
        // Test 5: Point on opposite edge (95% into cell) should NOT be detected  
        const oppEdgeX = rect.left + rect.width * 0.95;  // 95% into cell - should be in dead zone
        const oppEdgeY = rect.top + rect.height * 0.95;  // 95% into cell - should be in dead zone
        
        diagonalTests++;
        const oppEdgeDetection = window.getWordamentCellFromCoordinates(grid, oppEdgeX, oppEdgeY);
        
        if (!oppEdgeDetection) {
            successfulDiagonalDetections++;
            debugLog('Opposite edge test passed: Far edge point correctly NOT detected (in dead zone)');
        } else {
            debugLog(`Opposite edge test failed: Expected null, got (${oppEdgeDetection[0]},${oppEdgeDetection[1]}) - dead zone not working`);
        }
        
        // Test 6: Point at boundary of hit area (exactly 75% into cell) should be detected
        const boundaryX = rect.left + rect.width * 0.75;  // 75% into cell - right at boundary
        const boundaryY = rect.top + rect.height * 0.75;  // 75% into cell - right at boundary
        
        diagonalTests++;
        const boundaryDetection = window.getWordamentCellFromCoordinates(grid, boundaryX, boundaryY);
        
        if (boundaryDetection && boundaryDetection[0] === 0 && boundaryDetection[1] === 0) {
            successfulDiagonalDetections++;
            debugLog('Boundary test passed: Boundary point correctly detected as (0,0)');
        } else {
            debugLog(`Boundary test failed: Expected (0,0), got ${boundaryDetection ? `(${boundaryDetection[0]},${boundaryDetection[1]})` : 'null'}`);
        }
    }
    
    const successRate = (successfulDiagonalDetections / diagonalTests) * 100;
    debugLog(`Diagonal hit area test results: ${successfulDiagonalDetections}/${diagonalTests} tests passed (${successRate.toFixed(1)}%)`);
    
    if (successRate >= 83) { // 5/6 tests should pass
        debugLog('Diagonal hit area improvements are working well!');
        return true;
    } else {
        debugLog('Diagonal hit area improvements need more work');
        return false;
    }
};

// NEW: Visual function to show hit areas for debugging
window.visualizeHitAreas = function() {
    debugLog('Visualizing hit areas for diagonal drag debugging...');
    
    const grid = document.querySelector('.wordament-grid');
    if (!grid) {
        debugLog('Grid not found for visualization');
        return false;
    }
    
    const cells = grid.querySelectorAll('.wordament-cell');
    
    cells.forEach((cell, index) => {
        const x = cell.getAttribute('data-x');
        const y = cell.getAttribute('data-y');
        const rect = cell.getBoundingClientRect();
        
        // Create a visual overlay to show the reduced hit area
        const overlay = document.createElement('div');
        overlay.className = 'hit-area-overlay';
        overlay.style.position = 'absolute';
        overlay.style.pointerEvents = 'none';
        overlay.style.border = '2px solid red';
        overlay.style.backgroundColor = 'rgba(255, 0, 0, 0.1)';
        overlay.style.zIndex = '1000';
        
        // Calculate the reduced hit area (25% margin on each side)
        const margin = 0.25;
        const hitWidth = rect.width * (1 - 2 * margin);
        const hitHeight = rect.height * (1 - 2 * margin);
        const hitLeft = rect.left + rect.width * margin;
        const hitTop = rect.top + rect.height * margin;
        
        overlay.style.left = hitLeft + 'px';
        overlay.style.top = hitTop + 'px';
        overlay.style.width = hitWidth + 'px';
        overlay.style.height = hitHeight + 'px';
        
        // Add label
        const label = document.createElement('div');
        label.textContent = `(${x},${y})`;
        label.style.fontSize = '10px';
        label.style.color = 'red';
        label.style.fontWeight = 'bold';
        label.style.textAlign = 'center';
        label.style.lineHeight = hitHeight + 'px';
        overlay.appendChild(label);
        
        document.body.appendChild(overlay);
        
        debugLog(`Cell (${x},${y}): Hit area ${hitWidth.toFixed(0)}x${hitHeight.toFixed(0)} at (${hitLeft.toFixed(0)},${hitTop.toFixed(0)})`);
        
        // Auto-remove after 5 seconds
        setTimeout(() => {
            if (overlay.parentNode) {
                overlay.parentNode.removeChild(overlay);
            }
        }, 5000);
    });
    
    return true;
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
    debugLog('Initializing Wordament game...');
    
    // Only apply Wordament functionality if on the Wordament page
    if (window.location.pathname.includes('/wordament')) {
        // Add enhanced touch and mouse handling for Wordament grid
        setTimeout(() => {
            const grid = document.querySelector('.wordament-grid');
            if (grid) {
                debugLog('Setting up Wordament touch and drag handling');
                
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
                
                // Enhanced desktop mouse drag support
                window.enhanceWordamentDesktopDrag();
                
                // Enhanced touch drag support
                window.enhanceWordamentTouchDrag();
                
                // Set up native touch event debugging
                window.debugWordamentTouchEvents();
                
                console.log('[Wordament] Enhanced touch and mouse handling applied');
            } else {
                debugLog('Wordament grid not found for setup, will retry when Blazor registers...');
            }
        }, 300); // Shorter delay
        
        debugLog('Wordament initialization complete');
    }
};

// Enhanced debug function to test both coordinate detection AND Blazor integration
window.testWordamentDesktopDrag = function() {
    debugLog('Testing Wordament desktop drag functionality with diagonal improvements...');
    
    const grid = document.querySelector('.wordament-grid');
    if (!grid) {
        debugLog('Grid not found');
        return false;
    }
    
    const cells = grid.querySelectorAll('.wordament-cell');
    debugLog('Found', cells.length, 'cells');
    
    if (cells.length !== 16) {
        debugLog('Expected 16 cells, found', cells.length);
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
            debugLog(`Failed to detect cell (${x},${y}) at center (${centerX},${centerY})`);
        }
    });
    
    debugLog(`Center coordinate detection: ${successCount}/16 cells successful`);
    
    // Test 3: Check if drag handlers are attached
    const hasMouseHandlers = !!window.wordamentMouseHandlers;
    debugLog(`Mouse handlers attached: ${hasMouseHandlers}`);
    
    // Test 4: Check if drag state object exists
    const hasDragState = !!window.wordamentDragState;
    debugLog(`Drag state object: ${hasDragState}`);
    
    // Test 5: Check if visual functions exist
    const hasVisualFunctions = !!(window.updateWordamentDragVisuals && window.clearWordamentDragVisuals);
    debugLog(`Visual feedback functions: ${hasVisualFunctions}`);
    
    // Test 6: Check if Blazor component is registered
    const hasBlazorComponent = !!window.wordamentBlazorComponent;
    debugLog(`Blazor component registered: ${hasBlazorComponent}`);
    
    // Test 7: Check if animation functions exist
    const hasAnimationFunctions = !!(window.animateWordamentWordPlacement && window.animateWordamentCelebration && window.flashWordamentPath);
    debugLog(`Animation functions available: ${hasAnimationFunctions}`);
    
    // Overall test result
    const overallSuccess = successCount === 16 && hasMouseHandlers && hasDragState && hasVisualFunctions && hasBlazorComponent && hasAnimationFunctions;
    console.log(`[Wordament] Overall desktop drag test: ${overallSuccess ? 'PASSED' : 'FAILED'}`);
    
    if (overallSuccess) {
        debugLog('All tests passed! JavaScript functions are working correctly.');
    } else {
        debugLog('Some tests failed. Check the individual test results above for details.');
    }
    
    return overallSuccess;
};

// DEBUGGING HELPER: Simple function to test if JavaScript is working
window.testWordamentJavaScript = function() {
    console.log('[Wordament] Testing basic Wordament JavaScript functionality...');
    
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
    console.log('[Wordament] Test Results:');
    tests.forEach((test, index) => {
        const status = test.result ? '?' : '?';
        console.log(`${index + 1}. ${status} ${test.name}${test.details ? ` - ${test.details}` : ''}`);
    });
    
    const allPassed = tests.every(test => test.result);
    console.log(`[Wordament] Overall: ${allPassed ? 'ALL TESTS PASSED' : 'SOME TESTS FAILED'}`);
    
    return allPassed;
};