using Azure;
using Client.Games.Cards.Services;
using Microsoft.Playwright;
using System.Text.Json;
using System.Diagnostics;
using System.IO;
using System.Linq;
using static Microsoft.Playwright.Assertions;
using System.ComponentModel;

namespace TestProject1
{
    /// <summary>
    /// Represents a single row of solver statistics for a game run.
    /// Used by both CSV export (Excel) and nicely-formatted text output.
    /// </summary>
    public record SolverStatsRow(
        int GameId,
        double TimeMs,
        int Moves,
        int Nodes,
        int Visit,
        int MaxDepth,
        int BTrack,
        int Uber,
        int FndToTabl,
        int Mega,
        int Split,
        int Abut,
        int Neut,
        int Order,
        int InsertUnder,
        int BurFndRdy,
        int FCSeq,
        int ColClr,
        int MaxLkAhd,
        string Status)
    {
        /// <summary>Column headers matching the record properties, in order.</summary>
        public static readonly string[] Headers =
            ["Game", "TimeMs", "Moves", "Nodes", "Visit", "MaxDepth", "BTrack", "Uber",
             "Fnd=>Tabl", "Mega", "Split", "Abut", "Neut", "Order",
             "InsertUnder", "BurFndRdy", "FCSeq", "ColClr", "MaxLkAhd", "Stat"];

        /// <summary>Number of numeric stat columns (excludes the trailing Status string column).</summary>
        public static int LastNumericStatCol => Headers.Length - 1;

        public static string CsvHeader => string.Join(",", Headers);

        public static SolverStatsRow FromSolver(FreeCellSolver solver, int gameId, int nMoves, double timeMs, string status) => new(
            GameId: gameId,
            TimeMs: timeMs,
            Moves: nMoves,
            Nodes: solver._countNodesCreated,
            Visit: solver._countNodesVisited,
            MaxDepth: solver._maxDepth,
            BTrack: solver._numTimesBacktracked,
            Uber: solver._countNumberUberBacktrack,
            FndToTabl: solver._countNumberOfMovesFromFoundationToTableau,
            Mega: solver._countMegaMoves,
            Split: solver._countSplitMoves,
            Abut: solver._countAbutMoves,
            Neut: solver._countNeutralMoves,
            Order: solver._countOrderChangingMoves,
            InsertUnder: solver._countInsertUnderMoves,
            BurFndRdy: solver._countBuriedFndReady,
            FCSeq: solver._countFreeCellSeqMoves,
            ColClr: solver._countColumnClearAttempts,
            MaxLkAhd: solver._countMaxLookAhead,
            Status: status);

        /// <summary>All values as an object array (same order as Headers).</summary>
        public object[] ToValues() =>
            [GameId, TimeMs, Moves, Nodes, Visit, MaxDepth, BTrack, Uber,
             FndToTabl, Mega, Split, Abut, Neut, Order,
             InsertUnder, BurFndRdy, FCSeq, ColClr, MaxLkAhd, Status];
        public string ToCsvLine() => string.Join(",", ToValues());

        /// <summary>
        /// Nicely-formatted multi-line text summary suitable for test output.
        /// </summary>
        public string ToFormattedText()
        {
            var values = ToValues();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("┌─────────────────────────────────────┐");
            sb.AppendLine("│       FreeCell Solver Stats         │");
            sb.AppendLine("├──────────────┬──────────────────────┤");
            for (int i = 0; i < Headers.Length; i++)
            {
                var val = values[i] is double d ? d.ToString("N1") : $"{values[i]:N0}";
                if (values[i] is string s) val = s;
                sb.AppendLine($"│ {Headers[i],-12} │ {val,20} │");
            }
            sb.AppendLine("└──────────────┴──────────────────────┘");
            return sb.ToString();
        }
    }

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
                //page.Console += (_, msg) => Log($"[Browser Console] {msg.Text}");
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

            var autoMoveCheckbox = page.GetByLabel("Auto-move to foundation");
            //            var autoMoveCheckbox = page.Locator(".checkbox-item input[type='checkbox']");

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
            var gameId = 368;// 850;// 1;// 617;// 836535;// 617;// 227;// 2971;// 295;// 261127;// 63;
            var visitedNodeCountAtWhichToStart = 0;
            var justShowSolution = false; // calculate all the moves of the solution first, with no backtracking
            LogAction($"Showing solution for FreeCell game #{gameId}({visitedNodeCountAtWhichToStart})...");
            var pageClosedTcs = new TaskCompletionSource<bool>();
            var page = await GetPageForGame(gameId, pageClosedTcs);

