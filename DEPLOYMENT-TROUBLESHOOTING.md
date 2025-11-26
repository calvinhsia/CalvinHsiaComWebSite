# Azure Static Web Apps Deployment - 404 Troubleshooting Guide

## Problem
Tests pass in GitHub Actions, but deployed site returns 404 errors.

## Root Causes

### 1. App Location Configuration
Azure Static Web Apps needs to know where your built static files are located.

**Current Configuration**:
```yaml
app_location: "dist/wwwroot"
output_location: ""
skip_app_build: true
```

This tells Azure:
- Look in `dist/wwwroot/` for static files
- Don't run any build process
- Copy everything as-is to the hosting environment

### 2. Blazor WASM Publish Output Structure
When you run `dotnet publish Client/Client.csproj --output ./dist`, the structure is:
```
dist/
??? wwwroot/              ? This is where index.html lives
?   ??? index.html        ? Main entry point
?   ??? _framework/       ? Blazor WASM framework files
?   ??? css/
?   ??? js/
?   ??? ...
??? staticwebapp.config.json  ? Config file (needs to be in wwwroot/)
??? other files...
```

## Diagnostic Steps

### Step 1: Verify Local Publish Output
```bash
# Clean and publish locally
dotnet clean
dotnet publish Client/Client.csproj --configuration Release --output ./test-dist

# Check structure
ls ./test-dist/wwwroot/index.html
# Should show: ./test-dist/wwwroot/index.html exists

# Check if staticwebapp.config.json is in the right place
ls ./test-dist/wwwroot/staticwebapp.config.json
```

### Step 2: Check GitHub Actions Logs
1. Go to your repo ? **Actions** tab
2. Click latest workflow run
3. Expand **"Build and Deploy Job"**
4. Look for **"List published files (debug)"** step
5. Verify output shows:
   ```
   ? index.html found in ./dist/wwwroot/
   ```

