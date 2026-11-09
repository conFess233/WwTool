using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using WwTool.Core.Abstractions;
using WwTool.Core.Models.Domain;

namespace WwTool.UI.Services;

/// <summary>
/// 当前账号。页头账号胶囊显示并切换它；各页面跟随 <see cref="Current"/> 变化重新加载。
/// 选中的 UID 记在 <c>UserConfig.LastUserId</c>，下次启动恢复。只在 UI 线程读写。
/// </summary>
public sealed partial class AccountContext : ObservableObject
{
    private readonly IUserDataService _userData;
    private readonly IConfigService _config;
    private readonly ILoggerService _logger;

    public AccountContext(IUserDataService userData, IConfigService config, ILoggerService logger)
    {
        _userData = userData;
        _config = config;
        _logger = logger;
        _userData.AccountDeleted += (_, e) => Dispatcher.UIThread.Post(() => OnAccountDeleted(e.Uid));
    }

    public ObservableCollection<AccountSummary> Accounts { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAccount), nameof(DisplayName), nameof(HasLevel))]
    private AccountSummary? _current;

    public bool HasAccount => Current is not null;

    /// <summary>有等级信息（登录同步过）时显示 LV。</summary>
    public bool HasLevel => Current is { Level: > 0 };

    /// <summary>账号名；没有名字（只导入过抽卡链接）时显示 UID。</summary>
    public string DisplayName => Current is null ? string.Empty : string.IsNullOrWhiteSpace(Current.Name) ? Current.Uid : Current.Name;

    /// <summary>从数据库重新读取账号列表，并恢复（或保持）当前账号。</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AccountSummary> accounts = await _userData.ListAccountsAsync(cancellationToken);
        string? preferred = Current?.Uid ?? _config.User.LastUserId;
        Accounts.Clear();
        foreach (AccountSummary account in accounts) Accounts.Add(account);
        Current = accounts.FirstOrDefault(x => x.Uid == preferred) ?? accounts.FirstOrDefault();
        _logger.Info($"已读取 {accounts.Count} 个本地账号。");
    }

    /// <summary>
    /// 切换到 <paramref name="uid"/>。列表里还没有（例如刚导入一个新账号的抽卡链接）时先加入一个只有 UID 的条目。
    /// </summary>
    public void Select(string uid)
    {
        AccountSummary? account = Accounts.FirstOrDefault(x => x.Uid == uid);
        if (account is null)
        {
            account = new AccountSummary { Uid = uid };
            Accounts.Add(account);
        }
        Current = account;
    }

    partial void OnCurrentChanged(AccountSummary? value)
    {
        if (value is not null && _config.User.LastUserId != value.Uid) _config.User.LastUserId = value.Uid;
    }

    private void OnAccountDeleted(string uid)
    {
        foreach (AccountSummary account in Accounts.Where(x => x.Uid == uid).ToArray()) Accounts.Remove(account);
        if (Current?.Uid == uid) Current = Accounts.FirstOrDefault();
    }
}
