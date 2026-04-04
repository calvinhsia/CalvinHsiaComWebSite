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
            var (pathdb, didDownload) = await DownloadDbAsync();

            var builder = new HostBuilder();
            builder.ConfigureServices((context, services) =>
            {
                services.AddPooledDbContextFactory<MyPixWebDBContext>(
                    (serviceProvider, optionsBuilder) =>
                    {
                        var connstr = $"Filename={pathdb}";
                        optionsBuilder.UseSqlite(connstr);
                    });

                // Add HttpClientFactory for Graph API calls
                services.AddHttpClient();
            });
            var host = builder
                .ConfigureFunctionsWorkerDefaults()
                .Build();

            host.Run();
        }
    }
}