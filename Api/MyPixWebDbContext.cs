using Client.Shared;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Api
{
    public class MyPixWebDBContext : DbContext
    {
        public virtual DbSet<MyPix> MyPixes { get; set; }
        public virtual DbSet<Thumbs> Thumbs { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            //var fname = @"C:\Users\calvinh\source\repos\CalvinHsiaComWebSite\Api\MyPix.db";
            var fname = @"data\MyPix.db";
            if (!File.Exists(fname))
            {
                throw new FileNotFoundException(fname);
            }
            // Microsoft.Data.Sqlite.SqliteException (0x80004005): SQLite Error 5: 'database is locked'.
            optionsBuilder.UseSqlite($"Filename={fname}"); // write ahead logging: https://www.sqlite.org/wal.html
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MyPix>(entity =>
            {
                entity.ToTable("MyPix"); // needed for sqllite and sqllocaldb : map MyPixes=>MyPix
                entity.Property(e => e.Date).HasColumnType("datetime");
                entity.Property(e => e.FileName)
                    .HasMaxLength(255)
                    .IsUnicode(true);
                entity.Property(e => e.Notes)
                    .HasMaxLength(250)
                    .IsUnicode(true);
            });
        }
    }
}
