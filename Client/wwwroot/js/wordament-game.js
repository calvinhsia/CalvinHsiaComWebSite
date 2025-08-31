// Wordament Game JavaScript Functions

// Function to get Wordament cell from coordinates
window.getWordamentCellFromCoordinates = function (gridElement, clientX, clientY) {
    try {
        // Get the grid container element
        let gridContainer = gridElement;
        if (!gridContainer || !gridContainer.querySelector) {
            gridContainer = document.querySelector('.wordament-grid');
        }
        
        if (!gridContainer) {
            console.error('Wordament grid not found');
            return null;
        }

        // Get the actual grid element
        const grid = gridContainer.querySelector ? gridContainer.querySelector('.wordament-grid') : gridContainer;
        if (!grid) {
            console.error('Wordament grid element not found');
            return null;
        }

        // Use elementFromPoint to find the cell under the coordinates
        const elementUnderPoint = document.elementFromPoint(clientX, clientY);
        if (!elementUnderPoint) {
            return null;
        }

        // Find the closest wordament-cell element
        let cellElement = elementUnderPoint.closest('.wordament-cell');
        if (!cellElement) {
            return null;
        }

        // Get the data attributes for x,y coordinates
        const x = parseInt(cellElement.getAttribute('data-x'));
        const y = parseInt(cellElement.getAttribute('data-y'));

        if (isNaN(x) || isNaN(y)) {
            console.warn('Invalid cell coordinates found:', x, y);
            return null;
        }

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
        // Add any Wordament-specific initialization here
        console.log('? Wordament initialization complete');
    }
};

// Auto-initialize if on Wordament page
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', window.initializeWordament);
} else {
    window.initializeWordament();
}