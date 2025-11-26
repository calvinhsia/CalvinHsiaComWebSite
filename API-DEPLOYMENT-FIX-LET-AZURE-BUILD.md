# Azure Static Web Apps - API Deployment Fix

## Problem

Deployment fails with:
```
Cannot deploy to the function app because Function language info isn't provided.
```

## Root Cause

When using `skip_api_build: true` with a pre-built .NET isolated Functions app, Azure Static Web Apps cannot detect the runtime language because:
1. The Functions metadata isn't properly generated in the publish output
2. The Oryx build system needs to see source code to detect .NET

## Solution: Let Azure Build the API from Source

### 1. Update Workflow

**Remove** this step from your workflow:
```yaml
- name: Publish Azure Functions API
  run: dotnet publish Api/Api.csproj --configuration Release --no-build --output ./dist/api
```

**Change** the deployment config from:
```yaml
api_location: "dist/api"    # Pre-built location
skip_api_build: true        # Skip build
```

**To:**
```yaml
api_location: "Api"         # Source code location
skip_api_build: false       # Let Azure build it
```

### 2. Complete Updated Workflow

Replace your workflow with this version:

```yaml
name: Azure Static Web Apps CI/CD

on:
  push:
    branches:
      - '**'
  pull_request:
    types: [opened, synchronize, reopened, closed]
    branches:
      - '**'

permissions:
  contents: read
  checks: write
  pull-requests: write

jobs:
  build_and_deploy_job:
    if: github.event_name == 'push' || (github.event_name == 'pull_request' && github.event.action != 'closed')
    runs-on: ubuntu-22.04
    name: Build and Deploy Job
    steps:
      - uses: actions/checkout@v4
        with:
          submodules: true
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      
      - name: Restore dependencies
        run: dotnet restore
      
      - name: Build solution
        run: dotnet build --configuration Release --no-restore
      
      - name: Publish Blazor WebAssembly
        run: dotnet publish Client/Client.csproj --configuration Release --no-build --output ./dist
      
      # API will be built by Azure from source - no pre-build needed
      
      - name: Copy Static Web App Config to wwwroot
        run: |
          if [ -f "./dist/staticwebapp.config.json" ]; then
            cp ./dist/staticwebapp.config.json ./dist/wwwroot/staticwebapp.config.json
            echo "? Copied staticwebapp.config.json to wwwroot"
          else
            echo "??  staticwebapp.config.json not found in ./dist/"
          fi
        shell: bash
      
      - name: List published files (debug)
        run: |
          echo "Contents of ./dist/wwwroot:"
          ls -la ./dist/wwwroot
          echo ""
          echo "Checking for required files:"
          if [ -f "./dist/wwwroot/index.html" ]; then
            echo "? index.html found in ./dist/wwwroot/"
          else
            echo "? index.html NOT found in ./dist/wwwroot/"
          fi
          if [ -f "./dist/wwwroot/staticwebapp.config.json" ]; then
            echo "? staticwebapp.config.json found in ./dist/wwwroot/"
          else
            echo "? staticwebapp.config.json NOT found in ./dist/wwwroot/"
          fi
          echo ""
          echo "API will be built by Azure from source in Api/ directory"
        shell: bash
      
      - name: Setup Node.js for JavaScript tests
        uses: actions/setup-node@v4
        with:
          node-version: '18'
          cache: 'npm'
          cache-dependency-path: Client/package-lock.json
      
      - name: Install JavaScript dependencies
        run: |
          cd Client
          npm ci
      
      - name: Run JavaScript Unit Tests
        run: |
          cd Client
          npm test -- --ci --coverage --maxWorkers=2
        continue-on-error: false
      
      - name: Upload JavaScript Test Coverage
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: javascript-coverage
          path: Client/coverage/
          retention-days: 30
      
      - name: Install Playwright Browsers
        run: pwsh TestProject1/bin/Release/net8.0/playwright.ps1 install --with-deps chromium
      
      - name: Run Unit Tests
        run: |
          dotnet test TestProject1/TestProject1.csproj \
            --configuration Release \
            --no-build \
            --verbosity normal \
            --filter "TestCategory!=Manual&TestCategory!=Interactive" \
            --logger "trx;LogFileName=test-results.trx"
        continue-on-error: false
      
      - name: Run Playwright Tests
        run: |
          dotnet test TestProject1/TestProject1.csproj \
            --configuration Release \
            --no-build \
            --filter "TestCategory=Automated" \
            --logger "trx;LogFileName=playwright-results.trx" \
            --logger "console;verbosity=detailed"
        continue-on-error: true
        env:
          PLAYWRIGHT_VIDEO: 'on'
          PLAYWRIGHT_SCREENSHOTS: 'on'
      
      - name: Publish Test Results Summary
        uses: EnricoMi/publish-unit-test-result-action@v2
        if: always()
        with:
          files: |
            **/TestResults/**/*.trx
          check_name: "Test Results"
          comment_mode: off
    
      - name: Upload Test Results
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: test-results
          path: |
            **/TestResults/**/*.trx
            **/TestResults/**/*.xml
          retention-days: 30
      
      - name: Upload Playwright Artifacts
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: playwright-artifacts
          path: |
            TestProject1/bin/Release/net8.0/playwright-videos/**/*
            TestProject1/bin/Release/net8.0/playwright-screenshots/**/*
            TestProject1/bin/Release/net8.0/playwright-traces/**/*
          retention-days: 30
      
      - name: Build And Deploy
        id: builddeploy
        uses: Azure/static-web-apps-deploy@v1
        with:
          azure_static_web_apps_api_token: ${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN_NICE_COAST_0273FF81E }}
          repo_token: ${{ secrets.GITHUB_TOKEN }}
          action: "upload"
          app_location: "dist/wwwroot"
          api_location: "Api"              # Source code location, not pre-built
          output_location: ""
          skip_app_build: true
          skip_api_build: false            # Let Azure build the API

  close_pull_request_job:
    if: github.event_name == 'pull_request' && github.event.action == 'closed'
    runs-on: ubuntu-22.04
    name: Close Pull Request Job
    steps:
      - name: Close Pull Request
        id: closepullrequest
        uses: Azure/static-web-apps-deploy@v1
        with:
          azure_static_web_apps_api_token: ${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN_NICE_COAST_0273FF81E }}
          action: "close"
```

