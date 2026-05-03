using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Client.Services;
using Client.Shared;

namespace WordScapeBlazorWasm.Services
{
    /// <summary>
    /// Reusable helpers for Microsoft Graph $batch endpoint operations.
    /// </summary>
    public static class GraphBatchHelper
    {
        public const string BatchUrl = "https://graph.microsoft.com/v1.0/$batch";
        public const int BatchSize = 20; // Graph API hard limit per batch request

        /// <summary>
        /// Batch-resolves a list of MyPix file paths to their Graph item IDs.
        /// Returns a dictionary mapping chunk-relative index → item ID for items that resolved successfully.
        /// </summary>
        public static async Task<Dictionary<int, string>> ResolveItemIdsBatchAsync(
            HttpClient httpClient,
            IList<MyPix> chunk,
            SharedDriveContext? sharedContext,
            CancellationToken cancellationToken = default)
        {
            bool isGuest = sharedContext != null;
            var metadataRequests = chunk.Select((pix, i) =>
            {
                var graphPath = pix.GraphPath(isGuest);
                string url = sharedContext != null
                    ? $"/drives/{sharedContext.DriveId}/items/{sharedContext.RootItemId}:/{graphPath}?$select=id,name"
                    : $"/me/drive/root:/{graphPath}?$select=id,name";
                return new { id = i.ToString(), method = "GET", url };
            }).ToList();

            var body = JsonSerializer.Serialize(new { requests = metadataRequests });
            var indexToItemId = new Dictionary<int, string>();

            // Retry up to 3 times on throttling (429)
            for (int attempt = 0; attempt < 3; attempt++)
            {
                var response = await httpClient.PostAsync(BatchUrl,
                    new StringContent(body, Encoding.UTF8, "application/json"), cancellationToken);

                if ((int)response.StatusCode == 429)
                {
                    var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(5 * (attempt + 1));
                    Console.WriteLine($"[GraphBatch] 429 throttled on metadata, retrying after {retryAfter.TotalSeconds}s (attempt {attempt + 1})");
                    await Task.Delay(retryAfter, cancellationToken);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync(cancellationToken);
                    Console.WriteLine($"[GraphBatch] Batch metadata failed: {response.StatusCode} - {err[..Math.Min(200, err.Length)]}");
                    return indexToItemId;
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);
                foreach (var resp in doc.RootElement.GetProperty("responses").EnumerateArray())
                {
                    var idx = int.Parse(resp.GetProperty("id").GetString()!);
                    var status = resp.GetProperty("status").GetInt32();
                    var respBody = resp.GetProperty("body");
                    if (status == 200 &&
                        respBody.ValueKind == JsonValueKind.Object &&
                        respBody.TryGetProperty("id", out var idEl))
                    {
                        indexToItemId[idx] = idEl.GetString()!;
                    }
                    else
                    {
                        Console.WriteLine($"[GraphBatch] Meta failed index {idx}: status={status}");
                    }
                }
                return indexToItemId;
            }

            Console.WriteLine("[GraphBatch] Batch metadata failed after 3 throttle retries");
            return indexToItemId;
        }

        /// <summary>
        /// Resolves thumbnail download URLs for a list of MyPix items using two batch round-trips:
        /// one to get item IDs, one to get thumbnail redirect URLs.
        /// Calls <paramref name="onChunkReady"/> for each chunk of 20 without waiting for the
        /// download to complete, so the next chunk's batch requests start immediately.
        /// </summary>
        public static async Task GetThumbnailUrlsBatchAsync(
            HttpClient httpClient,
            IList<MyPix> pixList,
            string thumbSize,
            SharedDriveContext? sharedContext,
            Func<Dictionary<string, string?>, int, Task> onChunkReady,
            CancellationToken cancellationToken = default)
        {
            if (pixList.Count == 0) return;

            var pendingChunkTasks = new List<Task>();

            for (int chunkStart = 0; chunkStart < pixList.Count; chunkStart += BatchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var chunk = pixList.Skip(chunkStart).Take(BatchSize).ToList();

                var indexToItemId = await ResolveItemIdsBatchAsync(httpClient, chunk, sharedContext, cancellationToken);
                if (indexToItemId.Count == 0) continue;

                // Batch-fetch thumbnail redirect URLs
                var thumbRequests = indexToItemId.Select(kv =>
                {
                    string url = sharedContext != null
                        ? $"/drives/{sharedContext.DriveId}/items/{kv.Value}/thumbnails/0/{thumbSize}/content"
                        : $"/me/drive/items/{kv.Value}/thumbnails/0/{thumbSize}/content";
                    return new { id = kv.Key.ToString(), method = "GET", url };
                }).ToList();

                var thumbBatchBody = JsonSerializer.Serialize(new { requests = thumbRequests });
                var thumbResponse = await httpClient.PostAsync(BatchUrl,
                    new StringContent(thumbBatchBody, Encoding.UTF8, "application/json"), cancellationToken);

                if (!thumbResponse.IsSuccessStatusCode)
                {
                    var err = await thumbResponse.Content.ReadAsStringAsync(cancellationToken);
                    Console.WriteLine($"[GraphBatch] Batch thumbnail failed (chunk {chunkStart}): {thumbResponse.StatusCode} - {err[..Math.Min(200, err.Length)]}");
                    continue;
                }

                var thumbJson = await thumbResponse.Content.ReadAsStringAsync(cancellationToken);
                using var thumbDoc = JsonDocument.Parse(thumbJson);

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
                        Console.WriteLine($"[GraphBatch] Thumb failed for {pix.FileName}: status={status}");
                        chunkResult[pix.FullFileName] = null;
                    }
                }

                // Fire without awaiting so the next chunk's batch requests start immediately
                pendingChunkTasks.Add(onChunkReady(chunkResult, chunkStart));
            }

            await Task.WhenAll(pendingChunkTasks);
        }
    }
}
