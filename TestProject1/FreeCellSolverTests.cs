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

        public async Task<IPage> GetPageForGame(int gameId, TaskCompletionSource<bool> tcsPageClosed)
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
            await NavigateToBlazorPageAsync(page, $"/freecell/{gameId}", ".freecell-container");

            // Wait for user to close the browser
            page.Close += (_, _) =>
            {
                Console.WriteLine("[Event] Page.Close event fired");
                tcsPageClosed.TrySetResult(true);
            };

            context.Close += (_, _) =>
            {
                Console.WriteLine("[Event] Context.Close event fired");
                tcsPageClosed.TrySetResult(true);
            };
            await Task.Delay(1000);
            var newButton = page.Locator("button:has-text('New')");
            await newButton.ClickAsync();
            await Task.Delay(300);
            var gamebutton = page.Locator($"button:has-text('replay #{gameId}')");
            await gamebutton.ClickAsync();
            await Task.Delay(300);
            return page;
        }
        [TestMethod]
        public async Task AutoSolve_FreeCellSimple()
        {
            var gameId = 12345;
            var pageClosedTcs = new TaskCompletionSource<bool>();
            var page = await GetPageForGame(gameId, pageClosedTcs);

            var mover = await FreeCellMover.CreateAsync(page);
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

            //var registered = await page.EvaluateAsync<bool>("() => !!window.freecellBlazorComponent && !!window.freecellBlazorComponent.invokeMethodAsync");
            //if (!registered)
            //{
            //    Console.WriteLine($"[Interop] Blazor component NOT registered or missing invokeMethodAsync");
            //}
            //var json = await page.EvaluateAsync<string>("() => window.getFreeCellStateJson()");
            //if (string.IsNullOrEmpty(json))
            //{
            //    Console.WriteLine($"[Interop] getFreeCellStateJson returned empty");
            //}
            //FreeCellGameService? freecellGameService = null;
            //try
            //{
            //    freecellGameService = FreeCellGameService.FromJson(json!);
            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine($"[Interop] Failed to deserialize FreeCell state: {ex.Message}");
            //}

            //Console.WriteLine($"{freecellGameService?.Tableau.Count ?? 0} columns in tableau according to interop JSON");
            //Assert.AreEqual(8, freecellGameService?.Tableau.Count, "Should have 8 columns in tableau");
            //// dump out the cards in each column according to the interop JSON
            //for (int col = 0; col < freecellGameService?.Tableau.Count; col++)
            //{
            //    var columnCards = freecellGameService.Tableau[col];
            //    var cardList = columnCards.Select(c => $"{c.Rank}{c.Suit}").ToList();
            //    Console.WriteLine($"Column {col + 1}: {string.Join(", ", cardList)}");
            //}

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
            await mover.MoveTableauToTableauAsync(srcColumnIndex: 7, destColumnIndex: 4, cardCount: 1);
            await mover.MoveTableauToTableauAsync(srcColumnIndex: 4, destColumnIndex: 1, cardCount: 2);
            await mover.MoveTableauToFreeCellAsync(columnIndex: 4, freeCellIndex: 0);
            await mover.MoveTableauToFreeCellAsync(columnIndex: 4, freeCellIndex: 1);
            await mover.MoveTableauToFreeCellAsync(columnIndex: 4, freeCellIndex: 2);
            await mover.MoveTableauToFreeCellAsync(columnIndex: 4, freeCellIndex: 3);
            await mover.MoveFreeCellToTableauAsync(freeCellIndex: 3, columnIndex: 2);
            mover.dumpAllToLog("After some moves");
            /*
                FreeCells:   8♥   3♣   3♥      
            Tableau:
             Q♣ 10♠  Q♦  7♥      Q♥  4♠  4♦ 
             9♣  4♣  2♠  J♣      3♦  9♥  5♣ 
             2♥  8♠ 10♦  6♥      5♦  5♥  A♦ 
             2♣ 10♥  K♥  K♠      A♣ 10♣  J♥ 
             8♣  A♥  J♠  9♠      7♣  6♦  6♠ 
             8♦  J♦  K♣  K♦      9♦  7♠     
             2♦  5♠  7♦  Q♠                 
                 4♥  6♣                     
                 3♠                         
            Foundations:  A♠         
             */
            Assert.IsTrue(mover.gameService.FreeCells[0]!.ToString() == " 8♥", $"Expected ' 8♥' got {mover.gameService.FreeCells[0]!.ToString()}");
            Assert.IsTrue(mover.gameService.Foundations[0][^1]!.ToString() == " A♠", $"Expected  A♠, got {mover.gameService.Foundations[0][^1]!.ToString()}");
            Assert.IsTrue(mover.gameService.Tableau[0][^1].ToString() == " 2♦", $"Expected ' 2♦' got {mover.gameService.Tableau[0][^1].ToString()}");

            await mover.Undo();
            await mover.Undo();
            await mover.Undo();
            await mover.Undo();
            await mover.Undo();
            await mover.Undo();
            await mover.Undo();


            var tableauColumns = page.Locator(".tableau-column");

            await Task.Delay(8000);
            pageClosedTcs.TrySetResult(true); // Reset in case of multiple events

            await pageClosedTcs.Task;

            Console.WriteLine("Browser closed. Test ending.");
        }
        [TestMethod]
        [TestCategory("Manual")]
        public async Task AutoSolve_FreeCell()
        {
            var gameId = 12345;
            var pageClosedTcs = new TaskCompletionSource<bool>();
            var page = await GetPageForGame(gameId, pageClosedTcs);

            var mover = await FreeCellMover.CreateAsync(page);
            var solver = await FreeCellSolver.CreateAsync(mover.gameService);


            await pageClosedTcs.Task;
        }
        public class FreeCellMove
        {
            /*
             * A move can be between any combination of Foundation/Freecell/Tableau
             * If tableau to tableau, source and target index are column indexes and cardCount is how many cards from the bottom of the source column
             */
            public SourceType sourceType { get; set; }
            public SourceType targetType { get; set; }
            public int sourceIndex { get; set; }
            public int targetIndex { get; set; }
            public int srcColumnIndex { get; set; } // only for tableau to tableau moves
            public int dstColumnIndex { get; set; } // only for tableau to tableau moves
            public int cardCount { get; set; } // only for tableau to tableau moves, how many cards from the bottom of the source column

            public int score { get; set; }
        }
        public class FreeCellSolver
        {
            private FreeCellGameService gameService;

            public FreeCellSolver(FreeCellGameService gameService)
            {
                this.gameService = gameService;
            }

            public static async Task<FreeCellSolver> CreateAsync(FreeCellGameService gameService)
            {
                var solver = new FreeCellSolver(gameService);
                return solver;
            }
            public List<FreeCellMove> FindMoves()
            {
                int maxMove = gameService.MaxMovableCards;
                int nFreeCells = gameService.EmptyFreeCellCount;
                // first see if any of the freecells can be moved to a foundation or tableau
                foreach (var freecell in gameService.FreeCells)
                {
                }
                for (int i = 0; i < gameService.Tableau.Count; i++)
                {
                    var column = gameService.Tableau[i];
                    if (column.Count == 0) continue;
                    var topCard = column[^1];
                    // Check if we can move this card to a foundation
                    //if (CanMoveToFoundation(topCard))
                    //{
                    //    Console.WriteLine($"Can move {topCard} from tableau column {i + 1} to foundation");
                    //}
                    // Check if we can move this card to a free cell
                    if (nFreeCells > 0)
                    {
                        Console.WriteLine($"Can move {topCard} from tableau column {i + 1} to a free cell");
                    }
                }
                return new List<FreeCellMove>();
            }
        }
    }
}