### 3. Key Changes

1. **Removed**: API pre-build step
2. **Changed**: `api_location: "Api"` (source) instead of `"dist/api"` (pre-built)
3. **Changed**: `skip_api_build: false` to let Azure build it
4. **Simplified**: Debug logging (no need to check dist/api anymore)

## Why This Works

Azure Static Web Apps uses **Oryx** to detect and build Functions:

```
Source Code (Api/) ? Oryx Detects .NET 8 ? Builds Functions ? Deploys
```

When you pre-build, Oryx can't detect the language:

```
Pre-built DLLs (dist/api/) ? Oryx Can't Detect ? Fails ?
```

## Testing the Fix

1. **Close the diff view** of the workflow file in VS Code
2. **Copy the updated workflow** above into your workflow file
3. **Commit and push**:
   ```bash
   git add .github/workflows/azure-static-web-apps-nice-coast-0273ff81e.yml
   git add Api/host.json
   git add Api/.funcignore
   git commit -m "Fix: Let Azure build API from source for proper runtime detection"
   git push
   ```

4. **Monitor deployment** - it will take longer (~3-5 extra minutes) because Azure builds the API
5. **Check logs** - you should see:
   ```
   Detected .NET Core runtime in Api/
   Building .NET Functions...
   Successfully deployed Functions API
   ```

6. **Test endpoint**:
   ```bash
   curl "https://calvinhsia.com/api/QueryPix?Date1=1/1/1950&Date2=1/1/2030&MaxPix=10"
   ```

## Pros and Cons

### Letting Azure Build (Recommended)
? Proper runtime detection  
? Automatic deployment updates  
? No metadata configuration needed  
? Slower deployments (extra 3-5 minutes)  

### Pre-building (Not Recommended for Azure Static Web Apps)
? Faster deployments  
? Explicit control over build  
? Complex metadata setup  
? Runtime detection issues  
? May not work with Azure Static Web Apps at all

## Alternative: Use Azure Functions Separately

If you want faster deployments, consider deploying your API as a **separate Azure Functions app** instead of bundling it with Azure Static Web Apps:

1. Deploy Static Web App without API
2. Deploy Azure Functions separately
3. Configure CORS to allow your static site
4. Update API calls to point to Functions URL

This gives you:
- Faster static site deployments
- Independent API scaling
- Better separation of concerns

But it requires managing two separate Azure resources.

## Summary

**For Azure Static Web Apps, always let Azure build Functions from source code.** Pre-building .NET isolated Functions doesn't work well with the deployment system's runtime detection.
