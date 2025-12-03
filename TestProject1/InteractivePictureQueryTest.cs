using Microsoft.Playwright;

namespace TestProject1
{
    /// <summary>
    /// E2E tests for PictureQuery page - validates MyPix deserialization and UI functionality
    /// </summary>
    [TestClass]
    public class InteractivePictureQueryTest : InteractiveTestBase
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

        [TestInitialize]
        public new void BaseTestInitialize()
        {
            base.BaseTestInitialize();
        }

        /// <summary>
        /// Test that PictureQuery page loads without deserialization errors
        /// This catches WASM-specific System.Text.Json issues that unit tests miss
        /// </summary>
        [TestMethod]
        [TestCategory("Interactive")]
        public async Task PictureQuery_LoadsWithoutDeserializationErrors()
        {
            Console.WriteLine("?? Testing PictureQuery page load and deserialization...");

            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            var consoleErrors = new List<string>();
            var hasDeserializationError = false;

            page.Console += (_, msg) =>
            {
                var text = msg.Text;
                Console.WriteLine($"[Console {msg.Type}] {text}");

                if (msg.Type == "error")
                {
                    // Ignore MSAL/authentication errors as they're expected when not logged in
                    if (!text.Contains("ERR_NAME_NOT_RESOLVED") && 
                        !text.Contains("logincdn.msauth.net"))
                    {
                        consoleErrors.Add(text);
                        
                        if (text.Contains("DeserializeNoConstructor") || 
                            text.Contains("JsonConstructorAttribute") ||
                            text.Contains("MyPix"))
                        {
                            hasDeserializationError = true;
                            Console.WriteLine("? DESERIALIZATION ERROR DETECTED!");
                        }
                    }
                }
            };

            page.PageError += (_, error) =>
            {
                Console.WriteLine($"[Page Error] {error}");
                // Ignore MSAL/security errors from login page
                if (error.Contains("MyPix") && 
                    !error.Contains("SecurityError") && 
                    !error.Contains("msauth.net"))
                {
                    hasDeserializationError = true;
                }
            };

            // Navigate to PictureQuery page
            await NavigateToBlazorPageAsync(page, "/PictureQuery", 
                waitForSelector: null, // Auth required, so don't wait for content
                navigationTimeout: 30000);

            Console.WriteLine("? Page navigation complete");

            // Check if auth is required
            var authRequired = await page.Locator("text=Authentication Required").IsVisibleAsync();
            
            if (authRequired)
            {
                Console.WriteLine("?? Authentication required - this is expected behavior");
                Console.WriteLine("? Page loaded without errors (auth wall shown)");
            }
            else
            {
                // Wait a bit for any query operations
                await Task.Delay(2000);

                // Check for filter input to verify page loaded
                var filterInput = page.Locator("#NotesFilter");
                var isFilterVisible = await filterInput.IsVisibleAsync();

                if (isFilterVisible)
                {
                    Console.WriteLine("? PictureQuery page loaded successfully with auth");
                    Console.WriteLine("? Filter input is visible");
                }
            }

            // Take screenshot
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = "picturequery-load-test.png",
                FullPage = true
            });
            Console.WriteLine("?? Screenshot saved: picturequery-load-test.png");

            // Assert no deserialization errors
            Assert.IsFalse(hasDeserializationError, 
                "MyPix deserialization error detected! Check that MyPix has a parameterless constructor.");

            if (consoleErrors.Any())
            {
                Console.WriteLine($"\n?? Console errors found ({consoleErrors.Count}):");
                foreach (var error in consoleErrors)
                {
                    Console.WriteLine($"  - {error}");
                }
            }
            else
            {
                Console.WriteLine("\n? No console errors detected");
            }

            await Task.Delay(1000);
        }

        /// <summary>
        /// Manual interactive test - allows user to interact with PictureQuery page
        /// </summary>
        [TestMethod]
        [TestCategory("Manual")]
        public async Task Manual_PictureQuery_InteractiveBrowser()
        {
            Console.WriteLine("?? Launching interactive browser for PictureQuery page...");
            Console.WriteLine("?? Use this to:");
            Console.WriteLine("  - Test filter functionality");
            Console.WriteLine("  - Verify image loading");
            Console.WriteLine("  - Test album creation");
            Console.WriteLine("Close the browser window when done.");

            _browser = await _playwright!.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false,
                SlowMo = 100,
                Devtools = true
            });

            var context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1400, Height = 1200 },
                IgnoreHTTPSErrors = true
            });

            var page = await context.NewPageAsync();
            page.Console += (_, msg) => Console.WriteLine($"[Browser Console] {msg.Text}");

            await NavigateToBlazorPageAsync(page, "/PictureQuery", 
                waitForSelector: null,
                navigationTimeout: 60000);

            Console.WriteLine("? PictureQuery page loaded. Interact with it in the browser.");

            // Wait for page close
            var pageClosedTcs = new TaskCompletionSource<bool>();
            page.Close += (_, _) => pageClosedTcs.TrySetResult(true);
            context.Close += (_, _) => pageClosedTcs.TrySetResult(true);

            await pageClosedTcs.Task;
            Console.WriteLine("Browser closed. Test ending.");
        }
    }
}
