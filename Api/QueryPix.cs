using Client.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
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
                            if (string.IsNullOrEmpty(StrFilter))
                            {
                                return true;
                            }

                            var filt = StrFilter.Trim();
                            if (filt.StartsWith("$")) // filename filter
                            {
                                filt = filt[1..];
                                if (Regex.IsMatch(p.FileName ?? string.Empty, filt, RegexOptions.IgnoreCase))
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
                                        if ((p.Notes ?? string.Empty).Contains(filtpart, StringComparison.OrdinalIgnoreCase))
                                        {
                                            return true;
                                        }
                                    }
                                    return false;
                                }
                                else
                                {
                                    var filtParts = filt.Split(' ');
                                    foreach (var filtpart in filtParts)
                                    {
                                        if (!(p.Notes ?? string.Empty).Contains(filtpart, StringComparison.OrdinalIgnoreCase))
                                        {
                                            return false;
                                        }
                                    }
                                    return true;
                                }
                            }
                            if (filt.StartsWith("^") && filt.Length > 2)
                            {
                                filt = filt[1..];
                                if (Regex.IsMatch(p.Notes ?? string.Empty, filt, RegexOptions.IgnoreCase)) // ^(?=.*\bREGEX\b)(?=.*\bPATTERN\b).*$/     Precede with (?i) for case insensitive
                                {
                                    return true;
                                }
                            }
                            else
                            {
                                if ((p.Notes ?? string.Empty).Contains(StrFilter, StringComparison.OrdinalIgnoreCase))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                    return false;
                }
                var lstMyPix = dbc.MyPixes.AsEnumerable().Where(p => theFilter(p)).OrderBy(p => p.Date).ToList();
                lstMyPix.Reverse();

                _logger.LogInformation("Function called: {function} {qstring} {numresults}", nameof(QueryPix), StrFilter, lstMyPix.Count);

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


        private async Task UpdateDriveItemDescriptionAsync(HttpClient httpClient, string itemId, string description, CancellationToken cancellationToken = default, string? userId = null)
        {
            try
            {
                var updateUrl = string.IsNullOrEmpty(userId)
                    ? $"{MSGraphEndPoint}me/drive/items/{itemId}"
                    : $"{MSGraphEndPoint}users/{userId}/drive/items/{itemId}";

                var updateData = new
                {
                    description = description
                };

                var json = System.Text.Json.JsonSerializer.Serialize(updateData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger?.LogTrace($"Updating description for item {itemId}: {description}");

                var response = await httpClient.PatchAsync(updateUrl, content, cancellationToken);

                var responseContent = await response.Content.ReadAsStringAsync();
                _logger?.LogTrace($"Update description response ({response.StatusCode}): {responseContent}");

                if (response.IsSuccessStatusCode)
                {
                    _logger?.LogTrace($"Successfully updated description for item {itemId}");
                }
                else
                {
                    _logger?.LogTrace($"Failed to update description for item {itemId}. Status: {response.StatusCode}, Response: {responseContent}");
                    // Don't throw here - we want the album creation to continue even if description updates fail
                }
            }
            catch (OperationCanceledException)
            {
                _logger?.LogError($"Description update for item {itemId} was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error updating description for item {itemId}: {ex.Message}");
                // Don't throw here - we want the album creation to continue even if description updates fail
            }
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
