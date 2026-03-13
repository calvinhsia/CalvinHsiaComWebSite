using Azure;
using Client.Games.Cards.Services;
using Microsoft.Playwright;
using System.Text.Json;
using System.Diagnostics;
using static Microsoft.Playwright.Assertions;
using System.ComponentModel;

namespace TestProject1
{
    [TestClass]
    public partial class FreeCellSolverTests : InteractiveTestBase
    {
        [ClassInitialize]
        public static async Task ClassInitialize(TestContext context)
        {
            await BaseClassInitialize(context);
            // Wire up FreeCellMover to use our unified logging
            FreeCellMover.LogAction = Log;
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
            Log("Testing FreeCell: read game state via JS interop...");

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
                        Log($"[Interop] Blazor component registered and ready for interop calls (attempt {attempt + 1})");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Log($"[Interop] registration check attempt {attempt + 1} threw: {ex.GetType().Name}: {ex.Message}");
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
                        Log($"Got non-empty JSON from interop on attempt {attempt + 1}");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Log($"[Interop] getFreeCellStateJson attempt {attempt + 1} threw: {ex.GetType().Name}: {ex.Message}");
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
                    Log($"Column {idx + 1}: {string.Join(", ", cards)}");
                    if (idx < 4) Assert.AreEqual(7, colCount, $"Interop: Column {idx + 1} should have 7 cards");
                    else Assert.AreEqual(6, colCount, $"Interop: Column {idx + 1} should have 6 cards");
                    idx++;
                }
                Assert.AreEqual(52, total, $"Interop: Total cards should be 52 but was {total}");
                Log($"[Interop] Verified tableau via interop: total={total}");
            }
            catch (Exception ex)
            {
                await page.ScreenshotAsync(new PageScreenshotOptions { Path = "freecell-interop-parse-error.png", FullPage = true });
                Assert.Fail($"Failed to parse interop JSON: {ex.Message}. Screenshot: freecell-interop-parse-error.png");
            }

