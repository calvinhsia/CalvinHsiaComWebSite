# Azure Static Web Apps 404 Fix - Summary

## Problem
- ? GitHub Actions tests pass
- ? Deployment completes successfully  
- ? Website returns 404 errors

## Root Cause
The `staticwebapp.config.json` file was not in the correct location (`wwwroot/`) where Azure Static Web Apps expects it.

### Why This Matters
Azure Static Web Apps uses `staticwebapp.config.json` to configure:
- **Routing rules**: What URLs are allowed/protected
- **Navigation fallback**: Tells Azure to serve `index.html` for Blazor client-side routing
- **API endpoints**: How to route `/api/*` requests
- **Authentication**: Which routes require login

**Without this file in `wwwroot/`**, Azure doesn't know that `/logo`, `/wordscape`, etc. should all serve `index.html` for Blazor's client-side routing ? **Result: 404 errors**.

## The Fix

### 1. Workflow Changes

Updated `.github/workflows/azure-static-web-apps-nice-coast-0273ff81e.yml`:

```yaml
- name: Publish Blazor WebAssembly
  run: dotnet publish Client/Client.csproj --configuration Release --no-build --output ./dist

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
    echo "Checking for required files:"
    if [ -f "./dist/wwwroot/index.html" ]; then
      echo "? index.html found"
    fi
    if [ -f "./dist/wwwroot/staticwebapp.config.json" ]; then
      echo "? staticwebapp.config.json found"
    fi
```

### 2. Deployment Configuration

```yaml
- name: Build And Deploy
  uses: Azure/static-web-apps-deploy@v1
  with:
    app_location: "dist/wwwroot"    # Points to published static files
    api_location: ""                # No API in this deployment
    output_location: ""             # No additional build output
    skip_app_build: true            # We already built with dotnet publish
    skip_api_build: false           # Let Azure handle API if configured
```

## How to Verify the Fix

### Locally
```bash
# Clean and publish
dotnet clean
dotnet publish Client/Client.csproj -c Release -o ./test-dist

# Verify structure
ls ./test-dist/wwwroot/index.html                      # Should exist
ls ./test-dist/wwwroot/staticwebapp.config.json        # Should exist
ls ./test-dist/wwwroot/_framework                      # Should exist

# The config file enables Blazor routing!
cat ./test-dist/wwwroot/staticwebapp.config.json
```

### In GitHub Actions
1. Go to **Actions** tab
2. Click latest workflow run
3. Check **"List published files (debug)"** step
4. Should see both checkmarks:
   ```
   ? index.html found
   ? staticwebapp.config.json found
   ```

### After Deployment
1. Visit your site: `https://your-app.azurestaticapps.net/`
2. Test Blazor routes:
   - `/` - Should load home page
   - `/logo` - Should load Logo game
   - `/wordscape` - Should load WordScape game
   - `/fish` - Should load Fish simulation
3. **Hard refresh** (Ctrl+Shift+R) to clear cache
4. **Try incognito mode** to verify it's not a cache issue

## What Changed

| File | Change | Why |
|------|--------|-----|
| `.github/workflows/...yml` | Added copy step for config | Ensures `staticwebapp.config.json` is in `wwwroot/` |
| `.github/workflows/...yml` | Added debug logging | Helps verify files are in correct locations |
| `.github/workflows/...yml` | Changed output to `dist/` | Clearer naming than `publish/` |

## Key Concepts

### Blazor WASM Routing
Blazor WebAssembly uses **client-side routing**:
- Browser requests `/logo`
- Server must return `index.html` (not 404!)
- Blazor's JavaScript router loads the Logo component

### Azure Static Web Apps Config
The `navigationFallback` section tells Azure:
```json
{
  "navigationFallback": {
    "rewrite": "index.html",
    "exclude": [ "/images/*.{png,jpg,gif}", "/css/*" ]
  }
}
```

**Translation**: 
- For any URL not matching exclusions ? serve `index.html`
- Static files (CSS, images) ? serve as-is
- Blazor routes (/, /logo, /fish) ? all get `index.html`, then Blazor handles routing

## Troubleshooting Tips

### Still Getting 404?

1. **Check browser console** (F12):
   - Look for errors loading `_framework/*.wasm` files
   - Verify Blazor is starting up

2. **Check deployed files in Azure Portal**:
   - Go to Static Web App ? **Configuration**
   - Verify settings match workflow

3. **Check specific files**:
   ```
   https://your-app.azurestaticapps.net/staticwebapp.config.json
   ```
   Should return the config file (not 404)

4. **Clear ALL caches**:
   - Browser cache (Ctrl+Shift+R)
   - Service worker (DevTools ? Application ? Clear storage)
   - Try incognito mode

5. **Check GitHub Actions logs**:
   - Verify "? staticwebapp.config.json found" message
   - If missing, check copy step executed successfully

## Testing the Workflow

### Trigger a Test Deployment
```bash
# Option 1: Push empty commit
git commit --allow-empty -m "Test deployment"
git push

# Option 2: Manual workflow trigger (if enabled)
gh workflow run "Azure Static Web Apps CI/CD"
```

### Monitor Deployment
1. **GitHub**: Actions tab ? Watch workflow run
2. **Azure Portal**: Static Web App ? Deployments
3. **Browser**: Visit site after ~2 minutes

## Success Checklist

After pushing changes, verify:

- [ ] GitHub Actions workflow runs successfully
- [ ] "List published files" shows both checkmarks
- [ ] Azure Portal shows successful deployment
- [ ] Root URL (`/`) loads correctly
- [ ] Blazor routes work (`/logo`, `/wordscape`, `/fish`)
- [ ] No 404 errors in browser console
- [ ] Static files load (CSS, images, JS)
- [ ] Hard refresh still works
- [ ] Incognito mode works

## Next Steps

If all checks pass but you still see issues:

1. **API Routes**: If using Azure Functions API, verify `api_location` configuration
2. **Authentication**: Check auth routes in config match your setup
3. **Custom Domain**: If using custom domain, verify DNS and SSL settings
4. **Performance**: Consider enabling CDN caching for static assets

## Related Files

- `Client/staticwebapp.config.json` - Routing configuration
- `Client/wwwroot/index.html` - Blazor entry point
- `.github/workflows/azure-static-web-apps-nice-coast-0273ff81e.yml` - Deployment workflow

## References

- [Azure Static Web Apps Configuration](https://learn.microsoft.com/en-us/azure/static-web-apps/configuration)
- [Blazor Routing](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/routing)
- [GitHub Actions for Azure](https://github.com/Azure/static-web-apps-deploy)
