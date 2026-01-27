// Solitaire Game JavaScript - Drag and Drop Support
console.log('[Solitaire JS v6] Loading...');

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
    console.log('[Solitaire JS v6] Blazor component registered');
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
    
    console.log('[Solitaire JS v6] Cleanup complete');
};

// Initialize solitaire drag support
window.initializeSolitaire = function (retryCount = 0) {
    console.log('[Solitaire JS v6] Initializing...');
    
    // Clean up any previous state first
    window.cleanupSolitaire();
    
    const container = document.querySelector('.solitaire-container');
    if (!container) {
        // Limit retries to prevent infinite loop when on a different page
        if (retryCount < 5) {
            console.log(`[Solitaire JS v6] Container not found, retry ${retryCount + 1}/5...`);
            setTimeout(() => window.initializeSolitaire(retryCount + 1), 100);
        } else {
            console.log('[Solitaire JS v6] Container not found after 5 retries, stopping.');
        }
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
    
    console.log('[Solitaire JS v6] Initialization complete');
};

function setupSolitaireMouseHandlers(container) {
    window.solitaireMouseHandlers = {
        mouseDown: function(e) {
            console.log('[Solitaire JS v6] mouseDown on:', e.target.tagName, e.target.className);
            
            // Support both .card and .playing-card classes (PlayingCard component uses .playing-card)
            const card = e.target.closest('.playing-card, .card:not(.card-empty):not(.card-back)');
            if (!card) {
                console.log('[Solitaire JS v6] No card found at click target');
                return;
            }
            
            console.log('[Solitaire JS v6] Card found:', card.className);
            
            if (card.classList.contains('card-facedown')) {
                console.log('[Solitaire JS v6] Card is facedown, ignoring');
                return;
            }
            
            const cardInfo = getCardInfo(card);
            if (!cardInfo) {
                console.log('[Solitaire JS v6] Could not get card info');
                return;
            }
            
            console.log('[Solitaire JS v6] Card info:', cardInfo);
            
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
            
            console.log('[Solitaire JS v6] Potential drag started');
            
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
            console.log('[Solitaire JS v6] touchStart at:', touch.clientX, touch.clientY);
            
            // Support both .card and .playing-card classes
            const card = document.elementFromPoint(touch.clientX, touch.clientY)?.closest('.playing-card, .card:not(.card-empty):not(.card-back)');
            if (!card) {
                console.log('[Solitaire JS v6] No card found at touch point');
                return;
            }
            
            console.log('[Solitaire JS v6] Card found:', card.className);
            
            if (card.classList.contains('card-facedown')) {
                console.log('[Solitaire JS v6] Card is facedown, ignoring');
                return;
            }
            
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
    console.log('[Solitaire JS v6] getCardInfo for:', cardElement.className);
    
    const wastePile = cardElement.closest('.waste-pile');
    if (wastePile) {
        console.log('[Solitaire JS v6] Card is in waste pile');
        return { sourceType: 0, sourceIndex: 0, cardIndex: -1 };
    }
    
    const foundationPile = cardElement.closest('.foundation-pile');
    if (foundationPile) {
        const foundations = document.querySelectorAll('.foundation-pile');
        const foundationIndex = Array.from(foundations).indexOf(foundationPile);
        console.log('[Solitaire JS v6] Card is in foundation', foundationIndex);
        return { sourceType: 2, sourceIndex: foundationIndex, cardIndex: -1 };
    }
    
    const tableauColumn = cardElement.closest('.tableau-column');
    if (tableauColumn) {
        const columns = document.querySelectorAll('.tableau-column');
        const columnIndex = Array.from(columns).indexOf(tableauColumn);
        // Support both .tableau-card and .playing-card classes
        const cards = tableauColumn.querySelectorAll('.tableau-card, .playing-card');
        const cardIndex = Array.from(cards).indexOf(cardElement);
        console.log('[Solitaire JS v6] Card is in tableau column', columnIndex, 'at index', cardIndex);
        return { sourceType: 1, sourceIndex: columnIndex, cardIndex: cardIndex };
    }
    
    console.log('[Solitaire JS v6] Card not found in any pile');
    return null;
}

function startActualDrag(state) {
    console.log('[Solitaire JS v6] Starting actual drag');
    
    state.isDragging = true;
    state.isPotentialDrag = false;
    
    // Create visual drag element
    createDragVisual(state.sourceCard, state.sourceCardInfo);
    
    // Hide original cards being dragged
    hideSourceCards(state.sourceCardInfo);
}

function createDragVisual(cardElement, cardInfo) {
    console.log('[Solitaire JS v6] createDragVisual for:', cardInfo);
    
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
        // Support both .tableau-card and .playing-card classes
        const cards = column.querySelectorAll('.playing-card.tableau-card, .tableau-card');
        console.log('[Solitaire JS v6] Found', cards.length, 'cards in column, starting from index', cardInfo.cardIndex);
        
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
    console.log('[Solitaire JS v6] hideSourceCards for:', cardInfo);
    
    if (cardInfo.sourceType === 1 && cardInfo.cardIndex >= 0) {
        const column = document.querySelectorAll('.tableau-column')[cardInfo.sourceIndex];
        // Support both .tableau-card and .playing-card classes
        const cards = column.querySelectorAll('.playing-card.tableau-card, .tableau-card');
        console.log('[Solitaire JS v6] Hiding', cards.length - cardInfo.cardIndex, 'cards starting from index', cardInfo.cardIndex);
        
        for (let i = cardInfo.cardIndex; i < cards.length; i++) {
            cards[i].style.visibility = 'hidden';
        }
    } else if (cardInfo.sourceType === 0) {
        // Waste pile - hide only the TOP card (the one with .waste-top-card class)
        const topWasteCard = document.querySelector('.waste-pile .waste-top-card');
        if (topWasteCard) {
            console.log('[Solitaire JS v6] Hiding top waste card');
            topWasteCard.style.visibility = 'hidden';
        } else {
            // Fallback to any playing-card if waste-top-card not found
            const wasteCards = document.querySelectorAll('.waste-pile .playing-card');
            if (wasteCards.length > 0) {
                const lastCard = wasteCards[wasteCards.length - 1];
                console.log('[Solitaire JS v6] Hiding last waste card (fallback)');
                lastCard.style.visibility = 'hidden';
            }
        }
    } else if (cardInfo.sourceType === 2) {
        const foundationPile = document.querySelectorAll('.foundation-pile')[cardInfo.sourceIndex];
        // Support both .card and .playing-card
        const card = foundationPile.querySelector('.playing-card, .card:not(.card-empty)');
        if (card) {
            console.log('[Solitaire JS v6] Hiding foundation card');
            card.style.visibility = 'hidden';
        }
    }
}

function showSourceCards(cardInfo) {
    if (!cardInfo) return;
    
    console.log('[Solitaire JS v6] showSourceCards for:', cardInfo);
    
    if (cardInfo.sourceType === 1 && cardInfo.cardIndex >= 0) {
        const column = document.querySelectorAll('.tableau-column')[cardInfo.sourceIndex];
        if (column) {
            // Support both .tableau-card and .playing-card classes
            const cards = column.querySelectorAll('.playing-card.tableau-card, .tableau-card');
            for (let i = cardInfo.cardIndex; i < cards.length; i++) {
                cards[i].style.visibility = 'visible';
            }
        }
    } else if (cardInfo.sourceType === 0) {
        // Waste pile - show the top card
        const topWasteCard = document.querySelector('.waste-pile .waste-top-card');
        if (topWasteCard) {
            topWasteCard.style.visibility = 'visible';
        } else {
            // Fallback
            const wasteCards = document.querySelectorAll('.waste-pile .playing-card');
            if (wasteCards.length > 0) {
                wasteCards[wasteCards.length - 1].style.visibility = 'visible';
            }
        }
    } else if (cardInfo.sourceType === 2) {
        const foundationPile = document.querySelectorAll('.foundation-pile')[cardInfo.sourceIndex];
        if (foundationPile) {
            // Support both .card and .playing-card
            const card = foundationPile.querySelector('.playing-card, .card:not(.card-empty)');
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

// ==================== WIN ANIMATION ====================
// Win animation maximum duration (1 minute to save battery)
const WIN_ANIMATION_MAX_DURATION_MS = 60000;

// Win animation state
let solitaireWinAnimationId = null;
let solitaireWinAnimationMaxTimeout = null;
let solitaireBouncingCards = [];
let solitairePreloadedCardImages = [];

// Pre-load all 52 card images for the win animation
function preloadSolitaireCardImages() {
    if (solitairePreloadedCardImages.length === 52) {
        console.log('[Solitaire JS v6] Card images already preloaded');
        return Promise.resolve(solitairePreloadedCardImages);
    }
    
    const suits = ['H', 'D', 'C', 'S']; // Hearts, Diamonds, Clubs, Spades
    const ranks = ['A', '2', '3', '4', '5', '6', '7', '8', '9', '0', 'J', 'Q', 'K']; // 0 = 10
    const promises = [];
    
    suits.forEach(suit => {
        ranks.forEach(rank => {
            const img = new Image();
            const promise = new Promise((resolve) => {
                img.onload = () => resolve(img);
                img.onerror = () => {
                    console.warn('[Solitaire JS v6] Failed to load: ' + img.src);
                    resolve(null);
                };
            });
            img.src = `/img/cards/${rank}${suit}.png`;
            promises.push(promise);
        });
    });
    
    return Promise.all(promises).then(images => {
        solitairePreloadedCardImages = images.filter(img => img !== null);
        console.log('[Solitaire JS v6] Preloaded ' + solitairePreloadedCardImages.length + ' card images');
        return solitairePreloadedCardImages;
    });
}

// Win Animation - Bouncing Cards
window.startSolitaireWinAnimation = function() {
    console.log('[Solitaire JS v6] startSolitaireWinAnimation called');
    
    // Stop any existing animation first
    window.stopSolitaireWinAnimation();
    
    const canvas = document.getElementById('solitaire-win-animation-canvas');
    if (!canvas) {
        console.log('[Solitaire JS v6] ERROR: Canvas #solitaire-win-animation-canvas not found in DOM!');
        return;
    }
    
    // Get the game area bounds to constrain animation
    const gameArea = document.querySelector('.solitaire-game');
    const container = document.querySelector('.solitaire-container');
    
    if (!gameArea && !container) {
        console.log('[Solitaire JS v6] ERROR: Could not find .solitaire-game or .solitaire-container');
        return;
    }
    
    // Use game area if available, otherwise fall back to container
    const boundsElement = gameArea || container;
    const bounds = boundsElement.getBoundingClientRect();
    
    console.log('[Solitaire JS v6] Animation bounds:', bounds);
    
    // Force inline styles to ensure canvas is visible
    canvas.style.cssText = 'position: fixed !important; top: 0 !important; left: 0 !important; width: 100vw !important; height: 100vh !important; z-index: 999999 !important; pointer-events: none; display: block !important; visibility: visible !important;';
    
    // Set a maximum duration timeout to save battery (1 minute)
    solitaireWinAnimationMaxTimeout = setTimeout(() => {
        console.log('[Solitaire JS v6] Win animation stopped after 1 minute (battery saver)');
        window.stopSolitaireWinAnimation();
    }, WIN_ANIMATION_MAX_DURATION_MS);

    const ctx = canvas.getContext('2d');
    if (!ctx) {
        console.log('[Solitaire JS v6] ERROR: Could not get 2D context!');
        return;
    }
    
    // Canvas uses full viewport for drawing
    const canvasWidth = window.innerWidth;
    const canvasHeight = window.innerHeight;
    
    canvas.width = canvasWidth;
    canvas.height = canvasHeight;
    
    console.log('[Solitaire JS v6] Canvas size: ' + canvasWidth + 'x' + canvasHeight);

    // Preload all card images, then start animation
    preloadSolitaireCardImages().then(cardImages => {
        if (cardImages.length === 0) {
            console.log('[Solitaire JS v6] No card images loaded, animation cancelled');
            return;
        }
        
        console.log('[Solitaire JS v6] Starting animation with ' + cardImages.length + ' card images');

        // Create bouncing cards from all 52 cards
        solitaireBouncingCards = [];
        
        const numCards = 52;
        
        for (let i = 0; i < numCards; i++) {
            const img = cardImages[i % cardImages.length];
            
            solitaireBouncingCards.push({
                img: img,
                // Start cards above the game area, spread across its width
                x: bounds.left + Math.random() * bounds.width,
                y: bounds.top - 100 - Math.random() * 500, // Start above the bounds
                vx: (Math.random() - 0.5) * 8,
                vy: Math.random() * 2 + 1,
                rotation: Math.random() * Math.PI * 2,
                rotationSpeed: (Math.random() - 0.5) * 0.2,
                width: 60,
                height: 84,
                gravity: 0.3,
                bounce: 0.7 + Math.random() * 0.2,
                friction: 0.99,
                // Store bounds for this card
                boundsLeft: bounds.left,
                boundsRight: bounds.right,
                boundsTop: bounds.top,
                boundsBottom: bounds.bottom
            });
        }

        // Animation loop
        function animate() {
            // Check if animation was stopped
            if (solitaireWinAnimationId === null) {
                return;
            }
            
            ctx.clearRect(0, 0, canvasWidth, canvasHeight);
            
            solitaireBouncingCards.forEach(card => {
                // Apply gravity
                card.vy += card.gravity;
                
                // Apply velocity
                card.x += card.vx;
                card.y += card.vy;
                
                // Apply rotation
                card.rotation += card.rotationSpeed;
                
                // Bounce off bottom
                if (card.y + card.height > canvasHeight) {
                    card.y = canvasHeight - card.height;
                    card.vy *= -card.bounce;
                    card.vx *= card.friction;
                    card.rotationSpeed *= 0.8;
                }
                
                // Bounce off sides
                if (card.x < 0) {
                    card.x = 0;
                    card.vx *= -card.bounce;
                } else if (card.x + card.width > canvasWidth) {
                    card.x = canvasWidth - card.width;
                    card.vx *= -card.bounce;
                }
                
                // Draw card with rotation
                ctx.save();
                ctx.translate(card.x + card.width / 2, card.y + card.height / 2);
                ctx.rotate(card.rotation);
                ctx.drawImage(card.img, -card.width / 2, -card.height / 2, card.width, card.height);
                ctx.restore();
            });
            
            solitaireWinAnimationId = requestAnimationFrame(animate);
        }
        
        solitaireWinAnimationId = requestAnimationFrame(animate);
    });
};

// Stop win animation
window.stopSolitaireWinAnimation = function() {
    console.log('[Solitaire JS v6] stopSolitaireWinAnimation called');
    
    if (solitaireWinAnimationId !== null) {
        cancelAnimationFrame(solitaireWinAnimationId);
        solitaireWinAnimationId = null;
    }
    
    if (solitaireWinAnimationMaxTimeout !== null) {
        clearTimeout(solitaireWinAnimationMaxTimeout);
        solitaireWinAnimationMaxTimeout = null;
    }
    
    solitaireBouncingCards = [];
    
    // Clear canvas if it exists
    const canvas = document.getElementById('solitaire-win-animation-canvas');
    if (canvas) {
        const ctx = canvas.getContext('2d');
        if (ctx) {
            ctx.clearRect(0, 0, canvas.width, canvas.height);
        }
    }
};

// Auto-initialize when DOM is ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', window.initializeSolitaire);
} else {
    setTimeout(window.initializeSolitaire, 100);
}

console.log('[Solitaire JS v6] Loaded');
