using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace Api
{
    /// <summary>
    /// Hosted service that replays the pre-host startup log buffer through ILogger
    /// (and therefore Application Insights) once the full telemetry pipeline is active.
    /// StartAsync is called by the runtime inside host.Run(), after all sinks — including
    /// the Application Insights exporter — have been registered and are ready to receive events.
    /// </summary>
    internal class StartupLogReplayService : IHostedService
    {
        private readonly ILogger<StartupLogReplayService> _logger;

        public StartupLogReplayService(ILogger<StartupLogReplayService> logger)
        {
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var buffer = ApiIsolated.Program.StartupLogBuffer;
            _logger.LogInformation("[StartupReplay] Replaying {count} pre-host startup messages to telemetry", buffer.Count);
            foreach (var msg in buffer)
                _logger.LogInformation("{msg}", msg);
            buffer.Clear();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
