# GitHub Copilot Instructions

## Project Overview
This is a Blazor WebAssembly application with multiple interactive games and features:
- **WordScape**: Word puzzle game
- **Wordament**: Word search game
- **Logo**: Turtle graphics programming game
- **Cartoon**: Frame-by-frame animation drawing tool
- **Bounce**: Physics simulation with bouncing balls
- **Fish**: Fish vs Sharks cellular automata simulation

## Build Requirements

### Required SDK and Workloads
This project requires the following to build:

1. **.NET 8 SDK** - The project targets .NET 8 and uses a `global.json` to pin the SDK version
2. **wasm-tools workload** - Required for `WasmEnableSIMD=false` setting (iPad compatibility)

### First-time Setup on a New Machine
```bash
# Install .NET 8 SDK (if not already installed)
# Download from: https://dotnet.microsoft.com/download/dotnet/8.0
# Or use winget: winget install Microsoft.DotNet.SDK.8

# Install the wasm-tools workload (from the repo root)
dotnet workload install wasm-tools

# Build the project
dotnet build
```

### Why WasmEnableSIMD=false?
.NET 8 Blazor WebAssembly requires SIMD (Single Instruction Multiple Data) by default.
SIMD is **not supported** on older browsers:
- **iPad/iPhone**: Requires iOS 16.4+ (released March 2023)
- **Safari on Mac**: Requires macOS 13.3+

By setting `WasmEnableSIMD=false`, the app works on **all iPads** regardless of iOS version.

## General Coding Guidelines

### Documentation
- **DO NOT** create new `.md` files for every change or fix
- Only create documentation when:
  - Explicitly requested by the user
  - It's a major new feature requiring standalone documentation
  - There's no existing relevant documentation to update

### Code Changes
- Provide brief summaries in chat responses
- Make the changes directly without verbose documentation
- Use code comments for complex logic explanations

### Project Structure
- **Client**: Blazor WASM frontend (port 7193)
- **Api**: Azure Functions backend
- **Shared**: Shared models and utilities
- **TestProject1**: MSTest + Playwright interactive tests

## Technology Stack
- .NET 8
- Blazor WebAssembly
- C# 12.0
- Playwright (for E2E testing)
- MSTest
- Azure Functions (isolated worker)

## AI Assistant File Access Guidelines

### CRITICAL: Avoid Reading Diff/Temp Files Instead of Real Files

**Problem**: Visual Studio creates temporary comparison files in `TFSTemp` when showing diffs. AI assistants can accidentally read these diff files instead of the real source files, leading to:
- Reporting duplicate code that doesn't exist (red/green diff lines misinterpreted as duplicates)
- Incorrect analysis of current file state
- Confusion about what changes have been applied

#### How to Identify Temp Files

Temp files have paths like:
```
..\..\..\AppData\Local\Temp\TFSTemp\vctmp44372_208553.Fish.00000000.razor
```

Key indicators:
- Path contains `AppData\Local\Temp\TFSTemp`
- Filename has pattern `vctmpXXXXX_NNNNNN.OriginalName.00000000.ext`

#### Best Practices for AI Assistants

**ALWAYS:**
1. ? **Use `get_file(path)` with explicit path** instead of `get_currentfile()` when you need actual file content
2. ? **Check IDESTATE for TFSTemp files** - if present, user likely has diff view open
3. ? **Specify full relative path** from workspace root (e.g., `Client/Pages/Fish.razor`)
4. ? **Verify file path** doesn't contain `TFSTemp` before analyzing content

**NEVER:**
1. ? Don't use `get_currentfile()` if IDESTATE shows TFSTemp files are open
2. ? Don't report "duplicate code" without verifying you're reading the real file
3. ? Don't analyze diff markers (red/green lines) as if they're real code

#### Example: Correct File Access

```typescript
// ? WRONG - might read diff file if user has comparison open
const content = await get_currentfile();

// ? CORRECT - always reads the real file
const content = await get_file("Client/Pages/Fish.razor");
```

#### When User Reports Issues

If user says "there are no duplicates" or "that's a diff view":
1. Apologize for the confusion
2. Use `get_file()` with explicit path to get real content
3. Re-analyze based on actual file content
4. Learn from the mistake for future interactions

## Cache Detection and Verification

### CRITICAL: Always Verify Code Changes Are Actually Running

When debugging issues or making code fixes, **browser and Blazor WASM caching can make it appear that code hasn't changed even after rebuilding**. This leads to wasted time debugging "phantom issues" that were already fixed.

#### Best Practice: Add Version Markers to Console Logs

**Always add unique version markers** to console.log statements to verify which version of code is running:

```javascript
// JavaScript - Increment version number with each change
console.log('[Fish JS v8] Initializing canvas');  // Was v7, now v8
```

```csharp
// C# - Add version/timestamp to debug logs
DebugHelper.Log("[Fish v3.0] OnAfterRenderAsync - FIXED auto-start version", true);
```

#### Why This Matters

- **JavaScript files** can be cached by the browser even with hard refresh
- **Blazor WASM DLLs** are aggressively cached and may not reload
- **Service Workers** can serve stale content
- Version markers in logs **immediately confirm** which code version is running

#### When to Add Version Markers

1. **Before debugging** - Add version marker to confirm issue exists
2. **After making a fix** - Change version marker to verify fix is running
3. **When user reports "not working"** - Check logs for version marker mismatch

#### How to Clear Caches Properly

If version markers show old code is running:

1. **Increment cache busters** in `index.html`:
   ```html
   <script src="js/fish-game.js?v=8"></script>  <!-- Was v=7 -->
   ```

