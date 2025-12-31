// FreeCell Game JavaScript - Drag and Drop Support
console.log('[FreeCell JS v1] Loading...');

// Global state for FreeCell drag operations
window.freecellDragState = {
    isDragging: false,
    isPotentialDrag: false,
    sourceType: null,
    sourceIndex: null,
    cardIndex: null,
    dragElement: null,
    sourceCard: null,
    startX: 0,
    startY: 0,
    offsetX: 0,
    offsetY: 0
};

// Minimum distance to move before starting a drag
const DRAG_THRESHOLD = 5;

// Reference to Blazor component
window.freecellBlazorComponent = null;

// Register Blazor component for callbacks
window.registerFreeCellBlazorComponent = function (dotNetHelper) {
    window.freecellBlazorComponent = dotNetHelper;
    console.log('[FreeCell JS v1] Blazor component registered');
};

// Cleanup function
window.cleanupFreeCell = function() {
    const dragVisual = document.getElementById('freecell-drag-visual');
    if (dragVisual) {
        dragVisual.remove();
    }
    
    document.querySelectorAll('.drop-target-highlight').forEach(el => {
        el.classList.remove('drop-target-highlight');
    });
    
    const container = document.querySelector('.freecell-container');
    if (container) {
        if (window.freecellMouseHandlers) {
            container.removeEventListener('mousedown', window.freecellMouseHandlers.mouseDown);
        }
        if (window.freecellTouchHandlers) {
            container.removeEventListener('touchstart', window.freecellTouchHandlers.touchStart);
            container.removeEventListener('touchmove', window.freecellTouchHandlers.touchMove);
            container.removeEventListener('touchend', window.freecellTouchHandlers.touchEnd);
            container.removeEventListener('touchcancel', window.freecellTouchHandlers.touchCancel);
        }
    }
    if (window.freecellMouseHandlers) {
        document.removeEventListener('mousemove', window.freecellMouseHandlers.mouseMove);
        document.removeEventListener('mouseup', window.freecellMouseHandlers.mouseUp);
    }
    
    window.freecellDragState = {
        isDragging: false,
        isPotentialDrag: false,
        sourceType: null,
        sourceIndex: null,
        cardIndex: null,
        dragElement: null,
        sourceCard: null,
        startX: 0,
        startY: 0,
        offsetX: 0,
        offsetY: 0
    };
    
    console.log('[FreeCell JS v1] Cleanup complete');
};

// Initialize FreeCell drag support
window.initializeFreeCell = function () {
    console.log('[FreeCell JS v1] Initializing...');
    
    window.cleanupFreeCell();
    
    const container = document.querySelector('.freecell-container');
    if (!container) {
        console.log('[FreeCell JS v1] Container not found, retrying...');
        setTimeout(window.initializeFreeCell, 100);
        return;
    }

    setupFreeCellMouseHandlers(container);
    setupFreeCellTouchHandlers(container);
    
    console.log('[FreeCell JS v1] Initialization complete');
};

function setupFreeCellMouseHandlers(container) {
    window.freecellMouseHandlers = {
        mouseDown: function(e) {
            const card = e.target.closest('.card:not(.card-empty)');
            if (!card) return;
            
            const cardInfo = getFreeCellCardInfo(card);
            if (!cardInfo) return;
            
            const rect = card.getBoundingClientRect();
            
            window.freecellDragState = {
                isDragging: false,
                isPotentialDrag: true,
                sourceType: cardInfo.sourceType,
                sourceIndex: cardInfo.sourceIndex,
                cardIndex: cardInfo.cardIndex,
                dragElement: null,
                sourceCard: card,
                sourceCardInfo: cardInfo,
                startX: e.clientX,
                startY: e.clientY,
                offsetX: e.clientX - rect.left,
                offsetY: e.clientY - rect.top
            };
        },
        
        mouseMove: function(e) {
            const state = window.freecellDragState;
            
            if (!state.isPotentialDrag && !state.isDragging) return;
            
            const deltaX = e.clientX - state.startX;
            const deltaY = e.clientY - state.startY;
            const distance = Math.sqrt(deltaX * deltaX + deltaY * deltaY);
            
            if (state.isPotentialDrag && !state.isDragging) {
                if (distance >= DRAG_THRESHOLD) {
                    e.preventDefault();
                    startFreeCellDrag(state);
                }
                return;
            }
            
            if (state.isDragging) {
                e.preventDefault();
                updateFreeCellDrag(e.clientX, e.clientY);
            }
        },
        
        mouseUp: function(e) {
            const state = window.freecellDragState;
            
            if (state.isDragging) {
                e.preventDefault();
                endFreeCellDrag(e.clientX, e.clientY);
            } else if (state.isPotentialDrag) {
                window.freecellDragState = {
                    isDragging: false,
                    isPotentialDrag: false,
                    sourceType: null,
                    sourceIndex: null,
                    cardIndex: null,
                    dragElement: null,
                    sourceCard: null,
                    startX: 0,
                    startY: 0,
                    offsetX: 0,
                    offsetY: 0
                };
            }
        }
    };

    container.addEventListener('mousedown', window.freecellMouseHandlers.mouseDown);
    document.addEventListener('mousemove', window.freecellMouseHandlers.mouseMove);
    document.addEventListener('mouseup', window.freecellMouseHandlers.mouseUp);
}

