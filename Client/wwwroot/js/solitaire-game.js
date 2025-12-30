// Solitaire Game JavaScript - Drag and Drop Support
console.log('[Solitaire JS v3] Loading...');

// Global state for solitaire drag operations
window.solitaireDragState = {
    isDragging: false,
    isPotentialDrag: false, // New: tracks mousedown before threshold
    sourceType: null,
    sourceIndex: null,
    cardIndex: null,
    dragElement: null,
    sourceCard: null,       // Reference to original card element
    startX: 0,
    startY: 0,
    offsetX: 0,
    offsetY: 0
};

// Minimum distance to move before starting a drag (prevents accidental drags on clicks)
const DRAG_THRESHOLD = 5;

// Reference to Blazor component
window.solitaireBlazorComponent = null;

// Register Blazor component for callbacks
window.registerSolitaireBlazorComponent = function (dotNetHelper) {
    window.solitaireBlazorComponent = dotNetHelper;
    console.log('[Solitaire JS v3] Blazor component registered');
};

// Cleanup function - call this when navigating away or reinitializing
window.cleanupSolitaire = function() {
    // Remove any leftover drag visuals
    const dragVisual = document.getElementById('solitaire-drag-visual');
    if (dragVisual) {
        dragVisual.remove();
    }
    
    // Remove any drop target highlights
    document.querySelectorAll('.drop-target-highlight').forEach(el => {
        el.classList.remove('drop-target-highlight');
    });
    
    // Remove touch/mouse handlers if they exist
    const container = document.querySelector('.solitaire-container');
    if (container) {
        if (window.solitaireMouseHandlers) {
            container.removeEventListener('mousedown', window.solitaireMouseHandlers.mouseDown);
        }
        if (window.solitaireTouchHandlers) {
            container.removeEventListener('touchstart', window.solitaireTouchHandlers.touchStart);
            container.removeEventListener('touchmove', window.solitaireTouchHandlers.touchMove);
            container.removeEventListener('touchend', window.solitaireTouchHandlers.touchEnd);
            container.removeEventListener('touchcancel', window.solitaireTouchHandlers.touchCancel);
        }
    }
    if (window.solitaireMouseHandlers) {
        document.removeEventListener('mousemove', window.solitaireMouseHandlers.mouseMove);
        document.removeEventListener('mouseup', window.solitaireMouseHandlers.mouseUp);
    }
    
    // Reset state
    window.solitaireDragState = {
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
    
    console.log('[Solitaire JS v3] Cleanup complete');
};

// Initialize solitaire drag support
window.initializeSolitaire = function () {
    console.log('[Solitaire JS v3] Initializing...');
    
    // Clean up any previous state first
    window.cleanupSolitaire();
    
    const container = document.querySelector('.solitaire-container');
    if (!container) {
        console.log('[Solitaire JS v3] Container not found, retrying...');
        setTimeout(window.initializeSolitaire, 100);
        return;
    }

    // Remove old handlers if they exist
    if (window.solitaireMouseHandlers) {
        container.removeEventListener('mousedown', window.solitaireMouseHandlers.mouseDown);
        document.removeEventListener('mousemove', window.solitaireMouseHandlers.mouseMove);
        document.removeEventListener('mouseup', window.solitaireMouseHandlers.mouseUp);
    }
    if (window.solitaireTouchHandlers) {
        container.removeEventListener('touchstart', window.solitaireTouchHandlers.touchStart);
        container.removeEventListener('touchmove', window.solitaireTouchHandlers.touchMove);
        container.removeEventListener('touchend', window.solitaireTouchHandlers.touchEnd);
    }

    // Set up mouse event handlers
    setupSolitaireMouseHandlers(container);
    
    // Set up touch event handlers
    setupSolitaireTouchHandlers(container);
    
    console.log('[Solitaire JS v3] Initialization complete');
};

function setupSolitaireMouseHandlers(container) {
    window.solitaireMouseHandlers = {
        mouseDown: function(e) {
            const card = e.target.closest('.card:not(.card-empty):not(.card-back)');
            if (!card) return;
            
            if (card.classList.contains('card-facedown')) return;
            
            const cardInfo = getCardInfo(card);
            if (!cardInfo) return;
            
            const rect = card.getBoundingClientRect();
            
            // Mark as potential drag - don't create visual yet
            window.solitaireDragState = {
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
            
            // Don't prevent default here - let click events work normally
        },
        
        mouseMove: function(e) {
            const state = window.solitaireDragState;
            
            if (!state.isPotentialDrag && !state.isDragging) return;
            
            const deltaX = e.clientX - state.startX;
            const deltaY = e.clientY - state.startY;
            const distance = Math.sqrt(deltaX * deltaX + deltaY * deltaY);
            
            // If we haven't started dragging yet, check threshold
            if (state.isPotentialDrag && !state.isDragging) {
                if (distance >= DRAG_THRESHOLD) {
                    // Start actual drag
                    e.preventDefault();
                    startActualDrag(state);
                }
                return;
            }
            
            if (state.isDragging) {
                e.preventDefault();
                updateDrag(e.clientX, e.clientY);
            }
        },
        
        mouseUp: function(e) {
            const state = window.solitaireDragState;
            
            if (state.isDragging) {
                e.preventDefault();
                endDrag(e.clientX, e.clientY);
            } else if (state.isPotentialDrag) {
                // Was a click, not a drag - just reset state
                // Let the Blazor click handler do its thing
                window.solitaireDragState = {
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

    container.addEventListener('mousedown', window.solitaireMouseHandlers.mouseDown);
    document.addEventListener('mousemove', window.solitaireMouseHandlers.mouseMove);
    document.addEventListener('mouseup', window.solitaireMouseHandlers.mouseUp);
}

function setupSolitaireTouchHandlers(container) {
    // Track touch for double-tap detection
    let lastTapTime = 0;
    let lastTapTarget = null;
    const DOUBLE_TAP_DELAY = 300;
    
    window.solitaireTouchHandlers = {
        touchStart: function(e) {
            if (e.touches.length !== 1) return;
            
            const touch = e.touches[0];
            const card = document.elementFromPoint(touch.clientX, touch.clientY)?.closest('.card:not(.card-empty):not(.card-back)');
            if (!card) return;
            
            if (card.classList.contains('card-facedown')) return;
            
            const cardInfo = getCardInfo(card);
            if (!cardInfo) return;
            
            // Check for double-tap
            const now = Date.now();
            if (lastTapTarget === card && (now - lastTapTime) < DOUBLE_TAP_DELAY) {
                // Double-tap detected - auto-move to foundation
                e.preventDefault();
                console.log('[Solitaire JS v3] Double-tap detected');
                
                if (window.solitaireBlazorComponent) {
                    window.solitaireBlazorComponent.invokeMethodAsync(
                        'OnDoubleClick',
                        cardInfo.sourceType,
                        cardInfo.sourceIndex,
                        cardInfo.cardIndex
                    ).catch(err => console.error('[Solitaire JS v3] Double-tap callback error:', err));
                }
                
                lastTapTime = 0;
                lastTapTarget = null;
                return;
            }
            
            lastTapTime = now;
            lastTapTarget = card;
            
            const rect = card.getBoundingClientRect();
            
            // Prevent default to stop scrolling when touching cards
            e.preventDefault();
            
            window.solitaireDragState = {
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
            const state = window.solitaireDragState;
            
            if (!state.isPotentialDrag && !state.isDragging) return;
            if (e.touches.length !== 1) return;
            
            const touch = e.touches[0];
            const deltaX = touch.clientX - state.startX;
            const deltaY = touch.clientY - state.startY;
            const distance = Math.sqrt(deltaX * deltaX + deltaY * deltaY);
            
            if (state.isPotentialDrag && !state.isDragging) {
                if (distance >= DRAG_THRESHOLD) {
                    e.preventDefault();
                    startActualDrag(state);
                    updateDrag(touch.clientX, touch.clientY);
                }
                return;
            }
            
            if (state.isDragging) {
                e.preventDefault();
                updateDrag(touch.clientX, touch.clientY);
            }
        },
        
        touchEnd: function(e) {
            const state = window.solitaireDragState;
            
            if (state.isDragging) {
                e.preventDefault();
                const touch = e.changedTouches[0];
                endDrag(touch.clientX, touch.clientY);
            } else if (state.isPotentialDrag) {
                // Was a tap, not a drag - trigger click behavior via Blazor
                // The tap is handled by Blazor's onclick
                window.solitaireDragState = {
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
            // Handle touch cancel (e.g., when a call comes in)
            const state = window.solitaireDragState;
            
            if (state.isDragging) {
                // Show source cards again
                showSourceCards(state.sourceCardInfo);
                
                // Remove drag visual
                if (state.dragElement) {
                    state.dragElement.remove();
                }
            }
            
            // Reset state
            window.solitaireDragState = {
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
            
            // Remove any highlights
            document.querySelectorAll('.drop-target-highlight').forEach(el => {
                el.classList.remove('drop-target-highlight');
            });
        }
    };

    // Use passive: false for touchstart to allow preventDefault
    container.addEventListener('touchstart', window.solitaireTouchHandlers.touchStart, { passive: false });
    container.addEventListener('touchmove', window.solitaireTouchHandlers.touchMove, { passive: false });
    container.addEventListener('touchend', window.solitaireTouchHandlers.touchEnd, { passive: false });
    container.addEventListener('touchcancel', window.solitaireTouchHandlers.touchCancel, { passive: false });
}

function getCardInfo(cardElement) {
    const wastePile = cardElement.closest('.waste-pile');
    if (wastePile) {
        return { sourceType: 0, sourceIndex: 0, cardIndex: -1 };
    }
    
    const foundationPile = cardElement.closest('.foundation-pile');
    if (foundationPile) {
        const foundations = document.querySelectorAll('.foundation-pile');
        const foundationIndex = Array.from(foundations).indexOf(foundationPile);
        return { sourceType: 2, sourceIndex: foundationIndex, cardIndex: -1 };
    }
    
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

function startActualDrag(state) {
    console.log('[Solitaire JS v3] Starting actual drag');
    
    state.isDragging = true;
    state.isPotentialDrag = false;
    
    // Create visual drag element
    createDragVisual(state.sourceCard, state.sourceCardInfo);
    
    // Hide original cards being dragged
    hideSourceCards(state.sourceCardInfo);
}

function createDragVisual(cardElement, cardInfo) {
    // Remove any existing drag visual first
    const existing = document.getElementById('solitaire-drag-visual');
    if (existing) existing.remove();
    
    const dragContainer = document.createElement('div');
    dragContainer.id = 'solitaire-drag-visual';
    dragContainer.style.cssText = `
        position: fixed;
        pointer-events: none;
        z-index: 10000;
        opacity: 0.9;
        transform: rotate(3deg);
        transition: none;
    `;
    
    if (cardInfo.sourceType === 1 && cardInfo.cardIndex >= 0) {
        const column = document.querySelectorAll('.tableau-column')[cardInfo.sourceIndex];
        const cards = column.querySelectorAll('.tableau-card');
        
        for (let i = cardInfo.cardIndex; i < cards.length; i++) {
            const clone = cards[i].cloneNode(true);
            clone.style.position = 'relative';
            clone.style.top = `${(i - cardInfo.cardIndex) * 25}px`;
            clone.style.marginTop = i === cardInfo.cardIndex ? '0' : '-115px';
            clone.style.transform = 'none'; // Reset any transforms on cloned cards
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
    window.solitaireDragState.dragElement = dragContainer;
}

function hideSourceCards(cardInfo) {
    if (cardInfo.sourceType === 1 && cardInfo.cardIndex >= 0) {
        const column = document.querySelectorAll('.tableau-column')[cardInfo.sourceIndex];
        const cards = column.querySelectorAll('.tableau-card');
        
        for (let i = cardInfo.cardIndex; i < cards.length; i++) {
            cards[i].style.visibility = 'hidden';
        }
    } else if (cardInfo.sourceType === 0) {
        const wasteCard = document.querySelector('.waste-pile .card:not(.card-empty)');
        if (wasteCard) wasteCard.style.visibility = 'hidden';
    } else if (cardInfo.sourceType === 2) {
        const foundationPile = document.querySelectorAll('.foundation-pile')[cardInfo.sourceIndex];
        const card = foundationPile.querySelector('.card:not(.card-empty)');
        if (card) card.style.visibility = 'hidden';
    }
}

function showSourceCards(cardInfo) {
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
        const wasteCard = document.querySelector('.waste-pile .card:not(.card-empty)');
        if (wasteCard) wasteCard.style.visibility = 'visible';
    } else if (cardInfo.sourceType === 2) {
        const foundationPile = document.querySelectorAll('.foundation-pile')[cardInfo.sourceIndex];
        if (foundationPile) {
            const card = foundationPile.querySelector('.card:not(.card-empty)');
            if (card) card.style.visibility = 'visible';
        }
    }
}

function updateDrag(clientX, clientY) {
    const dragElement = window.solitaireDragState.dragElement;
    if (!dragElement) return;
    
    const x = clientX - window.solitaireDragState.offsetX;
    const y = clientY - window.solitaireDragState.offsetY;
    
    dragElement.style.left = x + 'px';
    dragElement.style.top = y + 'px';
    
    highlightDropTargets(clientX, clientY);
}

function highlightDropTargets(clientX, clientY) {
    document.querySelectorAll('.drop-target-highlight').forEach(el => {
        el.classList.remove('drop-target-highlight');
    });
    
    const dragElement = window.solitaireDragState.dragElement;
    if (dragElement) dragElement.style.display = 'none';
    
    const elementUnder = document.elementFromPoint(clientX, clientY);
    
    if (dragElement) dragElement.style.display = '';
    
    if (!elementUnder) return;
    
    const tableauColumn = elementUnder.closest('.tableau-column');
    const foundationPile = elementUnder.closest('.foundation-pile');
    
    if (tableauColumn) {
        tableauColumn.classList.add('drop-target-highlight');
    } else if (foundationPile) {
        foundationPile.classList.add('drop-target-highlight');
    }
}

function endDrag(clientX, clientY) {
    const state = window.solitaireDragState;
    
    // Remove drag visual
    if (state.dragElement) {
        state.dragElement.remove();
    }
    
    // Remove highlights
    document.querySelectorAll('.drop-target-highlight').forEach(el => {
        el.classList.remove('drop-target-highlight');
    });
    
    // Find drop target
    const elementUnder = document.elementFromPoint(clientX, clientY);
    let dropResult = null;
    
    if (elementUnder) {
        const tableauColumn = elementUnder.closest('.tableau-column');
        const foundationPile = elementUnder.closest('.foundation-pile');
        
        if (tableauColumn) {
            const columns = document.querySelectorAll('.tableau-column');
            const columnIndex = Array.from(columns).indexOf(tableauColumn);
            dropResult = { targetType: 1, targetIndex: columnIndex };
        } else if (foundationPile) {
            const foundations = document.querySelectorAll('.foundation-pile');
            const foundationIndex = Array.from(foundations).indexOf(foundationPile);
            dropResult = { targetType: 2, targetIndex: foundationIndex };
        }
    }
    
    // Show source cards again
    showSourceCards(state.sourceCardInfo || {
        sourceType: state.sourceType,
        sourceIndex: state.sourceIndex,
        cardIndex: state.cardIndex
    });
    
    // Notify Blazor only if we have a valid drop
    if (dropResult && window.solitaireBlazorComponent) {
        console.log('[Solitaire JS v3] Drop:', {
            source: { type: state.sourceType, index: state.sourceIndex, cardIndex: state.cardIndex },
            target: dropResult
        });
        
        window.solitaireBlazorComponent.invokeMethodAsync(
            'OnDragDrop',
            state.sourceType,
            state.sourceIndex,
            state.cardIndex,
            dropResult.targetType,
            dropResult.targetIndex
        ).catch(err => console.error('[Solitaire JS v3] Blazor callback error:', err));
    }
    
    // Reset state
    window.solitaireDragState = {
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
    document.addEventListener('DOMContentLoaded', window.initializeSolitaire);
} else {
    setTimeout(window.initializeSolitaire, 100);
}

console.log('[Solitaire JS v3] Loaded');
