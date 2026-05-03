using Api;
using Client.Shared;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using BlazorWasm.Services;

namespace TestProject1
{
    [TestClass]
    public class UnitTest1
    {
        static string sqliteConnStr = @$"Filename = data\Mypix.db";
        //        public TestContext TestContext { get; set; }
        [TestMethod]
        public void TestMethod1()
        {
            // ✓ Set debug mode for reproducible results
            DebugHelper.SetDebugMode(true);

            // ✓ Create centralized RandomService (will use fixed seed since debug=true)
            var randomService = new RandomService();

            var dictionaryService = new DictionaryService(randomService);
            var wh = new WordHandler(dictionaryService, randomService);
            for (int i = 0; i < 10; i++)
            {
                (var randword, var grid, var gfilled) = wh.CreateGrid();
                Console.WriteLine($"RandWord {randword} {grid}  {gfilled}");
            }
        }
        [TestMethod]
        [Ignore]
        public async Task TestQueryPix()
        {
            await Task.Yield();
            var dbc = new MyPixWebDBContext(new DbContextOptionsBuilder<MyPixWebDBContext>().UseSqlite(sqliteConnStr).Options);
            var querystring = "Tyler washing carrots in backyard";
            querystring = "carrots";
            var querystring2 = "aimee";
            var sqlstmt = $"select * from MyPix";
            Console.WriteLine($"query = {sqlstmt}");
            var valparam = new SqliteParameter("valparam", querystring);
            var result2 = await dbc.MyPixes.FromSqlInterpolated(
                $"select * from MyPix where Notes like {("%" + querystring + "%")}").ToListAsync();
            var result = await dbc.MyPixes.FromSqlInterpolated(
                $"select * from MyPix where Notes like {("%" + querystring + "%")} OR Notes like {("%" + querystring2 + "%")}").ToListAsync();

            Console.WriteLine($"# results for '{querystring}' = {result.Count}");
            foreach (var mypix in result)
            {
                Console.WriteLine($"{mypix}");
            }
        }
        [TestMethod]
        [Ignore]
        public async Task TestJson()
        {
            await Task.Yield();
            var dbc = new MyPixWebDBContext(new DbContextOptionsBuilder<MyPixWebDBContext>().UseSqlite(sqliteConnStr).Options);
            var mypixes = await dbc.MyPixes.FromSql($"Select * from MyPix where Notes like '%carrots%'").ToListAsync();
            var json = JsonSerializer.Serialize(mypixes);
            var back = JsonSerializer.Deserialize<MyPix[]>(json);
        }
        [TestMethod]
        [Ignore]
        public async Task TestRawData()
        {
            await Task.Yield();
            using var conn = new SqliteConnection(sqliteConnStr);
            conn.Open();
            var sqlCmd = new SqliteCommand(@"Select * from MyPix where Notes like '%carrots%'", conn);
            using var res = await sqlCmd.ExecuteReaderAsync();
            var lstMyPix = new List<MyPix>();
            while (res.Read())
            {
                MyPix mypix = new MyPix()
                {
                    Id = (int)(long)res["Id"],
                    FileName = (string)res["FileName"],
                    Date = DateTime.Parse((string)res["Date"]),
                    PathEnum = (int)(long)res["PathEnum"],
                    Notes = (string)res["Notes"],
                    Rotate = (int)(long)res["Rotate"]
                };
                Console.WriteLine($"{mypix}");
                lstMyPix.Add(mypix);
            }
            conn.Close();

        }
        [TestMethod]
        public void TestParseJson()
        {
            var jsonPicMeta = @"
{
    ""@odata.context"": ""https://graph.microsoft.com/v1.0/$metadata#users('calvin_hsia%40live.com')/drive/root/$entity"",
    ""@microsoft.graph.downloadUrl"": ""https://public.bl.files.1drv.com/y4m5N2VN3U2kB0vEvvx1m-uvmuw9rrrkfWRtNK4wXEezvGyzF1XPncJ3AiA_hoOrFQz-Q33DeY0gAf9VZccoPGAuMzZGlrNCulrjaMqkwCn0WJFcilIiAJRharM9REKP69gCkR4X8pB9-mh53bOQtvsjaX2YRpf-M0baRcFagkFTQzXIQkf_XFXQDcl1YzBRsEFs5LtLq_P8UHdPo0WfvvrtLw56tcC0D4CbRdGTx1R3TY"",
    ""createdDateTime"": ""2020-09-06T03:13:03.55Z"",
    ""cTag"": ""aYzpENjlGMzU1MkNFRkMyMSEzMTg5NDEuMjU3"",
    ""eTag"": ""aRDY5RjM1NTJDRUZDMjEhMzE4OTQxLjg1"",
    ""id"": ""D69F3552CEFC21!318941"",
    ""lastModifiedDateTime"": ""2023-06-22T03:46:11.317Z"",
    ""name"": ""IMG_4493.JPG"",
    ""size"": 1500346,
    ""webUrl"": ""https://1drv.ms/i/s!ACH8zlI1n9YAk7td"",
    ""reactions"": {
        ""commentCount"": 0
    },
    ""createdBy"": {
        ""application"": {
            ""displayName"": ""OneDrive"",
            ""id"": ""481710a4""
        },
        ""device"": {
            ""id"": ""18bffe49c0e629""
        },
        ""user"": {
            ""displayName"": ""Calvin Hsia"",
            ""id"": ""d69f3552cefc21""
        }
    },
    ""lastModifiedBy"": {
        ""application"": {
            ""displayName"": ""OneDrive"",
            ""id"": ""481710a4""
        },
        ""device"": {
            ""id"": ""18bffe49c0e629""
        },
        ""user"": {
            ""displayName"": ""Calvin Hsia"",
            ""id"": ""d69f3552cefc21""
        }
    },
    ""parentReference"": {
        ""driveId"": ""d69f3552cefc21"",
        ""driveType"": ""personal"",
        ""id"": ""D69F3552CEFC21!317041"",
        ""name"": ""16"",
        ""path"": ""/drive/root:/Pictures/OldPictures/2006/07/16""
    },
    ""file"": {
        ""mimeType"": ""image/jpeg"",
        ""hashes"": {
            ""quickXorHash"": ""Bwru6dKDjwU64Vswhju84c1ca/k="",
            ""sha1Hash"": ""BF9B33576F2B921ADB3549456621FDEE74B417C2"",
            ""sha256Hash"": ""3A43088BDF40A8240CCC1C3AAB526DB653C5C122D7EB4F959755581554BA5102""
        }
    },
    ""fileSystemInfo"": {
        ""createdDateTime"": ""2020-09-06T03:13:03.55Z"",
        ""lastModifiedDateTime"": ""2006-07-14T22:04:20Z""
    },
    ""image"": {
        ""height"": 1704,
        ""width"": 2272
    },
    ""pendingOperations"": {
        ""pendingContentUpdate"": {
            ""queuedDateTime"": ""2023-06-21T10:57:09.6344054Z""
        }
    },
    ""photo"": {
        ""cameraMake"": ""Canon"",
        ""cameraModel"": ""Canon PowerShot S410"",
        ""exposureDenominator"": 125.0,
        ""exposureNumerator"": 1.0,
        ""focalLength"": 7.40625,
        ""fNumber"": 7.1,
        ""orientation"": 1,
        ""takenDateTime"": ""2006-07-14T15:04:22Z""
    }
}";
            var json = JsonSerializer.Serialize(jsonPicMeta);
            using var doc = JsonDocument.Parse(jsonPicMeta);
            var id = doc.RootElement.GetProperty("id").GetString();
        }

