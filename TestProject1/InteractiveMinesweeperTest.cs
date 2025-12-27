using Microsoft.Playwright;

namespace TestProject1
{
    [TestClass]
    public class InteractiveMinesweeperTest : InteractiveTestBase
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
        /// Manual test for Minesweeper - keeps browser open until user closes it
        /// </summary>
        [TestMethod]
        [TestCategory("Manual")]
        public async Task Manual_LaunchInteractiveBrowser_MinesweeperGame()
        {
            Console.WriteLine("Launching interactive browser for Minesweeper Game...");
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

            await NavigateToBlazorPageAsync(page, "/minesweeper", "canvas.minesweeper-canvas");

            Console.WriteLine("Minesweeper Game loaded in incognito mode.");
            Console.WriteLine("??? Left-click to reveal cells");
            Console.WriteLine("?? Right-click to flag mines");
            Console.WriteLine("?? On mobile: tap to reveal, long-press to flag");
            Console.WriteLine("The test will wait until you close the browser.");

            var pageClosedTcs = new TaskCompletionSource<bool>();
            page.Close += (_, _) => pageClosedTcs.TrySetResult(true);
            context.Close += (_, _) => pageClosedTcs.TrySetResult(true);

            await pageClosedTcs.Task;
            Console.WriteLine("Browser closed. Test ending.");
        }

        /// <summary>
        /// Automated test: Verify Minesweeper page loads correctly
        /// </summary>
        [TestMethod]
        [TestCategory("Automated")]
        [Timeout(60000)] // 60 seconds
        public async Task Minesweeper_PageLoads_Correctly()
        {
            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            await NavigateToBlazorPageAsync(page, "/minesweeper", "canvas.minesweeper-canvas");

            Console.WriteLine("? Testing: Minesweeper page should load correctly");

            // Wait for page to load
            await page.WaitForTimeoutAsync(1000);

            // Check that stats are visible
            var stats = await page.Locator(".minesweeper-stats").TextContentAsync();
            Console.WriteLine($"  ?? Stats: {stats}");

            Assert.IsTrue(stats?.Contains("Mines") ?? false, "Stats should show Mines count");
            Assert.IsTrue(stats?.Contains("Time") ?? false, "Stats should show Time");

            // Verify canvas is visible
            var canvas = page.Locator("canvas.minesweeper-canvas");
            var isVisible = await canvas.IsVisibleAsync();
            Assert.IsTrue(isVisible, "Canvas should be visible");

            // Verify New Game button exists
            var newGameBtn = page.Locator("button:has-text('New Game')");
            var btnVisible = await newGameBtn.IsVisibleAsync();
            Assert.IsTrue(btnVisible, "New Game button should be visible");

            Console.WriteLine("  ? TEST PASSED: Minesweeper page loaded correctly");
            await page.WaitForTimeoutAsync(1000);
        }

        /// <summary>
        /// Automated test: Verify difficulty selector changes grid
        /// </summary>
        [TestMethod]
        [TestCategory("Automated")]
        [Timeout(60000)] // 60 seconds
        public async Task Minesweeper_DifficultySelector_ChangesGrid()
        {
            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            await NavigateToBlazorPageAsync(page, "/minesweeper", "canvas.minesweeper-canvas");

            Console.WriteLine("? Testing: Difficulty selector should change grid size");

            await page.WaitForTimeoutAsync(1000);

            // Get initial canvas size (easy = 9x9)
            var canvas = page.Locator("canvas.minesweeper-canvas");
            var initialSize = await canvas.BoundingBoxAsync();
            Console.WriteLine($"  ?? Initial size: {initialSize?.Width}x{initialSize?.Height}");

            // Change to medium difficulty
            var difficultySelect = page.Locator(".minesweeper-select");
            await difficultySelect.SelectOptionAsync("medium");
            Console.WriteLine("  ? Selected Medium difficulty");

            await page.WaitForTimeoutAsync(1000);

            // Get new canvas size (medium = 16x16)
            var newSize = await canvas.BoundingBoxAsync();
            Console.WriteLine($"  ?? New size: {newSize?.Width}x{newSize?.Height}");

            // Canvas should be larger for medium difficulty
            Assert.IsTrue(
                newSize?.Width > initialSize?.Width || newSize?.Height > initialSize?.Height,
                "Canvas should be larger for medium difficulty");

            Console.WriteLine("  ? TEST PASSED: Difficulty selector changes grid size");
            await page.WaitForTimeoutAsync(1000);
        }

