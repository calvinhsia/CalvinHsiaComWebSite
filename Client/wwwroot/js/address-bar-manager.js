// Mobile Browser Address Bar Management for WordScape Game
// Specifically optimized for Android Edge browser

// Global state for address bar management
window.addressBarManager = {
    isAndroidEdge: false,
    initialized: false,
    fullscreenRequested: false,
    addressBarPosition: 'unknown'
};

// Detect Android Edge browser
function detectAndroidEdge() {
    const userAgent = navigator.userAgent;
    const isAndroid = /Android/i.test(userAgent);
    const isEdge = /Edg\//i.test(userAgent); // Edge uses "Edg/" in user agent
    
    window.addressBarManager.isAndroidEdge = isAndroid && isEdge;
    
    if (window.addressBarManager.isAndroidEdge) {
        console.log('?? Android Edge detected - will optimize address bar for WordScape game');
    }
    
    return window.addressBarManager.isAndroidEdge;
}

// Request fullscreen mode to encourage address bar repositioning
async function requestOptimalViewport() {
    if (!window.addressBarManager.isAndroidEdge) {
        return false;
    }

    try {
        console.log('?? Requesting optimal viewport for Android Edge...');

        // Method 1: Try to enter fullscreen (this often triggers address bar behavior)
        if (document.documentElement.requestFullscreen && !window.addressBarManager.fullscreenRequested) {
            window.addressBarManager.fullscreenRequested = true;
            
            // Request fullscreen briefly to trigger address bar positioning dialog
            await document.documentElement.requestFullscreen();
            
            // Exit fullscreen after a brief moment - this often causes the browser
            // to show the address bar positioning options
            setTimeout(async () => {
                try {
                    if (document.exitFullscreen && document.fullscreenElement) {
                        await document.exitFullscreen();
                        console.log('?? Fullscreen toggle completed - address bar options may now be available');
                    }
                } catch (error) {
                    console.log('?? Fullscreen exit completed naturally');
                }
            }, 1000);
            
            return true;
        }

        // Method 2: Use Screen Orientation API to trigger browser UI refresh
        if (screen.orientation && screen.orientation.lock) {
            try {
                await screen.orientation.lock('portrait-primary');
                console.log('?? Screen orientation locked to portrait - may help with address bar positioning');
            } catch (orientationError) {
                console.log('?? Screen orientation lock not available or denied');
            }
        }

        // Method 3: Use viewport-fit=cover meta tag manipulation
        const viewportMeta = document.querySelector('meta[name="viewport"]');
        if (viewportMeta) {
            const currentContent = viewportMeta.getAttribute('content');
            if (!currentContent.includes('viewport-fit=cover')) {
                viewportMeta.setAttribute('content', currentContent + ', viewport-fit=cover');
                console.log('?? Added viewport-fit=cover to encourage full viewport usage');
            }
        }

        return true;
    } catch (error) {
        console.log('?? Error requesting optimal viewport:', error.message);
        return false;
    }
}

// Initialize address bar management for supported games
function initializeAddressBarManagement() {
    if (window.addressBarManager.initialized) {
        return;
    }

    console.log('?? Initializing address bar management...');
    
    window.addressBarManager.initialized = true;
    
    // Detect if we're on Android Edge
    if (detectAndroidEdge()) {
        const currentPath = window.location.pathname;
        const supportedGames = ['/wordscape', '/wordament'];
        const isOnSupportedGame = supportedGames.some(game => currentPath.includes(game));
        
        if (isOnSupportedGame) {
            const gameName = currentPath.includes('/wordscape') ? 'WordScape' : 
                           currentPath.includes('/wordament') ? 'Wordament' : 'Game';
            
            console.log(`?? Android Edge detected - setting up address bar optimizations for ${gameName}`);
            
            // Apply optimizations
            optimizeForAddressBarTop();
            
            // Start monitoring viewport changes
            monitorViewportChanges();
            
            // Show helpful hint to user (delayed to not interfere with game loading)
            setTimeout(() => {
                showAddressBarHint(gameName);
            }, 3000);
            
            // Try to request optimal viewport after a delay
            setTimeout(async () => {
                await requestOptimalViewport();
            }, 2000);
        } else {
            console.log('?? Android Edge detected but not on a supported game page');
        }
    }
}

