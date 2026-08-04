using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using WwTool.Common.Models;
using WwTool.Common.Models.Entities;

namespace WwTool.Common.Context
{
    public sealed class AppDbContext : DbContext
    {
        public DbSet<GachaRecord> GachaRecords { get; set; } = null!;
        public DbSet<UserAccount> UserAccounts { get; set; } = null!;
        public DbSet<PlayerBaseInfo> PlayerBaseInfos { get; set; } = null!;
        public DbSet<PlayerMotorData> PlayerMotorData { get; set; } = null!;
        public DbSet<PlayerMusicData> PlayerMusicData { get; set; } = null!;
        public DbSet<PlayerBattlePass> PlayerBattlePasses { get; set; } = null!;
        public DbSet<GachaImportBatch> GachaImportBatches { get; set; } = null!;
        public DbSet<SyncState> SyncStates { get; set; } = null!;
        public DbSet<GuideAccountCredential> GuideAccountCredentials { get; set; } = null!;
        public DbSet<GuidePlayerSnapshot> GuidePlayerSnapshots { get; set; } = null!;
        public DbSet<GuideRoleSnapshot> GuideRoleSnapshots { get; set; } = null!;
        public DbSet<GuideEquippedWeaponSnapshot> GuideEquippedWeaponSnapshots { get; set; } = null!;

        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }
    }
}
