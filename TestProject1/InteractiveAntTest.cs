using Microsoft.Playwright;

namespace TestProject1
{
    [TestClass]
    public class InteractiveAntTest : InteractiveTestBase
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
        /// Interactive test for Langton's Ant - keeps browser open until user closes it
        /// </summary>
        [TestMethod]
        [TestCategory("Manual")]
        public async Task LaunchInteractiveBrowser_LangtonsAnt()
        {
            Console.WriteLine("Launching interactive browser for Langton's Ant...");
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

            // Navigate to the Ant page using shared helper
            await NavigateToBlazorPageAsync(page, "/ant", "canvas.ant-canvas");

            Console.WriteLine("Langton's Ant page loaded in incognito mode.");
            Console.WriteLine("?? Watch the ant(s) build highways!");
            Console.WriteLine("?? Try different rules (RL, RLR, LLRR, etc.)");
            Console.WriteLine("? Use Steps/Frame slider for speed");
            Console.WriteLine("?? Try multiple ants for interesting interactions");
            Console.WriteLine("The test will wait until you close the browser.");

            // Create a TaskCompletionSource to wait for page close
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
        [TestCategory("Manual")]
        public async Task Ant_Interactive_RuleTesting()
        {
            _browser = await _playwright!.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false,
                SlowMo = 200
            });

