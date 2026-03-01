using Azure;
using Client.Games.Cards.Services;
using Microsoft.Playwright;
using System.Text.Json;
using System.Diagnostics;
using static Microsoft.Playwright.Assertions;

namespace TestProject1
{
    [TestClass]
    public class FreeCellSolverTests : InteractiveTestBase
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
        [TestMethod]
        [TestCategory("Automated")]
        [Timeout(60000)]
        public async Task AutomatedTest_FreeCellReadServiceViaInterop()
        {
            Console.WriteLine("Testing FreeCell: read game state via JS interop...");

            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            await NavigateToBlazorPageAsync(page, "/freecell", ".freecell-container");

            // Wait for the Blazor component instance to be registered on the page
            // The JS helper exists immediately, but it returns empty until the DotNet object
            // is registered by the component. Poll for registration and then call the helper.
            for (int attempt = 0; attempt < 25; attempt++)
            {
                try
                {
                    var registered = await page.EvaluateAsync<bool>("() => !!window.freecellBlazorComponent && !!window.freecellBlazorComponent.invokeMethodAsync");
                    if (registered)
                    {
                        Console.WriteLine($"[Interop] Blazor component registered and ready for interop calls (attempt {attempt + 1})");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Interop] registration check attempt {attempt + 1} threw: {ex.GetType().Name}: {ex.Message}");
                }
                await Task.Delay(200);
            }

            string json = string.Empty;
            // Try several times to get a non-empty JSON from the helper
            for (int attempt = 0; attempt < 10; attempt++)
            {
                try
                {
                    json = await page.EvaluateAsync<string>("() => window.getFreeCellStateJson()");
                    if (!string.IsNullOrEmpty(json))
                    {
                        Console.WriteLine($"Got non-empty JSON from interop on attempt {attempt + 1}");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Interop] getFreeCellStateJson attempt {attempt + 1} threw: {ex.GetType().Name}: {ex.Message}");
                }
                await Task.Delay(300);
            }

            if (string.IsNullOrEmpty(json))
            {
                await page.ScreenshotAsync(new PageScreenshotOptions { Path = "freecell-interop-empty.png", FullPage = true });
                Assert.Fail("Interop returned empty game state JSON. Screenshot: freecell-interop-empty.png");
            }

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                Assert.IsTrue(root.TryGetProperty("tableau", out var tableau) || root.TryGetProperty("Tableau", out tableau), "Interop JSON must contain 'Tableau'");

                int total = 0;
                int idx = 0;
                foreach (var col in tableau.EnumerateArray())
                {
                    var cards = col.EnumerateArray().Select(c => c.GetString() ?? "null").ToList();
                    var colCount = cards.Count;
                    total += colCount;
                    // Output the cards found for debugging
                    Console.WriteLine($"Column {idx + 1}: {string.Join(", ", cards)}");
                    if (idx < 4) Assert.AreEqual(7, colCount, $"Interop: Column {idx + 1} should have 7 cards");
                    else Assert.AreEqual(6, colCount, $"Interop: Column {idx + 1} should have 6 cards");
                    idx++;
                }
                Assert.AreEqual(52, total, $"Interop: Total cards should be 52 but was {total}");
                Console.WriteLine($"[Interop] Verified tableau via interop: total={total}");
            }
            catch (Exception ex)
            {
                await page.ScreenshotAsync(new PageScreenshotOptions { Path = "freecell-interop-parse-error.png", FullPage = true });
                Assert.Fail($"Failed to parse interop JSON: {ex.Message}. Screenshot: freecell-interop-parse-error.png");
            }

            Console.WriteLine("\n✓ FreeCell interop read test completed successfully!");
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
            // dump out the cards in each column according to the interop JSON
            for (int col = 0; col < freecellGameService?.Tableau.Count; col++)
            {
                var columnCards = freecellGameService.Tableau[col];
                var cardList = columnCards.Select(c => $"{c.Rank}{c.Suit}").ToList();
                Console.WriteLine($"Column {col + 1}: {string.Join(", ", cardList)}");
            }

