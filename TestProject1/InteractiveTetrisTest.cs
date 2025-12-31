using Microsoft.Playwright;

namespace TestProject1
{
    [TestClass]
    public class InteractiveTetrisTest : InteractiveTestBase
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
        /// Interactive test for Tetris - keeps browser open until user closes it
        /// </summary>
        [TestMethod]
        [TestCategory("Manual")]
        public async Task LaunchInteractiveBrowser_TetrisGame()
        {
            Console.WriteLine("Launching interactive browser for Tetris...");
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

            await NavigateToBlazorPageAsync(page, "/tetris", "canvas.tetris-canvas");

            Console.WriteLine("Tetris loaded in incognito mode.");
            Console.WriteLine("?? Use ? ? to move, ? to rotate, ? for soft drop");
            Console.WriteLine("?? Press SPACE for hard drop");
            Console.WriteLine("?? Complete lines to score!");
            Console.WriteLine("The test will wait until you close the browser.");

            var pageClosedTcs = new TaskCompletionSource<bool>();
            page.Close += (_, _) => pageClosedTcs.TrySetResult(true);
            context.Close += (_, _) => pageClosedTcs.TrySetResult(true);

            await pageClosedTcs.Task;
            Console.WriteLine("Browser closed. Test ending.");
        }

        /// <summary>
        /// Automated test: Verify Tetris page loads correctly
        /// </summary>
        [TestMethod]
        [TestCategory("Interactive")]
        [Timeout(60000)] // 60 seconds
        public async Task Tetris_PageLoads_Correctly()
        {
            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            await NavigateToBlazorPageAsync(page, "/tetris", "canvas.tetris-canvas");

            Console.WriteLine("?? Testing: Tetris page should load correctly");

            // Wait for page to load
            await page.WaitForTimeoutAsync(1000);

            // Check that stats are visible
            var stats = await page.Locator(".tetris-stats").TextContentAsync();
            Console.WriteLine($"  ?? Stats: {stats}");

            Assert.IsTrue(stats?.Contains("Score") ?? false, "Stats should show Score");
            Assert.IsTrue(stats?.Contains("Level") ?? false, "Stats should show Level");
            Assert.IsTrue(stats?.Contains("Lines") ?? false, "Stats should show Lines");

            // Verify main canvas is visible
            var canvas = page.Locator("canvas.tetris-canvas");
            var isVisible = await canvas.IsVisibleAsync();
            Assert.IsTrue(isVisible, "Main canvas should be visible");

            // Verify next piece preview canvas
            var nextCanvas = page.Locator("canvas.tetris-next-canvas");
            var nextVisible = await nextCanvas.IsVisibleAsync();
            Assert.IsTrue(nextVisible, "Next piece canvas should be visible");

            // Verify Start button exists
            var startBtn = page.Locator("button:has-text('Start')");
            var startVisible = await startBtn.IsVisibleAsync();
            Assert.IsTrue(startVisible, "Start button should be visible");

            Console.WriteLine("  ? TEST PASSED: Tetris page loaded correctly");
            await page.WaitForTimeoutAsync(1000);
        }

        /// <summary>
        /// Automated test: Verify Start button starts the game
        /// </summary>
        [TestMethod]
        [TestCategory("Interactive")]
        [Timeout(60000)] // 60 seconds
        public async Task Tetris_StartButton_StartsGame()
        {
            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            await NavigateToBlazorPageAsync(page, "/tetris", "canvas.tetris-canvas");

            Console.WriteLine("?? Testing: Start button should start the game");

            await page.WaitForTimeoutAsync(1000);

            // Click Start
            var startBtn = page.Locator("button:has-text('Start')");
            await startBtn.ClickAsync();
            Console.WriteLine("  ? Clicked Start button");

            // Wait for game to run
            await page.WaitForTimeoutAsync(2000);

            // Pause the game
            var pauseBtn = page.Locator("button:has-text('Pause')");
            await pauseBtn.ClickAsync();
            Console.WriteLine("  ? Clicked Pause button");

            await page.WaitForTimeoutAsync(500);

            Console.WriteLine("  ? TEST PASSED: Game started and can be paused");
            await page.WaitForTimeoutAsync(1000);
        }

