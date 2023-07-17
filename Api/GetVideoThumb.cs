using System.Collections.Generic;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using Client.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.IO;

namespace Api
{
    public class GetVideoThumbClass
    {
        private readonly ILogger _logger;
        private readonly IDbContextFactory<MyPixWebDBContext> dbContextFactory;

        public GetVideoThumbClass(
            IDbContextFactory<MyPixWebDBContext> dbContextFactory,
            ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<EnvInfoClass>();
            this.dbContextFactory = dbContextFactory;
        }

        [Function(nameof(GetVideoThumb))]
        public async Task<HttpResponseData> GetVideoThumb(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "GetVideoThumb/{MyPixId:int}")] HttpRequestData req, int myPixId
            )
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            try
            {
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                using var dbc = dbContextFactory.CreateDbContext();
                var lstThumbs = await dbc.Thumbs.Where(x => x.MyPixId == myPixId).ToListAsync();

                _logger.LogInformation("Function called: {function} {myPixId}  {numresults}", nameof(GetVideoThumb), myPixId, lstThumbs.Count);
                if (lstThumbs.Count > 0)
                {
                    response.Headers.Add("Content-Type", "image/jpeg");
                    var bytes = lstThumbs[0].ThumbData!;
                    //using var strm = new MemoryStream(bytes);
                    await response.WriteBytesAsync(bytes); ;
                }
                else
                {
                    throw new Exception($"Thumb for {myPixId} not found");
                }
            }
            catch (System.Exception ex)
            {
                await response.WriteStringAsync($"Error: {ex}");
                _logger.LogError("Error {type} {message} {ex}", ex.GetType().Name, ex.Message, ex.ToString());
                response.StatusCode = HttpStatusCode.InternalServerError;
            }
            return response;
        }
    }
}
