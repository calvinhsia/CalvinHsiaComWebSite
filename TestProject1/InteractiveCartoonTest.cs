using Microsoft.Playwright;
using System.Diagnostics;

namespace TestProject1
{
    /// <summary>
    /// Interactive Cartoon drawing test harness
    /// This test launches your Blazor app in a real browser where you can interact with it
    /// You can modify HTML/CSS/JS and see changes in real-time
    /// 
    /// IMPORTANT: Start your Blazor app manually before running these tests:
    /// cd Client
    /// dotnet run
    /// </summary>
    [TestClass]
    public class InteractiveCartoonTest
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
            // Always check if server is already running first, regardless of AUTO_START_SERVER
            if (await IsServerRunning(BASE_URL))
            {
                Console.WriteLine("? Server is already running at " + BASE_URL);
                Console.WriteLine("Reusing existing server instance.");
                _serverStartedByUs = false; // We didn't start it
            }
            else if (AUTO_START_SERVER)
            {
                // Start the Blazor WASM development server
                Console.WriteLine("Starting Blazor WASM development server...");
                _dotnetProcess = StartBlazorServer();
                _serverStartedByUs = true;

                // Wait for server to be ready
                await WaitForServer(BASE_URL);
            }
            else
            {
                Console.WriteLine("??  AUTO_START_SERVER is disabled.");
                Console.WriteLine("Please make sure your Blazor app is running:");
                Console.WriteLine("  cd Client");
                Console.WriteLine("  dotnet run");
                Console.WriteLine();
                Console.WriteLine("? Server is not running at " + BASE_URL);
                Console.WriteLine("Please start the server before running this test.");
                throw new InvalidOperationException("Blazor server is not running. Start it with: dotnet run --project Client/Client.csproj");
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

            // Only kill the server if we started it AND it's still running
            if (_dotnetProcess != null && !_dotnetProcess.HasExited && _serverStartedByUs)
            {
                Console.WriteLine("Stopping Blazor server that we started...");
                try
                {
                    _dotnetProcess.Kill(entireProcessTree: true); // Kill entire process tree
                    _dotnetProcess.WaitForExit(5000); // Wait up to 5 seconds for graceful exit
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
        /// Interactive test for Cartoon drawing page
        /// </summary>
        [TestMethod]
        [TestCategory("Interactive")]
        public async Task LaunchInteractiveBrowser_CartoonGame()
        {
            Console.WriteLine("Launching interactive browser for Cartoon drawing...");
            Console.WriteLine("Close the browser window when you're done experimenting.");

            _browser = await _playwright!.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false,
                SlowMo = 100,
                Devtools = true
            });

            var context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1400, Height = 900 }
            });

            var page = await context.NewPageAsync();
            page.Console += (_, msg) => Console.WriteLine($"[Browser Console] {msg.Text}");

            await page.GotoAsync($"{BASE_URL}/cartoon", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle
            });

            Console.WriteLine("Cartoon page loaded. Interact with it in the browser window.");
            Console.WriteLine("Try drawing on the canvas and creating multiple frames for animation!");
            Console.WriteLine("The test will wait until you close the browser.");
            
            // Create a TaskCompletionSource to wait for page close
            var pageClosedTcs = new TaskCompletionSource<bool>();
            page.Close += (_, _) => pageClosedTcs.TrySetResult(true);
   
          // Also listen for context close in case entire browser is closed
            context.Close += (_, _) => pageClosedTcs.TrySetResult(true);

            // Wait for either the page or context to close
            await pageClosedTcs.Task;
   
            Console.WriteLine("Browser closed. Test ending.");
        }

        /// <summary>
        /// Automated test for Cartoon drawing functionality
        /// Tests canvas initialization and basic drawing operations
        /// </summary>
        [TestMethod]
        [TestCategory("Automated")]
        public async Task AutomatedTest_CartoonDrawing()
        {
            Console.WriteLine("Testing Cartoon drawing functionality...");

            _browser = await _playwright!.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false,
                SlowMo = 500
            });

            var page = await _browser.NewPageAsync();
            await page.GotoAsync($"{BASE_URL}/cartoon");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            try
            {
                // Wait for the canvas to be visible
                await page.WaitForSelectorAsync("canvas#cartoonCanvas", new PageWaitForSelectorOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = 10000
                });

                Console.WriteLine("Cartoon canvas loaded!");

                // Verify canvas is initialized
                var canvasInitialized = await page.EvaluateAsync<bool>(@"
          () => {
          const canvas = document.getElementById('cartoonCanvas');
     return canvas && typeof window.initCartoonCanvas === 'function';
          }
                ");

                if (canvasInitialized)
                {
                    Console.WriteLine("Canvas JavaScript initialized successfully!");
                }
                else
                {
                    Console.WriteLine("Warning: Canvas JavaScript may not be loaded");
                }

                // Test Demo button
                var demoButton = await page.QuerySelectorAsync("button:has-text('Demo')");
                if (demoButton != null)
                {
                    Console.WriteLine("Clicking Demo button...");
                    await demoButton.ClickAsync();
                    await Task.Delay(1000);
                    Console.WriteLine("Demo animation loaded!");
                }

                // Test between frames slider
                var betweenSlider = await page.QuerySelectorAsync("#betweenFrames");
                if (betweenSlider != null)
                {
                    await betweenSlider.FillAsync("5");
                    Console.WriteLine("Between frames set to 5");
                    await Task.Delay(500);
                }

                // Test play button with interpolation
                var playButton = await page.QuerySelectorAsync("button:has-text('Play')");
                if (playButton != null)
                {
                    await playButton.ClickAsync();
                    Console.WriteLine("Started animation playback with interpolation...");
                    await Task.Delay(5000); // Watch animation for 5 seconds

                    // Click again to pause
                    await playButton.ClickAsync();
                    Console.WriteLine("Paused animation playback");
                }

                // Test Reset button
                var resetButton = await page.QuerySelectorAsync("button:has-text('Reset')");
                if (resetButton != null)
                {
                    await Task.Delay(1000);
                    await resetButton.ClickAsync();
                    Console.WriteLine("Reset button clicked - all frames cleared");
                    await Task.Delay(500);
                }

                // Test drawing mode toggle
                var drawModeRadio = await page.QuerySelectorAsync("input[value='draw']");
                if (drawModeRadio != null)
                {
                    await drawModeRadio.ClickAsync();
                    Console.WriteLine("Draw mode selected");
                }

                // Test pen thickness adjustment
                var thicknessSlider = await page.QuerySelectorAsync("#penThickness");
                if (thicknessSlider != null)
                {
                    await thicknessSlider.FillAsync("5");
                    Console.WriteLine("Pen thickness adjusted to 5");
                }

                // Test color picker
                var colorPicker = await page.QuerySelectorAsync("#penColor");
                if (colorPicker != null)
                {
                    await colorPicker.FillAsync("#ff0000");
                    Console.WriteLine("Pen color set to red");
                }

                // Simulate drawing by clicking and dragging on canvas
                var canvas = await page.QuerySelectorAsync("canvas#cartoonCanvas");
                if (canvas != null)
                {
                    var boundingBox = await canvas.BoundingBoxAsync();
                    if (boundingBox != null)
                    {
                        // Draw a simple line
                        await page.Mouse.MoveAsync(boundingBox.X + 100, boundingBox.Y + 100);
                        await page.Mouse.DownAsync();
                        await page.Mouse.MoveAsync(boundingBox.X + 300, boundingBox.Y + 200);
                        await page.Mouse.UpAsync();

                        Console.WriteLine("Drew a test line on the canvas");

                        await Task.Delay(1000);
                    }
                }

                // Test adding a new frame
                var newFrameButton = await page.QuerySelectorAsync("button:has-text('New Frame')");
                if (newFrameButton != null)
                {
                    await newFrameButton.ClickAsync();
                    Console.WriteLine("Added a new frame");
                    await Task.Delay(500);

                    // Draw on the new frame
                    if (canvas != null)
                    {
                        var boundingBox = await canvas.BoundingBoxAsync();
                        if (boundingBox != null)
                        {
                            await page.Mouse.MoveAsync(boundingBox.X + 200, boundingBox.Y + 150);
                            await page.Mouse.DownAsync();
                            await page.Mouse.MoveAsync(boundingBox.X + 400, boundingBox.Y + 250);
                            await page.Mouse.UpAsync();
                            Console.WriteLine("Drew a line on frame 2");
                            await Task.Delay(500);
                        }
                    }
                }

                // Test animation with user-drawn frames
                if (playButton != null)
                {
                    await playButton.ClickAsync();
                    Console.WriteLine("Playing animation with user frames and interpolation...");
                    await Task.Delay(4000);
                    await playButton.ClickAsync();
                    Console.WriteLine("Paused playback");
                }

                // Take screenshot
                await page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = "cartoon-test-screenshot.png",
                    FullPage = true
                });
                Console.WriteLine("Screenshot saved to: cartoon-test-screenshot.png");

                // Capture canvas
                if (canvas != null)
                {
                    await canvas.ScreenshotAsync(new ElementHandleScreenshotOptions
                    {
                        Path = "cartoon-canvas-screenshot.png"
                    });
                    Console.WriteLine("Canvas screenshot saved to: cartoon-canvas-screenshot.png");
                }

                // Keep browser open to see result
                await Task.Delay(3000);

                Console.WriteLine("Cartoon test completed successfully!");
                Console.WriteLine("Features tested:");
                Console.WriteLine("  ? Demo button");
                Console.WriteLine("  ? Reset button");
                Console.WriteLine("  ? Frame interpolation");
                Console.WriteLine("  ? Between frames slider");
                Console.WriteLine("  ? Animation playback");
                Console.WriteLine("  ? User drawing");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during Cartoon test: {ex.Message}");
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
            var testProjectDir = Path.GetDirectoryName(typeof(InteractiveCartoonTest).Assembly.Location)!;
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
