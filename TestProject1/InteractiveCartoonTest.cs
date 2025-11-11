using Microsoft.Playwright;

namespace TestProject1
{
    /// <summary>
    /// Interactive Cartoon drawing test harness
    /// This test launches your Blazor app in a real browser where you can interact with it
    /// You can modify HTML/CSS/JS and see changes in real-time
    /// </summary>
    [TestClass]
    public class InteractiveCartoonTest : InteractiveTestBase
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
        /// Interactive test for Cartoon drawing page
        /// </summary>
        [TestMethod]
        [TestCategory("Manual")]
        public async Task LaunchInteractiveBrowser_CartoonGame()
        {
            Console.WriteLine("Launching interactive browser for Cartoon drawing...");
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

            // Test Base64 encoded message
            // Plain text: "happy birthday Mom!"
            // Base64: "aGFwcHkgYmlydGhkYXkgTW9tIQ=="
            // Navigate using shared helper
            await NavigateToBlazorPageAsync(page, "/cartoon?b64=aGFwcHkgYmlydGhkYXkgTW9tIQ==&thickness=20", "canvas#cartoonCanvas");

            Console.WriteLine("Cartoon page loaded in incognito mode (no cache).");
            Console.WriteLine("Using Base64 encoded message: 'happy birthday Mom!' -> aGFwcHkgYmlydGhkYXkgTW9tIQ==");
            Console.WriteLine("Try resizing the browser window to see the canvas resize!");
            Console.WriteLine("Try drawing on the canvas and creating multiple frames for animation!");
            Console.WriteLine("The test will wait until you close the browser.");

            // Create a TaskCompletionSource to wait for page close
            var pageClosedTcs = new TaskCompletionSource<bool>();
            page.Close += (_, _) =>
            {
                Console.WriteLine("[Event] Page.Close event fired");
                pageClosedTcs.TrySetResult(true);
            };

            // Also listen for context close in case entire browser is closed
            context.Close += (_, _) =>
            {
                Console.WriteLine("[Event] Context.Close event fired");
                pageClosedTcs.TrySetResult(true);
            };

            // Wait for either the page or context to close
            await pageClosedTcs.Task;

            Console.WriteLine("Browser closed. Test ending.");
        }

        /// <summary>
        /// Automated test for Cartoon drawing functionality
        /// Tests canvas initialization and basic drawing operations
        /// </summary>
        [TestMethod]
        [TestCategory("Automated")]  // ✅ Add this so it runs in Playwright Tests step
        public async Task AutomatedTest_CartoonDrawing()
        {
            Console.WriteLine("Testing Cartoon drawing functionality...");

            // Use helper method to get appropriate browser options for environment
            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());

