using Microsoft.JSInterop;

namespace WordScapeBlazorWasm.Services
{
    public class DebugHelper
    {
        private readonly IJSRuntime _jsRuntime;
        private static bool _isDebugEnabled = false;

        public DebugHelper(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public static bool IsDebugEnabled => _isDebugEnabled;

        public static void SetDebugMode(bool enabled)
        {
            _isDebugEnabled = enabled;
        }

        public static void Log(string message, bool forceOutput = false)
        {
            if (_isDebugEnabled || forceOutput)
            {
                Console.WriteLine($"[WordScape Debug] {message}");
            }
        }

        public static void LogError(string message)
        {
            Console.WriteLine($"[WordScape ERROR] {message}");
        }

        public static void LogWarning(string message)
        {
            Console.WriteLine($"[WordScape WARNING] {message}");
        }

        public static void LogTouch(string message)
        {
            if (_isDebugEnabled)
            {
                Console.WriteLine($"[TouchDebug] {message}");
            }
        }

        public static void LogGrid(string message)
        {
            if (_isDebugEnabled)
            {
                Console.WriteLine($"[GridDebug] {message}");
            }
        }

        public async Task<bool> IsMobileDevice()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<bool>("blazorAuthHelper.isMobile");
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> GetUserAgent()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<string>("navigator.userAgent") ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        public async Task<object> GetWindowDimensions()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<object>("(() => ({ width: window.innerWidth, height: window.innerHeight, devicePixelRatio: window.devicePixelRatio }))()");
            }
            catch
            {
                return new { width = 0, height = 0, devicePixelRatio = 1 };
            }
        }
    }
}