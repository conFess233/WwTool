using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WwTool.Common.Models;
using WwTool.Common.Models.Entities;

namespace WwTool.Common.Context.Configurations;

public sealed class GachaRecordConfiguration : IEntityTypeConfiguration<GachaRecord>
{
    public void Configure(EntityTypeBuilder<GachaRecord> builder)
    {
        builder.ToTable("GachaRecords");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Uid).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ResourceType).HasMaxLength(64);
        builder.Property(x => x.NameSnapshot).HasColumnName("Name").HasMaxLength(256);
        builder.Property(x => x.Time).HasMaxLength(64).IsRequired();
        builder.Property(x => x.StableFingerprint).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => new { x.Uid, x.PoolType, x.SourceOrder }).IsUnique();
        builder.HasIndex(x => new { x.Uid, x.PoolType, x.SourceOccurredAtUtc });
        builder.HasIndex(x => new { x.Uid, x.PoolType, x.StableFingerprint }).IsUnique();
        builder.HasIndex(x => x.ImportBatchId);
        builder.HasOne(x => x.UserAccount).WithMany(x => x.GachaRecords).HasForeignKey(x => x.Uid).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.ImportBatch).WithMany(x => x.Records).HasForeignKey(x => x.ImportBatchId).OnDelete(DeleteBehavior.Restrict);
    }
}
