using Microsoft.Playwright;

namespace TestProject1
{
    /// <summary>
    /// Interactive Wordament game test harness
    /// </summary>
    [TestClass]
    public class InteractiveWordamentTest : InteractiveTestBase
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
        /// Interactive test for Wordament game
        /// </summary>
        [TestMethod]
        [TestCategory("Interactive")]
        public async Task LaunchInteractiveBrowser_WordamentGame()
        {
            Console.WriteLine("Launching interactive browser for Wordament game...");
            Console.WriteLine("Close the browser window when you're done experimenting.");

            _browser = await _playwright!.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false,
                SlowMo = 100,
                Devtools = true
            });

            var context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1280, Height = 720 }
            });

            var page = await context.NewPageAsync();
            page.Console += (_, msg) => Console.WriteLine($"[Browser Console] {msg.Text}");

            await page.GotoAsync($"{BASE_URL}/wordament", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle
            });

            Console.WriteLine("Wordament game loaded. Interact with it in the browser window.");
            Console.WriteLine("The test will wait until you close the browser.");

            // Create a TaskCompletionSource to wait for page close
            var pageClosedTcs = new TaskCompletionSource<bool>();
            page.Close += (_, _) => Console.WriteLine("[Event] Page.Close event fired");

            context.Close += (_, _) =>
                   {
                       Console.WriteLine("[Event] Context.Close event fired");
                       pageClosedTcs.TrySetResult(true);
                   };

            await pageClosedTcs.Task;

            Console.WriteLine("Browser closed. Test ending.");
        }

        /// <summary>
        /// Test Wordament game drag selection
        /// </summary>
        [TestMethod]
        [TestCategory("Automated")]
        public async Task AutomatedTest_WordamentDragSelection()
        {
            Console.WriteLine("Testing Wordament drag selection...");

            _browser = await _playwright!.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false,
                SlowMo = 500
            });

            var page = await _browser.NewPageAsync();
            await page.GotoAsync($"{BASE_URL}/wordament");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            try
            {
                await page.WaitForSelectorAsync(".wordament-grid", new PageWaitForSelectorOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = 10000
                });

                Console.WriteLine("Wordament grid loaded!");

                var cells = await page.QuerySelectorAllAsync(".wordament-cell");
                Console.WriteLine($"Found {cells.Count} cells");

                if (cells.Count >= 4)
                {
                    var firstCell = cells[0];
                    var boundingBox = await firstCell.BoundingBoxAsync();

                    if (boundingBox != null)
                    {
                        Console.WriteLine("Simulating drag across cells...");

                        await page.Mouse.MoveAsync(
                        boundingBox.X + boundingBox.Width / 2,
                      boundingBox.Y + boundingBox.Height / 2
                      );
                        await page.Mouse.DownAsync();
                        await Task.Delay(200);

                        for (int i = 1; i < 4 && i < cells.Count; i++)
                        {
                            var cellBox = await cells[i].BoundingBoxAsync();
                            if (cellBox != null)
                            {
                                await page.Mouse.MoveAsync(
                          cellBox.X + cellBox.Width / 2,
                                    cellBox.Y + cellBox.Height / 2
                               );
                                await Task.Delay(200);
                            }
                        }

                        await page.Mouse.UpAsync();
                        Console.WriteLine("Drag completed!");

                        var selectedWordElement = await page.QuerySelectorAsync(".selected-word");
                        if (selectedWordElement != null)
                        {
                            var selectedWord = await selectedWordElement.TextContentAsync();
                            Console.WriteLine($"Selected word: {selectedWord}");
                        }
                    }
                }

                await page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = "wordament-test-screenshot.png",
                    FullPage = true
                });

                Console.WriteLine("Screenshot saved to: wordament-test-screenshot.png");

                await Task.Delay(3000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during Wordament test: {ex.Message}");
                throw;
            }
        }
    }
}
