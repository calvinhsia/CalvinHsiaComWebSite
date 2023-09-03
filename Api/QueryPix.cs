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
using System.Security.Claims;

namespace Api
{
    public class QueryPixClass
    {
        private readonly ILogger _logger;
        private readonly IDbContextFactory<MyPixWebDBContext> dbContextFactory;

        public QueryPixClass(
            IDbContextFactory<MyPixWebDBContext> dbContextFactory,
            ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<EnvInfoClass>();
            this.dbContextFactory = dbContextFactory;
        }

        [Function(nameof(QueryPix))]
        public async Task<HttpResponseData> QueryPix(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req,
            ClaimsPrincipal principal
            )
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            try
            {
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                var query = HttpUtility.ParseQueryString(req.Url.Query);
                string? Date1txt = query["Date1"];
                string? Date2txt = query["Date2"];
                string? MediaType = query["MediaType"]; // "Pic" means only pic, "Mov" means movie, blank means both
                string? NotesFilter = query["NotesFilter"]?.ToLower();
                string? MaxPixStr = query["MaxPix"];
                var maxPix = 50;
                if (!string.IsNullOrEmpty(MaxPixStr))
                {
                    maxPix = int.Parse(MaxPixStr);
                }
                //var qparams = req.QueryParametersDictionary();
                //qparams.TryGetValue("NotesFilter", out string? NotesFilterString);
                //qparams.TryGetValue("Date1", out string? Date1txt);
                //qparams.TryGetValue("Date2", out string? Date2txt);
                //qparams.TryGetValue("MediaType", out string? MediaType); // "Pic","Mov", (none=both)
                DateTime? date1 = null;
                DateTime? date2 = null;
                if (!string.IsNullOrEmpty(Date1txt))
                {
                    date1 = DateTime.Parse(Date1txt);
                }
                if (!string.IsNullOrEmpty(Date2txt))
                {
                    date2 = DateTime.Parse(Date2txt);
                }

                using var dbc = dbContextFactory.CreateDbContext();
                var lstMyPixbase = await dbc.MyPixes.Where(x =>
                        (string.IsNullOrEmpty(NotesFilter) || x.Notes.ToLower().Contains(NotesFilter)) &&
                        (date1 == null || x.Date >= date1) &&
                        (date2 == null || x.Date <= date2)
                    ).ToListAsync();

                var lstMyPix = lstMyPixbase.Where(x =>
                        (MediaType == null || (MediaType == "Pic" ? !x.IsVideo : (MediaType == "Mov" ? x.IsVideo : true)))).Take(maxPix).ToList();
                //var lstMyPix = await dbc.MyPixes.FromSqlInterpolated($"select * from MyPix where Notes like {("%" + NotesFilterString + "%")}").ToListAsync();
                _logger.LogInformation("Function called: {function} {qstring}  {numresults}", nameof(QueryPix), NotesFilter, lstMyPix.Count);
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
    public static class HttpRequestExtensions
    {
        public static Dictionary<string, string> QueryParametersDictionary(this HttpRequestData req)
        {
            var dict = req.Url.Query.Substring(1).Split('&').Select(x =>
            {
                if (x.Length == 0)
                {
                    return new KeyValuePair<string, string>("", "");
                }
                var parts = x.Split('=');
                return new KeyValuePair<string, string>(parts[0], parts[1].ToLower());
            }).ToDictionary(x => x.Key, x => x.Value);
            return dict;
        }
    }
}
