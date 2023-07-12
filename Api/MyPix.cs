using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace Api
{
    public class MyPixWeb
    {
        public static string[] PathsToPix = { @"\Pictures\OldPictures", @"\SkyDrive camera roll" };
        public int Id { get; set; }

        public int PathEnum { get; set; } // 1 =="c:\users\calvinh\OneDrive\Pictures\OldPictures",2= "C:\Users\calvinh\OneDrive\SkyDrive camera roll"
        public string FileName { get; set; } = null!; // relative filename: relative to PathEnum

        public DateTime Date { get; set; } = DateTime.Now;

        public int Rotate { get; set; } = 0;

        public string Notes { get; set; } = string.Empty;
        public string FullFileName => Path.Combine(PathsToPix[PathEnum], FileName);
    }

    public class MyPixWebDBContext : DbContext
    {

    }
}
