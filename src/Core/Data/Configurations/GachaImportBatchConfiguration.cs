using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WwTool.Core.Data.Entities;

namespace WwTool.Core.Data.Configurations;

public sealed class GachaImportBatchConfiguration : IEntityTypeConfiguration<GachaImportBatch>
{
    public void Configure(EntityTypeBuilder<GachaImportBatch> builder)
    {
        builder.ToTable("GachaImportBatches");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Uid).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Source).HasMaxLength(32).IsRequired();
        builder.Property(x => x.SourceCursor).HasMaxLength(512);
        builder.HasOne(x => x.UserAccount).WithMany(x => x.GachaImportBatches).HasForeignKey(x => x.Uid).OnDelete(DeleteBehavior.Cascade);
    }
}