function setupFreeCellTouchHandlers(container) {
    let lastTapTime = 0;
    let lastTapTarget = null;
    const DOUBLE_TAP_DELAY = 300;
    
    window.freecellTouchHandlers = {
        touchStart: function(e) {
            if (e.touches.length !== 1) return;
            
            const touch = e.touches[0];
            const card = document.elementFromPoint(touch.clientX, touch.clientY)?.closest('.card:not(.card-empty)');
            if (!card) return;
            
            const cardInfo = getFreeCellCardInfo(card);
            if (!cardInfo) return;
            
            // Check for double-tap
            const now = Date.now();
            if (lastTapTarget === card && (now - lastTapTime) < DOUBLE_TAP_DELAY) {
                e.preventDefault();
                console.log('[FreeCell JS v1] Double-tap detected');
                
                if (window.freecellBlazorComponent) {
                    window.freecellBlazorComponent.invokeMethodAsync(
                        'OnDoubleClick',
                        cardInfo.sourceType,
                        cardInfo.sourceIndex,
                        cardInfo.cardIndex
                    ).catch(err => console.error('[FreeCell JS v1] Double-tap callback error:', err));
                }
                
                lastTapTime = 0;
                lastTapTarget = null;
                return;
            }
            
            lastTapTime = now;
            lastTapTarget = card;
            
            const rect = card.getBoundingClientRect();
            e.preventDefault();
            
            window.freecellDragState = {
                isDragging: false,
                isPotentialDrag: true,
                sourceType: cardInfo.sourceType,
                sourceIndex: cardInfo.sourceIndex,
                cardIndex: cardInfo.cardIndex,
                dragElement: null,
                sourceCard: card,
                sourceCardInfo: cardInfo,
                startX: touch.clientX,
                startY: touch.clientY,
                offsetX: touch.clientX - rect.left,
                offsetY: touch.clientY - rect.top
            };
        },
        
        touchMove: function(e) {
            const state = window.freecellDragState;
            
            if (!state.isPotentialDrag && !state.isDragging) return;
            if (e.touches.length !== 1) return;
            
            const touch = e.touches[0];
            const deltaX = touch.clientX - state.startX;
            const deltaY = touch.clientY - state.startY;
            const distance = Math.sqrt(deltaX * deltaX + deltaY * deltaY);
            
            if (state.isPotentialDrag && !state.isDragging) {
                if (distance >= DRAG_THRESHOLD) {
                    e.preventDefault();
                    startFreeCellDrag(state);
                    updateFreeCellDrag(touch.clientX, touch.clientY);
                }
                return;
            }
            
            if (state.isDragging) {
                e.preventDefault();
                updateFreeCellDrag(touch.clientX, touch.clientY);
            }
        },
        
        touchEnd: function(e) {
            const state = window.freecellDragState;
            
            if (state.isDragging) {
                e.preventDefault();
                const touch = e.changedTouches[0];
                endFreeCellDrag(touch.clientX, touch.clientY);
            } else if (state.isPotentialDrag) {
                window.freecellDragState = {
                    isDragging: false,
                    isPotentialDrag: false,
                    sourceType: null,
                    sourceIndex: null,
                    cardIndex: null,
                    dragElement: null,
                    sourceCard: null,
                    startX: 0,
                    startY: 0,
                    offsetX: 0,
                    offsetY: 0
                };
            }
        },
        
        touchCancel: function(e) {
            const state = window.freecellDragState;
            
            if (state.isDragging) {
                showFreeCellSourceCards(state.sourceCardInfo);
                if (state.dragElement) {
                    state.dragElement.remove();
                }
            }
            
            window.freecellDragState = {
                isDragging: false,
                isPotentialDrag: false,
                sourceType: null,
                sourceIndex: null,
                cardIndex: null,
                dragElement: null,
                sourceCard: null,
                startX: 0,
                startY: 0,
                offsetX: 0,
                offsetY: 0
            };
            
            document.querySelectorAll('.drop-target-highlight').forEach(el => {
                el.classList.remove('drop-target-highlight');
            });
        }
    };

    container.addEventListener('touchstart', window.freecellTouchHandlers.touchStart, { passive: false });
    container.addEventListener('touchmove', window.freecellTouchHandlers.touchMove, { passive: false });
    container.addEventListener('touchend', window.freecellTouchHandlers.touchEnd, { passive: false });
    container.addEventListener('touchcancel', window.freecellTouchHandlers.touchCancel, { passive: false });
}

