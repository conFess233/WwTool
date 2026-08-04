using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WwTool.Common.Models.Entities;

namespace WwTool.Common.Context.Configurations;

public sealed class GuideAccountCredentialConfiguration : IEntityTypeConfiguration<GuideAccountCredential>
{
    public void Configure(EntityTypeBuilder<GuideAccountCredential> builder)
    {
        builder.ToTable("GuideAccountCredentials");
        builder.HasKey(x => x.CUid);
        builder.Property(x => x.CUid).HasMaxLength(64);
        builder.Property(x => x.EncryptedGuideToken).IsRequired();
    }
}

public sealed class GuidePlayerSnapshotConfiguration : IEntityTypeConfiguration<GuidePlayerSnapshot>
{
    public void Configure(EntityTypeBuilder<GuidePlayerSnapshot> builder)
    {
        builder.ToTable("GuidePlayerSnapshots");
        builder.HasKey(x => x.Uid);
        builder.Property(x => x.Uid).HasMaxLength(32);
        builder.Property(x => x.CUid).HasMaxLength(64);
        builder.Property(x => x.ServerId).HasMaxLength(64);
        builder.HasOne(x => x.UserAccount).WithOne().HasForeignKey<GuidePlayerSnapshot>(x => x.Uid).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Credential).WithMany(x => x.Players).HasForeignKey(x => x.CUid).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class GuideRoleSnapshotConfiguration : IEntityTypeConfiguration<GuideRoleSnapshot>
{
    public void Configure(EntityTypeBuilder<GuideRoleSnapshot> builder)
    {
        builder.ToTable("GuideRoleSnapshots");
        builder.HasKey(x => new { x.Uid, x.RoleGbId });
        builder.Property(x => x.Uid).HasMaxLength(32);
        builder.Property(x => x.RoleGbId).HasMaxLength(64);
        builder.Property(x => x.MayRoleGbId).HasMaxLength(64);
        builder.HasOne(x => x.Player).WithMany(x => x.Roles).HasForeignKey(x => x.Uid).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.Uid, x.SourceOrder });
    }
}

public sealed class GuideEquippedWeaponSnapshotConfiguration : IEntityTypeConfiguration<GuideEquippedWeaponSnapshot>
{
    public void Configure(EntityTypeBuilder<GuideEquippedWeaponSnapshot> builder)
    {
        builder.ToTable("GuideEquippedWeaponSnapshots");
        builder.HasKey(x => new { x.Uid, x.OwnerRoleGbId });
        builder.Property(x => x.Uid).HasMaxLength(32);
        builder.Property(x => x.OwnerRoleGbId).HasMaxLength(64);
        builder.Property(x => x.WeaponGbId).HasMaxLength(64);
        builder.HasOne(x => x.Player).WithMany(x => x.Weapons).HasForeignKey(x => x.Uid).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.Uid, x.SourceOrder });
    }
}
