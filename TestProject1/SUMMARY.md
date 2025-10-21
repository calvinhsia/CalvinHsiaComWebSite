# Test Project Documentation

Comprehensive testing suite for WordScape Blazor WebAssembly application.

## Quick Links

### ?? Main Documentation

- **[TESTING_GUIDE.md](TESTING_GUIDE.md)** - Complete testing guide
  - Quick start
  - Interactive testing
  - Automated testing
  - Test harness
  - Reproducible tests
  - Configuration
  - Best practices

- **[TROUBLESHOOTING.md](TROUBLESHOOTING.md)** - All fixes and solutions
  - Reproducibility issues
  - Random number generation
  - Drag interaction fixes
  - Debug mode setup
  - Dictionary service
  - Performance optimization

## Quick Start

```bash
# Install Playwright (one-time)
pwsh TestProject1/bin/Debug/net8.0/playwright.ps1 install

# Run interactive test
dotnet test --filter "TestCategory=Interactive"

# Run automated test
dotnet test --filter "TestCategory=Automated"
```

## Test Files

### Interactive Tests
- `InteractiveWordScapeTest.cs` - WordScape game testing
- `InteractiveWordamentTest.cs` - Wordament game testing
- `InteractiveLogoTest.cs` - Logo turtle testing

### Unit Tests
- `TestWordScape.cs` - WordScape unit tests
- `TestWordament.cs` - Wordament unit tests
- `UnitTest1.cs` - Basic unit tests

### Test Harness
- `SimpleHtmlTestHarness.cs` - Static HTML testing

## Architecture

### Reproducible Testing System

```
URL (?debug=true)
    ?
DebugHelper
    ?
RandomService (Seed = 1)
    ?
All Game Components
    ?
Identical Results Every Time
```

### Two-Level Fixed Seed

**Test Seed (1):** Controls letter selection in tests  
**Game Seed (1):** Controls grid generation in game

Both work together for complete reproducibility.

## Key Features

? **Interactive Testing** - Manual browser exploration  
? **Automated Testing** - Scripted drag interactions  
? **Reproducible Results** - Fixed seed system  
? **Screenshots** - Visual verification  
? **Console Logging** - Debug output capture  
? **Memory Tracking** - Performance analysis

## Documentation Structure

```
TestProject1/
??? TESTING_GUIDE.md      ? How to test (procedures)
??? TROUBLESHOOTING.md    ? How to fix (issues/solutions)
??? SUMMARY.md            ? This file (index)
```

## Need Help?

1. **New to testing?** Start with [TESTING_GUIDE.md](TESTING_GUIDE.md)
2. **Something not working?** Check [TROUBLESHOOTING.md](TROUBLESHOOTING.md)
3. **Quick reference?** Use this SUMMARY.md

---

**Last Updated:** 2024  
**Maintainer:** Calvin Hsia  
**Framework:** .NET 8, MSTest, Playwright
