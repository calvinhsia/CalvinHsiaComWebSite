using Microsoft.Playwright;

namespace TestProject1
{
    /// <summary>
    /// Interactive WordScape game test harness
    /// </summary>
    [TestClass]
    public class InteractiveWordScapeTest : InteractiveTestBase
    {
        // FIXED: Use fixed seed for reproducible letter selection
        private static Random _random = new Random(1);

        [ClassInitialize]
        public static async Task ClassInitialize(TestContext context)
        {
            await BaseClassInitialize(context);
        }

        [ClassCleanup]
        public static async Task ClassCleanup()
        {
            await BaseClassCleanup();
        }

        [TestInitialize]
        public new void BaseTestInitialize()
        {
            base.BaseTestInitialize();
            // Reset random with fixed seed for each test to ensure reproducibility
            _random = new Random(1);
        }

        /// <summary>
        /// Interactive test - launches browser in headed mode
        /// </summary>
        [TestMethod]
        [TestCategory("Interactive")]
        public async Task LaunchInteractiveBrowser_WordScapeGame()
        {
            Console.WriteLine("Launching interactive browser for WordScape game...");
            Console.WriteLine("Close the browser window when you're done experimenting.");

            _browser = await _playwright!.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false,
                SlowMo = 100,
                Devtools = true
            });

