using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace TestProject1
{
    [TestClass]
    [TestCategory("Interactive")]
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
      public async Task Fish_Interactive_CellularAutomata()
  {
    _browser = await _playwright!.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
         {
    Headless = false,
              SlowMo = 300
        });

   var page = await _browser.NewPageAsync();

    // Navigate to the Fish page
          await page.GotoAsync($"{BASE_URL}/fish");
    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

  // Wait for canvas to be visible
        var canvas = page.Locator("canvas.fish-canvas");
            await Expect(canvas).ToBeVisibleAsync();

            Console.WriteLine("? Fish page loaded successfully");
            Console.WriteLine("?? Testing Fish vs Sharks cellular automata...");

   // Wait for initial render
            await page.WaitForTimeoutAsync(1000);

        // Check that stats are showing
            var statsText = await page.Locator(".fish-stats").TextContentAsync();
        Console.WriteLine($"?? Initial stats: {statsText}");

            // Test resume button (starts paused by default)
        var resumeButton = page.Locator("button:has-text('Resume')");
  await resumeButton.ClickAsync();
            Console.WriteLine("?? Started simulation");
 await page.WaitForTimeoutAsync(3000);

            // Check stats after running
            var runningStats = await page.Locator(".fish-stats").TextContentAsync();
        Console.WriteLine($"?? Running stats: {runningStats}");

            // Test pause button
 var pauseButton = page.Locator("button:has-text('Pause')");
      await pauseButton.ClickAsync();
            Console.WriteLine("?? Paused simulation");
        await page.WaitForTimeoutAsync(1000);

        // Test clicking on canvas to add fish (left-click)
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
    Console.WriteLine("?? Left-clicked canvas to add fish");
                await page.WaitForTimeoutAsync(500);

    // Right-click to add shark (using mouse down/up)
      await page.Mouse.MoveAsync(
             canvasBounds.X + canvasBounds.Width * 2 / 3,
        canvasBounds.Y + canvasBounds.Height * 2 / 3
            );
          await page.Mouse.DownAsync(new MouseDownOptions { Button = MouseButton.Right });
          await page.Mouse.UpAsync(new MouseUpOptions { Button = MouseButton.Right });
  Console.WriteLine("?? Right-clicked canvas to add shark");
         await page.WaitForTimeoutAsync(500);
            }

  // Test adjusting speed
  var speedSlider = page.Locator("input[type='range']");
            await speedSlider.FillAsync("30");
       Console.WriteLine("? Adjusted speed to 30 gen/sec");
     await page.WaitForTimeoutAsync(500);

            // Resume and let it run
            await resumeButton.ClickAsync();
    Console.WriteLine("?? Resumed simulation");
            await page.WaitForTimeoutAsync(5000);

        // Test reset button
      await pauseButton.ClickAsync();
      var resetButton = page.Locator("button:has-text('Reset')");
          await resetButton.ClickAsync();
            Console.WriteLine("?? Reset simulation");
await page.WaitForTimeoutAsync(1000);

      // Test changing parameters
 var fishBreedAge = page.Locator("input[type='number']").First;
       await fishBreedAge.FillAsync("5");
  Console.WriteLine("?? Changed fish breed age to 5");
            await page.WaitForTimeoutAsync(500);

            // Test toggling circles
     var circlesCheckbox = page.Locator("input[type='checkbox']").First;
         await circlesCheckbox.ClickAsync();
          Console.WriteLine("? Toggled circles display");
     await page.WaitForTimeoutAsync(1000);

    // Resume for final observation
    await resumeButton.ClickAsync();
            Console.WriteLine("?? Running final simulation...");
       await page.WaitForTimeoutAsync(5000);

            // Final stats
   var finalStats = await page.Locator(".fish-stats").TextContentAsync();
    Console.WriteLine($"?? Final stats: {finalStats}");

  Console.WriteLine("? Fish interactive test completed successfully!");
            Console.WriteLine("??? Watch the fish and sharks ecosystem for a few more seconds...");

    // Let the user observe the final result
      await page.WaitForTimeoutAsync(5000);
        }

  [TestMethod]
        public async Task Fish_Interactive_ParameterTesting()
        {
            _browser = await _playwright!.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
  {
          Headless = false,
  SlowMo = 200
       });

            var page = await _browser.NewPageAsync();
    await page.GotoAsync($"{BASE_URL}/fish");
 await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

    Console.WriteLine("?? Testing different parameter configurations...");

            // Test 1: Fast fish breeding
       Console.WriteLine("\n?? Test 1: Fast fish breeding");
   var fishBreedInputs = await page.Locator("text='Breed Age:'").Locator("..").Locator("input[type='number']").AllAsync();
   if (fishBreedInputs.Count > 0)
        {
await fishBreedInputs[0].FillAsync("1");
 Console.WriteLine("  ? Set fish breed age to 1");
          }

            var resumeButton = page.Locator("button:has-text('Resume')");
        await resumeButton.ClickAsync();
    Console.WriteLine("  ?? Running simulation...");
   await page.WaitForTimeoutAsync(5000);

            var stats1 = await page.Locator(".fish-stats").TextContentAsync();
     Console.WriteLine($"  ?? Result: {stats1}");

            // Reset
            var pauseButton = page.Locator("button:has-text('Pause')");
      await pauseButton.ClickAsync();
            var resetButton = page.Locator("button:has-text('Reset')");
            await resetButton.ClickAsync();
            await page.WaitForTimeoutAsync(1000);

          // Test 2: Sharks starve quickly
            Console.WriteLine("\n?? Test 2: Sharks starve quickly");
         var sharkStarve = page.Locator("text='Shark Starve:'").Locator("..").Locator("input[type='number']");
   await sharkStarve.FillAsync("2");
        Console.WriteLine("  ? Set shark starvation time to 2");

            await resumeButton.ClickAsync();
            Console.WriteLine("  ?? Running simulation...");
            await page.WaitForTimeoutAsync(5000);

            var stats2 = await page.Locator(".fish-stats").TextContentAsync();
Console.WriteLine($"  ?? Result: {stats2}");

          await pauseButton.ClickAsync();
            await resetButton.ClickAsync();
       await page.WaitForTimeoutAsync(1000);

   // Test 3: Torus vs bounded
       Console.WriteLine("\n?? Test 3: Testing torus mode");
   var torusCheckbox = page.Locator("text='Torus (wrap edges)'").Locator("..").Locator("input[type='checkbox']");
      await torusCheckbox.ClickAsync();
            Console.WriteLine("  ? Disabled torus mode");

    await resumeButton.ClickAsync();
        Console.WriteLine("  ?? Running simulation...");
     await page.WaitForTimeoutAsync(5000);

            var stats3 = await page.Locator(".fish-stats").TextContentAsync();
            Console.WriteLine($"  ?? Result: {stats3}");

         Console.WriteLine("\n? Parameter testing completed!");
            await page.WaitForTimeoutAsync(3000);
        }

        /// <summary>
        /// Interactive test for Fish - keeps browser open until user closes it
        /// </summary>
 [TestMethod]
        [TestCategory("Interactive")]
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

       await page.GotoAsync($"{BASE_URL}/fish", new PageGotoOptions
            {
 WaitUntil = WaitUntilState.NetworkIdle
  });

       Console.WriteLine("Fish vs Sharks page loaded in incognito mode.");
      Console.WriteLine("?? Left-click to add fish");
            Console.WriteLine("?? Right-click to add sharks");
            Console.WriteLine("?? Try different parameter combinations!");
  Console.WriteLine("?? Click Export to download population data");
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
    }
}
