# JavaScript Testing Setup

## Running Tests Locally

### First-time Setup
```bash
cd Client
npm install
```

### Run Tests
```bash
# Run all tests once
npm test

# Run tests in watch mode (auto-rerun on file changes)
npm run test:watch

# Run tests with coverage report
npm run test:coverage
```

## Running in CI/CD

JavaScript tests run automatically in GitHub Actions pipeline:
- Triggered on every push and pull request
- Runs after .NET build, before Playwright tests
- Uploads coverage reports as artifacts

## Test Files

- `Client/wwwroot/js/logo-fast.test.js` - Unit tests for Logo JavaScript interpreter
- Tests verify command parsing, movement, position commands, pen control, etc.

## Parity Testing

The test suite ensures JavaScript interpreter (fast mode) has parity with C# implementation:
1. **JavaScript Unit Tests** (Jest) - Test parsing and command structure
2. **Playwright Integration Tests** - Test actual execution in browser
3. **Parity Checker Test** - Compares C# LogoCommandType enum with JavaScript commands

## Coverage

Coverage reports are generated in `Client/coverage/` directory and uploaded as pipeline artifacts.

## Troubleshooting

If tests fail with module errors:
```bash
cd Client
rm -rf node_modules package-lock.json
npm install
```

If Jest is not found:
```bash
npm install --save-dev jest
```
