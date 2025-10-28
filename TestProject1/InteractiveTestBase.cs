using Microsoft.Playwright;
using System.Diagnostics;

namespace TestProject1
{
    /// <summary>
    /// Base class for interactive Blazor WebAssembly tests using Playwright
    /// Handles server startup, port cleanup, and Playwright initialization
    /// </summary>
    [TestClass]
    public abstract class InteractiveTestBase
    {
        protected static IPlaywright? _playwright;
        protected static IBrowser? _browser;
        protected const string BASE_URL = "https://localhost:7193";
        protected const int SERVER_PORT = 7193;

        // Set this to true if you want the test to auto-start the server
        // Set to false if you prefer to start the server manually (recommended)
        protected const bool AUTO_START_SERVER = true;
        protected static Process? _dotnetProcess;

        // Track if server was started by this test class
        protected static bool _serverStartedByUs = false;

        [ClassInitialize]
        public static async Task BaseClassInitialize(TestContext context)
        {
            // Always check if server is already running first, regardless of AUTO_START_SERVER
            if (await IsServerRunning(BASE_URL))
            {
                Console.WriteLine("? Server is already running at " + BASE_URL);
                Console.WriteLine("Reusing existing server instance.");
                _serverStartedByUs = false; // We didn't start it
            }
            else if (AUTO_START_SERVER)
            {
                // Start the Blazor WASM development server
                Console.WriteLine("Starting Blazor WASM development server...");
                _dotnetProcess = StartBlazorServer();
                _serverStartedByUs = true;

                // Wait for server to be ready
                await WaitForServer(BASE_URL);
            }
            else
            {
                Console.WriteLine("??  AUTO_START_SERVER is disabled.");
                Console.WriteLine("Please make sure your Blazor app is running:");
                Console.WriteLine("  cd Client");
                Console.WriteLine("  dotnet run");
                Console.WriteLine();
                Console.WriteLine("? Server is not running at " + BASE_URL);
                Console.WriteLine("Please start the server before running this test.");
                throw new InvalidOperationException("Blazor server is not running. Start it with: dotnet run --project Client/Client.csproj");
            }

            // Initialize Playwright
            Console.WriteLine("Initializing Playwright...");
            _playwright = await Playwright.CreateAsync();
        }

        [ClassCleanup]
        public static async Task BaseClassCleanup()
        {
            if (_browser != null)
            {
                try
                {
                    await _browser.CloseAsync();
                    await _browser.DisposeAsync();
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }

            if (_playwright != null)
            {
                _playwright.Dispose();
            }

            // Only kill the server if we started it AND it's still running
            if (_dotnetProcess != null && !_dotnetProcess.HasExited && _serverStartedByUs)
            {
                Console.WriteLine("Stopping Blazor server that we started...");
                try
                {
                    _dotnetProcess.Kill(entireProcessTree: true); // Kill entire process tree
                    _dotnetProcess.WaitForExit(5000); // Wait up to 5 seconds for graceful exit

                    // Give the OS time to release the port
                    await Task.Delay(500);

                    // Verify port is released
                    if (IsPortInUse(SERVER_PORT))
                    {
                        Console.WriteLine($"Warning: Port {SERVER_PORT} is still in use after stopping server");
                        Console.WriteLine("Attempting to kill process using the port...");
                        KillProcessUsingPort(SERVER_PORT);
                        await Task.Delay(1000); // Wait for port to be fully released
                    }
                    else
                    {
                        Console.WriteLine($"Port {SERVER_PORT} successfully released");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Error stopping server process: {ex.Message}");
                }
                finally
                {
                    _dotnetProcess.Dispose();
                    _dotnetProcess = null;
                }
            }
        }

        [TestInitialize]
        public void BaseTestInitialize()
        {
            // Reset browser for each test to ensure clean state
            _browser = null;
        }

        [TestCleanup]
        public async Task BaseTestCleanup()
        {
            // Close browser after each test
            if (_browser != null && _browser.IsConnected)
            {
                try
                {
                    await _browser.CloseAsync();
                    await _browser.DisposeAsync();
                }
                catch
                {
                    // Ignore cleanup errors
                }
                _browser = null;
            }
        }

        #region Helper Methods

        protected static async Task<bool> IsServerRunning(string url, int timeoutSeconds = 5)
        {
            try
            {
                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                };
                using var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
                var response = await httpClient.GetAsync(url);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        protected static bool IsPortInUse(int port)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "netstat",
                    Arguments = "-ano",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process != null)
                {
                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    return output.Contains($":{port}");
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        protected static void KillProcessUsingPort(int port)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "netstat",
                    Arguments = "-ano",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var netstatProcess = Process.Start(processInfo);
                if (netstatProcess != null)
                {
                    var output = netstatProcess.StandardOutput.ReadToEnd();
                    netstatProcess.WaitForExit();

                    // Parse netstat output to find PID
                    var lines = output.Split('\n');
                    foreach (var line in lines)
                    {
                        if (line.Contains($":{port}") && line.Contains("LISTENING"))
                        {
                            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length > 0 && int.TryParse(parts[^1], out int pid))
                            {
                                Console.WriteLine($"Found process {pid} using port {port}, terminating...");
                                try
                                {
                                    var process = Process.GetProcessById(pid);
                                    process.Kill(entireProcessTree: true);
                                    process.WaitForExit(3000);
                                    Console.WriteLine($"Process {pid} terminated successfully");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Failed to kill process {pid}: {ex.Message}");
                                }
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error killing process using port {port}: {ex.Message}");
            }
        }

        protected static Process StartBlazorServer()
        {
            // Get the solution directory (parent of TestProject1)
            var testProjectDir = Path.GetDirectoryName(typeof(InteractiveTestBase).Assembly.Location)!;
            var solutionDir = Path.GetFullPath(Path.Combine(testProjectDir, "..", "..", "..", ".."));
            var clientProjectPath = Path.Combine(solutionDir, "Client", "Client.csproj");

            Console.WriteLine($"Test project directory: {testProjectDir}");
            Console.WriteLine($"Solution directory: {solutionDir}");
            Console.WriteLine($"Client project path: {clientProjectPath}");

            if (!File.Exists(clientProjectPath))
            {
                throw new FileNotFoundException($"Client project not found at: {clientProjectPath}");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{clientProjectPath}\"",
                WorkingDirectory = solutionDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var process = new Process { StartInfo = startInfo };

            process.OutputDataReceived += (sender, e) =>
       {
           if (!string.IsNullOrEmpty(e.Data))
           {
               Console.WriteLine($"[Blazor Server] {e.Data}");
           }
       };

            process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Console.WriteLine($"[Blazor Server Error] {e.Data}");
            }
        };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return process;
        }

        protected static async Task WaitForServer(string url, int timeoutSeconds = 60)
        {
            var stopwatch = Stopwatch.StartNew();

            Console.WriteLine($"Waiting for server at {url}...");

            while (stopwatch.Elapsed.TotalSeconds < timeoutSeconds)
            {
                try
                {
                    // Skip SSL validation for local development
                    var handler = new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                    };
                    using var httpClient = new HttpClient(handler);
                    var response = await httpClient.GetAsync(url);

                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine("Server is ready!");
                        return;
                    }
                }
                catch
                {
                    // Server not ready yet
                }

                await Task.Delay(1000);
            }

            throw new TimeoutException($"Server at {url} did not become ready within {timeoutSeconds} seconds");
        }

        #endregion
    }
}