            var context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1280, Height = 1600 }
            });

            var page = await context.NewPageAsync();
            page.Console += (_, msg) => Console.WriteLine($"[Browser Console] {msg.Text}");

            Console.WriteLine($"Navigating to {BASE_URL}/wordscape");
            await page.GotoAsync($"{BASE_URL}/wordscape", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle
            });

            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            Console.WriteLine("WordScape game loaded. Interact with it in the browser window.");

            // Create a TaskCompletionSource to wait for page close
            var pageClosedTcs = new TaskCompletionSource<bool>();
            page.Close += (_, _) =>
       {
           Console.WriteLine("[Event] Page.Close event fired");
           pageClosedTcs.TrySetResult(true);
       };

            context.Close += (_, _) =>
                      {
                          Console.WriteLine("[Event] Context.Close event fired");
                          pageClosedTcs.TrySetResult(true);
                      };

            await pageClosedTcs.Task;
            Console.WriteLine("Browser closed. Test ending.");
        }

        /// <summary>
        /// Automated test - randomly selects letters from the wheel
        /// Uses FIXED SEED (1) for reproducible results
        /// </summary>
        [TestMethod]
        [TestCategory("Interactive")]
        public async Task AutomatedTest_RandomLetterSelection()
        {
            Console.WriteLine("🎲 Using FIXED SEED (1) for reproducible random letter selection");

            _browser = await _playwright!.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false,
                SlowMo = 300
            });

            var context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1280, Height = 1600 }
            });

            var page = await context.NewPageAsync();

            var consoleMessages = new List<string>();
            page.Console += (_, msg) =>
            {
                var text = msg.Text;
                consoleMessages.Add(text);
                Console.WriteLine($"[Browser Console] {text}");
            };

            Console.WriteLine("✅ Navigating to WordScape with debug=true for reproducible grid...");
            await page.GotoAsync($"{BASE_URL}/wordscape?debug=true");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            try
            {
                await page.WaitForSelectorAsync(".letter-wheel", new PageWaitForSelectorOptions
                {
                    State = WaitForSelectorState.Attached,
                    Timeout = 10000
                });

                Console.WriteLine("Page loaded successfully!");
                await Task.Delay(2000);

                var letterContainers = await page.QuerySelectorAllAsync("g.letter-container");
                int totalLetters = letterContainers.Count;

                Console.WriteLine($"\n✅ Found {totalLetters} letters in the wheel");
                Console.WriteLine($"🎲 Random seed: 1 (fixed for reproducibility)");

                Console.WriteLine("\n📊 Initial Memory Stats:");
                await LogMemoryStats(page);

                int attempts = 90;
                for (int i = 0; i < attempts; i++)
                {
                    Console.WriteLine($"\n--- Attempt {i + 1}/{attempts} ---");

                    int wordLength = _random.Next(3, Math.Min(9, totalLetters + 1));
                    Console.WriteLine($"Selecting {wordLength} random letters...");

                    var availableIndices = Enumerable.Range(0, totalLetters).ToList();
                    var selectedIndices = new List<int>();

                    for (int j = 0; j < wordLength && availableIndices.Count > 0; j++)
                    {
                        int randomIndex = _random.Next(availableIndices.Count);
                        int letterIndex = availableIndices[randomIndex];
                        selectedIndices.Add(letterIndex);
                        availableIndices.RemoveAt(randomIndex);
                    }

                    Console.WriteLine($"Selected letter indices: {string.Join(", ", selectedIndices)}");

                    if (selectedIndices.Count > 0)
                    {
                        var firstLetter = letterContainers[selectedIndices[0]];
                        var firstBox = await firstLetter.BoundingBoxAsync();

                        if (firstBox != null)
                        {
                            var firstTextElement = await firstLetter.QuerySelectorAsync("text");
                            var firstLetterText = firstTextElement != null ? await firstTextElement.TextContentAsync() : "?";

                            Console.WriteLine($"  Starting drag on letter {selectedIndices[0]}: {firstLetterText}");

                            var startX = firstBox.X + firstBox.Width / 2;
                            var startY = firstBox.Y + firstBox.Height / 2;
                            await page.Mouse.MoveAsync(startX, startY);
                            await page.Mouse.DownAsync();
                            await Task.Delay(100);

                            for (int j = 1; j < selectedIndices.Count; j++)
                            {
                                var letterElement = letterContainers[selectedIndices[j]];
                                var box = await letterElement.BoundingBoxAsync();

                                if (box != null)
                                {
                                    var textElement = await letterElement.QuerySelectorAsync("text");
                                    var letterText = textElement != null ? await textElement.TextContentAsync() : "?";

                                    Console.WriteLine($"  Dragging to letter {selectedIndices[j]}: {letterText}");

                                    var x = box.X + box.Width / 2;
                                    var y = box.Y + box.Height / 2;
                                    await page.Mouse.MoveAsync(x, y);
                                    await Task.Delay(150);
                                }
                            }

                            await page.Mouse.UpAsync();
                            Console.WriteLine("  Drag complete");
                            await Task.Delay(500);
                        }
                    }

                    await Task.Delay(500);

                    try
                    {
                        var currentWordDisplay = await page.QuerySelectorAsync(".current-word-display");
                        var currentWord = currentWordDisplay != null ? await currentWordDisplay.TextContentAsync() : "";
                        Console.WriteLine($"  ✅ Current word formed: '{currentWord}'");
                    }
                    catch { }

                    await Task.Delay(1000);

                    if ((i + 1) % 5 == 0)
                    {
                        Console.WriteLine($"\n📊 Memory Stats after {i + 1} iterations:");
                        await LogMemoryStats(page);
                    }

                    await page.ScreenshotAsync(new PageScreenshotOptions
                    {
                        Path = $"wordscape-random-attempt-{i + 1}.png",
                        FullPage = true
                    });

                    Console.WriteLine($"  📸 Screenshot saved: wordscape-random-attempt-{i + 1}.png");
                }

                Console.WriteLine($"\n✅ Completed {attempts} random letter selection attempts");

                Console.WriteLine("\n📊 Final Memory Stats:");
                await LogMemoryStats(page);

                await page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = "wordscape-random-final.png",
                    FullPage = true
                });

                Console.WriteLine($"Total console messages: {consoleMessages.Count}");
                Console.WriteLine("\nKeeping browser open for 5 seconds...");
                await Task.Delay(5000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during automated test: {ex.Message}");
                throw;
            }
        }

        private static async Task LogMemoryStats(IPage page)
        {
            try
            {
                var memoryInfo = await page.EvaluateAsync<dynamic>(@"
   () => {
if (performance.memory) {
         return {
     usedJSHeapSize: performance.memory.usedJSHeapSize,
                  totalJSHeapSize: performance.memory.totalJSHeapSize,
       jsHeapSizeLimit: performance.memory.jsHeapSizeLimit,
       available: true
       };
       }
               return { available: false };
  }
          ");

                if (memoryInfo.available)
                {
                    double usedMB = memoryInfo.usedJSHeapSize / (1024.0 * 1024.0);
                    double totalMB = memoryInfo.totalJSHeapSize / (1024.0 * 1024.0);
                    double limitMB = memoryInfo.jsHeapSizeLimit / (1024.0 * 1024.0);

                    Console.WriteLine($"  💾 JS Heap Used: {usedMB:F2} MB / {totalMB:F2} MB (Limit: {limitMB:F2} MB)");
                    Console.WriteLine($"  📈 Memory Usage: {(usedMB / limitMB * 100):F1}% of limit");
                }
            }
            catch { }
        }
    }
}