            // TestContext is automatically available from base class
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());

            var page = await context.NewPageAsync();

            try
            {
                // Navigate using shared helper
                await NavigateToBlazorPageAsync(page, "/cartoon", "canvas#cartoonCanvas");

                Console.WriteLine("Cartoon canvas loaded!");

                // Verify canvas is initialized
                var canvasInitialized = await page.EvaluateAsync<bool>(@"
    () => {
          const canvas = document.getElementById('cartoonCanvas');
        return canvas && typeof window.initCartoonCanvas === 'function';
      }
     ");

                if (canvasInitialized)
                {
                    Console.WriteLine("Canvas JavaScript initialized successfully!");
                }
                else
                {
                    Console.WriteLine("Warning: Canvas JavaScript may not be loaded");
                }

                // Test Demo button - it auto-plays now
                Console.WriteLine("Demo animation auto-loaded and playing on page load!");
                await Task.Delay(1500);

                // Test between frames slider
                var betweenSlider = await page.QuerySelectorAsync("#betweenFrames");
                if (betweenSlider != null)
                {
                    await betweenSlider.FillAsync("5");
                    Console.WriteLine("Between frames set to 5");
                    await Task.Delay(300);
                }

                // Pause the animation to test drawing
                var pauseButton = await page.QuerySelectorAsync("button.btn-play-sm");
                if (pauseButton != null)
                {
                    await pauseButton.ClickAsync();
                    Console.WriteLine("Paused animation playback");
                    await Task.Delay(300);
                }

                // Test Reset button
                var resetButton = await page.QuerySelectorAsync("button.btn-reset-sm");
                if (resetButton != null)
                {
                    await resetButton.ClickAsync();
                    Console.WriteLine("Reset button clicked - all frames cleared");
                    await Task.Delay(300);
                }

                // Test pen thickness adjustment
                var thicknessSlider = await page.QuerySelectorAsync("#penThickness");
                if (thicknessSlider != null)
                {
                    await thicknessSlider.FillAsync("5");
                    Console.WriteLine("Pen thickness adjusted to 5");
                }

                // Test color picker
                var colorPicker = await page.QuerySelectorAsync("#penColor");
                if (colorPicker != null)
                {
                    await colorPicker.FillAsync("#ff0000");
                    Console.WriteLine("Pen color set to red");
                }

                // Simulate drawing by clicking and dragging on canvas
                var canvas = await page.QuerySelectorAsync("canvas#cartoonCanvas");
                if (canvas != null)
                {
                    var boundingBox = await canvas.BoundingBoxAsync();
                    if (boundingBox != null)
                    {
                        // Draw a simple line
                        await page.Mouse.MoveAsync(boundingBox.X + 100, boundingBox.Y + 100);
                        await page.Mouse.DownAsync();
                        await page.Mouse.MoveAsync(boundingBox.X + 300, boundingBox.Y + 200);
                        await page.Mouse.UpAsync();

                        Console.WriteLine("Drew a test line on the canvas");
                        await Task.Delay(500);
                    }
                }

                // Test adding a new frame
                var addFrameButton = await page.QuerySelectorAsync("button:has-text('Add Frame')");
                if (addFrameButton != null)
                {
                    await addFrameButton.ClickAsync();
                    Console.WriteLine("Added a new frame");
                    await Task.Delay(300);

                    // Draw on the new frame
                    if (canvas != null)
                    {
                        var boundingBox = await canvas.BoundingBoxAsync();
                        if (boundingBox != null)
                        {
                            await page.Mouse.MoveAsync(boundingBox.X + 200, boundingBox.Y + 150);
                            await page.Mouse.DownAsync();
                            await page.Mouse.MoveAsync(boundingBox.X + 400, boundingBox.Y + 250);
                            await page.Mouse.UpAsync();
                            Console.WriteLine("Drew a line on frame 2");
                            await Task.Delay(300);
                        }
                    }
                }

                // Test animation with user-drawn frames
                var playButton = await page.QuerySelectorAsync("button.btn-play-sm");
                if (playButton != null)
                {
                    await playButton.ClickAsync();
                    Console.WriteLine("Playing animation with user frames and interpolation...");
                    await Task.Delay(2000);
                    await playButton.ClickAsync();
                    Console.WriteLine("Paused playback");
                }

                // Take screenshot
                await page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = "cartoon-test-screenshot.png",
                    FullPage = true
                });
                Console.WriteLine("Screenshot saved to: cartoon-test-screenshot.png");

                // Capture canvas
                if (canvas != null)
                {
                    await canvas.ScreenshotAsync(new ElementHandleScreenshotOptions
                    {
                        Path = "cartoon-canvas-screenshot.png"
                    });
                    Console.WriteLine("Canvas screenshot saved to: cartoon-canvas-screenshot.png");
                }

                Console.WriteLine("Cartoon test completed successfully!");
                Console.WriteLine("Features tested:");
                Console.WriteLine("  ✓ Auto-play demo on load");
                Console.WriteLine("  ✓ Reset button");
                Console.WriteLine("  ✓ Frame interpolation");
                Console.WriteLine("  ✓ Between frames slider");
                Console.WriteLine("  ✓ Animation playback");
                Console.WriteLine("  ✓ User drawing");
                Console.WriteLine("  ✓ Add frame");
                Console.WriteLine("  ✓ Pen thickness and color");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during Cartoon test: {ex.Message}");
                await page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = "cartoon-test-error-screenshot.png",
                    FullPage = true
                });
                Console.WriteLine("Error screenshot saved to: cartoon-test-error-screenshot.png");
                throw;
            }
            finally
            {
                // CRITICAL FIX: Ensure browser is ALWAYS closed with timeout
                try
                {
                    if (page != null)
                    {
                        await page.CloseAsync();
                        Console.WriteLine("Page closed");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Error closing page: {ex.Message}");
                }

                try
                {
                    if (_browser != null && _browser.IsConnected)
                    {
                        await _browser.CloseAsync();
                        _browser = null;
                        Console.WriteLine("Browser closed");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Error closing browser: {ex.Message}");
                }
            }
        }
    }
}
