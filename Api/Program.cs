using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Azure.Functions.Worker.Configuration;
using Api;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using Azure.Storage.Blobs;

namespace ApiIsolated
{
    public class Program
    {
        // Use the OneDrive database as the primary database
        const string dbFileName = "MyPixNoThumbs.db";
        static string dbPathDefault = $@"data\{dbFileName}";
        static string? dbPathLocal = Environment.GetEnvironmentVariable("MYPIXNOTHUMBSPATH");
        static string dbPathAzure = $@"d:\home\{dbFileName}";

        public static async Task<(string pathDb, bool didDownload)> DownloadDbAsync()
        {
            bool didDownload = false;
            var envvar = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT");
            var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");

            Console.WriteLine($"[DownloadDbAsync] Environment: {envvar ?? "null"}");
            Console.WriteLine($"[DownloadDbAsync] Connection string configured: {!string.IsNullOrEmpty(connectionString)}");

            // Determine local path based on environment
            if (envvar == "Development" && string.IsNullOrEmpty(dbPathLocal))
            {
                /* ensure Local.settings.json: (not launchsettings.json)
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "MYPIXNOTHUMBSPATH": "C:\\Users\\calvi\\OneDrive\\Documents\\MyPixNoThumbs.db"
  },
  "Host": {
    "CORS": "https://localhost:7193",
    "CORSCredentials": true
  }
}
                 
                 
                 */
                throw new Exception("Environment variable MYPIXNOTHUMBSPATH must be set in Development environment");
            }
            string localDbPath = envvar == "Development" ? dbPathLocal! : dbPathAzure;
            Console.WriteLine($"[DownloadDbAsync] Local DB path: {localDbPath}");
            
            if (!string.IsNullOrEmpty(connectionString))
            {
                var containerName = "mypixnothumbs";
                Console.WriteLine($"[DownloadDbAsync] Using Azure Blob Storage container: {containerName}");
                
                try
                {
                    var blobClient = new BlobServiceClient(connectionString)
                        .GetBlobContainerClient(containerName)
                        .GetBlobClient(dbFileName);

                    Console.WriteLine($"[DownloadDbAsync] Blob client created for: {dbFileName}");

                    // Check if we need to download
                    bool shouldDownload = false;

                    if (!File.Exists(localDbPath))
                    {
                        // Local file doesn't exist, download it
                        Console.WriteLine($"[DownloadDbAsync] Local file does not exist, will download");
                        shouldDownload = true;
                    }
                    else if (await blobClient.ExistsAsync())
                    {
                        // Both files exist, compare last modified dates
                        var blobProperties = await blobClient.GetPropertiesAsync();
                        var localFileInfo = new FileInfo(localDbPath);
                        
                        Console.WriteLine($"[DownloadDbAsync] Blob last modified: {blobProperties.Value.LastModified:yyyy-MM-dd HH:mm:ss} UTC");
                        Console.WriteLine($"[DownloadDbAsync] Local file last modified: {localFileInfo.LastWriteTimeUtc:yyyy-MM-dd HH:mm:ss} UTC");
                        
                        // Download if blob is newer than local file
                        if (blobProperties.Value.LastModified > localFileInfo.LastWriteTimeUtc)
                        {
                            Console.WriteLine($"[DownloadDbAsync] Blob is newer, will download");
                            shouldDownload = true;
                        }
                        else
                        {
                            Console.WriteLine($"[DownloadDbAsync] Local file is up to date");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[DownloadDbAsync] Blob does not exist in storage");
                    }

                    if (shouldDownload)
                    {
                        // Ensure directory exists for local development
                        if (envvar == "Development")
                        {
                            var dir = Path.GetDirectoryName(localDbPath)!;
                            Console.WriteLine($"[DownloadDbAsync] Creating directory: {dir}");
                            Directory.CreateDirectory(dir);
                        }
                        
                        Console.WriteLine($"[DownloadDbAsync] Downloading blob to: {localDbPath}");
                        await blobClient.DownloadToAsync(localDbPath);
                        File.SetAttributes(localDbPath, FileAttributes.Normal);
                        didDownload = true;
                        Console.WriteLine($"[DownloadDbAsync] Download complete");
                    }

                    Console.WriteLine($"[DownloadDbAsync] Returning path: {localDbPath}, Downloaded: {didDownload}");
                    return (localDbPath, didDownload);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DownloadDbAsync] Blob storage error: {ex.GetType().Name} - {ex.Message}");
                    
                    // Fallback to local file copy if blob storage fails
                    if (!File.Exists(localDbPath) && File.Exists(dbPathDefault))
                    {
                        Console.WriteLine($"[DownloadDbAsync] Falling back to local copy from: {dbPathDefault}");
                        
                        if (envvar == "Development")
                        {
                            var dir = Path.GetDirectoryName(localDbPath)!;
                            Console.WriteLine($"[DownloadDbAsync] Creating directory: {dir}");
                            Directory.CreateDirectory(dir);
                        }
                        File.Copy(dbPathDefault, localDbPath);
                        File.SetAttributes(localDbPath, FileAttributes.Normal);
                        didDownload = true;
                        Console.WriteLine($"[DownloadDbAsync] Local copy complete");
                    }
                    else
                    {
                        Console.WriteLine($"[DownloadDbAsync] Fallback not possible - local file already exists or default doesn't exist");
                    }
                }
            }
            else
            {
                Console.WriteLine($"[DownloadDbAsync] No connection string - using local file copy");
                
                // No connection string - fallback to local file copy
                if (!File.Exists(localDbPath) && File.Exists(dbPathDefault))
                {
                    Console.WriteLine($"[DownloadDbAsync] Copying from {dbPathDefault} to {localDbPath}");
                    
                    if (envvar == "Development")
                    {
                        var dir = Path.GetDirectoryName(localDbPath)!;
                        Console.WriteLine($"[DownloadDbAsync] Creating directory: {dir}");
                        Directory.CreateDirectory(dir);
                    }
                    File.Copy(dbPathDefault, localDbPath);
                    File.SetAttributes(localDbPath, FileAttributes.Normal);
                    didDownload = true;
                    Console.WriteLine($"[DownloadDbAsync] Local copy complete");
                }
                else
                {
                    Console.WriteLine($"[DownloadDbAsync] Local file already exists or default doesn't exist");
                    Console.WriteLine($"[DownloadDbAsync]   LocalDbPath exists: {File.Exists(localDbPath)}");
                    Console.WriteLine($"[DownloadDbAsync]   DbPathDefault exists: {File.Exists(dbPathDefault)}");
                }
            }

            Console.WriteLine($"[DownloadDbAsync] Final result - Path: {localDbPath}, Downloaded: {didDownload}");
            return (localDbPath, didDownload);
        }