            var mover = await FreeCellMover.CreateAsync(page, isDebugging: false);
            mover.DefaultDelayMs = 250;
            try
            {
                if (justShowSolution)
                {
                    var solver = new FreeCellSolver(mover.gameService, loggerAction: null);
                    var moves = await solver.FindSolutionAsync();
                    int movno = 0;
                    foreach (var move in moves)
                    {
                        LogAction($"{++movno,3} {move}");
                        var success = await mover.doMoveAsync(move);
                        if (!success)
                        {
                            LogAction($"Failed to execute move on UI: {move}");
                            break;
                        }
                        await Task.Delay(100); // Add a small delay between moves for better visibility during debugging
                    }

                }
                else
                {
                    var solver = new FreeCellSolver(mover.gameService, loggerAction: (msgfactory) => LogAction(msgfactory()));
                    var initialPosition = solver._game.dumpAllToLog("Initial game state");
                    var didStartUIMoves = false;
                    solver.OnDoMove += async (move) =>
                    {
                        if (solver._countNodesVisited == visitedNodeCountAtWhichToStart && !didStartUIMoves)
                        {
                            didStartUIMoves = true;
                            var dump = solver._game.dumpAllToLog($"Starting UI moves at visited node count {solver.VisitedNodeCount}");
                            var srv = FreeCellGameService.FromDumpString(dump);
                            await mover.LoadGameStateAsync(srv);

                        }
                        if (didStartUIMoves)
                        {
                            var success = await mover.doMoveAsync(move);
                            if (!success)
                            {
                                LogAction($"Failed to execute move on UI: {move}");
                            }
                            await Task.Delay(100); // Add a small delay between moves for better visibility during debugging
                        }
                    };
                    solver.OnUndoMove += async (move) =>
                    {
                        if (didStartUIMoves)
                        {
                            await mover.PushGameStateToPageAsync(solver._game);
                        }
                    };

                    var moves = await solver.FindSolutionAsync();
                    Assert.IsNotNull(moves);
                    var srv = FreeCellGameService.FromDumpString(initialPosition);
                    await mover.LoadGameStateAsync(srv);
                    for (int i = 0; i < moves.Count; i++)
                    {
                        var move = moves[i];
                        LogAction($"Executing {i,3} {move}");
                        await mover.doMoveAsync(move);
                    }
                    LogAction(mover.gameService.dumpAllToLog($"After auto-solve with {moves.Count} moves"));
                }
            }
            catch (Exception ex)
            {
                LogAction($"Error during solving or move execution: {ex}");
            }
            await Task.Delay(1000);
            pageClosedTcs.TrySetResult(true); // Reset in case of multiple events
            await pageClosedTcs.Task; ;
            //await Task.WhenAny(Task.Delay(1000), pageClosedTcs.Task); // Wait for either the page to close or a timeout)

        }
        [TestMethod]
        [TestCategory("Manual")]
        [DisableInterActive]
        [Timeout(TestTimeout.Infinite)]
        public async Task AutoSolve_FindSolution()
        {
            /*
Failure: Game    295   31,275.5ms Moves:   0 Solver failed 5353 to find any moves, but game is not won. Visited 1924265 states. MaxDepth = 46656 Created: 3270357 Visited:3031694 BackTrack:2916283 Uber   101 Found=>Tabl:7991
             */
            var gameId = 2260;// 86;// 617;// 418;// 565315;// 368;// 850;// 617;// 227;// 93;// 277;// 295;// 617;//2971;// 599526;// 617;// 295;// 579 // 859619
            try
            {
                LogAction($"Finding solution for FreeCell game #{gameId}...");
                var gameService = new FreeCellGameService();
                gameService.InitializeGame(gameId);
                /*
    ========== Starting test run ==========
    Inner exception: Exception of type 'System.OutOfMemoryException' was thrown.

    Stack trace:
       at System.Text.StringBuilder.ToString()
       at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.ThreadSafeStringWriter.ThreadSafeStringBuilder.ToString() in /_/src/Adapter/MSTestAdapter.PlatformServices/Services/ThreadSafeStringWriter.cs:line 240
       at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.ThreadSafeStringWriter.ToString() in /_/src/Adapter/MSTestAdapter.PlatformServices/Services/ThreadSafeStringWriter.cs:line 67
       at Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.TestContextImplementation.GetDiagnosticMessages() in /_/src/Adapter/MSTestAdapter.PlatformServices/Services/TestContextImplementation.cs:line 337
       at Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions.TestContextExtensions.GetAndClearDiagnosticMessages(ITestContext testContext) in /_/src/Adapter/MSTest.TestAdapter/Extensions/TestContextExtensions.cs:line 16
       at Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.UnitTestRunner.RunRequiredCleanups(ITestContext testContext, TestMethodInfo testMethodInfo, TestMethod testMethod, UnitTestResult[] results) in /_/src/Adapter/MSTest.TestAdapter/Execution/UnitTestRunner.cs:line 208
       at Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.UnitTestRunner.RunSingleTest(TestMethod testMethod, IDictionary`2 testContextProperties) in /_/src/Adapter/MSTest.TestAdapter/Execution/UnitTestRunner.cs:line 153
       at Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestExecutionManager.ExecuteTestsWithTestRunner(IEnumerable`1 tests, ITestExecutionRecorder testExecutionRecorder, String source, IDictionary`2 sourceLevelParameters, UnitTestRunner testRunner) in /_/src/Adapter/MSTest.TestAdapter/Execution/TestExecutionManager.cs:line 400
       at Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution.TestExecutionManager.<>c__DisplayClass20_1.<ExecuteTestsInSource>b__6() in /_/src/Adapter/MSTest.TestAdapter/Execution/TestExecutionManager.cs:line 335
       at System.Threading.ExecutionContext.RunInternal(ExecutionContext executionContext, ContextCallback callback, Object state)
    --- End of stack trace from previous location ---
       at System.Threading.ExecutionContext.RunInternal(ExecutionContext executionContext, ContextCallback callback, Object state)
       at System.Threading.Tasks.Task.ExecuteWithThreadLocal(Task& currentTaskSlot, Thread threadPoolThread)             */

                var solver = new FreeCellSolver(gameService, loggerAction: (msgFactory) => LogAction(msgFactory()));
                var saveloggeraction = solver._LoggerAction;
                //solver._LoggerAction = null;
                //FreeCellSolver._multipleAtWhichToUberReverse = 50000;
                //LogAction = (s) => { }; // Suppress logging for this test to avoid OOM after 1.8 min
                //solver._allowFoundationToTableau = false;
                var sw = Stopwatch.StartNew();
                var moves = await solver.FindSolutionAsync();
                sw.Stop();
                Assert.IsNotNull(moves);
                for (int i = 0; i < moves.Count; i++)
                {
                    LogAction($"{i,3} {moves[i]}");
                }
                var stats = SolverStatsRow.FromSolver(solver, gameId, moves.Count, sw.Elapsed.TotalMilliseconds, "OK");
                LogAction(stats.ToFormattedText());
            }
            catch (Exception ex)
            {
                Assert.Fail(ex.ToString());
            }
        }

        public string gamestr = @"
