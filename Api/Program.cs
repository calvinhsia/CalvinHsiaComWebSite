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
        static string dbPathDefault = @"data\MyPix.db"; //https://www.youtube.com/watch?v=xSAyEDFLFTw
        static string dbPathAzure = @"d:\home\MyPix.db";
        public static async Task<(string pathDb, bool didCopy)> CopyDbAsync()
        {
            var pathDBFile = dbPathDefault;
            bool DidCopy = false;
            var envvar = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT");
            if (envvar != "Development")
            {
                if (!File.Exists(dbPathAzure))
                {
                    await Task.Run(() =>
                    {
                        File.Copy(dbPathDefault, dbPathAzure);
                        File.SetAttributes(dbPathAzure, FileAttributes.Normal);
                        DidCopy = true;
                    });
                }
                pathDBFile = dbPathAzure;
            }
            return (pathDBFile, DidCopy);
        }
        public static async Task Main()
        {
            var (pathdb, didCopy) = await CopyDbAsync();

            var builder = new HostBuilder();
            builder.ConfigureServices((context, services) =>
            {
                services.AddPooledDbContextFactory<MyPixWebDBContext>(
                    (serviceProvider, optionsBuilder) =>
                    {
                        var connstr = $"Filename={pathdb}";
                        optionsBuilder.UseSqlite(connstr);
                    });
            });
            var host = builder
                .ConfigureFunctionsWorkerDefaults()
                .Build();

            host.Run();
        }
    }
}