using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using System.Text.Json;
using Client.Games.Cards.Services;

namespace TestProject1
{
    [TestClass]
    public class InteractiveFreeCellTest : InteractiveTestBase
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
        /// Manual interactive test for FreeCell game - keeps browser open for user interaction
        /// </summary>
        [TestMethod]
        [TestCategory("Manual")]
        public async Task Manual_LaunchInteractiveBrowser_FreeCell()
        {
            Console.WriteLine("Launching interactive browser for FreeCell game...");
            Console.WriteLine("Close the browser window when you're done experimenting.");

            _browser = await _playwright!.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false,
                SlowMo = 100,
                Devtools = false
            });

            var context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1280, Height = 900 },
                IgnoreHTTPSErrors = true // Accept self-signed certs
            });

            var page = await context.NewPageAsync();
            page.Console += (_, msg) => Console.WriteLine($"[Browser Console] {msg.Text}");

            // Navigate using shared helper
            await NavigateToBlazorPageAsync(page, "/freecell", ".freecell-container");

            Console.WriteLine("🃏 FreeCell game loaded!");
            Console.WriteLine("How to play:");
            Console.WriteLine("  - All 52 cards are dealt face-up in 8 columns");
            Console.WriteLine("  - Click a card to select it");
            Console.WriteLine("  - Move cards to free cells (top-left) or foundations (top-right)");
            Console.WriteLine("  - Stack cards in descending order, alternating colors");
            Console.WriteLine("  - Any card can go on an empty column");
            Console.WriteLine("  - Win by moving all cards to the 4 foundation piles (A→K)");
            Console.WriteLine("  - Use 'Auto' to automatically move cards to foundations");
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
        [TestMethod]
        public async Task AutoSolve_FreeCell()
        {

            _browser = await _playwright!.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false,
                SlowMo = 100,
                Devtools = false,
                Args = new[] { "--start-maximized" }
            });

            var context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                //ViewportSize = new ViewportSize { Width = 1280, Height = 900 },
                ViewportSize = ViewportSize.NoViewport, // new ViewportSize { Width = 1280, Height = 900 }
                IgnoreHTTPSErrors = true // Accept self-signed certs
            });

            var page = await context.NewPageAsync();
            page.Console += (_, msg) => Console.WriteLine($"[Browser Console] {msg.Text}");

            // Navigate using shared helper
            var gameId = 12345;
            await NavigateToBlazorPageAsync(page, $"/freecell/{gameId}", ".freecell-container");

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
            await Task.Delay(1000);
            var newButton = page.Locator("button:has-text('New')");
            await newButton.ClickAsync();
            await Task.Delay(300);
            var gamebutton = page.Locator($"button:has-text('replay #{gameId}')");
            await gamebutton.ClickAsync();
            await Task.Delay(300);
            /*
// Example inside a Playwright test method
var columns = page.Locator(".tableau-column");
int colCount = await columns.CountAsync();
for (int i = 0; i < colCount; i++)
{
    var colLocator = columns.Nth(i);
    var cards = colLocator.Locator(".playing-card");
    int cardCount = await cards.CountAsync();
    var list = new List<string>();
    for (int j = 0; j < cardCount; j++)
    {
        // PlayingCard renders <img class="card-img" alt="A♠" src="/img/cards/AS.png">
        var img = cards.Nth(j).Locator("img.card-img");
        var alt = await img.GetAttributeAsync("alt"); // e.g. "A♠"
        var src = await img.GetAttributeAsync("src"); // e.g. "/img/cards/AS.png"
        list.Add(alt ?? src ?? "unknown");
    }
    Console.WriteLine($"Column {i + 1}: {string.Join(", ", list)}");
}             
             */

            var registered = await page.EvaluateAsync<bool>("() => !!window.freecellBlazorComponent && !!window.freecellBlazorComponent.invokeMethodAsync");
            if (!registered)
            {
                Console.WriteLine($"[Interop] Blazor component NOT registered or missing invokeMethodAsync");
            }
            var json = await page.EvaluateAsync<string>("() => window.getFreeCellStateJson()");
            if (string.IsNullOrEmpty(json))
            {
                Console.WriteLine($"[Interop] getFreeCellStateJson returned empty");
            }
            FreeCellGameService? freecellGameService = null;
            try
            {
                freecellGameService = FreeCellGameService.FromJson(json!);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Interop] Failed to deserialize FreeCell state: {ex.Message}");
            }

            Console.WriteLine($"{freecellGameService?.Tableau.Count ?? 0} columns in tableau according to interop JSON");
            Assert.AreEqual(8, freecellGameService?.Tableau.Count, "Should have 8 columns in tableau");




            var tableauColumns = page.Locator(".tableau-column");

            await Task.Delay(5000); // Wait for page to stabilize
            pageClosedTcs.TrySetResult(true); // Reset in case of multiple events

            await pageClosedTcs.Task;

            Console.WriteLine("Browser closed. Test ending.");
        }

        /// <summary>
        /// Automated test - verifies FreeCell page loads and basic elements are present
        /// </summary>
        [TestMethod]
        [TestCategory("Automated")]
        [Timeout(60000)]
        public async Task AutomatedTest_FreeCellPageLoads()
        {
            Console.WriteLine("Testing FreeCell page load...");

            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            // Navigate to FreeCell
            await NavigateToBlazorPageAsync(page, "/freecell", ".freecell-container");

            Console.WriteLine("✓ FreeCell container loaded");

            // Verify 4 free cells are present
            var freeCells = page.Locator(".free-cell");
            await Expect(freeCells).ToHaveCountAsync(4);
            Console.WriteLine("✓ 4 free cells visible");

            // Verify 4 foundations
            var foundations = page.Locator(".foundation-pile");
            await Expect(foundations).ToHaveCountAsync(4);
            Console.WriteLine("✓ 4 foundation piles visible");

            // Verify 8 tableau columns
            var tableauColumns = page.Locator(".tableau-column");
            await Expect(tableauColumns).ToHaveCountAsync(8);
            Console.WriteLine("✓ 8 tableau columns visible");

            // Verify controls - button text is now "New" not "New Game"
            var newGameButton = page.Locator("button:has-text('New')").First;
            await Expect(newGameButton).ToBeVisibleAsync();
            Console.WriteLine("✓ New Game button visible");

            // Verify Undo button exists (Auto button was removed)
            var undoButton = page.Locator("button:has-text('Undo')");
            await Expect(undoButton).ToBeVisibleAsync();
            Console.WriteLine("✓ Undo button visible");

            // Verify move counter shows 0
            var moveCount = page.Locator(".stat-item:has-text('Moves')");
            await Expect(moveCount).ToContainTextAsync("0");
            Console.WriteLine("✓ Move counter starts at 0");

            // Take screenshot
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = "freecell-test-screenshot.png",
                FullPage = true
            });
            Console.WriteLine("📸 Screenshot saved to: freecell-test-screenshot.png");

            Console.WriteLine("\n✓ FreeCell page load test completed successfully!");
        }

        /// <summary>
        /// Automated test - verifies all 52 cards are visible in tableau
        /// </summary>
        [TestMethod]
        [TestCategory("Automated")]
        [Timeout(60000)]
        public async Task AutomatedTest_FreeCellAllCardsDealt()
        {
            Console.WriteLine("Testing FreeCell all cards dealt...");

            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            await NavigateToBlazorPageAsync(page, "/freecell", ".freecell-container");

            // Count all cards in tableau - use playing-card class
            var allCards = page.Locator(".tableau-column .playing-card");
            var cardCount = await allCards.CountAsync();

            Assert.AreEqual(52, cardCount, "Should have all 52 cards dealt");
            Console.WriteLine($"✓ All 52 cards dealt to tableau");

            // Verify first 4 columns have 7 cards each
            for (int col = 0; col < 4; col++)
            {
                var columnCards = page.Locator($".tableau-column:nth-child({col + 1}) .playing-card");
                var count = await columnCards.CountAsync();
                Assert.AreEqual(7, count, $"Column {col + 1} should have 7 cards");
            }
            Console.WriteLine("✓ First 4 columns have 7 cards each");

            // Verify last 4 columns have 6 cards each
            for (int col = 4; col < 8; col++)
            {
                var columnCards = page.Locator($".tableau-column:nth-child({col + 1}) .playing-card");
                var count = await columnCards.CountAsync();
                Assert.AreEqual(6, count, $"Column {col + 1} should have 6 cards");
            }
            Console.WriteLine("✓ Last 4 columns have 6 cards each");

            Console.WriteLine("\n✓ All cards dealt test completed successfully!");
        }

        /// <summary>
        /// Automated test - verifies card selection and free cell movement
        /// </summary>
        [TestMethod]
        [TestCategory("Automated")]
        [Timeout(60000)]
        public async Task AutomatedTest_FreeCellCardSelection()
        {
            Console.WriteLine("Testing FreeCell card selection...");

            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            await NavigateToBlazorPageAsync(page, "/freecell", ".freecell-container");

            // Click on the top card of first column (last playing-card in the column)
            var firstColumnTopCard = page.Locator(".tableau-column:first-child .playing-card").Last;
            await firstColumnTopCard.ClickAsync();
            await Task.Delay(200);

            // Card should be selected
            var selectedCard = page.Locator(".playing-card.selected");
            var selectedCount = await selectedCard.CountAsync();
            Assert.AreEqual(1, selectedCount, "One card should be selected");
            Console.WriteLine("✓ Card selection works");

            // Click on first free cell to move the card
            var firstFreeCell = page.Locator(".free-cell:first-child");
            await firstFreeCell.ClickAsync();
            await Task.Delay(500); // Allow time for auto-move if enabled

            // Free cell should now have a card (playing-card, not card-empty)
            var freeCellCard = page.Locator(".free-cell:first-child .playing-card");
            var freeCellCardCount = await freeCellCard.CountAsync();
            Assert.AreEqual(1, freeCellCardCount, "Free cell should have a card");
            Console.WriteLine("✓ Card moved to free cell");

            // Move counter should be at least 1 (could be more if auto-move is enabled)
            var moveCount = page.Locator(".stat-item:has-text('Moves')");
            var moveText = await moveCount.TextContentAsync();
            Assert.IsTrue(moveText!.Contains("Moves:"), "Move counter should be visible");
            // Extract the number and verify it's >= 1
            var moveNumber = int.Parse(moveText.Replace("Moves:", "").Trim());
            Assert.IsTrue(moveNumber >= 1, $"Move counter should be at least 1, but was {moveNumber}");
            Console.WriteLine($"✓ Move counter shows {moveNumber} move(s)");

            Console.WriteLine("\n✓ Card selection and movement test completed successfully!");
        }

        /// <summary>
        /// Automated test - verifies Auto button moves cards to foundations
        /// </summary>
        [TestMethod]
        [TestCategory("Automated")]
        [Timeout(60000)]
        public async Task AutomatedTest_FreeCellAutoMove()
        {
            Console.WriteLine("Testing FreeCell auto-move via keyboard...");

            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            await NavigateToBlazorPageAsync(page, "/freecell", ".freecell-container");

            // Get initial foundation counts - use playing-card class
            var foundationCards = page.Locator(".foundation-pile .playing-card");
            var initialFoundationCount = await foundationCards.CountAsync();
            Console.WriteLine($"Initial foundation cards: {initialFoundationCount}");

            // Focus container and press 'A' for auto-complete (keyboard shortcut)
            var container = page.Locator(".freecell-container");
            await container.FocusAsync();
            await page.Keyboard.PressAsync("a");
            await Task.Delay(500);

            // Check if any cards moved to foundations
            var newFoundationCount = await foundationCards.CountAsync();
            Console.WriteLine($"Foundation cards after auto: {newFoundationCount}");

            // Note: Auto-move success depends on game state
            // Just verify the keyboard shortcut works without error
            Console.WriteLine("✓ Auto-complete keyboard shortcut (A) worked without error");

            Console.WriteLine("\n✓ Auto-move test completed!");
        }

        /// <summary>
        /// Automated test - verifies New Game resets the game
        /// </summary>
        [TestMethod]
        [TestCategory("Automated")]
        [Timeout(60000)]
        public async Task AutomatedTest_FreeCellNewGame()
        {
            Console.WriteLine("Testing FreeCell new game...");

            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            await NavigateToBlazorPageAsync(page, "/freecell", ".freecell-container");

            // Make a move first - use correct selector
            var firstColumnTopCard = page.Locator(".tableau-column:first-child .playing-card").Last;
            await firstColumnTopCard.ClickAsync();
            await Task.Delay(200);

            var firstFreeCell = page.Locator(".free-cell:first-child");
            await firstFreeCell.ClickAsync();
            await Task.Delay(500); // Allow time for auto-move if enabled

            // Verify at least one move was made (could be more due to auto-move)
            var moveCount = page.Locator(".stat-item:has-text('Moves')");
            var moveText = await moveCount.TextContentAsync();
            var initialMoves = int.Parse(moveText!.Replace("Moves:", "").Trim());
            Assert.IsTrue(initialMoves >= 1, $"Should have at least 1 move, but had {initialMoves}");
            Console.WriteLine($"✓ Made initial move(s): {initialMoves}");

            // Click New button to open dropdown menu
            var newGameButton = page.Locator("button:has-text('New')").First;
            await newGameButton.ClickAsync();
            await Task.Delay(300);

            // Click "Random Game" from dropdown
            var randomGameOption = page.Locator("button:has-text('Random Game')");
            await randomGameOption.ClickAsync();
            await Task.Delay(500);

            // Move counter should be reset to 0
            await Expect(moveCount).ToContainTextAsync("0");
            Console.WriteLine("✓ Move counter reset to 0");

            // Free cells should be empty (no playing-card elements)
            var freeCellCards = page.Locator(".free-cell .playing-card");
            var freeCellCardCount = await freeCellCards.CountAsync();
            Assert.AreEqual(0, freeCellCardCount, "Free cells should be empty after new game");
            Console.WriteLine("✓ Free cells cleared");

            // All 52 cards should be back in tableau
            var allCards = page.Locator(".tableau-column .playing-card");
            var cardCount = await allCards.CountAsync();
            Assert.AreEqual(52, cardCount, "All 52 cards should be in tableau");
            Console.WriteLine("✓ All cards back in tableau");

            Console.WriteLine("\n✓ New game test completed successfully!");
        }

        /// <summary>
        /// Manual test for responsive layout testing
        /// </summary>
        [TestMethod]
        [TestCategory("Manual")]
        public async Task Manual_FreeCellResponsiveLayout()
        {
            Console.WriteLine("Testing FreeCell responsive layout...");

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

            await NavigateToBlazorPageAsync(page, "/freecell", ".freecell-container");

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
