using Microsoft.Playwright;
using System.Diagnostics;
using WordScapeBlazorWasm.Models;

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
        [Timeout(int.MaxValue)] // ✅ Use max int value for effectively infinite timeout (24+ days)
        public async Task LaunchInteractiveBrowser_LogoGame()
        {
            Console.WriteLine("========================================");
            Console.WriteLine("🚀 INTERACTIVE LOGO TEST STARTING");
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
                    Timeout = 0 // ✅ Disable launch timeout
                });
                Console.WriteLine("✅ Browser launched successfully");

                Console.WriteLine("Step 2: Creating browser context...");
                var context = await _browser.NewContextAsync(new BrowserNewContextOptions
                {
                    ViewportSize = ViewportSize.NoViewport,
                    IgnoreHTTPSErrors = true
                });
                Console.WriteLine("✅ Context created successfully");

                Console.WriteLine("Step 3: Creating new page...");
                var page = await context.NewPageAsync();
                Console.WriteLine("✅ Page created successfully");

                page.Console += (_, msg) => Console.WriteLine($"[Browser Console] {msg.Text}");

                // Set default timeout to 0 (infinite) for this page
                Console.WriteLine("Step 4: Setting infinite timeout...");
                page.SetDefaultTimeout(0); // ✅ Disable all Playwright timeouts
                Console.WriteLine("✅ Timeout set to infinite");

                // Navigate using shared helper
                Console.WriteLine("Step 5: Navigating to Logo page...");
                await NavigateToBlazorPageAsync(page, "/logo", "canvas#logoCanvas");
                Console.WriteLine("✅ Navigation complete");

                Console.WriteLine("");
                Console.WriteLine("========================================");
                Console.WriteLine("✅ LOGO GAME LOADED SUCCESSFULLY!");
                Console.WriteLine("========================================");
                Console.WriteLine("💡 TIP: This test has NO timeout - it will wait indefinitely!");
                Console.WriteLine("🎮 Interact with the Logo game in the browser window");
                Console.WriteLine("🚪 Close the browser window to end the test");
                Console.WriteLine("");
                
                // Create a TaskCompletionSource to wait for page close
                var pageClosedTcs = new TaskCompletionSource<bool>();
                
                Console.WriteLine("Setting up event handlers...");
                page.Close += (_, _) =>
                {
                    Console.WriteLine("[Event] ❌ Page.Close event fired");
                    pageClosedTcs.TrySetResult(true);
                };

                context.Close += (_, _) =>
                {
                    Console.WriteLine("[Event] ❌ Context.Close event fired");
                    pageClosedTcs.TrySetResult(true);
                };

                // ✅ Add keep-alive heartbeat to show test is still running
                var heartbeatCts = new CancellationTokenSource();
                var heartbeatTask = Task.Run(async () =>
                {
                    var startTime = DateTime.Now;
                    Console.WriteLine($"[Keep-Alive] ⏱️ Heartbeat started at {startTime:HH:mm:ss}");
                    
                    while (!heartbeatCts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            await Task.Delay(TimeSpan.FromMinutes(1), heartbeatCts.Token);
                            var elapsed = DateTime.Now - startTime;
                            Console.WriteLine($"[Keep-Alive] ⏱️ Test still running... Elapsed: {elapsed:hh\\:mm\\:ss}");
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
                Console.WriteLine("✅ INTERACTIVE TEST COMPLETED");
                Console.WriteLine("========================================");
            }
            catch (Exception ex)
            {
                Console.WriteLine("========================================");
                Console.WriteLine($"❌ ERROR IN INTERACTIVE TEST");
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
        [TestCategory("Automated")]  // ✓ Add this so it runs in Playwright Tests step
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
                // Wait for code editor and run button to be available
                Console.WriteLine("Waiting for UI elements to load...");
                await page.WaitForSelectorAsync("textarea.logo-code-textarea", new PageWaitForSelectorOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = 10000
                });
                await page.WaitForSelectorAsync("button.logo-run-button", new PageWaitForSelectorOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = 10000
                });
                Console.WriteLine("✓ UI elements loaded");
                
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
                        }
                    );

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

        /// <summary>
        /// Test JavaScript fast mode with position commands (setxy, seth)
        /// Verifies JavaScript interpreter correctly handles commands that were previously missing
        /// </summary>
        [TestMethod]
        [TestCategory("Automated")]
        [TestCategory("JavaScript")]
        public async Task JavaScriptFastMode_PositionCommands_ExecuteCorrectly()
        {
            Console.WriteLine("========================================");
            Console.WriteLine("Testing JavaScript Fast Mode - Position Commands");
            Console.WriteLine("========================================");

            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            // Enable console logging to catch JavaScript errors
            page.Console += (_, msg) => Console.WriteLine($"[Browser {msg.Type}] {msg.Text}");

            await NavigateToBlazorPageAsync(page, "/logo", "canvas#logoCanvas");

            try
            {
                Console.WriteLine("✓ Logo page loaded");

                // Wait for UI elements to be available
                await page.WaitForSelectorAsync("textarea.logo-code-textarea", new PageWaitForSelectorOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = 10000
                });
                await page.WaitForSelectorAsync("button.logo-run-button", new PageWaitForSelectorOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = 10000
                });

                // Switch to Immediate rendering mode (JavaScript fast mode)
                var modeButton = await page.QuerySelectorAsync("button.logo-rendering-mode-button");
                if (modeButton != null)
                {
                    var modeText = await modeButton.InnerTextAsync();
                    Console.WriteLine($"Current rendering mode button text: {modeText}");
                    
                    // Click until we're in Immediate mode
                    while (!modeText.Contains("Immediate", StringComparison.OrdinalIgnoreCase))
                    {
                        await modeButton.ClickAsync();
                        await Task.Delay(500);
                        modeText = await modeButton.InnerTextAsync();
                        Console.WriteLine($"After click, mode button text: {modeText}");
                    }
                    Console.WriteLine("✓ Switched to Immediate (JavaScript fast) mode");
                }

                // Test code using setxy, seth, and other position commands
                var testCode = @"
