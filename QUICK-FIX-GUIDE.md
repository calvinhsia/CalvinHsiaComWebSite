# Quick Start: Fixing Azure Static Web Apps 404 Errors

## The Problem in 30 Seconds
- Tests pass ?
- Deployment succeeds ?  
- Website shows 404 ?

**Cause**: `staticwebapp.config.json` not in the right place.

## The Fix in 3 Steps

### Step 1: Verify the Issue Locally
```bash
dotnet publish Client/Client.csproj -c Release -o ./test-dist

# Check if config is in wwwroot
ls ./test-dist/wwwroot/staticwebapp.config.json
# If "not found" ? This is your problem!
```

### Step 2: Apply the Fix
The workflow has been updated to automatically copy the config file:

```yaml
- name: Copy Static Web App Config to wwwroot
  run: |
    cp ./dist/staticwebapp.config.json ./dist/wwwroot/
```

**This is already done in your workflow file.**

### Step 3: Push and Deploy
```bash
git add .
git commit -m "Fix: Copy staticwebapp.config.json to wwwroot for Azure deployment"
git push
```

## Verify the Fix

### In GitHub Actions (2 minutes after push)
1. Go to **Actions** tab
2. Click latest workflow
3. Find **"List published files (debug)"** step
4. Look for:
   ```
   ? index.html found in ./dist/wwwroot/
   ? staticwebapp.config.json found in ./dist/wwwroot/
   ```

### On Your Live Site (5 minutes after push)
Visit these URLs and verify they work:

```
https://your-app.azurestaticapps.net/
https://your-app.azurestaticapps.net/logo
https://your-app.azurestaticapps.net/wordscape  
https://your-app.azurestaticapps.net/fish
```

**All should load without 404!**

## Still Getting 404?

### Quick Checks
1. **Hard refresh**: Ctrl+Shift+R (Windows) or Cmd+Shift+R (Mac)
2. **Try incognito**: Bypass browser cache completely
3. **Check logs**: GitHub Actions ? "List published files" ? See both ??
4. **Wait 5 mins**: Azure CDN can take time to update

### If Still Broken
See detailed troubleshooting in:
- `DEPLOYMENT-TROUBLESHOOTING.md`
- `DEPLOYMENT-FIX-SUMMARY.md`
- `DEPLOYMENT-VISUAL-GUIDE.md`

## What the Fix Does

**Before**:
```
dist/wwwroot/
??? index.html ?
??? [NO CONFIG] ?
```

**After**:
```
dist/wwwroot/
??? index.html ?
??? staticwebapp.config.json ?  ? ENABLES ROUTING!
```

## Why This Matters

The `staticwebapp.config.json` file tells Azure:
> "When someone visits `/logo`, serve `index.html` instead of returning 404"

Without it ? **404 errors**  
With it ? **Blazor routing works** ?

## Files Changed

- ? `.github/workflows/azure-static-web-apps-nice-coast-0273ff81e.yml`
  - Added config copy step
  - Added debug logging

## No Other Changes Needed

Everything else works:
- ? Tests pass
- ? Build succeeds  
- ? Playwright tests work
- ? JavaScript tests work

The **only** issue was the missing config file in deployment.

## Timeline

| Time | What Happens |
|------|--------------|
| T+0min | Push code to GitHub |
| T+2min | Tests complete |
| T+3min | Deployment starts |
| T+5min | Site updated on Azure |
| T+7min | CDN fully refreshed |

**Total: ~7 minutes from push to live**

## Success Indicators

You'll know it worked when:
- ? No more 404 errors on routes
- ? `/logo`, `/wordscape`, `/fish` all load
- ? Blazor app loads and routes work
- ? Browser console shows no errors

## Next Steps

After confirming the fix works:
1. Test all your routes
2. Clear the 404 cache (hard refresh)
3. Share the working site!
4. Consider adding more games ??

## Need Help?

Check these files for details:
- **Quick troubleshooting**: `DEPLOYMENT-TROUBLESHOOTING.md`
- **Technical details**: `DEPLOYMENT-FIX-SUMMARY.md`  
- **Visual explanation**: `DEPLOYMENT-VISUAL-GUIDE.md`

## Command Reference

```bash
# Test locally
dotnet publish Client/Client.csproj -c Release -o ./test-dist
python -m http.server 8000 -d ./test-dist/wwwroot

# Trigger deployment
git push

# Check deployment status (Azure CLI)
az staticwebapp list
az staticwebapp show --name your-app-name

# Force redeploy
git commit --allow-empty -m "Trigger redeploy"
git push
```

---

**That's it!** Push your changes and wait 5-7 minutes. Your site should work! ??
