using Microsoft.Playwright;
using System.Diagnostics;

namespace TestProject1
{
    /// <summary>
    /// Interactive Logo game test harness
    /// This test launches your Blazor app in a real browser where you can interact with it
    /// You can modify HTML/CSS/JS and see changes in real-time
    /// 
    /// IMPORTANT: Start your Blazor app manually before running these tests:
    /// cd Client
    /// dotnet run
    /// </summary>
    [TestClass]
    public class InteractiveLogoTest : InteractiveTestBase
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
        /// Override the base test cleanup to prevent it from closing the browser
        /// during interactive tests
        /// </summary>
        [TestCleanup]
        public new async Task TestCleanup()
        {
            // For interactive tests, we DON'T want to close the browser automatically
            // The test itself handles browser lifecycle
            Console.WriteLine("[TestCleanup] Skipping automatic browser close for interactive test");
            await Task.CompletedTask;
        }

        /// <summary>
        /// Interactive test for Logo game
        /// </summary>
        [TestMethod]
        [TestCategory("Manual")]
        [Timeout(int.MaxValue)] // ? Use max int value for effectively infinite timeout (24+ days)
        public async Task LaunchInteractiveBrowser_LogoGame()
        {
            Console.WriteLine("========================================");
            Console.WriteLine("?? INTERACTIVE LOGO TEST STARTING");
            Console.WriteLine("========================================");
            Console.WriteLine("Launching interactive browser for Logo game...");
            Console.WriteLine("Close the browser window when you're done experimenting.");

            try
            {
                Console.WriteLine("Step 1: Launching Chromium browser...");
                _browser = await _playwright!.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = false,
                    SlowMo = 100,
                    Devtools = true,
                    Timeout = 0 // ? Disable launch timeout
                });
                Console.WriteLine("? Browser launched successfully");

                Console.WriteLine("Step 2: Creating browser context...");
                var context = await _browser.NewContextAsync(new BrowserNewContextOptions
                {
                    ViewportSize = ViewportSize.NoViewport,
                    IgnoreHTTPSErrors = true
                });
                Console.WriteLine("? Context created successfully");

                Console.WriteLine("Step 3: Creating new page...");
                var page = await context.NewPageAsync();
                Console.WriteLine("? Page created successfully");

                page.Console += (_, msg) => Console.WriteLine($"[Browser Console] {msg.Text}");

                // Set default timeout to 0 (infinite) for this page
                Console.WriteLine("Step 4: Setting infinite timeout...");
                page.SetDefaultTimeout(0); // ? Disable all Playwright timeouts
                Console.WriteLine("? Timeout set to infinite");

                // Navigate using shared helper
                Console.WriteLine("Step 5: Navigating to Logo page...");
                await NavigateToBlazorPageAsync(page, "/logo", "canvas#logoCanvas");
                Console.WriteLine("? Navigation complete");

                Console.WriteLine("");
                Console.WriteLine("========================================");
                Console.WriteLine("? LOGO GAME LOADED SUCCESSFULLY!");
                Console.WriteLine("========================================");
                Console.WriteLine("?? TIP: This test has NO timeout - it will wait indefinitely!");
                Console.WriteLine("?? Interact with the Logo game in the browser window");
                Console.WriteLine("?? Close the browser window to end the test");
                Console.WriteLine("");
                
                // Create a TaskCompletionSource to wait for page close
                var pageClosedTcs = new TaskCompletionSource<bool>();
                
                Console.WriteLine("Setting up event handlers...");
                page.Close += (_, _) =>
                {
                    Console.WriteLine("[Event] ? Page.Close event fired");
                    pageClosedTcs.TrySetResult(true);
                };

                context.Close += (_, _) =>
                {
                    Console.WriteLine("[Event] ? Context.Close event fired");
                    pageClosedTcs.TrySetResult(true);
                };