        public static async Task Main()
        {
            // Write to both Console and a file so startup crashes are always captured.
            // On Azure the log file lands at d:\home\LogFiles\startup-YYYYMMDD-HHMMSS.log
            // and can be read via Kudu: https://<app>.scm.azurewebsites.net/api/vfs/LogFiles/
            var logPath = InitStartupLog();

            void Log(string msg)
            {
                Console.WriteLine(msg);
                try { File.AppendAllText(logPath, msg + Environment.NewLine); } catch { }
            }

            Log($"[Startup] ========== Azure Functions Host Starting ==========");
            Log($"[Startup] UTC time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
            Log($"[Startup] Log file: {logPath}");
            Log($"[Startup] .NET version: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
            Log($"[Startup] OS: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
            Log($"[Startup] Process ID: {System.Diagnostics.Process.GetCurrentProcess().Id}");

            // Log key environment variables (values redacted for secrets)
            LogEnvVar("AZURE_FUNCTIONS_ENVIRONMENT");
            LogEnvVar("FUNCTIONS_WORKER_RUNTIME");
            LogEnvVar("WEBSITE_SITE_NAME");
            LogEnvVar("WEBSITE_SLOT_NAME");
            LogEnvVar("FUNCTIONS_EXTENSION_VERSION");
            LogEnvVar("MYPIXNOTHUMBSPATH");
            LogEnvVar("AZURE_STORAGE_CONNECTION_STRING", redact: true);
            LogEnvVar("ALLOWED_EMAILS");
            LogEnvVar("APPLICATIONINSIGHTS_CONNECTION_STRING", redact: true);

            string pathdb;
            try
            {
                Log($"[Startup] Starting DB initialization...");
                var (resolvedPath, didDownload) = await DownloadDbAsync();
                pathdb = resolvedPath;
                Log($"[Startup] DB initialization complete. Path='{pathdb}' Downloaded={didDownload}");

                // Validate the DB file is accessible and non-empty
                if (!File.Exists(pathdb))
                {
                    Log($"[Startup] WARNING: DB file does not exist at '{pathdb}' — QueryPix will fail at runtime");
                }
                else
                {
                    var fi = new FileInfo(pathdb);
                    Log($"[Startup] DB file OK: size={fi.Length:N0} bytes, lastWrite={fi.LastWriteTimeUtc:yyyy-MM-dd HH:mm:ss} UTC");
                    if (fi.Length == 0)
                        Log($"[Startup] WARNING: DB file is 0 bytes!");
                }
            }
            catch (Exception ex)
            {
                Log($"[Startup] FATAL: DB initialization threw {ex.GetType().Name}: {ex.Message}");
                Log($"[Startup] {ex}");
                pathdb = dbPathAzure;
                Log($"[Startup] Continuing with fallback path '{pathdb}' — individual function calls will fail gracefully");
            }

            Log($"[Startup] Configuring host...");
            try
            {
                var builder = new HostBuilder();
                builder.ConfigureServices((context, services) =>
                {
                    Log($"[Startup] Registering DbContextFactory with path='{pathdb}'");
                    services.AddPooledDbContextFactory<MyPixWebDBContext>(
                        (serviceProvider, optionsBuilder) =>
                        {
                            var connstr = $"Filename={pathdb}";
                            optionsBuilder.UseSqlite(connstr);
                        });

                    // Add HttpClientFactory for Graph API calls
                    services.AddHttpClient();
                    Log($"[Startup] Services registered OK");
                });

                var host = builder
                    .ConfigureFunctionsWorkerDefaults()
                    .Build();

                Log($"[Startup] Host built successfully. Starting host.Run()...");
                host.Run();
            }
            catch (Exception ex)
            {
                Log($"[Startup] FATAL: Host startup threw {ex.GetType().Name}: {ex.Message}");
                Log($"[Startup] {ex}");
                throw;
            }
        }

        /// <summary>
        /// Creates a timestamped startup log file in the Azure LogFiles folder (or temp on local).
        /// Returns the full path so callers can append to it.
        /// </summary>
        private static string InitStartupLog()
        {
            try
            {
                // d:\home\LogFiles is always writable on Azure App Service / SWA Functions
                var logDir = Directory.Exists(@"d:\home\LogFiles")
                    ? @"d:\home\LogFiles"
                    : Path.GetTempPath();
                var fileName = $"startup-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log";
                var path = Path.Combine(logDir, fileName);
                File.WriteAllText(path, $"Log started {DateTime.UtcNow:O}{Environment.NewLine}");
                return path;
            }
            catch
            {
                return Path.Combine(Path.GetTempPath(), "startup-fallback.log");
            }
        }

        private static void LogEnvVar(string name, bool redact = false)
        {
            var val = Environment.GetEnvironmentVariable(name);
            if (val == null)
                Console.WriteLine($"[Startup] ENV {name} = <not set>");
            else if (redact)
                Console.WriteLine($"[Startup] ENV {name} = <set, {val.Length} chars>");
            else
                Console.WriteLine($"[Startup] ENV {name} = {val}");
        }
    }
}