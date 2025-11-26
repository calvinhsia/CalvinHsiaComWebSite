# Pipeline Fixes

## Issues Fixed

### 1. Playwright Test Failure: `JavaScriptFastMode_PositionCommands_ExecuteCorrectly`

**Problem**: Test was failing with "Run button not found" error.

**Root Cause**: 
- The Logo page has AUTO-START enabled by default, which automatically executes code on page load
- When code is running, the button class changes from `logo-run-button` to `logo-stop-button`
- Test was looking for `logo-run-button` but found `logo-stop-button` instead
- Test was also looking for a non-existent `button.logo-rendering-mode-button` - the actual UI uses radio buttons

**Fix**:
1. Added `?noautostart=true` query parameter to prevent auto-execution:
   ```csharp
   await NavigateToBlazorPageAsync(page, "/logo?noautostart=true", "canvas#logoCanvas");
   ```

2. Added explicit wait for Run button to ensure it's visible:
   ```csharp
   await page.WaitForSelectorAsync("button.logo-run-button", new PageWaitForSelectorOptions
   {
       State = WaitForSelectorState.Visible,
       Timeout = 10000
   });
   ```

3. Fixed rendering mode switching to use radio buttons:
   ```csharp
   var immediateRadio = await page.QuerySelectorAsync("input[type='radio'][value='Immediate']");
   if (immediateRadio != null && !await immediateRadio.IsCheckedAsync())
   {
       await immediateRadio.ClickAsync();
   }
   ```

**Files Modified**:
- `TestProject1/InteractiveLogoTest.cs`

---

### 2. Azure Static Web Apps Deployment Failure

**Problem**: Deployment failed with error:
```
Failed to find a default file in the app artifacts folder (Client). 
Valid default files: index.html,Index.html.
```

**Root Cause**:
- Blazor WASM publish output is in `Client/bin/Release/net8.0/publish/wwwroot/`
- Workflow was pointing to `Client/` directory directly
- Azure Static Web Apps expected to find `index.html` but couldn't locate it

**Fix**:
1. Added explicit publish step to create deployable output:
   ```yaml
   - name: Publish Blazor WebAssembly
     run: dotnet publish Client/Client.csproj --configuration Release --no-build --output ./publish
   ```

2. Updated deployment configuration to point to published output:
   ```yaml
   app_location: "publish/wwwroot"  # Changed from "Client"
   output_location: ""               # Changed from "wwwroot"
   skip_app_build: true             # Keep true since we're building ourselves
   ```

**Files Modified**:
- `.github/workflows/azure-static-web-apps-nice-coast-0273ff81e.yml`

---

## Testing

### Local Testing
```bash
# Build solution
dotnet build --configuration Release

# Publish Blazor app
dotnet publish Client/Client.csproj --configuration Release --output ./publish

# Verify output exists
ls ./publish/wwwroot/index.html

# Run Playwright tests
dotnet test TestProject1/TestProject1.csproj \
  --configuration Release \
  --filter "TestCategory=Automated"
```

### Pipeline Testing
Push changes to GitHub and monitor:
1. **JavaScript Unit Tests** - Should pass (already passing)
2. **Playwright Tests** - Should now pass without "Run button not found" error
3. **Azure Static Web Apps Deploy** - Should successfully find and deploy `index.html`

---

## Additional Notes

### ?noautostart Query Parameter
The Logo page now supports `?noautostart=true` query parameter to disable auto-execution:
- **Default behavior**: AUTO-START example loads and runs automatically
- **With ?noautostart=true**: Page loads without executing code
- **Use case**: Testing, debugging, or when you want manual control

### Rendering Mode UI
The Logo page uses radio buttons for rendering mode selection, not a toggle button:
```html
<input type="radio" name="renderingMode" value="Immediate" />
<input type="radio" name="renderingMode" value="Animated" />
```

Tests should query for radio inputs, not buttons.

---

## Version Markers (for cache detection)
When debugging, add version markers to verify code changes:

```javascript
console.log('[Logo-Fast v9] Initializing...'); // Increment version number
```

```csharp
DebugHelper.Log("[Logo v3.1] OnAfterRenderAsync", true); // Increment version
```

This helps confirm which version of code is actually running (vs cached).
