# Troubleshooting & Fixes

Comprehensive documentation of issues encountered and their solutions during WordScape development.

## Table of Contents
- [Reproducibility Issues](#reproducibility-issues)
- [Random Number Generation](#random-number-generation)
- [Drag Interaction](#drag-interaction)
- [Debug Mode](#debug-mode)
- [Dictionary Service](#dictionary-service)
- [Index.html Caching](#indexhtml-caching)

---

## Reproducibility Issues

### Problem: Tests Produce Different Results Each Run

**Symptoms:**
- Different grid layouts each test run
- Different letter wheels
- Different target words
- Screenshots don't match

**Root Cause:**
Multiple Random instances being created with different (or time-based) seeds.

### Solution: Centralized RandomService

**Implementation:**

1. **Create RandomService.cs:**
```csharp
public class RandomService
{
    private Random? _random;
    private bool? _isDebugMode;
    private readonly object _lock = new object();
    
    public Random GetRandom()
    {
        lock (_lock)
        {
            if (_random == null || _isDebugMode != DebugHelper.IsDebugEnabled)
            {
                _isDebugMode = DebugHelper.IsDebugEnabled;
                int seed = _isDebugMode.Value ? 1 : Environment.TickCount;
                _random = new Random(seed);
            }
            return _random;
        }
    }
}
```

2. **Register as Singleton:**
```csharp
// Program.cs
builder.Services.AddSingleton<RandomService>();
```

3. **Use Everywhere:**
```csharp
// Instead of: new Random()
// Use: randomService.GetRandom()
```

**Benefits:**
- ? Single source of truth for all random numbers
- ? Consistent seed in debug mode
- ? Lazy initialization (waits for debug mode from URL)
- ? Thread-safe

---

## Random Number Generation

### Problem: Dictionary Service Creating Its Own Random

**Issue:**
DictionaryService was creating new Random instances, bypassing the centralized RandomService.

**Code Before (? WRONG):**
```csharp
public class DictionaryService : IDictionaryService
{
    public DictionaryService()
    {
        // Creates NEW random instances!
        _smallDict = new DictionaryLib(DictionaryType.Small);
        _largeDict = new DictionaryLib(DictionaryType.Large);
    }
}
```

**Code After (? CORRECT):**
```csharp
public class DictionaryService : IDictionaryService
{
    public DictionaryService(RandomService randomService)
    {
        var random = randomService.GetRandom();
        _smallDict = new DictionaryLib(DictionaryType.Small, random);
        _largeDict = new DictionaryLib(DictionaryType.Large, random);
    }
}
```

**Key Changes:**
1. Inject `RandomService` via constructor
2. Get shared Random instance
3. Pass it to DictionaryLib constructors

---

## Drag Interaction

### Problem: Letters Not Selecting When Clicked

**Symptoms:**
```
Clicking letter 0: E
Clicking letter 1: A
Current word formed: ''  ? EMPTY!
```

**Root Cause:**
WordScape uses **drag/swipe interaction**, not individual clicks.

### Solution: Use Mouse Drag Pattern

**Wrong Approach (?):**
```csharp
// Just clicking doesn't work
foreach (int index in selectedIndices)
{
    var letterElement = letterContainers[index];
    await letterElement.ClickAsync();  // ? Doesn't select
}
```

**Correct Approach (?):**
```csharp
// Drag through letters
var firstLetter = letterContainers[selectedIndices[0]];
var firstBox = await firstLetter.BoundingBoxAsync();

// 1. Mouse down on first letter
var startX = firstBox.X + firstBox.Width / 2;
var startY = firstBox.Y + firstBox.Height / 2;
await page.Mouse.MoveAsync(startX, startY);
await page.Mouse.DownAsync();

// 2. Drag through remaining letters
for (int j = 1; j < selectedIndices.Count; j++)
{
    var letterElement = letterContainers[selectedIndices[j]];
    var box = await letterElement.BoundingBoxAsync();
    
    var x = box.X + box.Width / 2;
    var y = box.Y + box.Height / 2;
    await page.Mouse.MoveAsync(x, y);
    await Task.Delay(150);
}

// 3. Mouse up to submit
await page.Mouse.UpAsync();
```

**Why This Works:**
- Mouse down ? starts selection
- Mouse move ? adds letters to selection
- Mouse up ? submits word

**Game Design:**
- **Desktop:** Click and drag
- **Mobile:** Swipe finger
- **Not:** Individual clicks

---

## Debug Mode

### Problem: Debug Mode Not Applied from URL

**Issue:**
Navigating to `?debug=true` didn't enable debug mode because Random was created before URL was parsed.

**Timeline:**

```
? WRONG ORDER:
1. Program.cs executes
2. RandomService created
3. Random instance created with time-based seed
4. URL parsed
5. Debug mode set (TOO LATE!)

? CORRECT ORDER:
1. Program.cs executes
2. RandomService created (NO Random yet)
3. URL parsed
4. Debug mode set
5. Random instance created with seed=1 (JUST IN TIME!)
```

### Solution: Lazy Initialization

**Implementation:**

```csharp
public class RandomService
{
    private Random? _random;  // Nullable for lazy init
    
    public Random GetRandom()
    {
        if (_random == null)
        {
            // Called AFTER debug mode is set from URL
            int seed = DebugHelper.IsDebugEnabled ? 1 : Environment.TickCount;
            _random = new Random(seed);
        }
        return _random;
    }
}
```

**Key Points:**
- Random is `null` initially
- Created on first `GetRandom()` call
- By then, URL has been parsed
- Debug mode is correctly applied

**URL Parameter:**
```csharp
// App.razor or WordScapeGame.razor
protected override void OnInitialized()
{
    var uri = new Uri(NavigationManager.Uri);
    var query = HttpUtility.ParseQueryString(uri.Query);
    
    if (query["debug"] == "true")
    {
        DebugHelper.SetDebugMode(true);
    }
}
```

---

## Dictionary Service

### Problem: Multiple Dictionary Instances

**Issue:**
Creating new DictionaryLib instances for each game was expensive (100MB+ memory, slow startup).

**Before (?):**
```csharp
// Each game creates NEW dictionaries
var smallDict = new DictionaryLib(DictionaryType.Small);  // 50MB
var largeDict = new DictionaryLib(DictionaryType.Large);  // 100MB
```

**After (?):**
```csharp
// Share ONE dictionary across all games
public class DictionaryService : IDictionaryService
{
    private readonly DictionaryLib _smallDict;
    private readonly DictionaryLib _largeDict;
    
    public DictionaryService(RandomService randomService)
    {
        var random = randomService.GetRandom();
        _smallDict = new DictionaryLib(DictionaryType.Small, random);
        _largeDict = new DictionaryLib(DictionaryType.Large, random);
    }
    
    public DictionaryLib SmallDictionary => _smallDict;
    public DictionaryLib LargeDictionary => _largeDict;
}
```

**Benefits:**
- ? One-time 150MB allocation (not per game)
- ? Faster game startup
- ? Lower memory usage
- ? Consistent random seed

---

## Index.html Caching

### Problem: Changes Not Reflected in Browser

**Symptoms:**
- Modified `index.html` but changes don't appear
- Browser shows old version
- Hard refresh (Ctrl+F5) doesn't help

**Root Cause:**
Service Worker caching `index.html` aggressively.

### Solution: Cache Busting

**Add to index.html:**
```html
<meta http-equiv="Cache-Control" content="no-cache, no-store, must-revalidate">
<meta http-equiv="Pragma" content="no-cache">
<meta http-equiv="Expires" content="0">
```

**Update Service Worker:**
```javascript
// sw.js
const CACHE_NAME = 'wordscape-v2';  // Increment version

self.addEventListener('activate', event => {
    event.waitUntil(
        caches.keys().then(cacheNames => {
            return Promise.all(
                cacheNames
                    .filter(name => name !== CACHE_NAME)
                    .map(name => caches.delete(name))
            );
        })
    );
});
```

**Development Workaround:**
```javascript
// Disable service worker in development
if (location.hostname === 'localhost') {
    navigator.serviceWorker.getRegistrations()
        .then(registrations => {
            registrations.forEach(r => r.unregister());
        });
}
```

---

## Common Patterns

### Pattern: Two-Level Fixed Seed

**Problem:** Test and game in separate processes can't share Random instance.

**Solution:** Use two separate fixed seeds:

```
Test Process:     Random(1) ? Controls letter selection
                      ?
                  (Browser automation)
                      ?
Game Process:     Random(1) ? Controls grid generation
```

Both use seed=1, but in different processes, ensuring reproducibility.

### Pattern: Lazy Singleton Initialization

**Problem:** Need to initialize after configuration is loaded.

**Solution:**
```csharp
private T? _instance;

public T GetInstance()
{
    if (_instance == null)
    {
        // Read configuration first
        var config = LoadConfig();
        _instance = new T(config);
    }
    return _instance;
}
```

### Pattern: Diagnostic Logging

**Problem:** Hard to debug when Random is being created.

**Solution:**
```csharp
public Random GetRandom()
{
    if (_random == null)
    {
        var seed = DebugHelper.IsDebugEnabled ? 1 : Environment.TickCount;
        _random = new Random(seed);
        
        var randomId = _random.GetHashCode().ToString("X8");
        DebugHelper.Log($"?? Random created [ID:{randomId}, Seed:{seed}]");
    }
    return _random;
}
```

**Benefits:**
- Track when Random is created
- Verify correct seed
- Identify multiple instances

---

## Verification

### Check Reproducibility

**PowerShell Script:**
```powershell
# Run test twice
dotnet test --filter "AutomatedTest_RandomLetterSelection" > run1.txt
dotnet test --filter "AutomatedTest_RandomLetterSelection" > run2.txt

# Compare (ignore timestamps)
$run1 = Get-Content run1.txt | Where-Object { $_ -notmatch '\d{2}:\d{2}:\d{2}' }
$run2 = Get-Content run2.txt | Where-Object { $_ -notmatch '\d{2}:\d{2}:\d{2}' }

Compare-Object $run1 $run2
```

**Expected:** No differences (except timestamps)

### Check Random Usage

**Add Logging:**
```csharp
var randomId = random.GetHashCode().ToString("X8");
DebugHelper.Log($"?? Using Random [ID:{randomId}]");
```

**Verify:** All components use same Random ID

### Check Debug Mode

**Console Output:**
```
?? Debug mode enabled from URL
?? RandomService: Creating Random with seed=1
? Grid created with reproducible seed
```

---

## Performance Issues

### Issue: Slow Test Startup

**Cause:** Starting Blazor server takes 10-30 seconds.

**Solutions:**

1. **Reuse Running Server:**
```csharp
private const bool AUTO_START_SERVER = false;  // Start manually
```

2. **Check if Running First:**
```csharp
if (await IsServerRunning(BASE_URL))
{
    Console.WriteLine("Reusing existing server");
}
else
{
    _dotnetProcess = StartBlazorServer();
}
```

### Issue: Slow Dictionary Loading

**Cause:** Loading 150MB of dictionary data.

**Solution:** Singleton DictionaryService (load once, use everywhere)

### Issue: Slow Grid Generation

**Cause:** Complex word placement algorithm.

**Optimization:**
- Cache character positions
- Pre-sort words by length
- Use HashSet for duplicate detection

---

## Best Practices

### 1. Always Use Centralized Random

```csharp
? var random = new Random();
? var random = randomService.GetRandom();
```

### 2. Enable Debug Mode for Tests

```csharp
await page.GotoAsync($"{BASE_URL}/wordscape?debug=true");
```

### 3. Use Lazy Initialization

```csharp
// Wait for configuration before creating instances
if (_instance == null)
{
    var config = LoadConfiguration();
    _instance = CreateInstance(config);
}
```

### 4. Add Diagnostic Logging

```csharp
DebugHelper.Log($"?? Component initialized with Random [ID:{randomId}]");
```

### 5. Verify Reproducibility

```bash
# Run same test multiple times
for i in 1..5; do
    dotnet test --filter "MyTest" > "run$i.txt"
done

# All outputs should be identical
```

---

## Summary

### Key Fixes Applied

1. ? **Centralized RandomService** - Single source of truth
2. ? **Lazy Initialization** - Wait for URL parsing
3. ? **Drag Interaction** - Proper mouse events
4. ? **Singleton Dictionary** - One instance per app
5. ? **Debug Mode from URL** - `?debug=true` works
6. ? **Diagnostic Logging** - Track Random usage
7. ? **Two-Level Seeds** - Test + Game reproducibility

### Architecture

```
URL ? DebugHelper ? RandomService ? All Components
                        ?
                  Same Random Instance
                        ?
              Reproducible Results
```

### Reproducibility Guarantees

With `debug=true`:
- ? Same grid layout every time
- ? Same letter wheel every time  
- ? Same target word every time
- ? Same test behavior every time
- ? Same screenshots every time

All issues have been resolved, and the system is now fully reproducible for testing and debugging.