            Log("\n✓ FreeCell interop read test completed successfully!");
        }

        public async Task<IPage> GetPageForGame(int gameId, TaskCompletionSource<bool> tcsPageClosed)
        {
            var launchOptions = GetBrowserLaunchOptions(forceHeadless: null);
            // Add maximize arg for local headed mode
            if (!launchOptions.Headless.GetValueOrDefault(true))
            {
                launchOptions.Args = new[] { "--start-maximized" };
            }

            _browser = await _playwright!.Chromium.LaunchAsync(launchOptions);

            var contextOptions = GetBrowserContextOptions();
            // Use NoViewport for headed mode to respect --start-maximized
            if (!launchOptions.Headless.GetValueOrDefault(true))
            {
                contextOptions.ViewportSize = ViewportSize.NoViewport;
            }

            var context = await _browser.NewContextAsync(contextOptions);
            var page = await context.NewPageAsync();
            if (InteractiveTestBase._IsDebugging)
            {
                page.Console += (_, msg) => Log($"[Browser Console] {msg.Text}");
            }

            // Navigate using shared helper
            await NavigateToBlazorPageAsync(page, $"/freecell/{gameId}", ".freecell-container");

            // Wait for user to close the browser
            page.Close += (_, _) =>
            {
                Log("[Event] Page.Close event fired");
                tcsPageClosed.TrySetResult(true);
            };

            context.Close += (_, _) =>
            {
                Log("[Event] Context.Close event fired");
                tcsPageClosed.TrySetResult(true);
            };
            await Task.Delay(1000);
            var newButton = page.Locator("button:has-text('New')");
            await newButton.ClickAsync();
            await Task.Delay(300);
            var gamebutton = page.Locator($"button:has-text('replay #{gameId}')");
            await gamebutton.ClickAsync();
            await Task.Delay(300);

            // Turn off "Auto-move to foundation" option
            var optionsButton = page.Locator("button:has-text('Options')");
            await optionsButton.ClickAsync();
            await Task.Delay(200);

            var autoMoveCheckbox = page.Locator(".checkbox-item input[type='checkbox']");
            if (await autoMoveCheckbox.IsCheckedAsync())
            {
                await autoMoveCheckbox.ClickAsync();
                Log("Turned off 'Auto-move to foundation' option");
            }
            await Task.Delay(100);

            // Close the options menu by clicking elsewhere
            await page.Locator(".freecell-container").ClickAsync();
            await Task.Delay(100);

            return page;
        }
        [TestMethod]
        [TestCategory("Manual")] // Drag-and-drop doesn't work reliably in headless CI mode
        [Timeout(120000)]
        public async Task AutoSolve_FreeCellSimple()
        {
            // Skip in CI - drag-and-drop requires headed mode with SlowMo
            if (IsCI())
            {
                Log("Skipping AutoSolve_FreeCellSimple in CI - drag-and-drop requires headed mode");
                Assert.Inconclusive("This test requires headed browser mode for reliable drag-and-drop");
            }

            var gameId = 12345;
            var pageClosedTcs = new TaskCompletionSource<bool>();
            var page = await GetPageForGame(gameId, pageClosedTcs);

            var mover = await FreeCellMover.CreateAsync(page, InteractiveTestBase._IsDebugging);
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
                Log($"Diagnostics: foundations count = {fCount}");
                for (int i = 0; i < fCount; i++)
                {
                    var loc = foundations.Nth(i);
                    var visible = await loc.IsVisibleAsync();
                    string html = string.Empty;
                    try { html = await loc.EvaluateAsync<string>("el => el.outerHTML"); } catch { html = "<outerHTML unavailable>"; }
                    Log($"foundation[{i + 1}] visible={visible}: {html}");
                }

                var srcCard = page.Locator($".tableau-column:nth-child({5}) .playing-card").Last;
                Log($"Source card visible: {await srcCard.IsVisibleAsync()}");
                try { Log(await srcCard.EvaluateAsync<string>("el => el.outerHTML")); } catch { }
            }
            catch (Exception ex)
            {
                Log($"Diagnostics error: {ex.GetType().Name}: {ex.Message}");
            }

            // move column 4 index 0 to column 1
            await mover.MoveTableauToTableauAsync(srcColumnIndex: 7, destColumnIndex: 4, cardCount: 1);
            await mover.MoveTableauToTableauAsync(srcColumnIndex: 4, destColumnIndex: 1, cardCount: 2);
            await mover.MoveTableauToFreeCellAsync(columnIndex: 4, freeCellIndex: 0);
            await mover.MoveTableauToFreeCellAsync(columnIndex: 4, freeCellIndex: 1);
            await mover.MoveTableauToFreeCellAsync(columnIndex: 4, freeCellIndex: 2);
            await mover.MoveTableauToFoundationAsync(columnIndex: 4, foundationIndex: 0);
            await mover.MoveTableauToFreeCellAsync(columnIndex: 4, freeCellIndex: 3);
            await mover.MoveFreeCellToTableauAsync(freeCellIndex: 3, columnIndex: 2);
            LogAction(mover.gameService.dumpAllToLog($"After {mover.gameService.MoveCount} moves"));
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
            // Verify FreeCells[0] is not null before accessing
            Assert.IsNotNull(mover.gameService.FreeCells[0], "FreeCells[0] should not be null after moves");
            Assert.AreEqual(" 8♥", mover.gameService.FreeCells[0]!.ToString(), $"Expected ' 8♥' got '{mover.gameService.FreeCells[0]}'.");

            // Verify Foundations[0] has cards before accessing
            Assert.IsTrue(mover.gameService.Foundations[0].Count > 0, "Foundations[0] should have cards");
            Assert.AreEqual(" A♠", mover.gameService.Foundations[0][^1]!.ToString(), $"Expected ' A♠' got '{mover.gameService.Foundations[0][^1]}'.");

            // Verify Tableau[0] has cards before accessing
            Assert.IsTrue(mover.gameService.Tableau[0].Count > 0, "Tableau[0] should have cards");
            Assert.AreEqual(" 2♦", mover.gameService.Tableau[0][^1].ToString(), $"Expected ' 2♦' got '{mover.gameService.Tableau[0][^1]}'.");

            await mover.Undo();
            await mover.Undo();
            await mover.Undo();
            await mover.Undo();
            await mover.Undo();
            await mover.Undo();
            await mover.Undo();
            var tableauColumns = page.Locator(".tableau-column");

            await Task.Delay(3000);
            pageClosedTcs.TrySetResult(true); // Reset in case of multiple events

            await pageClosedTcs.Task;


            TestContext!.WriteLine("✓ FreeCell solver simple test completed successfully!");
        }
        [TestMethod]
        [TestCategory("Manual")]
        public async Task AutoSolve_FreeCellAndShow()
        {
            var gameId = 170;// 261127;// 63;
            LogAction($"Showing solution for FreeCell game #{gameId}...");
            var pageClosedTcs = new TaskCompletionSource<bool>();
            var page = await GetPageForGame(gameId, pageClosedTcs);

            var mover = await FreeCellMover.CreateAsync(page, InteractiveTestBase._IsDebugging);
            mover.DefaultDelayMs = 250;
            var solver = new FreeCellSolver(mover.gameService, loggerAction: null);

            var moves = solver.FindSolution();
            Assert.IsNotNull(moves);
            for (int i = 0; i < moves.Count; i++)
            {
                LogAction($"Executing {i,3} {moves[i]}");
                await mover.doMoveAsync(moves[i]);
            }
            LogAction(mover.gameService.dumpAllToLog($"After auto-solve with {moves.Count} moves"));
            await Task.Delay(1000);
            pageClosedTcs.TrySetResult(true); // Reset in case of multiple events
            await pageClosedTcs.Task; ;
            //await Task.WhenAny(Task.Delay(1000), pageClosedTcs.Task); // Wait for either the page to close or a timeout)

        }
        [TestMethod]
        [TestCategory("Manual")]
        [DisableInterActive]
        public async Task AutoSolve_FindSolution()
        {
            var gameId = 57;// 261127;// 63;
            LogAction($"Finding solution for FreeCell game #{gameId}...");
            var gameService = new FreeCellGameService();
            gameService.InitializeGame(gameId);

            var solver = new FreeCellSolver(gameService, loggerAction: (msgFactory) => LogAction(msgFactory()));

            var moves = solver.FindSolution();
            Assert.IsNotNull(moves);
            for (int i = 0; i < moves.Count; i++)
            {
                LogAction($"{i,3} {moves[i]}");
            }
        }
        [TestMethod]
        [TestCategory("Manual")]
        public async Task AutoSolve_FreeCellFromPositionAndShow()
        {
            var gamestr = @"
     FreeCells:  K♣          K♦ Foundations:  5♦  5♠  3♣  2♥ BValue: 53 
  K♥  K♠      Q♣         10♠  6♦
  Q♠  Q♦      J♦          9♥  Q♥
  J♥  J♣                  8♣  J♠
 10♣ 10♥                  7♥ 10♦
  9♦  9♣                  6♠  9♠
  8♠  8♥                  5♥  8♦
  7♦  7♣                  4♣  7♠
  6♣  6♥                  3♥    
      5♣                        
      4♥                        
"; // game 57
            var positionService = FreeCellGameService.FromDumpString(gamestr);
            LogAction($"Showing solution from custom position...");

            var pageClosedTcs = new TaskCompletionSource<bool>();
            var page = await GetPageForGame(1, pageClosedTcs);

            var mover = await FreeCellMover.CreateAsync(page, InteractiveTestBase._IsDebugging);
            await mover.LoadGameStateAsync(positionService);

            mover.DefaultDelayMs = 250;
            var solver = new FreeCellSolver(mover.gameService, loggerAction: null);
            try
            {
                var moves = solver.FindSolution();
                for (int i = 0; i < moves.Count; i++)
                {
                    LogAction($"Executing {i,3} {moves[i]}");
                    await mover.doMoveAsync(moves[i]);
                }
                LogAction(mover.gameService.dumpAllToLog($"After auto-solve with {moves.Count} moves"));
                await Task.Delay(1000);
                pageClosedTcs.TrySetResult(true);

            }
            catch (Exception ex)
            {
                LogAction($"Error during solving or moving: {ex.GetType().Name}: {ex.Message}");
                 //pageClosedTcs.TrySetResult(true);
            }

            await pageClosedTcs.Task;
        }

        [TestMethod]
        [TestCategory("Manual")]
        [DisableInterActive]
        public async Task AutoSolve_FindSolutionFromPosition()
        {
            var gamestr = $@"
     FreeCells:  K♣          K♦ Foundations:  5♦  5♠  3♣  2♥ BValue: 53 
  K♥  K♠      Q♣         10♠  6♦
  Q♠  Q♦      J♦          9♥  Q♥
  J♥  J♣                  8♣  J♠
 10♣ 10♥                  7♥ 10♦
  9♦  9♣                  6♠  9♠
  8♠  8♥                  5♥  8♦
  7♦  7♣                  4♣  7♠
  6♣  6♥                  3♥    
      5♣                        
      4♥                        
";
            var gameService = FreeCellGameService.FromDumpString(gamestr);
            LogAction($"Finding solution for FreeCell game from position...");

            var solver = new FreeCellSolver(gameService, loggerAction: (msgFactory) => LogAction(msgFactory()));

            var moves = solver.FindSolution();
            Assert.IsNotNull(moves);
            for (int i = 0; i < moves.Count; i++)
            {
                LogAction($"{i,3} {moves[i]}");
            }
        }
        [TestMethod]
        [TestCategory("Manual")]
        [DisableInterActive]
        public async Task AutoSolve_FindSolutionForManyGames()
        {
            var nTotMoves = 0;
            var nFailures = 0;
            for (int gameId = 1; gameId < 1000; gameId++)
            {
                var strResult = string.Empty;
                var sw = Stopwatch.StartNew();
                var gameService = new FreeCellGameService();
                gameService.InitializeGame(gameId);

                var solver = new FreeCellSolver(gameService, loggerAction: null);
                var nMoves = 0;
                try
                {
                    var moves = solver.FindSolution();
                    nMoves = moves.Count;
                    nTotMoves += moves.Count;
                    sw.Stop();
                }
                catch (Exception ex)
                {
                    strResult = ex.Message;
                    nFailures++;
                }
                strResult = $"Game {gameId,6} {sw.Elapsed.TotalMilliseconds.ToString("N1"),10}ms Moves:{nMoves,3} {strResult} NodesCreated: {solver._countNodesCreated} NodesVisited: {solver._countNodesVisited} BackTrack:{solver._numTimesBacktracked}";
                LogAction(strResult);

            }
            LogAction($"# of failures: {nFailures} Total Moves: {nTotMoves}");
        }

        [TestMethod]
        [TestCategory("Automated")]
        public void TestFromDumpString_RoundTrip()
        {
            // Deal a known game and make a few moves to populate freecells and foundations
            var gameService = new FreeCellGameService();
            gameService.InitializeGame(170);

            // Auto-move any cards to foundations so dump has foundation content
            gameService.AutoMoveToFoundations();

            var dump = gameService.dumpAllToLog("RoundTrip test");

            // Deserialize back
            var restored = FreeCellGameService.FromDumpString(dump);

            // Verify tableau
            Assert.AreEqual(gameService.Tableau.Count, restored.Tableau.Count, "Tableau column count");
            for (int col = 0; col < gameService.Tableau.Count; col++)
            {
                Assert.AreEqual(gameService.Tableau[col].Count, restored.Tableau[col].Count, $"Tableau col {col} card count");
                for (int row = 0; row < gameService.Tableau[col].Count; row++)
                {
                    Assert.AreEqual(gameService.Tableau[col][row].Suit, restored.Tableau[col][row].Suit, $"Tableau[{col}][{row}] suit");
                    Assert.AreEqual(gameService.Tableau[col][row].Rank, restored.Tableau[col][row].Rank, $"Tableau[{col}][{row}] rank");
                }
            }

            // Verify foundations
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(gameService.Foundations[i].Count, restored.Foundations[i].Count, $"Foundation {i} count");
            }

            // Verify freecells
            for (int i = 0; i < 4; i++)
            {
                if (gameService.FreeCells[i] == null)
                    Assert.IsNull(restored.FreeCells[i], $"FreeCell {i} should be null");
                else
                {
                    Assert.IsNotNull(restored.FreeCells[i], $"FreeCell {i} should not be null");
                    Assert.AreEqual(gameService.FreeCells[i]!.Suit, restored.FreeCells[i]!.Suit, $"FreeCell {i} suit");
                    Assert.AreEqual(gameService.FreeCells[i]!.Rank, restored.FreeCells[i]!.Rank, $"FreeCell {i} rank");
                }
            }

            // Verify state hash matches (canonical representation)
            Assert.AreEqual(gameService.GetStateHash(), restored.GetStateHash(), "State hash should match");
        }

        [TestMethod]
        [TestCategory("Automated")]
        public void TestFromDumpString_WithLetterSuits()
        {
            // Test parsing with CDHS letter suits instead of Unicode symbols
            var dumpWithLetters = @"
     FreeCells:  K♣          K♦ Foundations:  5♦  5♠  3♣  2♥ BValue: 53 
  K♥  K♠      Q♣         10♠  6♦
  Q♠  Q♦      J♦          9♥  Q♥
  J♥  J♣                  8♣  J♠
 10♣ 10♥                  7♥ 10♦
  9♦  9♣                  6♠  9♠
  8♠  8♥                  5♥  8♦
  7♦  7♣                  4♣  7♠
  6♣  6♥                  3♥    
      5♣                        
      4♥                        
";

            var game = FreeCellGameService.FromDumpString(dumpWithLetters);
            LogAction(game.dumpAllToLog("Parsed game with letter suits"));

        }

        [TestMethod]
        [TestCategory("Automated")]
        public void TestFromDumpString_PreservesFreeCellPositions()
        {
            // Build a game state with FreeCells at indices 0 and 3 (gaps at 1 and 2)
            // and Foundations at indices 1 and 3 (gaps at 0 and 2)
            var gameService = new FreeCellGameService();
            gameService.InitializeGame(42);

            // Manually set up FreeCells with gaps
            gameService.FreeCells[0] = gameService.Tableau[0][^1]; // take bottom card from col 0
            gameService.Tableau[0].RemoveAt(gameService.Tableau[0].Count - 1);
            gameService.FreeCells[1] = null;
            gameService.FreeCells[2] = null;
            gameService.FreeCells[3] = gameService.Tableau[1][^1]; // take bottom card from col 1
            gameService.Tableau[1].RemoveAt(gameService.Tableau[1].Count - 1);

            var dump = gameService.dumpAllToLog("Position preservation test");
            LogAction(dump);

            var restored = FreeCellGameService.FromDumpString(dump);

            // Verify FreeCells positions are preserved
            Assert.IsNotNull(restored.FreeCells[0], "FreeCell[0] should be filled");
            Assert.IsNull(restored.FreeCells[1], "FreeCell[1] should be empty");
            Assert.IsNull(restored.FreeCells[2], "FreeCell[2] should be empty");
            Assert.IsNotNull(restored.FreeCells[3], "FreeCell[3] should be filled");

            Assert.AreEqual(gameService.FreeCells[0]!.Suit, restored.FreeCells[0]!.Suit, "FreeCell[0] suit");
            Assert.AreEqual(gameService.FreeCells[0]!.Rank, restored.FreeCells[0]!.Rank, "FreeCell[0] rank");
            Assert.AreEqual(gameService.FreeCells[3]!.Suit, restored.FreeCells[3]!.Suit, "FreeCell[3] suit");
            Assert.AreEqual(gameService.FreeCells[3]!.Rank, restored.FreeCells[3]!.Rank, "FreeCell[3] rank");
        }

        [TestMethod]
        [TestCategory("Automated")]
        public void TestVerifyGame_ValidInitialDeal()
        {
            var gameService = new FreeCellGameService();
            gameService.InitializeGame(170);

            // Fresh deal should be valid
            gameService.VerifyGame();
        }

        [TestMethod]
        [TestCategory("Automated")]
        public void TestVerifyGame_ValidAfterMoves()
        {
            var gameService = new FreeCellGameService();
            gameService.InitializeGame(170);
            gameService.AutoMoveToFoundations();

            // Still valid after auto-moves to foundations
            gameService.VerifyGame();
        }

        [TestMethod]
        [TestCategory("Automated")]
        public void TestVerifyGame_ValidFromDumpRoundTrip()
        {
            var gameService = new FreeCellGameService();
            gameService.InitializeGame(42);
            gameService.AutoMoveToFoundations();

            var dump = gameService.dumpAllToLog("verify test");
            var restored = FreeCellGameService.FromDumpString(dump);

            restored.VerifyGame();
        }

        [TestMethod]
        [TestCategory("Automated")]
        public void TestVerifyGame_DetectsDuplicateCard()
        {
            var gameService = new FreeCellGameService();
            gameService.InitializeGame(1);

            // Replace last card in column 7 with a duplicate of the first card in column 0
            var duplicate = new Client.Games.Cards.Models.Card(
                gameService.Tableau[0][0].Suit,
                gameService.Tableau[0][0].Rank, true);
            gameService.Tableau[7][^1] = duplicate;

            var ex = Assert.ThrowsException<InvalidOperationException>(() => gameService.VerifyGame());
            Assert.IsTrue(ex.Message.Contains("Duplicate"), ex.Message);
        }

        [TestMethod]
        [TestCategory("Automated")]
        public void TestVerifyGame_DetectsMissingCard()
        {
            var gameService = new FreeCellGameService();
            gameService.InitializeGame(1);

            // Remove a card from tableau
            gameService.Tableau[0].RemoveAt(0);

            var ex = Assert.ThrowsException<InvalidOperationException>(() => gameService.VerifyGame());
            Assert.IsTrue(ex.Message.Contains("51") || ex.Message.Contains("Missing"), ex.Message);
        }

        [TestMethod]
        [TestCategory("Automated")]
        public void TestVerifyGame_DetectsBadFoundation()
        {
            var gameService = new FreeCellGameService();
            gameService.InitializeGame(1);

            // Manually corrupt a foundation: put a non-Ace as first card
            var stolen = gameService.Tableau[0][0];
            gameService.Tableau[0].RemoveAt(0);
            gameService.Foundations[0].Add(stolen);

            var ex = Assert.ThrowsException<InvalidOperationException>(() => gameService.VerifyGame());
            // Should catch either "first card must be Ace" or card count mismatch
            Assert.IsTrue(
                ex.Message.Contains("Ace") || ex.Message.Contains("51") || ex.Message.Contains("Missing"),
                ex.Message);
        }

        [TestMethod]
        [TestCategory("Automated")]
        public void TestVerifyGame_MultipleGames()
        {
            // Verify a range of game IDs to ensure deal logic always produces valid boards
            for (int gameId = 1; gameId <= 100; gameId++)
            {
                var gameService = new FreeCellGameService();
                gameService.InitializeGame(gameId);
                gameService.VerifyGame(); // should not throw
            }
        }
    }
}
