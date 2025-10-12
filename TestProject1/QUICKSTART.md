# ?? Quick Start: Interactive Blazor WASM Testing

## The Fastest Way to Start (No Server Required!)

Run this single command to get started:

```bash
dotnet test --filter "QuickStart_InteractiveHtmlTest"
```

This will:
1. Create a simple HTML test page with an interactive grid
2. Open it in your default browser
3. Let you modify the HTML/CSS/JS and refresh to see changes

**File created:** `TestProject1/bin/Debug/net8.0/quick-test.html`

**No dependencies, no server needed - just experiment!**

---

## Recommended Workflow

### For Quick HTML/CSS/JS Experiments ??

**Best for:** Prototyping UI, testing CSS animations, trying JavaScript interactions

```bash
# Option 1: Quick test (simplest)
dotnet test --filter "QuickStart_InteractiveHtmlTest"

# Option 2: More features
dotnet test --filter "CreateInteractiveHtmlTestHarness"

# Both create HTML files you can edit and refresh
```

### For Testing with Your Real Blazor App ??

**Best for:** Testing actual game functionality, debugging issues, live CSS/JS injection

```bash
# Terminal 1: Start your Blazor app
cd Client
dotnet run

# Terminal 2: Create the test harness
dotnet test --filter "CreateIframeTestHarness"

# Opens a browser with your app + testing tools
```

### For Browser Automation ??

**Best for:** Automated testing, screenshots, CI/CD integration

```bash
# Terminal 1: Start your Blazor app FIRST
cd Client
dotnet run

# Terminal 2: Run Playwright test
dotnet test --filter "LaunchInteractiveBrowser_WordScapeGame"

# Opens Chrome with DevTools automatically
```

---

## What I've Created For You

I've added **3 different test harnesses** to help you experiment with your Blazor WASM app:

### 1. ? Quick Test (Simplest - Start Here!)
**Test:** `TestWordScape.QuickStart_InteractiveHtmlTest()`

A standalone HTML file with a sample grid that you can click and interact with.

**Run it:**
```bash
dotnet test --filter "QuickStart_InteractiveHtmlTest"
```

**What you get:**
- Interactive grid with clickable cells
- Word selection display
- Easy to modify HTML/CSS/JS
- No dependencies needed
- ? **Works offline - no server required!**

---

### 2. ?? Simple HTML Test Harness (Best for Prototyping)
**Test:** `SimpleHtmlTestHarness.CreateInteractiveHtmlTestHarness()`

A more feature-rich standalone HTML page with test controls and output console.

**Run it:**
```bash
dotnet test --filter "CreateInteractiveHtmlTestHarness"
```

**What you get:**
- Test grid with selection
- Control buttons
- JavaScript console output
- Styled with professional UI
- ? **Works offline - no server required!**

---

### 3. ?? Iframe Test Harness (Best for Real App Testing)
**Test:** `SimpleHtmlTestHarness.CreateIframeTestHarness()`

Embeds your actual Blazor WASM app with tools to inject CSS/JS on-the-fly.

**Run it:**
```bash
# First, start your Blazor app
cd Client
dotnet run

# Then in another terminal:
dotnet test --filter "CreateIframeTestHarness"
```

**What you get:**
- Your real Blazor app in an iframe
- Live CSS injection (test styles without modifying files!)
- Live JavaScript injection (test code without recompiling!)
- Navigation controls
- Console logging
- ?? **Requires your Blazor server to be running**

---

### 4. ?? Playwright Browser Automation (Most Powerful)
**Test:** `InteractiveBlazorTest.LaunchInteractiveBrowser_**()`**

Uses Playwright to launch a real Chrome browser with DevTools for full testing.

**?? Important Setup:**

1. **Start your Blazor app manually first:**
   ```bash
   cd Client
   dotnet run
   ```

2. **First-time Playwright setup:**
   ```bash
   dotnet restore TestProject1
   pwsh TestProject1/bin/Debug/net8.0/playwright.ps1 install
   ```

3. **Run the test:**
   ```bash
   dotnet test --filter "LaunchInteractiveBrowser_WordScapeGame"
   ```

**What you get:**
- Automated browser launch
- DevTools automatically opened
- Can interact manually or programmatically
- Screenshot capabilities
- Mobile emulation support
- ?? **Requires Playwright installation and server running**

## ? **This is the Expected Behavior**

The Playwright test is configured with `AUTO_START_SERVER = false` (which is the recommended setting), so it checks if your server is running and gives you a clear error message if it's not.

**Note:** Your server runs on `https://localhost:7193` (configured in `Client/Properties/launchSettings.json`)

## ?? Two Ways to Fix This

### **Option 1: Start Server Manually (Recommended)**

```sh
# Terminal 1: Start your Blazor app
cd Client
dotnet run
# Server will start on https://localhost:7193

# Terminal 2: Then run the Playwright test
dotnet test --filter "LaunchInteractiveBrowser_WordScapeGame"
```

### **Option 2: Let the Test Auto-Start (Already Configured)**

The test is now configured with `AUTO_START_SERVER = true` and uses the correct port (`https://localhost:7193`), so it should work automatically!

Just run:
```sh
dotnet test --filter "LaunchInteractiveBrowser_WordScapeGame"
```

The test will:
1. Start your Blazor server automatically
2. Wait for it to be ready
3. Launch Chrome with DevTools
4. Navigate to your game
5. Keep the browser open for 5 minutes

If you prefer to start the server manually, edit `TestProject1/InteractiveBlazorTest.cs` line 20:

```csharp
// Change this line:
private const bool AUTO_START_SERVER = true;

// To this:
private const bool AUTO_START_SERVER = false;
```

---

