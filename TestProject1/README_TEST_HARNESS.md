# Interactive Blazor WASM Test Harnesses

This directory contains several test harnesses that allow you to explore and experiment with your Blazor WebAssembly application interactively.

## Available Test Harnesses

### 1. **Simple HTML Test Harness** (Recommended for Quick Experiments)

**Test:** `SimpleHtmlTestHarness.CreateInteractiveHtmlTestHarness()`

This creates a standalone HTML file that you can open in any browser and modify to experiment with HTML, CSS, and JavaScript.

**How to use:**
```bash
# Run the test
dotnet test --filter "FullyQualifiedName~CreateInteractiveHtmlTestHarness"

# The test will create 'interactive-test-harness.html' and open it in your browser
# Edit the HTML file directly to experiment with different layouts, styles, and interactions
# Refresh the browser to see your changes
```

**Benefits:**
- No dependencies needed
- Easy to modify HTML/CSS/JS directly in the file
- Works with any browser
- Great for prototyping UI components

---

### 2. **Iframe Test Harness** (Best for Testing Real App with Experiments)

**Test:** `SimpleHtmlTestHarness.CreateIframeTestHarness()`

This creates an advanced HTML file that embeds your actual Blazor WASM app in an iframe, allowing you to interact with the real app while injecting experimental CSS and JavaScript.

**How to use:**
```bash
# 1. Start your Blazor WASM app
cd Client
dotnet run

# 2. In another terminal, run the test
cd TestProject1
dotnet test --filter "FullyQualifiedName~CreateIframeTestHarness"

# The test will create 'iframe-test-harness.html' and open it
# Use the sidebar to:
# - Navigate to different pages
# - Inject custom CSS and JavaScript
# - Monitor console logs
```

**Benefits:**
- Interacts with your real Blazor app
- Live CSS/JS injection without modifying source files
- Easy navigation between pages
- Built-in logging and debugging tools

---

### 3. **Playwright Interactive Browser** (Best for Full Browser Automation)

**Test:** `InteractiveBlazorTest.LaunchInteractiveBrowser_WordScapeGame()`

This uses Playwright to launch a real browser with DevTools open, allowing you to interact with your Blazor app programmatically while also manually testing.

**Prerequisites:**
```bash
# Install Playwright
dotnet add TestProject1 package Microsoft.Playwright

# Install browser binaries (first time only)
cd TestProject1
pwsh bin/Debug/net8.0/playwright.ps1 install
```

**How to use:**
```bash
# Run the test (it will start your Blazor server automatically)
dotnet test --filter "FullyQualifiedName~LaunchInteractiveBrowser_WordScapeGame"

# The test will:
# 1. Start your Blazor WASM development server
# 2. Launch Chrome with DevTools
# 3. Navigate to your app
# 4. Keep the browser open for 5 minutes

# You can interact with the browser and DevTools while the test is running
```

**Available Tests:**
- `LaunchInteractiveBrowser_WordScapeGame` - Opens WordScape game
- `LaunchInteractiveBrowser_LogoGame` - Opens Logo game
- `LaunchInteractiveBrowser_WordamentGame` - Opens Wordament game
- `AutomatedTest_WordScapeGameInteraction` - Example automated test with screenshots

**Benefits:**
- Full browser automation capabilities
- DevTools automatically opened
- Can capture screenshots and videos
- Great for regression testing
- Supports mobile emulation

---

## Quick Start Guide

### For HTML/CSS/JS Experimentation:

1. **Run the Simple HTML Test Harness:**
   ```bash
   dotnet test --filter "CreateInteractiveHtmlTestHarness"
   ```

2. **Edit the generated `interactive-test-harness.html` file:**
   - Modify the CSS in the `<style>` section
   - Add HTML elements in the `experiment-area` div
   - Write JavaScript functions to test interactions

3. **Refresh your browser** to see changes

### For Testing with Your Real App:

1. **Start your Blazor app:**
   ```bash
   cd Client
   dotnet run
   ```

2. **In another terminal, run the Iframe Test Harness:**
   ```bash
   cd TestProject1
   dotnet test --filter "CreateIframeTestHarness"
   ```

3. **Use the sidebar controls to:**
   - Navigate to different game pages
   - Inject experimental CSS (e.g., change colors, sizes, layouts)
   - Inject experimental JavaScript (e.g., test new interactions)
   - Monitor console output

