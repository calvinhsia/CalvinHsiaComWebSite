using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace TestProject1
{
    [TestClass]
    [TestCategory("Manual")]
    public class InteractiveFishTest : InteractiveTestBase
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
        [TestCategory("Manual")]
        public async Task Fish_Interactive_CellularAutomata()
        {
            _browser = await _playwright!.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false,
                SlowMo = 300
            });

            var context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = ViewportSize.NoViewport
            });

            var page = await context.NewPageAsync();
            page.Console += (_, msg) => Console.WriteLine($"[Browser Console] {msg.Text}");

            // Navigate to the Fish page using shared helper
            await NavigateToBlazorPageAsync(page, "/fish", "canvas.fish-canvas");

            Console.WriteLine("✅ Fish page loaded successfully");
            Console.WriteLine("🧪 Testing Fish vs Sharks cellular automata...");

            // Wait for initial render
            await page.WaitForTimeoutAsync(1000);

            // Check that stats are showing
            var statsText = await page.Locator(".fish-stats").TextContentAsync();
            Console.WriteLine($"📊 Initial stats: {statsText}");

            // Test resume button (starts paused by default)
            var resumeButton = page.Locator("button:has-text('Resume')");
            await resumeButton.ClickAsync();
            Console.WriteLine("▶ Started simulation");
            await page.WaitForTimeoutAsync(3000);

            // Check stats after running
            var runningStats = await page.Locator(".fish-stats").TextContentAsync();
            Console.WriteLine($"📊 Running stats: {runningStats}");

            // Test pause button
            var pauseButton = page.Locator("button:has-text('Pause')");
            await pauseButton.ClickAsync();
            Console.WriteLine("⏸ Paused simulation");
            await page.WaitForTimeoutAsync(1000);

            // Test clicking on canvas to add fish (left-click)
            var canvas = page.Locator("canvas.fish-canvas");
            var canvasBounds = await canvas.BoundingBoxAsync();
            if (canvasBounds != null)
            {
                // Left-click to add fish
                await canvas.ClickAsync(new LocatorClickOptions
                {
                    Position = new Position
                    {
                        X = canvasBounds.Width / 3,
                        Y = canvasBounds.Height / 3
                    }
                });
                Console.WriteLine("🐟 Left-clicked canvas to add fish");
                await page.WaitForTimeoutAsync(500);

                // Right-click to add shark (using mouse down/up)
                await page.Mouse.MoveAsync(
                       canvasBounds.X + canvasBounds.Width * 2 / 3,
                  canvasBounds.Y + canvasBounds.Height * 2 / 3
                      );
                await page.Mouse.DownAsync(new MouseDownOptions { Button = MouseButton.Right });
                await page.Mouse.UpAsync(new MouseUpOptions { Button = MouseButton.Right });
                Console.WriteLine("🦈 Right-clicked canvas to add shark");
                await page.WaitForTimeoutAsync(500);
            }

            // Test adjusting speed
            var speedSlider = page.Locator("input[type='range']");
            await speedSlider.FillAsync("30");
            Console.WriteLine("⚡ Adjusted speed to 30 ms delay");
            await page.WaitForTimeoutAsync(500);

            // Resume and let it run
            await resumeButton.ClickAsync();
            Console.WriteLine("▶ Resumed simulation");
            await page.WaitForTimeoutAsync(5000);

            // Test reset button
            await pauseButton.ClickAsync();
            var resetButton = page.Locator("button:has-text('Reset')");
            await resetButton.ClickAsync();
            Console.WriteLine("🔄 Reset simulation");
            await page.WaitForTimeoutAsync(1000);

            // Test changing parameters
            var fishBreedAge = page.Locator("input[type='number']").First;
            await fishBreedAge.FillAsync("5");
            Console.WriteLine("⚙️ Changed fish breed age to 5");
            await page.WaitForTimeoutAsync(500);

            // Test toggling circles
            var circlesCheckbox = page.Locator("input[type='checkbox']").First;
            await circlesCheckbox.ClickAsync();
            Console.WriteLine("⭕ Toggled circles display");
            await page.WaitForTimeoutAsync(1000);

            // Resume for final observation
            await resumeButton.ClickAsync();
            Console.WriteLine("▶ Running final simulation...");
            await page.WaitForTimeoutAsync(5000);

            // Final stats
            var finalStats = await page.Locator(".fish-stats").TextContentAsync();
            Console.WriteLine($"📊 Final stats: {finalStats}");

            Console.WriteLine("\n✅ Fish interactive test completed successfully!");
            Console.WriteLine("🎮 Browser will stay open until you close it.");
            Console.WriteLine("Feel free to continue experimenting with the simulation!");

            // Wait for user to close the browser
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

        [TestMethod]
        [TestCategory("Manual")]
        public async Task Fish_Interactive_ParameterTesting()
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

            // Navigate to the Fish page using shared helper
            await NavigateToBlazorPageAsync(page, "/fish", "canvas.fish-canvas");

            Console.WriteLine("🧪 Testing different parameter configurations...");

            // Test 1: Fast fish breeding
            Console.WriteLine("\n🐟 Test 1: Fast fish breeding");
            var fishBreedInputs = await page.Locator("text='Breed Age:'").Locator("..").Locator("input[type='number']").AllAsync();
            if (fishBreedInputs.Count > 0)
            {
                await fishBreedInputs[0].FillAsync("1");
                Console.WriteLine("  ✓ Set fish breed age to 1");
            }

            var resumeButton = page.Locator("button:has-text('Resume')");
            await resumeButton.ClickAsync();
            Console.WriteLine("  ▶ Running simulation...");
            await page.WaitForTimeoutAsync(5000);

            var stats1 = await page.Locator(".fish-stats").TextContentAsync();
            Console.WriteLine($"  📊 Result: {stats1}");

            // Reset
            var pauseButton = page.Locator("button:has-text('Pause')");
            await pauseButton.ClickAsync();
            var resetButton = page.Locator("button:has-text('Reset')");
            await resetButton.ClickAsync();
            await page.WaitForTimeoutAsync(1000);

            // Test 2: Sharks starve quickly
            Console.WriteLine("\n🦈 Test 2: Sharks starve quickly");
            var sharkStarve = page.Locator("text='Shark Starve:'").Locator("..").Locator("input[type='number']");
            await sharkStarve.FillAsync("2");
            Console.WriteLine("  ✓ Set shark starvation time to 2");

            await resumeButton.ClickAsync();
            Console.WriteLine("  ▶ Running simulation...");
            await page.WaitForTimeoutAsync(5000);

            var stats2 = await page.Locator(".fish-stats").TextContentAsync();
            Console.WriteLine($"  📊 Result: {stats2}");

            await pauseButton.ClickAsync();
            await resetButton.ClickAsync();
            await page.WaitForTimeoutAsync(1000);

            // Test 3: Torus vs bounded
            Console.WriteLine("\n🌐 Test 3: Testing torus mode");
            var torusCheckbox = page.Locator("text='Torus (wrap edges)'").Locator("..").Locator("input[type='checkbox']");
            await torusCheckbox.ClickAsync();
            Console.WriteLine("  ✓ Disabled torus mode");

            await resumeButton.ClickAsync();
            Console.WriteLine("  ▶ Running simulation...");
            await page.WaitForTimeoutAsync(5000);

            var stats3 = await page.Locator(".fish-stats").TextContentAsync();
            Console.WriteLine($"  📊 Result: {stats3}");

            Console.WriteLine("\n✅ Parameter testing completed!");
            Console.WriteLine("🎮 Browser will stay open until you close it.");
            Console.WriteLine("Feel free to continue experimenting with different parameters!");

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
        /// Interactive test for Fish - keeps browser open until user closes it
        /// </summary>
        [TestMethod]
        [TestCategory("Manual")]
        public async Task LaunchInteractiveBrowser_FishVsSharks()
        {
            Console.WriteLine("Launching interactive browser for Fish vs Sharks simulation...");
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

            // Navigate to the Fish page using shared helper
            await NavigateToBlazorPageAsync(page, "/fish", "canvas.fish-canvas");

            Console.WriteLine("Fish vs Sharks page loaded in incognito mode.");
            Console.WriteLine("🐟 Left-click to add fish");
            Console.WriteLine("🦈 Right-click to add sharks");
            Console.WriteLine("⚙️ Try different parameter combinations!");
            Console.WriteLine("💾 Click Export to download population data");
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

        /// <summary>
        /// Test that fish die out when lifespan is 1 and no sharks present
        /// </summary>
        [TestMethod]
        [TestCategory("Automated")]  // ✅ Add this so it runs in Playwright Tests step
        public async Task Fish_DieOut_WhenLifespanIsOne_NoSharks()
        {
            // Use helper to get appropriate browser options for environment
            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());

            // TestContext is automatically available from base class
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());

            var page = await context.NewPageAsync();

            // Navigate to the Fish page using shared helper
            await NavigateToBlazorPageAsync(page, "/fish", "canvas.fish-canvas");

            Console.WriteLine("🧪 Testing: Fish should die out with lifespan=1, no sharks");

            // Wait for initial load
            await page.WaitForTimeoutAsync(1000);

            // Set fish lifespan to 1
            var lifeLengthInputs = await page.Locator("text='Life Span:'").Locator("..").Locator("input[type='number']").AllAsync();
            if (lifeLengthInputs.Count > 0)
            {
                await lifeLengthInputs[0].FillAsync("1"); // Fish life span
                Console.WriteLine("  ✓ Set fish life span to 1");
            }

            // Click Reset to apply the settings and start fresh
            var resetButton = page.Locator("button:has-text('Reset')");
            await resetButton.ClickAsync();
            await page.WaitForTimeoutAsync(1000);
            Console.WriteLine("  ✓ Reset simulation with new settings");

            // Get initial fish count
            var initialStats = await page.Locator(".fish-stats").TextContentAsync();
            Console.WriteLine($"  📊 Initial stats: {initialStats}");

            // Extract fish count from stats (format: "Gen: X Fish: Y Sharks: Z")
            var match = System.Text.RegularExpressions.Regex.Match(initialStats ?? "", @"Fish:\s*(\d+)");
            var initialFishCount = match.Success ? int.Parse(match.Groups[1].Value) : 0;
            Console.WriteLine($"  🐟 Initial fish count: {initialFishCount}");

            // Resume simulation
            var resumeButton = page.Locator("button:has-text('Resume')");
            await resumeButton.ClickAsync();
            Console.WriteLine("  ▶ Started simulation");

            // Wait and check periodically
            for (int i = 0; i < 5; i++)
            {
                await page.WaitForTimeoutAsync(2000);
                var currentStats = await page.Locator(".fish-stats").TextContentAsync();
                Console.WriteLine($"  📊 After {(i + 1) * 2}s: {currentStats}");

                // Check if fish count is decreasing
                match = System.Text.RegularExpressions.Regex.Match(currentStats ?? "", @"Fish:\s*(\d+)");
                var currentFishCount = match.Success ? int.Parse(match.Groups[1].Value) : 0;

                if (currentFishCount == 0)
                {
                    Console.WriteLine("  ✅ SUCCESS: All fish have died out as expected!");
                    break;
                }
            }

            // Pause to inspect
            var pauseButton = page.Locator("button:has-text('Pause')");
            await pauseButton.ClickAsync();

            // Final check
            var finalStats = await page.Locator(".fish-stats").TextContentAsync();
            Console.WriteLine($"  📊 Final stats: {finalStats}");

            match = System.Text.RegularExpressions.Regex.Match(finalStats ?? "", @"Fish:\s*(\d+)");
            var finalFishCount = match.Success ? int.Parse(match.Groups[1].Value) : 0;

            Console.WriteLine($"\n  Expected: Fish count should be 0 (or very close to 0)");
            Console.WriteLine($"  Actual: Fish count = {finalFishCount}");

            if (finalFishCount == 0)
            {
                Console.WriteLine("  ✅ TEST PASSED: Fish died out with lifespan=1");
            }
            else
            {
                Console.WriteLine($"  ❌ TEST FAILED: Fish still alive (count={finalFishCount})");
                Console.WriteLine("  🔍 Keeping browser open for inspection...");
                await page.WaitForTimeoutAsync(10000);
            }

            await page.WaitForTimeoutAsync(2000);
        }
    }
}
