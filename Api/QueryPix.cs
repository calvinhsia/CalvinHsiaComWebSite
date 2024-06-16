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
using System.Text.RegularExpressions;

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
                string? MediaType = query["MediaType"]; //tolower from client. "pic" means only pic, "mov" means movie, blank means both
                string? StrFilter = query["NotesFilter"]!.ToLower();
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
                DateTime? DtFilterStart = null;
                DateTime? DtFilterEnd = null;
                if (!string.IsNullOrEmpty(Date1txt))
                {
                    DtFilterStart = DateTime.Parse(Date1txt);
                }
                if (!string.IsNullOrEmpty(Date2txt))
                {
                    DtFilterEnd = DateTime.Parse(Date2txt);
                }

                using var dbc = dbContextFactory.CreateDbContext();
                //"Start with &amp; for AND.\nStart With '$' for filename search ('$^(.*)\.avi')\n Start with '^' for regex e.g. '^.*(pui|hallie).*'  (CaseIgnore)"
                bool theFilter(MyPix p)
                {
                    if (p.Date >= DtFilterStart && p.Date <= DtFilterEnd)
                    {
                        var include = false;
                        if (p.IsVideo)
                        {
                            if (MediaType != "pic")
                            {
                                include = true;
                            }
                        }
                        else
                        {
                            if (MediaType != "mov")
                            {
                                include = true;
                            }
                        }
                        if (include)
                        {
                            var filt = StrFilter.Trim();
                            if (filt.StartsWith("$")) // filename filter
                            {
                                filt = filt[1..];
                                if (Regex.IsMatch(p.FileName, filt, RegexOptions.IgnoreCase))
                                {
                                    return true;
                                }
                                return false;
                            }
                            if (filt.Contains(' '))
                            {
                                if (filt.StartsWith("|")) // starts with "|": do an OR: ^(?=.*\bDuncan\b)(?=.*\bMartin\b)(?=.*\btest\b).*
                                { // OR
                                    var filtParts = filt[1..].Split(' ');
                                    foreach (var filtpart in filtParts)
                                    {
                                        if (p.Notes.Contains(filtpart, StringComparison.OrdinalIgnoreCase))
                                        {
                                            return true;
                                        }
                                    }
                                    return false;
                                    //                            filt = @$"^.*({string.Join('|', filtParts)}).*"; // do an OR
                                }
                                else
                                {
                                    var filtParts = filt.Split(' ');
                                    foreach (var filtpart in filtParts)
                                    {
                                        if (!p.Notes.Contains(filtpart, StringComparison.OrdinalIgnoreCase))
                                        {
                                            return false;
                                        }
                                    }
                                    return true;
                                    //var sb = new StringBuilder();
                                    //for (var i = 0; i < filtParts.Length; i++)
                                    //{
                                    //    sb.Append($@"(?=.*{filtParts[i]}).*");
                                    //}
                                    //filt = $@"^{sb}";

                                }
                            }
                            if (filt.StartsWith("^") && filt.Length > 2)
                            {
                                filt = filt[1..];
                                if (Regex.IsMatch(p.Notes, filt, RegexOptions.IgnoreCase)) // ^(?=.*\bREGEX\b)(?=.*\bPATTERN\b).*$/     Precede with (?i) for case insensitive
                                {
                                    return true;
                                }
                            }
                            else
                            {
                                if (p.Notes.Contains(StrFilter, StringComparison.OrdinalIgnoreCase))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                    return false;
                }
                var lstMyPix = dbc.MyPixes.AsEnumerable().Where(p => theFilter(p)).OrderBy(p=>p.Date).Take(maxPix).ToList();

                //var lstMyPix = await dbc.MyPixes.FromSqlInterpolated($"select * from MyPix where Notes like {("%" + NotesFilterString + "%")}").ToListAsync();
                _logger.LogInformation("Function called: {function} {qstring}  {numresults}", nameof(QueryPix), StrFilter, lstMyPix.Count);
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
            var dict = req.Url.Query[1..].Split('&').Select(x =>
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
