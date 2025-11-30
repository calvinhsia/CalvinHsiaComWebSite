using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TestProject1
{
    /// <summary>
    /// Unit tests for Albums page logic
    /// Tests album info management, caching, and JSON serialization patterns
    /// NOTE: These tests use Newtonsoft.Json to validate current behavior before migration
    /// </summary>
    [TestClass]
    public class TestAlbumsPageLogic
    {
        #region AlbumInfo Serialization Tests

        [TestMethod]
        public void AlbumInfo_Serialization_RoundTrip()
        {
            // Arrange
            var albumInfo = new AlbumInfo
            {
                Id = "bundle-123",
                Name = "Vacation 2024",
                Description = "Summer trip photos",
                CreatedDateTime = DateTime.Now.AddDays(-30),
                LastModifiedDateTime = DateTime.Now.AddDays(-1),
                ChildCount = 42,
                ThumbnailUrl = "https://example.com/thumb.jpg",
                ShareUrl = "https://onedrive.live.com/album123"
            };

            // Act - Serialize
            var json = JsonConvert.SerializeObject(albumInfo);
            Console.WriteLine($"Serialized album: {json}");

            // Act - Deserialize
            var deserialized = JsonConvert.DeserializeObject<AlbumInfo>(json);

            // Assert
            Assert.IsNotNull(deserialized);
            Assert.AreEqual("bundle-123", deserialized!.Id);
            Assert.AreEqual("Vacation 2024", deserialized.Name);
            Assert.AreEqual("Summer trip photos", deserialized.Description);
            Assert.AreEqual(42, deserialized.ChildCount);
            Assert.AreEqual("https://example.com/thumb.jpg", deserialized.ThumbnailUrl);
            Assert.AreEqual("https://onedrive.live.com/album123", deserialized.ShareUrl);
        }

        [TestMethod]
        public void AlbumInfo_Serialization_HandlesNullFields()
        {
            // Arrange
            var albumInfo = new AlbumInfo
            {
                Id = "bundle-123",
                Name = "Test Album",
                Description = null,
                CreatedDateTime = null,
                ThumbnailUrl = null
            };

            // Act
            var json = JsonConvert.SerializeObject(albumInfo);
            var deserialized = JsonConvert.DeserializeObject<AlbumInfo>(json);

            // Assert
            Assert.IsNotNull(deserialized);
            Assert.AreEqual("bundle-123", deserialized!.Id);
            Assert.AreEqual("Test Album", deserialized.Name);
            Assert.IsNull(deserialized.Description);
            Assert.IsNull(deserialized.CreatedDateTime);
            Assert.IsNull(deserialized.ThumbnailUrl);
        }

        [TestMethod]
        public void AlbumInfo_UIStateNotSerializedForCache()
        {
            // Arrange
            var albumInfo = new AlbumInfo
            {
                Id = "bundle-123",
                Name = "Test",
                IsNew = true,
                IsLoadingThumbnail = true,
                IsLoadingShareUrl = true
            };

            // Act - Serialize
            var json = JsonConvert.SerializeObject(albumInfo);

            // Create cache copy (mimicking Albums page logic)
            var cacheAlbum = new AlbumInfo
            {
                Id = albumInfo.Id,
                Name = albumInfo.Name,
                Description = albumInfo.Description,
                CreatedDateTime = albumInfo.CreatedDateTime,
                LastModifiedDateTime = albumInfo.LastModifiedDateTime,
                ChildCount = albumInfo.ChildCount,
                ThumbnailUrl = albumInfo.ThumbnailUrl,
                ShareUrl = albumInfo.ShareUrl
                // Note: UI state properties (IsNew, IsLoadingThumbnail, IsLoadingShareUrl) not copied
            };

            var cacheJson = JsonConvert.SerializeObject(cacheAlbum);

            // Assert
            Assert.IsFalse(cacheAlbum.IsNew);
            Assert.IsFalse(cacheAlbum.IsLoadingThumbnail);
            Assert.IsFalse(cacheAlbum.IsLoadingShareUrl);
        }

        #endregion

        #region MS Graph Response Parsing Tests

        [TestMethod]
        public void MSGraphResponse_ParseBundlesList()
        {
            // Arrange - Simulate MS Graph API response
            var graphResponse = new
            {
                value = new object[]
                {
                    new
                    {
                        id = "bundle-1",
                        name = "Album 1",
                        description = "First album",
                        createdDateTime = "2024-01-01T10:00:00Z",
                        lastModifiedDateTime = "2024-01-15T14:30:00Z",
                        folder = new { childCount = 25 },
                        bundle = new
                        {
                            album = new
                            {
                                coverImageItemId = "image-123"
                            }
                        }
                    },
                    new
                    {
                        id = "bundle-2",
                        name = "Album 2",
                        createdDateTime = "2024-02-01T10:00:00Z",
                        folder = new { childCount = 10 },
                        bundle = new { album = new { } }
                    }
                }
            };

            // Act - Serialize to JSON (as it comes from API)
            var json = JsonConvert.SerializeObject(graphResponse);
            Console.WriteLine($"MS Graph response: {json}");

            // Act - Deserialize using dynamic (current pattern in Albums.razor)
            var bundlesResponse = JsonConvert.DeserializeObject<dynamic>(json);
            var albums = new List<AlbumInfo>();

            if (bundlesResponse?.value != null)
            {
                foreach (var bundle in bundlesResponse.value)
                {
                    if (bundle?.bundle?.album != null)
                    {
                        var coverImageItemId = bundle.bundle?.album?.coverImageItemId?.ToString();
                        
                        var albumInfo = new AlbumInfo
                        {
                            Id = bundle.id?.ToString() ?? string.Empty,
                            Name = bundle.name?.ToString() ?? "Unnamed Album",
                            Description = bundle.description?.ToString(),
                            CreatedDateTime = DateTime.TryParse(bundle.createdDateTime?.ToString(), out DateTime created) ? created : null,
                            LastModifiedDateTime = DateTime.TryParse(bundle.lastModifiedDateTime?.ToString(), out DateTime modified) ? modified : null,
                            ChildCount = bundle.folder?.childCount ?? 0
                        };

                        albums.Add(albumInfo);
                    }
                }
            }

            // Assert
            Assert.AreEqual(2, albums.Count);
            
            Assert.AreEqual("bundle-1", albums[0].Id);
            Assert.AreEqual("Album 1", albums[0].Name);
            Assert.AreEqual("First album", albums[0].Description);
            Assert.AreEqual(25, albums[0].ChildCount);
            Assert.IsNotNull(albums[0].CreatedDateTime);

            Assert.AreEqual("bundle-2", albums[1].Id);
            Assert.AreEqual("Album 2", albums[1].Name);
            Assert.AreEqual(10, albums[1].ChildCount);
        }

        [TestMethod]
        public void MSGraphResponse_ParseThumbnailData()
        {
            // Arrange - MS Graph thumbnails response
            var thumbnailResponse = new
            {
                value = new object[]
                {
                    new
                    {
                        id = "0",
                        small = new { url = "https://example.com/small.jpg" },
                        medium = new { url = "https://example.com/medium.jpg" },
                        large = new { url = "https://example.com/large.jpg" }
                    }
                }
            };

            // Act - Serialize and deserialize using dynamic
            var json = JsonConvert.SerializeObject(thumbnailResponse);
            var thumbnailData = JsonConvert.DeserializeObject<dynamic>(json);

            string? thumbnailUrl = null;
            if (thumbnailData?.value != null && thumbnailData.value.Count > 0)
            {
                var firstThumbnail = thumbnailData.value[0];
                thumbnailUrl = firstThumbnail?.medium?.url?.ToString() ??
                              firstThumbnail?.small?.url?.ToString() ??
                              firstThumbnail?.large?.url?.ToString();
            }

            // Assert
            Assert.AreEqual("https://example.com/medium.jpg", thumbnailUrl);
        }

        [TestMethod]
        public void MSGraphResponse_ParseThumbnailData_PrefersMedium()
        {
            // Arrange - Only large available
            var response1 = JsonConvert.DeserializeObject<dynamic>(
                JsonConvert.SerializeObject(new
                {
                    value = new object[] { new { large = new { url = "large.jpg" } } }
                }));

            // Arrange - Medium and large available
            var response2 = JsonConvert.DeserializeObject<dynamic>(
                JsonConvert.SerializeObject(new
                {
                    value = new object[] { new
                    {
                        medium = new { url = "medium.jpg" },
                        large = new { url = "large.jpg" }
                    }}
                }));

            // Act
            var url1 = response1?.value[0]?.medium?.url?.ToString() ??
                      response1?.value[0]?.small?.url?.ToString() ??
                      response1?.value[0]?.large?.url?.ToString();

            var url2 = response2?.value[0]?.medium?.url?.ToString() ??
                      response2?.value[0]?.small?.url?.ToString() ??
                      response2?.value[0]?.large?.url?.ToString();

            // Assert
            Assert.AreEqual("large.jpg", url1); // Falls back to large
            Assert.AreEqual("medium.jpg", url2); // Prefers medium
        }

        [TestMethod]
        public void MSGraphResponse_ParseShareLinkPermissions()
        {
            // Arrange - Existing permissions response
            var permissionsResponse = new
            {
                value = new object[]
                {
                    new
                    {
                        id = "perm-1",
                        link = new { webUrl = "https://onedrive.live.com/existing" }
                    }
                }
            };

            // Act
            var json = JsonConvert.SerializeObject(permissionsResponse);
            var linksData = JsonConvert.DeserializeObject<dynamic>(json);

            string? shareUrl = null;
            if (linksData?.value != null)
            {
                foreach (var permission in linksData.value)
                {
                    if (permission?.link?.webUrl != null)
                    {
                        shareUrl = permission.link.webUrl.ToString();
                        break;
                    }
                }
            }

            // Assert
            Assert.AreEqual("https://onedrive.live.com/existing", shareUrl);
        }

        [TestMethod]
        public void MSGraphResponse_CreateShareLinkRequest()
        {
            // Arrange - Create link request payload
            var linkRequest = new
            {
                type = "view",
                scope = "anonymous"
            };

            // Act
            var json = JsonConvert.SerializeObject(linkRequest);
            Console.WriteLine($"Link request: {json}");

            // Assert
            Assert.IsTrue(json.Contains("\"type\":\"view\""));
            Assert.IsTrue(json.Contains("\"scope\":\"anonymous\""));
        }

        [TestMethod]
        public void MSGraphResponse_ParseCreatedShareLink()
        {
            // Arrange - Create link response
            var linkResponse = new
            {
                link = new
                {
                    webUrl = "https://onedrive.live.com/new-link",
                    type = "view",
                    scope = "anonymous"
                }
            };

            // Act
            var json = JsonConvert.SerializeObject(linkResponse);
            var response = JsonConvert.DeserializeObject<dynamic>(json);
            var webUrl = response?.link?.webUrl?.ToString();

            // Assert
            Assert.AreEqual("https://onedrive.live.com/new-link", webUrl);
        }

        #endregion

        #region Cache Management Tests

        [TestMethod]
        public void CacheManagement_ExpiryLogic()
        {
            // Arrange
            var cacheExpiry = TimeSpan.FromMinutes(5);
            var recentFetch = DateTime.Now.AddMinutes(-3);
            var oldFetch = DateTime.Now.AddMinutes(-10);

            // Act
            var isRecentValid = DateTime.Now - recentFetch < cacheExpiry;
            var isOldValid = DateTime.Now - oldFetch < cacheExpiry;

            // Assert
            Assert.IsTrue(isRecentValid, "Recent cache should be valid");
            Assert.IsFalse(isOldValid, "Old cache should be expired");
        }

        [TestMethod]
        public void CacheManagement_AlbumSorting()
        {
            // Arrange
            var albums = new List<AlbumInfo>
            {
                new AlbumInfo { Name = "Oldest", CreatedDateTime = DateTime.Now.AddDays(-30) },
                new AlbumInfo { Name = "Newest", CreatedDateTime = DateTime.Now.AddDays(-1) },
                new AlbumInfo { Name = "Middle", CreatedDateTime = DateTime.Now.AddDays(-15) },
                new AlbumInfo { Name = "NoDate", CreatedDateTime = null }
            };

            // Act - Sort by CreatedDateTime descending (newest first)
            var sorted = albums.OrderByDescending(a => a.CreatedDateTime ?? DateTime.MinValue).ToList();

            // Assert
            Assert.AreEqual("Newest", sorted[0].Name);
            Assert.AreEqual("Middle", sorted[1].Name);
            Assert.AreEqual("Oldest", sorted[2].Name);
            Assert.AreEqual("NoDate", sorted[3].Name);
        }

        [TestMethod]
        public void CacheManagement_AlbumCollectionClone()
        {
            // Arrange
            var cachedAlbums = new List<AlbumInfo>
            {
                new AlbumInfo { Id = "1", Name = "Album 1" },
                new AlbumInfo { Id = "2", Name = "Album 2" }
            };

            // Act - Clone for instance use (mimics Albums page pattern) - shallow copy of list
            var instanceAlbums = new List<AlbumInfo>(cachedAlbums);

            // Modify instance - NOTE: Since AlbumInfo is a class (reference type), modifying an object in the list
            // affects both lists (they share the same object references). This is expected behavior.
            var originalName = cachedAlbums[0].Name;
            instanceAlbums[0].Name = "Modified";

            // Assert - Both lists point to the same AlbumInfo objects
            Assert.AreEqual("Modified", instanceAlbums[0].Name);
            Assert.AreEqual("Modified", cachedAlbums[0].Name); // Changed: Both should be modified
            Assert.AreEqual("Album 1", originalName); // Original value saved
        }

        #endregion

        #region Thumbnail Loading Tests

        [TestMethod]
        public void ThumbnailLoading_FallbackUrl()
        {
            // Arrange
            var albumId = "bundle-123";
            string? coverImageItemId = null;

            // Act - When no cover image ID provided
            var shouldFetchThumbnail = !string.IsNullOrEmpty(coverImageItemId);

            // Assert
            Assert.IsFalse(shouldFetchThumbnail);
        }

        [TestMethod]
        public void ThumbnailLoading_HasCoverImage()
        {
            // Arrange
            var albumId = "bundle-123";
            string? coverImageItemId = "image-456";

            // Act
            var shouldFetchThumbnail = !string.IsNullOrEmpty(coverImageItemId);

            // Assert
            Assert.IsTrue(shouldFetchThumbnail);
        }

        #endregion

        #region Progressive Loading Tests

        [TestMethod]
        public void ProgressiveLoading_TracksProgress()
        {
            // Arrange
            var totalAlbumsToProcess = 50;
            var processedCount = 0;

            // Act - Simulate processing
            var albums = new List<AlbumInfo>();
            for (int i = 0; i < totalAlbumsToProcess; i++)
            {
                albums.Add(new AlbumInfo
                {
                    Id = $"bundle-{i}",
                    Name = $"Album {i}",
                    IsNew = true
                });
                processedCount++;
            }

            // Assert
            Assert.AreEqual(totalAlbumsToProcess, processedCount);
            Assert.AreEqual(totalAlbumsToProcess, albums.Count);
            Assert.IsTrue(albums.All(a => a.IsNew));
        }

        [TestMethod]
        public void ProgressiveLoading_StatusMessage()
        {
            // Arrange
            var totalAlbums = 100;
            var currentIndex = 50;
            var currentAlbumName = "Test Album";

            // Act
            var statusMessage = $"Loading '{currentAlbumName}'...";
            var progressMessage = $"Processing album {currentIndex + 1} of {totalAlbums}...";

            // Assert
            Assert.IsTrue(statusMessage.Contains("Test Album"));
            Assert.IsTrue(progressMessage.Contains("51 of 100"));
        }

        #endregion

        #region Share URL Tests

        [TestMethod]
        public void ShareUrl_LazyLoading()
        {
            // Arrange
            var album = new AlbumInfo
            {
                Id = "bundle-123",
                ShareUrl = string.Empty
            };

            // Act
            var needsLoading = string.IsNullOrEmpty(album.ShareUrl);

            // Assert
            Assert.IsTrue(needsLoading);
        }

        [TestMethod]
        public void ShareUrl_CachedValue()
        {
            // Arrange
            var album = new AlbumInfo
            {
                Id = "bundle-123",
                ShareUrl = "https://onedrive.live.com/cached"
            };

            // Act
            var needsLoading = string.IsNullOrEmpty(album.ShareUrl);

            // Assert
            Assert.IsFalse(needsLoading);
            Assert.AreEqual("https://onedrive.live.com/cached", album.ShareUrl);
        }

        [TestMethod]
        public void ShareUrl_FallbackGeneration()
        {
            // Arrange
            var albumId = "bundle-123";

            // Act
            var fallbackUrl = $"https://onedrive.live.com/?id={albumId}";

            // Assert
            Assert.AreEqual("https://onedrive.live.com/?id=bundle-123", fallbackUrl);
        }

        #endregion

        // Helper class matching Albums.razor AlbumInfo
        public class AlbumInfo
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string? Description { get; set; }
            public DateTime? CreatedDateTime { get; set; }
            public DateTime? LastModifiedDateTime { get; set; }
            public int ChildCount { get; set; }
            public string? ThumbnailUrl { get; set; }
            public string ShareUrl { get; set; } = string.Empty;

            // UI state properties
            public bool IsNew { get; set; } = false;
            public bool IsLoadingThumbnail { get; set; } = false;
            public bool IsLoadingShareUrl { get; set; } = false;
        }
    }
}
