using Api;
using Client.Shared;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WordScapeBlazorWasm.Services;

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
            // ?? Set debug mode for reproducible results
            DebugHelper.SetDebugMode(true);

            // ?? Create centralized RandomService (will use fixed seed since debug=true)
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
            //            var sqllitefilename = @"C:\Users\calvinh\source\repos\CalvinHsiaComWebSite\Api\MyPix.db";
            //            using var db = new SqliteConnection($"Filename={sqllitefilename}");
            //            db.Open();
            //            var selectCommand = new SqliteCommand(@$"SELECT * from MyPix", db);
            //            using SqliteDataReader query = selectCommand.ExecuteReader();
            //            while (query.Read())
            //            {
            //                var id = query["id"];
            ////                lstIds.Add((long)id);
            //                var pathenum = query["PathEnum"];
            //                var fname = query["FileName"];
            //                var date = query["Date"];
            //                var notes = query["Notes"];
            //                Console.WriteLine($"read data {id} {pathenum} {fname} {date}  {notes}");
            //            }


            var dbc = new MyPixWebDBContext(new DbContextOptionsBuilder<MyPixWebDBContext>().UseSqlite(sqliteConnStr).Options);
            var querystring = "Tyler washing carrots in backyard";
            querystring = "carrots";
            var querystring2 = "aimee";
            var sqlstmt = $"select * from MyPix";
            //            var sqlstmt = $"select * from MyPix where Notes like '%{querystring}%'";
            Console.WriteLine($"query = {sqlstmt}");
            var valparam = new SqliteParameter("valparam", querystring);
            var result2 = await dbc.MyPixes.FromSqlInterpolated(
                $"select * from MyPix where Notes like {("%" + querystring + "%")}").ToListAsync();
            var result = await dbc.MyPixes.FromSqlInterpolated(
                $"select * from MyPix where Notes like {("%" + querystring + "%")} OR Notes like {("%" + querystring2 + "%")}").ToListAsync();

            //            var result = await dbc.MyPixes.FromSqlRaw($"select * from MyPix where Notes =@valparam", valparam).ToListAsync(); // works
            //            var result = await dbc.MyPixes.FromSqlRaw($"select * from MyPix where Notes ='Tyler washing carrots in backyard'", valparam).ToListAsync(); // works
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
            var json = JsonConvert.SerializeObject(mypixes);
            var back = JsonConvert.DeserializeObject<MyPix[]>(json);
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
            var json = JsonConvert.SerializeObject(jsonPicMeta);
            var parse = JObject.Parse(jsonPicMeta);
            var id = parse["id"];

        }
    }
}