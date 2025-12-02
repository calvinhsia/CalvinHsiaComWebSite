# Application Insights Setup for Client

Application Insights has been added to the Blazor WASM client to enable client-side telemetry logging.

## Configuration

### 1. Get Your Connection String

1. Go to Azure Portal ? Your Application Insights resource
2. Navigate to **"Properties"** or **"Overview"**
3. Copy the **Connection String** (looks like: `InstrumentationKey=...;IngestionEndpoint=...`)

### 2. Update appsettings.json

Edit `Client/wwwroot/appsettings.json`:

```json
{
  "ApplicationInsights": {
    "ConnectionString": "YOUR-CONNECTION-STRING-HERE"
  }
}
```

**Note**: For production, you may want to use different connection strings for different environments.

### 3. For Production Deployments

Add your connection string to the Static Web App configuration:

```yaml
# .github/workflows/azure-static-web-apps-*.yml
env:
  APP_INSIGHTS_CONNECTION_STRING: ${{ secrets.APP_INSIGHTS_CONNECTION_STRING }}
```

Then update the build to inject it into `appsettings.json` during deployment.

## Usage

### Inject the Logger

```csharp
@inject ApplicationInsightsLogger AppInsights
```

### Log Events

```csharp
await AppInsights.TrackEvent("ButtonClicked", new Dictionary<string, string>
{
    { "buttonName", "StartGame" },
    { "userId", "user123" }
});
```

### Log Traces (Informational Messages)

```csharp
await AppInsights.TrackTrace("[Client v1] Game started", SeverityLevel.Information);
```

### Log Exceptions

```csharp
catch (Exception ex)
{
    await AppInsights.TrackException(ex, new Dictionary<string, string>
    {
        { "component", "GamePage" },
        { "operation", "LoadGame" }
    });
}
```

### Track Page Views

```csharp
protected override async Task OnInitializedAsync()
{
    await AppInsights.TrackPageView("GamePage");
}
```

### Track Metrics

```csharp
await AppInsights.TrackMetric("GameScore", 1500.0);
```

## Severity Levels

- `SeverityLevel.Verbose = 0` - Detailed trace information
- `SeverityLevel.Information = 1` - Informational messages
- `SeverityLevel.Warning = 2` - Warning messages
- `SeverityLevel.Error = 3` - Error messages
- `SeverityLevel.Critical = 4` - Critical failures

## Querying Client Logs

In Application Insights, client logs will appear alongside server logs:

```kql
// See all client traces
traces
| where timestamp > ago(1h)
| where cloud_RoleName contains "Client" or sdkVersion contains "javascript"
| project timestamp, message, severityLevel
| order by timestamp desc

// See both client and server logs together
union traces, requests, exceptions
| where timestamp > ago(1h)
| extend source = iff(sdkVersion contains "javascript", "Client", "Server")
| project timestamp, source, message, name, severityLevel
| order by timestamp desc
```

## Example: FetchData Page

See `Client/Pages/FetchData.razor` for a complete example showing:
- Page view tracking
- Trace logging with version markers
- Exception logging
- Metric tracking

## Cost Notes

- Client-side logging typically generates **2-10 KB per page view**
- Add version markers like `[Client v1]` to verify code changes are running
- For typical traffic, you'll stay within the **free 5 GB/month tier**

## Troubleshooting

### Logs Not Appearing

1. **Check connection string** is set in `appsettings.json`
2. **Check browser console** for Application Insights initialization messages
3. **Wait 1-2 minutes** - Application Insights can have a delay
4. **Verify in Azure Portal** ? Application Insights ? Logs (not Metrics)

### Connection String Not Loading

If you see `?? Application Insights connection string not found in config` in browser console:
1. Verify `appsettings.json` has the correct format
2. Clear browser cache and hard refresh (Ctrl+Shift+R)
3. Check browser Network tab to confirm `appsettings.json` is loading

## Security Note

The Application Insights connection string will be visible in client-side code. This is normal and expected for browser telemetry. The connection string only allows **sending** telemetry data, not reading it.
