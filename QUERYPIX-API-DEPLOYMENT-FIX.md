# QueryPix API - Azure Static Web Apps Deployment Fix

## Problem
- ? API works locally (F5)
- ? Deployed site returns 500 for `/api/QueryPix`

## Root Causes
1. **API not being deployed** - workflow had `api_location: ""`
2. **Missing database** - API needs `MyPixNoThumbs.db` file
3. **Missing connection string** - API needs Azure Storage connection for database

## Solution

### 1. Workflow Changes (Already Applied)

The workflow now:
- ? Publishes API to `./dist/api`
- ? Sets `api_location: "dist/api"` for deployment
- ? Adds debug logging to verify files

### 2. Configure Azure Storage Connection String

Your API's `Program.cs` expects this environment variable:
```csharp
var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
```

#### Add to Azure Static Web App

1. **Via Azure Portal**:
   - Go to [Azure Portal](https://portal.azure.com)
   - Find your Static Web App: `nice-coast-0273ff81e`
   - Go to **Configuration** ? **Application settings**
   - Click **+ Add**
   - Name: `AZURE_STORAGE_CONNECTION_STRING`
   - Value: `DefaultEndpointsProtocol=https;AccountName=calvinhwebsitestorage;AccountKey=nhSqldZdEHGD76XtPh4YiV9QvDmprD4UXhaxGkx1pHXLDA4leiufvnDIs89lAQ+DKa6czrX4YqSK+AStoJGQCg==;EndpointSuffix=core.windows.net`
   - Click **OK** ? **Save**

2. **Via Azure CLI**:
   ```bash
   az staticwebapp appsettings set \
     --name nice-coast-0273ff81e \
     --setting-names AZURE_STORAGE_CONNECTION_STRING="DefaultEndpointsProtocol=https;AccountName=calvinhwebsitestorage;AccountKey=nhSqldZdEHGD76XtPh4YiV9QvDmprD4UXhaxGkx1pHXLDA4leiufvnDIs89lAQ+DKa6czrX4YqSK+AStoJGQCg==;EndpointSuffix=core.windows.net"
   ```

### 3. Ensure Database is in Blob Storage

Your API downloads the database from Azure Storage:
```csharp
var blobClient = new BlobServiceClient(connectionString)
    .GetBlobContainerClient("mypixnothumbs")
    .GetBlobClient("MyPixNoThumbs.db");
```

Verify the database exists:

1. **Via Azure Portal**:
   - Go to Storage Account: `calvinhwebsitestorage`
   - Click **Containers**
   - Look for container: `mypixnothumbs`
   - Verify file exists: `MyPixNoThumbs.db`

2. **Via Azure Storage Explorer**:
   - Open Azure Storage Explorer
   - Connect to your storage account
   - Navigate to `calvinhwebsitestorage` ? `mypixnothumbs`
   - Check if `MyPixNoThumbs.db` is present

### 4. Deploy and Test

```bash
# Commit and push the workflow changes
git add .github/workflows/azure-static-web-apps-nice-coast-0273ff81e.yml
git commit -m "Fix: Add API deployment for QueryPix endpoint"
git push
```

Wait 5-7 minutes for deployment, then test:
```bash
curl https://calvinhsia.com/api/QueryPix?Date1=1/1/1950&Date2=1/1/2030&MaxPix=10&NotesFilter=test
```

## Verification Checklist

After pushing changes:

- [ ] GitHub Actions **"List published files"** shows API files:
  ```
  ? host.json found in ./dist/api/
  ? QueryPix.dll found in ./dist/api/
  ```
- [ ] Azure Portal shows successful deployment
- [ ] Application Setting `AZURE_STORAGE_CONNECTION_STRING` is configured
- [ ] Container `mypixnothumbs` has `MyPixNoThumbs.db` file
- [ ] API endpoint responds: `https://calvinhsia.com/api/QueryPix?...`
- [ ] No 500 errors in browser console

## Troubleshooting

### Still Getting 500 After Deployment?

1. **Check API logs** (Azure Portal):
   - Go to Static Web App ? **Monitoring** ? **Application Insights**
   - Look for exceptions in the last hour
   - Check for database connection errors

2. **Verify API files deployed**:
   ```bash
   # Check if API is accessible
   curl https://calvinhsia.com/api/host.json
   # Should return the host.json content, not 404
   ```

3. **Check environment variables** in Azure Portal:
   - **Configuration** ? **Application settings**
   - Verify `AZURE_STORAGE_CONNECTION_STRING` is present
   - No typos in the setting name

4. **Database connection issues**:
   - API will fall back to local copy if blob storage fails
   - Check if `data/MyPixNoThumbs.db` is in your project
   - Ensure it's included in publish output

### Common Errors

#### Error: "Database is locked"
- SQLite database is read-only in Azure Functions
- Use connection pooling (already configured in your code)

#### Error: "Blob not found"
- Check container name is `mypixnothumbs` (lowercase)
- Check blob name is exact: `MyPixNoThumbs.db` (case-sensitive)
- Verify storage account connection string is correct

#### Error: "Access denied"
- Storage account key might be wrong
- Regenerate access key in Azure Portal if needed
- Update connection string in app settings

## How It Works

### Deployment Flow

```
???????????????????????????????????
?  GitHub Actions Workflow         ?
?                                  ?
?  1. Build solution               ?
?  2. Publish Client ? dist/wwwroot?
?  3. Publish Api ? dist/api       ? ? NEW
?  4. Deploy to Azure              ?
???????????????????????????????????
                 ?
???????????????????????????????????
?  Azure Static Web Apps           ?
?                                  ?
?  /            ? Blazor Client    ?
?  /api/QueryPix ? Azure Functions ? ? NOW AVAILABLE
???????????????????????????????????
```

### Runtime Flow

```
User requests: /api/QueryPix?Date1=...
        ?
Azure Static Web Apps routes to API
        ?
Azure Functions starts (if cold)
        ?
Program.cs checks for AZURE_STORAGE_CONNECTION_STRING
        ?
Downloads MyPixNoThumbs.db from blob storage
        ?
QueryPixClass.QueryPix executes
        ?
Returns JSON results
```

## Testing Locally

Your local setup (F5) works because:
- `local.settings.json` has the connection string
- Database can be accessed locally

To match production:
```bash
# Test API locally
cd Api
func start

# Test endpoint
curl http://localhost:7071/api/QueryPix?Date1=1/1/1950&Date2=1/1/2030&MaxPix=10&NotesFilter=test
```

## Security Notes

?? **Your storage account key is exposed in:**
- `Api/local.settings.json` (in repository)
- This README (for documentation)

**Recommended Actions:**
1. **Regenerate the storage account key** in Azure Portal
2. **Add `local.settings.json` to `.gitignore`** (if not already)
3. **Use Azure Key Vault** for secrets in production
4. **Consider using Managed Identity** instead of connection strings

```bash
# Check if local.settings.json is gitignored
git check-ignore Api/local.settings.json

# If not, add it:
echo "Api/local.settings.json" >> .gitignore
git rm --cached Api/local.settings.json
git commit -m "Security: Remove local.settings.json from repo"
```

## Next Steps

Once API is working:

1. **Monitor API usage** via Application Insights
2. **Add caching** for frequently queried data
3. **Consider CDN** for image thumbnails
4. **Implement rate limiting** to prevent abuse
5. **Add authentication** if needed for sensitive queries

## Related Files

- `Api/QueryPix.cs` - API endpoint implementation
- `Api/Program.cs` - Database initialization and DI setup
- `Api/host.json` - Azure Functions configuration
- `Api/local.settings.json` - Local development settings (should be gitignored)
- `.github/workflows/azure-static-web-apps-nice-coast-0273ff81e.yml` - Deployment workflow

## References

- [Azure Static Web Apps - API Configuration](https://learn.microsoft.com/en-us/azure/static-web-apps/apis-functions)
- [Azure Functions - Isolated Worker Process](https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide)
- [Azure Blob Storage - .NET SDK](https://learn.microsoft.com/en-us/azure/storage/blobs/storage-quickstart-blobs-dotnet)