function getFreeCellCardInfo(cardElement) {
    // Check free cells
    const freeCell = cardElement.closest('.free-cell');
    if (freeCell) {
        const freeCells = document.querySelectorAll('.free-cell');
        const cellIndex = Array.from(freeCells).indexOf(freeCell);
        return { sourceType: 0, sourceIndex: cellIndex, cardIndex: 0 };
    }
    
    // Check foundations
    const foundationPile = cardElement.closest('.foundation-pile');
    if (foundationPile) {
        const foundations = document.querySelectorAll('.foundation-pile');
        const foundationIndex = Array.from(foundations).indexOf(foundationPile);
        return { sourceType: 2, sourceIndex: foundationIndex, cardIndex: -1 };
    }
    
    // Check tableau
    const tableauColumn = cardElement.closest('.tableau-column');
    if (tableauColumn) {
        const columns = document.querySelectorAll('.tableau-column');
        const columnIndex = Array.from(columns).indexOf(tableauColumn);
        const cards = tableauColumn.querySelectorAll('.tableau-card');
        const cardIndex = Array.from(cards).indexOf(cardElement);
        return { sourceType: 1, sourceIndex: columnIndex, cardIndex: cardIndex };
    }
    
    return null;
}

function startFreeCellDrag(state) {
    console.log('[FreeCell JS v1] Starting drag');
    
    state.isDragging = true;
    state.isPotentialDrag = false;
    
    createFreeCellDragVisual(state.sourceCard, state.sourceCardInfo);
    hideFreeCellSourceCards(state.sourceCardInfo);
}

function createFreeCellDragVisual(cardElement, cardInfo) {
    const existing = document.getElementById('freecell-drag-visual');
    if (existing) existing.remove();
    
    const dragContainer = document.createElement('div');
    dragContainer.id = 'freecell-drag-visual';
    dragContainer.style.cssText = `
        position: fixed;
        pointer-events: none;
        z-index: 10000;
        opacity: 0.9;
        transform: rotate(3deg);
        transition: none;
    `;
    
    // For tableau, get the card and all cards on top
    if (cardInfo.sourceType === 1 && cardInfo.cardIndex >= 0) {
        const column = document.querySelectorAll('.tableau-column')[cardInfo.sourceIndex];
        const cards = column.querySelectorAll('.tableau-card');
        
        for (let i = cardInfo.cardIndex; i < cards.length; i++) {
            const clone = cards[i].cloneNode(true);
            clone.style.position = 'relative';
            clone.style.top = `${(i - cardInfo.cardIndex) * 22}px`;
            clone.style.marginTop = i === cardInfo.cardIndex ? '0' : '-83px';
            clone.style.transform = 'none';
            clone.classList.remove('selected');
            dragContainer.appendChild(clone);
        }
    } else {
        const clone = cardElement.cloneNode(true);
        clone.style.position = 'relative';
        clone.style.transform = 'none';
        clone.classList.remove('selected');
        dragContainer.appendChild(clone);
    }
    
    document.body.appendChild(dragContainer);
    window.freecellDragState.dragElement = dragContainer;
}

function hideFreeCellSourceCards(cardInfo) {
    if (cardInfo.sourceType === 1 && cardInfo.cardIndex >= 0) {
        const column = document.querySelectorAll('.tableau-column')[cardInfo.sourceIndex];
        const cards = column.querySelectorAll('.tableau-card');
        
        for (let i = cardInfo.cardIndex; i < cards.length; i++) {
            cards[i].style.visibility = 'hidden';
        }
    } else if (cardInfo.sourceType === 0) {
        const freeCell = document.querySelectorAll('.free-cell')[cardInfo.sourceIndex];
        const card = freeCell.querySelector('.card:not(.card-empty)');
        if (card) card.style.visibility = 'hidden';
    } else if (cardInfo.sourceType === 2) {
        const foundationPile = document.querySelectorAll('.foundation-pile')[cardInfo.sourceIndex];
        const card = foundationPile.querySelector('.card:not(.card-empty)');
        if (card) card.style.visibility = 'hidden';
    }
}

