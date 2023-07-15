using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Shared
{
    public class MyPix
    {
        public static string[] PathsToPix = {
            string.Empty, // 0 means entire path is in FileName
            @"Pictures\OldPictures",
            @"SkyDrive camera roll" };
        public int Id { get; set; }

        public int PathEnum { get; set; } // 1 =="c:\users\calvinh\OneDrive\Pictures\OldPictures",2= "C:\Users\calvinh\OneDrive\SkyDrive camera roll"
        public string FileName { get; set; } = null!; // relative filename: relative to PathEnum

        public DateTime Date { get; set; } = DateTime.Now;

        public int Rotate { get; set; } = 0;

        public string Notes { get; set; } = string.Empty;
        public string FullFileName => Path.Combine(PathsToPix[PathEnum], FileName);
        //[NotMapped] // tell EF Core that this is not a database property
        //public string Extension => Path.GetExtension(FileName).ToLower();
        public bool IsVideo => IsVideoFile(FileName);
        public static bool IsVideoFile(string fileName)
        {
            return (".avi.mp4.mov.wmv.mpg".Contains(Path.GetExtension(fileName).ToLower())); // select   distinct right(FileName,4)  from MyPix 
        }
        public override string ToString() => $" {Id} {FileName} {Date} {Notes} {PathEnum} {Rotate}";
    }
    public class Thumbs
    {
        public const int CurrentThumbVersion = 1;
        public int Id { get; set; }
        public int ThumbVersion { get; set; }
        public int MyPixId { get; set; }
        public int ThumbSize { get; set; }
        public byte[]? ThumbData { get; set; }
        public override string ToString() => $"Id={Id} MyPixId = {MyPixId} Size = {ThumbSize}";
    }

}
