# CalvinHsia.com - Blazor WebAssembly Portfolio & Games

A portfolio website and interactive games collection built with Blazor WebAssembly and Azure Functions. Features multiple educational games, creative tools, and a photo management application with Microsoft Graph API integration.

## Games & Features

### 🐟 Fish vs Sharks - Cellular Automata
- **Wator Simulation**: Based on A.K. Dewdney's December 1984 Scientific American article
- **Dynamic Ecosystem**: Watch predator-prey populations evolve over time
- **Interactive Controls**: Add fish (left-click) or sharks (right-click) during simulation
- **Real-time Stats**: Generation counter, population tracking, and generations per second
- **Customizable Rules**: Adjust breeding ages, lifespans, starvation rates, and more
- **Display Options**: Toggle between rectangles/circles, torus wrapping, cell sizes, and age-based coloring
- **Performance**: Optimized byte-packed rendering for smooth high-speed simulation
- **Data Export**: Export population data to CSV for analysis

### 🔤 WordScape - Word Puzzle Game
- **Letter Grid Gameplay**: Find words in a grid of random letters
- **Smart Word Detection**: Dictionary-based validation of discovered words
- **Score Tracking**: Points based on word length and complexity
- **Responsive Design**: Adaptive layout for all screen sizes

### 🔠 Wordament - Word Search Game
- **Timed Challenges**: Race against the clock to find words
- **Path Finding**: Connect adjacent letters to form words
- **Leaderboard**: Track high scores and personal bests

### 🐢 Logo - Turtle Graphics Programming
- **Educational Programming**: Learn programming through turtle graphics
- **Command Language**: Simple Logo-like commands (FORWARD, LEFT, RIGHT, etc.)
- **Visual Output**: Real-time drawing on HTML canvas
- **Interactive Learning**: Experiment with loops, variables, and procedures

### 🎨 Cartoon - Frame Animation Tool
- **Frame-by-Frame Animation**: Create animations with drawing tools
- **Multi-Frame Editor**: Manage multiple animation frames
- **Export Options**: Save your animations for sharing
- **Drawing Tools**: Pencil, shapes, colors, and eraser

### ⚽ Bounce - Physics Simulation
- **Realistic Physics**: Bouncing balls with gravity and collision detection
- **Interactive**: Add, remove, and modify balls during simulation
- **Customizable**: Adjust gravity, elasticity, and other physics parameters

### 📸 MyPix - Photo Management (Microsoft Graph Integration)
- **OneDrive Integration**: Search and browse your OneDrive photos
- **Advanced Search**: Filter by date, type, filename patterns, and regex
- **Album Creation**: Create OneDrive albums from search results
- **SQLite Database**: Fast local querying with Entity Framework Core
- **Responsive Gallery**: Adaptive thumbnail layouts

## Technology Stack

### Frontend
- **Blazor WebAssembly** (.NET 8)
- **C# 12.0** with modern language features
- **HTML5 Canvas** for game rendering
- **Responsive CSS** with mobile-first design

### Backend
- **Azure Functions** (.NET 8, isolated worker)
- **Microsoft Graph API** for OneDrive integration
- **Entity Framework Core** with SQLite

### Testing
- **MSTest** for unit testing
- **Playwright** for end-to-end testing
- **Interactive Test Harness** for automated browser testing

### Development Tools
- **Visual Studio 2022**
- **.NET 8 SDK**
- **Azure Static Web Apps CLI** (optional)

## Project Structure

```
CalvinHsiaComWebSite/
├── Client/  # Blazor WebAssembly frontend
│   ├── Pages/          # Game pages (Fish, Cartoon, Logo, etc.)
│   ├── Games/          # Game-specific components and services
│   ├── wwwroot/        # Static assets, CSS, JS
│   └── Services/       # Shared services (RandomService, DebugHelper)
├── Api/         # Azure Functions backend
├── Shared/        # Shared models and utilities
├── TestProject1/       # MSTest + Playwright tests
│   ├── Interactive*Test.cs  # Interactive tests for each game
│   └── InteractiveTestBase.cs  # Base class for browser tests
└── README.md
```

## Getting Started

### Prerequisites
- Visual Studio 2022 or VS Code
- .NET 8 SDK
- Azure account (for deployment)
- Microsoft 365 account (for MyPix OneDrive integration)

### Local Development

1. **Clone the repository**
   ```bash
   git clone https://github.com/calvinhsia/CalvinHsiaComWebSite
   cd CalvinHsiaComWebSite
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Run the application**
   
   **Option 1: Visual Studio 2022**
   - Open `CalvinHsiaComWebSite.sln`
   - Set **Client** as startup project
   - Press **F5**
   - Navigate to `https://localhost:7193`

   **Option 2: Command Line**
   ```bash
   cd Client
   dotnet run
   ```

4. **Run tests**
   ```bash
   cd TestProject1
   dotnet test
   ```

### Configuration

For MyPix photo integration:
1. Register an app in Azure AD
2. Configure Microsoft Graph permissions:
   - `Files.Read`
   - `Files.ReadWrite`
   - `User.Read`
3. Update API configuration in `Api/local.settings.json`

## Key Features

### Performance Optimizations
- **Byte-packed rendering**: Fish game uses 1 byte per cell (type + age)
- **Deterministic randomness**: `RandomService` for reproducible game states
- **Canvas-based rendering**: Direct 2D drawing for smooth animations
- **Lazy initialization**: Resources loaded on-demand

### Code Quality
- **File-scoped namespaces** (C# 12)
- **Primary constructors** where applicable
- **Nullable reference types** enabled
- **Modern async patterns** with `ValueTask`
- **Comprehensive testing** with Playwright

### Responsive Design
- Mobile-first CSS
- Touch-friendly controls
- Adaptive layouts (1-3 column grids)
- Canvas touch event handling

## Contributing

This is a personal portfolio project, but suggestions and bug reports are welcome via GitHub issues.

## License

Copyright © Calvin Hsia

## Author

**Calvin Hsia**
- Website: [calvinhsia.com](https://calvinhsia.com)
- GitHub: [@calvinhsia](https://github.com/calvinhsia)

---

*Built with ❤️ using Blazor WebAssembly*