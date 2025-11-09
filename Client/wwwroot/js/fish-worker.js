// Fish vs Sharks Web Worker - v1.0
console.log('[Fish Worker] Loading...');

// Simulation state
let cells = null;
let rows = 0;
let cols = 0;
let generation = 0;
let fishCount = 0;
let sharkCount = 0;

// Simulation parameters
let params = {
    fishBreedAge: 3,
    fishLifeLength: 22,
    sharkBreedAge: 10,
    sharkLifeLength: 20,
    sharkStarve: 6,
    oneActionPerYear: false,
    torus: true
};

// Cell type constants
const EMPTY = 0;
const FISH = 1;
const SHARK = 2;

// Direction constants
const NORTH = 0, SOUTH = 1, WEST = 2, EAST = 3;

// Simple PRNG (Mulberry32)
let seed = Date.now();
function random() {
    let t = seed += 0x6D2B79F5;
    t = Math.imul(t ^ t >>> 15, t | 1);
    t ^= t + Math.imul(t ^ t >>> 7, t | 61);
    return ((t ^ t >>> 14) >>> 0) / 4294967296;
}

function randomInt(max) {
    return Math.floor(random() * max);
}

// Fisher-Yates shuffle
function shuffle(array) {
    for (let i = array.length - 1; i > 0; i--) {
        const j = randomInt(i + 1);
        [array[i], array[j]] = [array[j], array[i]];
    }
    return array;
}

// Initialize world
function initWorld(data) {
    rows = data.rows;
    cols = data.cols;
    generation = 0;
    fishCount = 0;
    sharkCount = 0;
    seed = data.seed || Date.now();

    Object.assign(params, data.params);

    // Flat array: [type, age, lastMeal, lastAction, lastBirth, processed] per cell
    const cellCount = rows * cols;
    cells = new Uint32Array(cellCount * 6);

    for (let i = 0; i < cellCount; i++) {
        const rand = randomInt(100);
        const baseIdx = i * 6;

        if (rand < data.fishInitPct) {
            cells[baseIdx] = FISH;
            cells[baseIdx + 1] = 0;
            fishCount++;
        } else if (rand < data.fishInitPct + data.sharkInitPct) {
            cells[baseIdx] = SHARK;
            cells[baseIdx + 1] = 0;
            cells[baseIdx + 2] = 0;
            sharkCount++;
        }
    }

    console.log(`[Fish Worker] Initialized ${rows}x${cols} (${fishCount} fish, ${sharkCount} sharks)`);
}

// Get cell base index
function idx(row, col) {
    return (row * cols + col) * 6;
}

// Get shuffled neighbors
function getNeighbors(row, col) {
    const directions = shuffle([NORTH, SOUTH, WEST, EAST]);
    const neighbors = [];

    for (const dir of directions) {
        let newRow = row, newCol = col;

        if (dir === NORTH) {
            newRow--;
            if (newRow < 0) {
                if (!params.torus) continue;
                newRow = rows - 1;
            }
        } else if (dir === SOUTH) {
            newRow++;
            if (newRow >= rows) {
                if (!params.torus) continue;
                newRow = 0;
            }
        } else if (dir === WEST) {
            newCol--;
            if (newCol < 0) {
                if (!params.torus) continue;
                newCol = cols - 1;
            }
        } else { // EAST
            newCol++;
            if (newCol >= cols) {
                if (!params.torus) continue;
                newCol = 0;
            }
        }

        neighbors.push([newRow, newCol]);
    }

    return neighbors;
}

// Kill animal
function killAnimal(i) {
    if (cells[i] === FISH) fishCount--;
    else if (cells[i] === SHARK) sharkCount--;
    cells[i] = EMPTY;
    cells[i + 1] = 0;
}

