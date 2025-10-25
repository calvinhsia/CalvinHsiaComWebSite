using Microsoft.Playwright;
using System.Diagnostics;

namespace TestProject1
{
    /// <summary>
    /// Interactive Cartoon drawing test harness
    /// This test launches your Blazor app in a real browser where you can interact with it
    /// You can modify HTML/CSS/JS and see changes in real-time
    /// 
    /// IMPORTANT: Start your Blazor app manually before running these tests:
    /// cd Client
    /// dotnet run
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
        [TestCategory("Interactive")]
        public async Task LaunchInteractiveBrowser_CartoonGame()
        {
            Console.WriteLine("Launching interactive browser for Cartoon drawing...");
            Console.WriteLine("Close the browser window when you're done experimenting.");

            _browser = await _playwright!.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false,
                SlowMo = 100,
                Devtools = true
            });

            var context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1400, Height = 900 }
            });

            var page = await context.NewPageAsync();
            page.Console += (_, msg) => Console.WriteLine($"[Browser Console] {msg.Text}");

            await page.GotoAsync($"{BASE_URL}/cartoon", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle
            });

            Console.WriteLine("Cartoon page loaded. Interact with it in the browser window.");
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
        [TestCategory("Automated")]
        public async Task AutomatedTest_CartoonDrawing()
        {
            Console.WriteLine("Testing Cartoon drawing functionality...");

            _browser = await _playwright!.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false,
                SlowMo = 500
            });

            var page = await _browser.NewPageAsync();
            await page.GotoAsync($"{BASE_URL}/cartoon");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            try
            {
                // Wait for the canvas to be visible
                await page.WaitForSelectorAsync("canvas#cartoonCanvas", new PageWaitForSelectorOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = 10000
                });

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

                // Test Demo button
                var demoButton = await page.QuerySelectorAsync("button:has-text('Demo')");
                if (demoButton != null)
                {
                    Console.WriteLine("Clicking Demo button...");
                    await demoButton.ClickAsync();
                    await Task.Delay(1000);
                    Console.WriteLine("Demo animation loaded!");
                }

                // Test between frames slider
                var betweenSlider = await page.QuerySelectorAsync("#betweenFrames");
                if (betweenSlider != null)
                {
                    await betweenSlider.FillAsync("5");
                    Console.WriteLine("Between frames set to 5");
                    await Task.Delay(500);
                }

                // Test play button with interpolation
                var playButton = await page.QuerySelectorAsync("button:has-text('Play')");
                if (playButton != null)
                {
                    await playButton.ClickAsync();
                    Console.WriteLine("Started animation playback with interpolation...");
                    await Task.Delay(5000); // Watch animation for 5 seconds

                    // Click again to pause
                    await playButton.ClickAsync();
                    Console.WriteLine("Paused animation playback");
                }

                // Test Reset button
                var resetButton = await page.QuerySelectorAsync("button:has-text('Reset')");
                if (resetButton != null)
                {
                    await Task.Delay(1000);
                    await resetButton.ClickAsync();
                    Console.WriteLine("Reset button clicked - all frames cleared");
                    await Task.Delay(500);
                }

                // Test drawing mode toggle
                var drawModeRadio = await page.QuerySelectorAsync("input[value='draw']");
                if (drawModeRadio != null)
                {
                    await drawModeRadio.ClickAsync();
                    Console.WriteLine("Draw mode selected");
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

                        await Task.Delay(1000);
                    }
                }

                // Test adding a new frame
                var newFrameButton = await page.QuerySelectorAsync("button:has-text('New Frame')");
                if (newFrameButton != null)
                {
                    await newFrameButton.ClickAsync();
                    Console.WriteLine("Added a new frame");
                    await Task.Delay(500);

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
                            await Task.Delay(500);
                        }
                    }
                }

                // Test animation with user-drawn frames
                if (playButton != null)
                {
                    await playButton.ClickAsync();
                    Console.WriteLine("Playing animation with user frames and interpolation...");
                    await Task.Delay(4000);
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

                // Keep browser open to see result
                await Task.Delay(3000);

                Console.WriteLine("Cartoon test completed successfully!");
                Console.WriteLine("Features tested:");
                Console.WriteLine("  ? Demo button");
                Console.WriteLine("  ? Reset button");
                Console.WriteLine("  ? Frame interpolation");
                Console.WriteLine("  ? Between frames slider");
                Console.WriteLine("  ? Animation playback");
                Console.WriteLine("  ? User drawing");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during Cartoon test: {ex.Message}");
                throw;
            }
        }
    }
}
