using Api;
using Client.Shared;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace TestProject1
{
    [TestClass]
    public class UnitTest1
    {
        //        public TestContext TestContext { get; set; }
        [TestMethod]
        public void TestMethod1()
        {
            var rand = new Random(1);
            var wh = new WordHandler(rand);
            for (int i = 0; i < 10; i++)
            {
                (var randword, var grid, var gfilled) = wh.CreateGrid();
                Console.WriteLine($"RandWord {randword} {grid}  {gfilled}");
            }
        }
        [TestMethod]
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


            var dbc = new MyPixWebDBContext();
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
        public async Task TestJson()
        {
            await Task.Yield();
            var dbc = new MyPixWebDBContext();
            var mypixes = await dbc.MyPixes.FromSql($"Select * from MyPix where Notes like '%carrots%'").ToListAsync();
            var json = JsonConvert.SerializeObject(mypixes);
            var back = JsonConvert.DeserializeObject<MyPix[]>(json);
        }
        [TestMethod]
        public async Task TestRawData()
        {
            await Task.Yield();
            var conn = new SqliteConnection(@$"Filename = data\Mypix.db");
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

        }
    }
}