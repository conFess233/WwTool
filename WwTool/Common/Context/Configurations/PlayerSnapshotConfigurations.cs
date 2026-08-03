using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WwTool.Common.Models;
using WwTool.Common.Models.Entities;

namespace WwTool.Common.Context.Configurations;

public sealed class PlayerBaseInfoConfiguration : IEntityTypeConfiguration<PlayerBaseInfo>
{
    public void Configure(EntityTypeBuilder<PlayerBaseInfo> builder)
    {
        builder.ToTable("PlayerBaseInfos");
        builder.HasKey(x => x.Uid);
        builder.HasOne(x => x.UserAccount).WithOne().HasForeignKey<PlayerBaseInfo>(x => x.Uid).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PlayerMotorDataConfiguration : IEntityTypeConfiguration<PlayerMotorData>
{
    public void Configure(EntityTypeBuilder<PlayerMotorData> builder)
    {
        builder.ToTable("PlayerMotorData");
        builder.HasKey(x => x.Uid);
        builder.HasOne(x => x.UserAccount).WithOne().HasForeignKey<PlayerMotorData>(x => x.Uid).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PlayerBattlePassConfiguration : IEntityTypeConfiguration<PlayerBattlePass>
{
    public void Configure(EntityTypeBuilder<PlayerBattlePass> builder)
    {
        builder.ToTable("PlayerBattlePasses");
        builder.HasKey(x => x.Uid);
        builder.HasOne(x => x.UserAccount).WithOne().HasForeignKey<PlayerBattlePass>(x => x.Uid).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PlayerMusicDataConfiguration : IEntityTypeConfiguration<PlayerMusicData>
{
    public void Configure(EntityTypeBuilder<PlayerMusicData> builder)
    {
        builder.ToTable("PlayerMusicData");
        builder.HasKey(x => new { x.Uid, x.AlbumId });
        builder.Ignore(x => x.IconPath);
        builder.HasOne(x => x.UserAccount).WithMany().HasForeignKey(x => x.Uid).OnDelete(DeleteBehavior.Cascade);
    }
}
