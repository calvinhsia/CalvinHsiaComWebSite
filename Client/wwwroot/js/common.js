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
    ctx.fillText('??', 8, 8);

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

// Page visibility handler for game state saving
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

            console.log('Authentication cache clearing completed');
        } catch (e) {
            console.error('Error clearing auth cache:', e);
        }
    },

    // Force complete logout
    forceLogout: function () {
        try {
            console.log('Force logout initiated...');

            // Clear all storage
            localStorage.clear();
            sessionStorage.clear();

            // Clear cookies
            document.cookie.split(';').forEach(function (c) {
                document.cookie = c.replace(/^ +/, '').replace(/=.*/, '=;expires=' + new Date().toUTCString() + ';path=/');
            });

            console.log('Force logout completed');

            // Redirect to home
            setTimeout(() => {
                window.location.href = '/';
            }, 100);
        } catch (e) {
            console.error('Error during force logout:', e);
            window.location.href = '/';
        }
    },
    get userAgent() {
        return navigator.userAgent;
    },
    get platform() {
        return navigator.platform;
    },
    get cookieEnabled() {
        return navigator.cookieEnabled;
    },
    get deviceMemory() {
        return navigator.deviceMemory || 0;
    },
    get hardwareConcurrency() {
        return navigator.hardwareConcurrency || 0;
    },
    get screenResolution() {
        return `${screen.width}x${screen.height}`;
    },
    get viewportResolution() {
        return `${window.innerWidth}x${window.innerHeight}`;
    },
    get browserLanguage() {
        return navigator.language || navigator.userLanguage;
    }
};

// Auto-setup timeout on mobile devices
if (window.blazorAuthHelper.isMobile()) {
    window.blazorAuthHelper.setupAuthTimeout();
}

// Add global logout helper
window.globalLogout = function () {
    window.blazorAuthHelper.forceLogout();
};

// Initialize emoji detection when page loads
document.addEventListener('DOMContentLoaded', function () {
    window.detectEmojiSupport();
});