using Microsoft.Playwright;
using System.Diagnostics;
using System.Reflection;

namespace TestProject1
{
    [AttributeUsage(AttributeTargets.Method)]
    public class DisableIInterActiveAttribute: Attribute
    {
        // This attribute can be used to mark tests that should not run in interactive mode
        // It doesn't have any logic by itself, but can be checked in test discovery or execution
    }
    /// <summary>
    /// Base class for interactive Blazor WebAssembly tests using Playwright
    /// Handles server startup, port cleanup, and Playwright initialization
    /// </summary>
    [TestClass]
    public abstract class InteractiveTestBase
    {
        protected static IPlaywright? _playwright;
        protected static IBrowser? _browser;
        protected static bool _IsDebugging => Debugger.IsAttached;

        // Allow BASE_URL to be configured via environment variable for CI
        protected static string BASE_URL => Environment.GetEnvironmentVariable("PLAYWRIGHT_BASE_URL") ?? "https://localhost:7193";
        protected const int SERVER_PORT = 7193;

        // Set this to true if you want the test to auto-start the server
        // In CI, we skip auto-start if server is already running or if CI env var is set
        protected static bool AUTO_START_SERVER => !IsCI();
        protected static Process? _dotnetProcess;

        // Track if server was started by this test class
        protected static bool _serverStartedByUs = false;

        // Track if cleanup handlers have been registered
        private static bool _cleanupHandlersRegistered = false;

        // TestContext for accessing test information - instance property set by MSTest
        public TestContext? TestContext { get; set; }

        // Static TestContext for use in static methods and helpers
        protected static TestContext? CurrentTestContext { get; private set; }

        /// <summary>
        /// Configurable log action - defaults to TestContext.WriteLine
        /// Can be set to Console.WriteLine or any other Action&lt;string&gt; for use outside tests
        /// </summary>
        public static Action<string> LogAction { get; set; } = (msg) =>
        {
            CurrentTestContext?.WriteLine(msg);
            if (Debugger.IsAttached)
            {
                Debug.WriteLine(msg);
            }
        };

        /// <summary>
        /// Log a message using the configured LogAction
        /// Use this instead of Console.WriteLine for consolidated output
        /// </summary>
        public static void Log(string message) => LogAction(message);

