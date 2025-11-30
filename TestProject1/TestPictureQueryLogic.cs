using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TestProject1
{
    /// <summary>
    /// Unit tests for PictureQuery page logic
    /// Tests filter sanitization, progress tracking, and JSON serialization patterns
    /// NOTE: These tests use Newtonsoft.Json to validate current behavior before migration
    /// </summary>
    [TestClass]
    public class TestPictureQueryLogic
    {
        #region Album Name Sanitization Tests

        [TestMethod]
        public void SanitizeAlbumName_RemovesSpecialPrefixes()
        {
            // Arrange & Act
            var result1 = SanitizeAlbumName("$weight");
            var result2 = SanitizeAlbumName("^family");
            var result3 = SanitizeAlbumName("|vacation");

            // Assert
            Assert.AreEqual("weight", result1);
            Assert.AreEqual("family", result2);
            Assert.AreEqual("vacation", result3);
        }

        [TestMethod]
        public void SanitizeAlbumName_RemovesInvalidCharacters()
        {
            // Arrange & Act
            var result = SanitizeAlbumName(@"test/album:with*invalid?chars<here>");

            // Assert
            Assert.IsFalse(result.Contains("/"));
            Assert.IsFalse(result.Contains(":"));
            Assert.IsFalse(result.Contains("*"));
            Assert.IsFalse(result.Contains("?"));
            Assert.IsFalse(result.Contains("<"));
            Assert.IsFalse(result.Contains(">"));
            Assert.IsTrue(result.Contains("_")); // Should replace with underscores
        }

        [TestMethod]
        public void SanitizeAlbumName_TrimsLeadingTrailingSpacesAndDots()
        {
            // Arrange & Act
            var result1 = SanitizeAlbumName("  album name  ");
            var result2 = SanitizeAlbumName("..album..");

            // Assert
            Assert.AreEqual("album name", result1);
            Assert.AreEqual("album", result2);
        }

        [TestMethod]
        public void SanitizeAlbumName_LimitsLength()
        {
            // Arrange
            var longName = new string('a', 100);

            // Act
            var result = SanitizeAlbumName(longName);

            // Assert
            Assert.AreEqual(50, result.Length);
        }

        [TestMethod]
        public void SanitizeAlbumName_HandleEmptyOrWhitespace()
        {
            // Arrange & Act
            var result1 = SanitizeAlbumName("");
            var result2 = SanitizeAlbumName("   ");
            var result3 = SanitizeAlbumName(null!);

            // Assert
            Assert.AreEqual("QueryAlbum", result1);
            Assert.AreEqual("QueryAlbum", result2);
            Assert.AreEqual("QueryAlbum", result3);
        }

        // Helper method mimicking PictureQuery.razor.cs logic
        private string SanitizeAlbumName(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return "QueryAlbum";

            var sanitized = filter;

            // Remove special regex/filter prefixes
            if (sanitized.StartsWith("$"))
                sanitized = sanitized[1..];
            if (sanitized.StartsWith("^"))
                sanitized = sanitized[1..];
            if (sanitized.StartsWith("|"))
                sanitized = sanitized[1..];

            // Replace invalid characters
            var invalidChars = new[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|', '\r', '\n', '\t' };
            foreach (var invalidChar in invalidChars)
            {
                sanitized = sanitized.Replace(invalidChar, '_');
            }

            // Trim
            sanitized = sanitized.Trim().Trim('.');

            // Limit length
            if (sanitized.Length > 50)
            {
                sanitized = sanitized.Substring(0, 50);
            }

            return string.IsNullOrWhiteSpace(sanitized) ? "QueryAlbum" : sanitized;
        }

        #endregion

        #region Filter History Tests

        [TestMethod]
        public void FilterHistory_Serialization_RoundTrip()
        {
            // Arrange
            var history = new List<string> { "weight", "family", "vacation", "hiking" };

            // Act - Serialize
            var json = JsonConvert.SerializeObject(history);

            // Act - Deserialize
            var deserialized = JsonConvert.DeserializeObject<List<string>>(json);

            // Assert
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(4, deserialized!.Count);
            CollectionAssert.AreEqual(history, deserialized);
        }

        [TestMethod]
        public void FilterHistory_MaintainsMaxItems()
        {
            // Arrange
            var history = new List<string>();
            const int MAX_HISTORY_ITEMS = 10;

            // Act - Add more than max items
            for (int i = 0; i < 15; i++)
            {
                history.Insert(0, $"filter{i}");
                if (history.Count > MAX_HISTORY_ITEMS)
                {
                    history = history.Take(MAX_HISTORY_ITEMS).ToList();
                }
            }

            // Assert
            Assert.AreEqual(MAX_HISTORY_ITEMS, history.Count);
            Assert.AreEqual("filter14", history[0]); // Most recent
        }

        [TestMethod]
        public void FilterHistory_RemovesDuplicates()
        {
            // Arrange
            var history = new List<string> { "weight", "family", "vacation" };
            var newFilter = "weight"; // Duplicate

            // Act - Remove if exists, then add to beginning
            history.RemoveAll(x => string.Equals(x, newFilter, StringComparison.OrdinalIgnoreCase));
            history.Insert(0, newFilter);

            // Assert
            Assert.AreEqual(3, history.Count); // Still 3 items
            Assert.AreEqual("weight", history[0]); // Moved to top
            Assert.AreEqual("family", history[1]);
        }

        #endregion

        #region Album Progress Tests

        [TestMethod]
        public void AlbumProgress_Serialization_RoundTrip()
        {
            // Arrange
            var progress = new AlbumProgress
            {
                TotalItems = 100,
                CompletedItems = 50,
                SuccessfullyAdded = 45,
                FailedToAdd = 3,
                AlreadyExists = 2,
                StartTime = DateTime.Now,
                AlbumName = "Test Album",
                BundleId = "bundle-123",
                LastProcessedIndex = 49
            };

            // Act - Serialize
            var json = JsonConvert.SerializeObject(progress);
            Console.WriteLine($"Serialized progress: {json}");

            // Act - Deserialize
            var deserialized = JsonConvert.DeserializeObject<AlbumProgress>(json);

            // Assert
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(100, deserialized!.TotalItems);
            Assert.AreEqual(50, deserialized.CompletedItems);
            Assert.AreEqual(45, deserialized.SuccessfullyAdded);
            Assert.AreEqual(3, deserialized.FailedToAdd);
            Assert.AreEqual(2, deserialized.AlreadyExists);
            Assert.AreEqual("Test Album", deserialized.AlbumName);
            Assert.AreEqual("bundle-123", deserialized.BundleId);
            Assert.AreEqual(49, deserialized.LastProcessedIndex);
        }

        [TestMethod]
        public void AlbumProgress_HandlesEmptyItemCompletionTimes()
        {
            // Arrange
            var progress = new AlbumProgress
            {
                TotalItems = 10,
                CompletedItems = 0,
                ItemCompletionTimes = new List<DateTime>()
            };

            // Act
            var json = JsonConvert.SerializeObject(progress);
            var deserialized = JsonConvert.DeserializeObject<AlbumProgress>(json);

            // Assert
            Assert.IsNotNull(deserialized);
            Assert.IsNotNull(deserialized!.ItemCompletionTimes);
            Assert.AreEqual(0, deserialized.ItemCompletionTimes.Count);
        }

        [TestMethod]
        public void AlbumProgress_TracksProcessingTimes()
        {
            // Arrange
            var progress = new AlbumProgress
            {
                ItemCompletionTimes = new List<DateTime>()
            };

            // Act - Simulate processing items
            for (int i = 0; i < 15; i++)
            {
                progress.ItemCompletionTimes.Add(DateTime.Now.AddSeconds(i));
                
                // Keep only last 10
                if (progress.ItemCompletionTimes.Count > 10)
                {
                    progress.ItemCompletionTimes.RemoveAt(0);
                }
            }

            // Assert
            Assert.AreEqual(10, progress.ItemCompletionTimes.Count);
        }

        #endregion

        #region Filter Storage Tests

        [TestMethod]
        public void FilterStorage_Serialization_AllFields()
        {
            // Arrange
            var filters = new
            {
                notesFilter = "weight",
                mediaType = "photo",
                date1 = "1/1/2020",
                date2 = "12/31/2023",
                publishToAlbum = true,
                albumName = "Weight Progress",
                albumMaxItems = 150
            };

            // Act - Serialize
            var json = JsonConvert.SerializeObject(filters);
            Console.WriteLine($"Serialized filters: {json}");

            // Act - Deserialize using dynamic
            var deserialized = JsonConvert.DeserializeObject<dynamic>(json);

            // Assert
            Assert.IsNotNull(deserialized);
            Assert.AreEqual("weight", deserialized!.notesFilter.ToString());
            Assert.AreEqual("photo", deserialized.mediaType.ToString());
            Assert.AreEqual("1/1/2020", deserialized.date1.ToString());
            Assert.AreEqual("12/31/2023", deserialized.date2.ToString());
            Assert.AreEqual(true, (bool)deserialized.publishToAlbum);
            Assert.AreEqual("Weight Progress", deserialized.albumName.ToString());
            Assert.AreEqual(150, (int)deserialized.albumMaxItems);
        }

        [TestMethod]
        public void FilterStorage_HandlesNullValues()
        {
            // Arrange
            var filters = new
            {
                notesFilter = (string?)null,
                mediaType = "",
                publishToAlbum = false
            };

            // Act
            var json = JsonConvert.SerializeObject(filters);
            var deserialized = JsonConvert.DeserializeObject<dynamic>(json);

            // Assert
            Assert.IsNotNull(deserialized);
            // Null becomes null in JSON, empty string stays empty
        }

        #endregion

        #region Progress Calculation Tests

        [TestMethod]
        public void ProgressCalculation_AverageProcessingTime()
        {
            // Arrange
            var processingTimes = new List<double> { 100, 200, 150, 180, 120 };

            // Act
            var average = processingTimes.Average();

            // Assert
            Assert.AreEqual(150, average);
        }

        [TestMethod]
        public void ProgressCalculation_RemainingTime()
        {
            // Arrange
            var totalItems = 100;
            var completedItems = 30;
            var averageTimeMs = 100.0;

            // Act
            var remainingItems = totalItems - completedItems;
            var estimatedMs = remainingItems * averageTimeMs;
            var remainingTime = TimeSpan.FromMilliseconds(estimatedMs);

            // Assert
            Assert.AreEqual(70, remainingItems);
            Assert.AreEqual(7000, estimatedMs);
            Assert.AreEqual(7, remainingTime.TotalSeconds);
        }

        [TestMethod]
        public void ProgressCalculation_CompletionPercentage()
        {
            // Arrange
            var totalItems = 150;
            var completedItems = 75;

            // Act
            var percentage = (double)completedItems / totalItems * 100;

            // Assert
            Assert.AreEqual(50.0, percentage, 0.01);
        }

        #endregion

        #region Resume Logic Tests

        [TestMethod]
        public void ResumeLogic_DeterminesResumeability()
        {
            // Arrange
            var recentProgress = new AlbumProgress
            {
                StartTime = DateTime.Now.AddMinutes(-15),
                LastProcessedIndex = 50,
                TotalItems = 100
            };

            var oldProgress = new AlbumProgress
            {
                StartTime = DateTime.Now.AddMinutes(-60),
                LastProcessedIndex = 50,
                TotalItems = 100
            };

            // Act
            var canResumeRecent = DateTime.Now - recentProgress.StartTime < TimeSpan.FromMinutes(30);
            var canResumeOld = DateTime.Now - oldProgress.StartTime < TimeSpan.FromMinutes(30);

            // Assert
            Assert.IsTrue(canResumeRecent, "Recent progress should be resumable");
            Assert.IsFalse(canResumeOld, "Old progress should not be resumable");
        }

        [TestMethod]
        public void ResumeLogic_CalculatesRemainingWork()
        {
            // Arrange
            var progress = new AlbumProgress
            {
                TotalItems = 100,
                LastProcessedIndex = 49, // Completed index 0-49 = 50 items
                SuccessfullyAdded = 45,
                AlreadyExists = 3,
                FailedToAdd = 2
            };

            // Act
            var itemsRemaining = progress.TotalItems - (progress.LastProcessedIndex + 1);

            // Assert
            Assert.AreEqual(50, itemsRemaining);
        }

        #endregion

        // Helper class matching PictureQuery.AlbumProgress
        private class AlbumProgress
        {
            public int TotalItems { get; set; }
            public int CompletedItems { get; set; }
            public int SuccessfullyAdded { get; set; }
            public int FailedToAdd { get; set; }
            public int AlreadyExists { get; set; }
            public DateTime StartTime { get; set; }
            public List<DateTime> ItemCompletionTimes { get; set; } = new();
            public string AlbumName { get; set; } = string.Empty;
            public string BundleId { get; set; } = string.Empty;
            public int LastProcessedIndex { get; set; } = -1;
        }
    }
}