// Process single cell
function processCell(row, col) {
    const i = idx(row, col);
    const type = cells[i];

    if (cells[i + 5] || type === EMPTY) return;

    cells[i + 5] = 1; // Mark processed
    cells[i + 1]++; // Age

    const age = cells[i + 1];

    // Check survival
    if (type === FISH && age >= params.fishLifeLength) {
        killAnimal(i);
        return;
    }

    if (type === SHARK) {
        if (age >= params.sharkLifeLength || (generation - cells[i + 2]) >= params.sharkStarve) {
            killAnimal(i);
            return;
        }
    }

    // Check one action per year
    if (params.oneActionPerYear && cells[i + 3] === generation) return;

    const neighbors = getNeighbors(row, col);

    // Sharks eat fish
    if (type === SHARK) {
        const fishNeighbors = [];
        for (const [nr, nc] of neighbors) {
            const ni = idx(nr, nc);
            if (cells[ni] === FISH && cells[ni + 1] > 0) {
                fishNeighbors.push([nr, nc]);
            }
        }

        if (fishNeighbors.length > 0) {
            const [tr, tc] = fishNeighbors[randomInt(fishNeighbors.length)];
            const ti = idx(tr, tc);

            fishCount--;
            cells[ti] = SHARK;
            cells[ti + 1] = age;
            cells[ti + 2] = generation;
            cells[ti + 3] = generation;
            cells[ti + 4] = cells[i + 4];
            cells[ti + 5] = 1;

            if (age >= params.sharkBreedAge) {
                cells[i + 1] = 0;
                cells[i + 2] = generation;
                cells[i + 3] = generation;
                cells[i + 4] = generation;
                sharkCount++;
            } else {
                cells[i] = EMPTY;
            }
            return;
        }
    }

    // Movement and breeding
    const emptyNeighbors = [];
    for (const [nr, nc] of neighbors) {
        const ni = idx(nr, nc);
        if (cells[ni] === EMPTY) {
            emptyNeighbors.push([nr, nc]);
        }
    }

    if (emptyNeighbors.length > 0) {
        const canBreed = (type === FISH && age >= params.fishBreedAge) ||
            (type === SHARK && age >= params.sharkBreedAge);

        const [tr, tc] = emptyNeighbors[randomInt(emptyNeighbors.length)];
        const ti = idx(tr, tc);

        if (canBreed) {
            cells[ti] = type;
            cells[ti + 1] = 0;
            cells[ti + 2] = type === SHARK ? generation : 0;
            cells[ti + 3] = generation;
            cells[ti + 4] = generation;
            cells[i + 3] = generation;
            cells[i + 4] = generation;

            if (type === FISH) fishCount++;
            else sharkCount++;
        } else {
            cells[ti] = type;
            cells[ti + 1] = age;
            cells[ti + 2] = cells[i + 2];
            cells[ti + 3] = generation;
            cells[ti + 4] = cells[i + 4];
            cells[ti + 5] = 1;
            cells[i] = EMPTY;
        }
    }
}

// Run one generation
function doGeneration() {
    generation++;

    // Reset processed flags
    for (let i = 0; i < rows * cols; i++) {
        cells[i * 6 + 5] = 0;
    }

    // Random scan direction
    const forward = randomInt(2) === 0;
    const rowStart = forward ? 0 : rows - 1;
    const rowEnd = forward ? rows : -1;
    const rowInc = forward ? 1 : -1;
    const colStart = forward ? 0 : cols - 1;
    const colEnd = forward ? cols : -1;
    const colInc = forward ? 1 : -1;

    for (let row = rowStart; row !== rowEnd; row += rowInc) {
        for (let col = colStart; col !== colEnd; col += colInc) {
            processCell(row, col);
        }
    }
}

// Pack cells for rendering
function packCells() {
    const cellCount = rows * cols;
    const packed = new Uint8Array(cellCount);

    for (let i = 0; i < cellCount; i++) {
        const baseIdx = i * 6;
        const type = cells[baseIdx];
        const age = Math.min(cells[baseIdx + 1], 63);
        packed[i] = (type << 6) | age;
    }

    return packed;
}

// Handle messages
self.onmessage = function (e) {
    const { command, data } = e.data;

    try {
        if (command === 'init') {
            initWorld(data);
            const packed = packCells();
            self.postMessage({
                type: 'initialized',
                cells: packed.buffer,
                fishCount,
                sharkCount,
                generation
            }, [packed.buffer]);

        } else if (command === 'tick') {
            doGeneration();
            const packed = packCells();
            self.postMessage({
                type: 'generation',
                cells: packed.buffer,
                fishCount,
                sharkCount,
                generation
            }, [packed.buffer]);

        } else if (command === 'updateParams') {
            Object.assign(params, data);

        } else if (command === 'addAnimal') {
            const { row, col, animalType } = data;
            const i = idx(row, col);
            const oldType = cells[i];

            if (oldType !== animalType) {
                if (oldType === FISH) fishCount--;
                else if (oldType === SHARK) sharkCount--;

                cells[i] = animalType;
                cells[i + 1] = 0;
                cells[i + 2] = animalType === SHARK ? generation : 0;
                cells[i + 3] = generation;
                cells[i + 4] = generation;

                if (animalType === FISH) fishCount++;
                else if (animalType === SHARK) sharkCount++;

                const packed = packCells();
                self.postMessage({
                    type: 'updated',
                    cells: packed.buffer,
                    fishCount,
                    sharkCount,
                    generation
                }, [packed.buffer]);
            }
        }
    } catch (error) {
        console.error('[Fish Worker] Error:', error);
        self.postMessage({ type: 'error', error: error.message });
    }
};

console.log('[Fish Worker] Ready');
