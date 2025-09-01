// Common JavaScript Functions for All Games and Pages

// Function to open URL in new tab/window
window.openUrl = function (url) {
    window.open(url, '_blank');
};

// Function to detect emoji support and apply fallback
window.detectEmojiSupport = function () {
    // Test if the browser can render emojis properly
    const testElement = document.createElement('canvas');
    const ctx = testElement.getContext('2d');

    if (!ctx) return false;

    // Set canvas properties
    testElement.width = testElement.height = 16;
    ctx.textBaseline = 'middle';
    ctx.textAlign = 'center';
    ctx.font = '16px Arial, sans-serif';

    // Try to render a gear emoji
    ctx.fillText('?', 8, 8);

    // Check if anything was rendered (non-transparent pixels)
    const imageData = ctx.getImageData(0, 0, 16, 16);
    let hasPixels = false;

    for (let i = 0; i < imageData.data.length; i += 4) {
        if (imageData.data[i + 3] > 0) { // Alpha channel
            hasPixels = true;
            break;
        }
    }

    if (!hasPixels) {
        // No emoji support detected, show text fallbacks
        document.body.classList.add('no-emoji-support');
    }

    return hasPixels;
};

// Enhanced game state persistence with page visibility API
window.gameStateManager = {
    // Track active game component for state saving
    activeGameComponent: null,
    
    // Register a game component for state management
    registerGame: function (gameType, dotNetHelper) {
        console.log(`?? Registering ${gameType} game for state management`);
        
        // Unregister previous game if any
        if (this.activeGameComponent && this.activeGameComponent.gameType !== gameType) {
            console.log(`?? Switching from ${this.activeGameComponent.gameType} to ${gameType}`);
            this.unregisterGame();
        }
        
        this.activeGameComponent = {
            gameType: gameType,
            dotNetHelper: dotNetHelper,
            isActive: true
        };

        // Add event listeners for state saving
        this.addEventListeners();
    },
    
    // Unregister the current game component
    unregisterGame: function () {
        if (this.activeGameComponent) {
            console.log(`?? Unregistering ${this.activeGameComponent.gameType} game`);
            this.activeGameComponent.isActive = false;
            this.activeGameComponent = null;
        }
        this.removeEventListeners();
    },
    
    // Add event listeners for automatic state saving
    addEventListeners: function () {
        // Remove existing listeners first
        this.removeEventListeners();
        
        // Page visibility change
        document.addEventListener('visibilitychange', this.onVisibilityChange);
        
        // Before page unload
        window.addEventListener('beforeunload', this.onBeforeUnload);
        
        // Page hide (mobile Safari)
        window.addEventListener('pagehide', this.onPageHide);
        
        // Blazor navigation (SPA)
        window.addEventListener('popstate', this.onNavigation);
        
        console.log('?? Game state event listeners added');
    },
    
    // Remove event listeners
    removeEventListeners: function () {
        document.removeEventListener('visibilitychange', this.onVisibilityChange);
        window.removeEventListener('beforeunload', this.onBeforeUnload);
        window.removeEventListener('pagehide', this.onPageHide);
        window.removeEventListener('popstate', this.onNavigation);
    },
    
    // Event handlers
    onVisibilityChange: function () {
        if (window.gameStateManager.activeGameComponent && document.hidden) {
            console.log('?? Page hidden - saving game state');
            window.gameStateManager.saveCurrentGameState('visibility-change');
        }
    },
    
    onBeforeUnload: function (e) {
        if (window.gameStateManager.activeGameComponent) {
            console.log('?? Before unload - saving game state');
            window.gameStateManager.saveCurrentGameState('before-unload');
        }
    },
    
    onPageHide: function (e) {
        if (window.gameStateManager.activeGameComponent) {
            console.log('?? Page hide - saving game state');
            window.gameStateManager.saveCurrentGameState('page-hide');
        }
    },
    
    onNavigation: function (e) {
        if (window.gameStateManager.activeGameComponent) {
            console.log('?? Navigation detected - saving game state');
            window.gameStateManager.saveCurrentGameState('navigation');
        }
    },
    
    // Save current game state
    saveCurrentGameState: function (reason) {
        if (!this.activeGameComponent || !this.activeGameComponent.isActive) {
            return;
        }
        
        try {
            console.log(`?? Saving ${this.activeGameComponent.gameType} state (reason: ${reason})`);
            this.activeGameComponent.dotNetHelper.invokeMethodAsync('SaveGameStateFromJS', reason);
        } catch (error) {
            console.error('? Error saving game state:', error);
        }
    }
};