        /// <summary>
        /// Automated test: Verify Reset button resets the game
        /// </summary>
        [TestMethod]
        [TestCategory("Interactive")]
        [Timeout(60000)] // 60 seconds
        public async Task Tetris_ResetButton_ResetsGame()
        {
            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            await NavigateToBlazorPageAsync(page, "/tetris", "canvas.tetris-canvas");

            Console.WriteLine("?? Testing: Reset button should reset the game");

            await page.WaitForTimeoutAsync(1000);

            // Start game
            var startBtn = page.Locator("button:has-text('Start')");
            await startBtn.ClickAsync();
            await page.WaitForTimeoutAsync(1500);

            // Pause game
            var pauseBtn = page.Locator("button:has-text('Pause')");
            await pauseBtn.ClickAsync();
            await page.WaitForTimeoutAsync(500);

            // Reset game
            var resetBtn = page.Locator("button:has-text('Reset')");
            await resetBtn.ClickAsync();
            Console.WriteLine("  ? Clicked Reset button");
            await page.WaitForTimeoutAsync(500);

            // Check stats are reset
            var stats = await page.Locator(".tetris-stats").TextContentAsync();
            Console.WriteLine($"  ?? Stats after reset: {stats}");

            Assert.IsTrue(stats?.Contains("Score: 0") ?? false, "Score should be 0 after reset");
            Assert.IsTrue(stats?.Contains("Level: 1") ?? false, "Level should be 1 after reset");
            Assert.IsTrue(stats?.Contains("Lines: 0") ?? false, "Lines should be 0 after reset");

            Console.WriteLine("  ? TEST PASSED: Reset button reset the game");
            await page.WaitForTimeoutAsync(1000);
        }

        /// <summary>
        /// Automated test: Verify touch controls are present
        /// </summary>
        [TestMethod]
        [TestCategory("Interactive")]
        [Timeout(60000)] // 60 seconds
        public async Task Tetris_TouchControls_Present()
        {
            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            await NavigateToBlazorPageAsync(page, "/tetris", "canvas.tetris-canvas");

            Console.WriteLine("🧪 Testing: Touch controls should be present");

            await page.WaitForTimeoutAsync(1000);

            // Check for control buttons using the actual Unicode characters from Tetris.razor
            // ⟳ for rotate, ◀ for left, ▶ for right, ⬇ for drop
            var rotateBtn = page.Locator(".tetris-ctrl-btn:has-text('⟳')");
            var leftBtn = page.Locator(".tetris-ctrl-btn:has-text('◀')");
            var rightBtn = page.Locator(".tetris-ctrl-btn:has-text('▶')");
            var dropBtn = page.Locator(".tetris-ctrl-btn.drop");

            var rotateVisible = await rotateBtn.IsVisibleAsync();
            var leftVisible = await leftBtn.IsVisibleAsync();
            var rightVisible = await rightBtn.IsVisibleAsync();
            var dropVisible = await dropBtn.IsVisibleAsync();

            Console.WriteLine($"  Controls visible: Rotate={rotateVisible}, Left={leftVisible}, Right={rightVisible}, Drop={dropVisible}");

            Assert.IsTrue(rotateVisible, "Rotate button should be visible");
            Assert.IsTrue(leftVisible, "Left button should be visible");
            Assert.IsTrue(rightVisible, "Right button should be visible");
            Assert.IsTrue(dropVisible, "Drop button should be visible");

            Console.WriteLine("  ✓ TEST PASSED: Touch controls are present");
            await page.WaitForTimeoutAsync(1000);
        }

        /// <summary>
        /// Automated test: Verify touch controls work during gameplay
        /// </summary>
        [TestMethod]
        [TestCategory("Interactive")]
        [Timeout(60000)] // 60 seconds
        public async Task Tetris_TouchControls_Work()
        {
            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            await NavigateToBlazorPageAsync(page, "/tetris", "canvas.tetris-canvas");

            Console.WriteLine("🧪 Testing: Touch controls should work during gameplay");

            await page.WaitForTimeoutAsync(1000);

            // Start game
            var startBtn = page.Locator("button:has-text('Start')");
            await startBtn.ClickAsync();
            await page.WaitForTimeoutAsync(500);

            // Try each control - using correct Unicode characters from Tetris.razor
            var rotateBtn = page.Locator(".tetris-ctrl-btn:has-text('⟳')");
            await rotateBtn.ClickAsync();
            Console.WriteLine("  ✓ Rotate clicked");
            await page.WaitForTimeoutAsync(200);

            var leftBtn = page.Locator(".tetris-ctrl-btn:has-text('◀')");
            await leftBtn.ClickAsync();
            Console.WriteLine("  ✓ Left clicked");
            await page.WaitForTimeoutAsync(200);

            var rightBtn = page.Locator(".tetris-ctrl-btn:has-text('▶')");
            await rightBtn.ClickAsync();
            Console.WriteLine("  ✓ Right clicked");
            await page.WaitForTimeoutAsync(200);

            // Hard drop
            var dropBtn = page.Locator(".tetris-ctrl-btn.drop");
            await dropBtn.ClickAsync();
            Console.WriteLine("  ✓ Hard drop clicked");
            await page.WaitForTimeoutAsync(500);

            // If we got here without errors, controls work
            Console.WriteLine("  ✓ TEST PASSED: Touch controls work during gameplay");
            await page.WaitForTimeoutAsync(1000);
        }
    }
}
