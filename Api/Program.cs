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

        public static void Main()
        {
            var envvar = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT");
            var pathdb = envvar != "Development" ? dbPathAzure : dbPathDefault;

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