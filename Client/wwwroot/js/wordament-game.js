// Wordament Game JavaScript Functions

// Function to get Wordament cell from coordinates
window.getWordamentCellFromCoordinates = function (gridElement, clientX, clientY) {
    try {
        console.log('getWordamentCellFromCoordinates called with:', { clientX, clientY });
        
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

        console.log('Found cell coordinates:', [x, y]);
        return [x, y];
    } catch (error) {
        console.error('Error getting Wordament cell from coordinates:', error);
        return null;
    }
};

// Initialize Wordament-specific functionality
window.initializeWordament = function () {
    console.log('?? Initializing Wordament game...');
    
    // Only apply Wordament functionality if on the Wordament page
    if (window.location.pathname.includes('/wordament')) {
        // Add enhanced touch handling for Wordament grid
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
                
                console.log('? Enhanced Wordament touch handling applied');
            } else {
                console.log('?? Wordament grid not found for touch handling setup');
            }
        }, 500); // Delay to ensure DOM is ready
        
        console.log('? Wordament initialization complete');
    }
};

// Auto-initialize if on Wordament page
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', window.initializeWordament);
} else {
    window.initializeWordament();
}