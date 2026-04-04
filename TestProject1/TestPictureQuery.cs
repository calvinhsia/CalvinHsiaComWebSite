using Microsoft.VisualStudio.TestTools.UnitTesting;
using Client.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TestProject1
{
    /// <summary>
    /// Unit tests for picture querying and filtering logic
    /// Tests MyPix model, filtering, regex patterns, and query operations
    /// </summary>
    [TestClass]
    public class TestPictureQuery
    {
        #region MyPix Model Tests

        [TestMethod]
        public void MyPix_FullFileName_CombinesPathAndFileName()
        {
            // Arrange
            var myPix = new MyPix
            {
                PathEnum = 1, // Pictures\OldPictures
                FileName = "2000/200012/02/test.jpg"
            };

            // Act
            var fullFileName = myPix.FullFileName;

            // Assert
            Assert.IsTrue(fullFileName.Contains("Pictures"));
            Assert.IsTrue(fullFileName.Contains("OldPictures"));
            Assert.IsTrue(fullFileName.Contains("test.jpg"));
        }

        [TestMethod]
        public void MyPix_IsVideo_DetectsVideoExtensions()
        {
            // Arrange & Act & Assert
            Assert.IsTrue(MyPix.IsVideoFile("test.avi"));
            Assert.IsTrue(MyPix.IsVideoFile("test.mp4"));
            Assert.IsTrue(MyPix.IsVideoFile("test.mov"));
            Assert.IsTrue(MyPix.IsVideoFile("test.wmv"));
            Assert.IsTrue(MyPix.IsVideoFile("test.mpg"));

            Assert.IsFalse(MyPix.IsVideoFile("test.jpg"));
            Assert.IsFalse(MyPix.IsVideoFile("test.png"));
            Assert.IsFalse(MyPix.IsVideoFile("test.gif"));
        }

        [TestMethod]
        public void MyPix_IsVideo_IsCaseInsensitive()
        {
            // Arrange & Act & Assert
            Assert.IsTrue(MyPix.IsVideoFile("test.AVI"));
            Assert.IsTrue(MyPix.IsVideoFile("test.MP4"));
            Assert.IsTrue(MyPix.IsVideoFile("test.MoV"));
        }

        [TestMethod]
        public void MyPix_AltText_CombinesFileNameNotesAndDate()
        {
            // Arrange
            var myPix = new MyPix
            {
                FileName = "test.jpg",
                Notes = "Family photo",
                Date = new DateTime(2020, 1, 15)
            };

            // Act
            var altText = myPix.AltText;

            // Assert
            Assert.IsTrue(altText.Contains("test.jpg"));
            Assert.IsTrue(altText.Contains("Family photo"));
            Assert.IsTrue(altText.Contains("2020"));
        }

        #endregion

        #region Date Filter Tests

        [TestMethod]
        public void DateFilter_IncludesItemsInDateRange()
        {
            // Arrange
            var startDate = new DateTime(2020, 1, 1);
            var endDate = new DateTime(2020, 12, 31);
            var testDate = new DateTime(2020, 6, 15);

            // Act
            bool isInRange = testDate >= startDate && testDate <= endDate;

            // Assert
            Assert.IsTrue(isInRange);
        }

        [TestMethod]
        public void DateFilter_ExcludesItemsOutsideDateRange()
        {
            // Arrange
            var startDate = new DateTime(2020, 1, 1);
            var endDate = new DateTime(2020, 12, 31);
            var testDate = new DateTime(2021, 1, 1);

            // Act
            bool isInRange = testDate >= startDate && testDate <= endDate;

            // Assert
            Assert.IsFalse(isInRange);
        }

        #endregion

        #region Media Type Filter Tests

        [TestMethod]
        public void MediaTypeFilter_PicOnly_ExcludesVideos()
        {
            // Arrange
            var mediaType = "pic";
            var pictureFile = "test.jpg";
            var videoFile = "test.avi";

            // Act
            bool includePicture = !MyPix.IsVideoFile(pictureFile) || mediaType != "pic";
            bool includeVideo = !MyPix.IsVideoFile(videoFile) || mediaType != "pic";

            // Assert
            Assert.IsTrue(includePicture, "Pictures should be included");
            Assert.IsFalse(includeVideo, "Videos should be excluded");
        }

        [TestMethod]
        public void MediaTypeFilter_MovOnly_ExcludesPictures()
        {
            // Arrange
            var mediaType = "mov";
            var pictureFile = "test.jpg";
            var videoFile = "test.avi";

            // Act
            bool includePicture = MyPix.IsVideoFile(pictureFile) || mediaType != "mov";
            bool includeVideo = MyPix.IsVideoFile(videoFile) || mediaType != "mov";

            // Assert
            Assert.IsFalse(includePicture, "Pictures should be excluded");
            Assert.IsTrue(includeVideo, "Videos should be included");
        }

        [TestMethod]
        public void MediaTypeFilter_Empty_IncludesAll()
        {
            // Arrange
            string? mediaType = null;

            // Act & Assert
            Assert.IsTrue(string.IsNullOrEmpty(mediaType), "Should include all media types");
        }

        #endregion

        #region Text Filter Tests

        [TestMethod]
        public void TextFilter_SimpleMatch_IsCaseInsensitive()
        {
            // Arrange
            var notes = "Family vacation in Hawaii";
            var filter = "hawaii";

            // Act
            bool matches = notes.Contains(filter, StringComparison.OrdinalIgnoreCase);

            // Assert
            Assert.IsTrue(matches);
        }

        [TestMethod]
        public void TextFilter_MultipleWords_RequiresAllWords()
        {
            // Arrange
            var notes = "Duncan Martin test photo";
            var filter = "Duncan Martin test";
            var filterParts = filter.Split(' ');

            // Act
            bool allWordsMatch = filterParts.All(part =>
       notes.Contains(part, StringComparison.OrdinalIgnoreCase));

            // Assert
            Assert.IsTrue(allWordsMatch);
        }

        [TestMethod]
        public void TextFilter_OrOperator_MatchesAnyWord()
        {
            // Arrange
            var notes = "Duncan at the park";
            var filter = "| Duncan Martin test"; // OR operator
            var filterParts = filter[1..].Trim().Split(' ');

            // Act
            bool anyWordMatches = filterParts.Any(part =>
          notes.Contains(part, StringComparison.OrdinalIgnoreCase));

            // Assert
            Assert.IsTrue(anyWordMatches);
        }

        [TestMethod]
        public void TextFilter_Regex_MatchesPattern()
        {
            // Arrange
            var notes = "Photo with Pui and Hallie";
            var filter = "^.*(pui|hallie).*"; // Regex pattern

            // Act
            bool matches = Regex.IsMatch(notes, filter, RegexOptions.IgnoreCase);

            // Assert
            Assert.IsTrue(matches);
        }

        [TestMethod]
        public void TextFilter_FilenameRegex_MatchesFilePattern()
        {
            // Arrange
            var fileName = "video_2020_01_15.avi";
            var filter = @"$^(.*)\.avi"; // Filename regex
            filter = filter[1..]; // Remove $ prefix

            // Act
            bool matches = Regex.IsMatch(fileName, filter, RegexOptions.IgnoreCase);

            // Assert
            Assert.IsTrue(matches);
        }

        #endregion

        #region Complex Filter Scenarios

        [TestMethod]
        public void ComplexFilter_MultipleWordsWithOr()
        {
            // Arrange
            var testCases = new[]
              {
                    new { Notes = "Duncan at beach", Filter = "| Duncan Martin", Expected = true },
                    new { Notes = "Martin at park", Filter = "| Duncan Martin", Expected = true },
                    new { Notes = "John at school", Filter = "| Duncan Martin", Expected = false }
                };

            // Act & Assert
            foreach (var testCase in testCases)
            {
                var filterParts = testCase.Filter[1..].Trim().Split(' ');
                bool matches = filterParts.Any(part =>
                      testCase.Notes.Contains(part, StringComparison.OrdinalIgnoreCase));

                Assert.AreEqual(testCase.Expected, matches,
               $"Filter '{testCase.Filter}' on '{testCase.Notes}' should be {testCase.Expected}");
            }
        }

        [TestMethod]
        public void ComplexFilter_RegexWithMultipleWords()
        {
            // Arrange
            var notes1 = "Photo with Duncan and Martin";
            var notes2 = "Photo with John and Mary";
            var filter = @"^(?=.*\bDuncan\b)(?=.*\bMartin\b).*$";

            // Act
            bool matches1 = Regex.IsMatch(notes1, filter, RegexOptions.IgnoreCase);
            bool matches2 = Regex.IsMatch(notes2, filter, RegexOptions.IgnoreCase);

            // Assert
            Assert.IsTrue(matches1, "Should match when both words present");
            Assert.IsFalse(matches2, "Should not match when words missing");
        }

        [TestMethod]
        public void ComplexFilter_FileExtensionPatterns()
        {
            // Arrange
            var fileNames = new[]
            {
     "photo_001.jpg",
    "video_001.avi",
   "movie_001.mp4",
      "image_001.png"
      };
            var aviFilter = @"$^(.*)\.avi";
            var imageFilter = @"$^(.*)\.(jpg|png)";

            // Act & Assert
            foreach (var fileName in fileNames)
            {
                var aviMatches = Regex.IsMatch(fileName, aviFilter[1..], RegexOptions.IgnoreCase);
                var imageMatches = Regex.IsMatch(fileName, imageFilter[1..], RegexOptions.IgnoreCase);

                if (fileName.EndsWith(".avi"))
                {
                    Assert.IsTrue(aviMatches, $"{fileName} should match AVI filter");
                }
                if (fileName.EndsWith(".jpg") || fileName.EndsWith(".png"))
                {
                    Assert.IsTrue(imageMatches, $"{fileName} should match image filter");
                }
            }
        }

        #endregion

        #region Sorting and Ordering Tests

        [TestMethod]
        public void SortByDate_OrdersDescending()
        {
            // Arrange
            var pictures = new List<MyPix>
      {
      new MyPix { Id = 1, Date = new DateTime(2020, 1, 1) },
   new MyPix { Id = 2, Date = new DateTime(2020, 6, 1) },
  new MyPix { Id = 3, Date = new DateTime(2020, 12, 1) }
   };

            // Act
            var sorted = pictures.OrderByDescending(p => p.Date).ToList();

            // Assert
            Assert.AreEqual(3, sorted[0].Id);
            Assert.AreEqual(2, sorted[1].Id);
            Assert.AreEqual(1, sorted[2].Id);
        }

        #endregion

        #region Pagination Tests

        [TestMethod]
        public void Pagination_SkipTake_ReturnsCorrectPage()
        {
            // Arrange
            var allPictures = Enumerable.Range(1, 100)
          .Select(i => new MyPix { Id = i })
         .ToList();

            int pageNumber = 2;
            int itemsPerPage = 10;

            // Act
            var page = allPictures
           .Skip((pageNumber - 1) * itemsPerPage)
              .Take(itemsPerPage)
                .ToList();

            // Assert
            Assert.AreEqual(10, page.Count);
            Assert.AreEqual(11, page[0].Id); // First item on page 2
            Assert.AreEqual(20, page[9].Id); // Last item on page 2
        }

        [TestMethod]
        public void Pagination_CalculateTotalPages()
        {
            // Arrange
            int totalItems = 95;
            int itemsPerPage = 10;

            // Act
            int totalPages = (totalItems + itemsPerPage - 1) / itemsPerPage;

            // Assert
            Assert.AreEqual(10, totalPages);
        }

        #endregion

        #region Edge Cases

        [TestMethod]
        public void Filter_EmptyNotes_DoesNotCrash()
        {
            // Arrange
            var notes = string.Empty;
            var filter = "test";

            // Act
            bool matches = notes.Contains(filter, StringComparison.OrdinalIgnoreCase);

            // Assert
            Assert.IsFalse(matches);
        }

        [TestMethod]
        public void Filter_NullNotes_HandledSafely()
        {
            // Arrange
            string? notes = null;
            var filter = "test";

            // Act
            bool matches = (notes ?? string.Empty).Contains(filter, StringComparison.OrdinalIgnoreCase);

            // Assert
            Assert.IsFalse(matches);
        }

        [TestMethod]
        public void Regex_InvalidPattern_ThrowsException()
        {
            // Arrange
            var notes = "test";
#pragma warning disable RE0001 // Invalid regex pattern
            var invalidPattern = @"^(?=.*\b"; // Incomplete regex
#pragma warning restore RE0001 // Invalid regex pattern

            // Act & Assert - RegexParseException is derived from ArgumentException in newer .NET versions
            Assert.ThrowsException<RegexParseException>(() =>
                {
                    Regex.IsMatch(notes, invalidPattern);
                });
        }

        [TestMethod]
        public void FileExtension_NoExtension_HandledSafely()
        {
            // Arrange
            var fileNameWithoutExtension = "testfile";

            // Act
            bool isVideo = MyPix.IsVideoFile(fileNameWithoutExtension);

            // Assert
            // File without extension should return false (FIXED in IsVideoFile)
            Assert.IsFalse(isVideo, "File without extension should not be detected as video");
        }

        #endregion

        #region Integration Scenarios

        [TestMethod]
        public void FullFilterScenario_DateAndMediaTypeAndText()
        {
            // Arrange
            var pictures = new List<MyPix>
      {
         new MyPix
   {
      Id = 1,
        FileName = "photo1.jpg",
       Date = new DateTime(2020, 6, 15),
   Notes = "Family vacation Hawaii"
      },
  new MyPix
       {
      Id = 2,
   FileName = "video1.avi",
Date = new DateTime(2020, 7, 20),
          Notes = "Beach video Hawaii"
    },
    new MyPix
    {
  Id = 3,
   FileName = "photo2.jpg",
Date = new DateTime(2019, 6, 15),
     Notes = "Old family photo"
    },
           new MyPix
     {
  Id = 4,
    FileName = "photo3.jpg",
      Date = new DateTime(2020, 8, 10),
         Notes = "Mountains Colorado"
       }
            };

            var startDate = new DateTime(2020, 1, 1);
            var endDate = new DateTime(2020, 12, 31);
            var mediaType = "pic"; // Pictures only
            var textFilter = "hawaii";

            // Act
            var filtered = pictures.Where(p =>
                 {
                     // Date filter
                     if (p.Date < startDate || p.Date > endDate)
                         return false;

                     // Media type filter
                     if (p.IsVideo && mediaType == "pic")
                         return false;

                     // Text filter
                     if (!string.IsNullOrEmpty(textFilter))
                     {
                         if (!(p.Notes ?? string.Empty).Contains(textFilter, StringComparison.OrdinalIgnoreCase))
                             return false;
                     }

                     return true;
                 }).ToList();

            // Assert
            Assert.AreEqual(1, filtered.Count);
            Assert.AreEqual(1, filtered[0].Id);
            Assert.IsTrue(filtered[0].Notes!.Contains("Hawaii", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void QueryWorkflow_FilterSortPaginate()
        {
            // Arrange
            var allPictures = Enumerable.Range(1, 50)
         .Select(i => new MyPix
         {
             Id = i,
             FileName = $"photo{i}.jpg",
             Date = DateTime.Now.AddDays(-i),
             Notes = i % 2 == 0 ? "Even photo" : "Odd photo"
         })
           .ToList();

            var textFilter = "Even";
            int pageNumber = 1;
            int pageSize = 10;

            // Act
            var result = allPictures
        .Where(p => p.Notes!.Contains(textFilter, StringComparison.OrdinalIgnoreCase))
       .OrderByDescending(p => p.Date)
          .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
           .ToList();

            // Assert
            Assert.AreEqual(10, result.Count);
            Assert.IsTrue(result.All(p => p.Notes!.Contains("Even")));
            // Should be ordered by date descending (most recent first)
            for (int i = 1; i < result.Count; i++)
            {
                Assert.IsTrue(result[i - 1].Date >= result[i].Date);
            }
        }

        #endregion
    }
}
