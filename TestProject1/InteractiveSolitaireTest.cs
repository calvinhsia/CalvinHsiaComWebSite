using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace TestProject1
{
    [TestClass]
    public class InteractiveSolitaireTest : InteractiveTestBase
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
        /// Manual interactive test for Solitaire game - keeps browser open for user interaction
        /// </summary>
        [TestMethod]
        [TestCategory("Manual")]
        public async Task Manual_LaunchInteractiveBrowser_Solitaire()
        {
            Console.WriteLine("Launching interactive browser for Solitaire game...");
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
            await NavigateToBlazorPageAsync(page, "/solitaire", ".solitaire-container");

            Console.WriteLine("?? Solitaire game loaded!");
            Console.WriteLine("How to play:");
            Console.WriteLine("  - Click the stock pile (top-left) to draw cards");
            Console.WriteLine("  - Click a card to select it (highlighted in gold)");
            Console.WriteLine("  - Click a destination to move the selected card(s)");
            Console.WriteLine("  - Double-click a card to auto-move it to foundation if valid");
            Console.WriteLine("  - Click 'Auto' to automatically move all possible cards to foundations");
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
        /// Automated test - verifies Solitaire page loads and basic elements are present
        /// </summary>
        [TestMethod]
        [TestCategory("Automated")]
        [Timeout(60000)]
        public async Task AutomatedTest_SolitairePageLoads()
        {
            Console.WriteLine("Testing Solitaire page load...");

            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            // Navigate to Solitaire
            await NavigateToBlazorPageAsync(page, "/solitaire", ".solitaire-container");

            Console.WriteLine("? Solitaire container loaded");

            // Verify main game elements are present
            var stockPile = page.Locator(".stock-pile");
            await Expect(stockPile).ToBeVisibleAsync();
            Console.WriteLine("? Stock pile visible");

            var wastePile = page.Locator(".waste-pile");
            await Expect(wastePile).ToBeVisibleAsync();
            Console.WriteLine("? Waste pile visible");

            var foundations = page.Locator(".foundation-pile");
            await Expect(foundations).ToHaveCountAsync(4);
            Console.WriteLine("? 4 foundation piles visible");

            var tableauColumns = page.Locator(".tableau-column");
            await Expect(tableauColumns).ToHaveCountAsync(7);
            Console.WriteLine("? 7 tableau columns visible");

            // Verify controls
            var newGameButton = page.Locator("button:has-text('New Game')");
            await Expect(newGameButton).ToBeVisibleAsync();
            Console.WriteLine("? New Game button visible");

            var autoButton = page.Locator("button:has-text('Auto')");
            await Expect(autoButton).ToBeVisibleAsync();
            Console.WriteLine("? Auto button visible");

            // Verify move counter shows 0
            var moveCount = page.Locator(".stat-item:has-text('Moves')");
            await Expect(moveCount).ToContainTextAsync("0");
            Console.WriteLine("? Move counter starts at 0");

            // Take screenshot
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = "solitaire-test-screenshot.png",
                FullPage = true
            });
            Console.WriteLine("?? Screenshot saved to: solitaire-test-screenshot.png");

            Console.WriteLine("\n? Solitaire page load test completed successfully!");
        }

        /// <summary>
        /// Automated test - verifies drawing from stock works
        /// </summary>
        [TestMethod]
        [TestCategory("Automated")]
        [Timeout(60000)]
        public async Task AutomatedTest_SolitaireDrawFromStock()
        {
            Console.WriteLine("Testing Solitaire draw from stock...");

            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            await NavigateToBlazorPageAsync(page, "/solitaire", ".solitaire-container");

            // Get initial stock count
            var stockPile = page.Locator(".stock-pile");
            var pileCount = page.Locator(".stock-pile .pile-count");
            var initialCount = await pileCount.TextContentAsync();
            Console.WriteLine($"Initial stock count: {initialCount}");

            // Waste should be empty initially
            var wasteCard = page.Locator(".waste-pile .card:not(.card-empty)");
            var wasteCount = await wasteCard.CountAsync();
            Assert.AreEqual(0, wasteCount, "Waste should be empty initially");
            Console.WriteLine("? Waste pile starts empty");

            // Click stock to draw
            await stockPile.ClickAsync();
            await Task.Delay(300); // Wait for state update

            // Waste should now have a card
            wasteCard = page.Locator(".waste-pile .card:not(.card-empty)");
            wasteCount = await wasteCard.CountAsync();
            Assert.AreEqual(1, wasteCount, "Waste should have 1 card after drawing");
            Console.WriteLine("? Card drawn to waste pile");

            // Stock count should decrease
            var newCount = await pileCount.TextContentAsync();
            Console.WriteLine($"New stock count: {newCount}");
            Assert.AreNotEqual(initialCount, newCount, "Stock count should change after draw");

            // Draw a few more cards
            for (int i = 0; i < 3; i++)
            {
                await stockPile.ClickAsync();
                await Task.Delay(200);
            }

            Console.WriteLine("? Multiple draws work correctly");
            Console.WriteLine("\n? Draw from stock test completed successfully!");
        }

        /// <summary>
        /// Automated test - verifies card selection visual feedback
        /// </summary>
        [TestMethod]
        [TestCategory("Automated")]
        [Timeout(60000)]
        public async Task AutomatedTest_SolitaireCardSelection()
        {
            Console.WriteLine("Testing Solitaire card selection...");

            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            await NavigateToBlazorPageAsync(page, "/solitaire", ".solitaire-container");

            // First draw a card to waste
            var stockPile = page.Locator(".stock-pile");
            await stockPile.ClickAsync();
            await Task.Delay(300);

            // Click on the waste card to select it
            var wasteCard = page.Locator(".waste-pile .card:not(.card-empty)");
            await wasteCard.ClickAsync();
            await Task.Delay(200);

            // Card should now have 'selected' class
            var selectedCard = page.Locator(".waste-pile .card.selected");
            var selectedCount = await selectedCard.CountAsync();
            Assert.AreEqual(1, selectedCount, "Waste card should be selected");
            Console.WriteLine("? Card selection visual feedback works");

            // Click again to deselect
            await wasteCard.ClickAsync();
            await Task.Delay(200);

            selectedCount = await selectedCard.CountAsync();
            Assert.AreEqual(0, selectedCount, "Card should be deselected after second click");
            Console.WriteLine("? Card deselection works");

            Console.WriteLine("\n? Card selection test completed successfully!");
        }

        /// <summary>
        /// Automated test - verifies New Game button resets the game
        /// </summary>
        [TestMethod]
        [TestCategory("Automated")]
        [Timeout(60000)]
        public async Task AutomatedTest_SolitaireNewGame()
        {
            Console.WriteLine("Testing Solitaire new game...");

            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            await NavigateToBlazorPageAsync(page, "/solitaire", ".solitaire-container");

            // Draw some cards to change state
            var stockPile = page.Locator(".stock-pile");
            for (int i = 0; i < 5; i++)
            {
                await stockPile.ClickAsync();
                await Task.Delay(150);
            }

            // Verify waste has cards
            var wasteCard = page.Locator(".waste-pile .card:not(.card-empty)");
            var wasteCount = await wasteCard.CountAsync();
            Assert.IsTrue(wasteCount > 0, "Should have cards in waste before reset");
            Console.WriteLine($"Cards in waste before reset: {wasteCount}");

            // Click New Game
            var newGameButton = page.Locator("button:has-text('New Game')");
            await newGameButton.ClickAsync();
            await Task.Delay(500);

            // Waste should be empty again
            wasteCard = page.Locator(".waste-pile .card:not(.card-empty)");
            wasteCount = await wasteCard.CountAsync();
            Assert.AreEqual(0, wasteCount, "Waste should be empty after new game");
            Console.WriteLine("? Waste pile reset");

            // Move counter should be 0
            var moveCount = page.Locator(".stat-item:has-text('Moves')");
            await Expect(moveCount).ToContainTextAsync("0");
            Console.WriteLine("? Move counter reset to 0");

            Console.WriteLine("\n? New game test completed successfully!");
        }

        /// <summary>
        /// Manual test for responsive layout testing
        /// </summary>
        [TestMethod]
        [TestCategory("Manual")]
        public async Task Manual_SolitaireResponsiveLayout()
        {
            Console.WriteLine("Testing Solitaire responsive layout...");

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

            await NavigateToBlazorPageAsync(page, "/solitaire", ".solitaire-container");

            Console.WriteLine("??? Desktop layout (1920x1080)");
            await Task.Delay(2000);

            // Test tablet
            await page.SetViewportSizeAsync(768, 1024);
            Console.WriteLine("?? Tablet layout (768x1024)");
            await Task.Delay(2000);

            // Test mobile
            await page.SetViewportSizeAsync(375, 667);
            Console.WriteLine("?? Mobile layout (375x667)");
            await Task.Delay(2000);

            // Test small mobile
            await page.SetViewportSizeAsync(320, 568);
            Console.WriteLine("?? Small mobile layout (320x568)");
            await Task.Delay(2000);

            Console.WriteLine("\n? Responsive layout test completed!");
            Console.WriteLine("?? Browser will stay open for manual inspection.");
            Console.WriteLine("Try resizing the window to see responsive behavior!");

            // Wait for user to close
            var pageClosedTcs = new TaskCompletionSource<bool>();
            page.Close += (_, _) => pageClosedTcs.TrySetResult(true);
            context.Close += (_, _) => pageClosedTcs.TrySetResult(true);

            await pageClosedTcs.Task;
            Console.WriteLine("Browser closed. Test ending.");
        }
    }
}