function showFreeCellSourceCards(cardInfo) {
    if (!cardInfo) return;
    
    if (cardInfo.sourceType === 1 && cardInfo.cardIndex >= 0) {
        const column = document.querySelectorAll('.tableau-column')[cardInfo.sourceIndex];
        if (column) {
            const cards = column.querySelectorAll('.tableau-card');
            for (let i = cardInfo.cardIndex; i < cards.length; i++) {
                cards[i].style.visibility = 'visible';
            }
        }
    } else if (cardInfo.sourceType === 0) {
        const freeCell = document.querySelectorAll('.free-cell')[cardInfo.sourceIndex];
        if (freeCell) {
            const card = freeCell.querySelector('.card:not(.card-empty)');
            if (card) card.style.visibility = 'visible';
        }
    } else if (cardInfo.sourceType === 2) {
        const foundationPile = document.querySelectorAll('.foundation-pile')[cardInfo.sourceIndex];
        if (foundationPile) {
            const card = foundationPile.querySelector('.card:not(.card-empty)');
            if (card) card.style.visibility = 'visible';
        }
    }
}

function updateFreeCellDrag(clientX, clientY) {
    const dragElement = window.freecellDragState.dragElement;
    if (!dragElement) return;
    
    const x = clientX - window.freecellDragState.offsetX;
    const y = clientY - window.freecellDragState.offsetY;
    
    dragElement.style.left = x + 'px';
    dragElement.style.top = y + 'px';
    
    highlightFreeCellDropTargets(clientX, clientY);
}

function highlightFreeCellDropTargets(clientX, clientY) {
    document.querySelectorAll('.drop-target-highlight').forEach(el => {
        el.classList.remove('drop-target-highlight');
    });
    
    const dragElement = window.freecellDragState.dragElement;
    if (dragElement) dragElement.style.display = 'none';
    
    const elementUnder = document.elementFromPoint(clientX, clientY);
    
    if (dragElement) dragElement.style.display = '';
    
    if (!elementUnder) return;
    
    const tableauColumn = elementUnder.closest('.tableau-column');
    const foundationPile = elementUnder.closest('.foundation-pile');
    const freeCell = elementUnder.closest('.free-cell');
    
    if (tableauColumn) {
        tableauColumn.classList.add('drop-target-highlight');
    } else if (foundationPile) {
        foundationPile.classList.add('drop-target-highlight');
    } else if (freeCell) {
        freeCell.classList.add('drop-target-highlight');
    }
}

function endFreeCellDrag(clientX, clientY) {
    const state = window.freecellDragState;
    
    if (state.dragElement) {
        state.dragElement.remove();
    }
    
    document.querySelectorAll('.drop-target-highlight').forEach(el => {
        el.classList.remove('drop-target-highlight');
    });
    
    const elementUnder = document.elementFromPoint(clientX, clientY);
    let dropResult = null;
    
    if (elementUnder) {
        const tableauColumn = elementUnder.closest('.tableau-column');
        const foundationPile = elementUnder.closest('.foundation-pile');
        const freeCell = elementUnder.closest('.free-cell');
        
        if (tableauColumn) {
            const columns = document.querySelectorAll('.tableau-column');
            const columnIndex = Array.from(columns).indexOf(tableauColumn);
            dropResult = { targetType: 1, targetIndex: columnIndex };
        } else if (foundationPile) {
            const foundations = document.querySelectorAll('.foundation-pile');
            const foundationIndex = Array.from(foundations).indexOf(foundationPile);
            dropResult = { targetType: 2, targetIndex: foundationIndex };
        } else if (freeCell) {
            const freeCells = document.querySelectorAll('.free-cell');
            const cellIndex = Array.from(freeCells).indexOf(freeCell);
            dropResult = { targetType: 0, targetIndex: cellIndex };
        }
    }
    
    showFreeCellSourceCards(state.sourceCardInfo || {
        sourceType: state.sourceType,
        sourceIndex: state.sourceIndex,
        cardIndex: state.cardIndex
    });
    
    if (dropResult && window.freecellBlazorComponent) {
        console.log('[FreeCell JS v1] Drop:', {
            source: { type: state.sourceType, index: state.sourceIndex, cardIndex: state.cardIndex },
            target: dropResult
        });
        
        window.freecellBlazorComponent.invokeMethodAsync(
            'OnDragDrop',
            state.sourceType,
            state.sourceIndex,
            state.cardIndex,
            dropResult.targetType,
            dropResult.targetIndex
        ).catch(err => console.error('[FreeCell JS v1] Blazor callback error:', err));
    }
    
    window.freecellDragState = {
        isDragging: false,
        isPotentialDrag: false,
        sourceType: null,
        sourceIndex: null,
        cardIndex: null,
        dragElement: null,
        sourceCard: null,
        startX: 0,
        startY: 0,
        offsetX: 0,
        offsetY: 0
    };
}

// Auto-initialize when DOM is ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', window.initializeFreeCell);
} else {
    setTimeout(window.initializeFreeCell, 100);
}

console.log('[FreeCell JS v1] Loaded');
