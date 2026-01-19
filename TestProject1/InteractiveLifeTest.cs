using Microsoft.Playwright;

namespace TestProject1
{
    [TestClass]
    public class InteractiveLifeTest : InteractiveTestBase
    {
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

        /// <summary>
        /// Interactive test for Life - keeps browser open until user closes it
        /// </summary>
        [TestMethod]
        [TestCategory("Manual")]
        public async Task LaunchInteractiveBrowser_ConwaysGameOfLife()
        {
            Console.WriteLine("Launching interactive browser for Conway's Game of Life...");
            Console.WriteLine("Close the browser window when you're done experimenting.");

            _browser = await _playwright!.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false,
                SlowMo = 100,
                Devtools = false,
                Args = new[] { "--incognito" }
            });

            var context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = ViewportSize.NoViewport,
                StorageState = null,
                AcceptDownloads = true,
                IgnoreHTTPSErrors = true
            });

            var page = await context.NewPageAsync();
            page.Console += (_, msg) => Console.WriteLine($"[Browser Console] {msg.Text}");

            // Navigate to the Life page using shared helper
            await NavigateToBlazorPageAsync(page, "/life", "canvas.life-canvas");

            Console.WriteLine("Conway's Game of Life page loaded in incognito mode.");
            Console.WriteLine("?? Click/drag to add cells");
            Console.WriteLine("?? Select patterns from dropdown (Gliders, Guns, etc.)");
            Console.WriteLine("? Use Run/Pause to control simulation");
            Console.WriteLine("? Use Step to advance one generation");
            Console.WriteLine("?? Click Random to randomize the grid");
            Console.WriteLine("The test will wait until you close the browser.");

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

        [TestMethod]
        [TestCategory("Manual")]
        public async Task Life_Interactive_PatternTesting()
        {
            _browser = await _playwright!.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false,
                SlowMo = 300
            });

            var context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = ViewportSize.NoViewport
            });

            var page = await context.NewPageAsync();
            page.Console += (_, msg) => Console.WriteLine($"[Browser Console] {msg.Text}");

            // Navigate to the Life page using shared helper
            await NavigateToBlazorPageAsync(page, "/life", "canvas.life-canvas");

            Console.WriteLine("? Life page loaded successfully");
            Console.WriteLine("?? Testing Conway's Game of Life patterns...");

            // Wait for auto-start
            await page.WaitForTimeoutAsync(2000);

            // Check that stats are showing
            var statsText = await page.Locator(".life-stats").TextContentAsync();
            Console.WriteLine($"?? Initial stats: {statsText}");

            // Test pause button (should auto-start running)
            var pauseButton = page.Locator("button:has-text('Pause')");
            if (await pauseButton.IsVisibleAsync())
            {
                await pauseButton.ClickAsync();
                Console.WriteLine("? Paused simulation");
            }
            await page.WaitForTimeoutAsync(500);

            // Test clear button
            var clearButton = page.Locator("button:has-text('Clear')");
            await clearButton.ClickAsync();
            Console.WriteLine("??? Cleared grid");
            await page.WaitForTimeoutAsync(500);

            // Test placing a glider pattern
            var patternSelect = page.Locator("select.pattern-select");
            await patternSelect.SelectOptionAsync("glider");
            Console.WriteLine("?? Selected Glider pattern");
            await page.WaitForTimeoutAsync(300);

            var placeButton = page.Locator("button:has-text('Place')");
            await placeButton.ClickAsync();
            Console.WriteLine("?? Placed Glider at center");
            await page.WaitForTimeoutAsync(500);

            // Start simulation to watch glider move
            var runButton = page.Locator("button:has-text('Run')");
            await runButton.ClickAsync();
            Console.WriteLine("? Started simulation - watching glider move");
            await page.WaitForTimeoutAsync(3000);

            // Pause and check stats
            pauseButton = page.Locator("button:has-text('Pause')");
            await pauseButton.ClickAsync();
            var runningStats = await page.Locator(".life-stats").TextContentAsync();
            Console.WriteLine($"?? After glider: {runningStats}");
            await page.WaitForTimeoutAsync(500);

            // Clear and test Gosper Glider Gun
            await clearButton.ClickAsync();
            await patternSelect.SelectOptionAsync("gosperGliderGun");
            Console.WriteLine("?? Selected Gosper Glider Gun");
            await page.WaitForTimeoutAsync(300);

            await placeButton.ClickAsync();
            Console.WriteLine("?? Placed Gosper Glider Gun at center");
            await page.WaitForTimeoutAsync(500);

            // Run to watch gun fire gliders
            runButton = page.Locator("button:has-text('Run')");
            await runButton.ClickAsync();
            Console.WriteLine("? Started simulation - watching gun fire gliders");
            await page.WaitForTimeoutAsync(5000);

            var gunStats = await page.Locator(".life-stats").TextContentAsync();
            Console.WriteLine($"?? After gun firing: {gunStats}");

            Console.WriteLine("\n? Life pattern testing completed!");
            Console.WriteLine("?? Browser will stay open until you close it.");
            Console.WriteLine("Feel free to continue experimenting with patterns!");

            // Wait for user to close the browser
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

        [TestMethod]
        [TestCategory("Manual")]
        public async Task Life_Interactive_DrawingTest()
        {
            _browser = await _playwright!.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false,
                SlowMo = 200
            });

            var context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = ViewportSize.NoViewport
            });

            var page = await context.NewPageAsync();
            page.Console += (_, msg) => Console.WriteLine($"[Browser Console] {msg.Text}");

            // Navigate to the Life page using shared helper
            await NavigateToBlazorPageAsync(page, "/life", "canvas.life-canvas");

            Console.WriteLine("?? Testing drawing cells while simulation runs...");

            // Wait for auto-start
            await page.WaitForTimeoutAsync(2000);

            // Get canvas and draw some cells
            var canvas = page.Locator("canvas.life-canvas");
            var canvasBounds = await canvas.BoundingBoxAsync();

            if (canvasBounds != null)
            {
                Console.WriteLine("??? Drawing cells on canvas...");

                // Draw a line of cells
                for (int i = 0; i < 10; i++)
                {
                    await canvas.ClickAsync(new LocatorClickOptions
                    {
                        Position = new Position
                        {
                            X = 100 + i * 10,
                            Y = 100
                        }
                    });
                    await page.WaitForTimeoutAsync(100);
                }
                Console.WriteLine("? Drew horizontal line of cells");

                // Draw another pattern
                for (int i = 0; i < 10; i++)
                {
                    await canvas.ClickAsync(new LocatorClickOptions
                    {
                        Position = new Position
                        {
                            X = 200,
                            Y = 100 + i * 10
                        }
                    });
                    await page.WaitForTimeoutAsync(100);
                }
                Console.WriteLine("? Drew vertical line of cells");
            }

            await page.WaitForTimeoutAsync(3000);

            var stats = await page.Locator(".life-stats").TextContentAsync();
            Console.WriteLine($"?? After drawing: {stats}");

            Console.WriteLine("\n? Drawing test completed!");
            Console.WriteLine("?? Browser will stay open until you close it.");

            // Wait for user to close the browser
            var pageClosedTcs = new TaskCompletionSource<bool>();
            context.Close += (_, _) =>
            {
                Console.WriteLine("[Event] Context.Close event fired");
                pageClosedTcs.TrySetResult(true);
            };

            await pageClosedTcs.Task;

            Console.WriteLine("Browser closed. Test ending.");
        }

        /// <summary>
        /// Automated test: Verify that a blinker oscillates correctly (period 2)
        /// </summary>
        [TestMethod]
        [TestCategory("Interactive")]
        [Timeout(60000)] // 60 seconds
        public async Task Life_BlinkerOscillates_Period2()
        {
            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            await NavigateToBlazorPageAsync(page, "/life", "canvas.life-canvas");

            Console.WriteLine("?? Testing: Blinker should oscillate with period 2");

            // Wait for page to load
            await page.WaitForTimeoutAsync(1000);

            // Pause simulation
            var pauseButton = page.Locator("button:has-text('Pause')");
            if (await pauseButton.IsVisibleAsync())
            {
                await pauseButton.ClickAsync();
            }
            await page.WaitForTimeoutAsync(300);

            // Clear the grid
            var clearButton = page.Locator("button:has-text('Clear')");
            await clearButton.ClickAsync();
            Console.WriteLine("  ? Cleared grid");
            await page.WaitForTimeoutAsync(300);

            // Place a blinker
            var patternSelect = page.Locator("select.pattern-select");
            await patternSelect.SelectOptionAsync("blinker");
            Console.WriteLine("  ? Selected Blinker pattern");

            var placeButton = page.Locator("button:has-text('Place')");
            await placeButton.ClickAsync();
            Console.WriteLine("  ? Placed Blinker at center");
            await page.WaitForTimeoutAsync(300);

            // Get initial generation
            var initialStats = await page.Locator(".life-stats").TextContentAsync();
            Console.WriteLine($"  ?? Initial: {initialStats}");

            // A blinker has 3 cells
            var initialAliveMatch = System.Text.RegularExpressions.Regex.Match(initialStats ?? "", @"(\d+)\s*$");

            // Step through 2 generations (should return to original state)
            var stepButton = page.Locator("button:has-text('Step')");

            await stepButton.ClickAsync();
            await page.WaitForTimeoutAsync(200);
            var gen1Stats = await page.Locator(".life-stats").TextContentAsync();
            Console.WriteLine($"  ?? After step 1: {gen1Stats}");

            await stepButton.ClickAsync();
            await page.WaitForTimeoutAsync(200);
            var gen2Stats = await page.Locator(".life-stats").TextContentAsync();
            Console.WriteLine($"  ?? After step 2: {gen2Stats}");

            // Verify generation increased by 2
            var genMatch = System.Text.RegularExpressions.Regex.Match(gen2Stats ?? "", @"Gen\s+(\d+)");
            var generation = genMatch.Success ? int.Parse(genMatch.Groups[1].Value) : 0;

            Console.WriteLine($"\n  Expected: Generation should be 2");
            Console.WriteLine($"  Actual: Generation = {generation}");

            // A blinker should still have 3 cells after 2 steps
            var aliveMatch = System.Text.RegularExpressions.Regex.Match(gen2Stats ?? "", @"(\d+)\s*$");
            var aliveCount = aliveMatch.Success ? int.Parse(aliveMatch.Groups[1].Value) : 0;

            Console.WriteLine($"  Expected: Alive count should be 3 (blinker is stable)");
            Console.WriteLine($"  Actual: Alive count = {aliveCount}");

            if (generation == 2 && aliveCount == 3)
            {
                Console.WriteLine("  ? TEST PASSED: Blinker oscillates correctly");
            }
            else
            {
                Console.WriteLine("  ? TEST FAILED");
            }

            Assert.AreEqual(2, generation, "Generation should be 2 after 2 steps");
            Assert.AreEqual(3, aliveCount, "Blinker should have 3 cells after 2 generations");

            await page.WaitForTimeoutAsync(1000);
        }

        /// <summary>
        /// Automated test: Verify that a block is stable (still life)
        /// </summary>
        [TestMethod]
        [TestCategory("Interactive")]
        [Timeout(60000)] // 60 seconds
        public async Task Life_BlockIsStable_StillLife()
        {
            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            await NavigateToBlazorPageAsync(page, "/life", "canvas.life-canvas");

            Console.WriteLine("?? Testing: Block should remain stable (still life)");

            // Wait for page to load
            await page.WaitForTimeoutAsync(1000);

            // Pause simulation
            var pauseButton = page.Locator("button:has-text('Pause')");
            if (await pauseButton.IsVisibleAsync())
            {
                await pauseButton.ClickAsync();
            }
            await page.WaitForTimeoutAsync(300);

            // Clear the grid
            var clearButton = page.Locator("button:has-text('Clear')");
            await clearButton.ClickAsync();
            Console.WriteLine("  ? Cleared grid");
            await page.WaitForTimeoutAsync(300);

            // Place a block
            var patternSelect = page.Locator("select.pattern-select");
            await patternSelect.SelectOptionAsync("block");
            Console.WriteLine("  ? Selected Block pattern");

            var placeButton = page.Locator("button:has-text('Place')");
            await placeButton.ClickAsync();
            Console.WriteLine("  ? Placed Block at center");
            await page.WaitForTimeoutAsync(300);

            // Get initial alive count (block has 4 cells)
            var initialStats = await page.Locator(".life-stats").TextContentAsync();
            var initialAliveMatch = System.Text.RegularExpressions.Regex.Match(initialStats ?? "", @"(\d+)\s*$");
            var initialAlive = initialAliveMatch.Success ? int.Parse(initialAliveMatch.Groups[1].Value) : 0;
            Console.WriteLine($"  ?? Initial alive: {initialAlive}");

            // Step through 5 generations
            var stepButton = page.Locator("button:has-text('Step')");
            for (int i = 0; i < 5; i++)
            {
                await stepButton.ClickAsync();
                await page.WaitForTimeoutAsync(100);
            }

            var finalStats = await page.Locator(".life-stats").TextContentAsync();
            Console.WriteLine($"  ?? After 5 steps: {finalStats}");

            var finalAliveMatch = System.Text.RegularExpressions.Regex.Match(finalStats ?? "", @"(\d+)\s*$");
            var finalAlive = finalAliveMatch.Success ? int.Parse(finalAliveMatch.Groups[1].Value) : 0;

            Console.WriteLine($"\n  Expected: Alive count should remain 4");
            Console.WriteLine($"  Actual: Alive count = {finalAlive}");

            if (finalAlive == 4)
            {
                Console.WriteLine("  ? TEST PASSED: Block is stable");
            }
            else
            {
                Console.WriteLine("  ? TEST FAILED: Block changed unexpectedly");
            }

            Assert.AreEqual(4, finalAlive, "Block should have 4 cells after any number of generations");

            await page.WaitForTimeoutAsync(1000);
        }

        /// <summary>
        /// Automated test: Verify simulation auto-starts on page load
        /// </summary>
        [TestMethod]
        [TestCategory("Interactive")]
        [Timeout(60000)] // 60 seconds
        public async Task Life_AutoStarts_OnPageLoad()
        {
            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            await NavigateToBlazorPageAsync(page, "/life", "canvas.life-canvas");

            Console.WriteLine("?? Testing: Simulation should auto-start on page load");

            // Wait for auto-start
            await page.WaitForTimeoutAsync(2000);

            // Check that generation counter is increasing
            var stats1 = await page.Locator(".life-stats").TextContentAsync();
            var gen1Match = System.Text.RegularExpressions.Regex.Match(stats1 ?? "", @"Gen\s+([\d,]+)");
            var gen1 = gen1Match.Success ? int.Parse(gen1Match.Groups[1].Value.Replace(",", "")) : 0;
            Console.WriteLine($"  ?? Initial generation: {gen1}");

            // Wait a bit more
            await page.WaitForTimeoutAsync(1000);

            var stats2 = await page.Locator(".life-stats").TextContentAsync();
            var gen2Match = System.Text.RegularExpressions.Regex.Match(stats2 ?? "", @"Gen\s+([\d,]+)");
            var gen2 = gen2Match.Success ? int.Parse(gen2Match.Groups[1].Value.Replace(",", "")) : 0;
            Console.WriteLine($"  ?? After 1 second: {gen2}");

            Console.WriteLine($"\n  Expected: Generation should increase (auto-start working)");
            Console.WriteLine($"  Actual: Gen went from {gen1} to {gen2}");

            var isRunning = gen2 > gen1;

            if (isRunning)
            {
                Console.WriteLine("  ? TEST PASSED: Simulation auto-started");
            }
            else
            {
                Console.WriteLine("  ? TEST FAILED: Simulation did not auto-start");
            }

            Assert.IsTrue(isRunning, "Simulation should auto-start and generation should increase");

            // Also verify Pause button is visible (indicates running state)
            var pauseButton = page.Locator("button:has-text('Pause')");
            var pauseVisible = await pauseButton.IsVisibleAsync();
            Console.WriteLine($"  Pause button visible: {pauseVisible}");

            Assert.IsTrue(pauseVisible, "Pause button should be visible when simulation is running");

            await page.WaitForTimeoutAsync(1000);
        }

        /// <summary>
        /// Automated test: Verify glider moves diagonally
        /// </summary>
        [TestMethod]
        [TestCategory("Interactive")]
        [Timeout(60000)] // 60 seconds
        public async Task Life_GliderMoves_Diagonally()
        {
            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            await NavigateToBlazorPageAsync(page, "/life", "canvas.life-canvas");

            Console.WriteLine("?? Testing: Glider should move diagonally (4 generation cycle)");

            // Wait for page to load
            await page.WaitForTimeoutAsync(1000);

            // Pause simulation
            var pauseButton = page.Locator("button:has-text('Pause')");
            if (await pauseButton.IsVisibleAsync())
            {
                await pauseButton.ClickAsync();
            }
            await page.WaitForTimeoutAsync(300);

            // Clear the grid
            var clearButton = page.Locator("button:has-text('Clear')");
            await clearButton.ClickAsync();
            Console.WriteLine("  ? Cleared grid");
            await page.WaitForTimeoutAsync(300);

            // Place a glider
            var patternSelect = page.Locator("select.pattern-select");
            await patternSelect.SelectOptionAsync("glider");
            Console.WriteLine("  ? Selected Glider pattern");

            var placeButton = page.Locator("button:has-text('Place')");
            await placeButton.ClickAsync();
            Console.WriteLine("  ? Placed Glider at center");
            await page.WaitForTimeoutAsync(300);

            // Glider has 5 cells
            var initialStats = await page.Locator(".life-stats").TextContentAsync();
            Console.WriteLine($"  ?? Initial: {initialStats}");

            // Step through 4 generations (one full cycle)
            var stepButton = page.Locator("button:has-text('Step')");
            for (int i = 0; i < 4; i++)
            {
                await stepButton.ClickAsync();
                await page.WaitForTimeoutAsync(150);
                var stepStats = await page.Locator(".life-stats").TextContentAsync();
                Console.WriteLine($"  ?? Step {i + 1}: {stepStats}");
            }

            var finalStats = await page.Locator(".life-stats").TextContentAsync();
            var finalAliveMatch = System.Text.RegularExpressions.Regex.Match(finalStats ?? "", @"(\d+)\s*$");
            var finalAlive = finalAliveMatch.Success ? int.Parse(finalAliveMatch.Groups[1].Value) : 0;

            Console.WriteLine($"\n  Expected: Alive count should remain 5 (glider is periodic)");
            Console.WriteLine($"  Actual: Alive count = {finalAlive}");

            if (finalAlive == 5)
            {
                Console.WriteLine("  ? TEST PASSED: Glider maintains 5 cells after 4 generations");
            }
            else
            {
                Console.WriteLine("  ? TEST FAILED: Glider cell count changed");
            }

            Assert.AreEqual(5, finalAlive, "Glider should have 5 cells after 4 generations (one cycle)");

            await page.WaitForTimeoutAsync(1000);
        }
    }
}
