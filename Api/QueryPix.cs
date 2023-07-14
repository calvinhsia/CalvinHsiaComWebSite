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
    public class QueryPixClass
    {
        private readonly ILogger _logger;
        string dbPathDefault = @"data\MyPix.db"; //https://www.youtube.com/watch?v=xSAyEDFLFTw
        string dbPathAzure = @"d:\home\MyPix.db";

        public QueryPixClass(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<EnvInfoClass>();
        }
        async Task<(string pathDb, bool didCopy)> CopyDbAsync()
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

        [Function(nameof(QueryPix))]
        public async Task<HttpResponseData> QueryPix([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            try
            {
                var (pathdb, didCopy) = await CopyDbAsync();
                response.Headers.Add("Content-Type", "application/json");
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                var httpQuery = HttpUtility.ParseQueryString(req.Url.Query);
                var QueryString = (httpQuery["QueryString"]);

                using var dbc = new MyPixWebDBContext(pathdb);
                var lstMyPix = await dbc.MyPixes.FromSqlInterpolated($"select * from MyPix where Notes like {("%" + QueryString + "%")}").ToListAsync();
                _logger.LogInformation("Function called: {function} {qstring}  {numresults} {DidCopy}", nameof(QueryPix), QueryString, lstMyPix.Count, didCopy);
                var json = JsonConvert.SerializeObject(lstMyPix);

                await response.WriteStringAsync(json);
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
