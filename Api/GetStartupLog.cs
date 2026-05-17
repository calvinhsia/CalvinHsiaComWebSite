using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Api
{
    public class GetStartupLogClass
    {
        private readonly ILogger _logger;

        public GetStartupLogClass(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<GetStartupLogClass>();
        }

        [Function(nameof(GetStartupLog))]
        public async Task<HttpResponseData> GetStartupLog(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            // In-memory buffer is always available (no file I/O needed)
            var bufferText = ApiIsolated.Program.StartupLogText;

            // Also try to read the file for any messages written before the buffer existed
            var fileText = "";
            var logPath = ApiIsolated.Program.StartupLogPath;
            if (!string.IsNullOrEmpty(logPath) && File.Exists(logPath))
            {
                try { fileText = await File.ReadAllTextAsync(logPath); }
                catch { fileText = $"(error reading {logPath})"; }
            }

            var combined = string.IsNullOrEmpty(fileText)
                ? bufferText
                : fileText; // file is the superset — it was written first

            _logger.LogInformation("[GetStartupLog] Returning {bytes} bytes of startup log", combined.Length);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
            await response.WriteStringAsync(combined);
            return response;
        }
    }
}
