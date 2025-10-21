# WordScape Testing Guide

Complete guide for testing the WordScape Blazor WebAssembly application using Playwright and MSTest.

## Table of Contents
- [Quick Start](#quick-start)
- [Interactive Testing](#interactive-testing)
- [Automated Testing](#automated-testing)
- [Test Harness](#test-harness)
- [Reproducible Tests](#reproducible-tests)

---

## Quick Start

### Prerequisites
```bash
# Install Playwright browsers (one-time setup)
pwsh TestProject1/bin/Debug/net8.0/playwright.ps1 install
```

### Running Tests

**Interactive Mode** (browser stays open):
```bash
dotnet test --filter "TestCategory=Interactive"
```

**Automated Mode** (scripted testing):
```bash
dotnet test --filter "TestCategory=Automated"
```

**Specific Test**:
```bash
dotnet test --filter "FullyQualifiedName~AutomatedTest_RandomLetterSelection"
```

---

## Interactive Testing

### LaunchInteractiveBrowser_WordScapeGame

Opens a browser window where you can manually interact with the WordScape game.

**Features:**
- ? Browser stays open until you close it
- ? DevTools automatically opened
- ? Console logging enabled
- ? Slow motion (100ms delay between actions)

**Usage:**
```csharp
[TestMethod]
[TestCategory("Interactive")]
public async Task LaunchInteractiveBrowser_WordScapeGame()
```

**Tips:**
- Use F12 DevTools to inspect elements
- Modify CSS/JS and refresh to see changes
- Console shows all browser events
- Close browser window to end test

---

## Automated Testing

### AutomatedTest_RandomLetterSelection

Automatically selects random letters from the wheel to form words.

**Features:**
- ? Uses **fixed seed (1)** for reproducible results
- ? Drag interaction (mousedown ? mousemove ? mouseup)
- ? 10 attempts with screenshots
- ? Console logging and memory tracking

**How It Works:**

1. **Navigates** to `/wordscape?debug=true`
2. **Finds** letter containers in the wheel
3. **Randomly selects** 3-8 letters (using fixed seed)
4. **Drags mouse** across selected letters
5. **Captures** formed word and screenshot
6. **Repeats** 10 times

**Example Output:**
```
--- Attempt 1/10 ---
Selecting 5 random letters (using seed 1)...
Selected letter indices: 0, 1, 4, 2, 3
  Starting drag on letter 0: E
  Dragging to letter 1: A
  Dragging to letter 4: S
  Dragging to letter 2: C
  Dragging to letter 3: S
  Drag complete - releasing mouse
  ? Current word formed: 'EASCS'
  ?? Screenshot saved: wordscape-random-attempt-1.png
```

---

## Test Harness

### SimpleHtmlTestHarness

Creates static HTML pages for testing without running the full Blazor server.

**Features:**
- ? No server needed
- ? Fast test iterations
- ? Isolated component testing

**Example:**
```csharp
[TestMethod]
public async Task CreateStaticHtmlPage()
{
    var html = @"
        <!DOCTYPE html>
        <html>
            <body>
                <h1>Test Page</h1>
            </body>
        </html>";
    
    File.WriteAllText("test.html", html);
    // Navigate to file:///test.html
}
```

---

## Reproducible Tests

### The Two-Level Fixed Seed System

For complete reproducibility, we use **two separate fixed seeds**:

#### 1. Game Seed (Seed = 1)
Controlled by `debug=true` URL parameter:
- ? Same grid layout every time
- ? Same letter wheel every time
- ? Same target word every time

#### 2. Test Seed (Seed = 1)
Controlled by test's Random instance:
- ? Same letter selection every time
- ? Same drag path every time
- ? Same test results every time

### Why Two Seeds?

The test and game run in **separate processes**:

```
???????????????????????????????????????
?  Test Process (MSTest/Playwright)   ?
?  ?????????????????????????????????? ?
?  ? Random _random = new Random(1) ? ?  ? Test Seed
?  ? Controls: Which letters to click? ?
?  ?????????????????????????????????? ?
???????????????????????????????????????
                ? (Browser Automation)
???????????????????????????????????????
?  Browser Process (Blazor WASM)      ?
?  ?????????????????????????????????? ?
?  ? RandomService with seed=1      ? ?  ? Game Seed
?  ? Controls: Grid, letters, words ? ?
?  ?????????????????????????????????? ?
???????????????????????????????????????
```

### Verifying Reproducibility

Run the test twice and compare:
```bash
# First run
dotnet test --filter "AutomatedTest_RandomLetterSelection" > run1.txt

# Second run
dotnet test --filter "AutomatedTest_RandomLetterSelection" > run2.txt

# Compare (should be identical except timestamps)
diff run1.txt run2.txt
```

**What Should Match:**
- ? Letter wheel layout
- ? Selected letter indices
- ? Formed words
- ? Screenshots (pixel-perfect)

**What Might Differ:**
- ? Timestamps in logs
- ? Performance timing

---

## Drag Interaction

### Why Drag Instead of Click?

WordScape uses **swipe/drag interaction**, not individual clicks:

**Desktop:** Click and drag mouse across letters  
**Mobile:** Swipe finger across letters

### Implementation

```csharp
// ? WRONG: Individual clicks don't work
await letterElement.ClickAsync();

// ? RIGHT: Drag interaction works
await page.Mouse.MoveAsync(x, y);
await page.Mouse.DownAsync();      // Press on first letter
// ... move to other letters ...
await page.Mouse.UpAsync();        // Release to submit
```

### The Drag Pattern

```
Mouse Down ? Move ? Move ? Move ? Mouse Up
    ?         ?      ?      ?       ?
  Letter 1  Letter 2 Letter 3 Letter 4  Submit Word
    (E)       (A)      (S)      (T)
```

---

## Configuration

### Browser Options

```csharp
new BrowserTypeLaunchOptions
{
    Headless = false,    // Show browser window
    SlowMo = 300,       // 300ms delay (visible actions)
    Devtools = true     // Open DevTools
}
```

### Viewport Size

```csharp
new BrowserNewContextOptions
{
    ViewportSize = new ViewportSize 
    { 
        Width = 1280, 
        Height = 1600 
    }
}
```

### Timeouts

```csharp
new PageWaitForSelectorOptions
{
    State = WaitForSelectorState.Attached,
    Timeout = 10000  // 10 seconds
}
```

---

## Screenshots

Automatically saved to test project directory:

- `wordscape-random-attempt-1.png` through `-10.png`
- `wordscape-random-final.png`

**Location:** `TestProject1/bin/Debug/net8.0/`

---

## Console Logging

All browser console messages are captured:

```csharp
page.Console += (_, msg) =>
{
    Console.WriteLine($"[Browser Console] {msg.Text}");
};
```

**Useful for:**
- Debugging JavaScript errors
- Monitoring game state
- Tracking performance
- Verifying debug mode

---

## Memory Tracking

CDP (Chrome DevTools Protocol) session enabled:

```csharp
var client = await page.Context.NewCDPSessionAsync(page);
```

**Capabilities:**
- Memory heap analysis
- Performance profiling
- Network monitoring
- Code coverage

---

## Troubleshooting

### Test Fails to Start Server

**Error:** `Server is not running at https://localhost:7193`

**Solution:**
```bash
# Start server manually
cd Client
dotnet run
```

Or set `AUTO_START_SERVER = false` in test class.

### Letters Not Selecting

**Issue:** Clicking letters doesn't form words

**Cause:** Using clicks instead of drag interaction

**Fix:** Use the drag pattern (already implemented in current version)

### Non-Reproducible Results

**Issue:** Tests produce different results each run

**Check:**
1. ? Using `debug=true` parameter?
2. ? Test seed is fixed (`new Random(1)`)?
3. ? Random instance is reset in `TestInitialize`?

### Screenshots Not Saving

**Location:** Check `TestProject1/bin/Debug/net8.0/`

Not the test project root directory!

---

## Best Practices

### 1. Always Use Debug Mode for Reproducibility
```csharp
await page.GotoAsync($"{BASE_URL}/wordscape?debug=true");
```

### 2. Reset Random Seed Per Test
```csharp
[TestInitialize]
public void TestInitialize()
{
    _random = new Random(1);
}
```

### 3. Use Proper Timing
```csharp
await Task.Delay(100);   // After mouse down
await Task.Delay(150);   // Between letters
await Task.Delay(500);   // After mouse up
```

### 4. Always Clean Up
```csharp
[TestCleanup]
public async Task TestCleanup()
{
    if (_browser != null && _browser.IsConnected)
    {
        await _browser.CloseAsync();
    }
}
```

---

## Advanced Topics

### Creating New Tests

1. **Interactive Test Template:**
```csharp
[TestMethod]
[TestCategory("Interactive")]
public async Task MyInteractiveTest()
{
    _browser = await _playwright!.Chromium.LaunchAsync(new()
    {
        Headless = false,
        SlowMo = 100,
        Devtools = true
    });
    
    var page = await (await _browser.NewContextAsync()).NewPageAsync();
    await page.GotoAsync($"{BASE_URL}/wordscape");
    
    // Your test code here
    
    while (_browser.IsConnected)
    {
        await Task.Delay(1000);
    }
}
```

2. **Automated Test Template:**
```csharp
[TestMethod]
[TestCategory("Automated")]
public async Task MyAutomatedTest()
{
    _browser = await _playwright!.Chromium.LaunchAsync(new()
    {
        Headless = false,
        SlowMo = 300
    });
    
    var page = await (await _browser.NewContextAsync(new()
    {
        ViewportSize = new() { Width = 1280, Height = 1600 }
    })).NewPageAsync();
    
    await page.GotoAsync($"{BASE_URL}/wordscape?debug=true");
    
    // Your test code here
    
    await page.ScreenshotAsync(new() { Path = "test-result.png" });
}
```

---

## Reference

### Key Files

- `InteractiveWordScapeTest.cs` - Main test class
- `InteractiveWordamentTest.cs` - Wordament tests
- `InteractiveLogoTest.cs` - Logo turtle tests
- `SimpleHtmlTestHarness.cs` - Static HTML tests

### Dependencies

- **Microsoft.Playwright** - Browser automation
- **MSTest** - Test framework
- **Chrome/Chromium** - Browser engine

### Resources

- [Playwright Documentation](https://playwright.dev/dotnet/)
- [MSTest Documentation](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-mstest)
- [Blazor WebAssembly](https://learn.microsoft.com/en-us/aspnet/core/blazor/)

---

## Summary

? **Interactive tests** for manual exploration  
? **Automated tests** with drag interaction  
? **Reproducible results** with dual fixed seeds  
? **Screenshots** for visual verification  
? **Console logging** for debugging  
? **Memory tracking** via CDP  

The testing framework provides comprehensive coverage for WordScape game functionality with full reproducibility for regression testing and debugging.
