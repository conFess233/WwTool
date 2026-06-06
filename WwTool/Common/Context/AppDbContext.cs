using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using WwTool.Common.Models;

namespace WwTool.Common.Context
{
    public class AppDbContext : DbContext
    {
        public DbSet<GachaRecord> GachaRecords { get; set; }
        public DbSet<UserAccount> UserAccounts { get; set; }
        public DbSet<PlayerBaseInfo> PlayerBaseInfos { get; set; }
        public DbSet<PlayerMotorData> PlayerMotorData { get; set; }
        public DbSet<PlayerMusicData> PlayerMusicData { get; set; }
        public DbSet<PlayerBattlePass> PlayerBattlePasses { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // 确保本地数据文件夹存在
            Directory.CreateDirectory("Local/Data");
            string dbPath = Path.Combine("Local/Data", "LocalData.db");

            // 配置使用 SQLite
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 配置 One-to-Many 关系及级联删除
            modelBuilder.Entity<GachaRecord>()
                .HasOne(g => g.UserAccount)
                .WithMany(u => u.GachaRecords)
                .HasForeignKey(g => g.Uid)
                .OnDelete(DeleteBehavior.Cascade); // 开启级联删除

            modelBuilder.Entity<PlayerMusicData>()
                .HasKey(m => new { m.Uid, m.AlbumId });

            modelBuilder.Entity<PlayerBaseInfo>()
                .HasOne(p => p.UserAccount)
                .WithOne()
                .HasForeignKey<PlayerBaseInfo>(p => p.Uid)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PlayerMotorData>()
                .HasOne(m => m.UserAccount)
                .WithOne()
                .HasForeignKey<PlayerMotorData>(m => m.Uid)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PlayerBattlePass>()
                .HasOne(b => b.UserAccount)
                .WithOne()
                .HasForeignKey<PlayerBattlePass>(b => b.Uid)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PlayerMusicData>()
                .HasOne(m => m.UserAccount)
                .WithMany()
                .HasForeignKey(m => m.Uid)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
