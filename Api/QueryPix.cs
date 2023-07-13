using System.Net;
using System.Threading.Tasks;
using System.Web;
using Client.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Api
{
    public class QueryPixClass
    {
        private readonly ILogger _logger;

        public QueryPixClass(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<EnvInfoClass>();
        }

        [Function(nameof(QueryPix))]
        public async Task<HttpResponseData> QueryPix([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            try
            {
                _logger.LogInformation("Function called: {function}", nameof(QueryPix));

                response.Headers.Add("Content-Type", "application/json");
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                var httpQuery = HttpUtility.ParseQueryString(req.Url.Query);
                var QueryString = (httpQuery["QueryString"]);
                var dbc = new MyPixWebDBContext();
                var res = await dbc.MyPixes.FromSqlInterpolated($"select * from MyPix where Notes like {("%" + QueryString + "%")}").ToListAsync();
                var json = JsonConvert.SerializeObject(res);

                await response.WriteStringAsync(json);
            }
            catch (System.Exception ex)
            {
                await response.WriteStringAsync($"Error: {ex}");
            }
            return response;
        }
    }
}
