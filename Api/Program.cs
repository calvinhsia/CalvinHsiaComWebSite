using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Azure.Functions.Worker.Configuration;
using Api;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;

namespace ApiIsolated
{
    public class Program
    {
        // Use the OneDrive database as the primary database
        const string dbFileName = "MyPixNoThumbs.db";
        static string dbPathDefault = $@"data\{dbFileName}";
        static string? dbPathLocal = Environment.GetEnvironmentVariable("MYPIXNOTHUMBSPATH");
        static string dbPathAzure = $@"d:\home\{dbFileName}";

        /// <summary>
        /// Buffer of messages logged before the host (and Application Insights) is available.
        /// Replayed through ILogger after the host is built so they appear in AI telemetry.
        /// </summary>
        private static readonly List<string> _startupLogBuffer = new();

        /// <summary>Writes to Console, the startup log file (if provided), and the AI replay buffer.</summary>
        internal static void StartupLog(string msg, string? logPath = null)
        {
            StartupLog(msg);
            _startupLogBuffer.Add(msg);
            if (logPath != null)
                try { File.AppendAllText(logPath, msg + Environment.NewLine); } catch { }
        }

        public static async Task<(string pathDb, bool didDownload)> DownloadDbAsync()
        {
            bool didDownload = false;
            var envvar = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT");
            var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");

            StartupLog($"[DownloadDbAsync] Environment: {envvar ?? "null"}");
            StartupLog($"[DownloadDbAsync] Connection string configured: {!string.IsNullOrEmpty(connectionString)}");

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
            StartupLog($"[DownloadDbAsync] Local DB path: {localDbPath}");
            
            if (!string.IsNullOrEmpty(connectionString))
            {
                var containerName = "mypixnothumbs";
                StartupLog($"[DownloadDbAsync] Using Azure Blob Storage container: {containerName}");
                
                try
                {
                    var blobClient = new BlobServiceClient(connectionString)
                        .GetBlobContainerClient(containerName)
                        .GetBlobClient(dbFileName);

                    StartupLog($"[DownloadDbAsync] Blob client created for: {dbFileName}");

                    // Check if we need to download
                    bool shouldDownload = false;

                    if (!File.Exists(localDbPath))
                    {
                        // Local file doesn't exist, download it
                        StartupLog($"[DownloadDbAsync] Local file does not exist, will download");
                        shouldDownload = true;
                    }
                    else if (await blobClient.ExistsAsync())
                    {
                        // Both files exist, compare last modified dates
                        var blobProperties = await blobClient.GetPropertiesAsync();
                        var localFileInfo = new FileInfo(localDbPath);
                        
                        StartupLog($"[DownloadDbAsync] Blob last modified: {blobProperties.Value.LastModified:yyyy-MM-dd HH:mm:ss} UTC");
                        StartupLog($"[DownloadDbAsync] Local file last modified: {localFileInfo.LastWriteTimeUtc:yyyy-MM-dd HH:mm:ss} UTC");
                        
                        // Download if blob is newer than local file
                        if (blobProperties.Value.LastModified > localFileInfo.LastWriteTimeUtc)
                        {
                            StartupLog($"[DownloadDbAsync] Blob is newer, will download");
                            shouldDownload = true;
                        }
                        else
                        {
                            StartupLog($"[DownloadDbAsync] Local file is up to date");
                        }
                    }
                    else
                    {
                        StartupLog($"[DownloadDbAsync] Blob does not exist in storage");
                    }

                    if (shouldDownload)
                    {
                        // Ensure directory exists for local development
                        if (envvar == "Development")
                        {
                            var dir = Path.GetDirectoryName(localDbPath)!;
                            StartupLog($"[DownloadDbAsync] Creating directory: {dir}");
                            Directory.CreateDirectory(dir);
                        }
                        
                        StartupLog($"[DownloadDbAsync] Downloading blob to: {localDbPath}");
                            await blobClient.DownloadToAsync(localDbPath);
                            File.SetAttributes(localDbPath, FileAttributes.Normal);
                            didDownload = true;
                            StartupLog($"[DownloadDbAsync] Download complete");
                        }

                        // Download PictureSettings.json from the same container (always, to pick up changes)
                        const string settingsFileName = "PictureSettings.json";
                        var settingsLocalPath = Path.Combine(Path.GetDirectoryName(localDbPath)!, settingsFileName);
                        try
                        {
                            var settingsBlobClient = new BlobServiceClient(connectionString)
                                .GetBlobContainerClient(containerName)
                                .GetBlobClient(settingsFileName);
                            if (await settingsBlobClient.ExistsAsync())
                            {
                                StartupLog($"[DownloadDbAsync] Downloading {settingsFileName} to: {settingsLocalPath}");
                                await settingsBlobClient.DownloadToAsync(settingsLocalPath);
                                StartupLog($"[DownloadDbAsync] {settingsFileName} download complete");
                            }
                            else
                            {
                                StartupLog($"[DownloadDbAsync] {settingsFileName} not found in blob storage");
                            }
                        }
                        catch (Exception ex)
                        {
                            StartupLog($"[DownloadDbAsync] Error downloading {settingsFileName}: {ex.Message}");
                        }

                        // Load picture settings (from blob download or local file alongside the DB)
                        Api.SwaAuthHelper.LoadPictureSettings(
                            settingsLocalPath,
                            Path.Combine(AppContext.BaseDirectory, settingsFileName));

                        StartupLog($"[DownloadDbAsync] Returning path: {localDbPath}, Downloaded: {didDownload}");
                    return (localDbPath, didDownload);
                }
                catch (Exception ex)
                {
                    StartupLog($"[DownloadDbAsync] Blob storage error: {ex.GetType().Name} - {ex.Message}");
                    
                    // Fallback to local file copy if blob storage fails
                    if (!File.Exists(localDbPath) && File.Exists(dbPathDefault))
                    {
                        StartupLog($"[DownloadDbAsync] Falling back to local copy from: {dbPathDefault}");
                        
                        if (envvar == "Development")
                        {
                            var dir = Path.GetDirectoryName(localDbPath)!;
                            StartupLog($"[DownloadDbAsync] Creating directory: {dir}");
                            Directory.CreateDirectory(dir);
                        }
                        File.Copy(dbPathDefault, localDbPath);
                        File.SetAttributes(localDbPath, FileAttributes.Normal);
                        didDownload = true;
                        StartupLog($"[DownloadDbAsync] Local copy complete");
                    }
                    else
                    {
                        StartupLog($"[DownloadDbAsync] Fallback not possible - local file already exists or default doesn't exist");
                    }
                }
            }
            else
            {
                StartupLog($"[DownloadDbAsync] No connection string - using local file copy");
                
                // No connection string - fallback to local file copy
                if (!File.Exists(localDbPath) && File.Exists(dbPathDefault))
                {
                    StartupLog($"[DownloadDbAsync] Copying from {dbPathDefault} to {localDbPath}");
                    
                    if (envvar == "Development")
                    {
                        var dir = Path.GetDirectoryName(localDbPath)!;
                        StartupLog($"[DownloadDbAsync] Creating directory: {dir}");
                        Directory.CreateDirectory(dir);
                    }
                    File.Copy(dbPathDefault, localDbPath);
                    File.SetAttributes(localDbPath, FileAttributes.Normal);
                    didDownload = true;
                    StartupLog($"[DownloadDbAsync] Local copy complete");
                }
                else
                {
                    StartupLog($"[DownloadDbAsync] Local file already exists or default doesn't exist");
                    StartupLog($"[DownloadDbAsync]   LocalDbPath exists: {File.Exists(localDbPath)}");
                    StartupLog($"[DownloadDbAsync]   DbPathDefault exists: {File.Exists(dbPathDefault)}");
                }
            }

            StartupLog($"[DownloadDbAsync] Final result - Path: {localDbPath}, Downloaded: {didDownload}");

            // Load picture settings from well-known local locations (fallback when no blob storage)
            Api.SwaAuthHelper.LoadPictureSettings(
                Path.Combine(AppContext.BaseDirectory, "PictureSettings.json"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PictureSettings.json"));

            return (localDbPath, didDownload);
        }

        public static async Task Main()
        {
            // Write to both Console and a file so startup crashes are always captured.
            // On Azure the log file lands at d:\home\LogFiles\startup-YYYYMMDD-HHMMSS.log
            // and can be read via Kudu: https://<app>.scm.azurewebsites.net/api/vfs/LogFiles/
            var logPath = InitStartupLog();

            void Log(string msg) => StartupLog(msg, logPath);

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

                // Replay all pre-host startup messages through ILogger so they appear in Application Insights
                var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger<Program>();
                foreach (var msg in _startupLogBuffer)
                    logger.LogInformation("{msg}", msg);
                _startupLogBuffer.Clear();

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
                StartupLog($"[Startup] ENV {name} = <not set>");
            else if (redact)
                StartupLog($"[Startup] ENV {name} = <set, {val.Length} chars>");
            else
                StartupLog($"[Startup] ENV {name} = {val}");
        }
    }
}