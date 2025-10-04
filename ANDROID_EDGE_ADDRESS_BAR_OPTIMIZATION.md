# Android Edge Address Bar Optimization for WordScape Game

This feature automatically optimizes the WordScape game for Android Edge browser users by encouraging the address bar to be positioned at the top, providing more vertical screen space for gameplay.

## Features Implemented

### 1. **Web App Manifest** (`manifest.json`)
- Enables PWA-like behavior with `standalone` display mode
- Provides app icons and shortcuts for better mobile experience
- Configures the app as a games category application

### 2. **Address Bar Manager** (`js/address-bar-manager.js`)
- **Automatic Detection**: Detects Android Edge browser automatically
- **Viewport Optimization**: Applies CSS optimizations for maximum screen usage
- **User Hints**: Shows helpful tips to users about moving the address bar to the top
- **Fullscreen Requests**: Temporarily requests fullscreen to trigger browser address bar options
- **Viewport Monitoring**: Tracks viewport changes to detect address bar repositioning

### 3. **WordScape Integration**
- **Settings Panel**: Added Android Edge optimization section in game settings
- **Manual Hints**: Users can manually trigger the address bar hint from settings
- **Game Integration**: Address bar optimizations are applied during game initialization
- **Responsive Design**: Enhanced CSS for better viewport utilization when address bar is at top

## How It Works

### For Android Edge Users:

1. **Automatic Detection**: When the WordScape game loads, it automatically detects if you're using Android Edge browser

2. **Initial Optimization**: The system applies viewport optimizations and may briefly request fullscreen to encourage the browser to show address bar positioning options

3. **User Guidance**: A helpful hint appears (once per session) explaining how to move the address bar to the top:
   - Hold/touch the address bar
   - Select "Move address bar to the top" from the menu
   - The address bar will scroll out of view when scrolling, giving more game space

4. **Persistent Settings**: Access the hint again anytime through the game settings panel

### For Other Browsers:
- The feature gracefully degrades and has no impact on other browsers
- All standard game functionality remains unchanged

## Benefits

- **More Screen Space**: When the address bar is at the top, it scrolls out of view during gameplay
- **Better Game Experience**: Larger visible area for the crossword grid and letter wheel
- **Seamless Integration**: Works automatically without user configuration
- **Non-Intrusive**: Only activates for Android Edge, invisible to other browsers

## Technical Implementation

### Files Added/Modified:

1. **`Client/wwwroot/manifest.json`** - Web app manifest for PWA features
2. **`Client/wwwroot/js/address-bar-manager.js`** - Core address bar management logic
3. **`Client/wwwroot/icon.svg`** - App icon for the manifest
4. **`Client/wwwroot/index.html`** - Updated with manifest link and PWA meta tags
5. **`Client/wwwroot/js/wordscape-game.js`** - Integration with address bar manager
6. **`Client/Pages/WordScapeGame.razor`** - Settings panel and C# integration

### Key Technologies Used:

- **Browser Detection**: User agent parsing to identify Android Edge
- **Viewport API**: `window.visualViewport` for monitoring viewport changes
- **Fullscreen API**: Temporary fullscreen requests to trigger browser options
- **CSS Viewport Units**: `100vh`, `100svh`, `100dvh` for optimal viewport usage
- **PWA Features**: Web app manifest for app-like behavior

## Browser Support

- **Primary Target**: Android Edge browser
- **Fallback**: Graceful degradation for all other browsers
- **No Breaking Changes**: Existing functionality preserved for all users

## Usage Examples

### For Developers:
```javascript
// Check if address bar management is active
if (window.addressBarManager && window.addressBarManager.isAndroidEdge) {
    console.log('Address bar optimizations are active');
}

// Manually show the hint
window.addressBarManager.showHint();
```

### For Users:
1. Open WordScape game on Android Edge
2. Look for the blue hint that appears automatically
3. Follow the instructions to move address bar to top
4. Enjoy more screen space while playing!
5. Access the hint again via Settings ? Android Edge Optimization

This feature enhances the mobile gaming experience specifically for Android Edge users while maintaining full compatibility with all other browsers and devices.