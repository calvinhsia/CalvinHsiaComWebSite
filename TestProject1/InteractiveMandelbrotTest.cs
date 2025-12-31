using Microsoft.Playwright;

namespace TestProject1
{
    [TestClass]
    public class InteractiveMandelbrotTest : InteractiveTestBase
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
        /// Interactive test for Mandelbrot - keeps browser open until user closes it
        /// </summary>
        [TestMethod]
        [TestCategory("Manual")]
        public async Task LaunchInteractiveBrowser_MandelbrotExplorer()
        {
            Console.WriteLine("Launching interactive browser for Mandelbrot Explorer...");
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

            await NavigateToBlazorPageAsync(page, "/mandelbrot", "canvas.mandelbrot-canvas");

            Console.WriteLine("Mandelbrot Explorer loaded in incognito mode.");
            Console.WriteLine("?? Left-click to zoom in at a point");
            Console.WriteLine("?? Right-click to zoom out");
            Console.WriteLine("?? Try the preset locations (Seahorse, Deep Spiral, etc.)");
            Console.WriteLine("?? Adjust iterations for more detail at deep zooms");
            Console.WriteLine("The test will wait until you close the browser.");

            var pageClosedTcs = new TaskCompletionSource<bool>();
            page.Close += (_, _) => pageClosedTcs.TrySetResult(true);
            context.Close += (_, _) => pageClosedTcs.TrySetResult(true);

            await pageClosedTcs.Task;
            Console.WriteLine("Browser closed. Test ending.");
        }

        /// <summary>
        /// Automated test: Verify Mandelbrot renders on page load
        /// </summary>
        [TestMethod]
        [TestCategory("Interactive")]
        public async Task Mandelbrot_RendersOnPageLoad()
        {
            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            await NavigateToBlazorPageAsync(page, "/mandelbrot", "canvas.mandelbrot-canvas");

            Console.WriteLine("?? Testing: Mandelbrot should render on page load");

            // Wait for render
            await page.WaitForTimeoutAsync(3000);

            // Check that zoom level is displayed
            var stats = await page.Locator(".mandelbrot-stats").TextContentAsync();
            Console.WriteLine($"  ?? Stats: {stats}");

            Assert.IsTrue(stats?.Contains("Zoom") ?? false, "Stats should show zoom level");

            // Verify canvas is visible
            var canvas = page.Locator("canvas.mandelbrot-canvas");
            var isVisible = await canvas.IsVisibleAsync();
            Assert.IsTrue(isVisible, "Canvas should be visible");

            Console.WriteLine("  ? TEST PASSED: Mandelbrot rendered on page load");
            await page.WaitForTimeoutAsync(1000);
        }

        /// <summary>
        /// Automated test: Verify zoom in works
        /// </summary>
        [TestMethod]
        [TestCategory("Interactive")]
        public async Task Mandelbrot_ZoomIn_IncreasesZoomLevel()
        {
            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            await NavigateToBlazorPageAsync(page, "/mandelbrot", "canvas.mandelbrot-canvas");

            Console.WriteLine("?? Testing: Zoom in should increase zoom level");

            // Wait for initial render
            await page.WaitForTimeoutAsync(2000);

            // Get initial zoom
            var stats1 = await page.Locator(".mandelbrot-stats").TextContentAsync();
            var zoom1Match = System.Text.RegularExpressions.Regex.Match(stats1 ?? "", @"Zoom:\s+([\d.]+)");
            var zoom1 = zoom1Match.Success ? double.Parse(zoom1Match.Groups[1].Value) : 0;
            Console.WriteLine($"  ?? Initial zoom: {zoom1}");

            // Click zoom in button
            var zoomInBtn = page.Locator("button:has-text('Zoom In')");
            await zoomInBtn.ClickAsync();

            // Wait for re-render
            await page.WaitForTimeoutAsync(2000);

            // Get new zoom
            var stats2 = await page.Locator(".mandelbrot-stats").TextContentAsync();
            var zoom2Match = System.Text.RegularExpressions.Regex.Match(stats2 ?? "", @"Zoom:\s+([\d.]+)");
            var zoom2 = zoom2Match.Success ? double.Parse(zoom2Match.Groups[1].Value) : 0;
            Console.WriteLine($"  ?? After zoom in: {zoom2}");

            Assert.IsTrue(zoom2 > zoom1, $"Zoom should increase: {zoom1} -> {zoom2}");

            Console.WriteLine("  ? TEST PASSED: Zoom in increased zoom level");
            await page.WaitForTimeoutAsync(1000);
        }

        /// <summary>
        /// Automated test: Verify reset button works
        /// </summary>
        [TestMethod]
        [TestCategory("Interactive")]
        public async Task Mandelbrot_Reset_RestoresDefaultView()
        {
            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            await NavigateToBlazorPageAsync(page, "/mandelbrot", "canvas.mandelbrot-canvas");

            Console.WriteLine("?? Testing: Reset should restore default view");

            // Wait for initial render
            await page.WaitForTimeoutAsync(2000);

            // Zoom in a few times
            var zoomInBtn = page.Locator("button:has-text('Zoom In')");
            await zoomInBtn.ClickAsync();
            await page.WaitForTimeoutAsync(1500);
            await zoomInBtn.ClickAsync();
            await page.WaitForTimeoutAsync(1500);

            // Get zoomed zoom level
            var zoomedStats = await page.Locator(".mandelbrot-stats").TextContentAsync();
            var zoomedMatch = System.Text.RegularExpressions.Regex.Match(zoomedStats ?? "", @"Zoom:\s+([\d.]+)");
            var zoomedLevel = zoomedMatch.Success ? double.Parse(zoomedMatch.Groups[1].Value) : 0;
            Console.WriteLine($"  ?? Zoomed level: {zoomedLevel}");

            Assert.IsTrue(zoomedLevel > 1, "Should be zoomed in");

            // Click reset
            var resetBtn = page.Locator("button:has-text('Reset')");
            await resetBtn.ClickAsync();
            await page.WaitForTimeoutAsync(2000);

            // Get reset zoom level
            var resetStats = await page.Locator(".mandelbrot-stats").TextContentAsync();
            var resetMatch = System.Text.RegularExpressions.Regex.Match(resetStats ?? "", @"Zoom:\s+([\d.]+)");
            var resetLevel = resetMatch.Success ? double.Parse(resetMatch.Groups[1].Value) : 0;
            Console.WriteLine($"  ?? After reset: {resetLevel}");

            Assert.AreEqual(1.0, resetLevel, 0.1, "Zoom should be back to 1.00x");

            Console.WriteLine("  ? TEST PASSED: Reset restored default view");
            await page.WaitForTimeoutAsync(1000);
        }
    }
}
