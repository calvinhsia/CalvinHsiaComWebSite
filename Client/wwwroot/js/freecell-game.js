// FreeCell Game JavaScript - Drag and Drop Support + Win Animation
(function() {
    'use strict';

    // Version tag used in all console.log messages — change in ONE place.
    const VER = '[FreeCell JS v10]';

    // Prevent multiple initializations of the IIFE (script loading)
    if (window.freecellGameInitialized) {
        console.log(VER + ' IIFE already ran, skipping...');
        return;
    }
    window.freecellGameInitialized = true;

    console.log(VER + ' Loading script...');

    // Minimum distance to move before starting a drag
    const DRAG_THRESHOLD = 5;
    
    // Win animation: maximum number of iterations (card drops)
    const WIN_ANIMATION_MAX_ITERATIONS = 3;

    // Win animation state
    let winAnimationId = null;
    let winAnimationTimeout = null;
    let winAnimationIteration = 0;
    let bouncingCards = [];
    
    // Pre-loaded card images for animation
    let preloadedCardImages = [];
    
    // Game won state - disables drag/drop
    window.freecellGameWon = false;
    
    // Auto-solving state - disables drag/drop during auto-move animation
    window.freecellAutoMoving = false;

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

    // Reference to Blazor component
    window.freecellBlazorComponent = null;

    // Register Blazor component for callbacks
    window.registerFreeCellBlazorComponent = function (dotNetHelper) {
        window.freecellBlazorComponent = dotNetHelper;
        console.log(VER + ' Blazor component registered');
    };

    // Expose helper to return serialized FreeCell state JSON from Blazor component
    window.getFreeCellStateJson = async function () {
        try {
            if (window.freecellBlazorComponent && window.freecellBlazorComponent.invokeMethodAsync) {
                // Invoke instance method on registered component
                return await window.freecellBlazorComponent.invokeMethodAsync('GetCurrentFreeCellJson');
            }
            // No instance registered - return empty string
            return '';
        }
        catch (ex) {
            console.log(VER + ' getFreeCellStateJson error: ' + ex);
            return '';
        }
    };

    // Helper function to check if drag/drop should be disabled
    function isDragDropDisabled() {
        // Disable drag/drop when game is won or auto-solving is in progress
        return window.freecellGameWon || 
               window.freecellAutoMoving || 
               document.getElementById('win-animation-canvas') !== null;
    }

    // Pre-load all 52 card images for the win animation
    function preloadCardImages() {
        if (preloadedCardImages.length === 52) {
            console.log(VER + ' Card images already preloaded');
            return Promise.resolve(preloadedCardImages);
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
                        console.warn(VER + ' Failed to load: ' + img.src);
                        resolve(null);
                    };
                });
                img.src = `/img/cards/${rank}${suit}.png`;
                promises.push(promise);
            });
        });
        
        return Promise.all(promises).then(images => {
            preloadedCardImages = images.filter(img => img !== null);
            console.log(VER + ' Preloaded ' + preloadedCardImages.length + ' card images');
            return preloadedCardImages;
        });
    }

    // Win Animation - Bouncing Cards
    window.startFreeCellWinAnimation = function() {
        console.log(VER + ' startFreeCellWinAnimation called');
        
        // Mark game as won to disable drag/drop
        window.freecellGameWon = true;
        
        // Stop any existing animation first
        window.stopFreeCellWinAnimation();
        
        const canvas = document.getElementById('win-animation-canvas');
        if (!canvas) {
            console.log(VER + ' ERROR: Canvas #win-animation-canvas not found in DOM!');
            return;
        }
        
        // Get the game area bounds to constrain animation
        const gameArea = document.querySelector('.freecell-game');
        const container = document.querySelector('.freecell-container');
        
        if (!gameArea && !container) {
            console.log(VER + ' ERROR: Could not find .freecell-game or .freecell-container');
            return;
        }
        
        // Use game area if available, otherwise fall back to container
        const boundsElement = gameArea || container;
        const bounds = boundsElement.getBoundingClientRect();
        
        console.log(VER + ' Animation bounds:', bounds);
        
        // Force inline styles to ensure canvas is visible (overrides any CSS issues)
        canvas.style.cssText = 'position: fixed !important; top: 0 !important; left: 0 !important; width: 100vw !important; height: 100vh !important; z-index: 999999 !important; pointer-events: none; display: block !important; visibility: visible !important;';
        
        // Reset iteration counter
        winAnimationIteration = 1;
        console.log(VER + ' Starting iteration 1 of ' + WIN_ANIMATION_MAX_ITERATIONS);

        const ctx = canvas.getContext('2d');
        if (!ctx) {
            console.log(VER + ' ERROR: Could not get 2D context!');
            return;
        }
        
        // Canvas uses full viewport for drawing, but we'll constrain cards to bounds
        const canvasWidth = window.innerWidth;
        const canvasHeight = window.innerHeight;
        
        canvas.width = canvasWidth;
        canvas.height = canvasHeight;
        
        console.log(VER + ' Canvas size: ' + canvasWidth + 'x' + canvasHeight);

        // Preload all card images, then start animation
        preloadCardImages().then(cardImages => {
            if (cardImages.length === 0) {
                console.log(VER + ' No card images loaded, animation cancelled');
                return;
            }
            
            console.log(VER + ' Starting animation with ' + cardImages.length + ' card images');

            // Create bouncing cards from all 52 cards
            bouncingCards = [];
            
            const numCards = 52;
            
            for (let i = 0; i < numCards; i++) {
                const img = cardImages[i % cardImages.length];
                
                bouncingCards.push({
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
                if (winAnimationId === null) {
                    return;
                }
                
                ctx.clearRect(0, 0, canvasWidth, canvasHeight);

                let allSettled = true;

                bouncingCards.forEach(card => {
                    // Apply gravity
                    card.vy += card.gravity;
                    
                    // Update position
                    card.x += card.vx;
                    card.y += card.vy;
                    card.rotation += card.rotationSpeed;

                    // Apply friction
                    card.vx *= card.friction;

                    // Bounce off bottom of game area
                    if (card.y + card.height > card.boundsBottom) {
                        card.y = card.boundsBottom - card.height;
                        card.vy = -card.vy * card.bounce;
                        card.rotationSpeed *= 0.8;
                        
                        // Add some random horizontal velocity on bounce
                        if (Math.abs(card.vy) > 1) {
                            card.vx += (Math.random() - 0.5) * 3;
                        }
                    }

                    // Bounce off left side of game area
                    if (card.x < card.boundsLeft) {
                        card.x = card.boundsLeft;
                        card.vx = -card.vx * card.bounce;
                    }
                    
                    // Bounce off right side of game area
                    if (card.x + card.width > card.boundsRight) {
                        card.x = card.boundsRight - card.width;
                        card.vx = -card.vx * card.bounce;
                    }

                    // Check if card is still moving
                    if (Math.abs(card.vy) > 0.5 || Math.abs(card.vx) > 0.5 || card.y < card.boundsBottom - card.height - 5) {
                        allSettled = false;
                    }

                    // Draw card using pre-loaded image
                    ctx.save();
                    ctx.translate(card.x + card.width / 2, card.y + card.height / 2);
                    ctx.rotate(card.rotation);
                    
                    if (card.img) {
                        try {
                            ctx.drawImage(card.img, -card.width / 2, -card.height / 2, card.width, card.height);
                        } catch (e) {
                            // Fallback: draw a white rectangle with border
                            ctx.fillStyle = '#fff';
                            ctx.fillRect(-card.width / 2, -card.height / 2, card.width, card.height);
                            ctx.strokeStyle = '#000';
                            ctx.strokeRect(-card.width / 2, -card.height / 2, card.width, card.height);
                        }
                    }
                    
                    ctx.restore();
                });

                // Continue animation or stop after max iterations
                if (!allSettled) {
                    winAnimationId = requestAnimationFrame(animate);
                } else if (winAnimationIteration >= WIN_ANIMATION_MAX_ITERATIONS) {
                    // All iterations complete, stop animation
                    console.log(VER + ' Win animation complete after ' + winAnimationIteration + ' iterations');
                    window.stopFreeCellWinAnimation();
                } else {
                    // More iterations remaining, wait and restart
                    winAnimationIteration++;
                    console.log(VER + ' Starting iteration ' + winAnimationIteration + ' of ' + WIN_ANIMATION_MAX_ITERATIONS);
                    winAnimationTimeout = setTimeout(() => {
                        // Check if animation was stopped during timeout
                        if (winAnimationId === null) {
                            return;
                        }

                        // Get fresh bounds in case window was resized
                        const freshBounds = boundsElement.getBoundingClientRect();

                        // Reset cards to fall again
                        bouncingCards.forEach(card => {
                            card.boundsLeft = freshBounds.left;
                            card.boundsRight = freshBounds.right;
                            card.boundsTop = freshBounds.top;
                            card.boundsBottom = freshBounds.bottom;
                            card.y = freshBounds.top - 100 - Math.random() * 300;
                            card.vy = Math.random() * 2 + 1;
                            card.vx = (Math.random() - 0.5) * 8;
                            card.x = freshBounds.left + Math.random() * freshBounds.width;
                        });
                        winAnimationId = requestAnimationFrame(animate);
                    }, 2000);
                }
            }

            winAnimationId = requestAnimationFrame(animate);
            console.log(VER + ' Animation loop started, winAnimationId=' + winAnimationId + ', bouncingCards.length=' + bouncingCards.length);
        });
    };

    window.stopFreeCellWinAnimation = function() {
        console.log(VER + ' stopFreeCellWinAnimation called, winAnimationId=' + winAnimationId);
        // Cancel animation frame
        if (winAnimationId !== null) {
            cancelAnimationFrame(winAnimationId);
            winAnimationId = null;
        }
        
        // Cancel any pending timeout
        if (winAnimationTimeout !== null) {
            clearTimeout(winAnimationTimeout);
            winAnimationTimeout = null;
        }
        
        // Reset iteration counter
        winAnimationIteration = 0;
        
        bouncingCards = [];
        console.log(VER + ' Win animation stopped');
    };

    // Cleanup function
    window.cleanupFreeCell = function() {
        console.log(VER + ' Starting cleanup...');

        window.stopFreeCellWinAnimation();
        
        // CRITICAL FIX: Reset game won state so drag/drop works after navigating back
        window.freecellGameWon = false;
        window.freecellAutoMoving = false;

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
                // CRITICAL: Must pass { passive: false } to match addEventListener options
                container.removeEventListener('touchstart', window.freecellTouchHandlers.touchStart, { passive: false });
                container.removeEventListener('touchmove', window.freecellTouchHandlers.touchMove, { passive: false });
                container.removeEventListener('touchend', window.freecellTouchHandlers.touchEnd, { passive: false });
                container.removeEventListener('touchcancel', window.freecellTouchHandlers.touchCancel, { passive: false });
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
        
        // Reset initialization flag so event handlers can be re-registered
        window.freecellGameInitialized = false;
        
        console.log(VER + ' Cleanup complete - game state reset');
    };

    // Initialize FreeCell drag support
    window.initializeFreeCell = function () {
        console.log(VER + ' initializeFreeCell called');
        
        const container = document.querySelector('.freecell-container');
        if (!container) {
            console.log(VER + ' Container not found, retrying...');
            setTimeout(window.initializeFreeCell, 100);
            return;
        }
        
        // Check if THIS specific container element already has handlers attached
        // This is critical because Blazor creates NEW elements on navigation
        if (container._freecellHandlersAttached) {
            console.log(VER + ' This container already has handlers, skipping');
            return;
        }
        
        // Mark THIS container element as having handlers
        container._freecellHandlersAttached = true;
        console.log(VER + ' Attaching handlers to container:', container);

        setupFreeCellMouseHandlers(container);
        setupFreeCellTouchHandlers(container);
        
        console.log(VER + ' Handlers attached successfully!');
    };

    function setupFreeCellMouseHandlers(container) {
        window.freecellMouseHandlers = {
            mouseDown: function(e) {
                // Skip drag operations if game is won
                if (isDragDropDisabled()) return;
                
                // Support both .card and .playing-card classes
                const card = e.target.closest('.playing-card, .card:not(.card-empty)');
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
        
        console.log(VER + ' setupFreeCellTouchHandlers - attaching to container');
        
        window.freecellTouchHandlers = {
            touchStart: function(e) {
                console.log(VER + ' touchStart fired! touches:', e.touches.length);
                
                // Skip drag operations if game is won
                if (isDragDropDisabled()) {
                    console.log(VER + ' touchStart - drag/drop disabled, returning');
                    return;
                }
                
                if (e.touches.length !== 1) return;
                
                const touch = e.touches[0];
                // Support both .card and .playing-card classes
                const card = document.elementFromPoint(touch.clientX, touch.clientY)?.closest('.playing-card, .card:not(.card-empty)');
                console.log(VER + ' touchStart - card found:', card);
                if (!card) return;
                
                const cardInfo = getFreeCellCardInfo(card);
                if (!cardInfo) return;
                
                // Check for double-tap
                const now = Date.now();
                if (lastTapTarget === card && (now - lastTapTime) < DOUBLE_TAP_DELAY) {
                    e.preventDefault();
                    console.log(VER + ' Double-tap detected');
                    
                    if (window.freecellBlazorComponent) {
                        window.freecellBlazorComponent.invokeMethodAsync(
                            'OnDoubleClick',
                            cardInfo.sourceType,
                            cardInfo.sourceIndex,
                            cardInfo.cardIndex
                        ).catch(err => console.error(VER + ' Double-tap callback error:', err));
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
        
        // Check tableau - support both .tableau-card class and .playing-card inside tableau
        const tableauColumn = cardElement.closest('.tableau-column');
        if (tableauColumn) {
            const columns = document.querySelectorAll('.tableau-column');
            const columnIndex = Array.from(columns).indexOf(tableauColumn);
            // Find all cards in the column (both .tableau-card and .playing-card)
            const cards = tableauColumn.querySelectorAll('.tableau-card, .playing-card.tableau-card');
            const cardIndex = Array.from(cards).indexOf(cardElement);
            // If cardElement is a .playing-card, find it among playing-cards
            if (cardIndex === -1) {
                const playingCards = tableauColumn.querySelectorAll('.playing-card');
                const pcIndex = Array.from(playingCards).indexOf(cardElement);
                return { sourceType: 1, sourceIndex: columnIndex, cardIndex: pcIndex };
            }
            return { sourceType: 1, sourceIndex: columnIndex, cardIndex: cardIndex };
        }
        
        return null;
    }

    function startFreeCellDrag(state) {
        console.log(VER + ' Starting drag');
        
        state.isDragging = true;
        state.isPotentialDrag = false;
        
        // Notify Blazor to close any open menus
        if (window.freecellBlazorComponent) {
            window.freecellBlazorComponent.invokeMethodAsync('OnDragStart').catch(() => {});
        }
        
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
            display: flex;
            flex-direction: column;
        `;
        
        // For tableau, get the card and all cards on top
        if (cardInfo.sourceType === 1 && cardInfo.cardIndex >= 0) {
            const column = document.querySelectorAll('.tableau-column')[cardInfo.sourceIndex];
            // Support both .tableau-card and .playing-card
            const cards = column.querySelectorAll('.tableau-card, .playing-card.tableau-card');
            
            // Clone cards in order (first card at cardIndex is on top of stack visually but first in DOM)
            for (let i = cardInfo.cardIndex; i < cards.length; i++) {
                const clone = cards[i].cloneNode(true);
                // Reset positioning - use negative margin for overlap effect
                clone.style.cssText = `
                    position: relative !important;
                    top: 0 !important;
                    left: 0 !important;
                    margin-top: ${i === cardInfo.cardIndex ? '0' : '-40px'};
                    transform: none !important;
                    visibility: visible !important;
                    z-index: ${i} !important;
                `;
                clone.classList.remove('selected');
                dragContainer.appendChild(clone);
            }
        } else {
            const clone = cardElement.cloneNode(true);
            clone.style.cssText = `
                position: relative !important;
                top: 0 !important;
                left: 0 !important;
                transform: none !important;
                visibility: visible !important;
            `;
            clone.classList.remove('selected');
            dragContainer.appendChild(clone);
        }
        
        document.body.appendChild(dragContainer);
        window.freecellDragState.dragElement = dragContainer;
    }

    function hideFreeCellSourceCards(cardInfo) {
        if (cardInfo.sourceType === 1 && cardInfo.cardIndex >= 0) {
            const column = document.querySelectorAll('.tableau-column')[cardInfo.sourceIndex];
            // Support both .tableau-card and .playing-card
            const cards = column.querySelectorAll('.tableau-card, .playing-card.tableau-card');
            
            for (let i = cardInfo.cardIndex; i < cards.length; i++) {
                cards[i].style.visibility = 'hidden';
            }
        } else if (cardInfo.sourceType === 0) {
            const freeCell = document.querySelectorAll('.free-cell')[cardInfo.sourceIndex];
            const card = freeCell.querySelector('.playing-card, .card:not(.card-empty)');
            if (card) card.style.visibility = 'hidden';
        } else if (cardInfo.sourceType === 2) {
            const foundationPile = document.querySelectorAll('.foundation-pile')[cardInfo.sourceIndex];
            const card = foundationPile.querySelector('.playing-card, .card:not(.card-empty)');
            if (card) card.style.visibility = 'hidden';
        }
    }

    function showFreeCellSourceCards(cardInfo) {
        if (!cardInfo) return;
        
        if (cardInfo.sourceType === 1 && cardInfo.cardIndex >= 0) {
            const column = document.querySelectorAll('.tableau-column')[cardInfo.sourceIndex];
            if (column) {
                // Support both .tableau-card and .playing-card
                const cards = column.querySelectorAll('.tableau-card, .playing-card.tableau-card');
                for (let i = cardInfo.cardIndex; i < cards.length; i++) {
                    cards[i].style.visibility = 'visible';
                }
            }
        } else if (cardInfo.sourceType === 0) {
            const freeCell = document.querySelectorAll('.free-cell')[cardInfo.sourceIndex];
            if (freeCell) {
                const card = freeCell.querySelector('.playing-card, .card:not(.card-empty)');
                if (card) card.style.visibility = 'visible';
            }
        } else if (cardInfo.sourceType === 2) {
            const foundationPile = document.querySelectorAll('.foundation-pile')[cardInfo.sourceIndex];
            if (foundationPile) {
                const card = foundationPile.querySelector('.playing-card, .card:not(.card-empty)');
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
            console.log(VER + ' Drop:', {
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
            ).catch(err => console.error(VER + ' Blazor callback error:', err));
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

    // Set FreeCell game state from JSON (called from tests to load a custom position)
    window.setFreeCellStateJson = async function (json) {
        try {
            if (window.freecellBlazorComponent && window.freecellBlazorComponent.invokeMethodAsync) {
                var result = await window.freecellBlazorComponent.invokeMethodAsync('LoadGameFromJson', json);
                console.log(VER + ' setFreeCellStateJson result: ' + result);
                return result;
            }
            console.warn(VER + ' setFreeCellStateJson: Blazor component not registered');
            return false;
        } catch (err) {
            console.error(VER + ' setFreeCellStateJson error:', err);
            return false;
        }
    };

    // Reset game won state for new games
    window.resetFreeCellGameState = function() {
        window.freecellGameWon = false;
        window.freecellAutoMoving = false;
        console.log(VER + ' Game state reset');
    };
    
    // Set auto-solving state (called from Blazor during auto-solve animation)
    window.setFreeCellAutoMoving = function(isAutoMoving) {
        window.freecellAutoMoving = isAutoMoving;
        console.log(VER + ' Auto-moving: ' + isAutoMoving);
    };

    // â”€â”€ Card move animation â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Animates a card image flying from its current DOM position to the
    // target pile/column.  Returns a Promise that resolves when the
    // animation finishes (or immediately if elements can't be found).

    function getCardElementForAnimation(sourceType, sourceIndex, cardIndex) {
        if (sourceType === 0) { // FreeCell
            const cell = document.querySelectorAll('.free-cell')[sourceIndex];
            return cell ? cell.querySelector('.playing-card') : null;
        }
        if (sourceType === 1) { // Tableau
            const col = document.querySelectorAll('.tableau-column')[sourceIndex];
            if (!col) return null;
            const cards = col.querySelectorAll('.playing-card');
            return cards[cardIndex] || cards[cards.length - 1] || null;
        }
        if (sourceType === 2) { // Foundation
            const pile = document.querySelectorAll('.foundation-pile')[sourceIndex];
            return pile ? pile.querySelector('.playing-card') : null;
        }
        return null;
    }

    function getTargetRectForAnimation(targetType, targetIndex) {
        if (targetType === 0) { // FreeCell
            const cell = document.querySelectorAll('.free-cell')[targetIndex];
            if (!cell) return null;
            const card = cell.querySelector('.playing-card');
            return (card || cell).getBoundingClientRect();
        }
        if (targetType === 1) { // Tableau
            const col = document.querySelectorAll('.tableau-column')[targetIndex];
            if (!col) return null;
            const cards = col.querySelectorAll('.playing-card');
            if (cards.length > 0) {
                // Land just below the last card (offset by the row gap)
                const last = cards[cards.length - 1];
                const r = last.getBoundingClientRect();
                // Read the CSS --row-offset that Blazor applies (defaults to ~22px)
                const style = getComputedStyle(last);
                const topVal = parseFloat(style.top) || 0;
                // The next card position is one row-offset below the last card's top
                const rowOffset = cards.length >= 2
                    ? cards[1].getBoundingClientRect().top - cards[0].getBoundingClientRect().top
                    : 22;
                return new DOMRect(r.left, r.top + rowOffset, r.width, r.height);
            }
            // Empty column â€“ land on the column placeholder
            const empty = col.querySelector('.card-empty') || col;
            return empty.getBoundingClientRect();
        }
        if (targetType === 2) { // Foundation
            const pile = document.querySelectorAll('.foundation-pile')[targetIndex];
            if (!pile) return null;
            const card = pile.querySelector('.playing-card');
            return (card || pile).getBoundingClientRect();
        }
        return null;
    }

    window.animateFreeCellCard = function(sourceType, sourceIndex, cardIndex, targetType, targetIndex, cardImageUrl, durationMs) {
        return new Promise(function(resolve) {
            var srcEl = getCardElementForAnimation(sourceType, sourceIndex, cardIndex);
            if (!srcEl) { resolve(); return; }

            var srcRect = srcEl.getBoundingClientRect();
            var dstRect = getTargetRectForAnimation(targetType, targetIndex);
            if (!dstRect) { resolve(); return; }

            // Skip animation if source and destination are essentially the same spot
            var dx = dstRect.left - srcRect.left;
            var dy = dstRect.top - srcRect.top;
            if (Math.abs(dx) < 2 && Math.abs(dy) < 2) { resolve(); return; }

            // Create flying card
            var fly = document.createElement('img');
            fly.src = cardImageUrl;
            fly.style.cssText =
                'position:fixed;z-index:10000;pointer-events:none;border-radius:6px;' +
                'width:' + srcRect.width + 'px;height:' + srcRect.height + 'px;' +
                'left:' + srcRect.left + 'px;top:' + srcRect.top + 'px;' +
                'transition:left ' + durationMs + 'ms ease-in-out,top ' + durationMs + 'ms ease-in-out;';
            document.body.appendChild(fly);

            // Hide source card so it looks like the flying card replaces it
            srcEl.style.visibility = 'hidden';

            // Force layout then start transition
            fly.getBoundingClientRect();
            fly.style.left = dstRect.left + 'px';
            fly.style.top = dstRect.top + 'px';

            var done = false;
            function finish() {
                if (done) return;
                done = true;
                fly.remove();
                srcEl.style.visibility = '';
                resolve();
            }
            fly.addEventListener('transitionend', finish, { once: true });
            // Safety timeout in case transitionend doesn't fire
            setTimeout(finish, durationMs + 80);
        });
    };

    console.log(VER + ' Loaded');
})();
