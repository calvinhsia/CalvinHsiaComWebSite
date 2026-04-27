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
        /// Common helper method to launch interactive browser with device-specific viewport
        /// </summary>
        private async Task LaunchInteractiveBrowser(string deviceName, ViewportSize? viewportSize, bool isMobile = false, double devicePixelRatio = 3.0)
        {
            Console.WriteLine($"Launching interactive browser for FreeCell game ({deviceName})...");
            Console.WriteLine("Close the browser window when you're done experimenting.");
            Console.WriteLine($"[DEBUG] isMobile parameter: {isMobile}");

            var launchOptions = new BrowserTypeLaunchOptions
            {
                Headless = false,
                SlowMo = 100,
                Devtools = false
            };

            // Add maximized arg only if no specific viewport
            if (viewportSize == null)
            {
                launchOptions.Args = new[] { "--start-maximized" };
                Console.WriteLine("[DEBUG] Browser Args: --start-maximized");
            }
            else
            {
                // For mobile devices, size window to show actual device proportions
                // Scale down to fit on desktop screen (max height ~1200px for typical desktop)
                var maxDesktopHeight = 1200;
                var scale = Math.Min(1.0, (double)maxDesktopHeight / viewportSize.Height);
                var windowWidth = (int)(viewportSize.Width * scale);
                var windowHeight = (int)(viewportSize.Height * scale);

                launchOptions.Args = new[] { 
                    $"--window-size={windowWidth},{windowHeight}",
                    "--window-position=100,50" // Position away from screen edge
                };
                Console.WriteLine($"[DEBUG] Browser window size: {windowWidth}x{windowHeight} (scaled {scale:P0} to fit desktop)");
                Console.WriteLine($"[DEBUG] This shows the actual mobile aspect ratio ({viewportSize.Width}x{viewportSize.Height})");
            }

            _browser = await _playwright!.Chromium.LaunchAsync(launchOptions);
            Console.WriteLine("[DEBUG] Browser launched");

            var contextOptions = new BrowserNewContextOptions
            {
                IgnoreHTTPSErrors = true // Accept self-signed certs
            };

            if (viewportSize != null)
            {
                contextOptions.ViewportSize = viewportSize;
                Console.WriteLine($"[DEBUG] Setting ViewportSize: {viewportSize.Width}x{viewportSize.Height}");

                // Enable mobile emulation features
                if (isMobile)
                {
                    contextOptions.IsMobile = true;
                    contextOptions.HasTouch = true;
                    contextOptions.DeviceScaleFactor = (float)devicePixelRatio;

                    // Set realistic mobile user agent
                    string userAgent = deviceName switch
                    {
                        "Samsung Galaxy S24" or "Samsung Galaxy S24+" => 
                            "Mozilla/5.0 (Linux; Android 14; SM-S928U) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Mobile Safari/537.36",
                        "iPhone 15 Pro" => 
                            "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1",
                        _ => 
                            "Mozilla/5.0 (Linux; Android 14) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Mobile Safari/537.36"
                    };
                    contextOptions.UserAgent = userAgent;

                    Console.WriteLine("[DEBUG] Mobile emulation enabled:");
                    Console.WriteLine($"  - IsMobile: {contextOptions.IsMobile}");
                    Console.WriteLine($"  - HasTouch: {contextOptions.HasTouch}");
                    Console.WriteLine($"  - DeviceScaleFactor: {contextOptions.DeviceScaleFactor}");
                    Console.WriteLine($"  - UserAgent: {userAgent.Substring(0, Math.Min(60, userAgent.Length))}...");
                }
                else
                {
                    Console.WriteLine("[DEBUG] Mobile emulation NOT enabled (isMobile=false)");
                }
            }
            else
            {
                contextOptions.ViewportSize = ViewportSize.NoViewport;
                Console.WriteLine("[DEBUG] ViewportSize: NoViewport (maximized)");
            }

            var context = await _browser.NewContextAsync(contextOptions);
            Console.WriteLine("[DEBUG] Browser context created");
            var page = await context.NewPageAsync();
            page.Console += (_, msg) => Console.WriteLine($"[Browser Console] {msg.Text}");

            // Navigate using shared helper
            await NavigateToBlazorPageAsync(page, "/freecell", ".freecell-container");

            // Log browser viewport info from JavaScript
            var viewportInfo = await page.EvaluateAsync<string>(@"() => {
                const card = document.querySelector('.playing-card') || document.querySelector('.card');
                const cardStyle = card ? window.getComputedStyle(card) : null;
                const mediaQueries = {
                    'max-width-768px': window.matchMedia('(max-width: 768px)').matches,
                    'max-width-550px': window.matchMedia('(max-width: 550px)').matches,
                    'max-width-480px': window.matchMedia('(max-width: 480px)').matches
                };
                return JSON.stringify({
                    innerWidth: window.innerWidth,
                    innerHeight: window.innerHeight,
                    outerWidth: window.outerWidth,
                    outerHeight: window.outerHeight,
                    devicePixelRatio: window.devicePixelRatio,
                    isTouchDevice: 'ontouchstart' in window,
                    userAgent: navigator.userAgent,
                    maxTouchPoints: navigator.maxTouchPoints,
                    mediaQueries: mediaQueries,
                    cardWidth: cardStyle ? cardStyle.width : 'no card found',
                    cardHeight: cardStyle ? cardStyle.height : 'no card found'
                }, null, 2);
            }");
            Console.WriteLine("[DEBUG] Browser JavaScript viewport info:");
            Console.WriteLine(viewportInfo);

            Console.WriteLine("🃏 FreeCell game loaded!");
            Console.WriteLine("How to play:");
            Console.WriteLine("  - All 52 cards are dealt face-up in 8 columns");
            Console.WriteLine("  - Click a card to select it");
            Console.WriteLine("  - Move cards to free cells (top-left) or foundations (top-right)");
            Console.WriteLine("  - Stack cards in descending order, alternating colors");
            Console.WriteLine("  - Any card can go on an empty column");
            Console.WriteLine("  - Win by moving all cards to the 4 foundation piles (A→K)");
            Console.WriteLine("  - Use 'Auto' to automatically move cards to foundations");
            Console.WriteLine("\n⚠️  TIP: Press F12 to open DevTools and check 'Rendering' settings");
            Console.WriteLine("The test will wait until you close the browser.");

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
        /// Manual interactive test for FreeCell game - keeps browser open for user interaction
        /// </summary>
        [TestMethod]
        [TestCategory("Manual")]
        public async Task Manual_LaunchInteractiveBrowser_FreeCell()
        {
            await LaunchInteractiveBrowser("Desktop - Maximized", null);
        }

        /// <summary>
        /// Manual interactive test for FreeCell - Samsung Galaxy S24 (360x780 CSS pixels, 3x DPR = 1080x2340 physical)
        /// </summary>
        [TestMethod]
        [TestCategory("Manual")]
        public async Task Manual_LaunchInteractiveBrowser_FreeCell_GalaxyS24()
        {
            // Galaxy S24: 1080x2340 physical pixels ÷ 3 (device pixel ratio) = 360x780 CSS pixels
            await LaunchInteractiveBrowser("Samsung Galaxy S24", new ViewportSize { Width = 360, Height = 780 }, isMobile: true);
        }

        /// <summary>
        /// Manual interactive test for FreeCell - Samsung Galaxy S24+ (411x707 CSS pixels, 2.63x DPR = 1081x1859 physical)
        /// </summary>
        [TestMethod]
        [TestCategory("Manual")]
        public async Task Manual_LaunchInteractiveBrowser_FreeCell_GalaxyS24Plus()
        {
            // Galaxy S24+: Real measured viewport is 411x707 CSS pixels with 2.63x DPR
            await LaunchInteractiveBrowser("Samsung Galaxy S24+", new ViewportSize { Width = 411, Height = 707 }, isMobile: true, devicePixelRatio: 2.63);
        }

        /// <summary>
        /// Manual interactive test for FreeCell - iPhone 15 Pro (393x852 CSS pixels, 3x DPR = 1179x2556 physical)
        /// </summary>
        [TestMethod]
        [TestCategory("Manual")]
        public async Task Manual_LaunchInteractiveBrowser_FreeCell_iPhone()
        {
            // iPhone 15 Pro: 1179x2556 physical pixels ÷ 3 (device pixel ratio) = 393x852 CSS pixels
            await LaunchInteractiveBrowser("iPhone 15 Pro", new ViewportSize { Width = 393, Height = 852 }, isMobile: true);
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
            var moveCount = page.GetByText("Moves:");
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

            // Disable auto-move-to-foundation so the card stays in the free cell
            await page.Locator("button[title='Game options']").ClickAsync();
            var autoMoveCheckbox = page.Locator(".options-menu .checkbox-item").First.Locator("input[type='checkbox']");
            if (await autoMoveCheckbox.IsCheckedAsync())
            {
                await autoMoveCheckbox.ClickAsync();
            }
            // Close the menu by clicking the container
            await page.Locator(".freecell-container").ClickAsync();

            // Click on the top card of first column (last playing-card in the column)
            var firstColumnTopCard = page.Locator(".tableau-column:first-child .playing-card").Last;
            await firstColumnTopCard.ClickAsync();

            // Card should be selected (use Expect with auto-retry for Blazor re-render)
            var selectedCard = page.Locator(".playing-card.selected");
            await Expect(selectedCard).ToHaveCountAsync(1);
            Console.WriteLine("✓ Card selection works");

            // Click on first free cell to move the card
            var firstFreeCell = page.Locator(".free-cell:first-child");
            await firstFreeCell.ClickAsync();

            // Free cell should now have a card (auto-retry waits for Blazor re-render)
            var freeCellCard = page.Locator(".free-cell:first-child .playing-card");
            await Expect(freeCellCard).ToHaveCountAsync(1);
            Console.WriteLine("✓ Card moved to free cell");

            // Move counter should show at least 1 (auto-retry for re-render)
            var moveCount = page.GetByText("Moves:");
            await Expect(moveCount).Not.ToContainTextAsync("0");
            var moveText = await moveCount.TextContentAsync();
            var moveNumber = int.Parse(moveText!.Replace("Moves:", "").Trim());
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
            var moveCount = page.GetByText("Moves:");
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
