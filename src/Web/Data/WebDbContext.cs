using Microsoft.EntityFrameworkCore;

namespace WwTool.Web.Data;

/// <summary>
/// Web 专属数据：站点账号、UID 绑定与转移通知、数据来源标记。
/// 抽卡记录与账号资料仍存在 Core 的 <c>AppDbContext</c>（按 UID 分区，沿用桌面端的 SourceOrder 规则），
/// 两库之间只通过 UID 关联。这里不保存任何游戏凭据。
/// </summary>
public sealed class WebDbContext(DbContextOptions<WebDbContext> options) : DbContext(options)
{
    public DbSet<SiteUser> Users => Set<SiteUser>();
    public DbSet<UidBinding> UidBindings => Set<UidBinding>();
    public DbSet<UidTransferNotice> TransferNotices => Set<UidTransferNotice>();
    public DbSet<UidDataSource> DataSources => Set<UidDataSource>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<SiteUser>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.DisplayName).HasMaxLength(64);
            e.Property(x => x.UserName).HasMaxLength(32);
            e.Property(x => x.NormalizedUserName).HasMaxLength(32);
            e.HasIndex(x => x.NormalizedUserName).IsUnique();
            e.Property(x => x.PasswordHash).HasMaxLength(128);
            e.Property(x => x.RecoveryCodeHash).HasMaxLength(64);
            e.Property(x => x.SecurityStamp).HasMaxLength(32);
        });
        b.Entity<UidBinding>(e =>
        {
            e.HasKey(x => x.Uid);
            e.Property(x => x.Uid).HasMaxLength(16);
            e.Property(x => x.Region).HasMaxLength(16);
            e.Property(x => x.Method).HasMaxLength(16);
            e.HasIndex(x => x.UserId);
            e.HasOne<SiteUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<UidTransferNotice>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.MaskedUid).HasMaxLength(16);
            e.HasIndex(x => x.UserId);
            e.HasOne<SiteUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<UidDataSource>(e =>
        {
            e.HasKey(x => new { x.Uid, x.Kind });
            e.Property(x => x.Uid).HasMaxLength(16);
            e.Property(x => x.Kind).HasMaxLength(16);
            e.Property(x => x.Source).HasMaxLength(16);
        });
    }
}

public sealed class SiteUser
{
    public Guid Id { get; set; }
    /// <summary>登录名，保留注册时的大小写；唯一性按 <see cref="NormalizedUserName"/>（大写）判断。</summary>
    public string UserName { get; set; } = string.Empty;
    public string NormalizedUserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    /// <summary>PBKDF2 哈希，见 <c>PasswordHasher</c>。为空表示不能用密码登录（开发账号）。</summary>
    public string PasswordHash { get; set; } = string.Empty;
    /// <summary>一次性恢复码的 SHA-256；用过或重新生成后替换。</summary>
    public string? RecoveryCodeHash { get; set; }
    /// <summary>改密码、重设密码时更换，使其他设备上的登录 Cookie 失效。</summary>
    public string SecurityStamp { get; set; } = string.Empty;
    public int FailedLogins { get; set; }
    public DateTimeOffset? LockoutUntil { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
}

/// <summary>UID 当前属于哪个站点账号。用该 UID 的抽卡链接或游戏账号登录都可以把绑定转移到自己名下。</summary>
public sealed class UidBinding
{
    public string Uid { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    /// <summary>抽卡服务器：<c>global</c> 或 <c>china</c>。</summary>
    public string Region { get; set; } = string.Empty;
    /// <summary>最近一次绑定或验证的方式：<c>gacha</c>（抽卡链接，后端验证）或 <c>game</c>（游戏账号登录，浏览器上报）。</summary>
    public string Method { get; set; } = string.Empty;
    public DateTimeOffset BoundAt { get; set; }
    public DateTimeOffset LastVerifiedAt { get; set; }
}

/// <summary>绑定被其他站点账号转走时，给原账号的提示。</summary>
public sealed class UidTransferNotice
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string MaskedUid { get; set; } = string.Empty;
    public DateTimeOffset At { get; set; }
}

/// <summary>
/// 数据来源标记：<c>gacha</c> 由后端用链接直接拉取（server，可信）；<c>profile</c> 由浏览器转交（client，不可信，
/// 只给本人看，禁止进入公开聚合）；导入文件为 import。
/// </summary>
public sealed class UidDataSource
{
    public string Uid { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
}
