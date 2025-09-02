# Copilot Development Instructions

## Repository Structure

This repository contains **3 games** (with potential for more):

### Current Games
1. **WordScape** - A circular letter wheel word game where players find words from letters arranged in a circle
   - Files: `Client/Pages/WordScapeGame.razor`, `Client/Games/WordScape/Services/WordScapeGameService.cs`
   - CSS: `Client/wwwroot/css/wordscape-game.css`

2. **Wordament** - A 4x4 grid word game similar to Boggle where players find words by connecting adjacent letters
   - Files: `Client/Pages/WordamentGame.razor`, `Client/Games/WordScape/Services/WordamentGameService.cs`
   - Models: `Client/Games/WordScape/Models/WordamentModels.cs`

3. index.razor has a tiny word game: "Find the snaking 10-16 letter word"
1. **[Future Game]** - Prepared for but not yet implemented

### Shared Components
- **DictionaryService** - Shared word validation across all games
- **DebugHelper** - Shared debugging utilities
- **GameStateService** - Shared game state persistence
- **Word Dictionary** - Common word lists used by both games

## Development Guidelines

### 🎯 Single Game Focus Rule
**IMPORTANT:** Typically work on only **ONE GAME AT A TIME**. When a request mentions a specific game:
- Only modify files related to that specific game
- Do NOT touch files belonging to other games unless explicitly requested
- Be very careful about shared components - changes affect all games

### Game-Specific Files

**WordScape Files (DO NOT MODIFY unless specifically requested):**
- `Client/Pages/WordScapeGame.razor`
- `Client/Games/WordScape/Services/WordScapeGameService.cs` 
- `Client/wwroot/css/wordscape-game.css`

**Wordament Files:**
- `Client/Pages/WordamentGame.razor`
- `Client/Games/WordScape/Services/WordamentGameService.cs`
- `Client/Games/WordScape/Models/WordamentModels.cs`
- `Client/wwroot/css/wordament-game.css`

**Shared Files (Modify with EXTREME caution):**
- `Client/Games/WordScape/Services/DictionaryService.cs`
- `Client/Games/WordScape/Services/DebugHelper.cs`
- `Client/Games/WordScape/Services/GameStateService.cs`
- Word dictionary files

### When Working on Wordament
- Focus only on Wordament-related files
- Test changes don't break WordScape functionality
- Shared dictionary changes should benefit both games

### When Working on WordScape  
- Focus only on WordScape-related files
- Avoid modifying Wordament code
- Test shared component changes don't affect Wordament

### Exceptions to Single-Game Rule
Only modify multiple games simultaneously when:
1. **Dictionary improvements** - Both games share word validation
2. **Debug system changes** - Both games use DebugHelper
3. **Common UI/UX patterns** - Cross-game consistency improvements
4. **Shared infrastructure** - Game state management, etc.

### CSS Organization
- Each game has its own CSS sections in `wordscape-game.css`
- Shared styles are clearly marked
- Game-specific styles are prefixed (`.wordscape-*`, `.wordament-*`)

### Testing Approach
- Test files are separated: `TestWordScape.cs`, `TestWordament.cs`
- When modifying shared components, run tests for ALL games
- When modifying game-specific code, focus on that game's tests

### Navigation & Routing
- Games are accessible via `/wordscape` and `/wordament` routes
- Navigation menu in `Client/Shared/NavMenu.razor` lists all games
- Each game is independent in terms of user experience

## Current Status
- **WordScape**: Fully functional, production-ready
- **Wordament**: Fully functional, production-ready  
- **Future Game**: Architecture prepared, awaiting implementation

Remember: **One game at a time** unless explicitly working on shared functionality!