### For Full Browser Automation:

1. **Install Playwright** (first time only):
   ```bash
   dotnet add TestProject1 package Microsoft.Playwright
   cd TestProject1
   pwsh bin/Debug/net8.0/playwright.ps1 install
   ```

2. **Run an interactive test:**
   ```bash
   dotnet test --filter "LaunchInteractiveBrowser"
   ```

3. **Interact with the browser:**
   - The browser will open with your app loaded
   - DevTools will be open automatically
   - You can manually interact with the page
   - Console logs from the browser appear in your test output

---

## Tips for Experimentation

### CSS Experimentation:
```css
/* In the Simple HTML Test Harness, add to the <style> section: */

/* Change grid cell colors */
.test-cell {
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
}

/* Add animations */
.test-cell:hover {
    animation: pulse 0.5s ease-in-out;
}

@keyframes pulse {
    0%, 100% { transform: scale(1); }
    50% { transform: scale(1.1); }
}
```

### JavaScript Experimentation:
```javascript
// In the Simple HTML Test Harness, add to the <script> section:

// Test touch/mouse interactions
function testDragBehavior() {
    const grid = document.getElementById('testGrid');
    let isDragging = false;
    
    grid.addEventListener('mousedown', (e) => {
        isDragging = true;
        log('Drag started');
    });
    
    grid.addEventListener('mousemove', (e) => {
        if (isDragging) {
            // Your drag logic here
        }
    });
    
    grid.addEventListener('mouseup', (e) => {
        isDragging = false;
        log('Drag ended');
    });
}
```

### Iframe Injection Examples:
```css
/* In the Iframe Test Harness, paste into "Inject Custom CSS" textarea: */

/* Highlight all buttons */
button {
    outline: 3px solid red !important;
}

/* Make the grid larger */
.wordscape-grid {
    transform: scale(1.2);
}
```

```javascript
// In the Iframe Test Harness, paste into "Inject Custom JS" textarea:

// Log all button clicks
document.querySelectorAll('button').forEach(btn => {
    btn.addEventListener('click', (e) => {
        console.log('Button clicked:', e.target.textContent);
    });
});

// Test Blazor interop
console.log('DotNet available:', typeof DotNet !== 'undefined');
```

---

## Troubleshooting

### "Failed to connect to browser" (Playwright)
- Make sure you've installed browser binaries: `pwsh bin/Debug/net8.0/playwright.ps1 install`
- Try running as administrator

### "Cannot access frame content" (Iframe Harness)
- Make sure your Blazor app is running on the same origin (localhost)
- Check the server URL in the sidebar matches your app's port

### "Page not loading"
- Verify your Blazor app is running: `dotnet run --project Client/Client.csproj`
- Check the console for errors
- Try changing the port in launchSettings.json if 5000/5001 is in use

### Changes not appearing
- Hard refresh your browser (Ctrl+F5 or Cmd+Shift+R)
- Clear browser cache
- Check browser console for errors

---

## Example Workflows

### Workflow 1: Testing a New UI Component
1. Run `CreateInteractiveHtmlTestHarness`
2. Add your component's HTML to the experiment area
3. Style it with CSS in the `<style>` section
4. Add interaction handlers in the `<script>` section
5. Once satisfied, port the code to your actual Blazor component

### Workflow 2: Debugging Touch Interactions
1. Run `CreateIframeTestHarness`
2. Open your app in the iframe
3. Inject JavaScript to log touch events
4. Use Chrome DevTools device emulation to test mobile
5. Monitor the log section for touch event details

### Workflow 3: Testing CSS Changes
1. Run `CreateIframeTestHarness`
2. Navigate to the game page you want to style
3. Paste experimental CSS into the "Inject Custom CSS" area
4. Click "Inject CSS" to see changes immediately
5. Iterate until satisfied, then copy to your actual CSS file

### Workflow 4: Automated Regression Testing
1. Install Playwright
2. Create a test method in `InteractiveBlazorTest`
3. Use Playwright's API to interact with elements
4. Add assertions to verify behavior
5. Run as part of your CI/CD pipeline

---

## Next Steps

- Explore the test files to see more examples
- Modify the test harnesses to fit your workflow
- Add custom test methods for specific scenarios
- Integrate with your development workflow

Happy experimenting! ??
