using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Azure.Functions.Worker.Configuration;

namespace ApiIsolated
{
    public class Program
    {
        public static void Main()
        {
            var builder = new HostBuilder();
            builder.ConfigureServices((context, services) =>
            {
            });
            var host = builder
                .ConfigureFunctionsWorkerDefaults()
                .Build();

            host.Run();
        }
    }
}