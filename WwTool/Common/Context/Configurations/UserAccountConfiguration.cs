using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WwTool.Common.Models;
using WwTool.Common.Models.Entities;

namespace WwTool.Common.Context.Configurations;

public sealed class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        builder.ToTable("UserAccounts");
        builder.HasKey(x => x.Uid);
        builder.Property(x => x.Uid).HasMaxLength(32);
        builder.Property(x => x.Region).HasMaxLength(32);
        builder.Property(x => x.Name).HasMaxLength(128);
        builder.Ignore(x => x.IconPath);
    }
}