No moves? Fnd>t
Game #676592 Moves: 60
 FreeCells:  J♣ 10♥  K♣  3♥ Foundations:  2♦  4♣  4♠  A♥ BValue: 13
  K♠  5♠  7♣  6♠  Q♦  7♥  6♣  K♥
  Q♥  4♦  3♦  5♦  7♦ 10♠  5♥  Q♣
  J♠      8♦      2♥  9♠      J♦
 10♦              6♦  K♦        
  9♣              9♥  Q♠        
  8♥              8♣  J♥        
  7♠                 10♣        
  6♥                  9♦        
  5♣                  8♠        
  4♥                            
MoveHistory:
  4♥:Col6>Col3
  4♠:Col6>Col2
  8♠:Col5>Col1
  J♥:Col0>Free0
  6♥:Col0>Col6
  3♥:Col0>Col2
  5♦:Col0>Free1
  9♦:Col1>Col0x2
  J♥:Free0>Col1
  10♣:Col0>Col1x3
  K♣:Col0>Free0
  2♠:Col0>Col2
  K♣:Free0>Col0
  10♦:Col4>Free3
  8♣:Col5>Col4
  K♣:Col0>Free2
  5♣:Col3>Col6x2
  10♦:Free3>Col3
  Q♠:Col1>Col5x5
  K♠:Col7>Col0
  A♦:Col7>Fnd0
  9♣:Col7>Col3
  5♦:Free1>Col7
  Q♥:Col1>Col0
  5♠:Col1>Free1
  4♣:Col1>Col7
  3♠:Col1>Free0
  4♦:Col1>Free3
  5♠:Free1>Col1
  4♦:Free3>Col1
  3♠:Free0>Col1
  J♠:Col3>Col0x3
  A♣:Col3>Fnd1
  8♥:Col3>Col0
  7♠:Col6>Col0x4
  2♣:Col6>Fnd1
  6♣:Col6>Free3
  A♠:Col6>Fnd2
  2♠:Col2>Fnd2
  3♠:Col1>Fnd2
  6♣:Free3>Col6
  5♥:Col2>Col6x3
  10♥:Col3>Col2
  2♦:Col3>Fnd0
  K♥:Col3>Free3
  K♥:Free3>Col3
  K♥:Col3>Free3
  6♠:Col7>Col3x3
  3♣:Col7>Fnd1
  4♣:Col3>Fnd1
  J♦:Col7>Free1
  J♣:Col2>Col7x2
  A♥:Col2>Fnd3
  J♦:Free1>Col2
  10♥:Col7>Free1
  J♣:Col7>Free0
  K♥:Free3>Col7
  Q♣:Col2>Col7x2
  3♥:Col6>Free3
  4♠:Col6>Fnd2