        /// <summary>
        /// Detects if running in CI/CD environment (GitHub Actions, Azure DevOps, etc.)
        /// </summary>
        protected static bool IsCI()
        {
            // GitHub Actions sets CI=true
            var ci = Environment.GetEnvironmentVariable("CI");
            if (!string.IsNullOrEmpty(ci) && ci.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Azure Pipelines sets TF_BUILD=True
            var tfBuild = Environment.GetEnvironmentVariable("TF_BUILD");
            if (!string.IsNullOrEmpty(tfBuild) && tfBuild.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Jenkins sets JENKINS_URL
            var jenkinsUrl = Environment.GetEnvironmentVariable("JENKINS_URL");
            if (!string.IsNullOrEmpty(jenkinsUrl))
            {
                return true;
            }

            // GitLab CI sets GITLAB_CI
            var gitlabCi = Environment.GetEnvironmentVariable("GITLAB_CI");
            if (!string.IsNullOrEmpty(gitlabCi))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets browser launch options appropriate for the environment (CI vs local)
        /// </summary>
        protected static BrowserTypeLaunchOptions GetBrowserLaunchOptions(bool? forceHeadless = null)
        {
            bool isCI = forceHeadless ?? IsCI();

            var options = new BrowserTypeLaunchOptions
            {
                Headless = isCI, // Headless in CI, headed locally
                SlowMo = isCI ? 0 : 100, // No slowmo in CI for speed
            };

            if (!isCI)
            {
                // Local development - open DevTools for debugging
                options.Devtools = false; // Can be changed to true for debugging
            }

            Log($"Browser configuration: Headless={options.Headless}, SlowMo={options.SlowMo}ms, Environment={(isCI ? "CI/CD" : "Local")}");

            return options;
        }

        /// <summary>
        /// Gets browser context options for automated tests with video recording in CI
        /// </summary>
        protected BrowserNewContextOptions GetBrowserContextOptions()
        {
            bool isCI = IsCI();

            var options = new BrowserNewContextOptions
            {
                IgnoreHTTPSErrors = true // Accept self-signed certs
            };

            // Enable video recording in CI environments
            if (isCI)
            {
                // Organize videos by test name in subdirectories
                // Use TestContext property from base class
                var testName = TestContext?.TestName ?? "UnknownTest";
                var videoDir = $"playwright-videos/{testName}/";

                options.RecordVideoDir = videoDir;
                options.RecordVideoSize = new RecordVideoSize { Width = 1280, Height = 720 };

                Log($"🎥 Video recording enabled: {videoDir}");
            }

            return options;
        }

        /// <summary>
        /// Common mobile device viewport definitions for testing
        /// </summary>
        protected static class MobileDevices
        {
            /// <summary>Galaxy S24+ - 6.7" display, 1080x2340 @ ~2.625 DPR = 411x891 CSS pixels</summary>
            public static ViewportSize GalaxyS24Plus => new() { Width = 411, Height = 891 };

            /// <summary>Galaxy S24+ landscape</summary>
            public static ViewportSize GalaxyS24PlusLandscape => new() { Width = 891, Height = 411 };

            /// <summary>iPhone 14 Pro Max - 6.7" display</summary>
            public static ViewportSize IPhone14ProMax => new() { Width = 430, Height = 932 };

            /// <summary>iPhone SE (older small phone)</summary>
            public static ViewportSize IPhoneSE => new() { Width = 375, Height = 667 };

            /// <summary>iPad Pro 11"</summary>
            public static ViewportSize IPadPro11 => new() { Width = 834, Height = 1194 };

            /// <summary>Generic tablet portrait</summary>
            public static ViewportSize TabletPortrait => new() { Width = 768, Height = 1024 };
        }

        /// <summary>
        /// Gets browser context options configured for a specific mobile device
        /// </summary>
        protected BrowserNewContextOptions GetMobileContextOptions(ViewportSize viewport, bool hasTouch = true)
        {
            var options = GetBrowserContextOptions();
            options.ViewportSize = viewport;
            options.HasTouch = hasTouch;
            options.IsMobile = true;
            return options;
        }
        private static bool _NeedToCleanup = true;
        [ClassInitialize]
        public static async Task BaseClassInitialize(TestContext context)
        {
            CurrentTestContext = context;
            Log($"[ClassInitialize] {DateTime.Now} Starting tests for {context.FullyQualifiedTestClassName}");
            var typeClass = Type.GetType(context.FullyQualifiedTestClassName);
            var method = typeClass?.GetMethod(context.TestName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (method!.GetCustomAttribute<DisableIInterActiveAttribute>() != null)
            {
                _NeedToCleanup = false;
                Log("⚠  This test is marked with [DisableIInterActive], skipping server startup and Playwright initialization.");
                return;
            }
            _NeedToCleanup = true;
            // Register cleanup handlers to ensure server is stopped even if test is interrupted
            RegisterCleanupHandlers();
            if (_IsDebugging)
            {
                Log($"[CI Detection] IsCI={IsCI()}, CI env var='{Environment.GetEnvironmentVariable("CI")}'");
                Log($"[Server] BASE_URL={BASE_URL}");
                Log($"[Server] AUTO_START_SERVER={AUTO_START_SERVER}");
            }
            // Always check if server is already running first
            var serverRunning = await IsServerRunning(BASE_URL);

            if (serverRunning)
            {
                if (_IsDebugging)
                {
                    Log("✓ Server is already running at " + BASE_URL);
                    Log("Reusing existing server instance.");
                }
                _serverStartedByUs = false; // We didn't start it
            }
            else if (AUTO_START_SERVER)
            {
                // Start the Blazor WASM development server (only in local dev, not CI)
                Log("Starting Blazor WASM development server...");
                _dotnetProcess = StartBlazorServer();
                _serverStartedByUs = true;

                // Wait for server to be ready
                await WaitForServer(BASE_URL);
            }
            else
            {
                // In CI, server should be started by the pipeline
                Log("⚠  AUTO_START_SERVER is disabled (CI mode).");
                Log($"Expected server at: {BASE_URL}");
                Log("The CI pipeline should start the server before running tests.");
                Log("PLAYWRIGHT_BASE_URL env var: " + (Environment.GetEnvironmentVariable("PLAYWRIGHT_BASE_URL") ?? "(not set)"));
                throw new InvalidOperationException($"Server is not running at {BASE_URL}. In CI, ensure the pipeline starts the server first.");
            }

            // Initialize Playwright
            if (_IsDebugging)
            {
                Log("Initializing Playwright...");
            }
            _playwright = await Playwright.CreateAsync();
        }

        /// <summary>
        /// Registers handlers for process exit, Ctrl+C, and app domain unload to ensure cleanup
        /// </summary>
        private static void RegisterCleanupHandlers()
        {
            if (_cleanupHandlersRegistered) return;
            _cleanupHandlersRegistered = true;

            // Handle Ctrl+C and Ctrl+Break
            Console.CancelKeyPress += (sender, e) =>
            {
                Log("[Cleanup] Ctrl+C detected, cleaning up...");
                CleanupServerSync();
                // Don't cancel - let the process exit naturally
            };

            // Handle process exit
            AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
            {
                Log("[Cleanup] Process exit detected, cleaning up...");
                CleanupServerSync();
            };
        }

        /// <summary>
        /// Synchronous cleanup for use in event handlers
        /// </summary>
        private static void CleanupServerSync()
        {
            try
            {
                // Kill the server process if we started it
                if (_dotnetProcess != null && !_dotnetProcess.HasExited)
                {
                    Log("[Cleanup] Killing server process...");
                    try
                    {
                        _dotnetProcess.Kill(entireProcessTree: true);
                        _dotnetProcess.WaitForExit(3000);
                    }
                    catch (Exception ex)
                    {
                        Log($"[Cleanup] Error killing server: {ex.Message}");
                    }
                }

                // Also kill any other process using the port (belt and suspenders)
                if (IsPortInUse(SERVER_PORT))
                {
                    Log($"[Cleanup] Port {SERVER_PORT} still in use, killing process...");
                    KillProcessUsingPort(SERVER_PORT);
                }
            }
            catch (Exception ex)
            {
                Log($"[Cleanup] Error in CleanupServerSync: {ex.Message}");
            }
        }

        [ClassCleanup]
        public static async Task BaseClassCleanup()
        {
            if (!_NeedToCleanup) return;
            if (_browser != null)
            {
                try
                {
                    await _browser.CloseAsync();
                    await _browser.DisposeAsync();
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }

            if (_playwright != null)
            {
                _playwright.Dispose();
            }

            // Always try to clean up the server process and port
            // This ensures cleanup happens even if _serverStartedByUs tracking got out of sync
            Log("[ClassCleanup] Starting server cleanup...");

            if (_dotnetProcess != null)
            {
                if (!_dotnetProcess.HasExited)
                {
                    Log("Stopping Blazor server process...");
                    try
                    {
                        _dotnetProcess.Kill(entireProcessTree: true);
                        _dotnetProcess.WaitForExit(5000);
                    }
                    catch (Exception ex)
                    {
                        Log($"Warning: Error stopping server process: {ex.Message}");
                    }
                }

                _dotnetProcess.Dispose();
                _dotnetProcess = null;
            }

            // Give the OS time to release the port
            await Task.Delay(500);

            // Always verify and clean up port, regardless of _serverStartedByUs flag
            // This handles cases where the flag got out of sync or a previous run left a zombie process
            if (IsPortInUse(SERVER_PORT))
            {
                Log($"Port {SERVER_PORT} is still in use after stopping server, cleaning up...");
                KillProcessUsingPort(SERVER_PORT);
                await Task.Delay(1000);

                if (IsPortInUse(SERVER_PORT))
                {
                    Log($"WARNING: Port {SERVER_PORT} is STILL in use! You may need to manually run kill-port-7193.ps1");
                }
                else
                {
                    Log($"Port {SERVER_PORT} successfully released");
                }
            }
            else
            {
                Log($"Port {SERVER_PORT} is free");
            }

            _serverStartedByUs = false;
        }

        [TestInitialize]
        public void BaseTestInitialize()
        {
            // Update the static TestContext so Log() works in this test
            CurrentTestContext = TestContext;


            // Reset browser for each test to ensure clean state
            _browser = null;
        }

        [TestCleanup]
        public async Task BaseTestCleanup()
        {
            // Close browser after each test
            if (_browser != null && _browser.IsConnected)
            {
                try
                {
                    await _browser.CloseAsync();
                    await _browser.DisposeAsync();
                }
                catch
                {
                    // Ignore cleanup errors
                }
                _browser = null;
            }
        }

        #region Helper Methods

        protected static async Task<bool> IsServerRunning(string url, int timeoutSeconds = 5)
        {
            try
            {
                if (_IsDebugging) Log($"[IsServerRunning] Checking {url} with {timeoutSeconds}s timeout...");
                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                };
                using var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
                var response = await httpClient.GetAsync(url);
                if (_IsDebugging) Log($"[IsServerRunning] Got response: {response.StatusCode}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Log($"[IsServerRunning] Exception: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Navigate to a Blazor WASM page with appropriate timeouts and wait conditions.
        /// Waits for DOMContentLoaded and then for a specified selector to be visible.
        /// </summary>
        /// <param name="page">Playwright page</param>
        /// <param name="relativeUrl">Relative URL (e.g., "/fish", "/wordscape")</param>
        /// <param name="waitForSelector">CSS selector to wait for (e.g., "canvas.fish-canvas")</param>
        /// <param name="navigationTimeout">Navigation timeout in milliseconds (default: 60000)</param>
        /// <param name="selectorTimeout">Selector visibility timeout in milliseconds (default: 30000)</param>
        protected static async Task NavigateToBlazorPageAsync(
            IPage page,
            string relativeUrl,
            string? waitForSelector = null,
            int navigationTimeout = 60000,
            int selectorTimeout = 30000)
        {
            var fullUrl = $"{BASE_URL}{relativeUrl}";

            Log($"Navigating to {fullUrl}...");

            // Navigate with lenient wait conditions for Blazor WASM
            await page.GotoAsync(fullUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded, // Less strict than NetworkIdle
                Timeout = navigationTimeout
            });

            // If a selector is provided, wait for it to be visible
            if (!string.IsNullOrEmpty(waitForSelector))
            {
                var element = page.Locator(waitForSelector);
                await element.WaitForAsync(new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = selectorTimeout
                });
                Log($"✓ Element '{waitForSelector}' is visible, page is ready");
            }
            else
            {
                Log("✓ Page navigation complete");
            }
        }

        protected static bool IsPortInUse(int port)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "netstat",
                    Arguments = "-ano",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process != null)
                {
                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    return output.Contains($":{port}");
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        protected static void KillProcessUsingPort(int port)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "netstat",
                    Arguments = "-ano",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var netstatProcess = Process.Start(processInfo);
                if (netstatProcess != null)
                {
                    var output = netstatProcess.StandardOutput.ReadToEnd();
                    netstatProcess.WaitForExit();

                    // Parse netstat output to find PID
                    var lines = output.Split('\n');
                    foreach (var line in lines)
                    {
                        if (line.Contains($":{port}") && line.Contains("LISTENING"))
                        {
                            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length > 0 && int.TryParse(parts[^1], out int pid))
                            {
                                Log($"Found process {pid} using port {port}, terminating...");
                                try
                                {
                                    var process = Process.GetProcessById(pid);
                                    process.Kill(entireProcessTree: true);
                                    process.WaitForExit(3000);
                                    Log($"Process {pid} terminated successfully");
                                }
                                catch (Exception ex)
                                {
                                    Log($"Failed to kill process {pid}: {ex.Message}");
                                }
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error killing process using port {port}: {ex.Message}");
            }
        }

        protected static Process StartBlazorServer()
        {
            // Get the solution directory (parent of TestProject1)
            var testProjectDir = Path.GetDirectoryName(typeof(InteractiveTestBase).Assembly.Location)!;
            var solutionDir = Path.GetFullPath(Path.Combine(testProjectDir, "..", "..", "..", ".."));
            var clientProjectPath = Path.Combine(solutionDir, "Client", "Client.csproj");

            if (_IsDebugging)
            {
                Log($"Test project directory: {testProjectDir}");
                Log($"Solution directory: {solutionDir}");
                Log($"Client project path: {clientProjectPath}");
            }
            if (!File.Exists(clientProjectPath))
            {
                throw new FileNotFoundException($"Client project not found at: {clientProjectPath}");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{clientProjectPath}\"",
                WorkingDirectory = solutionDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var process = new Process { StartInfo = startInfo };

            process.OutputDataReceived += (sender, e) =>
            {
                if (InteractiveTestBase._IsDebugging && !string.IsNullOrEmpty(e.Data))
                {
                    Debug.WriteLine($"[Blazor Server] {e.Data}");
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Log($"[Blazor Server Error] {e.Data}");
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return process;
        }

        protected static async Task WaitForServer(string url, int timeoutSeconds = 60)
        {
            var stopwatch = Stopwatch.StartNew();

            Log($"Waiting for server at {url}...");

            while (stopwatch.Elapsed.TotalSeconds < timeoutSeconds)
            {
                try
                {
                    // Skip SSL validation for local development
                    var handler = new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                    };
                    using var httpClient = new HttpClient(handler);
                    var response = await httpClient.GetAsync(url);

                    if (response.IsSuccessStatusCode)
                    {
                        Log("Server is ready!");
                        return;
                    }
                }
                catch
                {
                    // Server not ready yet
                }

                await Task.Delay(1000);
            }

            throw new TimeoutException($"Server at {url} did not become ready within {timeoutSeconds} seconds");
        }

        #endregion
    }
}
