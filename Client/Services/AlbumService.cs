using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Client.Shared;
using Client.Services;

namespace WordScapeBlazorWasm.Services
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
        /// Resolves thumbnail URLs for a batch of MyPix items using the Graph API $batch endpoint.
        /// Sends one batch POST to get item IDs, then a second batch POST to get thumbnail redirect URLs.
        /// Returns a dictionary keyed by MyPix.FullFileName → direct thumbnail URL (or null on failure).
        /// Up to 20 items per batch (Graph API limit).
        /// </summary>
        public async Task GetThumbnailUrlsBatchAsync(
            HttpClient httpClient, IList<MyPix> pixList, string thumbSize,
            Func<Dictionary<string, string?>, int, Task> onChunkReady,
            CancellationToken cancellationToken = default)
        {
            if (pixList.Count == 0) return;

            bool isGuest = SharedContext != null;
            const string batchUrl = "https://graph.microsoft.com/v1.0/$batch";
            const int batchSize = 20; // Graph API hard limit

            for (int chunkStart = 0; chunkStart < pixList.Count; chunkStart += batchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var chunk = pixList.Skip(chunkStart).Take(batchSize).ToList();

                // Step 1: batch-resolve paths to item IDs
                var metadataRequests = chunk.Select((pix, i) =>
                {
                    var graphPath = pix.GraphPath(isGuest);
                    string url = SharedContext != null
                        ? $"/drives/{SharedContext.DriveId}/items/{SharedContext.RootItemId}:/{graphPath}?$select=id,name"
                        : $"/me/drive/root:/{graphPath}?$select=id,name";
                    return new { id = i.ToString(), method = "GET", url };
                }).ToList();

                var metaBatchBody = JsonSerializer.Serialize(new { requests = metadataRequests });
                var metaResponse = await httpClient.PostAsync(batchUrl,
                    new StringContent(metaBatchBody, Encoding.UTF8, "application/json"), cancellationToken);

                if (!metaResponse.IsSuccessStatusCode)
                {
                    var errBody = await metaResponse.Content.ReadAsStringAsync(cancellationToken);
                    Console.WriteLine($"[AlbumService] Batch metadata failed (chunk {chunkStart}): {metaResponse.StatusCode} - {errBody}");
                    continue;
                }

                var metaJson = await metaResponse.Content.ReadAsStringAsync(cancellationToken);
                using var metaDoc = JsonDocument.Parse(metaJson);

                var indexToItemId = new Dictionary<int, string>();
                foreach (var resp in metaDoc.RootElement.GetProperty("responses").EnumerateArray())
                {
                    var idx = int.Parse(resp.GetProperty("id").GetString()!);
                    var metaStatus = resp.GetProperty("status").GetInt32();
                    var metaBody = resp.GetProperty("body");
                    if (metaStatus == 200 &&
                        metaBody.ValueKind == JsonValueKind.Object &&
                        metaBody.TryGetProperty("id", out var idEl))
                    {
                        indexToItemId[idx] = idEl.GetString()!;
                    }
                    else
                    {
                        Console.WriteLine($"[AlbumService] Batch meta failed index {chunkStart + idx}: status={metaStatus} body={metaBody}");
                    }
                }

                if (indexToItemId.Count == 0) continue;

                // Step 2: batch-fetch thumbnail redirect URLs
                var thumbRequests = indexToItemId.Select(kv =>
                {
                    string url = SharedContext != null
                        ? $"/drives/{SharedContext.DriveId}/items/{kv.Value}/thumbnails/0/{thumbSize}/content"
                        : $"/me/drive/items/{kv.Value}/thumbnails/0/{thumbSize}/content";
                    return new { id = kv.Key.ToString(), method = "GET", url };
                }).ToList();

                var thumbBatchBody = JsonSerializer.Serialize(new { requests = thumbRequests });
                var thumbResponse = await httpClient.PostAsync(batchUrl,
                    new StringContent(thumbBatchBody, Encoding.UTF8, "application/json"), cancellationToken);

                if (!thumbResponse.IsSuccessStatusCode)
                {
                    var errBody = await thumbResponse.Content.ReadAsStringAsync(cancellationToken);
                    Console.WriteLine($"[AlbumService] Batch thumbnail failed (chunk {chunkStart}): {thumbResponse.StatusCode} - {errBody}");
                    continue;
                }

                var thumbJson = await thumbResponse.Content.ReadAsStringAsync(cancellationToken);
                using var thumbDoc = JsonDocument.Parse(thumbJson);

                // Build the chunk result dictionary keyed by FullFileName
                var chunkResult = new Dictionary<string, string?>();
                foreach (var resp in thumbDoc.RootElement.GetProperty("responses").EnumerateArray())
                {
                    var idx = int.Parse(resp.GetProperty("id").GetString()!);
                    var pix = chunk[idx];
                    var status = resp.GetProperty("status").GetInt32();
                    var body = resp.GetProperty("body");

                    if ((status == 200 || status == 302) &&
                        body.ValueKind == JsonValueKind.Object &&
                        body.TryGetProperty("@microsoft.graph.downloadUrl", out var dlUrl))
                    {
                        chunkResult[pix.FullFileName] = dlUrl.GetString();
                    }
                    else if (status == 302 &&
                        resp.TryGetProperty("headers", out var headers) &&
                        headers.TryGetProperty("Location", out var locationEl))
                    {
                        chunkResult[pix.FullFileName] = locationEl.GetString();
                    }
                    else
                    {
                        Console.WriteLine($"[AlbumService] Batch thumb failed for {pix.FileName}: status={status} body={body}");
                        chunkResult[pix.FullFileName] = null;
                    }
                }

                // Deliver this chunk to the caller immediately — progressive rendering
                await onChunkReady(chunkResult, chunkStart);
            }
        }

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