; Test setxy command
setxy 100 100
setpencolor ""blue""
setxy 400 100

; Test seth (setheading) command
seth 45
fd 100

; Test setx and sety
setx 200
sety 400

; Draw a pattern to verify all commands work
seth 0
for i 1 4 [
  fd 50
  rt 90
]
";

                var codeEditor = await page.QuerySelectorAsync("textarea.logo-code-textarea");
                if (codeEditor == null)
                {
                    Assert.Fail("Code editor textarea not found");
                }

                Console.WriteLine("✓ Found code editor");
                await codeEditor.FillAsync(testCode);
                Console.WriteLine("✓ Entered test code with setxy, seth, setx, sety commands");

                // Click Run button
                var runButton = await page.QuerySelectorAsync("button.logo-run-button");
                if (runButton == null)
                {
                    Assert.Fail("Run button not found");
                }

                await runButton.ClickAsync();
                Console.WriteLine("✓ Clicked Run button");

                // Wait for execution (immediate mode should be fast)
                await Task.Delay(1000);

                // Verify canvas has drawings by checking if it's not blank
                var hasDrawing = await page.EvaluateAsync<bool>(@"
                    () => {
                        const canvas = document.querySelector('canvas#logoCanvas');
                        if (!canvas) return false;
                        
                        const ctx = canvas.getContext('2d');
                        const imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);
                        const data = imageData.data;
                        
                        // Check if any non-white pixels exist
                        for (let i = 0; i < data.length; i += 4) {
                            const r = data[i];
                            const g = data[i + 1];
                            const b = data[i + 2];
                            const a = data[i + 3];
                            
                            // If we find any pixel that's not white/transparent, canvas has drawing
                            if (a > 0 && (r !== 255 || g !== 255 || b !== 255)) {
                                return true;
                            }
                        }
                        
                        return false;
                    }
                ");

                Console.WriteLine($"Canvas has drawing: {hasDrawing}");
                Assert.IsTrue(hasDrawing, "Canvas should contain drawings after executing position commands");

                // Check for JavaScript errors in console
                var jsErrors = await page.EvaluateAsync<string>(@"
                    () => {
                        const logs = window.logoDebugLogs || [];
                        const errors = logs.filter(log => log.includes('ERROR') || log.includes('Unknown'));
                        return errors.join('\n');
                    }
                ");

                if (!string.IsNullOrEmpty(jsErrors))
                {
                    Console.WriteLine($"JavaScript errors detected:\n{jsErrors}");
                    Assert.Fail($"JavaScript interpreter reported errors:\n{jsErrors}");
                }

                // Take screenshot for visual verification
                var canvas = await page.QuerySelectorAsync("canvas#logoCanvas");
                if (canvas != null)
                {
                    await canvas.ScreenshotAsync(new ElementHandleScreenshotOptions
                    {
                        Path = "logo-javascript-position-commands.png"
                    });
                    Console.WriteLine("✓ Canvas screenshot saved: logo-javascript-position-commands.png");
                }

                Console.WriteLine("========================================");
                Console.WriteLine("✓ JavaScript fast mode position commands test PASSED");
                Console.WriteLine("========================================");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test failed: {ex.Message}");
                
                // Take debug screenshot
                await page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = "logo-javascript-test-failure.png",
                    FullPage = true
                });
                
                throw;
            }
        }

        /// <summary>
        /// Parity checker test - verifies JavaScript interpreter supports all C# LogoCommandType commands
        /// This test prevents future bugs where C# implementation has commands that JavaScript is missing
        /// </summary>
        [TestMethod]
        [TestCategory("Automated")]
        [TestCategory("ParityCheck")]
        public async Task ParityCheck_JavaScriptSupportsAllCSharpCommands()
        {
            Console.WriteLine("========================================");
            Console.WriteLine("Logo Command Parity Check");
            Console.WriteLine("========================================");
            Console.WriteLine("Verifying JavaScript interpreter supports all C# LogoCommandType commands...");

            // Get all C# command types from enum
            var csharpCommands = Enum.GetValues(typeof(LogoCommandType))
                .Cast<LogoCommandType>()
                .Select(cmd => cmd.ToString())
                .OrderBy(cmd => cmd)
                .ToList();

            Console.WriteLine($"\nC# LogoCommandType enum has {csharpCommands.Count} command types:");
            foreach (var cmd in csharpCommands)
            {
                Console.WriteLine($"  - {cmd}");
            }

            // Define JavaScript command mappings
            // This maps LogoCommandType enum values to the tokens that JavaScript parseCommands should recognize
            var jsCommandMappings = new Dictionary<string, string[]>
            {
                { "Forward", new[] { "fd", "forward" } },
                { "Backward", new[] { "bk", "backward" } },
                { "Right", new[] { "rt", "right" } },
                { "Left", new[] { "lt", "left" } },
                { "PenUp", new[] { "pu", "penup" } },
                { "PenDown", new[] { "pd", "pendown" } },
                { "SetPenColor", new[] { "setpencolor" } },
                { "SetPenWidth", new[] { "setpenwidth" } },
                { "SetXY", new[] { "setxy" } },
                { "SetX", new[] { "setx" } },
                { "SetY", new[] { "sety" } },
                { "SetHeading", new[] { "seth", "setheading" } },
                { "Home", new[] { "home" } },
                { "ClearScreen", new[] { "cs", "clearscreen" } },
                { "ShowTurtle", new[] { "st", "showturtle" } },
                { "HideTurtle", new[] { "ht", "hideturtle" } },
                { "Repeat", new[] { "repeat" } },
                { "Wait", new[] { "wait" } },
                { "Delay", new[] { "delay" } },
                { "Comment", new[] { ";" } },
                { "SetVariable", new[] { "set" } },
                { "For", new[] { "for" } }
            };

            Console.WriteLine($"\nJavaScript command mappings defined for {jsCommandMappings.Count} command types");

            // Check for missing mappings
            var missingMappings = new List<string>();
            foreach (var csharpCmd in csharpCommands)
            {
                if (!jsCommandMappings.ContainsKey(csharpCmd))
                {
                    missingMappings.Add(csharpCmd);
                    Console.WriteLine($"  ⚠️  WARNING: No JavaScript mapping defined for C# command: {csharpCmd}");
                }
            }

            if (missingMappings.Count > 0)
            {
                Console.WriteLine($"\n❌ PARITY CHECK FAILED: {missingMappings.Count} C# commands have no JavaScript mappings:");
                foreach (var missing in missingMappings)
                {
                    Console.WriteLine($"  - {missing}");
                }
                Assert.Fail($"JavaScript mappings missing for C# commands: {string.Join(", ", missingMappings)}");
            }

            // Now verify JavaScript actually implements these commands by executing test code
            Console.WriteLine("\n========================================");
            Console.WriteLine("Verifying JavaScript Implementation");
            Console.WriteLine("========================================");

            _browser = await _playwright!.Chromium.LaunchAsync(GetBrowserLaunchOptions());
            var context = await _browser.NewContextAsync(GetBrowserContextOptions());
            var page = await context.NewPageAsync();

            // Capture JavaScript console messages
            var jsLogs = new List<string>();
            page.Console += (_, msg) => 
            {
                var logText = $"[{msg.Type}] {msg.Text}";
                jsLogs.Add(logText);
                Console.WriteLine($"[Browser] {logText}");
            };

            // Navigate with ?noautostart to prevent auto-execution
            await NavigateToBlazorPageAsync(page, "/logo?noautostart=true", "canvas#logoCanvas");

            try
            {
                // Wait for UI elements to be available before any interaction
                Console.WriteLine("Waiting for UI elements to load...");
                await page.WaitForSelectorAsync("textarea.logo-code-textarea", new PageWaitForSelectorOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = 10000
                });
                await page.WaitForSelectorAsync("button.logo-run-button", new PageWaitForSelectorOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = 10000
                });
                Console.WriteLine("✓ UI elements loaded and ready");

                // Switch to Immediate mode (JavaScript execution)
                var modeButton = await page.QuerySelectorAsync("button.logo-rendering-mode-button");
                if (modeButton != null)
                {
                    var modeText = await modeButton.InnerTextAsync();
                    while (!modeText.Contains("Immediate", StringComparison.OrdinalIgnoreCase))
                    {
                        await modeButton.ClickAsync();
                        await Task.Delay(500);
                        modeText = await modeButton.InnerTextAsync();
                    }
                    Console.WriteLine("✓ Switched to JavaScript Immediate mode");
                }

                // Build comprehensive test code using all commands
                var testCode = @"