";
        [TestMethod]
        [TestCategory("Manual")]
        [Microsoft.VisualStudio.TestTools.UnitTesting.Description("Given an initial position, show it, then use the solver to do each move one at a time, so breakpoints can be used")]
        public async Task AutoSolve_FreeCellFromPositionAndShow()
        {
            var positionService = FreeCellGameService.FromDumpString(gamestr);
            LogAction($"Showing solution from custom position...");

            var pageClosedTcs = new TaskCompletionSource<bool>();
            var page = await GetPageForGame(1, pageClosedTcs);

            var mover = await FreeCellMover.CreateAsync(page, InteractiveTestBase._IsDebugging);
            await mover.LoadGameStateAsync(positionService);

            mover.DefaultDelayMs = 250;
            var solver = new FreeCellSolver(mover.gameService, loggerAction: (m) => LogAction(m()));
            try
            {
                solver.OnDoMove += async (move) =>
                {
                    await mover.doMoveAsync(move);
                    await Task.Delay(100); // Add a small delay between moves for better visibility during debugging
                };
                solver.OnUndoMove += async (move) =>
                {
                    await mover.PushGameStateToPageAsync(solver._game);
                };

                var moves = await solver.FindSolutionAsync();
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
            var gameService = FreeCellGameService.FromDumpString(gamestr);

            var solver = new FreeCellSolver(gameService, loggerAction: (msgFactory) => LogAction(msgFactory()));
            LogAction(solver._game.dumpAllToLog("Finding solution for FreeCell game from position"));

            var sw = Stopwatch.StartNew();
            var moves = await solver.FindSolutionAsync();
            sw.Stop();
            Assert.IsNotNull(moves);
            for (int i = 0; i < moves.Count; i++)
            {
                LogAction($"{i,3} {moves[i]}");
            }
            var stats = SolverStatsRow.FromSolver(solver, 0, moves.Count, sw.Elapsed.TotalMilliseconds, "OK");
            LogAction(stats.ToFormattedText());
        }
        [TestMethod]
        [TestCategory("Automated")]
        [DisableInterActive]
        public async Task AutoSolve_Game71_ShouldBeSolvable()
        {
            var gameService = new FreeCellGameService();
            gameService.InitializeGame(71);
            var solver = new FreeCellSolver(gameService, loggerAction: null);
            var moves = await solver.FindSolutionAsync();
            Assert.IsNotNull(moves, "Game 71 should be solvable");
            Assert.IsTrue(moves.Count > 0, $"Game 71 should have moves, got {moves.Count}");
            LogAction($"Game 71 solved with {moves.Count} moves, Nodes:{solver._countNodesCreated}, Visit:{solver._countNodesVisited}");
        }

        [TestMethod]
        [TestCategory("Manual")]
        [DisableInterActive]
        public async Task AutoSolve_FindSolutionForManyGames()
        {
            /*
    # of failures: 3 Total Moves: 1485336 Max:4000000 Uber:30000
solved with insertunder: Failure: Game   7345   40,153.6ms Moves:   0 Solver failed 4537 to find any moves, but game is not won. Visited 1856609 states. MaxDepth = 9341 Created: 2706088 Visited:2461865 BackTrack:2340870 Uber    82 Found=>Tabl:8024 Megamoves: 1612 Split 2822 AbutMoves:119024 NeutralMoves: 3665
solved with insertunder: Failure: Game   9925    2,837.4ms Moves:   0 Solver failed 13 to find any moves, but game is not won. Visited 144740 states. MaxDepth = 814 Created:  180894 Visited: 180050 BackTrack:179478 Uber     6 Found=>Tabl:276 Megamoves:    0 Split 399 AbutMoves:13463 NeutralMoves:  491
solved with insertunder: Failure: Game   5911    7,175.8ms Moves:   0 Solver failed 14 to find any moves, but game is not won. Visited 186912 states. MaxDepth = 6956 Nodes:  228090 Visit: 210040 BTrack:193439 Uber:    7 Fnd=>Tabl:15595 Mega:    0 Split 170 Abut:11943 Neut:  884 Order:1477
Failure: Game   7566    8,064.6ms Moves:   0 Solver failed 10 to find any moves, but game is not won. Visited 177762 states. MaxDepth = 915 Nodes:  246053 Visit: 240059 BTrack:237183 Uber:    8 Fnd=>Tabl:0 Mega:    0 Split 8 Abut: 2172 Neut:  376 Order:0
Failure: Game   8591      478.6ms Moves:   0 Solver failed 0 to find any moves, but game is not won. Visited 20918 states. MaxDepth = 60 Nodes:   22878 Visit:  22878 BTrack:22878 Uber:    0 Fnd=>Tabl:0 Mega:    0 Split 0 Abut:   76 Neut:   56 Order:0
20k: 15.1 min
Failure: Game   4368    6,158.4ms Moves:   0 Solver failed 10 to find any moves, but game is not won. Visited 185970 states. MaxDepth = 5527 Nodes:  261341 Visit: 240051 BTrack:226424 Uber:    8 Fnd=>Tabl:8481 Mega:    0 Split 3 Abut: 2507 Neut:  728 Order:6014 InertUnder:1373
Failure: Game   6291    4,357.7ms Moves:   0 Solver failed 11 to find any moves, but game is not won. Visited 180002 states. MaxDepth = 697 Nodes:  243168 Visit: 240038 BTrack:238614 Uber:    8 Fnd=>Tabl:1220 Mega:    6 Split 22 Abut: 6167 Neut: 4702 Order:381 InertUnder: 784
Failure: Game   7566    4,990.4ms Moves:   0 Solver failed 10 to find any moves, but game is not won. Visited 178992 states. MaxDepth = 935 Nodes:  249300 Visit: 240067 BTrack:235692 Uber:    8 Fnd=>Tabl:16 Mega:    0 Split 8 Abut:  312 Neut:  344 Order:0 InertUnder:1464
Failure: Game   8591      337.4ms Moves:   0 Solver failed 0 to find any moves, but game is not won. Visited 21740 states. MaxDepth = 71 Nodes:   23787 Visit:  23787 BTrack:23787 Uber:    0 Fnd=>Tabl:0 Mega:    0 Split 0 Abut:   68 Neut:   44 Order:0 InertUnder:  43
Failure: Game  10533   11,500.6ms Moves:   0 Solver failed 1571 to find any moves, but game is not won. Visited 332084 states. MaxDepth = 26973 Nodes:  647844 Visit: 360346 BTrack:169542 Uber:   12 Fnd=>Tabl:17743 Mega:    0 Split 775 Abut:  684 Neut: 3956 Order:16463 InertUnder:11540
Failure: Game  10692    2,034.3ms Moves:   0 Solver failed 0 to find any moves, but game is not won. Visited 104753 states. MaxDepth = 2265 Nodes:  123050 Visit: 120493 BTrack:119179 Uber:    4 Fnd=>Tabl:0 Mega:    2 Split 188 Abut: 4559 Neut:  149 Order:0 InertUnder: 396

Game	TimeMs	Moves	Nodes	Visit	MaxDepth	BTrack	Uber	Fnd=>Tabl	Mega	Split	Abut	Neut	Order	InsertUnder	BurFndRdy	FCSeq	ColClr	MaxLkAhd	Stat
2,260	32	0	2,947	1,310	1,041	93	0	126	0	2	4	0	60	4	9	282	6	6	Solver failed 1013 to find any moves; but game is not won. Visited 1311 states. MaxDepth = 1041
2,638	97	0	6,440	4,689	1,110	3,277	0	346	0	1	0	42	135	131	63	313	6	4	Solver failed 1024 to find any moves; but game is not won. Visited 4019 states. MaxDepth = 1110
3,261	22	0	2,379	1,237	1,006	232	0	85	0	5	1	3	24	1	2	474	1	4	Solver failed 647 to find any moves; but game is not won. Visited 1233 states. MaxDepth = 1006
6,015	161	0	15,993	8,108	2,627	4,498	0	0	0	1	14	2	1	201	1	3,126	21	6	Solver failed 2119 to find any moves; but game is not won. Visited 7087 states. MaxDepth = 2627
6,240	151	0	11,934	5,881	1,202	3,191	0	472	3	2	27	33	164	19	27	394	23	6	Solver failed 1141 to find any moves; but game is not won. Visited 5844 states. MaxDepth = 1202
6,268	1,142	0	21,556	9,225	2,150	1,676	0	1,522	0	7	0	120	1,429	44	13	269	21	6	Solver failed 43 to find any moves; but game is not won. Visited 8578 states. MaxDepth = 2150
7,186	2,233	0	29,008	20,027	1,042	12,355	0	2,334	0	136	1	233	2,569	1,523	5	884	8	6	Solver failed 69 to find any moves; but game is not won. Visited 18370 states. MaxDepth = 1042
7,382	1,556	0	64,705	60,018	1,004	57,319	2	4,744	8	5	518	102	1,198	83	67	872	2	5	Solver failed 5 to find any moves; but game is not won. Visited 51616 states. MaxDepth = 1004
7,666	1,868	0	149,632	97,326	1,066	75,528	3	1,402	17	0	50	110	52	175	1	12,890	27	6	Solver failed 972 to find any moves; but game is not won. Visited 83875 states. MaxDepth = 1066
8,226	1,697	0	101,355	90,012	1,037	83,299	3	2,640	21	30	597	261	195	2,291	1	14,832	6	4	Solver failed 19 to find any moves; but game is not won. Visited 71331 states. MaxDepth = 1037
8,591	295	0	23,596	23,596	70	23,596	0	0	0	0	68	44	0	43	4	138	0	2	Solver failed 0 to find any moves; but game is not won. Visited 21560 states. MaxDepth = 70
9,376	1,462	0	76,036	70,993	1,011	67,705	2	1,069	0	41	1,040	455	1	332	11	3,805	3	2	Solver failed 11 to find any moves; but game is not won. Visited 63106 states. MaxDepth = 1011
9,693	55	0	3,277	1,441	1,161	229	0	132	1	1	1	45	155	34	0	226	4	6	Solver failed 1116 to find any moves; but game is not won. Visited 1430 states. MaxDepth = 1161
             
             */
            var nTotMoves = 0;
            var csvHeader = SolverStatsRow.CsvHeader;
            var lastNumericStatCol = SolverStatsRow.LastNumericStatCol;

            var csvSuccesses = new List<string>();
            var csvFailures = new List<string>();
            var swAll = Stopwatch.StartNew();
            for (int gameId = 1; gameId <= 1000; gameId++)
            {
                var errorMessage = "OK";
                var sw = Stopwatch.StartNew();
                var gameService = new FreeCellGameService();
                gameService.InitializeGame(gameId);
                var solver = new FreeCellSolver(gameService, loggerAction: null);
                var nMoves = 0;
                var failed = false;
                try
                {
                    var moves = await solver.FindSolutionAsync();
                    nMoves = moves.Count;
                    nTotMoves += moves.Count;
                    sw.Stop();
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    errorMessage = ex.Message.Replace(",", ";"); // sanitize commas for CSV
                    failed = true;
                }

                var row = SolverStatsRow.FromSolver(solver, gameId, nMoves, sw.Elapsed.TotalMilliseconds, errorMessage);
                var csvLine = row.ToCsvLine();
                LogAction(csvLine);
                if (failed)
                    csvFailures.Add(csvLine);
                else
                    csvSuccesses.Add(csvLine);
            }
            swAll.Stop();
            LogAction($"# of failures: {csvFailures.Count} Total Moves: {nTotMoves} Max:{FreeCellSolver._nMaxNodesToVisit} Uber:{FreeCellSolver._multipleAtWhichToUberReverse}");

            // Build CSV content: header, then failures first for visibility, then successes
            var csvLines = new List<string> { csvHeader };
            csvLines.AddRange(csvFailures);
            csvLines.AddRange(csvSuccesses);
            var csvContent = string.Join(Environment.NewLine, csvLines);
            var csvPath = Path.Combine(Path.GetTempPath(), $"FreeCellSolver_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            File.WriteAllText(csvPath, csvContent);
            LogAction($"CSV written to: {csvPath}");
            if (OperatingSystem.IsWindows())
            {
                LogAction("Attempting to open CSV in Excel...");
                // Open CSV in Excel via COM automation using dynamic for clean late-bound calls
                // Excel constants (from Microsoft.Office.Interop.Excel enums)
                const int xlMaximized = -4137;      // XlWindowState.xlMaximized
                const int xlSrcRange = 1;           // XlListObjectSourceType.xlSrcRange
                const int xlYes = 1;                // XlYesNoGuess.xlYes
                try
                {
                    var excelType = Type.GetTypeFromProgID("Excel.Application");
                    if (excelType != null)
                    {
                        dynamic excel = Activator.CreateInstance(excelType)!;
                        excel.Visible = true;
                        excel.WindowState = xlMaximized;

                        dynamic workbook = excel.Workbooks.Open(csvPath);
                        dynamic sheet = workbook.ActiveSheet;

                        // Insert two rows at the top so we can place a textbox summary above the table
                        sheet.Rows[1].Insert();
                        sheet.Rows[1].Insert();

                        // Compute header/data positions so the table starts at row 3
                        var headerRow = 3;
                        var dataRows = csvFailures.Count + csvSuccesses.Count; // number of data rows (not counting header)
                        var lastDataRow = headerRow + dataRows; // last row that contains data

                        // Auto-fit columns (before sizing textbox)
                        sheet.UsedRange.Columns.AutoFit();

                        // Determine last used column for the table range
                        var lastCol = sheet.UsedRange.Columns.Count;

                        // Build a specific range that starts at row 3 so the ListObject (table) begins there
                        dynamic tableRange = sheet.Range[sheet.Cells[headerRow, 1], sheet.Cells[lastDataRow, lastCol]];
                        sheet.ListObjects.Add(xlSrcRange, tableRange, Type.Missing, xlYes);

                        // Create a textbox in the two rows above the table with summary information
                        const int msoTextOrientationHorizontal = 1; // msoTextOrientationHorizontal
                        try
                        {
                            // Position the textbox over rows 1-2, stopping before the header row so it doesn't block sort/filter clicks
                            var left = (double)sheet.Cells[1, 1].Left;
                            var top = (double)sheet.Cells[1, 1].Top;
                            var headerTop = (double)sheet.Cells[headerRow, 1].Top;
                            var height = Math.Max(20, headerTop - top - 2); // fit within the 2 inserted rows with a small gap
                            var width = Math.Max(400, (int)((double)sheet.UsedRange.Columns.Count * 50));
                            dynamic shp = sheet.Shapes.AddTextbox(msoTextOrientationHorizontal, left, top, width, height);
                            var summaryText = $"{DateTime.Now} {swAll.Elapsed.TotalSeconds.ToString("N1")} Failures: {csvFailures.Count} / {dataRows}";
                            shp.TextFrame.Characters().Text = summaryText;
                            shp.Line.Visible = false;
                            try { shp.Fill.Visible = false; } catch { }
                            try { shp.TextFrame.HorizontalAlignment = -4108; } catch { } // xlHAlignCenter
                        }
                        catch
                        {
                            // Ignore textbox failures and continue – the table/data are still useful
                        }

                        // Format all numeric cells: no decimal places, thousands comma separator
                        sheet.UsedRange.NumberFormat = "#,##0";

                        // Re-autofit columns after formatting
                        sheet.UsedRange.Columns.AutoFit();

                        // Add Min/Max/Avg/Sum rows below the table for numeric columns (B through lastNumericStatCol)
                        var statsLabels = new[] { "Min", "Max", "Avg", "Total" };
                        var statsFuncs = new[] { "MIN", "MAX", "AVERAGE", "SUM" };
                        var statsStartRow = lastDataRow + 2; // leave one blank row after data
                        for (int s = 0; s < statsLabels.Length; s++)
                        {
                            var statsRow = statsStartRow + s;
                            dynamic labelCell = sheet.Cells[statsRow, 1];
                            labelCell.Value2 = statsLabels[s];
                            labelCell.Font.Bold = true;
                            // Columns B(2) through lastNumericStatCol are numeric stats
                            for (int col = 2; col <= lastNumericStatCol; col++)
                            {
                                dynamic formulaCell = sheet.Cells[statsRow, col];
                                var colLetter = (char)('A' + col - 1); // B=2 -> 'B', etc.
                                var dataStartRow = headerRow + 1; // data rows begin after headerRow
                                formulaCell.Formula = $"={statsFuncs[s]}({colLetter}{dataStartRow}:{colLetter}{lastDataRow})";
                            }
                        }

                        // Re-format and autofit after adding stats rows
                        sheet.UsedRange.NumberFormat = "#,##0";
                        sheet.UsedRange.Columns.AutoFit();

                        LogAction("Excel opened with CSV data as table starting at row 3, textbox summary added, formatted with #,##0 and auto-fitted.");
                    }
                    else
                    {
                        LogAction("Excel COM type not found – skipping Excel automation.");
                    }
                }
                catch (Exception ex)
                {
                    LogAction($"Excel automation error: {ex.GetType().Name}: {ex.Message}");
                }
            }
            Assert.AreEqual(0, csvFailures.Count, "There should be no failures");
        }

        [TestMethod]
        [TestCategory("Manual")]
        [DisableInterActive]
        [Microsoft.VisualStudio.TestTools.UnitTesting.Description("Find the most recent FreeCellSolver CSV in the temp folder and export it to .xlsx under artifacts/analysis in the repo root. Manual utility.")]
        public void Manual_ExportLatestCsvToXlsx()
        {
            if (!OperatingSystem.IsWindows())
            {
                LogAction("Excel export skipped: not running on Windows.");
                return;
            }

            try
            {
                var temp = Path.GetTempPath();
                var csv = Directory.GetFiles(temp, "FreeCellSolver_*.csv")
                                   .OrderByDescending(f => File.GetCreationTimeUtc(f))
                                   .FirstOrDefault();
                if (string.IsNullOrEmpty(csv))
                {
                    LogAction($"No FreeCellSolver_*.csv found in {temp}");
                    return;
                }

                const int xlOpenXMLWorkbook = 51; // XlFileFormat.xlOpenXMLWorkbook
                var excelType = Type.GetTypeFromProgID("Excel.Application");
                if (excelType == null)
                {
                    LogAction("Excel COM not available on this machine.");
                    return;
                }

                dynamic excel = Activator.CreateInstance(excelType)!;
                try
                {
                    excel.Visible = false;
                    dynamic wb = excel.Workbooks.Open(csv);

                    // Determine repository root by walking up until a .git folder is found (fallback to current dir)
                    var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory()) ?? Directory.GetCurrentDirectory();
                    var artifactsDir = Path.Combine(repoRoot, "artifacts", "analysis");
                    Directory.CreateDirectory(artifactsDir);
                    var xlsxPath = Path.Combine(artifactsDir, Path.GetFileNameWithoutExtension(csv) + ".xlsx");

                    wb.SaveAs(xlsxPath, xlOpenXMLWorkbook);
                    wb.Close(false);
                    LogAction($"Exported CSV '{csv}' to XLSX: {xlsxPath}");
                }
                finally
                {
                    try { excel.Quit(); } catch { }
                }
            }
            catch (Exception ex)
            {
                LogAction($"Manual Excel export failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        // Walk up directory tree to find repository root (contains .git folder). Returns null if not found.
        private static string? FindRepoRoot(string startDir)
        {
            var dir = new DirectoryInfo(startDir);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            return null;
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

        [TestMethod]
        [TestCategory("Manual")]
        [DisableInterActive]
        [Timeout(120000)]
        public async Task TestInsertUnderSeq_PreviouslyFailedGames()
        {
            // Games that previously failed the solver (documented in AutoSolve_FindSolutionForManyGames comments).
            // Test whether the InsertUnderSeq heuristic enables solving any of them.
            var knownFailureGameIds = new[] { 8591, 5911, 7566, 7345, 9925 };
            var solved = new List<(int gameId, int moves, int inertUnder, long ms)>();
            var unsolved = new List<(int gameId, string error)>();

            foreach (var gameId in knownFailureGameIds)
            {
                var sw = Stopwatch.StartNew();
                var gameService = new FreeCellGameService();
                gameService.InitializeGame(gameId);
                /*
                var solver = new FreeCellSolver(gameService, loggerAction: (msgf) => LogAction(msgf()));
                /*/
                var solver = new FreeCellSolver(gameService, loggerAction: null);
                //*/
                try
                {
                    var moves = await solver.FindSolutionAsync();
                    sw.Stop();
                    solved.Add((gameId, moves.Count, solver._countInsertUnderMoves, sw.ElapsedMilliseconds));
                    LogAction($"Game {gameId}: SOLVED in {sw.ElapsedMilliseconds}ms, {moves.Count} moves, InertUnder:{solver._countInsertUnderMoves}");
                }
                catch (Exception)
                {
                    sw.Stop();
                    unsolved.Add((gameId, $"Failed in {sw.ElapsedMilliseconds}ms"));
                    LogAction($"Game {gameId}: still unsolved after {sw.ElapsedMilliseconds}ms");
                }
            }

            LogAction($"Results: {solved.Count} solved, {unsolved.Count} still unsolvable out of {knownFailureGameIds.Length} known failures");
            foreach (var s in solved)
                LogAction($"  Solved game {s.gameId}: {s.moves} moves, InertUnder:{s.inertUnder}, {s.ms}ms");
            foreach (var u in unsolved)
                LogAction($"  Unsolved game {u.gameId}: {u.error}");
        }
    }
}
