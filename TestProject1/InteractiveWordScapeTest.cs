using Microsoft.Playwright;
using System.Diagnostics;

namespace TestProject1
{
    /// <summary>
    /// Interactive WordScape game test harness
    /// This test launches your Blazor app in a real browser where you can interact with it
    /// You can modify HTML/CSS/JS and see changes in real-time
    /// 
    /// IMPORTANT: Start your Blazor app manually before running these tests:
    /// cd Client
    /// dotnet run
    /// </summary>
    [TestClass]
    public class InteractiveWordScapeTest
    {
        private static IPlaywright? _playwright;
        private static IBrowser? _browser;
        private const string BASE_URL = "https://localhost:7193"; // Updated to match launchSettings.json
        
        // Set this to true if you want the test to auto-start the server
        // Set to false if you prefer to start the server manually (recommended)
        private const bool AUTO_START_SERVER = true;
        private static Process? _dotnetProcess;
        
        // Track if server was started by this test class
        private static bool _serverStartedByUs = false;

        [ClassInitialize]
        public static async Task ClassInitialize(TestContext context)
        {
            if (AUTO_START_SERVER)
            {
                // Check if server is already running first
                if (await IsServerRunning(BASE_URL))
                {
                    Console.WriteLine("? Server is already running at " + BASE_URL);
                    Console.WriteLine("Reusing existing server instance.");
                }
                else
                {
                    // Start the Blazor WASM development server
                    Console.WriteLine("Starting Blazor WASM development server...");
                    _dotnetProcess = StartBlazorServer();
                    _serverStartedByUs = true;
                    
                    // Wait for server to be ready
                    await WaitForServer(BASE_URL);
                }
            }
            else
            {
                Console.WriteLine("??  AUTO_START_SERVER is disabled.");
                Console.WriteLine("Please make sure your Blazor app is running:");
                Console.WriteLine("  cd Client");
                Console.WriteLine("  dotnet run");
                Console.WriteLine();
                
                // Quick check if server is accessible
                if (!await IsServerRunning(BASE_URL))
                {
                    Console.WriteLine("? Server is not running at " + BASE_URL);
                    Console.WriteLine("Please start the server before running this test.");
                    throw new InvalidOperationException("Blazor server is not running. Start it with: dotnet run --project Client/Client.csproj");
                }
                
                Console.WriteLine("? Server detected at " + BASE_URL);
            }
            
            // Initialize Playwright
            Console.WriteLine("Initializing Playwright...");
            _playwright = await Playwright.CreateAsync();
        }

        [ClassCleanup]
        public static async Task ClassCleanup()
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

            // Only kill the server if we started it
            if (_dotnetProcess != null && !_dotnetProcess.HasExited && _serverStartedByUs)
            {
                Console.WriteLine("Stopping Blazor server that we started...");
                _dotnetProcess.Kill();
                _dotnetProcess.Dispose();
            }
        }

        [TestInitialize]
        public void TestInitialize()
        {
            // Reset browser for each test to ensure clean state
            _browser = null;
        }

        [TestCleanup]
        public async Task TestCleanup()
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

        /// <summary>
        /// Interactive test - launches browser in headed mode (visible)
        /// You can interact with your WordScape game and experiment with it
        /// The browser stays open until you close it
        /// </summary>
        [TestMethod]
        [TestCategory("Interactive")]
        public async Task LaunchInteractiveBrowser_WordScapeGame()
        {
            Console.WriteLine("Launching interactive browser for WordScape game...");
            Console.WriteLine("You can now interact with the browser window.");
            Console.WriteLine("Close the browser window when you're done experimenting.");

            // Launch browser in headed mode (visible)
            _browser = await _playwright!.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false, // Show the browser window
                SlowMo = 100,     // Slow down operations by 100ms for easier viewing
                Devtools = true   // Open DevTools automatically
            });

            var context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1280, Height = 720 },
                // Optionally emulate mobile
                // IsMobile = true,
                // HasTouch = true
            });

            var page = await context.NewPageAsync();
            
            // Enable console logging from the browser
            page.Console += (_, msg) => Console.WriteLine($"[Browser Console] {msg.Text}");
            
            // Navigate to WordScape game
            Console.WriteLine($"Navigating to {BASE_URL}/wordscape");
            await page.GotoAsync($"{BASE_URL}/wordscape", new PageGotoOptions 
            { 
                WaitUntil = WaitUntilState.NetworkIdle 
            });

            // Wait for Blazor to initialize
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            
            Console.WriteLine("Page loaded. You can now interact with the WordScape game.");
            Console.WriteLine("\nTips:");
            Console.WriteLine("- Use DevTools (F12) to inspect elements and modify CSS");
            Console.WriteLine("- Use Console to test JavaScript");
            Console.WriteLine("- Modify files in your workspace and refresh to see changes");
            Console.WriteLine("- Close the browser window to end the test");

            // Wait until browser is closed
            while (_browser.IsConnected)
            {
                await Task.Delay(1000);
            }

            Console.WriteLine("Browser closed. Test ending.");
        }

        /// <summary>
        /// Automated test example - shows how to interact programmatically
        /// This can be useful for regression testing or automated UI testing
        /// </summary>
        [TestMethod]
        [TestCategory("Automated")]
        public async Task AutomatedTest_WordScapeGameInteraction()
        {
            _browser = await _playwright!.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false, // Set to true for CI/CD
                SlowMo = 500      // Slow down so you can see what's happening
            });

            var page = await _browser.NewPageAsync();
            await page.GotoAsync($"{BASE_URL}/wordscape");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            try
            {
                // Wait for the page to be fully loaded
                await page.WaitForSelectorAsync("body", new PageWaitForSelectorOptions
                {
                    State = WaitForSelectorState.Attached,
                    Timeout = 10000
                });

                Console.WriteLine("Page loaded successfully!");
                
                // FIXED: Wait for the actual grid container class used in WordScape
                await page.WaitForSelectorAsync(".grid-container", new PageWaitForSelectorOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = 10000
                });

                Console.WriteLine("WordScape grid loaded!");

                // FIXED: Get all grid cells using the correct class name
                var cells = await page.QuerySelectorAllAsync(".grid-cell");
                Console.WriteLine($"Found {cells.Count} cells in the grid");

                // Click the first 4 cells to form a word
                if (cells.Count >= 4)
                {
                    Console.WriteLine("Clicking cells to form a word...");
                    for (int i = 0; i < 4; i++)
                    {
                        await cells[i].ClickAsync();
                        await Task.Delay(300); // Brief delay between clicks
                    }

                    // Check if word display is updated
                    var currentWordElement = await page.QuerySelectorAsync(".current-word-display");
                    if (currentWordElement != null)
                    {
                        var currentWord = await currentWordElement.TextContentAsync();
                        Console.WriteLine($"Current word display: {currentWord}");
                    }
                }

                // Take a screenshot
                await page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = "wordscape-test-screenshot.png",
                    FullPage = true
                });
                
                Console.WriteLine("Screenshot saved to: wordscape-test-screenshot.png");

                // Keep browser open briefly to see the result
                await Task.Delay(3000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during automated test: {ex.Message}");
                throw;
            }
        }

        #region Helper Methods

        private static async Task<bool> IsServerRunning(string url, int timeoutSeconds = 5)
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

        private static Process StartBlazorServer()
        {
            // Get the solution directory (parent of TestProject1)
            var testProjectDir = Path.GetDirectoryName(typeof(InteractiveWordScapeTest).Assembly.Location)!;
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

        private static async Task WaitForServer(string url, int timeoutSeconds = 60)
        {
            var client = new HttpClient();
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