        /// <summary>
        /// Opens all source files and checks their BOM (Byte Order Mark) status.
        /// Fails if any file with Unicode characters is missing UTF-8 BOM.
        /// This ensures consistent encoding across the codebase.
        /// Only runs on Windows since BOM is primarily a Visual Studio/Windows concern.
        /// </summary>
        [TestMethod]
        public async Task CheckSourceFileBOMStatus()
        {
            // Skip on non-Windows platforms - BOM enforcement is primarily for Visual Studio on Windows
            if (!OperatingSystem.IsWindows())
            {
                Console.WriteLine("⏭️ Skipping BOM check on non-Windows platform");
                Assert.Inconclusive("BOM check only runs on Windows");
                return;
            }

            Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║           Source File BOM Status Check                        ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            var baseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
            Console.WriteLine($"Base directory: {baseDir}");
            Console.WriteLine();

            var extensions = new[] { ".cs", ".razor", ".css", ".js", ".json", ".html", ".csproj" };
            // Exclude directories - use Path.DirectorySeparatorChar for cross-platform compatibility
            var excludeDirs = new[] { "bin", "obj", "node_modules", ".git" };
            var excludePaths = new[] { $"wwwroot{Path.DirectorySeparatorChar}lib" }; // Third-party libraries

            var filesWithBom = new List<string>();
            var filesWithoutBom = new List<string>();
            var filesWithUnicode = new List<(string path, string chars)>();

            foreach (var ext in extensions)
            {
                var files = Directory.GetFiles(baseDir, $"*{ext}", SearchOption.AllDirectories)
                    .Where(f => !excludeDirs.Any(d => f.Contains(Path.DirectorySeparatorChar + d + Path.DirectorySeparatorChar)))
                    .Where(f => !excludePaths.Any(p => f.Contains(p))) // Exclude third-party lib paths
                    .ToList();

                foreach (var file in files)
                {
                    try
                    {
                        var bytes = await File.ReadAllBytesAsync(file);
                        var relativePath = Path.GetRelativePath(baseDir, file);

                        // Check for UTF-8 BOM (EF BB BF)
                        bool hasBom = bytes.Length >= 3 && 
                                      bytes[0] == 0xEF && 
                                      bytes[1] == 0xBB && 
                                      bytes[2] == 0xBF;

                        if (hasBom)
                        {
                            filesWithBom.Add(relativePath);
                        }
                        else
                        {
                            filesWithoutBom.Add(relativePath);
                        }

                        // Check for Unicode characters (non-ASCII)
                        var content = await File.ReadAllTextAsync(file);
                        var unicodeChars = content.Where(c => c > 127).Distinct().ToArray();
                        if (unicodeChars.Length > 0)
                        {
                            var charDisplay = string.Join(" ", unicodeChars.Take(10).Select(c => $"{c} (U+{(int)c:X4})"));
                            if (unicodeChars.Length > 10) charDisplay += " ...";
                            filesWithUnicode.Add((relativePath, charDisplay));
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️  Error reading {file}: {ex.Message}");
                    }
                }
            }

            // Summary
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine($"📊 SUMMARY");
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine($"  Files WITH UTF-8 BOM:    {filesWithBom.Count}");
            Console.WriteLine($"  Files WITHOUT BOM:       {filesWithoutBom.Count}");
            Console.WriteLine($"  Files with Unicode:      {filesWithUnicode.Count}");
            Console.WriteLine();

            // Files with BOM
            Console.WriteLine("───────────────────────────────────────────────────────────────");
            Console.WriteLine("📄 Files WITH UTF-8 BOM:");
            Console.WriteLine("───────────────────────────────────────────────────────────────");
            if (filesWithBom.Count == 0)
            {
                Console.WriteLine("  (none)");
            }
            else
            {
                foreach (var file in filesWithBom.OrderBy(f => f))
                {
                    Console.WriteLine($"  ✓ {file}");
                }
            }
            Console.WriteLine();

            // Files with Unicode characters (important ones)
            Console.WriteLine("───────────────────────────────────────────────────────────────");
            Console.WriteLine("🔤 Files with Unicode characters (may need BOM):");
            Console.WriteLine("───────────────────────────────────────────────────────────────");
            if (filesWithUnicode.Count == 0)
            {
                Console.WriteLine("  (none)");
            }
            else
            {
                foreach (var (path, chars) in filesWithUnicode.OrderBy(f => f.path))
                {
                    var hasBom = filesWithBom.Contains(path);
                    var status = hasBom ? "✓" : "⚠️";
                    Console.WriteLine($"  {status} {path}");
                    Console.WriteLine($"      Unicode: {chars}");
                }
            }
            Console.WriteLine();

            // Files without BOM that have Unicode (potential issues)
            var unicodeWithoutBom = filesWithUnicode
                .Where(f => !filesWithBom.Contains(f.path))
                .ToList();

            if (unicodeWithoutBom.Count > 0)
            {
                Console.WriteLine("───────────────────────────────────────────────────────────────");
                Console.WriteLine("⚠️  POTENTIAL ISSUES - Unicode files without BOM:");
                Console.WriteLine("───────────────────────────────────────────────────────────────");
                foreach (var (path, chars) in unicodeWithoutBom)
                {
                    Console.WriteLine($"  ❌ {path}");
                    Console.WriteLine($"      Unicode: {chars}");
                }
                Console.WriteLine();
                Console.WriteLine("💡 TIP: These files should be saved with UTF-8 BOM encoding");
                Console.WriteLine("   in Visual Studio to ensure Unicode characters display correctly.");
            }

            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine("✅ BOM status check completed!");
            Console.WriteLine("═══════════════════════════════════════════════════════════════");

            if (unicodeWithoutBom.Count > 0)
            {
                var failingFiles = string.Join(", ", unicodeWithoutBom.Select(f => f.path));
                Assert.Fail($"Unicode files missing UTF-8 BOM ({unicodeWithoutBom.Count}): {failingFiles}"); // comment out this line to fix automatically
                Console.WriteLine();
                Console.WriteLine("🔧 FIXING: Adding UTF-8 BOM to Unicode files without BOM...");
                Console.WriteLine();

                foreach (var (relativePath, _) in unicodeWithoutBom)
                {
                    var fullPath = Path.Combine(baseDir, relativePath);
                    try
                    {
                        // Read the file content
                        var content = await File.ReadAllTextAsync(fullPath, Encoding.UTF8);

                        // Write back with UTF-8 BOM
                        var utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
                        await File.WriteAllTextAsync(fullPath, content, utf8WithBom);

                        Console.WriteLine($"  ✅ Fixed: {relativePath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  ❌ Error fixing {relativePath}: {ex.Message}");
                    }
                }

                Console.WriteLine();
                Console.WriteLine("🔧 BOM fix completed!");
            }

            await Task.CompletedTask;
        }
    }
}