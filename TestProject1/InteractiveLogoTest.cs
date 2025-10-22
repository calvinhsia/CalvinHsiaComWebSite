using Microsoft.Playwright;
using System.Diagnostics;

namespace TestProject1
{
    /// <summary>
    /// Interactive Logo game test harness
    /// This test launches your Blazor app in a real browser where you can interact with it
    /// You can modify HTML/CSS/JS and see changes in real-time
    /// 
    /// IMPORTANT: Start your Blazor app manually before running these tests:
    /// cd Client
    /// dotnet run
    /// </summary>
    [TestClass]
    public class InteractiveLogoTest
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
        /// Interactive test for Logo game
        /// </summary>
        [TestMethod]
        [TestCategory("Interactive")]
        public async Task LaunchInteractiveBrowser_LogoGame()
        {
            Console.WriteLine("Launching interactive browser for Logo game...");
            Console.WriteLine("Close the browser window when you're done experimenting.");
            
            _browser = await _playwright!.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false,
                SlowMo = 100,
                Devtools = true
            });

            var context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1280, Height = 720 }
            });

            var page = await context.NewPageAsync();
            page.Console += (_, msg) => Console.WriteLine($"[Browser Console] {msg.Text}");
            
            await page.GotoAsync($"{BASE_URL}/logo", new PageGotoOptions 
            { 
                WaitUntil = WaitUntilState.NetworkIdle 
            });

            Console.WriteLine("Logo game loaded. Interact with it in the browser window.");
            Console.WriteLine("The test will wait until you close the browser.");
            
            // Wait until browser is closed
            while (_browser.IsConnected)
            {
                await Task.Delay(1000);
            }
            
            Console.WriteLine("Browser closed. Test ending.");
        }

        /// <summary>
        /// Test Logo game commands
        /// Demonstrates JavaScript execution and canvas capture
        /// </summary>
        [TestMethod]
        [TestCategory("Automated")]
        public async Task AutomatedTest_LogoGameCommands()
        {
            Console.WriteLine("Testing Logo game commands...");
            
            _browser = await _playwright!.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false,
                SlowMo = 500
            });

            var page = await _browser.NewPageAsync();
            await page.GotoAsync($"{BASE_URL}/logo");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            try
            {
                // Wait for the Logo game canvas
                await page.WaitForSelectorAsync("canvas#logoCanvas", new PageWaitForSelectorOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = 10000
                });

                Console.WriteLine("Logo canvas loaded!");

                // FIXED: Use the correct class name from LogoGame.razor
                var codeEditor = await page.QuerySelectorAsync("textarea.logo-code-textarea");
                
                if (codeEditor != null)
                {
                    Console.WriteLine("Found code editor, entering Logo commands...");
                    
                    // Clear existing content and type Logo commands
                    await codeEditor.FillAsync(@"
forward 100
right 90
forward 100
right 90
forward 100
right 90
forward 100
");

                    Console.WriteLine("Logo commands entered:");
                    Console.WriteLine("- Drawing a square");

                    // FIXED: Find the Run button using the correct class from LogoGame.razor
                    var runButton = await page.QuerySelectorAsync("button.logo-run-button");
                    if (runButton != null)
                    {
                        Console.WriteLine("Clicking Run button...");
                        await runButton.ClickAsync();
                        
                        // Wait for animation to complete
                        await Task.Delay(3000);
                        
                        Console.WriteLine("Logo commands executed!");
                    }
                    else
                    {
                        Console.WriteLine("Run button not found!");
                        // Take a screenshot to debug
                        await page.ScreenshotAsync(new PageScreenshotOptions
                        {
                            Path = "logo-debug-no-button.png",
                            FullPage = true
                        });
                    }
                }
                else
                {
                    Console.WriteLine("Code editor not found!");
                    
                    // Take a screenshot to debug what's on the page
                    await page.ScreenshotAsync(new PageScreenshotOptions
                    {
                        Path = "logo-debug-no-editor.png",
                        FullPage = true
                    });
                    
                    // Try to list what elements exist
                    var elementsFound = await page.EvaluateAsync<string>(@"
                        () => {
                            const textareas = document.querySelectorAll('textarea');
                            const buttons = document.querySelectorAll('button');
                            return `Textareas: ${textareas.length}, Buttons: ${buttons.length}`;
                        }
                    ");
                    Console.WriteLine($"Elements found on page: {elementsFound}");
                }

                // Capture the canvas
                var canvas = await page.QuerySelectorAsync("canvas#logoCanvas");
                if (canvas != null)
                {
                    await canvas.ScreenshotAsync(new ElementHandleScreenshotOptions
                    {
                        Path = "logo-canvas-screenshot.png"
                    });
                    Console.WriteLine("Canvas screenshot saved to: logo-canvas-screenshot.png");
                }

                // Take full page screenshot
                await page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = "logo-test-screenshot.png",
                    FullPage = true
                });
                
                Console.WriteLine("Full page screenshot saved to: logo-test-screenshot.png");

                // Get canvas state via JavaScript
                var canvasData = await page.EvaluateAsync<string>(@"
                    () => {
                        const canvas = document.querySelector('canvas#logoCanvas');
                        if (canvas) {
                            const ctx = canvas.getContext('2d');
                            return canvas.toDataURL();
                        }
                        return null;
                    }
                ");
                
                if (!string.IsNullOrEmpty(canvasData))
                {
                    Console.WriteLine($"Canvas data captured: {canvasData.Substring(0, 50)}...");
                }

                // Keep browser open to see result
                await Task.Delay(3000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during Logo test: {ex.Message}");
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
            var testProjectDir = Path.GetDirectoryName(typeof(InteractiveLogoTest).Assembly.Location)!;
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