### Step 3: Check Azure Portal
1. Open [Azure Portal](https://portal.azure.com)
2. Find your Static Web App resource
3. Go to **Deployment** ? **Overview**
4. Check latest deployment status
5. Click deployment to see logs

### Step 4: Verify Routing Configuration

Check `Client/staticwebapp.config.json`:

```json
{
  "navigationFallback": {
    "rewrite": "index.html",
    "exclude": [ "/images/*.{png,jpg,gif}", "/css/*" ]
  }
}
```

This tells Azure to serve `index.html` for all routes not matching excludes.

### Step 5: Test Specific URLs

Try these URLs on your deployed site:

1. **Root**: `https://your-site.azurestaticapps.net/`
   - Should load Blazor app
   
2. **Direct route**: `https://your-site.azurestaticapps.net/logo`
   - Should load logo page via Blazor routing
   
3. **Static file**: `https://your-site.azurestaticapps.net/css/logo-game.css`
   - Should serve CSS file directly

If **root works** but **routes return 404**:
- Problem: `navigationFallback` not configured
- Fix: Ensure `staticwebapp.config.json` is in `wwwroot/`

If **nothing works** (all 404):
- Problem: Files not deployed or wrong `app_location`
- Fix: Check GitHub Actions logs for file structure

## Common Fixes

### Fix 1: Ensure staticwebapp.config.json is Deployed

The config file must be in `wwwroot/` for Azure to find it.

**Option A: Copy during publish** (automated):
Add to `Client.csproj`:
```xml
<ItemGroup>
  <Content Include="staticwebapp.config.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
  </Content>
</ItemGroup>
```

**Option B: Copy in workflow** (manual):
```yaml
- name: Copy config to wwwroot
  run: cp Client/staticwebapp.config.json ./dist/wwwroot/
```

### Fix 2: Verify app_location Path

Test locally that the path is correct:
```bash
cd dist/wwwroot
ls index.html  # Should exist
```

If `index.html` is not directly under `app_location`, Azure won't find it.

### Fix 3: Clear Browser Cache

After deployment, browsers may cache old content:
- **Hard refresh**: Ctrl+Shift+R (Windows) or Cmd+Shift+R (Mac)
- **Incognito mode**: Test in private browsing window
- **Clear cache**: Developer Tools ? Application ? Clear storage

### Fix 4: Check API Configuration

If you have an API, it needs separate configuration:

```yaml
api_location: "Api"          # Folder containing Azure Functions
skip_api_build: false        # Let Azure build the Functions
```

Or deploy API separately and don't include in Static Web App.

## Workflow Configuration Explained

### Current Setup
```yaml
- name: Publish Blazor WebAssembly
  run: dotnet publish Client/Client.csproj --configuration Release --no-build --output ./dist

- name: Build And Deploy
  uses: Azure/static-web-apps-deploy@v1
  with:
    app_location: "dist/wwwroot"
    api_location: ""
    output_location: ""
    skip_app_build: true
```

**What this does**:
1. `dotnet publish` creates `./dist/wwwroot/` with all static files
2. Azure deploy action copies everything from `dist/wwwroot/` to hosting
3. `skip_app_build: true` means no build happens on Azure side
4. Empty `api_location` and `output_location` means no API and no additional build output

### Alternative: Let Azure Build

```yaml
# Don't publish manually, let Azure do it
- name: Build And Deploy
  uses: Azure/static-web-apps-deploy@v1
  with:
    app_location: "Client"           # Source folder
    api_location: "Api"              # API source folder
    output_location: "wwwroot"       # Where Blazor outputs
    skip_app_build: false            # Let Azure build
```

**Pros**: Simpler workflow
**Cons**: Slower builds, less control

## Verification Checklist

After deploying, verify:

- [ ] GitHub Actions workflow completes successfully
- [ ] "List published files" step shows `index.html` exists
- [ ] Azure Portal shows deployment succeeded
- [ ] Root URL loads Blazor loading screen
- [ ] Blazor app fully loads (check browser console)
- [ ] Direct navigation to `/logo` works
- [ ] Hard refresh clears any cached 404s
- [ ] Incognito mode loads correctly

## Still Getting 404?

### Check These Settings in Azure Portal

1. **Configuration** ? **General settings**:
   - Platform: `.NET`
   - Version: Should match your project (8.0)

2. **Configuration** ? **Application settings**:
   - No special settings needed for static sites

3. **Networking**:
   - Ensure no IP restrictions blocking access

### Enable Detailed Logging

Add this to your workflow temporarily:
```yaml
- name: Debug deployment
  run: |
    echo "=== Checking dist structure ==="
    find ./dist -type f -name "index.html"
    find ./dist -type f -name "staticwebapp.config.json"
    
    echo "=== Checking file sizes ==="
    du -h ./dist/wwwroot/index.html
    du -h ./dist/wwwroot/_framework/*.wasm
    
    echo "=== Checking permissions ==="
    ls -la ./dist/wwwroot/index.html
```

### Contact Support

If still stuck after trying all fixes:
1. Share GitHub Actions logs (with debug output)
2. Share Azure Portal deployment logs
3. Share exact URL and error message
4. Include output of local `dotnet publish` structure

## Quick Reference

### Local Test
```bash
dotnet publish Client/Client.csproj -c Release -o ./test
cd test/wwwroot
python -m http.server 8000
# Open http://localhost:8000
```

### Force Redeploy
```bash
# Trigger workflow manually
gh workflow run "Azure Static Web Apps CI/CD"

# Or push empty commit
git commit --allow-empty -m "Trigger redeploy"
git push
```

### Check Deployment Status
```bash
# Using Azure CLI
az staticwebapp list
az staticwebapp deployment list --name your-app-name

# Or check portal at:
https://portal.azure.com/#blade/HubsExtension/BrowseResource/resourceType/Microsoft.Web%2FStaticSites
```
