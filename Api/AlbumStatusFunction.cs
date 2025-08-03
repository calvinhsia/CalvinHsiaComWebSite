using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Collections.Concurrent;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace Api
{
    public class AlbumStatusFunction
    {
        // In-memory storage for album status (use Redis/database in production)
        private static readonly ConcurrentDictionary<string, AlbumStatus> _albumStatuses = new();

        [Function("GetAlbumStatus")]
        public async Task<HttpResponseData> GetAlbumStatus(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            response.Headers.Add("Access-Control-Allow-Origin", "*");

            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var albumId = query["albumId"];

            if (string.IsNullOrEmpty(albumId))
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("albumId parameter is required");
                return response;
            }

            if (_albumStatuses.TryGetValue(albumId, out var status))
            {
                var json = JsonConvert.SerializeObject(status);
                await response.WriteStringAsync(json);
            }
            else
            {
                var notFoundStatus = new AlbumStatus 
                { 
                    AlbumId = albumId, 
                    Status = "not_found",
                    Message = "Album status not found"
                };
                var json = JsonConvert.SerializeObject(notFoundStatus);
                await response.WriteStringAsync(json);
            }

            return response;
        }

        public static void UpdateAlbumStatus(string albumId, string status, string message = "", string shareLink = "", int totalItems = 0, int completedItems = 0)
        {
            _albumStatuses.AddOrUpdate(albumId, 
                new AlbumStatus 
                { 
                    AlbumId = albumId, 
                    Status = status, 
                    Message = message,
                    ShareLink = shareLink,
                    TotalItems = totalItems,
                    CompletedItems = completedItems,
                    LastUpdated = DateTime.UtcNow 
                },
                (key, existing) => 
                {
                    existing.Status = status;
                    existing.Message = message;
                    existing.ShareLink = shareLink;
                    existing.TotalItems = totalItems;
                    existing.CompletedItems = completedItems;
                    existing.LastUpdated = DateTime.UtcNow;
                    return existing;
                });
        }

        // Clean up old statuses (call periodically)
        public static void CleanupOldStatuses(TimeSpan maxAge)
        {
            var cutoff = DateTime.UtcNow - maxAge;
            var keysToRemove = _albumStatuses
                .Where(kvp => kvp.Value.LastUpdated < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                _albumStatuses.TryRemove(key, out _);
            }
        }
    }

    public class AlbumStatus
    {
        public string AlbumId { get; set; } = "";
        public string Status { get; set; } = ""; // "creating", "completed", "failed"
        public string Message { get; set; } = "";
        public string ShareLink { get; set; } = "";
        public DateTime LastUpdated { get; set; }
        public int TotalItems { get; set; }
        public int CompletedItems { get; set; }
    }
}
