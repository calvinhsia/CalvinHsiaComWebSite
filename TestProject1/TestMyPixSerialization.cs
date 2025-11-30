using Microsoft.VisualStudio.TestTools.UnitTesting;
using Client.Shared;
using System.Text.Json;
using System;

namespace TestProject1
{
    /// <summary>
    /// Unit tests for MyPix serialization/deserialization with System.Text.Json
    /// Validates that the Newtonsoft.Json ? System.Text.Json migration works correctly
    /// </summary>
    [TestClass]
    public class TestMyPixSerialization
    {
        [TestMethod]
        public void MyPix_Serialize_BasicProperties_Success()
        {
            // Arrange
            var myPix = new MyPix
            {
                Id = 123,
                PathEnum = 1,
                FileName = "test.jpg",
                Date = new DateTime(2024, 1, 15, 10, 30, 0),
                Rotate = 90,
                Notes = "Test photo"
            };

            // Act
            var json = JsonSerializer.Serialize(myPix);

            // Assert
            Assert.IsNotNull(json);
            Assert.IsTrue(json.Contains("\"Id\":123"));
            Assert.IsTrue(json.Contains("\"FileName\":\"test.jpg\""));
            Assert.IsTrue(json.Contains("\"Notes\":\"Test photo\""));
            
            // Verify computed properties are NOT in JSON (they have [JsonIgnore])
            Assert.IsFalse(json.Contains("AltText"), "AltText should not be serialized");
            Assert.IsFalse(json.Contains("FullFileName"), "FullFileName should not be serialized");
            Assert.IsFalse(json.Contains("IsVideo"), "IsVideo should not be serialized");
        }

        [TestMethod]
        public void MyPix_Deserialize_BasicProperties_Success()
        {
            // Arrange
            var json = @"{
                ""Id"": 456,
                ""PathEnum"": 2,
                ""FileName"": ""photo.jpg"",
                ""Date"": ""2024-02-20T15:45:00"",
                ""Rotate"": 180,
                ""Notes"": ""Test notes""
            }";

            // Act
            var myPix = JsonSerializer.Deserialize<MyPix>(json);

            // Assert
            Assert.IsNotNull(myPix);
            Assert.AreEqual(456, myPix.Id);
            Assert.AreEqual(2, myPix.PathEnum);
            Assert.AreEqual("photo.jpg", myPix.FileName);
            Assert.AreEqual(180, myPix.Rotate);
            Assert.AreEqual("Test notes", myPix.Notes);
        }

        [TestMethod]
        public void MyPix_Deserialize_ComputedPropertiesInJson_Ignored()
        {
            // Arrange - JSON contains computed properties that should be ignored
            var json = @"{
                ""Id"": 789,
                ""PathEnum"": 1,
                ""FileName"": ""video.mp4"",
                ""Date"": ""2024-03-10T12:00:00"",
                ""Rotate"": 0,
                ""Notes"": ""Video test"",
                ""AltText"": ""should be ignored"",
                ""FullFileName"": ""should be ignored"",
                ""IsVideo"": false
            }";

            // Act
            var myPix = JsonSerializer.Deserialize<MyPix>(json);

            // Assert
            Assert.IsNotNull(myPix);
            Assert.AreEqual(789, myPix.Id);
            Assert.AreEqual("video.mp4", myPix.FileName);
            
