using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WwTool.Common.Models;
using WwTool.Common.Models.Entities;

namespace WwTool.Common.Context.Configurations;

public sealed class SyncStateConfiguration : IEntityTypeConfiguration<SyncState>
{
    public void Configure(EntityTypeBuilder<SyncState> builder)
    {
        builder.ToTable("SyncStates");
        builder.HasKey(x => new { x.Uid, x.DataKind, x.ScopeKey });
        builder.Property(x => x.Uid).HasMaxLength(32);
        builder.Property(x => x.DataKind).HasMaxLength(32);
        builder.Property(x => x.ScopeKey).HasMaxLength(64);
        builder.Property(x => x.LastCursor).HasMaxLength(512);
        builder.HasOne(x => x.UserAccount).WithMany(x => x.SyncStates).HasForeignKey(x => x.Uid).OnDelete(DeleteBehavior.Cascade);
    }
}