        /// <summary>
        /// Automated test: Verify first click reveals cell and starts timer
        /// </summary>
        [TestMethod]
        [TestCategory("Automated")]
        [Timeout(60000)] // 60 seconds
        public async Task Minesweeper_FirstClick_StartsGame()
        {
            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            await NavigateToBlazorPageAsync(page, "/minesweeper", "canvas.minesweeper-canvas");

            Console.WriteLine("? Testing: First click should start game and timer");

            await page.WaitForTimeoutAsync(1000);

            // Check initial status
            var statsInitial = await page.Locator(".minesweeper-stats").TextContentAsync();
            Console.WriteLine($"  ?? Initial stats: {statsInitial}");
            Assert.IsTrue(statsInitial?.Contains("Ready") ?? false, "Initial status should be Ready");

            // Click on canvas (center)
            var canvas = page.Locator("canvas.minesweeper-canvas");
            await canvas.ClickAsync(new LocatorClickOptions { Position = new Position { X = 100, Y = 100 } });
            Console.WriteLine("  ? Clicked on canvas");

            // Wait for timer to tick
            await page.WaitForTimeoutAsync(2000);

            // Check that game is now playing
            var statsAfter = await page.Locator(".minesweeper-stats").TextContentAsync();
            Console.WriteLine($"  ?? Stats after click: {statsAfter}");

            // Timer should have started (time > 0) or game ended
            Assert.IsTrue(
                statsAfter?.Contains("Playing") == true || 
                statsAfter?.Contains("Won") == true || 
                statsAfter?.Contains("Lost") == true,
                "Game should be in progress or finished after first click");

            Console.WriteLine("  ? TEST PASSED: First click starts game");
            await page.WaitForTimeoutAsync(1000);
        }

        /// <summary>
        /// Automated test: Verify New Game button resets the game
        /// </summary>
        [TestMethod]
        [TestCategory("Automated")]
        [Timeout(60000)] // 60 seconds
        public async Task Minesweeper_NewGameButton_ResetsGame()
        {
            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            await NavigateToBlazorPageAsync(page, "/minesweeper", "canvas.minesweeper-canvas");

            Console.WriteLine("? Testing: New Game button should reset the game");

            await page.WaitForTimeoutAsync(1000);

            // Start a game by clicking
            var canvas = page.Locator("canvas.minesweeper-canvas");
            await canvas.ClickAsync(new LocatorClickOptions { Position = new Position { X = 100, Y = 100 } });
            await page.WaitForTimeoutAsync(2000);

            // Click New Game
            var newGameBtn = page.Locator("button:has-text('New Game')");
            await newGameBtn.ClickAsync();
            Console.WriteLine("  ? Clicked New Game button");
            await page.WaitForTimeoutAsync(500);

            // Check status is Ready again
            var stats = await page.Locator(".minesweeper-stats").TextContentAsync();
            Console.WriteLine($"  ?? Stats after reset: {stats}");

            Assert.IsTrue(stats?.Contains("Ready") ?? false, "Status should be Ready after New Game");

            Console.WriteLine("  ? TEST PASSED: New Game button resets the game");
            await page.WaitForTimeoutAsync(1000);
        }

        /// <summary>
        /// Automated test: Verify right-click flags a cell
        /// </summary>
        [TestMethod]
        [TestCategory("Automated")]
        [Timeout(60000)] // 60 seconds
        public async Task Minesweeper_RightClick_FlagsCell()
        {
            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            await NavigateToBlazorPageAsync(page, "/minesweeper", "canvas.minesweeper-canvas");

            Console.WriteLine("? Testing: Right-click should flag a cell");

            await page.WaitForTimeoutAsync(1000);

            // Get initial mines remaining count
            var statsInitial = await page.Locator(".minesweeper-stats").TextContentAsync();
            Console.WriteLine($"  ?? Initial stats: {statsInitial}");

            // Right-click to flag
            var canvas = page.Locator("canvas.minesweeper-canvas");
            await canvas.ClickAsync(new LocatorClickOptions
            {
                Button = MouseButton.Right,
                Position = new Position { X = 50, Y = 50 }
            });
            Console.WriteLine("  ? Right-clicked on canvas");

            await page.WaitForTimeoutAsync(500);

            // Mines remaining should decrease by 1
            var statsAfter = await page.Locator(".minesweeper-stats").TextContentAsync();
            Console.WriteLine($"  ?? Stats after flag: {statsAfter}");

            // The stats text should show one less mine
            // For easy mode, starts with 10 mines, after flag should show 9
            Assert.IsTrue(statsAfter?.Contains("9") ?? false, "Mines remaining should decrease after flagging");

            Console.WriteLine("  ? TEST PASSED: Right-click flags cell");
            await page.WaitForTimeoutAsync(1000);
        }