            // Computed properties should be calculated, not from JSON
            Assert.IsTrue(myPix.IsVideo, "IsVideo should be computed from filename");
            Assert.IsTrue(myPix.AltText.Contains("video.mp4"), "AltText should be computed");
            Assert.IsTrue(myPix.FullFileName.Contains("video.mp4"), "FullFileName should be computed");
        }

        [TestMethod]
        public void MyPix_RoundTrip_PreservesData()
        {
            // Arrange
            var original = new MyPix
            {
                Id = 999,
                PathEnum = 1,
                FileName = "roundtrip.jpg",
                Date = DateTime.Now,
                Rotate = 270,
                Notes = "Round trip test"
            };

            // Act
            var json = JsonSerializer.Serialize(original);
            var deserialized = JsonSerializer.Deserialize<MyPix>(json);

            // Assert
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(original.Id, deserialized.Id);
            Assert.AreEqual(original.PathEnum, deserialized.PathEnum);
            Assert.AreEqual(original.FileName, deserialized.FileName);
            Assert.AreEqual(original.Rotate, deserialized.Rotate);
            Assert.AreEqual(original.Notes, deserialized.Notes);
            // Date comparison with tolerance for serialization precision
            Assert.AreEqual(original.Date.ToString("yyyy-MM-dd HH:mm:ss"), 
                          deserialized.Date.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        [TestMethod]
        public void MyPix_Array_Serialize_Success()
        {
            // Arrange
            var myPixArray = new[]
            {
                new MyPix { Id = 1, FileName = "pic1.jpg", Notes = "First" },
                new MyPix { Id = 2, FileName = "pic2.jpg", Notes = "Second" },
                new MyPix { Id = 3, FileName = "pic3.jpg", Notes = "Third" }
            };

            // Act
            var json = JsonSerializer.Serialize(myPixArray);
            var deserialized = JsonSerializer.Deserialize<MyPix[]>(json);

            // Assert
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(3, deserialized.Length);
            Assert.AreEqual("pic1.jpg", deserialized[0].FileName);
            Assert.AreEqual("pic2.jpg", deserialized[1].FileName);
            Assert.AreEqual("pic3.jpg", deserialized[2].FileName);
        }

        [TestMethod]
        public void MyPix_VideoFile_IsVideoComputed()
        {
            // Arrange - Test various video file extensions
            var videoFiles = new[]
            {
                new MyPix { FileName = "test.avi" },
                new MyPix { FileName = "test.mp4" },
                new MyPix { FileName = "test.mov" },
                new MyPix { FileName = "test.wmv" },
                new MyPix { FileName = "test.mpg" }
            };

            var imageFiles = new[]
            {
                new MyPix { FileName = "test.jpg" },
                new MyPix { FileName = "test.png" },
                new MyPix { FileName = "test.gif" }
            };

            // Act & Assert - Videos
            foreach (var video in videoFiles)
            {
                Assert.IsTrue(video.IsVideo, $"{video.FileName} should be detected as video");
            }

            // Act & Assert - Images
            foreach (var image in imageFiles)
            {
                Assert.IsFalse(image.IsVideo, $"{image.FileName} should not be detected as video");
            }
        }

        [TestMethod]
        public void MyPix_AltText_ComputedCorrectly()
        {
            // Arrange
            var myPix = new MyPix
            {
                FileName = "sunset.jpg",
                Notes = "Beautiful sunset",
                Date = new DateTime(2024, 6, 15, 18, 30, 0)
            };

            // Act
            var altText = myPix.AltText;

            // Assert
            Assert.IsTrue(altText.Contains("sunset.jpg"));
            Assert.IsTrue(altText.Contains("Beautiful sunset"));
            Assert.IsTrue(altText.Contains("2024"));
        }

        [TestMethod]
        public void MyPix_FullFileName_ComputedCorrectly()
        {
            // Arrange
            var myPix = new MyPix
            {
                PathEnum = 1, // Pictures\OldPictures
                FileName = @"2024\01\photo.jpg"
            };

            // Act
            var fullFileName = myPix.FullFileName;

            // Assert
            Assert.IsTrue(fullFileName.Contains("Pictures"));
            Assert.IsTrue(fullFileName.Contains("OldPictures"));
            Assert.IsTrue(fullFileName.Contains("photo.jpg"));
        }

        [TestMethod]
        public void MyPix_EmptyNotes_DefaultsToEmptyString()
        {
            // Arrange & Act
            var myPix = new MyPix
            {
                FileName = "test.jpg"
            };

            // Assert
            Assert.AreEqual(string.Empty, myPix.Notes);
        }

        [TestMethod]
        public void MyPix_DefaultRotate_IsZero()
        {
            // Arrange & Act
            var myPix = new MyPix
            {
                FileName = "test.jpg"
            };

            // Assert
            Assert.AreEqual(0, myPix.Rotate);
        }

        [TestMethod]
        public void MyPix_Deserialize_MissingOptionalFields_UsesDefaults()
        {
            // Arrange - Minimal JSON
            var json = @"{
                ""Id"": 100,
                ""PathEnum"": 0,
                ""FileName"": ""minimal.jpg""
            }";

            // Act
            var myPix = JsonSerializer.Deserialize<MyPix>(json);

            // Assert
            Assert.IsNotNull(myPix);
            Assert.AreEqual(100, myPix.Id);
            Assert.AreEqual("minimal.jpg", myPix.FileName);
            Assert.AreEqual(0, myPix.Rotate, "Default Rotate should be 0");
            Assert.AreEqual(string.Empty, myPix.Notes, "Default Notes should be empty string");
            // Date should be close to now due to default initializer
            Assert.IsTrue((DateTime.Now - myPix.Date).TotalSeconds < 2, "Default Date should be recent");
        }

        [TestMethod]
        public void Thumbs_Serialize_Success()
        {
            // Arrange
            var thumbs = new Thumbs
            {
                Id = 1,
                ThumbVersion = Thumbs.CurrentThumbVersion,
                MyPixId = 123,
                ThumbSize = 176,
                ThumbData = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 } // JPEG header
            };

            // Act
            var json = JsonSerializer.Serialize(thumbs);
            var deserialized = JsonSerializer.Deserialize<Thumbs>(json);

            // Assert
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(thumbs.Id, deserialized.Id);
            Assert.AreEqual(thumbs.MyPixId, deserialized.MyPixId);
            Assert.AreEqual(thumbs.ThumbSize, deserialized.ThumbSize);
            CollectionAssert.AreEqual(thumbs.ThumbData, deserialized.ThumbData);
        }

        [TestMethod]
        public void MyPix_ToString_FormatsCorrectly()
        {
            // Arrange
            var myPix = new MyPix
            {
                Id = 42,
                FileName = "test.jpg",
                Date = new DateTime(2024, 1, 1),
                Notes = "Test",
                PathEnum = 1,
                Rotate = 90
            };

            // Act
            var result = myPix.ToString();

            // Assert
            Assert.IsTrue(result.Contains("42"));
            Assert.IsTrue(result.Contains("test.jpg"));
            Assert.IsTrue(result.Contains("Test"));
        }

        #region Edge Case Tests

        [TestMethod]
        public void MyPix_PathEnum_Zero_UsesFullPath()
        {
            // Arrange - PathEnum 0 means entire path is in FileName
            var myPix = new MyPix
            {
                PathEnum = 0,
                FileName = @"C:\Full\Path\To\photo.jpg"
            };

            // Act
            var fullFileName = myPix.FullFileName;

            // Assert
            Assert.AreEqual(@"C:\Full\Path\To\photo.jpg", fullFileName);
        }

        [TestMethod]
        [ExpectedException(typeof(IndexOutOfRangeException))]
        public void MyPix_PathEnum_OutOfRange_ThrowsException()
        {
            // Arrange
            var myPix = new MyPix
            {
                PathEnum = 99,
                FileName = "test.jpg"
            };

            // Act - accessing FullFileName should throw
            var _ = myPix.FullFileName;

            // Assert - handled by ExpectedException
        }

        [TestMethod]
        public void MyPix_IsVideoFile_CaseInsensitive()
        {
            // Arrange & Act & Assert
            Assert.IsTrue(MyPix.IsVideoFile("video.MP4"), "MP4 uppercase should be detected");
            Assert.IsTrue(MyPix.IsVideoFile("VIDEO.AVI"), "AVI uppercase should be detected");
            Assert.IsTrue(MyPix.IsVideoFile("test.MoV"), "MoV mixed case should be detected");
            Assert.IsTrue(MyPix.IsVideoFile("file.WMV"), "WMV uppercase should be detected");
            Assert.IsTrue(MyPix.IsVideoFile("clip.MPG"), "MPG uppercase should be detected");
        }

        [TestMethod]
        public void MyPix_IsVideoFile_NoExtension_ReturnsFalse()
        {
            // Arrange & Act
            var result = MyPix.IsVideoFile("videofile");

            // Assert
            Assert.IsFalse(result, "File without extension should not be detected as video");
        }

        [TestMethod]
        public void MyPix_IsVideoFile_EmptyString_ReturnsFalse()
        {
            // Arrange & Act
            var result = MyPix.IsVideoFile("");

            // Assert
            Assert.IsFalse(result, "Empty string should not be detected as video");
        }

        [TestMethod]
        public void MyPix_AltText_WithEmptyNotes_FormatsCorrectly()
        {
            // Arrange
            var myPix = new MyPix
            {
                FileName = "photo.jpg",
                Notes = "",
                Date = new DateTime(2024, 1, 15)
            };

            // Act
            var altText = myPix.AltText;

            // Assert
            Assert.IsTrue(altText.Contains("photo.jpg"));
            Assert.IsTrue(altText.Contains("1/15/2024") || altText.Contains("2024"));
            Assert.IsNotNull(altText);
        }

        [TestMethod]
        public void MyPix_AltText_WithSpecialCharacters_HandlesCorrectly()
        {
            // Arrange
            var myPix = new MyPix
            {
                FileName = "photo & test.jpg",
                Notes = "Quote's and \"quotes\"",
                Date = DateTime.Now
            };

            // Act
            var altText = myPix.AltText;

            // Assert
            Assert.IsTrue(altText.Contains("&"), "Ampersand should be preserved");
            Assert.IsTrue(altText.Contains("'"), "Single quote should be preserved");
            Assert.IsTrue(altText.Contains("\""), "Double quote should be preserved");
        }

        [TestMethod]
        public void MyPix_Date_DefaultIsNow()
        {
            // Arrange
            var before = DateTime.Now.AddSeconds(-1);

            // Act
            var myPix = new MyPix();

            // Assert
            var after = DateTime.Now.AddSeconds(1);
            Assert.IsTrue(myPix.Date >= before && myPix.Date <= after,
                "Default Date should be close to DateTime.Now");
        }

        [TestMethod]
        public void MyPix_Date_CanBeSetExplicitly()
        {
            // Arrange
            var testDate = new DateTime(2020, 6, 15, 14, 30, 0);

            // Act
            var myPix = new MyPix { Date = testDate };

            // Assert
            Assert.AreEqual(testDate, myPix.Date);
        }

        [TestMethod]
        public void MyPix_ToString_ContainsAllProperties()
        {
            // Arrange
            var myPix = new MyPix
            {
                Id = 42,
                FileName = "test.jpg",
                Date = new DateTime(2024, 1, 15),
                Notes = "Test note",
                PathEnum = 1,
                Rotate = 90
            };

            // Act
            var result = myPix.ToString();

            // Assert - all properties should appear in ToString
            Assert.IsTrue(result.Contains("42"), "Id should be in ToString");
            Assert.IsTrue(result.Contains("test.jpg"), "FileName should be in ToString");
            Assert.IsTrue(result.Contains("Test note"), "Notes should be in ToString");
            Assert.IsTrue(result.Contains("1"), "PathEnum should be in ToString");
            Assert.IsTrue(result.Contains("90"), "Rotate should be in ToString");
        }

        #endregion
    }
}
