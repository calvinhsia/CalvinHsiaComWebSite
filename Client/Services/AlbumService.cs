using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Client.Shared;
using Client.Services;

namespace BlazorWasm.Services
{
    /// <summary>
    /// Service for managing OneDrive album operations via Microsoft Graph API
    /// </summary>
    public class AlbumService
    {
        private readonly PictureService _pictureService;

        public AlbumService(PictureService pictureService)
        {
            _pictureService = pictureService;
        }

        private SharedDriveContext? SharedContext => _pictureService.SharedContext;

        private const string MSGraphEndPoint = "https://graph.microsoft.com/v1.0/";

        /// <summary>
        /// Finds an existing album by name
        /// </summary>
        public async Task<string?> FindExistingAlbumAsync(HttpClient httpClient, string albumName)
        {
            try
            {
                var response = await httpClient.GetAsync($"{MSGraphEndPoint}me/drive/bundles");

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[AlbumService] Could not list bundles: {response.StatusCode}");
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                
                if (doc.RootElement.TryGetProperty("value", out var valueArray))
                {
                    foreach (var bundle in valueArray.EnumerateArray())
                    {
                        if (bundle.TryGetProperty("name", out var nameElement) &&
                            bundle.TryGetProperty("id", out var idElement))
                        {
                            var bundleName = nameElement.GetString();
                            if (string.Equals(bundleName, albumName, StringComparison.OrdinalIgnoreCase))
                            {
                                var bundleId = idElement.GetString();
                                Console.WriteLine($"[AlbumService] Found existing album '{albumName}' with ID: {bundleId}");
                                return bundleId;
                            }
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AlbumService] Error finding existing album: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Creates a new album in OneDrive
        /// </summary>
        public async Task<string?> CreateNewAlbumAsync(HttpClient httpClient, string albumName)
        {
            try
            {
                var bundleRequest = new
                {
                    name = albumName,
                    bundle = new { album = new { } },
                    conflict = "rename"
                };

                var json = JsonSerializer.Serialize(bundleRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync($"{MSGraphEndPoint}me/drive/bundles", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(responseJson);
                    
                    var bundleId = doc.RootElement.TryGetProperty("id", out var idElement) 
                        ? idElement.GetString() 
                        : null;
                    Console.WriteLine($"[AlbumService] ✅ Created new album '{albumName}' with ID: {bundleId}");
                    return bundleId;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[AlbumService] ❌ Failed to create album: {response.StatusCode} - {errorContent}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AlbumService] ❌ Error creating album: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Returns the set of file names (not IDs) already present in the album (bundle), following all @odata.nextLink pages.
        /// Comparison by name is used because OneDrive can return different ID formats (numeric vs GUID) for the same file.
        /// </summary>
        /// <summary>
        /// Returns the set of item IDs already present in the album (bundle), following all @odata.nextLink pages.
        /// Deduplication by ID is correct because the same filename can exist in multiple source folders;
        /// bundles reference items by ID, so both can be added.
        /// </summary>
        public async Task<HashSet<string>> GetAlbumItemIdsAsync(HttpClient httpClient, string bundleId, CancellationToken cancellationToken = default)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string? nextLink = $"{MSGraphEndPoint}me/drive/items/{bundleId}/children?$select=id,name&$top=1000";
            while (nextLink != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var response = await httpClient.GetAsync(nextLink, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[AlbumService] GetAlbumItemIds failed: {response.StatusCode}");
                    break;
                }
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);
                foreach (var item in doc.RootElement.GetProperty("value").EnumerateArray())
                {
                    if (item.TryGetProperty("id", out var idEl))
                        ids.Add(idEl.GetString()!);
                }
                nextLink = doc.RootElement.TryGetProperty("@odata.nextLink", out var nl) ? nl.GetString() : null;
            }
            Console.WriteLine($"[AlbumService] Album {bundleId} already has {ids.Count} items.");
            return ids;
        }

        /// <summary>
        /// Gets a shareable link for the album
        /// </summary>
        public async Task<string> GetShareLinkAsync(HttpClient httpClient, string bundleId)
        {
            try
            {
                var linkRequest = new
                {
                    type = "view",
                    scope = "anonymous"
                };

                var json = JsonSerializer.Serialize(linkRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync($"{MSGraphEndPoint}me/drive/items/{bundleId}/createLink", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(responseJson);
                    
                    if (doc.RootElement.TryGetProperty("link", out var linkElement) &&
                        linkElement.TryGetProperty("webUrl", out var webUrlElement))
                    {
                        return webUrlElement.GetString() ?? $"https://onedrive.live.com/?id={bundleId}";
                    }
                    
                    return $"https://onedrive.live.com/?id={bundleId}";
                }
                else
                {
                    Console.WriteLine($"[AlbumService] Could not create share link: {response.StatusCode}");
                    return $"https://onedrive.live.com/?id={bundleId}";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AlbumService] Error creating share link: {ex.Message}");
                return $"https://onedrive.live.com/?id={bundleId}";
            }
        }

        /// <summary>
        /// Updates the description/notes field for a drive item
        /// </summary>
        public async Task UpdateItemDescriptionAsync(HttpClient httpClient, string itemId, string description, CancellationToken cancellationToken = default)
        {
            try
            {
                var updateUrl = $"{MSGraphEndPoint}me/drive/items/{itemId}";
                var updateData = new { description = description };
                var json = JsonSerializer.Serialize(updateData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PatchAsync(updateUrl, content, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[AlbumService] ✅ Updated description for item {itemId}");
                }
                else
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[AlbumService] ⚠️ Failed to update description for {itemId}: {response.StatusCode}");
                    // Don't throw - continue album creation even if description update fails
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"[AlbumService] Description update for item {itemId} was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AlbumService] ⚠️ Error updating description for {itemId}: {ex.Message}");
                // Don't throw - continue album creation even if description update fails
            }
        }

        /// <summary>
        /// Gets file metadata from OneDrive for the given <paramref name="pix"/>,
        /// routing through the shared drive context when set.
        /// </summary>
        public async Task<JsonElement?> GetFileMetadataAsync(HttpClient httpClient, MyPix pix, CancellationToken cancellationToken = default)
        {
            var graphPath = pix.GraphPath(SharedContext != null);
            try
            {
                string url;
                if (SharedContext != null)
                    url = $"{MSGraphEndPoint}drives/{SharedContext.DriveId}/items/{SharedContext.RootItemId}:/{graphPath}:";
                else
                    url = $"{MSGraphEndPoint}me/drive/root:/{graphPath}";

                var response = await httpClient.GetAsync(url, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(cancellationToken);
                    using var doc = JsonDocument.Parse(json);
                    return doc.RootElement.Clone();
                }

                Console.WriteLine($"[AlbumService] GetFileMetadataAsync failed ({response.StatusCode}) for {graphPath}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AlbumService] Error getting file metadata for {graphPath}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Resolves thumbnail URLs for a batch of MyPix items using the Graph $batch endpoint.
        /// Delegates to <see cref="GraphBatchHelper.GetThumbnailUrlsBatchAsync"/>.
        /// </summary>
        public Task GetThumbnailUrlsBatchAsync(
            HttpClient httpClient, IList<MyPix> pixList, string thumbSize,
            Func<Dictionary<string, string?>, int, Task> onChunkReady,
            CancellationToken cancellationToken = default)
            => GraphBatchHelper.GetThumbnailUrlsBatchAsync(
                httpClient, pixList, thumbSize, SharedContext, onChunkReady, cancellationToken);

        /// <summary>
        /// Builds the URL for a thumbnail, respecting the shared drive context.
        /// </summary>
        public string GetThumbnailUrl(string itemId, string thumbSize)
        {
            if (SharedContext != null)
                return $"{MSGraphEndPoint}drives/{SharedContext.DriveId}/items/{itemId}/thumbnails/0/{thumbSize}/content";
            return $"{MSGraphEndPoint}me/drive/items/{itemId}/thumbnails/0/{thumbSize}/content";
        }

        /// <summary>
        /// Builds the URL for full item content, respecting the shared drive context.
        /// </summary>
        public string GetItemContentUrl(string itemId)
        {
            if (SharedContext != null)
                return $"{MSGraphEndPoint}drives/{SharedContext.DriveId}/items/{itemId}/content";
            return $"{MSGraphEndPoint}me/drive/items/{itemId}/content";
        }

        /// <summary>
        /// Gets the pre-authenticated CDN download URL (@microsoft.graph.downloadUrl), the
        /// video rotation angle (from video.rotation in Graph metadata), and the file size.
        /// The URL supports HTTP range requests for native browser streaming.
        /// Returns (null, 0, 0) if the metadata call fails.
        /// </summary>
        public async Task<(string? Url, int Rotation, long FileSize)> GetDownloadUrlAsync(HttpClient httpClient, MyPix pix, CancellationToken cancellationToken = default)
        {
            var fileData = await GetFileMetadataAsync(httpClient, pix, cancellationToken);
            if (fileData == null) return (null, 0, 0);

            string? url = null;
            if (fileData.Value.TryGetProperty("@microsoft.graph.downloadUrl", out var urlProp))
                url = urlProp.GetString();
            else if (fileData.Value.TryGetProperty("id", out var idProp))
                url = GetItemContentUrl(idProp.GetString()!);

            // video.rotation is set by the phone's camera (e.g. 90 for portrait-recorded MP4)
            int rotation = 0;
            if (fileData.Value.TryGetProperty("video", out var videoProp) &&
                videoProp.TryGetProperty("rotation", out var rotProp))
                rotation = rotProp.GetInt32();

            long fileSize = 0;
            if (fileData.Value.TryGetProperty("size", out var sizeProp))
                fileSize = sizeProp.GetInt64();

            return (url, rotation, fileSize);
        }

        /// <summary>
        /// Adds a batch of items to an album using two Graph $batch round-trips per chunk of 20:
        /// one to resolve file paths → item IDs, one to POST children to the bundle.
        /// Calls <paramref name="onChunkDone"/> after each chunk with per-item results.
        /// Returns as soon as cancellation is requested.
        /// </summary>
        public async Task AddItemsToAlbumBatchAsync(
            HttpClient httpClient,
            string bundleId,
            IList<MyPix> items,
            int startIndex,
            Func<IList<(MyPix pix, bool success, string? error)>, Task> onChunkDone,
            HashSet<string>? existingItemIds = null,
            CancellationToken cancellationToken = default)
        {
            const string batchUrl = GraphBatchHelper.BatchUrl;
            const int batchSize = GraphBatchHelper.BatchSize;

            for (int chunkStart = startIndex; chunkStart < items.Count; chunkStart += batchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var chunk = items.Skip(chunkStart).Take(batchSize).ToList();

                // Step 1: resolve file paths → item IDs
                var indexToItemId = await GraphBatchHelper.ResolveItemIdsBatchAsync(
                    httpClient, chunk, SharedContext, cancellationToken);

                Console.WriteLine($"[AlbumService] Chunk {chunkStart}: {chunk.Count} items, resolved {indexToItemId.Count} IDs. bundleId={bundleId}");

                var addResults = new List<(MyPix pix, bool success, string? error)>();

                // Mark items that failed ID resolution as failures
                for (int i = 0; i < chunk.Count; i++)
                {
                    if (!indexToItemId.ContainsKey(i))
                        addResults.Add((chunk[i], false, "id_not_resolved"));
                }

                // Skip items already in the album by item ID.
                if (existingItemIds != null)
                {
                    int skippedCount = 0;
                    foreach (var kv in indexToItemId.ToList())
                    {
                        if (existingItemIds.Contains(kv.Value))
                        {
                            addResults.Add((chunk[kv.Key], false, "already_exists"));
                            indexToItemId.Remove(kv.Key);
                            skippedCount++;
                        }
                    }
                    Console.WriteLine($"[AlbumService] Chunk {chunkStart}: pre-check skipped {skippedCount}/{chunk.Count}, sending {indexToItemId.Count} to batch");
                }

                // Step 2: batch-add children to the bundle (only items not already present)
                var addRequests = indexToItemId.Select(kv => new
                {
                    id = kv.Key.ToString(),
                    method = "POST",
                    url = $"/me/drive/bundles/{bundleId}/children",
                    headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
                    body = new { id = kv.Value }
                }).ToList();

                if (addRequests.Count > 0)
                {
                    var addBatchBody = JsonSerializer.Serialize(new { requests = addRequests });
                    Console.WriteLine($"[AlbumService] Batch add request (chunk {chunkStart}): {addBatchBody[..Math.Min(500, addBatchBody.Length)]}");

                    HttpResponseMessage addResponse = null!;
                    string addJson = string.Empty;
                    for (int attempt = 0; attempt < 3; attempt++)
                    {
                        addResponse = await httpClient.PostAsync(batchUrl,
                            new StringContent(addBatchBody, Encoding.UTF8, "application/json"), cancellationToken);
                        addJson = await addResponse.Content.ReadAsStringAsync(cancellationToken);
                        if ((int)addResponse.StatusCode == 429)
                        {
                            var retryAfter = addResponse.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(5 * (attempt + 1));
                            Console.WriteLine($"[AlbumService] 429 throttled on add (chunk {chunkStart}), retrying after {retryAfter.TotalSeconds}s");
                            await Task.Delay(retryAfter, cancellationToken);
                            continue;
                        }
                        break;
                    }
                    Console.WriteLine($"[AlbumService] Batch add HTTP status={addResponse.StatusCode} response: {addJson[..Math.Min(1000, addJson.Length)]}");

                    if (!addResponse.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"[AlbumService] Batch add outer HTTP failed (chunk {chunkStart}): {addResponse.StatusCode}");
                        foreach (var kv in indexToItemId)
                            addResults.Add((chunk[kv.Key], false, $"batch_http_{addResponse.StatusCode}"));
                    }
                    else
                    {
                        using var addDoc = JsonDocument.Parse(addJson);
                        foreach (var resp in addDoc.RootElement.GetProperty("responses").EnumerateArray())
                        {
                            var idx = int.Parse(resp.GetProperty("id").GetString()!);
                            var status = resp.GetProperty("status").GetInt32();
                            var pix = chunk[idx];
                            if (status == 200 || status == 201 || status == 204)
                            {
                                addResults.Add((pix, true, null));
                                existingItemIds?.Add(indexToItemId[idx]);
                                Console.WriteLine($"[AlbumService] ✅ Batch added {pix.FileName} status={status}");
                            }
                            else
                            {
                                // Check for already-exists conflicts (409)
                                string? errorCode = null;
                                string? errorMsg = null;
                                if (resp.TryGetProperty("body", out var bodyEl) &&
                                    bodyEl.ValueKind == JsonValueKind.Object &&
                                    bodyEl.TryGetProperty("error", out var errEl))
                                {
                                    errEl.TryGetProperty("code", out var codeEl);
                                    errEl.TryGetProperty("message", out var msgEl);
                                    errorCode = codeEl.GetString();
                                    errorMsg = msgEl.GetString();
                                }

                                bool alreadyExists = status == 409 ||
                                    errorCode == "itemAlreadyExists" ||
                                    errorCode == "nameAlreadyExists" ||
                                    (errorMsg != null && errorMsg.Contains("already exists", StringComparison.OrdinalIgnoreCase));

                                addResults.Add((pix, false, alreadyExists ? "already_exists" : $"status_{status}"));
                                if (alreadyExists)
                                    existingItemIds?.Add(indexToItemId[idx]);
                                Console.WriteLine($"[AlbumService] ❌ Batch add status={status} for {pix.FileName}: code={errorCode} msg={errorMsg}");
                            }
                        }
                    }
                }

                await onChunkDone(addResults);
            }
        }

        /// <summary>
        /// Updates descriptions for a batch of (itemId, description) pairs using Graph $batch PATCH requests.
        /// Failures are logged but not thrown.
        /// </summary>
        public async Task UpdateDescriptionsBatchAsync(
            HttpClient httpClient,
            IList<(string itemId, string description)> updates,
            CancellationToken cancellationToken = default)
        {
            const string batchUrl = GraphBatchHelper.BatchUrl;
            const int batchSize = GraphBatchHelper.BatchSize;

            for (int chunkStart = 0; chunkStart < updates.Count; chunkStart += batchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var chunk = updates.Skip(chunkStart).Take(batchSize).ToList();
                var requests = chunk.Select((u, i) => new
                {
                    id = i.ToString(),
                    method = "PATCH",
                    url = $"/me/drive/items/{u.itemId}",
                    headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
                    body = new { description = u.description }
                }).ToList();

                var body = JsonSerializer.Serialize(new { requests });
                try
                {
                    var response = await httpClient.PostAsync(batchUrl,
                        new StringContent(body, Encoding.UTF8, "application/json"), cancellationToken);
                    if (!response.IsSuccessStatusCode)
                        Console.WriteLine($"[AlbumService] Batch description update failed: {response.StatusCode}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AlbumService] Batch description update error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Adds a file to an album
        /// </summary>
        public async Task<(bool success, string? errorMessage)> AddFileToAlbumAsync(
            HttpClient httpClient,
            string bundleId,
            string fileId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var addRequest = new { id = fileId };
                var json = JsonSerializer.Serialize(addRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync($"{MSGraphEndPoint}me/drive/bundles/{bundleId}/children", content, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return (true, null);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);

                    // Check if it already exists
                    if (response.StatusCode == System.Net.HttpStatusCode.Conflict ||
                        errorContent.Contains("already exists") ||
                        errorContent.Contains("itemAlreadyExists") ||
                        errorContent.Contains("nameAlreadyExists"))
                    {
                        return (false, "already_exists");
                    }

                    return (false, errorContent);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AlbumService] Error adding file to album: {ex.Message}");
                return (false, ex.Message);
            }
        }
    }
}
