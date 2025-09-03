// Common JavaScript Functions for All Games and Pages

// Function to open URL in new tab/window
window.openUrl = function (url) {
    window.open(url, '_blank');
};

// Function to detect emoji support and apply fallback
window.detectEmojiSupport = function () {
    // Test if the browser can render emojis and Unicode symbols properly
    const testElement = document.createElement('canvas');
    const ctx = testElement.getContext('2d');

    if (!ctx) {
        // No canvas support, assume no emoji support
        document.body.classList.add('no-emoji-support');
        console.log('?? No canvas support - using text fallbacks');
        return false;
    }

    // Set canvas properties
    testElement.width = testElement.height = 32; // Larger canvas for better detection
    ctx.textBaseline = 'middle';
    ctx.textAlign = 'center';
    ctx.font = '20px Arial, sans-serif'; // Larger font for better testing

    // Test multiple Unicode symbols used in the app
    const testSymbols = [
        { symbol: '?', name: 'Settings gear' },
        { symbol: '?', name: 'Shuffle arrow' }, 
        { symbol: '?', name: 'New game square' },
        { symbol: '?', name: 'Star' },
        { symbol: '?', name: 'Checkmark' },
        { symbol: '?', name: 'X mark' }
    ];
    
    let hasSymbolSupport = false;
    let supportedSymbols = [];
    let unsupportedSymbols = [];

    for (const { symbol, name } of testSymbols) {
        // Clear canvas
        ctx.clearRect(0, 0, 32, 32);
        
        // Attempt to render the symbol
        ctx.fillStyle = '#000000';
        ctx.fillText(symbol, 16, 16);

        // Check if anything was rendered (non-transparent pixels)
        const imageData = ctx.getImageData(0, 0, 32, 32);
        let hasPixels = false;

        // Check for non-transparent pixels
        for (let i = 0; i < imageData.data.length; i += 4) {
            if (imageData.data[i + 3] > 10) { // Alpha channel with some tolerance
                hasPixels = true;
                break;
            }
        }

        if (hasPixels) {
            hasSymbolSupport = true;
            supportedSymbols.push(name);
        } else {
            unsupportedSymbols.push(name);
        }
    }

    // Additional checks for system font support
    const systemSupportsUnicode = (
        // Check for modern browser
        (typeof window.navigator !== 'undefined' && 
         window.navigator.userAgent && 
         !window.navigator.userAgent.match(/MSIE|Trident/)) &&
        // Check for basic CSS support
        (typeof CSS !== 'undefined' && CSS.supports)
    );

    // Enhanced detection - check if we have at least basic symbol support
    const hasMinimalSupport = supportedSymbols.length >= (testSymbols.length * 0.5); // At least 50% support

    console.log(`?? Unicode symbol detection results:`);
    console.log(`  - Supported symbols (${supportedSymbols.length}): ${supportedSymbols.join(', ')}`);
    console.log(`  - Unsupported symbols (${unsupportedSymbols.length}): ${unsupportedSymbols.join(', ')}`);
    console.log(`  - System supports Unicode: ${systemSupportsUnicode}`);
    console.log(`  - Has minimal support: ${hasMinimalSupport}`);

    if (!hasMinimalSupport || !systemSupportsUnicode) {
        // No emoji/symbol support detected, show text fallbacks
        document.body.classList.add('no-emoji-support');
        console.log('?? Limited Unicode symbol support detected - using text fallbacks');
        
        // Optional: Add a debug class to test the fallback system
        if (window.location.search.includes('debug-text')) {
            document.body.classList.add('debug-text-fallback');
            console.log('?? Debug mode: Forcing text fallbacks');
        }
    } else {
        console.log('? Good Unicode symbol support detected - using icons');
        
        // Optional: Add a debug class to force text fallbacks for testing
        if (window.location.search.includes('debug-text')) {
            document.body.classList.add('debug-text-fallback');
            console.log('?? Debug mode: Forcing text fallbacks despite icon support');
        }
    }

    return hasMinimalSupport && systemSupportsUnicode;
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

// DEBUG: Add a global function to test icon fallback system
window.debugIconFallback = function() {
    const hasNoEmojiSupport = document.body.classList.contains('no-emoji-support');
    const hasDebugTextFallback = document.body.classList.contains('debug-text-fallback');
    
    console.log('?? Icon fallback system status:');
    console.log(`  - Body has 'no-emoji-support' class: ${hasNoEmojiSupport}`);
    console.log(`  - Body has 'debug-text-fallback' class: ${hasDebugTextFallback}`);
    
    // Check what's actually displayed
    const iconFallbacks = document.querySelectorAll('.icon-fallback');
    const textFallbacks = document.querySelectorAll('.text-fallback');
    
    console.log(`  - Found ${iconFallbacks.length} .icon-fallback elements`);
    console.log(`  - Found ${textFallbacks.length} .text-fallback elements`);
    
    iconFallbacks.forEach((element, index) => {
        const computedStyle = window.getComputedStyle(element);
        const display = computedStyle.getPropertyValue('display');
        const content = element.textContent || element.innerText;
        console.log(`    Icon ${index + 1}: "${content}" (display: ${display})`);
    });
    
    textFallbacks.forEach((element, index) => {
        const computedStyle = window.getComputedStyle(element);
        const display = computedStyle.getPropertyValue('display');
        const content = element.textContent || element.innerText;
        console.log(`    Text ${index + 1}: "${content}" (display: ${display})`);
    });
    
    return {
        hasNoEmojiSupport,
        hasDebugTextFallback,
        iconCount: iconFallbacks.length,
        textCount: textFallbacks.length
    };
};

// DEBUG: Add a function to toggle fallback mode for testing
window.toggleIconFallback = function(forceText = null) {
    if (forceText === true) {
        document.body.classList.add('debug-text-fallback');
        console.log('?? Forcing text fallbacks');
    } else if (forceText === false) {
        document.body.classList.remove('debug-text-fallback');
        document.body.classList.remove('no-emoji-support');
        console.log('?? Forcing icon display');
    } else {
        // Toggle
        if (document.body.classList.contains('debug-text-fallback')) {
            document.body.classList.remove('debug-text-fallback');
            console.log('?? Disabled text fallback debug mode');
        } else {
            document.body.classList.add('debug-text-fallback');
            console.log('?? Enabled text fallback debug mode');
        }
    }
    
    return window.debugIconFallback();
};

// DEBUG: Add comprehensive Unicode rendering test
window.testUnicodeRenderingComprehensive = function() {
    console.log('?? Comprehensive Unicode Rendering Test');
    
    // Create a test area
    const testDiv = document.createElement('div');
    testDiv.style.cssText = `
        position: fixed; 
        top: 10px; 
        right: 10px; 
        background: white; 
        border: 2px solid #333; 
        padding: 15px; 
        z-index: 10000; 
        font-size: 16px;
        max-width: 300px;
        box-shadow: 0 4px 12px rgba(0,0,0,0.2);
    `;
    
    // Test the specific symbols used in the application
    const testSymbols = [
        { symbol: '?', name: 'Settings gear', expected: 'SET' },
        { symbol: '?', name: 'Shuffle arrow', expected: 'MIX' }, 
        { symbol: '?', name: 'New game square', expected: 'NEW' },
        { symbol: '?', name: 'Checkmark', expected: 'SUBMIT' },
        { symbol: '?', name: 'X mark', expected: 'CLEAR' }
    ];
    
    let testResults = '<h4>Unicode Symbol Test</h4>';
    let allPassed = true;
    
    testSymbols.forEach(({ symbol, name, expected }) => {
        // Test rendering with the same font stack as buttons
        const testSpan = document.createElement('span');
        testSpan.style.fontFamily = "'Apple Color Emoji', 'Segoe UI Emoji', 'Segoe UI Symbol', 'Noto Color Emoji', 'Symbola', 'DejaVu Sans', 'Arial Unicode MS', Arial, sans-serif";
        testSpan.style.fontSize = '16px';
        testSpan.textContent = symbol;
        
        const fallbackSpan = document.createElement('span');
        fallbackSpan.style.cssText = 'font-size: 10px; font-weight: bold; color: blue; margin-left: 5px;';
        fallbackSpan.textContent = expected;
        
        const container = document.createElement('div');
        container.style.marginBottom = '8px';
        container.appendChild(testSpan);
        container.appendChild(document.createTextNode(` - ${name} `));
        container.appendChild(fallbackSpan);
        
        testDiv.appendChild(container);
        
        // Check if the symbol renders as a "?" or missing character
        const testCanvas = document.createElement('canvas');
        const ctx = testCanvas.getContext('2d');
        testCanvas.width = testCanvas.height = 32;
        ctx.font = "16px 'Apple Color Emoji', 'Segoe UI Emoji', 'Segoe UI Symbol', 'Noto Color Emoji', Arial, sans-serif";
        ctx.fillText(symbol, 5, 20);
        
        const imageData = ctx.getImageData(0, 0, 32, 32);
        let hasRenderedPixels = false;
        for (let i = 0; i < imageData.data.length; i += 4) {
            if (imageData.data[i + 3] > 10) {
                hasRenderedPixels = true;
                break;
            }
        }
        
        if (!hasRenderedPixels) {
            allPassed = false;
            container.style.backgroundColor = '#ffeeee';
            console.log(`? Symbol '${symbol}' (${name}) not rendering properly`);
        } else {
            container.style.backgroundColor = '#eeffee';
            console.log(`? Symbol '${symbol}' (${name}) renders correctly`);
        }
    });
    
    // Add close button
    const closeBtn = document.createElement('button');
    closeBtn.textContent = 'Close Test';
    closeBtn.style.cssText = 'margin-top: 10px; padding: 5px 10px; cursor: pointer;';
    closeBtn.onclick = () => testDiv.remove();
    testDiv.appendChild(closeBtn);
    
    // Add overall result
    const resultDiv = document.createElement('div');
    resultDiv.style.cssText = `margin-top: 10px; padding: 8px; font-weight: bold; ${allPassed ? 'background: #d4edda; color: #155724;' : 'background: #f8d7da; color: #721c24;'}`;
    resultDiv.textContent = allPassed ? '? All symbols render correctly' : '? Some symbols have rendering issues';
    testDiv.appendChild(resultDiv);
    
    document.body.appendChild(testDiv);
    
    return {
        allPassed,
        testSymbols: testSymbols.map(({ symbol, name }) => ({ symbol, name, renders: true })) // Simplified for now
    };
};

// DEBUG: Add function to force different fallback modes for testing
window.testFallbackModes = function() {
    console.log('?? Testing different fallback modes...');
    
    const modes = [
        { name: 'Icons Only', className: '', description: 'Show Unicode icons' },
        { name: 'Text Only', className: 'debug-text-fallback', description: 'Force text fallbacks' },
        { name: 'No Emoji Support', className: 'no-emoji-support', description: 'Simulate no emoji support' }
    ];
    
    let currentMode = 0;
    
    const testCycle = () => {
        const mode = modes[currentMode];
        
        // Remove all previous classes
        document.body.classList.remove('debug-text-fallback', 'no-emoji-support');
        
        // Add new class if needed
        if (mode.className) {
            document.body.classList.add(mode.className);
        }
        
        console.log(`?? Testing mode: ${mode.name} - ${mode.description}`);
        
        // Show what should be visible
        const iconElements = document.querySelectorAll('.icon-fallback');
        const textElements = document.querySelectorAll('.text-fallback');
        
        console.log(`   Icons visible: ${Array.from(iconElements).filter(el => window.getComputedStyle(el).display !== 'none').length}/${iconElements.length}`);
        console.log(`   Text visible: ${Array.from(textElements).filter(el => window.getComputedStyle(el).display !== 'none').length}/${textElements.length}`);
        
        currentMode = (currentMode + 1) % modes.length;
        
        if (currentMode === 0) {
            console.log('?? Test cycle complete. Run testFallbackModes() again to repeat.');
            return;
        }
        
        setTimeout(testCycle, 3000);
    };
    
    testCycle();
};

// DEBUG: Check current icon/text visibility state
window.checkIconTextState = function() {
    console.log('?? Current Icon/Text State Analysis:');
    
    const bodyClasses = Array.from(document.body.classList);
    console.log(`Body classes: [${bodyClasses.join(', ')}]`);
    
    const buttons = document.querySelectorAll('.control-button');
    console.log(`Found ${buttons.length} control buttons`);
    
    buttons.forEach((button, index) => {
        const iconElement = button.querySelector('.icon-fallback');
        const textElement = button.querySelector('.text-fallback');
        
        if (iconElement && textElement) {
            const iconStyle = window.getComputedStyle(iconElement);
            const textStyle = window.getComputedStyle(textElement);
            
            const iconVisible = iconStyle.display !== 'none';
            const textVisible = textStyle.display !== 'none';
            
            const iconContent = iconElement.textContent || iconElement.innerText;
            const textContent = textElement.textContent || textElement.innerText;
            
            console.log(`Button ${index + 1}:`);
            console.log(`  Icon: "${iconContent}" (${iconVisible ? 'visible' : 'hidden'})`);
            console.log(`  Text: "${textContent}" (${textVisible ? 'visible' : 'hidden'})`);
            console.log(`  Icon font-family: ${iconStyle.fontFamily}`);
        }
    });
    
    return {
        bodyClasses,
        buttonCount: buttons.length,
        iconElements: document.querySelectorAll('.icon-fallback').length,
        textElements: document.querySelectorAll('.text-fallback').length
    };
};