            // Diagnostics: list foundation elements and source card before attempting move
            try
            {
                var foundations = page.Locator(".foundation-pile");
                var fCount = await foundations.CountAsync();
                Console.WriteLine($"Diagnostics: foundations count = {fCount}");
                for (int i = 0; i < fCount; i++)
                {
                    var loc = foundations.Nth(i);
                    var visible = await loc.IsVisibleAsync();
                    string html = string.Empty;
                    try { html = await loc.EvaluateAsync<string>("el => el.outerHTML"); } catch { html = "<outerHTML unavailable>"; }
                    Console.WriteLine($"foundation[{i + 1}] visible={visible}: {html}");
                }

                var srcCard = page.Locator($".tableau-column:nth-child({5}) .playing-card").Last;
                Console.WriteLine($"Source card visible: {await srcCard.IsVisibleAsync()}");
                try { Console.WriteLine(await srcCard.EvaluateAsync<string>("el => el.outerHTML")); } catch { }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Diagnostics error: {ex.GetType().Name}: {ex.Message}");
            }

            // move column 4 index 0 to column 1
            var mover = new FreeCellMover(page);
            //await mover.DragCardOrStackAsync(5, 6, $".free-cell:nth-child{1})");
            await mover.MoveBottomCardToFreeCellAsync(columnIndex: 5, freeCellIndex: 1);
            await mover.MoveBottomCardToFreeCellAsync(columnIndex: 5, freeCellIndex: 2);
            await mover.MoveBottomCardToFreeCellAsync(columnIndex: 5, freeCellIndex: 3);
            await mover.MoveBottomCardToFreeCellAsync(columnIndex: 5, freeCellIndex: 4);
            await mover.MoveFreeCellToTableauAsync(freeCellIndex: 1, destColumnIndex: 2);
            await mover.MoveStackToColumnAsync(srcColumnIndex: 5, cardIndexFromTop: -1, destColumnIndex: 3);



            var tableauColumns = page.Locator(".tableau-column");

            await Task.Delay(8000);
            pageClosedTcs.TrySetResult(true); // Reset in case of multiple events

            await pageClosedTcs.Task;

