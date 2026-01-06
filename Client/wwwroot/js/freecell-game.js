// FreeCell Game JavaScript - Drag and Drop Support + Win Animation
(function() {
    'use strict';
    
    // Prevent multiple initializations
    if (window.freecellGameInitialized) {
        console.log('[FreeCell JS v3] Already initialized, skipping...');
        return;
    }
    window.freecellGameInitialized = true;
    
    console.log('[FreeCell JS v3] Loading...');

    // Minimum distance to move before starting a drag
    const DRAG_THRESHOLD = 5;
    
    // Win animation maximum duration (1 minute to save battery)
    const WIN_ANIMATION_MAX_DURATION_MS = 60000;

    // Win animation state
    let winAnimationId = null;
    let winAnimationTimeout = null;
    let winAnimationMaxTimeout = null; // New: timeout for 1-minute limit
    let bouncingCards = [];

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
        console.log('[FreeCell JS v3] Blazor component registered');
    };

    // Win Animation - Bouncing Cards
    window.startFreeCellWinAnimation = function() {
        console.log('[FreeCell JS v6] Starting win animation (max 1 minute)');
        
        // Stop any existing animation first
        window.stopFreeCellWinAnimation();
        
        const canvas = document.getElementById('win-animation-canvas');
        if (!canvas) {
            console.log('[FreeCell JS v6] Canvas not found');
            return;
        }
        
        // Set a maximum duration timeout to save battery (1 minute)
        winAnimationMaxTimeout = setTimeout(() => {
            console.log('[FreeCell JS v6] Win animation stopped after 1 minute (battery saver)');
            window.stopFreeCellWinAnimation();
        }, WIN_ANIMATION_MAX_DURATION_MS);

        const ctx = canvas.getContext('2d');
        const container = document.querySelector('.freecell-container');
        
        // Set canvas size
        canvas.width = container.offsetWidth;
        canvas.height = container.offsetHeight;

        // Get all card images from the page
        const cardImages = [];
        const cardElements = document.querySelectorAll('.foundation-pile .card img, .foundation-pile .playing-card img');
        cardElements.forEach(img => {
            if (img.complete && img.naturalWidth > 0) {
                cardImages.push(img);
            }
        });

        // Also add some cards from tableau if foundations don't have enough
        if (cardImages.length < 10) {
            document.querySelectorAll('.tableau-card img, .free-cell .card img, .tableau-card .playing-card img').forEach(img => {
                if (img.complete && img.naturalWidth > 0 && cardImages.length < 20) {
                    cardImages.push(img);
                }
            });
        }
        
        console.log('[FreeCell JS v5] Found ' + cardImages.length + ' card images');

        // Create bouncing cards from foundations
        bouncingCards = [];
        
        // Define suits for synthetic cards
        const suits = [
            { symbol: '?', color: '#d40000' },
            { symbol: '?', color: '#d40000' },
            { symbol: '?', color: '#000000' },
            { symbol: '?', color: '#000000' }
        ];
        const ranks = ['A', '2', '3', '4', '5', '6', '7', '8', '9', '10', 'J', 'Q', 'K'];
        
        const numCards = 52;
        const useSyntheticCards = cardImages.length < 4;
        
        console.log('[FreeCell JS v5] Using synthetic cards: ' + useSyntheticCards);
        
        for (let i = 0; i < numCards; i++) {
            const img = cardImages.length > 0 ? cardImages[i % cardImages.length] : null;
            const suit = suits[i % 4];
            const rank = ranks[i % 13];
            
            bouncingCards.push({
                img: img,
                suit: suit.symbol,
                suitColor: suit.color,
                rank: rank,
                useSynthetic: useSyntheticCards || !img,
                x: Math.random() * canvas.width,
                y: -100 - Math.random() * 500, // Start above screen
                vx: (Math.random() - 0.5) * 8,
                vy: Math.random() * 2 + 1,
                rotation: Math.random() * Math.PI * 2,
                rotationSpeed: (Math.random() - 0.5) * 0.2,
                width: 60,
                height: 84,
                gravity: 0.3,
                bounce: 0.7 + Math.random() * 0.2,
                friction: 0.99
            });
        }

        // Animation loop
        function animate() {
            // Check if animation was stopped
            if (winAnimationId === null) {
                return;
            }
            
            ctx.clearRect(0, 0, canvas.width, canvas.height);

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

                // Bounce off bottom
                if (card.y + card.height > canvas.height) {
                    card.y = canvas.height - card.height;
                    card.vy = -card.vy * card.bounce;
                    card.rotationSpeed *= 0.8;
                    
                    // Add some random horizontal velocity on bounce
                    if (Math.abs(card.vy) > 1) {
                        card.vx += (Math.random() - 0.5) * 3;
                    }
                }

                // Bounce off sides
                if (card.x < 0) {
                    card.x = 0;
                    card.vx = -card.vx * card.bounce;
                }
                if (card.x + card.width > canvas.width) {
                    card.x = canvas.width - card.width;
                    card.vx = -card.vx * card.bounce;
                }

                // Check if card is still moving
                if (Math.abs(card.vy) > 0.5 || Math.abs(card.vx) > 0.5 || card.y < canvas.height - card.height - 5) {
                    allSettled = false;
                }

                // Draw card
                ctx.save();
                ctx.translate(card.x + card.width / 2, card.y + card.height / 2);
                ctx.rotate(card.rotation);
                
                if (card.useSynthetic || !card.img) {
                    // Draw synthetic card
                    // White background with rounded corners
                    ctx.fillStyle = '#fff';
                    ctx.beginPath();
                    const r = 4; // corner radius
                    const w = card.width;
                    const h = card.height;
                    const x = -w / 2;
                    const y = -h / 2;
                    ctx.moveTo(x + r, y);
                    ctx.lineTo(x + w - r, y);
                    ctx.quadraticCurveTo(x + w, y, x + w, y + r);
                    ctx.lineTo(x + w, y + h - r);
                    ctx.quadraticCurveTo(x + w, y + h, x + w - r, y + h);
                    ctx.lineTo(x + r, y + h);
                    ctx.quadraticCurveTo(x, y + h, x, y + h - r);
                    ctx.lineTo(x, y + r);
                    ctx.quadraticCurveTo(x, y, x + r, y);
                    ctx.closePath();
                    ctx.fill();
                    
                    // Border
                    ctx.strokeStyle = '#333';
                    ctx.lineWidth = 1;
                    ctx.stroke();
                    
                    // Rank in corner
                    ctx.fillStyle = card.suitColor;
                    ctx.font = 'bold 14px Arial';
                    ctx.textAlign = 'left';
                    ctx.textBaseline = 'top';
                    ctx.fillText(card.rank, x + 4, y + 2);
                    
                    // Suit symbol in corner
                    ctx.font = '12px Arial';
                    ctx.fillText(card.suit, x + 4, y + 16);
                    
                    // Large suit in center
                    ctx.font = '28px Arial';
                    ctx.textAlign = 'center';
                    ctx.textBaseline = 'middle';
                    ctx.fillText(card.suit, 0, 8);
                } else {
                    try {
                        ctx.drawImage(card.img, -card.width / 2, -card.height / 2, card.width, card.height);
                    } catch (e) {
                        // Fallback: draw a colored rectangle
                        ctx.fillStyle = '#fff';
                        ctx.fillRect(-card.width / 2, -card.height / 2, card.width, card.height);
                        ctx.strokeStyle = '#000';
                        ctx.strokeRect(-card.width / 2, -card.height / 2, card.width, card.height);
                    }
                }
                
                ctx.restore();
            });

            // Continue animation or restart after a delay
            if (!allSettled) {
                winAnimationId = requestAnimationFrame(animate);
            } else {
                // All cards settled, wait and restart
                winAnimationTimeout = setTimeout(() => {
                    // Check if animation was stopped during timeout
                    if (winAnimationId === null) {
                        return;
                    }
                    // Reset cards to fall again
                    bouncingCards.forEach(card => {
                        card.y = -100 - Math.random() * 300;
                        card.vy = Math.random() * 2 + 1;
                        card.vx = (Math.random() - 0.5) * 8;
                        card.x = Math.random() * canvas.width;
                    });
                    winAnimationId = requestAnimationFrame(animate);
                }, 2000);
            }
        }

        winAnimationId = requestAnimationFrame(animate);
    };

    window.stopFreeCellWinAnimation = function() {
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
        
        // Cancel max duration timeout if it's set
        if (winAnimationMaxTimeout !== null) {
            clearTimeout(winAnimationMaxTimeout);
            winAnimationMaxTimeout = null;
        }
        
        bouncingCards = [];
        console.log('[FreeCell JS v4] Win animation stopped');
    };

    // Cleanup function
    window.cleanupFreeCell = function() {
        window.stopFreeCellWinAnimation();
        
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
        
        console.log('[FreeCell JS v3] Cleanup complete');
    };

    // Initialize FreeCell drag support
    window.initializeFreeCell = function () {
        console.log('[FreeCell JS v3] Initializing...');
        
        window.cleanupFreeCell();
        
        const container = document.querySelector('.freecell-container');
        if (!container) {
            console.log('[FreeCell JS v3] Container not found, retrying...');
            setTimeout(window.initializeFreeCell, 100);
            return;
        }

        setupFreeCellMouseHandlers(container);
        setupFreeCellTouchHandlers(container);
        
        console.log('[FreeCell JS v3] Initialization complete');
    };

    function setupFreeCellMouseHandlers(container) {
        window.freecellMouseHandlers = {
            mouseDown: function(e) {
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
        
        window.freecellTouchHandlers = {
            touchStart: function(e) {
                if (e.touches.length !== 1) return;
                
                const touch = e.touches[0];
                // Support both .card and .playing-card classes
                const card = document.elementFromPoint(touch.clientX, touch.clientY)?.closest('.playing-card, .card:not(.card-empty)');
                if (!card) return;
                
                const cardInfo = getFreeCellCardInfo(card);
                if (!cardInfo) return;
                
                // Check for double-tap
                const now = Date.now();
                if (lastTapTarget === card && (now - lastTapTime) < DOUBLE_TAP_DELAY) {
                    e.preventDefault();
                    console.log('[FreeCell JS v4] Double-tap detected');
                    
                    if (window.freecellBlazorComponent) {
                        window.freecellBlazorComponent.invokeMethodAsync(
                            'OnDoubleClick',
                            cardInfo.sourceType,
                            cardInfo.sourceIndex,
                            cardInfo.cardIndex
                        ).catch(err => console.error('[FreeCell JS v4] Double-tap callback error:', err));
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
        console.log('[FreeCell JS v3] Starting drag');
        
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
            console.log('[FreeCell JS v3] Drop:', {
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
            ).catch(err => console.error('[FreeCell JS v3] Blazor callback error:', err));
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

    console.log('[FreeCell JS v3] Loaded');
})();
