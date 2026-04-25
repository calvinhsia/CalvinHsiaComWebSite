using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Client.Shared;

namespace WordScapeBlazorWasm.Services
{
    /// <summary>
    /// Holds remote drive context for accessing a shared OneDrive folder.
    /// </summary>
    public record SharedDriveContext(string DriveId, string RootItemId);

    /// <summary>
    /// Service for managing OneDrive album operations via Microsoft Graph API
    /// </summary>
    public class AlbumService
    {
        private const string SharedFolderName = "OldPictures";

        /// <summary>
        /// When non-null, all file access is routed through this shared drive context.
        /// </summary>
        public SharedDriveContext? SharedContext { get; private set; }

        /// <summary>
        /// Call once after authentication to set up the shared context when the signed-in user
        /// is not the owner. Searches sharedWithMe for a folder named "OldPictures".
        /// Returns an error message if the folder is not found, or null on success.
        /// </summary>
        public async Task<string?> InitializeSharedContextAsync(HttpClient httpClient)
        {
            SharedContext = null;
            try
            {
                var response = await httpClient.GetAsync($"{MSGraphEndPoint}me/drive/sharedWithMe");
                if (!response.IsSuccessStatusCode)
                {
                    return $"Could not access sharedWithMe: {response.StatusCode}";
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("value", out var valueArray))
                    return $"Shared folder '{SharedFolderName}' not found (no value array).";

                foreach (var item in valueArray.EnumerateArray())
                {
                    if (item.TryGetProperty("name", out var nameEl) &&
                        nameEl.GetString() == SharedFolderName &&
                        item.TryGetProperty("remoteItem", out var remoteItem))
                    {
                        var remoteItemId = remoteItem.GetProperty("id").GetString()!;
                        var remoteDriveId = remoteItem.GetProperty("parentReference").GetProperty("driveId").GetString()!;
                        SharedContext = new SharedDriveContext(remoteDriveId, remoteItemId);
                        Console.WriteLine($"[AlbumService] Shared context initialized: driveId={remoteDriveId} itemId={remoteItemId}");
                        return null; // success
                    }
                }

                return $"Shared folder '{SharedFolderName}' not found in sharedWithMe.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AlbumService] Error initializing shared context: {ex.Message}");
                return $"Error accessing shared folder: {ex.Message}";
            }
        }
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
