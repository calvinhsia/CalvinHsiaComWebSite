using Microsoft.JSInterop;

namespace Client.Services;

/// <summary>
/// Client-side Application Insights logger that sends telemetry via JavaScript interop
/// </summary>
public class ApplicationInsightsLogger
{
    private readonly IJSRuntime _jsRuntime;
    private bool? _isReady;

    public ApplicationInsightsLogger(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Check if Application Insights SDK is loaded and ready
    /// </summary>
    private async Task<bool> IsReadyAsync()
    {
        // If already confirmed ready, return cached result
        if (_isReady == true)
            return true;

        // Try up to 10 times with 300ms delays (3 seconds total) to wait for SDK to load from CDN
        for (int i = 0; i < 10; i++)
        {
            try
            {
                // Check both that appInsights exists AND that it has the trackEvent method (fully loaded)
                var ready = await _jsRuntime.InvokeAsync<bool>("eval", 
                    "typeof appInsights !== 'undefined' && appInsights !== null && typeof appInsights.trackEvent === 'function'");
                if (ready)
                {
                    _isReady = true;
                    Console.WriteLine($"[AppInsights v3] SDK fully ready after {i * 300}ms");
                    return true;
                }
                else
                {
                    // Log what we found for debugging
                    var appInsightsType = await _jsRuntime.InvokeAsync<string>("eval", "typeof appInsights");
                    var trackEventType = await _jsRuntime.InvokeAsync<string>("eval", "typeof (appInsights?.trackEvent)");
                    Console.WriteLine($"[AppInsights v3] Check {i + 1}/10: appInsights={appInsightsType}, trackEvent={trackEventType}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AppInsights v3] Check {i + 1}/10 failed: {ex.Message}");
            }

            // Wait before retry (except on last iteration)
            if (i < 9)
            {
                await Task.Delay(300);
            }
        }

        // Cache negative result only after all retries exhausted
        _isReady = false;
        Console.WriteLine($"[AppInsights v3] SDK not ready after 3000ms of retries - telemetry will be skipped");
        return false;
    }

    public async Task TrackEvent(string eventName, Dictionary<string, string>? properties = null)
    {
        if (!await IsReadyAsync())
        {
            Console.WriteLine($"?? Application Insights not ready - skipping event: {eventName}");
            return;
        }

        try
        {
            await _jsRuntime.InvokeVoidAsync("appInsights.trackEvent", 
                new { name = eventName, properties = properties ?? new Dictionary<string, string>() });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to track event: {ex.Message}");
        }
    }

    public async Task TrackTrace(string message, int severityLevel = 1, Dictionary<string, string>? properties = null)
    {
        if (!await IsReadyAsync())
        {
            Console.WriteLine($"?? Application Insights not ready - skipping trace: {message}");
            return;
        }

        try
        {
            await _jsRuntime.InvokeVoidAsync("appInsights.trackTrace",
                new { message, severityLevel, properties = properties ?? new Dictionary<string, string>() });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to track trace: {ex.Message}");
        }
    }

    public async Task TrackException(Exception exception, Dictionary<string, string>? properties = null)
    {
        if (!await IsReadyAsync())
        {
            Console.WriteLine($"?? Application Insights not ready - skipping exception: {exception.Message}");
            return;
        }

        try
        {
            await _jsRuntime.InvokeVoidAsync("appInsights.trackException",
                new 
                { 
                    exception = new 
                    { 
                        message = exception.Message,
                        typeName = exception.GetType().Name,
                        stack = exception.StackTrace
                    },
                    properties = properties ?? new Dictionary<string, string>()
                });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to track exception: {ex.Message}");
        }
    }

    public async Task TrackPageView(string pageName, Dictionary<string, string>? properties = null)
    {
        if (!await IsReadyAsync())
        {
            Console.WriteLine($"?? Application Insights not ready - skipping page view: {pageName}");
            return;
        }

        try
        {
            await _jsRuntime.InvokeVoidAsync("appInsights.trackPageView",
                new { name = pageName, properties = properties ?? new Dictionary<string, string>() });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to track page view: {ex.Message}");
        }
    }

    public async Task TrackMetric(string metricName, double value, Dictionary<string, string>? properties = null)
    {
        if (!await IsReadyAsync())
        {
            Console.WriteLine($"?? Application Insights not ready - skipping metric: {metricName}");
            return;
        }

        try
        {
            await _jsRuntime.InvokeVoidAsync("appInsights.trackMetric",
                new { name = metricName, average = value, properties = properties ?? new Dictionary<string, string>() });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to track metric: {ex.Message}");
        }
    }
}

/// <summary>
/// Severity levels matching Application Insights
/// </summary>
public static class SeverityLevel
{
    public const int Verbose = 0;
    public const int Information = 1;
    public const int Warning = 2;
    public const int Error = 3;
    public const int Critical = 4;
}