            var context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = ViewportSize.NoViewport
            });

            var page = await context.NewPageAsync();
            page.Console += (_, msg) => Console.WriteLine($"[Browser Console] {msg.Text}");

            // Navigate to the Ant page using shared helper
            await NavigateToBlazorPageAsync(page, "/ant", "canvas.ant-canvas");

            Console.WriteLine("? Ant page loaded successfully");
            Console.WriteLine("?? Testing Langton's Ant rules...");

            // Wait for auto-start
            await page.WaitForTimeoutAsync(2000);

            // Check that stats are showing
            var statsText = await page.Locator(".ant-stats").TextContentAsync();
            Console.WriteLine($"?? Initial stats: {statsText}");

            // Pause simulation
            var pauseButton = page.Locator("button:has-text('Pause')");
            if (await pauseButton.IsVisibleAsync())
            {
                await pauseButton.ClickAsync();
                Console.WriteLine("? Paused simulation");
            }
            await page.WaitForTimeoutAsync(500);

            // Test different rules
            var ruleSelect = page.Locator("select.ant-select");

            // Test RLR rule
            await ruleSelect.SelectOptionAsync("RLR");
            Console.WriteLine("?? Selected RLR rule (triangle)");
            await page.WaitForTimeoutAsync(500);

            var runButton = page.Locator("button:has-text('Run')");
            await runButton.ClickAsync();
            Console.WriteLine("? Started simulation with RLR");
            await page.WaitForTimeoutAsync(5000);

            var rlrStats = await page.Locator(".ant-stats").TextContentAsync();
            Console.WriteLine($"?? After RLR: {rlrStats}");

            // Pause and clear
            pauseButton = page.Locator("button:has-text('Pause')");
            await pauseButton.ClickAsync();
            await page.WaitForTimeoutAsync(300);

            var clearButton = page.Locator("button:has-text('Clear')");
            await clearButton.ClickAsync();
            Console.WriteLine("??? Cleared grid");
            await page.WaitForTimeoutAsync(500);

            // Test LLRR rule
            await ruleSelect.SelectOptionAsync("LLRR");
            Console.WriteLine("?? Selected LLRR rule (symmetric)");
            await page.WaitForTimeoutAsync(500);

            runButton = page.Locator("button:has-text('Run')");
            await runButton.ClickAsync();
            Console.WriteLine("? Started simulation with LLRR");
            await page.WaitForTimeoutAsync(5000);

            var llrrStats = await page.Locator(".ant-stats").TextContentAsync();
            Console.WriteLine($"?? After LLRR: {llrrStats}");

            Console.WriteLine("\n? Rule testing completed!");
            Console.WriteLine("?? Browser will stay open until you close it.");
            Console.WriteLine("Feel free to experiment with more rules!");

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
        /// Automated test: Verify simulation auto-starts on page load
        /// </summary>
        [TestMethod]
        [TestCategory("Interactive")]
        public async Task Ant_AutoStarts_OnPageLoad()
        {
            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            await NavigateToBlazorPageAsync(page, "/ant", "canvas.ant-canvas");

            Console.WriteLine("?? Testing: Simulation should auto-start on page load");

            // Wait for auto-start
            await page.WaitForTimeoutAsync(2000);

            // Check that step counter is increasing
            var stats1 = await page.Locator(".ant-stats").TextContentAsync();
            var step1Match = System.Text.RegularExpressions.Regex.Match(stats1 ?? "", @"Steps\s+([\d,]+)");
            var step1 = step1Match.Success ? int.Parse(step1Match.Groups[1].Value.Replace(",", "")) : 0;
            Console.WriteLine($"  ?? Initial steps: {step1}");

            // Wait a bit more
            await page.WaitForTimeoutAsync(1000);

            var stats2 = await page.Locator(".ant-stats").TextContentAsync();
            var step2Match = System.Text.RegularExpressions.Regex.Match(stats2 ?? "", @"Steps\s+([\d,]+)");
            var step2 = step2Match.Success ? int.Parse(step2Match.Groups[1].Value.Replace(",", "")) : 0;
            Console.WriteLine($"  ?? After 1 second: {step2}");

            Console.WriteLine($"\n  Expected: Steps should increase (auto-start working)");
            Console.WriteLine($"  Actual: Steps went from {step1} to {step2}");

            var isRunning = step2 > step1;

            if (isRunning)
            {
                Console.WriteLine("  ? TEST PASSED: Simulation auto-started");
            }
            else
            {
                Console.WriteLine("  ? TEST FAILED: Simulation did not auto-start");
            }

            Assert.IsTrue(isRunning, "Simulation should auto-start and step count should increase");

            // Also verify Pause button is visible (indicates running state)
            var pauseButton = page.Locator("button:has-text('Pause')");
            var pauseVisible = await pauseButton.IsVisibleAsync();
            Console.WriteLine($"  Pause button visible: {pauseVisible}");

            Assert.IsTrue(pauseVisible, "Pause button should be visible when simulation is running");

            await page.WaitForTimeoutAsync(1000);
        }

        /// <summary>
        /// Automated test: Verify classic RL rule creates colored cells
        /// </summary>
        [TestMethod]
        [TestCategory("Interactive")]
        public async Task Ant_ClassicRL_CreatesColoredCells()
        {
            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            await NavigateToBlazorPageAsync(page, "/ant", "canvas.ant-canvas");

            Console.WriteLine("?? Testing: Classic RL rule should create colored cells");

            // Wait for page to load
            await page.WaitForTimeoutAsync(1000);

            // Verify RL is the default rule
            var ruleSelect = page.Locator("select.ant-select");
            var selectedValue = await ruleSelect.InputValueAsync();
            Console.WriteLine($"  Selected rule: {selectedValue}");

            // Let it run for a bit
            await page.WaitForTimeoutAsync(3000);

            // Check that colored cells count is increasing
            var stats = await page.Locator(".ant-stats").TextContentAsync();
            Console.WriteLine($"  ?? Stats: {stats}");

            // Extract colored count (last number in stats typically)
            var coloredMatch = System.Text.RegularExpressions.Regex.Match(stats ?? "", @"(\d+)\s*$");
            var coloredCount = coloredMatch.Success ? int.Parse(coloredMatch.Groups[1].Value) : 0;

            Console.WriteLine($"\n  Expected: Colored cells > 0");
            Console.WriteLine($"  Actual: Colored cells = {coloredCount}");

            if (coloredCount > 0)
            {
                Console.WriteLine("  ? TEST PASSED: Ant is creating colored cells");
            }
            else
            {
                Console.WriteLine("  ? TEST FAILED: No colored cells created");
            }

            Assert.IsTrue(coloredCount > 0, "Ant should create colored cells as it moves");

            await page.WaitForTimeoutAsync(1000);
        }

        /// <summary>
        /// Automated test: Verify step button advances simulation when paused
        /// </summary>
        [TestMethod]
        [TestCategory("Interactive")]
        public async Task Ant_StepButton_AdvancesSimulation()
        {
            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            await NavigateToBlazorPageAsync(page, "/ant", "canvas.ant-canvas");

            Console.WriteLine("?? Testing: Step button should advance simulation when paused");

            // Wait for page to load
            await page.WaitForTimeoutAsync(1000);

            // Pause simulation
            var pauseButton = page.Locator("button:has-text('Pause')");
            if (await pauseButton.IsVisibleAsync())
            {
                await pauseButton.ClickAsync();
            }
            await page.WaitForTimeoutAsync(300);

            // Clear to start fresh
            var clearButton = page.Locator("button:has-text('Clear')");
            await clearButton.ClickAsync();
            Console.WriteLine("  ? Cleared grid");
            await page.WaitForTimeoutAsync(300);

            // Get initial step count (should be 0)
            var initialStats = await page.Locator(".ant-stats").TextContentAsync();
            var initialStepMatch = System.Text.RegularExpressions.Regex.Match(initialStats ?? "", @"Steps\s+([\d,]+)");
            var initialSteps = initialStepMatch.Success ? int.Parse(initialStepMatch.Groups[1].Value.Replace(",", "")) : 0;
            Console.WriteLine($"  ?? Initial steps: {initialSteps}");

            // Click step button multiple times
            var stepButton = page.Locator("button:has-text('Step')");
            for (int i = 0; i < 5; i++)
            {
                await stepButton.ClickAsync();
                await page.WaitForTimeoutAsync(100);
            }

            var finalStats = await page.Locator(".ant-stats").TextContentAsync();
            var finalStepMatch = System.Text.RegularExpressions.Regex.Match(finalStats ?? "", @"Steps\s+([\d,]+)");
            var finalSteps = finalStepMatch.Success ? int.Parse(finalStepMatch.Groups[1].Value.Replace(",", "")) : 0;
            Console.WriteLine($"  ?? After 5 step clicks: {finalSteps}");

            Console.WriteLine($"\n  Expected: Steps should be 5");
            Console.WriteLine($"  Actual: Steps = {finalSteps}");

            if (finalSteps == 5)
            {
                Console.WriteLine("  ? TEST PASSED: Step button advances simulation correctly");
            }
            else
            {
                Console.WriteLine("  ? TEST FAILED: Step count unexpected");
            }

            Assert.AreEqual(5, finalSteps, "Step button should advance exactly 1 step per click");

            await page.WaitForTimeoutAsync(1000);
        }

        /// <summary>
        /// Automated test: Verify clear button resets simulation
        /// </summary>
        [TestMethod]
        [TestCategory("Interactive")]
        public async Task Ant_ClearButton_ResetsSimulation()
        {
            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            await NavigateToBlazorPageAsync(page, "/ant", "canvas.ant-canvas");

            Console.WriteLine("?? Testing: Clear button should reset simulation");

            // Wait for simulation to run a bit
            await page.WaitForTimeoutAsync(3000);

            // Get stats before clear
            var beforeStats = await page.Locator(".ant-stats").TextContentAsync();
            var beforeStepMatch = System.Text.RegularExpressions.Regex.Match(beforeStats ?? "", @"Steps\s+([\d,]+)");
            var beforeSteps = beforeStepMatch.Success ? int.Parse(beforeStepMatch.Groups[1].Value.Replace(",", "")) : 0;
            Console.WriteLine($"  ?? Before clear - Steps: {beforeSteps}");

            Assert.IsTrue(beforeSteps > 0, "Simulation should have run before clear");

            // Pause first
            var pauseButton = page.Locator("button:has-text('Pause')");
            if (await pauseButton.IsVisibleAsync())
            {
                await pauseButton.ClickAsync();
            }
            await page.WaitForTimeoutAsync(300);

            // Click clear
            var clearButton = page.Locator("button:has-text('Clear')");
            await clearButton.ClickAsync();
            Console.WriteLine("  ? Clicked Clear button");
            await page.WaitForTimeoutAsync(500);

            // Get stats after clear
            var afterStats = await page.Locator(".ant-stats").TextContentAsync();
            var afterStepMatch = System.Text.RegularExpressions.Regex.Match(afterStats ?? "", @"Steps\s+([\d,]+)");
            var afterSteps = afterStepMatch.Success ? int.Parse(afterStepMatch.Groups[1].Value.Replace(",", "")) : 0;
            Console.WriteLine($"  ?? After clear - Steps: {afterSteps}");

            Console.WriteLine($"\n  Expected: Steps should be 0");
            Console.WriteLine($"  Actual: Steps = {afterSteps}");

            if (afterSteps == 0)
            {
                Console.WriteLine("  ? TEST PASSED: Clear button resets simulation");
            }
            else
            {
                Console.WriteLine("  ? TEST FAILED: Simulation not reset");
            }

            Assert.AreEqual(0, afterSteps, "Clear button should reset step count to 0");

            await page.WaitForTimeoutAsync(1000);
        }

        /// <summary>
        /// Automated test: Verify rule change works
        /// </summary>
        [TestMethod]
        [TestCategory("Interactive")]
        public async Task Ant_RuleChange_AppliesNewRule()
        {
            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            await NavigateToBlazorPageAsync(page, "/ant", "canvas.ant-canvas");

            Console.WriteLine("?? Testing: Changing rule should apply new rule");

            // Wait for page to load
            await page.WaitForTimeoutAsync(1000);

            // Pause simulation
            var pauseButton = page.Locator("button:has-text('Pause')");
            if (await pauseButton.IsVisibleAsync())
            {
                await pauseButton.ClickAsync();
            }
            await page.WaitForTimeoutAsync(300);

            // Get initial rule (should be RL)
            var ruleSelect = page.Locator("select.ant-select");
            var initialRule = await ruleSelect.InputValueAsync();
            Console.WriteLine($"  Initial rule: {initialRule}");
            Assert.AreEqual("RL", initialRule, "Default rule should be RL");

            // Change to LLRR
            await ruleSelect.SelectOptionAsync("LLRR");
            await page.WaitForTimeoutAsync(500);

            var newRule = await ruleSelect.InputValueAsync();
            Console.WriteLine($"  New rule: {newRule}");

            Console.WriteLine($"\n  Expected: Rule should be LLRR");
            Console.WriteLine($"  Actual: Rule = {newRule}");

            if (newRule == "LLRR")
            {
                Console.WriteLine("  ? TEST PASSED: Rule changed successfully");
            }
            else
            {
                Console.WriteLine("  ? TEST FAILED: Rule did not change");
            }

            Assert.AreEqual("LLRR", newRule, "Rule should change to LLRR");

            await page.WaitForTimeoutAsync(1000);
        }
    }
}
