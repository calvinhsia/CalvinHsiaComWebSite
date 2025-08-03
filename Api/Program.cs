using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Azure.Functions.Worker.Configuration;
using Api;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;

namespace ApiIsolated
{
    public class Program
    {
        // Use the OneDrive database as the primary database
        const string dbFileName = "MyPixNoThumbs.db";
        static string dbPathDefault = $@"data\{dbFileName}";
        static string dbPathAzure = $@"d:\home\{dbFileName}";

        // Fallback to original database if OneDrive version doesn't exist
        const string fallbackDbFileName = "MyPix.db";
        static string fallbackDbPathDefault = $@"data\{fallbackDbFileName}";
        static string fallbackDbPathAzure = $@"d:\home\{fallbackDbFileName}";

        public static string GetDataFilePath()
        {
            var envvar = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT");
            var primaryPath = envvar != "Development" ? dbPathAzure : dbPathDefault;
            
            // If primary database exists, use it
            if (File.Exists(primaryPath))
            {
                return primaryPath;
            }
            
            // Otherwise, use fallback database
            var fallbackPath = envvar != "Development" ? fallbackDbPathAzure : fallbackDbPathDefault;
            return fallbackPath;
        }

        public static void Main()
        {
            var pathdb = GetDataFilePath();

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