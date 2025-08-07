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
        static string dbPathLocal = $@"temp\{dbFileName}";  // Local temp folder
        static string dbPathAzure = $@"d:\home\{dbFileName}";

        public static async Task<(string pathDb, bool didDownload)> DownloadDbAsync()
        {
            bool didDownload = false;
            var envvar = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT");
            var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");

            // Determine local path based on environment
            string localDbPath = envvar == "Development" ? dbPathLocal : dbPathAzure;
            
            if (!string.IsNullOrEmpty(connectionString))
            {
                var containerName = "mypixnothumbs";
                try
                {
                    var blobClient = new BlobServiceClient(connectionString)
                        .GetBlobContainerClient(containerName)
                        .GetBlobClient(dbFileName);

                    // Check if we need to download
                    bool shouldDownload = false;

                    if (!File.Exists(localDbPath))
                    {
                        // Local file doesn't exist, download it
                        shouldDownload = true;
                    }
                    else if (await blobClient.ExistsAsync())
                    {
                        // Both files exist, compare last modified dates
                        var blobProperties = await blobClient.GetPropertiesAsync();
                        var localFileInfo = new FileInfo(localDbPath);
                        
                        // Download if blob is newer than local file
                        if (blobProperties.Value.LastModified > localFileInfo.LastWriteTimeUtc)
                        {
                            shouldDownload = true;
                        }
                    }

                    if (shouldDownload)
                    {
                        // Ensure directory exists for local development
                        if (envvar == "Development")
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(localDbPath)!);
                        }
                        
                        await blobClient.DownloadToAsync(localDbPath);
                        File.SetAttributes(localDbPath, FileAttributes.Normal);
                        didDownload = true;
                    }

                    return (localDbPath, didDownload);
                }
                catch (Exception)
                {
                    // Fallback to local file copy if blob storage fails
                    if (!File.Exists(localDbPath) && File.Exists(dbPathDefault))
                    {
                        if (envvar == "Development")
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(localDbPath)!);
                        }
                        File.Copy(dbPathDefault, localDbPath);
                        File.SetAttributes(localDbPath, FileAttributes.Normal);
                        didDownload = true;
                    }
                }
            }
            else
            {
                // No connection string - fallback to local file copy
                if (!File.Exists(localDbPath) && File.Exists(dbPathDefault))
                {
                    if (envvar == "Development")
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(localDbPath)!);
                    }
                    File.Copy(dbPathDefault, localDbPath);
                    File.SetAttributes(localDbPath, FileAttributes.Normal);
                    didDownload = true;
                }
            }

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