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

                // Check if we need to download the database from OneDrive
                await EnsureDatabaseIsAvailableAsync(req);

                var query = HttpUtility.ParseQueryString(req.Url.Query);
                string? Date1txt = query["Date1"];
                string? Date2txt = query["Date2"];
                string? MediaType = query["MediaType"];
                string? StrFilter = query["NotesFilter"]?.ToLower() ?? string.Empty;
                string? MaxPixStr = query["MaxPix"];
                string? PublishToAlbum = query["PublishToAlbum"];
                string? AlbumName = SanitizeOneDriveFileName(query["AlbumName"]);
                string? AlbumMaxItemsStr = query["AlbumMaxItems"];

                var maxPix = 50;
                if (!string.IsNullOrEmpty(MaxPixStr))
                {
                    maxPix = int.Parse(MaxPixStr);
                }

                var albumMaxItems = 100; // Default album item limit
                if (!string.IsNullOrEmpty(AlbumMaxItemsStr))
                {
                    if (int.TryParse(AlbumMaxItemsStr, out var parsedLimit))
                    {
                        albumMaxItems = parsedLimit;
                    }
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

                string? albumId = null;

                // Create GraphAPI album if PublishToAlbum is true (fire-and-forget)
                if (PublishToAlbum?.ToLower() == "true" && !string.IsNullOrWhiteSpace(AlbumName) && lstMyPix.Count > 0)
                {
                    albumId = Guid.NewGuid().ToString();

                    // Limit the items for album creation based on albumMaxItems
                    var albumItems = lstMyPix.Take(albumMaxItems).ToList();

                    // Start album creation in background without waiting
                    _ = Task.Run(async () =>
                    {
                        await CreateAlbumAsync(albumItems, AlbumName, req, albumId);
                    });

                    _logger.LogInformation("Album creation started in background for '{albumName}' with {count}/{total} items (limit: {limit})", 
                        AlbumName, albumItems.Count, lstMyPix.Count, albumMaxItems);
                }

                _logger.LogInformation("Function called: {function} {qstring} {numresults}", nameof(QueryPix), StrFilter, lstMyPix.Count);

                var result = new
                {
                    Results = lstMyPix,
                    AlbumId = albumId
                };

                var json = JsonConvert.SerializeObject(result);
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

        private async Task EnsureDatabaseIsAvailableAsync(HttpRequestData req)
        {
            var envvar = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT");
            var oneDriveDbPath = envvar != "Development" ? @"d:\home\MyPixNoThumbs.db" : @"data\MyPixNoThumbs.db";

            // If OneDrive database already exists, we're good
            if (File.Exists(oneDriveDbPath))
            {
                _logger.LogInformation("OneDrive database already exists at {path}", oneDriveDbPath);
                return;
            }

            try
            {
                _logger.LogInformation("OneDrive database not found, attempting to download...");

                // Use the user's access token from the request
                using var httpClient = getGraphAPIHttpClient(req);

                var filePath = Environment.GetEnvironmentVariable("ONEDRIVE_FILE_PATH") ?? "Documents/MyPixNoThumbs.db";
                var downloadUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{filePath}:/content";

                var response = await httpClient.GetAsync(downloadUrl);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Failed to download OneDrive database: {statusCode} - {error}", response.StatusCode, errorContent);
                    _logger.LogInformation("Will use fallback database instead");
                    return;
                }

                // Ensure directory exists
                var directory = Path.GetDirectoryName(oneDriveDbPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Save the downloaded database
                using var fileStream = File.Create(oneDriveDbPath);
                await response.Content.CopyToAsync(fileStream);

                var fileInfo = new FileInfo(oneDriveDbPath);
                _logger.LogInformation("Successfully downloaded OneDrive database: {size} bytes to {path}", fileInfo.Length, oneDriveDbPath);

                // Restart the DbContext factory to use the new database
                // Note: This would require more complex logic to refresh the connection string
                // For now, the app will use the new database on the next cold start
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error downloading OneDrive database, will use fallback: {error}", ex.Message);
            }
        }

        private async Task<string> GetBundleShareLinkAsync(HttpClient httpClient, string bundleId, CancellationToken cancellationToken = default)
        {
            var shareLinkUrl = MSGraphEndPoint + $"me/drive/items/{bundleId}/createLink";

            var linkData = new
            {
                type = "view",
                scope = "anonymous"
            };

            var json = System.Text.Json.JsonSerializer.Serialize(linkData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(shareLinkUrl, content, cancellationToken);

            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();

            // Parse the share link from the response
            using var jsonDoc = JsonDocument.Parse(responseContent);
            if (jsonDoc.RootElement.TryGetProperty("link", out var linkElement) &&
                linkElement.TryGetProperty("webUrl", out var webUrlElement))
            {
                return webUrlElement.GetString() ?? throw new Exception("Share link is null");
            }

            throw new Exception("Could not extract share link from response");
        }

        private HttpClient getGraphAPIHttpClient(HttpRequestData req)
        {
            var authHeader = req.Headers.FirstOrDefault(h => h.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase));
            if (authHeader.Key == null || !authHeader.Value.Any())
            {
                throw new UnauthorizedAccessException("No authorization header found");
            }
            
            var token = authHeader.Value.First().Replace("Bearer ", "");
            
            // Add token debugging
            _logger.LogInformation("Token length: {length}", token.Length);
            _logger.LogInformation("Token starts with: {start}", token.Substring(0, Math.Min(20, token.Length)));
            
            // Check if it's a JWT token (3 parts) or other format
            var parts = token.Split('.');
            if (parts.Length == 3)
            {
                try
                {
                    // Decode the header to check token info (without validation)
                    var headerJson = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(AddPadding(parts[0])));
                    _logger.LogInformation("JWT Token header: {header}", headerJson);
                    
                    // Decode the payload to check token info (without validation)
                    var payloadJson = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(AddPadding(parts[1])));
                    _logger.LogInformation("JWT Token payload: {payload}", payloadJson);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not decode JWT token for debugging");
                }
            }
            else
            {
                // Handle non-JWT tokens (like MSA tokens)
                _logger.LogInformation("Non-JWT token received with {count} parts - this is normal for some Microsoft Graph scenarios", parts.Length);
            }
            
            // Create HttpClient with the token regardless of format
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            return httpClient;
        }

        private static string AddPadding(string base64)
        {
            var padding = 4 - (base64.Length % 4);
            if (padding < 4)
            {
                base64 += new string('=', padding);
            }
            return base64;
        }

        private async Task CreateAlbumAsync(List<MyPix> myPixes, string albumName, HttpRequestData req, string albumId)
        {
            try
            {
                using var httpClient = getGraphAPIHttpClient(req);
                // first test Read access to OneDrive: get the list of albums (bundles)
                var test = await FindExistingBundleAsync(httpClient, "MyPixAlbumTest", CancellationToken.None);
                _logger.LogInformation("Test album found: {test}", test ?? "No album found, will create new one");

                // Update status to creating with total items
                AlbumStatusFunction.UpdateAlbumStatus(albumId, "creating",
                    $"Creating album '{albumName}' with {myPixes.Count} items", "", myPixes.Count, 0);

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

                // Update progress as items are added
                var completedItems = 0;
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
                            await UpdateDriveItemDescriptionAsync(httpClient, fileId, pix.Notes, CancellationToken.None);
                        }
                        else
                        {
                            _logger.LogWarning("Could not find file {FileName} in OneDrive", pix.FullFileName);
                        }

                        completedItems++;
                        // Update with progress including TotalItems and CompletedItems
                        AlbumStatusFunction.UpdateAlbumStatus(albumId, "creating",
                            $"Added {completedItems}/{myPixes.Count} items to album", "", myPixes.Count, completedItems);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error adding file {FileName} to album {AlbumName}", pix.FileName, albumName);
                    }
                }

                // Get share link and mark as completed
                var shareLink = await GetBundleShareLinkAsync(httpClient, bundleId);
                AlbumStatusFunction.UpdateAlbumStatus(albumId, "completed",
                    $"Album '{albumName}' created successfully with {completedItems} items", shareLink, myPixes.Count, completedItems);

                _logger.LogInformation("Successfully created album '{albumName}' with {count} items", albumName, completedItems);
            }
            catch (Exception albumEx)
            {
                AlbumStatusFunction.UpdateAlbumStatus(albumId, "failed",
                    $"Failed to create album '{albumName}': {albumEx.Message}", "", 0, 0);
                _logger.LogError(albumEx, "Failed to create album '{albumName}': {error}", albumName, albumEx.Message);
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
        private async Task UpdateDriveItemDescriptionAsync(HttpClient httpClient, string itemId, string description, CancellationToken cancellationToken = default)
        {
            try
            {
                var updateUrl = MSGraphEndPoint + $"me/drive/items/{itemId}";

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