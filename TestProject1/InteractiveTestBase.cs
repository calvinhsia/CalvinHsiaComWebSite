using Microsoft.Playwright;
using System.Diagnostics;

namespace TestProject1
{
    /// <summary>
    /// Base class for interactive Blazor WebAssembly tests using Playwright
    /// Handles server startup, port cleanup, and Playwright initialization
    /// </summary>
    [TestClass]
    public abstract class InteractiveTestBase
    {
        protected static IPlaywright? _playwright;
        protected static IBrowser? _browser;
        
        // Allow BASE_URL to be configured via environment variable for CI
        protected static string BASE_URL => Environment.GetEnvironmentVariable("PLAYWRIGHT_BASE_URL") ?? "https://localhost:7193";
        protected const int SERVER_PORT = 7193;

        // Set this to true if you want the test to auto-start the server
        // In CI, we skip auto-start if server is already running or if CI env var is set
        protected static bool AUTO_START_SERVER => !IsCI();
        protected static Process? _dotnetProcess;

        // Track if server was started by this test class
        protected static bool _serverStartedByUs = false;

        // TestContext for accessing test information
        public TestContext? TestContext { get; set; }

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

            Console.WriteLine($"Browser configuration: Headless={options.Headless}, SlowMo={options.SlowMo}ms, Environment={(isCI ? "CI/CD" : "Local")}");

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

                Console.WriteLine($"?? Video recording enabled: {videoDir}");
            }

            return options;
        }

        [ClassInitialize]
        public static async Task BaseClassInitialize(TestContext context)
        {
            // Always check if server is already running first
            if (await IsServerRunning(BASE_URL))
            {
                Console.WriteLine("? Server is already running at " + BASE_URL);
                Console.WriteLine("Reusing existing server instance.");
                _serverStartedByUs = false; // We didn't start it
            }
            else if (AUTO_START_SERVER)
            {
                // Start the Blazor WASM development server (only in local dev, not CI)
                Console.WriteLine("Starting Blazor WASM development server...");
                _dotnetProcess = StartBlazorServer();
                _serverStartedByUs = true;

                // Wait for server to be ready
                await WaitForServer(BASE_URL);
            }
            else
            {
                // In CI, server should be started by the pipeline
                Console.WriteLine("??  AUTO_START_SERVER is disabled (CI mode).");
                Console.WriteLine($"Expected server at: {BASE_URL}");
                Console.WriteLine("The CI pipeline should start the server before running tests.");
                throw new InvalidOperationException($"Server is not running at {BASE_URL}. In CI, ensure the pipeline starts the server first.");
            }

            // Initialize Playwright
            Console.WriteLine("Initializing Playwright...");
            _playwright = await Playwright.CreateAsync();
        }

        [ClassCleanup]
        public static async Task BaseClassCleanup()
        {
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

            // Only kill the server if we started it AND it's still running
            if (_dotnetProcess != null && !_dotnetProcess.HasExited && _serverStartedByUs)
            {
                Console.WriteLine("Stopping Blazor server that we started...");
                try
                {
                    _dotnetProcess.Kill(entireProcessTree: true); // Kill entire process tree
                    _dotnetProcess.WaitForExit(5000); // Wait up to 5 seconds for graceful exit

                    // Give the OS time to release the port
                    await Task.Delay(500);

                    // Verify port is released
                    if (IsPortInUse(SERVER_PORT))
                    {
                        Console.WriteLine($"Warning: Port {SERVER_PORT} is still in use after stopping server");
                        Console.WriteLine("Attempting to kill process using the port...");
                        KillProcessUsingPort(SERVER_PORT);
                        await Task.Delay(1000); // Wait for port to be fully released
                    }
                    else
                    {
                        Console.WriteLine($"Port {SERVER_PORT} successfully released");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Error stopping server process: {ex.Message}");
                }
                finally
                {
                    _dotnetProcess.Dispose();
                    _dotnetProcess = null;
                }
            }
        }

        [TestInitialize]
        public void BaseTestInitialize()
        {
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
                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                };
                using var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
                var response = await httpClient.GetAsync(url);
                return response.IsSuccessStatusCode;
            }
            catch
            {
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

            Console.WriteLine($"Navigating to {fullUrl}...");

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
                Console.WriteLine($"? Element '{waitForSelector}' is visible, page is ready");
            }
            else
            {
                Console.WriteLine("? Page navigation complete");
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
                                Console.WriteLine($"Found process {pid} using port {port}, terminating...");
                                try
                                {
                                    var process = Process.GetProcessById(pid);
                                    process.Kill(entireProcessTree: true);
                                    process.WaitForExit(3000);
                                    Console.WriteLine($"Process {pid} terminated successfully");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Failed to kill process {pid}: {ex.Message}");
                                }
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error killing process using port {port}: {ex.Message}");
            }
        }

        protected static Process StartBlazorServer()
        {
            // Get the solution directory (parent of TestProject1)
            var testProjectDir = Path.GetDirectoryName(typeof(InteractiveTestBase).Assembly.Location)!;
            var solutionDir = Path.GetFullPath(Path.Combine(testProjectDir, "..", "..", "..", ".."));
            var clientProjectPath = Path.Combine(solutionDir, "Client", "Client.csproj");

            Console.WriteLine($"Test project directory: {testProjectDir}");
            Console.WriteLine($"Solution directory: {solutionDir}");
            Console.WriteLine($"Client project path: {clientProjectPath}");

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
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Console.WriteLine($"[Blazor Server] {e.Data}");
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Console.WriteLine($"[Blazor Server Error] {e.Data}");
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

            Console.WriteLine($"Waiting for server at {url}...");

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
                        Console.WriteLine("Server is ready!");
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
