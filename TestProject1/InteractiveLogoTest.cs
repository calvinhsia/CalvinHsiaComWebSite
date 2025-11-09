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
        /// Interactive test for Logo game
        /// </summary>
        [TestMethod]
        [TestCategory("Manual")]
        public async Task LaunchInteractiveBrowser_LogoGame()
        {
            Console.WriteLine("Launching interactive browser for Logo game...");
            Console.WriteLine("Close the browser window when you're done experimenting.");

            _browser = await _playwright!.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false,
                SlowMo = 100,
                Devtools = true
            });

            var context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1280, Height = 720 }
            });

            var page = await context.NewPageAsync();
            page.Console += (_, msg) => Console.WriteLine($"[Browser Console] {msg.Text}");

            // Navigate using shared helper
            await NavigateToBlazorPageAsync(page, "/logo", "canvas#logoCanvas");

            Console.WriteLine("Logo game loaded. Interact with it in the browser window.");
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
        /// Test Logo game commands
        /// Demonstrates JavaScript execution and canvas capture
        /// </summary>
        [TestMethod]
        public async Task AutomatedTest_LogoGameCommands()
        {
            Console.WriteLine("Testing Logo game commands...");

            // Use helper to get appropriate browser options
     _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());

            var page = await _browser.NewPageAsync();

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
