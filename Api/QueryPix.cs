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
                response.Headers.Add("Content-Type", "application/json");
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                var httpQuery = HttpUtility.ParseQueryString(req.Url.Query);
                var QueryString = (httpQuery["QueryString"]);
                string json;
                lock (this)
                {
                    using var conn = new SqliteConnection(@$"Filename = data\Mypix.db");
                    conn.Open();
                    var sqlCmd = new SqliteCommand(@"Select * from MyPix where Notes like '%carrots%'", conn);
                    using var res = sqlCmd.ExecuteReader();
                    var lstMyPix = new List<MyPix>();
                    while (res.Read())
                    {
                        MyPix mypix = new MyPix()
                        {
                            Id = (int)(long)res["Id"],
                            FileName = (string)res["FileName"],
                            Date = DateTime.Parse((string)res["Date"]),
                            PathEnum = (int)(long)res["PathEnum"],
                            Notes = (string)res["Notes"],
                            Rotate = (int)(long)res["Rotate"]
                        };
                        Console.WriteLine($"{mypix}");
                        lstMyPix.Add(mypix);
                    }
                    //using var dbc = new MyPixWebDBContext();
                    //var lstMyPix = await dbc.MyPixes.FromSqlInterpolated($"select * from MyPix where Notes like {("%" + QueryString + "%")}").ToListAsync();
                    //_logger.LogInformation("Function called: {function} {qstring}  {numresults}", nameof(QueryPix), QueryString, lstMyPix.Count);
                    json = JsonConvert.SerializeObject(lstMyPix);
                    conn.Close();
                }

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
