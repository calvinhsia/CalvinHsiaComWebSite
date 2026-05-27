using Client.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Api
{

    public class QueryPixClass
    {
        private readonly ILogger _logger;
        private readonly IDbContextFactory<MyPixWebDBContext> dbContextFactory;
        private readonly IHttpClientFactory _httpClientFactory;
        public const string MSGraphEndPoint = "https://graph.microsoft.com/v1.0/";

        public QueryPixClass(
            IDbContextFactory<MyPixWebDBContext> dbContextFactory,
            ILoggerFactory loggerFactory,
            IHttpClientFactory httpClientFactory)
        {
            _logger = loggerFactory.CreateLogger<QueryPixClass>();
            this.dbContextFactory = dbContextFactory;
            _httpClientFactory = httpClientFactory;
        }

        [Function(nameof(QueryPix))]
        public async Task<HttpResponseData> QueryPix(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var callerOid = await SwaAuthHelper.GetAuthorizedOidAsync(req, _logger);
            if (SwaAuthHelper.IsRejected(callerOid))
            {
                var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
                unauthorized.Headers.Add("Content-Type", "text/plain");
                await unauthorized.WriteStringAsync(SwaAuthHelper.RejectionReason(callerOid));
                return unauthorized;
            }

            // Resolve per-user picture settings (empty oid = DEBUG bypass)
            var userSettings = string.IsNullOrEmpty(callerOid) ? null : SwaAuthHelper.GetUserSettings(callerOid);

            var response = req.CreateResponse(HttpStatusCode.OK);
            try
            {
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                response.Headers.Add("Access-Control-Allow-Origin", "*");

                var query = HttpUtility.ParseQueryString(req.Url.Query);
                string? Date1txt = query["Date1"];
                string? Date2txt = query["Date2"];
                string? MediaType = query["MediaType"];
                string? StrFilter = query["NotesFilter"]?.ToLower() ?? string.Empty;

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

                // Apply per-user restrictions from PictureSettings.json
                string userPreFilter = "";
                if (userSettings != null)
                {
                    // Clamp the requested date range to what this user is allowed to see
                    if (userSettings.StartDate.HasValue)
                        DtFilterStart = DtFilterStart.HasValue && DtFilterStart > userSettings.StartDate
                            ? DtFilterStart : userSettings.StartDate;
                    if (userSettings.EndDate.HasValue)
                        DtFilterEnd = DtFilterEnd.HasValue && DtFilterEnd < userSettings.EndDate
                            ? DtFilterEnd : userSettings.EndDate;
                    userPreFilter = userSettings.Filter;
                    _logger.LogInformation("[QueryPix] User {oid} restricted: preFilter='{f}' start={s} end={e}",
                        callerOid, userPreFilter, DtFilterStart, DtFilterEnd);
                }

                using var dbc = dbContextFactory.CreateDbContext();

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
                            // Apply per-user pre-filter before the caller's own filter
                            if (!string.IsNullOrEmpty(userPreFilter) && !MatchesFilter(p, userPreFilter))
                                return false;

                            if (string.IsNullOrEmpty(StrFilter))
                            {
                                return true;
                            }

                            return MatchesFilter(p, StrFilter);
                        }
                    }
                    return false;
                }
                var lstMyPix = dbc.MyPixes.AsEnumerable().Where(p => theFilter(p)).OrderBy(p => p.Date).ToList();
                lstMyPix.Reverse();

                _logger.LogInformation("Function called: {function} {qstring} {numresults}", nameof(QueryPix), StrFilter, lstMyPix.Count);

                var json = JsonSerializer.Serialize(lstMyPix);
                await response.WriteStringAsync(json);
            }
            catch (System.Exception ex)
            {
                _logger.LogError("Error {type} {message} {ex}", ex.GetType().Name, ex.Message, ex.ToString());
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                errorResponse.Headers.Add("Content-Type", "text/plain; charset=utf-8");
                await errorResponse.WriteStringAsync($"Error: {ex.GetType().Name}: {ex.Message}");
                return errorResponse;
            }
            return response;
        }

        private static bool MatchesFilter(MyPix p, string filter)
        {
            var filt = filter.Trim();
            if (string.IsNullOrEmpty(filt)) return true;

            if (filt.StartsWith("$")) // filename filter
            {
                filt = filt[1..];
                return Regex.IsMatch(p.FileName ?? string.Empty, filt, RegexOptions.IgnoreCase);
            }
            if (filt.Contains(' '))
            {
                if (filt.StartsWith("|")) // OR: any word matches
                {
                    var filtParts = filt[1..].Split(' ');
                    foreach (var filtpart in filtParts)
                    {
                        if ((p.Notes ?? string.Empty).Contains(filtpart, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                    return false;
                }
                else // AND: all words must match
                {
                    var filtParts = filt.Split(' ');
                    foreach (var filtpart in filtParts)
                    {
                        if (!(p.Notes ?? string.Empty).Contains(filtpart, StringComparison.OrdinalIgnoreCase))
                            return false;
                    }
                    return true;
                }
            }
            if (filt.StartsWith("^") && filt.Length > 2)
            {
                filt = filt[1..];
                return Regex.IsMatch(p.Notes ?? string.Empty, filt, RegexOptions.IgnoreCase);
            }
            return (p.Notes ?? string.Empty).Contains(filt, StringComparison.OrdinalIgnoreCase);
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