// Page visibility handler for game state saving (legacy support)
window.addPageVisibilityHandler = function (dotNetHelper) {
    document.addEventListener('visibilitychange', function () {
        const isVisible = !document.hidden;
        dotNetHelper.invokeMethodAsync('OnPageVisibilityChanged', isVisible);
    });
};

// Enhanced authentication helper
window.blazorAuthHelper = {
    isMobile: function () {
        return /Android|webOS|iPhone|iPad|iPod|Opera Mini|IEMobile|WPDesktop/i.test(navigator.userAgent);
    },
    isIOS: function () {
        return /iPhone|iPad|iPod/i.test(navigator.userAgent);
    },
    isSafari: function () {
        return /^((?!chrome|android).)*safari/i.test(navigator.userAgent);
    },
    isEdge: function () {
        return /Edg/i.test(navigator.userAgent);
    },
    isFirefox: function () {
        return /firefox|fxios/i.test(navigator.userAgent);
    },
    isChrome: function () {
        return /chrome|crios|crmo/i.test(navigator.userAgent);
    },
    openUrl: function (url) {
        window.open(url, '_blank');
    },
    // Redirect to authentication URL with optional parameters
    signIn: function (returnUrl) {
        let url = `/_signin?redirectUri=${encodeURIComponent(window.location.href)}`;
        if (returnUrl) {
            url += `&returnUrl=${encodeURIComponent(returnUrl)}`;
        }
        window.location.href = url;
    },
    signOut: function () {
        const url = `/_signout?redirectUri=${encodeURIComponent(window.location.href)}`;
        window.location.href = url;
    },
    setupAuthTimeout: function () {
        if (this.isMobile()) {
            // Increase timeout for mobile and check auth state first
            setTimeout(function () {
                if (window.location.pathname.includes('/authentication/login-callback')) {
                    console.log('Auth callback timeout - checking if user is actually authenticated');
                    // Instead of immediately redirecting, trigger the auth check
                    if (window.DotNet) {
                        console.log('Triggering auth status check from timeout');
                        // This will be handled by the CheckAuthAndRedirect method
                    } else {
                        console.log('DotNet not available, redirecting to home');
                        window.location.href = '/';
                    }
                }
            }, 15000); // Increase to 15 seconds
        }
    },

    // Clear authentication cache
    clearAuthCache: function () {
        try {
            console.log('Clearing authentication cache...');

            // Clear MSAL specific items
            Object.keys(localStorage).forEach(key => {
                if (key.includes('msal') || key.includes('auth') || key.includes('token') || key.includes('account')) {
                    localStorage.removeItem(key);
                    console.log('Removed localStorage key:', key);
                }
            });

            Object.keys(sessionStorage).forEach(key => {
                if (key.includes('msal') || key.includes('auth') || key.includes('token') || key.includes('account')) {
                    sessionStorage.removeItem(key);
                }
            });

            console.log('Authentication cache cleared');
        } catch (error) {
            console.error('Error clearing auth cache:', error);
        }
    }
};

// Initialize emoji detection on page load
document.addEventListener('DOMContentLoaded', function () {
    window.detectEmojiSupport();
    console.log('? Common.js loaded and emoji detection initialized');
});

// Initialize immediately if DOM already loaded
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
        window.detectEmojiSupport();
    });
} else {
    window.detectEmojiSupport();
}