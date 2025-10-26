# GitHub Copilot Instructions

## Project Overview
This is a Blazor WebAssembly application with multiple interactive games and features:
- **WordScape**: Word puzzle game
- **Wordament**: Word search game
- **Logo**: Turtle graphics programming game
- **Cartoon**: Frame-by-frame animation drawing tool
- **Bounce**: Physics simulation with bouncing balls

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

## Test Guidelines

### Interactive Tests
- Use `InteractiveTestBase` for all new Playwright tests
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
  - Special symbols: ?, ?, ?, ?, etc.

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

**Remember**: Focus on delivering working code with minimal documentation overhead.
