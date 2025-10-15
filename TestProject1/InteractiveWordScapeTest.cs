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
        
        // FIXED: Use fixed seed for reproducible letter selection (matches debug mode behavior)
        // Using seed=1 for test reproducibility (game uses seed=1 in debug mode for grid generation)
        private static Random _random = new Random(1);

        [ClassInitialize]
        public static async Task ClassInitialize(TestContext context)
        {
            if (AUTO_START_SERVER)
            {
                // Check if server is already running first
                if (await IsServerRunning(BASE_URL))
                {
                    Console.WriteLine("✅ Server is already running at " + BASE_URL);
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
                Console.WriteLine("⚠️  AUTO_START_SERVER is disabled.");
                Console.WriteLine("Please make sure your Blazor app is running:");
                Console.WriteLine("  cd Client");
                Console.WriteLine("  dotnet run");
                Console.WriteLine();
                
                // Quick check if server is accessible
                if (!await IsServerRunning(BASE_URL))
                {
                    Console.WriteLine("❌ Server is not running at " + BASE_URL);
                    Console.WriteLine("Please start the server before running this test.");
                    throw new InvalidOperationException("Blazor server is not running. Start it with: dotnet run --project Client/Client.csproj");
                }
                
                Console.WriteLine("✅ Server detected at " + BASE_URL);
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
            
            // IMPORTANT: Reset random with fixed seed for each test to ensure reproducibility
            _random = new Random(1);
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
                ViewportSize = new ViewportSize { Width = 1280, Height = 1600 },
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
        /// Automated test - randomly selects letters from the wheel using drag interaction
        /// Uses FIXED SEED (1) for reproducible letter selection
        /// Game uses debug=true with FIXED SEED (1) for reproducible grid generation
        /// Together this ensures identical test results on every run
        /// </summary>
        [TestMethod]
        [TestCategory("Automated")]
        public async Task AutomatedTest_RandomLetterSelection()
        {
            Console.WriteLine("🎲 Using FIXED SEED (1) for reproducible random letter selection");
            Console.WriteLine("🎯 Game will use debug=true with FIXED SEED (1) for reproducible grid");
            Console.WriteLine("✅ This ensures identical test results on every run\n");
            
            _browser = await _playwright!.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false, // Set to true for CI/CD
                SlowMo = 300      // Slow down so you can see what's happening
            });

            var context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1280, Height = 1600 }
            });

            var page = await context.NewPageAsync();
            
            // Enable Chrome DevTools Protocol for memory tracking
            var client = await page.Context.NewCDPSessionAsync(page);
            
            // Attach console listener
            var consoleMessages = new List<string>();
            
            page.Console += (_, msg) =>
            {
                var text = msg.Text;
                consoleMessages.Add(text);
                Console.WriteLine($"[Browser Console] {text}");
            };
            
            // Navigate with debug=true parameter for reproducible grid generation
            Console.WriteLine("✅ Navigating to WordScape with debug=true for reproducible grid...");
            await page.GotoAsync($"{BASE_URL}/wordscape?debug=true");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            try
            {
                // Wait for the page to be fully loaded
                await page.WaitForSelectorAsync(".letter-wheel", new PageWaitForSelectorOptions
                {
                    State = WaitForSelectorState.Attached,
                    Timeout = 10000
                });

                Console.WriteLine("Page loaded successfully!");
                
                // Wait for all letter containers to be rendered
                await Task.Delay(2000);
                
                // Get all letter containers
                var letterContainers = await page.QuerySelectorAllAsync("g.letter-container");
                int totalLetters = letterContainers.Count;
                
                Console.WriteLine($"\n✅ Found {totalLetters} letters in the wheel");
                Console.WriteLine($"🎲 Random seed: 1 (fixed for reproducibility)");
                
                // Try forming 10 random words
                int attempts = 30;
                for (int i = 0; i < attempts; i++)
                {
                    Console.WriteLine($"\n--- Attempt {i + 1}/{attempts} ---");
                    
                    // Randomly select between 3-8 letters using FIXED SEED
                    int wordLength = _random.Next(3, Math.Min(9, totalLetters + 1));
                    Console.WriteLine($"Selecting {wordLength} random letters (using seed 1)...");
                    
                    // Create a list of available letter indices
                    var availableIndices = Enumerable.Range(0, totalLetters).ToList();
                    var selectedIndices = new List<int>();
                    
                    // Randomly select letters using FIXED SEED
                    for (int j = 0; j < wordLength && availableIndices.Count > 0; j++)
                    {
                        int randomIndex = _random.Next(availableIndices.Count);
                        int letterIndex = availableIndices[randomIndex];
                        selectedIndices.Add(letterIndex);
                        availableIndices.RemoveAt(randomIndex);
                    }
                    
                    Console.WriteLine($"Selected letter indices: {string.Join(", ", selectedIndices)}");
                    
                    // CRITICAL FIX: Use drag interaction instead of simple clicks
                    // WordScape uses mousedown -> mousemove -> mouseup pattern
                    
                    if (selectedIndices.Count > 0)
                    {
                        // Start drag on first letter
                        var firstLetter = letterContainers[selectedIndices[0]];
                        var firstBox = await firstLetter.BoundingBoxAsync();
                        
                        if (firstBox != null)
                        {
                            // Get first letter text
                            var firstTextElement = await firstLetter.QuerySelectorAsync("text");
                            var firstLetterText = firstTextElement != null ? await firstTextElement.TextContentAsync() : "?";
                            
                            Console.WriteLine($"  Starting drag on letter {selectedIndices[0]}: {firstLetterText}");
                            
                            // Mouse down on first letter (center of the circle)
                            var startX = firstBox.X + firstBox.Width / 2;
                            var startY = firstBox.Y + firstBox.Height / 2;
                            await page.Mouse.MoveAsync(startX, startY);
                            await page.Mouse.DownAsync();
                            await Task.Delay(100);
                            
                            // Drag through remaining letters
                            for (int j = 1; j < selectedIndices.Count; j++)
                            {
                                var letterElement = letterContainers[selectedIndices[j]];
                                var box = await letterElement.BoundingBoxAsync();
                                
                                if (box != null)
                                {
                                    // Get letter text for logging
                                    var textElement = await letterElement.QuerySelectorAsync("text");
                                    var letterText = textElement != null ? await textElement.TextContentAsync() : "?";
                                    
                                    Console.WriteLine($"  Dragging to letter {selectedIndices[j]}: {letterText}");
                                    
                                    // Move mouse to center of this letter
                                    var x = box.X + box.Width / 2;
                                    var y = box.Y + box.Height / 2;
                                    await page.Mouse.MoveAsync(x, y);
                                    await Task.Delay(150); // Pause between letters
                                }
                            }
                            
                            // Mouse up to complete selection
                            await page.Mouse.UpAsync();
                            Console.WriteLine("  Drag complete - releasing mouse");
                            await Task.Delay(500);
                        }
                    }
                    
                    // Wait a moment to see the formed word
                    await Task.Delay(500);
                    
                    // Check current word display
                    try
                    {
                        var currentWordDisplay = await page.QuerySelectorAsync(".current-word-display");
                        var currentWord = currentWordDisplay != null ? await currentWordDisplay.TextContentAsync() : "";
                        Console.WriteLine($"  ✅ Current word formed: '{currentWord}'");
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("  ⚠️ Could not read current word display");
                    }
                    
                    // Wait for word to be processed
                    await Task.Delay(1000);
                    
                    // Take a screenshot for this attempt
                    await page.ScreenshotAsync(new PageScreenshotOptions
                    {
                        Path = $"wordscape-random-attempt-{i + 1}.png",
                        FullPage = true
                    });
                    
                    Console.WriteLine($"  📸 Screenshot saved: wordscape-random-attempt-{i + 1}.png");
                }
                
                Console.WriteLine($"\n✅ Completed {attempts} random letter selection attempts");
                Console.WriteLine($"🎲 All selections used fixed seed (1) - results are reproducible!");
                
                // Take a final screenshot
                await page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = "wordscape-random-final.png",
                    FullPage = true
                });
                
                Console.WriteLine("\nFinal screenshot saved.");
                Console.WriteLine($"Total console messages: {consoleMessages.Count}");

                // Keep browser open briefly to see the result
                Console.WriteLine("\nKeeping browser open for 10 seconds...");
                await Task.Delay(5000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during automated test: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
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
