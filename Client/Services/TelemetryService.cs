using Microsoft.JSInterop;

namespace BlazorWasm.Services
{
    /// <summary>
    /// Client-side telemetry service for tracking errors and events
    /// Sends data to Application Insights via JavaScript interop
    /// </summary>
    public class TelemetryService
    {
        private readonly IJSRuntime _jsRuntime;
        private bool _isInitialized = false;
        
        public TelemetryService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public async Task InitializeAsync(string? instrumentationKey = null)
        {
            if (_isInitialized) return;

            try
            {
                // Initialize Application Insights if key is provided
                if (!string.IsNullOrEmpty(instrumentationKey))
                {
                    await _jsRuntime.InvokeVoidAsync("initializeAppInsights", instrumentationKey);
                }
                
                // Set up global error handlers
                await _jsRuntime.InvokeVoidAsync("setupGlobalErrorHandlers");
                
                _isInitialized = true;
                DebugHelper.Log("Telemetry service initialized", true);
            }
            catch (Exception ex)
            {
                DebugHelper.LogError($"Failed to initialize telemetry: {ex.Message}");
            }
        }

        /// <summary>
        /// Track an exception with context
        /// </summary>
        public async Task TrackExceptionAsync(Exception exception, Dictionary<string, string>? properties = null)
        {
            try
            {
                var exceptionData = new
                {
                    message = exception.Message,
                    stackTrace = exception.StackTrace ?? "",
                    type = exception.GetType().Name,
                    innerException = exception.InnerException?.Message,
                    timestamp = DateTime.UtcNow.ToString("o"),
                    url = await GetCurrentUrlAsync(),
                    userAgent = await GetUserAgentAsync(),
                    properties = properties ?? new Dictionary<string, string>()
                };

                await _jsRuntime.InvokeVoidAsync("trackException", exceptionData);
                DebugHelper.LogError($"[Telemetry] Tracked exception: {exception.Message}");
            }
            catch (Exception ex)
            {
                DebugHelper.LogError($"Failed to track exception: {ex.Message}");
                // Fallback: at least log to console
                Console.Error.WriteLine($"[Telemetry Error] {exception.GetType().Name}: {exception.Message}");
            }
        }

        /// <summary>
        /// Track a custom event with properties
        /// </summary>
        public async Task TrackEventAsync(string eventName, Dictionary<string, string>? properties = null)
        {
            try
            {
                var eventData = new
                {
                    name = eventName,
                    timestamp = DateTime.UtcNow.ToString("o"),
                    url = await GetCurrentUrlAsync(),
                    properties = properties ?? new Dictionary<string, string>()
                };

                await _jsRuntime.InvokeVoidAsync("trackEvent", eventData);
                DebugHelper.Log($"[Telemetry] Tracked event: {eventName}", forceOutput: false);
            }
            catch (Exception ex)
            {
                DebugHelper.LogError($"Failed to track event: {ex.Message}");
            }
        }

        /// <summary>
        /// Track JavaScript errors from the browser
        /// </summary>
        public async Task TrackJavaScriptErrorAsync(string message, string? source = null, int? lineNumber = null, int? columnNumber = null)
        {
            try
            {
                var errorData = new
                {
                    message,
                    source,
                    lineNumber,
                    columnNumber,
                    timestamp = DateTime.UtcNow.ToString("o"),
                    url = await GetCurrentUrlAsync(),
                    userAgent = await GetUserAgentAsync()
                };

                await _jsRuntime.InvokeVoidAsync("trackJavaScriptError", errorData);
                DebugHelper.LogError($"[Telemetry] JS Error: {message} at {source}:{lineNumber}");
            }
            catch (Exception ex)
            {
                DebugHelper.LogError($"Failed to track JS error: {ex.Message}");
            }
        }

        /// <summary>
        /// Track performance metrics
        /// </summary>
        public async Task TrackPerformanceAsync(string metricName, double value, Dictionary<string, string>? properties = null)
        {
            try
            {
                var metricData = new
                {
                    name = metricName,
                    value,
                    timestamp = DateTime.UtcNow.ToString("o"),
                    properties = properties ?? new Dictionary<string, string>()
                };

                await _jsRuntime.InvokeVoidAsync("trackMetric", metricData);
                DebugHelper.Log($"[Telemetry] Metric: {metricName} = {value}", forceOutput: false);
            }
            catch (Exception ex)
            {
                DebugHelper.LogError($"Failed to track metric: {ex.Message}");
            }
        }

        /// <summary>
        /// [Telemetry v1] Flush all pending telemetry events to Application Insights server
        /// Critical for mobile networks where events may be buffered
        /// </summary>
        public async Task FlushAsync()
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("flushAppInsights");
                DebugHelper.Log("[Telemetry v1] Flushed pending events to server", forceOutput: true);
            }
            catch (Exception ex)
            {
                DebugHelper.LogError($"[Telemetry v1] Failed to flush events: {ex.Message}");
            }
        }

        private async Task<string> GetCurrentUrlAsync()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<string>("eval", "window.location.href");
            }
            catch
            {
                return "unknown";
            }
        }

        private async Task<string> GetUserAgentAsync()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<string>("eval", "navigator.userAgent");
            }
            catch
            {
                return "unknown";
            }
        }
    }
}
