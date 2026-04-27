using System.Net;
using System.Threading.Tasks;
using Client.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api
{
    public class EnvInfoClass
    {
        private readonly ILogger _logger;

        public EnvInfoClass(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<EnvInfoClass>();
        }

        [Function(nameof(EnvInfo))]
        public async Task<HttpResponseData> EnvInfo([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
        {
            if (!SwaAuthHelper.IsAuthorized(req, _logger))
            {
                var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
                return unauthorized;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            try
            {
                _logger.LogInformation("Function called: {function}", nameof(EnvInfo));

                response.Headers.Add("Content-Type", "application/json");
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                var einfo = new EnvInfo();
                var data = await einfo.GetDataAsync();

                await response.WriteStringAsync(data);
            }
            catch (System.Exception ex)
            {
                await response.WriteStringAsync($"Error: {ex}");
            }
            return response;
        }
    }
}