2. **Clean and rebuild**:
   ```bash
   dotnet clean
   dotnet build
   ```

3. **Hard refresh browser** (Ctrl+Shift+R or Cmd+Shift+R)

4. **Clear all browser cache**:
   - F12 ? Application ? Clear storage ? Clear site data

5. **Test in Incognito/Private window** to bypass all caches

#### Example: Version Marker Workflow

```
1. User reports: "Auto-start not working"
2. Check logs: "[Fish v2.0] OnAfterRenderAsync" ? Correct version
3. Make fix, increment version: "[Fish v2.1] OnAfterRenderAsync"
4. User tests, check logs: Still shows "v2.0" ? Cached!
5. Increment cache buster in index.html: ?v=8
6. User tests, check logs: Now shows "v2.1" ? Fix is running
7. Verify fix actually works
```

## Test Guidelines

### ⚠️ CRITICAL: Never Run Manual Tests

**Manual tests (`Manual_` prefix / `[TestCategory("Manual")]`) require human interaction and will hang indefinitely.** Always exclude them when running tests.

```bash
# ✅ Run all automated tests (excludes manual tests that hang)
dotnet test --filter "FullyQualifiedName!~Manual_"

# ✅ Run only fast unit tests (exclude Playwright AND manual)
dotnet test --filter "FullyQualifiedName!~Interactive&FullyQualifiedName!~Manual_"

# ✅ Run specific interactive test
dotnet test --filter "FullyQualifiedName~InteractiveLogoTest"

# ❌ WRONG - Includes manual tests that hang waiting for user input
dotnet test
```

### Test Categories

**Automated tests** (run in CI/CD and locally):
- **Unit tests**: Fast logic tests (e.g., `TestMyPixSerialization`, `TestPictureQuery`, `TestWordScape`, `TestFreeCell`)
- **Integration tests**: Test component interactions
- **Interactive tests**: Automated Playwright/browser tests that run to completion without user intervention

**Manual tests** (excluded from automated runs):
- Require user interaction to complete (e.g., clicking buttons, visual inspection)
- Must be ended manually by the user
- Use `[TestCategory("Manual")]` attribute AND prefix test methods with `Manual_` (e.g., `Manual_LaunchInteractiveBrowser_FreeCell`)

### Test Naming Conventions

- **Automated interactive tests**: Use `Interactive` prefix on class name (e.g., `InteractiveLogoTest`)
  - These are automated E2E tests using Playwright
  - Run to completion without user intervention
  - Should run in CI/CD pipeline
  
- **Manual tests**: Use `Manual_` prefix on method name (e.g., `Manual_VisualCheck`)
  - Require user interaction or manual completion
  - Excluded from automated test runs
  - Used for exploratory testing and visual validation

### Interactive Test Setup
- Use `InteractiveTestBase` for all Playwright tests
- Tests auto-start server on port 7193 if not running
- Proper cleanup implemented (see `InteractiveTestBase.cs`)
- Don't create redundant cleanup code

### Port Management
- Port 7193 cleanup is automated in test base class
- `kill-port-7193.ps1` available for manual intervention
- No need to document port issues repeatedly

## Common Patterns

### File Encoding and Unicode Characters
**IMPORTANT**: When creating `.razor` files with Unicode characters (emoji, special symbols):
- The file will be created without UTF-8 BOM by default
- Visual Studio will prompt to save as Unicode when Unicode characters are detected
- **Always click "Yes"** to save as UTF-8 with BOM
- This is the standard encoding for Blazor `.razor` files
- Examples of Unicode characters that require this:
  - Emoji: ??, ??, ??, ??, etc.
  - Special symbols: ?, ?, ?, ??, etc.

**Note to AI assistants**: The `create_file` and `edit_file` tools cannot control file encoding or BOM. When creating files with Unicode characters, inform the user they will need to save the file as UTF-8 with BOM when prompted by Visual Studio.

### Canvas Coordinate Handling
When working with HTML canvas and touch events:
```csharp
// Always account for canvas scaling
var scaleX = canvasWidth / displayWidth;
var scaleY = canvasHeight / displayHeight;
x = touchX * scaleX;
y = touchY * scaleY;
```

### Blazor Component Lifecycle
- Use `OnAfterRenderAsync(firstRender)` for JS initialization
- Check `firstRender` before initializing JS interop
- Dispose timers and resources in `Dispose()`

### JavaScript Interop
- Prefer dedicated JS functions over `eval`
- Use typed parameters in `InvokeAsync<T>`
- Handle JS exceptions gracefully

## Response Format Preferences
- Be concise in explanations
- Focus on the fix, not the analysis process
- Provide code directly, not just suggestions
- Build and verify changes automatically when appropriate
- **Always add version markers to verify code changes are actually running**

## Code Style
- Use C# 12 features (file-scoped namespaces, primary constructors, etc.)
- Follow existing naming conventions in the project
- Use `var` for local variables when type is obvious
- Keep methods focused and single-purpose

## Testing Approach
- Write reproducible tests with fixed seeds
- Use `RandomService` for deterministic randomness in tests
- Prefer integration tests over unit tests for game logic
- Interactive tests for UI/UX validation

## Performance Considerations
- Lazy initialization for expensive resources
- Cache dictionary lookups
- Use `ValueTask` for frequently-called async methods
- Avoid allocations in hot paths (e.g., animation loops)

---

**Remember**: Focus on delivering working code with minimal documentation overhead. **Always add version markers to verify code changes are actually running.**