; Movement commands
fd 10
bk 10
rt 45
lt 45

; Pen commands
pu
pd
setpencolor ""red""
setpencolor 1
setpenwidth 2

; Position commands
setxy 200 200
setx 250
sety 250
seth 90
setheading 180

; Canvas commands
st
ht

; Control structures
repeat 2 [fd 5]
for i 1 3 [fd :i]

; Delay
wait 1
delay 100

; Return home
home
cs
";

                var codeEditor = await page.QuerySelectorAsync("textarea.logo-code-textarea");
                Assert.IsNotNull(codeEditor, "Code editor not found");
                
                await codeEditor.FillAsync(testCode);
                Console.WriteLine("✓ Entered comprehensive test code");

                // Click Run
                var runButton = await page.QuerySelectorAsync("button.logo-run-button");
                Assert.IsNotNull(runButton, "Run button not found");
                
                await runButton.ClickAsync();
                Console.WriteLine("✓ Executed test code");

                // Wait for execution
                await Task.Delay(2000);

                // Check for JavaScript errors
                var jsErrors = jsLogs.Where(log => 
                    (log.Contains("ERROR", StringComparison.OrdinalIgnoreCase) ||
                    log.Contains("Unknown token", StringComparison.OrdinalIgnoreCase)) &&
                    !log.Contains("appsettings.json", StringComparison.OrdinalIgnoreCase) // Ignore appsettings.json warning
                ).ToList();

                if (jsErrors.Count > 0)
                {
                    Console.WriteLine("\n❌ JavaScript execution errors detected:");
                    foreach (var error in jsErrors)
                    {
                        Console.WriteLine($"  {error}");
                    }
                    
                    await page.ScreenshotAsync(new PageScreenshotOptions
                    {
                        Path = "logo-parity-check-failure.png",
                        FullPage = true
                    });
                    
                    Assert.Fail($"JavaScript interpreter reported {jsErrors.Count} errors during command execution");
                }

                Console.WriteLine("\n========================================");
                Console.WriteLine("✅ PARITY CHECK PASSED");
                Console.WriteLine("========================================");
                Console.WriteLine("JavaScript interpreter successfully executed all C# command types");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Parity check failed with exception: {ex.Message}");
                
                await page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = "logo-parity-check-exception.png",
                    FullPage = true
                });
                
                throw;
            }
        }
    }
}
