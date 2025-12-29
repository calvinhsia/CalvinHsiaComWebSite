using Microsoft.Playwright;

namespace TestProject1
{
    [TestClass]
    public class InteractiveSnakeTest : InteractiveTestBase
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
        /// Interactive test for Snake - keeps browser open until user closes it
        /// </summary>
        [TestMethod]
        [TestCategory("Manual")]
        public async Task LaunchInteractiveBrowser_SnakeGame()
        {
            Console.WriteLine("Launching interactive browser for Snake Game...");
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

            await NavigateToBlazorPageAsync(page, "/snake", "canvas.snake-canvas");

            Console.WriteLine("Snake Game loaded in incognito mode.");
            Console.WriteLine("?? Use arrow keys or WASD to control the snake");
            Console.WriteLine("?? Eat the red food to grow");
            Console.WriteLine("?? Don't hit walls or yourself!");
            Console.WriteLine("The test will wait until you close the browser.");

            var pageClosedTcs = new TaskCompletionSource<bool>();
            page.Close += (_, _) => pageClosedTcs.TrySetResult(true);
            context.Close += (_, _) => pageClosedTcs.TrySetResult(true);

            await pageClosedTcs.Task;
            Console.WriteLine("Browser closed. Test ending.");
        }

        /// <summary>
        /// Automated test: Verify Snake page loads correctly
        /// </summary>
        [TestMethod]
        [TestCategory("Interactive")]
        public async Task Snake_PageLoads_Correctly()
        {
            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            await NavigateToBlazorPageAsync(page, "/snake", "canvas.snake-canvas");

            Console.WriteLine("?? Testing: Snake page should load correctly");

            // Wait for page to load
            await page.WaitForTimeoutAsync(1000);

            // Check that stats are visible
            var stats = await page.Locator(".snake-stats").TextContentAsync();
            Console.WriteLine($"  ?? Stats: {stats}");

            Assert.IsTrue(stats?.Contains("Score") ?? false, "Stats should show Score");
            Assert.IsTrue(stats?.Contains("High Score") ?? false, "Stats should show High Score");

            // Verify canvas is visible
            var canvas = page.Locator("canvas.snake-canvas");
            var isVisible = await canvas.IsVisibleAsync();
            Assert.IsTrue(isVisible, "Canvas should be visible");

            // Verify Start button exists
            var startBtn = page.Locator("button:has-text('Start')");
            var startVisible = await startBtn.IsVisibleAsync();
            Assert.IsTrue(startVisible, "Start button should be visible");

            Console.WriteLine("  ? TEST PASSED: Snake page loaded correctly");
            await page.WaitForTimeoutAsync(1000);
        }

        /// <summary>
        /// Automated test: Verify Start button starts the game
        /// </summary>
        [TestMethod]
        [TestCategory("Interactive")]
        public async Task Snake_StartButton_StartsGame()
        {
            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            await NavigateToBlazorPageAsync(page, "/snake", "canvas.snake-canvas");

            Console.WriteLine("?? Testing: Start button should start the game");

            await page.WaitForTimeoutAsync(1000);

            // Click Start
            var startBtn = page.Locator("button:has-text('Start')");
            await startBtn.ClickAsync();
            Console.WriteLine("  ? Clicked Start button");

            // Wait for game to run
            await page.WaitForTimeoutAsync(2000);

            // The snake should have moved (score might still be 0, but game is running)
            // We can verify by checking that Pause button is responsive
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
        public async Task Snake_ResetButton_ResetsGame()
        {
            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            await NavigateToBlazorPageAsync(page, "/snake", "canvas.snake-canvas");

            Console.WriteLine("?? Testing: Reset button should reset the game");

            await page.WaitForTimeoutAsync(1000);

            // Start game
            var startBtn = page.Locator("button:has-text('Start')");
            await startBtn.ClickAsync();
            await page.WaitForTimeoutAsync(1000);

            // Stop game
            var pauseBtn = page.Locator("button:has-text('Pause')");
            await pauseBtn.ClickAsync();
            await page.WaitForTimeoutAsync(500);

            // Reset game
            var resetBtn = page.Locator("button:has-text('Reset')");
            await resetBtn.ClickAsync();
            Console.WriteLine("  ? Clicked Reset button");
            await page.WaitForTimeoutAsync(500);

            // Check score is 0
            var stats = await page.Locator(".snake-stats").TextContentAsync();
            Console.WriteLine($"  ?? Stats after reset: {stats}");

            Assert.IsTrue(stats?.Contains("Score: 0") ?? false, "Score should be 0 after reset");

            Console.WriteLine("  ? TEST PASSED: Reset button reset the game");
            await page.WaitForTimeoutAsync(1000);
        }

        /// <summary>
        /// Automated test: Verify mobile controls are present
        /// </summary>
        [TestMethod]
        [TestCategory("Interactive")]
        public async Task Snake_MobileControls_Present()
        {
            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 375, Height = 667 } // Mobile viewport
            });
            var page = await context.NewPageAsync();

            await NavigateToBlazorPageAsync(page, "/snake", "canvas.snake-canvas");

            Console.WriteLine("?? Testing: Mobile controls should be present on mobile viewport");

            await page.WaitForTimeoutAsync(1000);

            // Check for D-pad buttons
            var upBtn = page.Locator(".snake-dpad-btn.up");
            var downBtn = page.Locator(".snake-dpad-btn.down");
            var leftBtn = page.Locator(".snake-dpad-btn.left");
            var rightBtn = page.Locator(".snake-dpad-btn.right");

            var upVisible = await upBtn.IsVisibleAsync();
            var downVisible = await downBtn.IsVisibleAsync();
            var leftVisible = await leftBtn.IsVisibleAsync();
            var rightVisible = await rightBtn.IsVisibleAsync();

            Console.WriteLine($"  D-pad visible: Up={upVisible}, Down={downVisible}, Left={leftVisible}, Right={rightVisible}");

            Assert.IsTrue(upVisible, "Up button should be visible");
            Assert.IsTrue(downVisible, "Down button should be visible");
            Assert.IsTrue(leftVisible, "Left button should be visible");
            Assert.IsTrue(rightVisible, "Right button should be visible");

            Console.WriteLine("  ? TEST PASSED: Mobile controls are present");
            await page.WaitForTimeoutAsync(1000);
        }
    }
}
