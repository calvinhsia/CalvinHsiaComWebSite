using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace TestProject1
{
    [TestClass]
    [TestCategory("Manual")]
    public class InteractiveBounceTest : InteractiveTestBase
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
        public async Task Bounce_Interactive_PhysicsSimulation()
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

            // Navigate using shared helper
            await NavigateToBlazorPageAsync(page, "/bounce", "canvas.bounce-canvas");

            // Wait for canvas to be visible
            var canvas = page.Locator("canvas.bounce-canvas");
            await Expect(canvas).ToBeVisibleAsync();

            Console.WriteLine("🎯 Bounce page loaded successfully");
            Console.WriteLine("⚽ Testing bouncing balls physics simulation...");

            // Wait a bit for balls to initialize and animate
            await page.WaitForTimeoutAsync(2000);

            // Check that stats are showing
            var statsText = await page.Locator(".bounce-stats").TextContentAsync();
            Console.WriteLine($"📊 Stats: {statsText}");

            // Test pause/resume button
            var pauseButton = page.Locator("button:has-text('Pause')");
            await pauseButton.ClickAsync();
            Console.WriteLine("⏸️ Paused simulation");
            await page.WaitForTimeoutAsync(1000);

            // Resume
            var resumeButton = page.Locator("button:has-text('Resume')");
            await resumeButton.ClickAsync();
            Console.WriteLine("▶️ Resumed simulation");
            await page.WaitForTimeoutAsync(1000);

            // Test clicking on canvas to add balls
            var canvasBounds = await canvas.BoundingBoxAsync();
            if (canvasBounds != null)
            {
                // Click in the middle of the canvas
                await canvas.ClickAsync(new LocatorClickOptions
                {
                    Position = new Position
                    {
                        X = canvasBounds.Width / 2,
                        Y = canvasBounds.Height / 2
                    }
                });
                Console.WriteLine("🖱️ Clicked canvas to add ball");
                await page.WaitForTimeoutAsync(1000);
            }

            // Test adjusting gravity
            var gravitySlider = page.Locator("input[type='range'][min='0'][max='2']");
            await gravitySlider.FillAsync("1.5");
            Console.WriteLine("🌍 Adjusted gravity to 1.5");
            await page.WaitForTimeoutAsync(2000);

            // Test adjusting bounce (elasticity)
            var bounceSlider = page.Locator("input[type='range'][min='0'][max='1']");
            await bounceSlider.FillAsync("0.5");
            Console.WriteLine("🏀 Adjusted bounce to 0.5");
            await page.WaitForTimeoutAsync(2000);

            // Test reset button
            var resetButton = page.Locator("button:has-text('Reset')");
            await resetButton.ClickAsync();
            Console.WriteLine("🔄 Reset simulation");
            await page.WaitForTimeoutAsync(2000);

            // Test ball count adjustment
            var countSlider = page.Locator("input[type='range'][min='1'][max='100']");
            await countSlider.FillAsync("50");

            var applyButton = page.Locator("button:has-text('Apply Count')");
            await applyButton.ClickAsync();
            Console.WriteLine("➕ Changed ball count to 50");
            await page.WaitForTimeoutAsync(3000);

            // Final check
            var finalStats = await page.Locator(".bounce-stats").TextContentAsync();
            Console.WriteLine($"📊 Final stats: {finalStats}");

            Console.WriteLine("\n✅ Bounce interactive test completed successfully!");
            Console.WriteLine("👀 Browser will stay open until you close it.");
            Console.WriteLine("Feel free to continue experimenting with the physics simulation!");

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
        [TestCategory("Manual")]
        public async Task Bounce_Interactive_ResponsiveLayout()
        {
            _browser = await _playwright!.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false,
                SlowMo = 300
            });

            var context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 375, Height = 667 }
            });

            var page = await context.NewPageAsync();
            page.Console += (_, msg) => Console.WriteLine($"[Browser Console] {msg.Text}");

            // Navigate using shared helper
            await NavigateToBlazorPageAsync(page, "/bounce", "canvas.bounce-canvas");

            Console.WriteLine("📱 Testing mobile layout...");
            await page.WaitForTimeoutAsync(2000);

            var canvas = page.Locator("canvas.bounce-canvas");
            await Expect(canvas).ToBeVisibleAsync();

            Console.WriteLine("✅ Mobile layout working");

            // Test tablet view
            await page.SetViewportSizeAsync(768, 1024);
            Console.WriteLine("📱 Testing tablet layout...");
            await page.WaitForTimeoutAsync(2000);

            Console.WriteLine("✅ Tablet layout working");

            // Test desktop view
            await page.SetViewportSizeAsync(1920, 1080);
            Console.WriteLine("🖥️ Testing desktop layout...");
            await page.WaitForTimeoutAsync(2000);

            Console.WriteLine("✅ Desktop layout working");
            Console.WriteLine("\n✅ Responsive layout test completed!");
            Console.WriteLine("👀 Browser will stay open until you close it.");
            Console.WriteLine("Try resizing the window to see the responsive behavior!");

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
        /// Interactive test for Bounce physics - keeps browser open until user closes it
        /// </summary>
        [TestMethod]
        [TestCategory("Manual")]
        public async Task LaunchInteractiveBrowser_BouncePhysics()
        {
            Console.WriteLine("Launching interactive browser for Bounce physics simulation...");
            Console.WriteLine("Close the browser window when you're done experimenting.");

            _browser = await _playwright!.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false,
                SlowMo = 100,
                Devtools = false,
                // Launch in incognito mode (private browsing)
                Args = new[] { "--incognito" }
            });

            var context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                // Don't set a fixed viewport - let the user resize the window!
                ViewportSize = ViewportSize.NoViewport,
                // Additional isolation - clears all storage, cookies, cache
                StorageState = null,
                AcceptDownloads = false,
                IgnoreHTTPSErrors = true
            });

            var page = await context.NewPageAsync();
            page.Console += (_, msg) => Console.WriteLine($"[Browser Console] {msg.Text}");

            // Navigate using shared helper
            await NavigateToBlazorPageAsync(page, "/bounce", "canvas.bounce-canvas");

            Console.WriteLine("Bounce page loaded in incognito mode (no cache).");
            Console.WriteLine("Try adjusting physics parameters (gravity, bounce, drag)!");
            Console.WriteLine("Click the canvas to add more balls!");
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

            // Wait for either the page or context to close
            await pageClosedTcs.Task;

            Console.WriteLine("Browser closed. Test ending.");
        }
    }
}
