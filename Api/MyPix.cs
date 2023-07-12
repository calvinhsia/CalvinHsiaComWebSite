using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Grpc.Core.Metadata;

namespace Api
{
    public class MyPix
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

    public class MyPixWebDBContext : DbContext
    {
        public virtual DbSet<MyPix> MyPixes { get; set; }
        public virtual DbSet<Thumbs> Thumbs { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("MyPix.db");
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MyPix>(entity =>
            {
                entity.ToTable("MyPix"); // needed for sqllite and sqllocaldb : map MyPixes=>MyPix
            });
        }
    }
}
