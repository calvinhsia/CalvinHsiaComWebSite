using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WordScapeBlazorWasm.Services;
using System.Text.Json;

namespace TestProject1
{
    /// <summary>
    /// Unit tests for AlbumService
    /// Tests OneDrive album operations via Microsoft Graph API
    /// </summary>
    [TestClass]
    public class TestAlbumService
    {
        private AlbumService _albumService = null!;
        private Mock<HttpMessageHandler> _mockHttpMessageHandler = null!;
        private HttpClient _httpClient = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            _albumService = new AlbumService();
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _httpClient?.Dispose();
        }

        #region FindExistingAlbumAsync Tests

        [TestMethod]
        public async Task FindExistingAlbumAsync_ReturnsAlbumId_WhenAlbumExists()
        {
            // Arrange
            var albumName = "Test Album";
            var expectedBundleId = "bundle-id-123";

            var responseJson = JsonSerializer.Serialize(new
            {
                value = new[]
                {
                    new { id = expectedBundleId, name = albumName },
                    new { id = "bundle-id-456", name = "Other Album" }
                }
            });

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Get &&
                        req.RequestUri!.ToString().Contains("me/drive/bundles")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(responseJson)
                });

            // Act
            var result = await _albumService.FindExistingAlbumAsync(_httpClient, albumName);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(expectedBundleId, result);
        }

        [TestMethod]
        public async Task FindExistingAlbumAsync_ReturnsNull_WhenAlbumDoesNotExist()
        {
            // Arrange
            var albumName = "Nonexistent Album";

            var responseJson = JsonSerializer.Serialize(new
            {
                value = new[]
                {
                    new { id = "bundle-id-123", name = "Other Album" }
                }
            });

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(responseJson)
                });

            // Act
            var result = await _albumService.FindExistingAlbumAsync(_httpClient, albumName);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task FindExistingAlbumAsync_IsCaseInsensitive()
        {
            // Arrange
            var albumName = "test album";
            var expectedBundleId = "bundle-id-123";

            var responseJson = JsonSerializer.Serialize(new
            {
                value = new[]
                {
                    new { id = expectedBundleId, name = "TEST ALBUM" }
                }
            });

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(responseJson)
                });

            // Act
            var result = await _albumService.FindExistingAlbumAsync(_httpClient, albumName);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(expectedBundleId, result);
        }

        [TestMethod]
        public async Task FindExistingAlbumAsync_HandlesApiError()
        {
            // Arrange
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.Unauthorized
                });

            // Act
            var result = await _albumService.FindExistingAlbumAsync(_httpClient, "Test Album");

            // Assert
            Assert.IsNull(result);
        }

        #endregion

        #region CreateNewAlbumAsync Tests

        [TestMethod]
        public async Task CreateNewAlbumAsync_ReturnsAlbumId_OnSuccess()
        {
            // Arrange
            var albumName = "New Album";
            var expectedBundleId = "new-bundle-id-123";

            var responseJson = JsonSerializer.Serialize(new
            {
                id = expectedBundleId,
                name = albumName
            });

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Post &&
                        req.RequestUri!.ToString().Contains("me/drive/bundles")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.Created,
                    Content = new StringContent(responseJson)
                });

            // Act
            var result = await _albumService.CreateNewAlbumAsync(_httpClient, albumName);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(expectedBundleId, result);
        }

        [TestMethod]
        public async Task CreateNewAlbumAsync_ReturnsNull_OnFailure()
        {
            // Arrange
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    Content = new StringContent("Error creating album")
                });

            // Act
            var result = await _albumService.CreateNewAlbumAsync(_httpClient, "Test Album");

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task CreateNewAlbumAsync_SendsCorrectPayload()
        {
            // Arrange
            var albumName = "Test Album";
            HttpRequestMessage? capturedRequest = null;

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.Created,
                    Content = new StringContent(JsonSerializer.Serialize(new { id = "test-id" }))
                });

            // Act
            await _albumService.CreateNewAlbumAsync(_httpClient, albumName);

            // Assert
            Assert.IsNotNull(capturedRequest);
            var content = await capturedRequest!.Content!.ReadAsStringAsync();
            Assert.IsTrue(content.Contains(albumName));
            Assert.IsTrue(content.Contains("album"));
            Assert.IsTrue(content.Contains("rename")); // conflict resolution
        }

        #endregion

        #region GetShareLinkAsync Tests

        [TestMethod]
        public async Task GetShareLinkAsync_ReturnsWebUrl_OnSuccess()
        {
            // Arrange
            var bundleId = "bundle-123";
            var expectedUrl = "https://onedrive.live.com/view/album123";

            var responseJson = JsonSerializer.Serialize(new
            {
                link = new { webUrl = expectedUrl }
            });

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Post &&
                        req.RequestUri!.ToString().Contains($"items/{bundleId}/createLink")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(responseJson)
                });

            // Act
            var result = await _albumService.GetShareLinkAsync(_httpClient, bundleId);

            // Assert
            Assert.AreEqual(expectedUrl, result);
        }

        [TestMethod]
        public async Task GetShareLinkAsync_ReturnsFallbackUrl_OnFailure()
        {
            // Arrange
            var bundleId = "bundle-123";
            var expectedFallbackUrl = $"https://onedrive.live.com/?id={bundleId}";

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.BadRequest
                });

            // Act
            var result = await _albumService.GetShareLinkAsync(_httpClient, bundleId);

            // Assert
            Assert.AreEqual(expectedFallbackUrl, result);
        }

        [TestMethod]
        public async Task GetShareLinkAsync_RequestsAnonymousViewPermission()
        {
            // Arrange
            HttpRequestMessage? capturedRequest = null;

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(new { link = new { webUrl = "test" } }))
                });

            // Act
            await _albumService.GetShareLinkAsync(_httpClient, "test-bundle");

            // Assert
            Assert.IsNotNull(capturedRequest);
            var content = await capturedRequest!.Content!.ReadAsStringAsync();
            Assert.IsTrue(content.Contains("\"type\":\"view\""));
            Assert.IsTrue(content.Contains("\"scope\":\"anonymous\""));
        }

        #endregion

        #region UpdateItemDescriptionAsync Tests

        [TestMethod]
        public async Task UpdateItemDescriptionAsync_SendsPatchRequest()
        {
            // Arrange
            var itemId = "item-123";
            var description = "Test description";
            HttpRequestMessage? capturedRequest = null;

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK
                });

            // Act
            await _albumService.UpdateItemDescriptionAsync(_httpClient, itemId, description);

            // Assert
            Assert.IsNotNull(capturedRequest);
            Assert.AreEqual(new HttpMethod("PATCH"), capturedRequest!.Method);
            Assert.IsTrue(capturedRequest.RequestUri!.ToString().Contains(itemId));

            var content = await capturedRequest.Content!.ReadAsStringAsync();
            Assert.IsTrue(content.Contains(description));
        }

        [TestMethod]
        public async Task UpdateItemDescriptionAsync_DoesNotThrow_OnFailure()
        {
            // Arrange
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.BadRequest
                });

            // Act - should not throw
            await _albumService.UpdateItemDescriptionAsync(_httpClient, "item-123", "description");

            // Assert - if we got here, no exception was thrown
            Assert.IsTrue(true);
        }

        [TestMethod]
        public async Task UpdateItemDescriptionAsync_SupportsCancellation()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel();

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new TaskCanceledException());

            // Act & Assert
            await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () =>
            {
                await _albumService.UpdateItemDescriptionAsync(_httpClient, "item-123", "description", cts.Token);
            });
        }

        #endregion

        #region GetFileMetadataAsync Tests

        [TestMethod]
        public async Task GetFileMetadataAsync_ReturnsMetadata_OnSuccess()
        {
            // Arrange
            var fileName = "test-file.jpg";
            var fileMetadata = new
            {
                id = "file-123",
                name = fileName,
                size = 12345
            };

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Get &&
                        req.RequestUri!.ToString().Contains(fileName)),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(fileMetadata))
                });

            // Act
            var result = await _albumService.GetFileMetadataAsync(_httpClient, fileName);

            // Assert
            Assert.IsTrue(result.HasValue, "File metadata should be returned");
            Assert.IsTrue(result.Value.TryGetProperty("id", out var idProp), "Metadata should contain 'id' property");
            Assert.AreEqual("file-123", idProp.GetString());
        }

        [TestMethod]
        public async Task GetFileMetadataAsync_ReturnsNull_WhenFileNotFound()
        {
            // Arrange
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.NotFound
                });

            // Act
            var result = await _albumService.GetFileMetadataAsync(_httpClient, "nonexistent.jpg");

            // Assert
            Assert.IsNull(result);
        }

        #endregion

        #region AddFileToAlbumAsync Tests

        [TestMethod]
        public async Task AddFileToAlbumAsync_ReturnsSuccess_WhenFileAdded()
        {
            // Arrange
            var bundleId = "bundle-123";
            var fileId = "file-456";

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Post &&
                        req.RequestUri!.ToString().Contains($"bundles/{bundleId}/children")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK
                });

            // Act
            var (success, errorMessage) = await _albumService.AddFileToAlbumAsync(_httpClient, bundleId, fileId);

            // Assert
            Assert.IsTrue(success);
            Assert.IsNull(errorMessage);
        }

        [TestMethod]
        public async Task AddFileToAlbumAsync_DetectsAlreadyExists_OnConflict()
        {
            // Arrange
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.Conflict,
                    Content = new StringContent("itemAlreadyExists")
                });

            // Act
            var (success, errorMessage) = await _albumService.AddFileToAlbumAsync(_httpClient, "bundle", "file");

            // Assert
            Assert.IsFalse(success);
            Assert.AreEqual("already_exists", errorMessage);
        }

        [TestMethod]
        public async Task AddFileToAlbumAsync_SendsCorrectPayload()
        {
            // Arrange
            var fileId = "file-123";
            HttpRequestMessage? capturedRequest = null;

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK
                });

            // Act
            await _albumService.AddFileToAlbumAsync(_httpClient, "bundle", fileId);

            // Assert
            Assert.IsNotNull(capturedRequest);
            var content = await capturedRequest!.Content!.ReadAsStringAsync();
            Assert.IsTrue(content.Contains($"\"id\":\"{fileId}\""));
        }

        [TestMethod]
        public async Task AddFileToAlbumAsync_SupportsCancellation()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            cts.Cancel();

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new OperationCanceledException());

            // Act
            var (success, errorMessage) = await _albumService.AddFileToAlbumAsync(_httpClient, "bundle", "file", cts.Token);

            // Assert
            Assert.IsFalse(success);
            Assert.IsNotNull(errorMessage);
        }

        #endregion

        #region Integration Scenario Tests

        [TestMethod]
        public async Task AlbumWorkflow_CreateNewAlbum_GetShareLink()
        {
            // Arrange
            var albumName = "Vacation 2024";
            var bundleId = "bundle-vacation-2024";
            var shareUrl = "https://onedrive.live.com/vacation2024";

            // Mock album creation
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Post &&
                        req.RequestUri!.ToString().Contains("bundles") &&
                        !req.RequestUri.ToString().Contains("createLink")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.Created,
                    Content = new StringContent(JsonSerializer.Serialize(new { id = bundleId }))
                });

            // Mock share link creation
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Post &&
                        req.RequestUri!.ToString().Contains("createLink")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(new { link = new { webUrl = shareUrl } }))
                });

            // Act
            var createdBundleId = await _albumService.CreateNewAlbumAsync(_httpClient, albumName);
            var link = await _albumService.GetShareLinkAsync(_httpClient, createdBundleId!);

            // Assert
            Assert.AreEqual(bundleId, createdBundleId);
            Assert.AreEqual(shareUrl, link);
        }

        [TestMethod]
        public async Task AlbumWorkflow_FindOrCreateAlbum()
        {
            // Arrange
            var albumName = "Test Album";
            var existingBundleId = "existing-bundle-123";

            // First call - album exists
            _mockHttpMessageHandler.Protected()
                .SetupSequence<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(new
                    {
                        value = new[] { new { id = existingBundleId, name = albumName } }
                    }))
                });

            // Act
            var bundleId = await _albumService.FindExistingAlbumAsync(_httpClient, albumName);

            // Assert
            Assert.AreEqual(existingBundleId, bundleId);
        }

        #endregion
    }
}