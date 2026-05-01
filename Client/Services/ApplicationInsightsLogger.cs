using Microsoft.JSInterop;

namespace Client.Services;

/// <summary>
/// Client-side Application Insights logger that sends telemetry via JavaScript interop
/// </summary>
public class ApplicationInsightsLogger
{
    private readonly IJSRuntime _jsRuntime;
    private bool? _isReady;

    // Common properties cached after first collection
    private string? _sessionId;
    private string? _os;
    private string? _userEmail;
    private string? _url;
    private string? _environment;

    /// <summary>Set the authenticated user's email for inclusion in all subsequent events.</summary>
    public void SetUserEmail(string email) => _userEmail = email.ToLowerInvariant();

    /// <summary>True once the origin has been resolved and it is a local dev URL.</summary>
    public bool IsDevEnvironment => _environment == "dev";

    private async Task<Dictionary<string, string>> GetCommonPropertiesAsync()
    {
        // Lazy-initialize session ID (one per page-load lifetime)
        _sessionId ??= Guid.NewGuid().ToString("N")[..12];

        if (_os == null)
        {
            try
            {
                var ua = await _jsRuntime.InvokeAsync<string>("eval", "navigator.userAgent");
                _os = DetectOs(ua);
            }
            catch
            {
                _os = "unknown";
            }
        }

        if (_url == null)
        {
            try
            {
                _url = await _jsRuntime.InvokeAsync<string>("eval", "window.location.origin");
                _environment = DetectEnvironment(_url);
            }
            catch
            {
                _url = "unknown";
                _environment = "unknown";
            }
        }

        // Capture the current full URL on every call (page may have navigated)
        string currentUrl;
        try { currentUrl = await _jsRuntime.InvokeAsync<string>("eval", "window.location.href"); }
        catch  { currentUrl = _url; }

        return new Dictionary<string, string>
        {
            ["sessionId"]   = _sessionId,
            ["os"]          = _os,
            ["userEmail"]   = _userEmail ?? "anonymous",
            ["url"]         = currentUrl,
            ["environment"] = _environment ?? "unknown",
        };
    }

    private static string DetectOs(string ua)
    {
        if (ua.Contains("iPhone") || ua.Contains("iPad"))  return "iOS";
        if (ua.Contains("Android"))                         return "Android";
        if (ua.Contains("Windows"))                         return "Windows";
        if (ua.Contains("Macintosh") || ua.Contains("Mac OS X")) return "macOS";
        if (ua.Contains("Linux"))                           return "Linux";
        return "Other";
    }

    private static string DetectEnvironment(string origin)
    {
        if (origin.Contains("localhost") || origin.Contains("127.0.0.1")) return "dev";
        if (origin.Contains(".azurestaticapps.net"))  return "staging";
        // Add your production hostname(s) here
        if (origin.Contains("calvinhsia.com"))         return "prod";
        return "prod"; // Default — assume production for any other deployed origin
    }

    /// <summary>
    /// Fired once at app startup from Program.cs after the host is built.
    /// Captures build metadata and browser capabilities alongside common properties.
    /// </summary>
    public async Task TrackSiteLoadedAsync(string buildTime, string gitBranch, string browser, string isMobile)
    {
        var props = await GetCommonPropertiesAsync();
        props["buildTime"]  = buildTime;
        props["gitBranch"]  = gitBranch;
        props["browser"]    = browser;
        props["isMobile"]   = isMobile;
        await TrackEvent("Site:Loaded", props);
    }

    /// <summary>Track that a page was activated (first render).</summary>
    public async Task TrackPageActivationAsync(string pageName, Dictionary<string, string>? extra = null)
    {
        var props = await GetCommonPropertiesAsync();
        props["page"] = pageName;
        if (extra != null)
            foreach (var kv in extra) props[kv.Key] = kv.Value;

        await TrackPageView(pageName, props);
        await TrackEvent($"PageActivation:{pageName}", props);
    }

    /// <summary>
    /// Track a login event.
    /// <paramref name="outcome"/>: "started" | "success" | "failure"
    /// <paramref name="reason"/>:  human-readable detail, e.g. exception message or "token_ok"
    /// </summary>
    public async Task TrackLoginAsync(string outcome, string? reason = null, string? userId = null)
    {
        var props = await GetCommonPropertiesAsync();
        props["outcome"] = outcome;
        props["reason"]  = reason ?? string.Empty;
        // Allow the caller to supply the just-resolved userId before SetUserId() is called
        if (!string.IsNullOrEmpty(userId))
        {
            props["userEmail"] = userId.ToLowerInvariant();
            SetUserEmail(userId); // persist for all subsequent events in this session
        }
        await TrackEvent("Auth:Login", props);
    }

    /// <summary>
    /// Track a logout event.
    /// <paramref name="outcome"/>: "started" | "success" | "failure"
    /// <paramref name="reason"/>:  human-readable detail or exception message
    /// </summary>
    public async Task TrackLogoutAsync(string outcome, string? reason = null)
    {
        var props = await GetCommonPropertiesAsync();
        props["outcome"] = outcome;
        props["reason"]  = reason ?? string.Empty;
        await TrackEvent("Auth:Logout", props);
    }

    /// <summary>Track a PictureQuery filter execution.</summary>
    public async Task TrackPictureQueryFilterAsync(string filter, string mediaType, int resultCount)
    {
        var props = await GetCommonPropertiesAsync();
        props["filter"]      = filter.ToLower();
        props["mediaType"]   = string.IsNullOrEmpty(mediaType) ? "all" : mediaType;
        props["resultCount"] = resultCount.ToString();

        await TrackEvent("PictureQuery:Filter", props);
    }

    /// <summary>Track an application error with context.</summary>
    public async Task TrackErrorAsync(string source, Exception ex, Dictionary<string, string>? extra = null)
    {
        var props = await GetCommonPropertiesAsync();
        props["source"]         = source;
        props["errorType"]      = ex.GetType().Name;
        props["errorMessage"]   = ex.Message;
        if (ex.StackTrace is { } st)
            props["stackTrace"] = st.Length > 512 ? st[..512] : st;
        if (extra != null)
            foreach (var kv in extra) props[kv.Key] = kv.Value;

        await TrackException(ex, props);
        await TrackEvent("AppError", props);
    }

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
        if (IsDevEnvironment)
        {
            Console.WriteLine($"[Telemetry suppressed-dev] {eventName}");
            return;
        }
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
        if (IsDevEnvironment)
        {
            Console.WriteLine($"[Telemetry suppressed-dev] Trace:{message}");
            return;
        }
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
        if (IsDevEnvironment)
        {
            Console.WriteLine($"[Telemetry suppressed-dev] Exception:{exception.GetType().Name}: {exception.Message}");
            return;
        }
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
        if (IsDevEnvironment)
        {
            Console.WriteLine($"[Telemetry suppressed-dev] PageView:{pageName}");
            return;
        }
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
