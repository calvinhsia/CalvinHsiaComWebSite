using Client.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req
            //ClaimsPrincipal principal
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
                string? StrFilter = query["NotesFilter"]?.ToLower() ?? string.Empty;
                string? MaxPixStr = query["MaxPix"];
                string? PublishToAlbum = query["PublishToAlbum"]; // "true" or "false"
                string? AlbumName = SanitizeOneDriveFileName(query["AlbumName"]); // name of album to publish to, if PublishToAlbum is true
                var maxPix = 50;
                if (!string.IsNullOrEmpty(MaxPixStr))
                {
                    maxPix = int.Parse(MaxPixStr);
                }
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
                var lstMyPix = dbc.MyPixes.AsEnumerable().Where(p => theFilter(p)).OrderBy(p => p.Date).Take(maxPix).ToList();
                lstMyPix.Reverse();

                // Create GraphAPI album if PublishToAlbum is true
                if (PublishToAlbum?.ToLower() == "true" && !string.IsNullOrWhiteSpace(AlbumName) && lstMyPix.Count > 0)
                {
                    try
                    {
                        await CreateGraphApiAlbumAsync(lstMyPix, AlbumName, req);
                        _logger.LogInformation("Successfully created album '{albumName}' with {count} items", AlbumName, lstMyPix.Count);
                    }
                    catch (Exception albumEx)
                    {
                        _logger.LogError(albumEx, "Failed to create album '{albumName}': {error}", AlbumName, albumEx.Message);
                        // Don't fail the main query, just log the error
                    }
                }

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
        private HttpClient getGraphAPIHttpClient(HttpRequestData req)
        {
            var authHeader = req.Headers.FirstOrDefault(h => h.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase));
            if (authHeader.Key == null || !authHeader.Value.Any())
            {
                throw new UnauthorizedAccessException("No authorization header found");
            }
            var token = authHeader.Value.First().Replace("Bearer ", "");
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            return httpClient;
        }

        private async Task CreateGraphApiAlbumAsync(List<MyPix> myPixes, string albumName, HttpRequestData req)
        {
            using var httpClient = getGraphAPIHttpClient(req);

            // Create a bundle (album) using Microsoft Graph API
            var bundleRequest = new Dictionary<string, object>
            {
                ["name"] = albumName,
                ["bundle"] = new { album = new { } },
                ["@microsoft.graph.conflictBehavior"] = "fail" // rename, replace, fail
            };
            var bundleJson = JsonConvert.SerializeObject(bundleRequest);
            var bundleContent = new StringContent(bundleJson, Encoding.UTF8, "application/json");

            var createBundleResponse = await httpClient.PostAsync($"{MSGraphEndPoint}drive/bundles", bundleContent);
            var bundleId = string.Empty;
            if (!createBundleResponse.IsSuccessStatusCode)
            {
                var errorContent = await createBundleResponse.Content.ReadAsStringAsync();
                if (!errorContent.Contains("already exists")) // {"error":{"code":"invalidRequest","message":"An Album with same name already exists.","innerError":{"code":"albumSameNameExists","date":"2025-08-03T07:05:06","request-id":"2131aea3-2054-46be-b316-6f1dc509a8ba","client-request-id":"2131aea3-2054-46be-b316-6f1dc509a8ba"}}}
                {
                    throw new Exception($"Failed to create bundle: {createBundleResponse.StatusCode} - {errorContent}");
                }
                // get the existing bundle ID if it already exists
                _logger.LogWarning("Bundle with name '{AlbumName}' already exists. Attempting to retrieve existing bundle ID.", albumName);
                bundleId = await FindExistingBundleAsync(httpClient, albumName);
                _logger.LogInformation("Found existing bundle {AlbumName} with ID: {BundleId}", albumName, bundleId);
            }
            else
            {
                var bundleResponseJson = await createBundleResponse.Content.ReadAsStringAsync();
                var bundleResponse = JsonConvert.DeserializeObject<dynamic>(bundleResponseJson);

                if (bundleResponse?.id == null)
                {
                    throw new Exception("Bundle creation response did not contain an ID");
                }
                bundleId = bundleResponse.id?.ToString() ?? string.Empty;
                _logger.LogInformation("Created bundle {AlbumName} with ID: {BundleId}", albumName, bundleId);
            }
            // Add items to the bundle
            foreach (var pix in myPixes)
            {
                try
                {
                    if (string.IsNullOrEmpty(pix.FullFileName))
                    {
                        _logger.LogWarning("Skipping pix with empty FullFileName: {FileName}", pix.FileName);
                        continue;
                    }

                    // Get the file ID first
                    var fileUrl = $"{MSGraphEndPoint}me/drive/root:/{pix.FullFileName}";
                    var fileResponse = await httpClient.GetAsync(fileUrl);

                    if (fileResponse.IsSuccessStatusCode)
                    {
                        var fileJson = await fileResponse.Content.ReadAsStringAsync();
                        var fileData = JsonConvert.DeserializeObject<dynamic>(fileJson);

                        if (fileData?.id == null)
                        {
                            _logger.LogWarning("File response did not contain an ID for file {FileName}", pix.FullFileName);
                            continue;
                        }

                        var fileId = fileData.id?.ToString() ?? string.Empty;

                        var addToBundleRequest = new
                        {
                            id = fileId
                        };

                        var addJson = JsonConvert.SerializeObject(addToBundleRequest);
                        var addContent = new StringContent(addJson, Encoding.UTF8, "application/json");

                        var addResponse = await httpClient.PostAsync($"{MSGraphEndPoint}drive/bundles/{bundleId}/children", addContent);

                        if (addResponse.IsSuccessStatusCode)
                        {
                            _logger.LogDebug("Added file {FileName} to bundle {AlbumName}", pix.FileName, albumName);
                        }
                        else
                        {
                            var addErrorContent = await addResponse.Content.ReadAsStringAsync();
                            _logger.LogWarning("Failed to add file {FileName} to bundle: {StatusCode} - {Error}",
                                pix.FileName, addResponse.StatusCode, addErrorContent);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Could not find file {FileName} in OneDrive", pix.FullFileName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error adding file {FileName} to album {AlbumName}", pix.FileName, albumName);
                }
            }
        }
        private static string SanitizeOneDriveFileName(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "MyPixAlbum";

            // OneDrive has specific restrictions
            var invalidChars = new[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };
            var sanitized = fileName;

            foreach (var invalidChar in invalidChars)
            {
                sanitized = sanitized.Replace(invalidChar, '_');
            }

            // Remove leading/trailing spaces and dots
            sanitized = sanitized.Trim().Trim('.');

            // Limit length
            if (sanitized.Length > 100)
            {
                sanitized = sanitized.Substring(0, 100);
            }

            return string.IsNullOrWhiteSpace(sanitized) ? "MyPixAlbum" : sanitized;
        }
        private async Task<string?> FindExistingBundleAsync(HttpClient httpClient, string albumName, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger?.LogTrace($"Searching for existing bundle with name: {albumName}");

                // Get all bundles for the user
                var getBundlesUrl = MSGraphEndPoint + "me/drive/bundles";

                var response = await httpClient.GetAsync(getBundlesUrl, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger?.LogTrace($"Failed to get bundles list. Status: {response.StatusCode}");
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                _logger?.LogTrace($"Get bundles response: {responseContent}");

                using var jsonDoc = JsonDocument.Parse(responseContent);
                if (jsonDoc.RootElement.TryGetProperty("value", out var bundlesArray))
                {
                    foreach (var bundle in bundlesArray.EnumerateArray())
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (bundle.TryGetProperty("name", out var nameElement) &&
                            bundle.TryGetProperty("id", out var idElement))
                        {
                            var bundleName = nameElement.GetString();
                            var bundleId = idElement.GetString();

                            _logger?.LogTrace($"Found bundle: '{bundleName}' with ID: {bundleId}");

                            // Check if this bundle matches our album name
                            if (string.Equals(bundleName, albumName, StringComparison.OrdinalIgnoreCase))
                            {
                                _logger?.LogTrace($"Match found! Existing bundle '{bundleName}' matches requested album '{albumName}'");
                                return bundleId;
                            }
                        }
                    }
                }

                _logger?.LogTrace($"No existing bundle found with name: {albumName}");
                return null;
            }
            catch (OperationCanceledException)
            {
                _logger?.LogTrace("Bundle search was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogTrace($"Error searching for existing bundle: {ex.Message}");
                return null;
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