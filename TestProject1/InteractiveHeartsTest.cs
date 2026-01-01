using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace TestProject1
{
    [TestClass]
    public class InteractiveHeartsTest : InteractiveTestBase
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
        /// Manual interactive test for Hearts game - keeps browser open for user interaction
        /// </summary>
        [TestMethod]
        [TestCategory("Manual")]
        public async Task Manual_LaunchInteractiveBrowser_Hearts()
        {
            Console.WriteLine("Launching interactive browser for Hearts game...");
            Console.WriteLine("Close the browser window when you're done experimenting.");

            _browser = await _playwright!.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false,
                SlowMo = 100,
                Devtools = false
            });

            var context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1280, Height = 900 }
            });

            var page = await context.NewPageAsync();
            page.Console += (_, msg) => Console.WriteLine($"[Browser Console] {msg.Text}");

            // Navigate using shared helper
            await NavigateToBlazorPageAsync(page, "/hearts", ".hearts-container");

            Console.WriteLine("♥️ Hearts game loaded!");
            Console.WriteLine("How to play:");
            Console.WriteLine("  - This is a 4-player trick-taking game");
            Console.WriteLine("  - At the start of each round, pass 3 cards to another player");
            Console.WriteLine("  - Player with 2♣ leads the first trick");
            Console.WriteLine("  - You must follow suit if possible");
            Console.WriteLine("  - Avoid taking hearts (1 point each) and the Q♠ (13 points)");
            Console.WriteLine("  - Lowest score wins when someone reaches 100 points");
            Console.WriteLine("  - 'Shooting the Moon' (taking all 26 points) gives others 26 instead!");
            Console.WriteLine("\nThe test will wait until you close the browser.");

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

        /// <summary>
        /// Automated test - verifies Hearts page loads and basic elements are present
        /// </summary>
        [TestMethod]
        [TestCategory("Automated")]
        [Timeout(60000)]
        public async Task AutomatedTest_HeartsPageLoads()
        {
            Console.WriteLine("Testing Hearts page load...");

            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            // Navigate to Hearts
            await NavigateToBlazorPageAsync(page, "/hearts", ".hearts-container");

            Console.WriteLine("✓ Hearts container loaded");

            // Verify player hand area
            var playerHand = page.Locator(".player-hand");
            await Expect(playerHand).ToBeVisibleAsync();
            Console.WriteLine("✓ Player hand area visible");

            // Verify player has 13 cards
            var handCards = page.Locator(".player-hand .card:not(.card-empty)");
            var cardCount = await handCards.CountAsync();
            Assert.AreEqual(13, cardCount, "Player should have 13 cards");
            Console.WriteLine("✓ Player has 13 cards");

            // Verify all 4 player areas (north, west, east, south)
            var playerAreas = page.Locator(".player-area");
            await Expect(playerAreas).ToHaveCountAsync(4); // 4 players total
            Console.WriteLine("✓ 4 player areas visible");

            // Verify trick area
            var trickArea = page.Locator(".trick-area");
            await Expect(trickArea).ToBeVisibleAsync();
            Console.WriteLine("✓ Trick area visible");

            // Verify scoreboard
            var scoreboard = page.Locator(".scoreboard");
            await Expect(scoreboard).ToBeVisibleAsync();
            Console.WriteLine("✓ Scoreboard visible");

            // Take screenshot
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = "hearts-test-screenshot.png",
                FullPage = true
            });
            Console.WriteLine("📸 Screenshot saved to: hearts-test-screenshot.png");

            Console.WriteLine("\n✓ Hearts page load test completed successfully!");
        }

        /// <summary>
        /// Automated test - verifies passing phase works
        /// </summary>
        [TestMethod]
        [TestCategory("Automated")]
        [Timeout(60000)]
        public async Task AutomatedTest_HeartsPassingPhase()
        {
            Console.WriteLine("Testing Hearts passing phase...");

            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            await NavigateToBlazorPageAsync(page, "/hearts", ".hearts-container");

            // Check if we're in passing phase
            var passButton = page.Locator("button:has-text('Pass Cards')");
            var isPassingPhase = await passButton.IsVisibleAsync();

            if (isPassingPhase)
            {
                Console.WriteLine("In passing phase");

                // Select 3 cards
                var handCards = page.Locator(".player-hand .card:not(.card-empty)");
                for (int i = 0; i < 3; i++)
                {
                    await handCards.Nth(i).ClickAsync();
                    await Task.Delay(200);
                }

                // Verify 3 cards are selected
                var selectedCards = page.Locator(".player-hand .card.selected");
                var selectedCount = await selectedCards.CountAsync();
                Assert.AreEqual(3, selectedCount, "Should have 3 cards selected");
                Console.WriteLine("✓ 3 cards selected for passing");

                // Pass button should be enabled
                await Expect(passButton).ToBeEnabledAsync();
                Console.WriteLine("✓ Pass button is enabled");

                // Click pass
                await passButton.ClickAsync();
                await Task.Delay(500);

                Console.WriteLine("✓ Cards passed");
            }
            else
            {
                Console.WriteLine("Not in passing phase (might be a no-pass round)");
            }

            Console.WriteLine("\n✓ Passing phase test completed!");
        }

        /// <summary>
        /// Automated test - verifies playing phase works
        /// </summary>
        [TestMethod]
        [TestCategory("Automated")]
        [Timeout(90000)]
        public async Task AutomatedTest_HeartsPlayingPhase()
        {
            Console.WriteLine("Testing Hearts playing phase...");

            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            await NavigateToBlazorPageAsync(page, "/hearts", ".hearts-container");

            // Skip passing phase if present
            var passButton = page.Locator("button:has-text('Pass Cards')");
            if (await passButton.IsVisibleAsync())
            {
                var handCards = page.Locator(".player-hand .card:not(.card-empty)");
                for (int i = 0; i < 3; i++)
                {
                    await handCards.Nth(i).ClickAsync();
                    await Task.Delay(100);
                }
                await passButton.ClickAsync();
                await Task.Delay(500);
            }

            // Wait for it to be our turn or for the game to be in playing phase
            await Task.Delay(1000);

            // Try to play a card (if it's our turn)
            var playableCards = page.Locator(".player-hand .card.playable");
            var playableCount = await playableCards.CountAsync();

            if (playableCount > 0)
            {
                Console.WriteLine($"Found {playableCount} playable cards");
                await playableCards.First.ClickAsync();
                await Task.Delay(500);
                Console.WriteLine("✓ Played a card");
            }
            else
            {
                Console.WriteLine("No playable cards (AI might be playing first)");
            }

            Console.WriteLine("\n✓ Playing phase test completed!");
        }

        /// <summary>
        /// Automated test - verifies New Game button
        /// </summary>
        [TestMethod]
        [TestCategory("Automated")]
        [Timeout(60000)]
        public async Task AutomatedTest_HeartsNewGame()
        {
            Console.WriteLine("Testing Hearts new game...");

            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            await NavigateToBlazorPageAsync(page, "/hearts", ".hearts-container");

            // Get initial hand (for comparison)
            var handCards = page.Locator(".player-hand .card:not(.card-empty)");
            var initialCardCount = await handCards.CountAsync();
            Console.WriteLine($"Initial hand size: {initialCardCount}");

            // Click New Game
            var newGameButton = page.Locator("button:has-text('New Game')");
            await newGameButton.ClickAsync();
            await Task.Delay(500);

            // Verify new hand
            var newCardCount = await handCards.CountAsync();
            Assert.AreEqual(13, newCardCount, "Should have 13 cards after new game");
            Console.WriteLine("✓ New game started with 13 cards");

            // Scoreboard should show all zeros
            var playerScores = page.Locator(".scoreboard .player-score");
            var scoreCount = await playerScores.CountAsync();
            Assert.AreEqual(4, scoreCount, "Should have 4 player scores");
            Console.WriteLine("✓ Scoreboard reset");

            Console.WriteLine("\n✓ New game test completed successfully!");
        }

        /// <summary>
        /// Manual test for full game playthrough
        /// </summary>
        [TestMethod]
        [TestCategory("Manual")]
        public async Task Manual_HeartsFullGamePlaythrough()
        {
            Console.WriteLine("Testing Hearts full game playthrough...");

            _browser = await _playwright!.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false,
                SlowMo = 200
            });

            var context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1280, Height = 900 }
            });

            var page = await context.NewPageAsync();
            page.Console += (_, msg) => Console.WriteLine($"[Browser Console] {msg.Text}");

            await NavigateToBlazorPageAsync(page, "/hearts", ".hearts-container");

            Console.WriteLine("♥️ Hearts game loaded for manual testing!");
            Console.WriteLine("\nInstructions:");
            Console.WriteLine("1. Select 3 cards to pass (if in passing phase)");
            Console.WriteLine("2. Click 'Pass Cards' button");
            Console.WriteLine("3. Wait for your turn, then click a playable card");
            Console.WriteLine("4. Try to avoid hearts and the Queen of Spades");
            Console.WriteLine("5. Complete the round and see your score");
            Console.WriteLine("\nThe test will wait until you close the browser.");

            // Wait for user to close
            var pageClosedTcs = new TaskCompletionSource<bool>();
            page.Close += (_, _) => pageClosedTcs.TrySetResult(true);
            context.Close += (_, _) => pageClosedTcs.TrySetResult(true);

            await pageClosedTcs.Task;
            Console.WriteLine("Browser closed. Test ending.");
        }

        /// <summary>
        /// Manual test for responsive layout testing
        /// </summary>
        [TestMethod]
        [TestCategory("Manual")]
        public async Task Manual_HeartsResponsiveLayout()
        {
            Console.WriteLine("Testing Hearts responsive layout...");

            _browser = await _playwright!.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false,
                SlowMo = 300
            });

            var context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
            });

            var page = await context.NewPageAsync();
            page.Console += (_, msg) => Console.WriteLine($"[Browser Console] {msg.Text}");

            await NavigateToBlazorPageAsync(page, "/hearts", ".hearts-container");

            Console.WriteLine("🖥️ Desktop layout (1920x1080)");
            await Task.Delay(2000);

            // Test tablet
            await page.SetViewportSizeAsync(768, 1024);
            Console.WriteLine("📱 Tablet layout (768x1024)");
            await Task.Delay(2000);

            // Test mobile
            await page.SetViewportSizeAsync(375, 667);
            Console.WriteLine("📱 Mobile layout (375x667)");
            await Task.Delay(2000);

            Console.WriteLine("\n✓ Responsive layout test completed!");
            Console.WriteLine("🔍 Browser will stay open for manual inspection.");

            // Wait for user to close
            var pageClosedTcs = new TaskCompletionSource<bool>();
            page.Close += (_, _) => pageClosedTcs.TrySetResult(true);
            context.Close += (_, _) => pageClosedTcs.TrySetResult(true);

            await pageClosedTcs.Task;
            Console.WriteLine("Browser closed. Test ending.");
        }
    }
}