// Show helpful message to user about address bar positioning
function showAddressBarHint(gameName = 'Game') {
    if (!window.addressBarManager.isAndroidEdge) {
        return;
    }

    // Only show hint once per session
    if (sessionStorage.getItem('addressBarHintShown')) {
        return;
    }

    const hintElement = document.createElement('div');
    hintElement.id = 'address-bar-hint';
    hintElement.innerHTML = `
        <div style="
            position: fixed;
            top: 10px;
            left: 10px;
            right: 10px;
            background: #007bff;
            color: white;
            padding: 12px;
            border-radius: 8px;
            font-size: 14px;
            z-index: 10000;
            box-shadow: 0 2px 10px rgba(0,0,0,0.3);
            text-align: center;
            animation: slideDown 0.3s ease-out;
        ">
            <div style="font-weight: bold; margin-bottom: 5px;">?? More Screen Space Available!</div>
            <div style="font-size: 12px; line-height: 1.3;">
                For the best ${gameName} experience, hold the address bar and select <strong>"Move address bar to the top"</strong> from the menu. 
                This gives you more vertical space as the address bar scrolls out of view!
            </div>
            <button onclick="document.getElementById('address-bar-hint').remove(); sessionStorage.setItem('addressBarHintShown', 'true');" 
                    style="
                        background: rgba(255,255,255,0.2);
                        border: none;
                        color: white;
                        padding: 5px 10px;
                        border-radius: 4px;
                        margin-top: 8px;
                        cursor: pointer;
                        font-size: 12px;
                    ">Got it!</button>
        </div>
        <style>
            @keyframes slideDown {
                from { transform: translateY(-100%); opacity: 0; }
                to { transform: translateY(0); opacity: 1; }
            }
        </style>
    `;

    document.body.appendChild(hintElement);
    
    // Auto-remove after 10 seconds
    setTimeout(() => {
        if (document.getElementById('address-bar-hint')) {
            document.getElementById('address-bar-hint').remove();
            sessionStorage.setItem('addressBarHintShown', 'true');
        }
    }, 10000);

    sessionStorage.setItem('addressBarHintShown', 'true');
    console.log(`?? Address bar positioning hint displayed for ${gameName}`);
}

// Detect viewport changes that might indicate address bar position changes
function monitorViewportChanges() {
    if (!window.addressBarManager.isAndroidEdge) {
        return;
    }

    let lastViewportHeight = window.visualViewport ? window.visualViewport.height : window.innerHeight;
    
    function checkViewportChange() {
        const currentViewportHeight = window.visualViewport ? window.visualViewport.height : window.innerHeight;
        const heightDifference = Math.abs(currentViewportHeight - lastViewportHeight);
        
        // Significant height change might indicate address bar repositioning
        if (heightDifference > 50) {
            console.log(`?? Viewport height changed significantly: ${lastViewportHeight} ? ${currentViewportHeight}`);
            
            // Apply Android positioning fixes when viewport changes
            setTimeout(() => {
                if (typeof window.fixAndroidGridPosition === 'function') {
                    window.fixAndroidGridPosition();
                }
                if (typeof window.forceAndroidFullWidth === 'function') {
                    window.forceAndroidFullWidth();
                }
            }, 300);
        }
        
        lastViewportHeight = currentViewportHeight;
    }

    // Monitor viewport changes
    if (window.visualViewport) {
        window.visualViewport.addEventListener('resize', checkViewportChange);
        window.visualViewport.addEventListener('scroll', checkViewportChange);
    }
    
    window.addEventListener('resize', checkViewportChange);
    window.addEventListener('orientationchange', () => {
        setTimeout(checkViewportChange, 500);
    });

    console.log('?? Viewport change monitoring enabled for address bar detection');
}

// Try to programmatically influence address bar behavior  
function optimizeForAddressBarTop() {
    if (!window.addressBarManager.isAndroidEdge) {
        return;
    }

    console.log('?? Applying optimizations for top address bar positioning...');

    // Method 1: Scroll to top to encourage address bar to show
    window.scrollTo(0, 0);
    
    // Method 2: Add CSS to encourage full viewport usage
    const style = document.createElement('style');
    style.id = 'address-bar-optimization';
    style.textContent = `
        /* Optimize for address bar at top */
        html, body {
            height: 100vh !important;
            height: 100svh !important; /* Use small viewport height when available */
            overflow-x: hidden !important;
        }
        
        /* Use dynamic viewport units when available */
        @supports (height: 100dvh) {
            html, body {
                height: 100dvh !important;
            }
        }
        
        /* Encourage browser to hide address bar when scrolling */
        .wordscape-fixed-game {
            min-height: 100vh !important;
            min-height: 100svh !important;
        }
        
        @supports (min-height: 100dvh) {
            .wordscape-fixed-game {
                min-height: 100dvh !important;
            }
        }
    `;
    
    if (!document.head.querySelector('#address-bar-optimization')) {
        document.head.appendChild(style);
        console.log('?? Address bar optimization CSS applied');
    }

    // Method 3: Trigger a slight scroll down then back up to activate address bar behavior
    setTimeout(() => {
        window.scrollTo(0, 1);
        setTimeout(() => {
            window.scrollTo(0, 0);
        }, 100);
    }, 500);
}

// Hook into page visibility changes to reapply optimizations
document.addEventListener('visibilitychange', () => {
    if (!document.hidden && window.addressBarManager.isAndroidEdge) {
        // Page became visible again - reapply optimizations
        setTimeout(() => {
            optimizeForAddressBarTop();
        }, 500);
    }
});

// Export functions for external use
window.addressBarManager.init = initializeAddressBarManagement;
window.addressBarManager.showHint = showAddressBarHint;
window.addressBarManager.requestOptimalViewport = requestOptimalViewport;

// Auto-initialize on page load
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initializeAddressBarManagement);
} else {
    initializeAddressBarManagement();
}

console.log('?? Address bar management module loaded');