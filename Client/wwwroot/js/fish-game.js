// Fish vs Sharks Cellular Automata Game JavaScript

let fishCanvas = null;
let fishCtx = null;

window.initFishCanvas = function (canvasId, width, height) {
    console.log(`[Fish JS] Initializing canvas: ${canvasId}`);
    fishCanvas = document.getElementById(canvasId);

    if (!fishCanvas) {
        console.error(`[Fish JS] Canvas element '${canvasId}' not found`);
        return;
    }

    fishCanvas.width = width;
    fishCanvas.height = height;
    fishCtx = fishCanvas.getContext('2d');

    // Prevent context menu on right-click
    fishCanvas.addEventListener('contextmenu', (e) => {
        e.preventDefault();
        return false;
    });

    console.log(`[Fish JS] Canvas initialized: ${width}x${height}`);

    // Clear canvas
    fishCtx.fillStyle = '#FFFFFF';
    fishCtx.fillRect(0, 0, width, height);
};

window.fishRenderFrame = function (cellData, rows, cols, cellWidth, cellHeight, useCircles, colorAgeGradient) {
    if (!fishCtx || !fishCanvas) {
        console.error('[Fish JS] Canvas not initialized');
        return;
    }

    // Clear canvas
    fishCtx.fillStyle = '#FFFFFF';
    fishCtx.fillRect(0, 0, fishCanvas.width, fishCanvas.height);

    let index = 0;
    for (let row = 0; row < rows; row++) {
        for (let col = 0; col < cols; col++) {
            const cell = cellData[index++];
            const x = col * cellWidth;
            const y = row * cellHeight;

            let color = '#FFFFFF'; // Empty = white

            if (cell.type === 1) {
                // Fish = green (darken with age)
                const ageAdjust = Math.min(cell.age * colorAgeGradient, 255);
                const greenValue = Math.max(0, 255 - ageAdjust);
                color = `rgb(0, ${greenValue}, 0)`;
            } else if (cell.type === 2) {
                // Shark = red (darken with age)
                const ageAdjust = Math.min(cell.age * colorAgeGradient, 255);
                const redValue = Math.max(0, 255 - ageAdjust);
                color = `rgb(${redValue}, 0, 0)`;
            }

            fishCtx.fillStyle = color;

            if (useCircles && cell.type !== 0) {
                // Draw circle
                const centerX = x + cellWidth / 2;
                const centerY = y + cellHeight / 2;
                const radius = Math.min(cellWidth, cellHeight) / 2;

                fishCtx.beginPath();
                fishCtx.arc(centerX, centerY, radius, 0, 2 * Math.PI);
                fishCtx.fill();
            } else {
                // Draw rectangle
                fishCtx.fillRect(x, y, cellWidth, cellHeight);
            }
        }
    }
};

window.downloadCsv = function (csvContent, filename) {
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const link = document.createElement('a');
    const url = URL.createObjectURL(blob);

    link.setAttribute('href', url);
    link.setAttribute('download', filename);
    link.style.visibility = 'hidden';

    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);

    console.log(`[Fish JS] Downloaded ${filename}`);
};