## Comparison Chart

| Feature | Quick Test | Simple HTML | Iframe | Playwright |
|---------|-----------|-------------|--------|------------|
| **Setup Time** | Instant ? | Instant ? | Start server | Install + server |
| **Modify HTML/CSS/JS** | ? Edit file | ? Edit file | ? Live inject | ? Via code |
| **Uses Real Blazor App** | ? | ? | ? | ? |
| **Works Offline** | ? | ? | ? | ? |
| **Browser DevTools** | ? Manual | ? Manual | ? Manual | ? Auto-opens |
| **Automation** | ? | ? | Partial | ? Full |
| **Best For** | Quick demos | Prototyping | Live testing | CI/CD |
| **Recommended?** | ????? | ????? | ???? | ??? |

---

## Example Workflow

### Scenario: You want to test a new drag-and-drop interaction

**Option A - Quick Prototype (Recommended):**
```bash
# 1. Create test page (takes 1 second)
dotnet test --filter "QuickStart_InteractiveHtmlTest"

# 2. The HTML file opens in your browser
# 3. Edit the file at: TestProject1/bin/Debug/net8.0/quick-test.html
# 4. Add your drag-and-drop JavaScript
# 5. Save and refresh browser to test
# 6. Once it works, port the code to your Blazor component
```

**Option B - Test with Real App:**
```bash
# 1. Start your Blazor app
cd Client
dotnet run

# 2. Create iframe harness (in another terminal)
dotnet test --filter "CreateIframeTestHarness"

# 3. In the sidebar, paste your experimental JavaScript
# 4. Click "Inject JS" to test it live
# 5. Iterate until it works
# 6. Copy the working code to your actual JS files
```

---

## Where Are The Files?

After running tests, you'll find generated files in:
```
TestProject1/bin/Debug/net8.0/
??? quick-test.html                  (Quick Test) ? Start here!
??? interactive-test-harness.html    (Simple HTML)
??? iframe-test-harness.html         (Iframe)
```

The test classes are in:
```
TestProject1/
??? TestWordScape.cs           (Contains QuickStart test) ?
??? SimpleHtmlTestHarness.cs   (HTML and Iframe tests)
??? InteractiveBlazorTest.cs   (Playwright tests)
??? README_TEST_HARNESS.md     (Detailed documentation)
```

---

## Tips for Experimentation

### Modify CSS
Edit the `<style>` section in any HTML file:
```css
.cell:hover {
    transform: scale(1.2) rotate(5deg);
    box-shadow: 0 8px 16px rgba(0,0,0,0.3);
    animation: pulse 0.5s ease-in-out;
}

@keyframes pulse {
    0%, 100% { opacity: 1; }
    50% { opacity: 0.7; }
}
```

### Modify JavaScript
Edit the `<script>` section:
```javascript
function toggleCell(cell) {
    // Add your custom logic here
    console.log('Cell clicked:', cell.dataset.letter);
    
    // Add animation
    cell.style.animation = 'bounce 0.5s';
    
    // Play a sound
    new Audio('click.mp3').play();
}
```

### Test Touch Events
```javascript
cell.addEventListener('touchstart', (e) => {
    e.preventDefault();
    console.log('Touch started at:', e.touches[0].clientX, e.touches[0].clientY);
});

cell.addEventListener('touchmove', (e) => {
    e.preventDefault();
    console.log('Touch moved to:', e.touches[0].clientX, e.touches[0].clientY);
});
```

### Test Drag and Drop
```javascript
let isDragging = false;
let selectedCells = [];

grid.addEventListener('mousedown', (e) => {
    isDragging = true;
    selectedCells = [];
});

grid.addEventListener('mouseover', (e) => {
    if (isDragging && e.target.classList.contains('cell')) {
        e.target.classList.add('selected');
        selectedCells.push(e.target);
    }
});

grid.addEventListener('mouseup', (e) => {
    isDragging = false;
    console.log('Selected word:', selectedCells.map(c => c.textContent).join(''));
});
```

---

## Next Steps

1. ? **Start with the Quick Test** - Run it now to get familiar!
   ```bash
   dotnet test --filter "QuickStart_InteractiveHtmlTest"
   ```

2. **Experiment with HTML/CSS/JS** - Edit the generated file and refresh

3. **Use Iframe Test** when you want to test with your real app

4. **Use Playwright** for automated regression tests

For detailed documentation, see `README_TEST_HARNESS.md`

---

## Troubleshooting

**"Browser didn't open"**
- Manually open the HTML file from `TestProject1/bin/Debug/net8.0/`
- Check if your default browser is set correctly

**"Iframe shows blank page"**
- Make sure your Blazor app is running (`dotnet run` in Client folder)
- Check the server URL in the sidebar matches your app's port (usually 5000 or 5001)
- Check browser console for CORS or connection errors

**"Playwright not found"**
- Run: `pwsh TestProject1/bin/Debug/net8.0/playwright.ps1 install`
- If on Linux/Mac: `pwsh` might be `powershell` or install PowerShell first

**"Server at https://localhost:5001 did not become ready"**
- The test is configured to check if the server is running first
- Just start your server manually before running the test:
  ```bash
  cd Client
  dotnet run
  ```
- Or set `AUTO_START_SERVER = true` in `InteractiveBlazorTest.cs` (line 15)

**"Changes not showing up"**
- Hard refresh your browser (Ctrl+F5 or Cmd+Shift+R)
- Clear browser cache
- Check if you're editing the right file (look in bin/Debug/net8.0/)

---

Happy experimenting! ??

**Pro Tip:** The Quick Test and Simple HTML Test harness work completely offline and don't require any setup. They're perfect for rapid prototyping!