                // ? Add keep-alive heartbeat to show test is still running
                var heartbeatCts = new CancellationTokenSource();
                var heartbeatTask = Task.Run(async () =>
                {
                    var startTime = DateTime.Now;
                    Console.WriteLine($"[Keep-Alive] ?? Heartbeat started at {startTime:HH:mm:ss}");
                    
                    while (!heartbeatCts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            await Task.Delay(TimeSpan.FromMinutes(1), heartbeatCts.Token);
                            var elapsed = DateTime.Now - startTime;
                            Console.WriteLine($"[Keep-Alive] ?? Test still running... Elapsed: {elapsed:hh\\:mm\\:ss}");
                        }
                        catch (OperationCanceledException)
                        {
                            Console.WriteLine("[Keep-Alive] Heartbeat cancelled");
                            break;
                        }
                    }
                }, heartbeatCts.Token);

                Console.WriteLine("Waiting for browser to close...");
                
                // Wait for either the page or context to close
                await pageClosedTcs.Task;

                Console.WriteLine("Browser close detected, cleaning up...");

                // Stop heartbeat
                heartbeatCts.Cancel();
                try { await heartbeatTask; } catch { }

                Console.WriteLine("========================================");
                Console.WriteLine("? INTERACTIVE TEST COMPLETED");
                Console.WriteLine("========================================");
            }
            catch (Exception ex)
            {
                Console.WriteLine("========================================");
                Console.WriteLine($"? ERROR IN INTERACTIVE TEST");
                Console.WriteLine("========================================");
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Test Logo game commands
        /// Demonstrates JavaScript execution and canvas capture
        /// </summary>
        [TestMethod]
        [TestCategory("Automated")]  // ? Add this so it runs in Playwright Tests step
        public async Task AutomatedTest_LogoGameCommands()
        {
            Console.WriteLine("Testing Logo game commands...");

            // Use helper to get appropriate browser options
            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());

            // TestContext is automatically available from base class
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());

            var page = await context.NewPageAsync();

            // Navigate using shared helper
            await NavigateToBlazorPageAsync(page, "/logo", "canvas#logoCanvas");

            try
            {
                Console.WriteLine("Logo canvas loaded!");

                // FIXED: Use the correct class name from LogoGame.razor
                var codeEditor = await page.QuerySelectorAsync("textarea.logo-code-textarea");

                if (codeEditor != null)
                {
                    Console.WriteLine("Found code editor, entering Logo commands...");

                    // Clear existing content and type Logo commands
                    await codeEditor.FillAsync(@"
forward 100
right 90
forward 100
right 90
forward 100
right 90
forward 100
");

                    Console.WriteLine("Logo commands entered:");
                    Console.WriteLine("- Drawing a square");
                    // FIXED: Find the Run button using the correct class from LogoGame.razor
                    var runButton = await page.QuerySelectorAsync("button.logo-run-button");
                    if (runButton != null)
                    {
                        Console.WriteLine("Clicking Run button...");
                        await runButton.ClickAsync();

                        // Wait for animation to complete
                        await Task.Delay(3000);

                        Console.WriteLine("Logo commands executed!");
                    }
                    else
                    {
                        Console.WriteLine("Run button not found!");
                        // Take a screenshot to debug
                        await page.ScreenshotAsync(new PageScreenshotOptions
                        {
                            Path = "logo-debug-no-button.png",
                            FullPage = true
                        });
                    }
                }
                else
                {
                    Console.WriteLine("Code editor not found!");

                    // Take a screenshot to debug what's on the page
                    await page.ScreenshotAsync(new PageScreenshotOptions
                    {
                        Path = "logo-debug-no-editor.png",
                        FullPage = true
                    });

                    // Try to list what elements exist
                    var elementsFound = await page.EvaluateAsync<string>(@"
     () => {
           const textareas = document.querySelectorAll('textarea');
 const buttons = document.querySelectorAll('button');
                return `Textareas: ${textareas.length}, Buttons: ${buttons.length}`;
      }
        ");
                    Console.WriteLine($"Elements found on page: {elementsFound}");
                }

                // Capture the canvas
                var canvas = await page.QuerySelectorAsync("canvas#logoCanvas");
                if (canvas != null)
                {
                    await canvas.ScreenshotAsync(new ElementHandleScreenshotOptions
                    {
                        Path = "logo-canvas-screenshot.png"
                    });
                    Console.WriteLine("Canvas screenshot saved to: logo-canvas-screenshot.png");
                }

                // Take full page screenshot
                await page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = "logo-test-screenshot.png",
                    FullPage = true
                });

                Console.WriteLine("Full page screenshot saved to: logo-test-screenshot.png");

                // Get canvas state via JavaScript
                var canvasData = await page.EvaluateAsync<string>(@"
             () => {
          const canvas = document.querySelector('canvas#logoCanvas');
               if (canvas) {
       const ctx = canvas.getContext('2d');
            return canvas.toDataURL();
      }
              return null;
        }
            ");

                if (!string.IsNullOrEmpty(canvasData))
                {
                    Console.WriteLine($"Canvas data captured: {canvasData.Substring(0, 50)}...");
                }

                // Keep browser open to see result
                await Task.Delay(3000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during Logo test: {ex.Message}");
                throw;
            }
        }
    }
}