            Console.WriteLine("Browser closed. Test ending.");
        }

    }

    public class FreeCellMover
    {
        private readonly IPage _page;

        public FreeCellMover(IPage page)
        {
            _page = page ?? throw new ArgumentNullException(nameof(page));
        }

        public async Task MoveBottomCardToFreeCellAsync(int columnIndex = 2, int freeCellIndex = 1)
        {
            // columnIndex and freeCellIndex are 1-based (matches nth-child)
            var source = _page.Locator($".tableau-column:nth-child({columnIndex}) .playing-card").Last;
            await source.ClickAsync();
            await Task.Delay(100); // small pause for UI selection

            var dest = _page.Locator($".free-cell:nth-child({freeCellIndex})");
            await dest.ClickAsync();
        }

        //
        // await DragCardOrStackAsync(columnIndex: 2, cardIndexFromTop: -1, destSelector: ".free-cell:nth-child(1)");0
        public async Task DragCardOrStackAsync(int columnIndex, int cardIndexFromTop, string destSelector)
        {
            // cardIndexFromTop is 0-based index within the column (0 = top). Use Last for bottom if you prefer.
            var cards = _page.Locator($".tableau-column:nth-child({columnIndex}) .playing-card");
            var source = (cardIndexFromTop == -1) ? cards.Last : cards.Nth(cardIndexFromTop);

            var dest = _page.Locator(destSelector);

            // Prefer built-in drag API if available
            try
            {
                await source.DragToAsync(dest);
            }
            catch
            {
                // Fallback to raw mouse drag if DragToAsync isn't available or fails
                var sBox = await source.BoundingBoxAsync();
                var dBox = await dest.BoundingBoxAsync();
                if (sBox == null || dBox == null) throw new InvalidOperationException("Unable to get bounding boxes for drag.");

                var startX = sBox.X + sBox.Width / 2;
                var startY = sBox.Y + sBox.Height / 2;
                var endX = dBox.X + dBox.Width / 2;
                var endY = dBox.Y + dBox.Height / 2;

                await _page.Mouse.MoveAsync(startX, startY);
                await _page.Mouse.DownAsync();
                await _page.Mouse.MoveAsync(endX, endY, new MouseMoveOptions { Steps = 10 });
                await _page.Mouse.UpAsync();
            }
        }

        // Click-based move: tableau -> foundation
        public async Task MoveBottomCardToFoundationAsync(int columnIndex = 2, int foundationIndex = 1)
        {
            var ok = await TryMoveBottomCardToFoundationAsync(columnIndex, foundationIndex);
            if (!ok)
            {
                // Provide diagnostics if the simple move failed
                var file = $"freecell-move-failure-{DateTime.UtcNow:yyyyMMddHHmmss}.png";
                try { await _page.ScreenshotAsync(new PageScreenshotOptions { Path = file, FullPage = true }); } catch { }
                throw new InvalidOperationException($"Move to foundation failed. Screenshot: {file}");
            }
        }

        // Robust attempt that verifies the destination foundation received a new card
        public async Task<bool> TryMoveBottomCardToFoundationAsync(int columnIndex = 2, int foundationIndex = 1, int timeoutMs = 3000)
        {
            // Determine source and destination locators
            var source = _page.Locator($".tableau-column:nth-child({columnIndex}) .playing-card").Last;
            var foundations = _page.Locator(".foundation-pile");
            var dest = foundations.Nth(Math.Max(0, foundationIndex - 1));
            // Ensure destination is present/visible before trying to interact
            try
            {
                await dest.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 2000 });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MoveDiag] Destination wait failed: {ex.GetType().Name}: {ex.Message}");
            }
            Console.WriteLine($"[MoveDiag] Attempting move: tableau column={columnIndex} -> foundation={foundationIndex}");

            // Before interacting, verify the move is legal according to game state (via interop)
            try
            {
                var stateJson = await _page.EvaluateAsync<string>("() => window.getFreeCellStateJson()");
                if (!string.IsNullOrEmpty(stateJson))
                {
                    try
                    {
                        var svc = Client.Games.Cards.Services.FreeCellGameService.FromJson(stateJson);
                        // Convert to 0-based indices
                        var srcColIdx = Math.Max(0, columnIndex - 1);
                        var fIdx = Math.Max(0, foundationIndex - 1);
                        Client.Games.Cards.Models.Card? srcCard = null;
                        try { srcCard = svc.Tableau[srcColIdx].Count > 0 ? svc.Tableau[srcColIdx][^1] : null; } catch { srcCard = null; }

                        if (srcCard == null)
                        {
                            Console.WriteLine("[MoveDiag] Interop: source card not found or column empty");
                            return false;
                        }

                        var foundationPile = svc.Foundations[fIdx];
                        bool legal = false;
                        if (foundationPile.Count == 0)
                        {
                            legal = srcCard.Rank == Client.Games.Cards.Models.Rank.Ace;
                        }
                        else
                        {
                            var top = foundationPile[^1];
                            legal = top.Suit == srcCard.Suit && (int)srcCard.Rank == (int)top.Rank + 1;
                        }

                        Console.WriteLine($"[MoveDiag] Interop: source={srcCard.Rank} {srcCard.Suit}, foundationTopCount={foundationPile.Count}, legal={legal}");
                        if (!legal)
                        {
                            Console.WriteLine("[MoveDiag] Move is illegal according to game rules; skipping UI attempt");
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[MoveDiag] Failed to parse interop state: {ex.GetType().Name}: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine("[MoveDiag] Interop returned empty state JSON");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MoveDiag] Interop call failed: {ex.GetType().Name}: {ex.Message}");
            }

            // Record initial foundation count (diagnostic)
            int initialCount = 0;
            try
            {
                initialCount = await dest.Locator(".playing-card").CountAsync();
                Console.WriteLine($"[MoveDiag] Initial foundation count: {initialCount}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MoveDiag] Failed to get initial foundation count: {ex.GetType().Name}: {ex.Message}");
                initialCount = 0;
            }

            // Try to capture bounding boxes for diagnostics
            try
            {
                var sBox = await source.BoundingBoxAsync();
                var dBox = await dest.BoundingBoxAsync();
                Console.WriteLine($"[MoveDiag] Source bbox: {FormatBox(sBox)}, Dest bbox: {FormatBox(dBox)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MoveDiag] BoundingBox retrieval failed: {ex.GetType().Name}: {ex.Message}");
            }

            // Click source then destination, using force in case of overlapping elements
            try
            {
                await source.ClickAsync(new LocatorClickOptions { Force = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MoveDiag] Click on source failed: {ex.GetType().Name}: {ex.Message}");
                return false;
            }

            await Task.Delay(120);

            var clicked = false;
            try
            {
                await dest.ClickAsync(new LocatorClickOptions { Force = true });
                clicked = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MoveDiag] Click on destination failed: {ex.GetType().Name}: {ex.Message}");
                // Try drag fallback
                try
                {
                    Console.WriteLine("[MoveDiag] Attempting DragToAsync fallback");
                    await source.DragToAsync(dest);
                    clicked = true;
                }
                catch (Exception dex)
                {
                    Console.WriteLine($"[MoveDiag] DragToAsync failed: {dex.GetType().Name}: {dex.Message}");
                    // Mouse fallback
                    try
                    {
                        var sBox = await source.BoundingBoxAsync();
                        var dBox = await dest.BoundingBoxAsync();
                        if (sBox != null && dBox != null)
                        {
                            var startX = sBox.X + sBox.Width / 2;
                            var startY = sBox.Y + sBox.Height / 2;
                            var endX = dBox.X + dBox.Width / 2;
                            var endY = dBox.Y + dBox.Height / 2;
                            await _page.Mouse.MoveAsync(startX, startY);
                            await _page.Mouse.DownAsync();
                            await _page.Mouse.MoveAsync(endX, endY, new MouseMoveOptions { Steps = 10 });
                            await _page.Mouse.UpAsync();
                            clicked = true;
                        }
                        else
                        {
                            Console.WriteLine("[MoveDiag] Mouse fallback: bounding boxes unavailable");
                        }
                    }
                    catch (Exception mex)
                    {
                        Console.WriteLine($"[MoveDiag] Mouse fallback failed: {mex.GetType().Name}: {mex.Message}");
                    }
                }
            }

            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try
                {
                    var newCount = await dest.Locator(".playing-card").CountAsync();
                    Console.WriteLine($"[MoveDiag] Checking foundation count: {newCount}");
                    if (newCount > initialCount) return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MoveDiag] Counting foundation cards threw: {ex.GetType().Name}: {ex.Message}");
                }
                await Task.Delay(100);
            }

            Console.WriteLine("[MoveDiag] Move attempt timed out without foundation count increasing");
            return false;
        }

        private static string FormatBox(Microsoft.Playwright.LocatorBoundingBoxResult? box)
        {
            if (box == null) return "null";
            return $"x={box.X:0.##},y={box.Y:0.##},w={box.Width:0.##},h={box.Height:0.##}";
        }

        // Click-based move: freecell -> foundation
        public async Task MoveFreeCellToFoundationAsync(int freeCellIndex = 1, int foundationIndex = 1)
        {
            var source = _page.Locator($".free-cell:nth-child({freeCellIndex}) .playing-card");
            // If free cell is empty the locator may not match; guard
            var count = await source.CountAsync();
            if (count == 0) throw new InvalidOperationException($"Free cell {freeCellIndex} is empty");

            await source.First.ClickAsync();
            await Task.Delay(100);

            var dest = _page.Locator($".foundation-pile:nth-child({foundationIndex})");
            await dest.ClickAsync();
            await Task.Delay(100);
        }

        // Click-based move: freecell -> tableau column
        public async Task MoveFreeCellToTableauAsync(int freeCellIndex = 1, int destColumnIndex = 1)
        {
            var source = _page.Locator($".free-cell:nth-child({freeCellIndex}) .playing-card");
            var count = await source.CountAsync();
            if (count == 0) throw new InvalidOperationException($"Free cell {freeCellIndex} is empty");

            await source.First.ClickAsync();
            await Task.Delay(100);

            var dest = _page.Locator($".tableau-column:nth-child({destColumnIndex})");
            await dest.ClickAsync();
            await Task.Delay(100);
        }

        // Drag a card or stack from a tableau column to another tableau column
        public async Task MoveStackToColumnAsync(int srcColumnIndex, int cardIndexFromTop, int destColumnIndex)
        {
            var destSelector = $".tableau-column:nth-child({destColumnIndex})";
            await DragCardOrStackAsync(srcColumnIndex, cardIndexFromTop, destSelector);
            await Task.Delay(100);
        }
    }
}
