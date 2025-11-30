using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WordScapeBlazorWasm.Services
{
    /// <summary>
    /// Service for managing OneDrive album operations via Microsoft Graph API
    /// </summary>
    public class AlbumService
    {
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
        /// Gets file metadata from OneDrive
        /// </summary>
        public async Task<JsonElement?> GetFileMetadataAsync(HttpClient httpClient, string fullFileName, CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await httpClient.GetAsync($"{MSGraphEndPoint}me/drive/root:/{fullFileName}", cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(cancellationToken);
                    using var doc = JsonDocument.Parse(json);
                    return doc.RootElement.Clone();
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AlbumService] Error getting file metadata for {fullFileName}: {ex.Message}");
                return null;
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