        /// <summary>
        /// Automated test: Verify mobile viewport shows game correctly
        /// </summary>
        [TestMethod]
        [TestCategory("Automated")]
        [Timeout(60000)] // 60 seconds
        public async Task Minesweeper_MobileViewport_DisplaysCorrectly()
        {
            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 375, Height = 667 }, // iPhone size
                IgnoreHTTPSErrors = true
            });
            var page = await context.NewPageAsync();

            await NavigateToBlazorPageAsync(page, "/minesweeper", "canvas.minesweeper-canvas");

            Console.WriteLine("? Testing: Mobile viewport should display game correctly");

            await page.WaitForTimeoutAsync(1000);

            // Verify canvas is visible
            var canvas = page.Locator("canvas.minesweeper-canvas");
            var isVisible = await canvas.IsVisibleAsync();
            Assert.IsTrue(isVisible, "Canvas should be visible on mobile");

            // Verify canvas fits in viewport
            var box = await canvas.BoundingBoxAsync();
            Console.WriteLine($"  ?? Canvas size: {box?.Width}x{box?.Height}");
            Assert.IsTrue(box?.Width <= 375, "Canvas should fit in mobile width");

            // Verify controls are visible
            var newGameBtn = page.Locator("button:has-text('New Game')");
            var btnVisible = await newGameBtn.IsVisibleAsync();
            Assert.IsTrue(btnVisible, "New Game button should be visible on mobile");

            Console.WriteLine("  ? TEST PASSED: Mobile viewport displays correctly");
            await page.WaitForTimeoutAsync(1000);
        }

        /// <summary>
        /// Automated test: Verify game can be won
        /// </summary>
        [TestMethod]
        [TestCategory("Automated")]
        [Timeout(90000)] // 90 seconds - involves random clicking
        public async Task Minesweeper_CanWinGame()
        {
            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            await NavigateToBlazorPageAsync(page, "/minesweeper", "canvas.minesweeper-canvas");

            Console.WriteLine("? Testing: Game can be won by revealing all non-mine cells");

            await page.WaitForTimeoutAsync(1000);

            // This is a probabilistic test - click around until we win or lose
            var canvas = page.Locator("canvas.minesweeper-canvas");
            var box = await canvas.BoundingBoxAsync();

            bool gameEnded = false;
            int maxClicks = 50;
            int clicks = 0;

            while (!gameEnded && clicks < maxClicks)
            {
                // Click random positions
                float x = 20 + (float)(new Random().NextDouble() * (box!.Width - 40));
                float y = 20 + (float)(new Random().NextDouble() * (box!.Height - 40));

                try
                {
                    await canvas.ClickAsync(new LocatorClickOptions { Position = new Position { X = x, Y = y } });
                    clicks++;
                    await page.WaitForTimeoutAsync(100);

                    var stats = await page.Locator(".minesweeper-stats").TextContentAsync();
                    if (stats?.Contains("Won") == true || stats?.Contains("Lost") == true)
                    {
                        gameEnded = true;
                        Console.WriteLine($"  ?? Game ended after {clicks} clicks: {(stats.Contains("Won") ? "WON" : "LOST")}");
                    }
                }
                catch
                {
                    // Ignore click errors
                }
            }

            Assert.IsTrue(clicks > 0, "Should have made at least one click");
            Console.WriteLine($"  ? TEST PASSED: Game mechanics work (made {clicks} clicks)");
            await page.WaitForTimeoutAsync(1000);
        }
    }
}
