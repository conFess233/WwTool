using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WwTool.Core.Abstractions;
using WwTool.Core.Enums;
using WwTool.Core.Localization;
using WwTool.Core.Models.Domain;
using WwTool.UI.Feedback;
using WwTool.UI.Navigation;
using WwTool.UI.Services;
using WwTool.UI.Shell;

namespace WwTool.UI.Pages.Accounts;

/// <summary>账号页的一张账号卡。</summary>
/// <summary>网格末尾的“添加游戏账号”格。</summary>
public sealed class AddAccountTile
{
    public static AddAccountTile Instance { get; } = new();
}

public sealed class AccountCard
{
    public required AccountSummary Account { get; init; }
    public required string Name { get; init; }
    public required string UidText { get; init; }
    public required bool IsCurrent { get; init; }
    public required string Server { get; init; }
    public required string CreateDate { get; init; }
    public required string ActiveDays { get; init; }
    public required string LastSynced { get; init; }

    public string IconPath => Account.IconPath;
    public bool IsNotCurrent => !IsCurrent;
    public string AutomationId => "account-" + Account.Uid;
}

/// <summary>
/// 账号数据页（从页头账号胶囊进入）：本地账号卡片、切换、删除、添加。
/// 创建日期与活跃天数来自各账号的本地游戏数据快照。
/// </summary>
public sealed partial class AccountsViewModel : ObservableObject, INavigationAware, IFabProvider
{
    public const string PageKey = "accounts";

    private readonly AccountContext _accounts;
    private readonly AccountActions _actions;
    private readonly IUserDataService _userData;
    private readonly LocalizationService _localization;
    private readonly IUIStateService _uiState;
    private readonly ErrorReporter _errors;
    private readonly Dictionary<string, PlayerBaseSnapshot?> _facts = new(StringComparer.Ordinal);
    private bool _isActive;
    private int _version;

    private readonly PlayerData _player;

    /// <summary>当前账号的登录已失效（同步时授权被拒）。</summary>
    public bool IsCurrentExpired => _accounts.Current is { } a && _player.AuthExpiredUid == a.Uid;

    public string ExpiredTitle => string.Format(_localization["Accounts_ExpiredTitle"],
        _accounts.Current is { Name: { Length: > 0 } name } ? name : _accounts.Current?.MaskedUid ?? string.Empty);

    /// <summary>空状态「登录失效」：重新登录后立即同步。</summary>
    public IAsyncRelayCommand ReloginCommand { get; }

    /// <summary>空状态「登录失效」：移除账号（确认后删除）。</summary>
    public IAsyncRelayCommand RemoveExpiredCommand { get; }

    private void NotifyExpired()
    {
        OnPropertyChanged(nameof(IsCurrentExpired));
        OnPropertyChanged(nameof(ExpiredTitle));
    }

    public AccountsViewModel(
        AccountContext accounts,
        AccountActions actions,
        IUserDataService userData,
        LocalizationService localization,
        IUIStateService uiState,
        ErrorReporter errors,
        INavigationService navigation,
        PlayerData player)
    {
        _player = player;
        player.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlayerData.AuthExpiredUid)) NotifyExpired();
        };
        accounts.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AccountContext.Current)) NotifyExpired();
        };
        ReloginCommand = new AsyncRelayCommand(async () =>
        {
            if (await actions.AddAccountAsync()) await player.SyncAsync();
        });
        RemoveExpiredCommand = new AsyncRelayCommand(async () =>
        {
            if (accounts.Current is { } current) await actions.DeleteAsync(current);
        });
        // 悬浮按钮：收起态加号，展开为「刷新数据」（主，重读账号列表）与「返回上一页」。
        FabActions =
        [
            new FabAction("refresh", "Fab_RefreshData", "avares://WwTool/Assets/Images/Refresh.png", RefreshCommand, localization),
            new FabAction("back", "Fab_Back", "Icon.Back", new RelayCommand(() => navigation.GoBack()), localization),
        ];
        _accounts = accounts;
        _actions = actions;
        _userData = userData;
        _localization = localization;
        _uiState = uiState;
        _errors = errors;
        accounts.PropertyChanged += OnAccountsChanged;
        accounts.Accounts.CollectionChanged += OnAccountListChanged;
        _localization.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == LocalizationService.IndexerName) Rebuild();
        };
    }

    /// <summary>账号卡（<see cref="AccountCard"/>）加末尾一格 <see cref="AddAccountTile"/>。</summary>
    public ObservableCollection<object> Cards { get; } = [];

    public IReadOnlyList<FabAction> FabActions { get; }

    [ObservableProperty] private string _summary = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public void OnNavigatedTo(object? parameter)
    {
        _isActive = true;
        _ = LoadFactsAsync();
    }

    public void OnNavigatedFrom() => _isActive = false;

    [RelayCommand]
    private void Switch(AccountCard card) => _accounts.Select(card.Account.Uid);

    [RelayCommand]
    private async Task DeleteAsync(AccountCard card) => await _actions.DeleteAsync(card.Account);

    [RelayCommand]
    private async Task AddAsync()
    {
        if (await _actions.AddAccountAsync()) await LoadFactsAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            await _accounts.RefreshAsync();
            await LoadFactsAsync();
            _uiState.ShowToast(new NotificationRequest
            {
                Title = _localization["Toast_Success"],
                Message = string.Format(_localization["Msg_ReadAccountsSuccess"], _accounts.Accounts.Count),
                Type = NotificationType.Success,
                Priority = NotificationPriority.Important,
                Source = "Accounts",
                DedupeKey = "accounts-refresh",
            });
        }
        catch (Exception ex)
        {
            _errors.Report(ex, _localization["Index_RefreshAccountList"]);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnAccountsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AccountContext.Current)) Rebuild();
    }

    // 刷新账号列表会先清空再逐个加入，合并成一次重建。
    private void OnAccountListChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_rebuildQueued) return;
        _rebuildQueued = true;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _rebuildQueued = false;
            Rebuild();
            if (_isActive) _ = LoadFactsAsync();
        });
    }

    private bool _rebuildQueued;

    // 逐个读取各账号的本地快照；读完一次性重建卡片。
    private async Task LoadFactsAsync()
    {
        int version = ++_version;
        string[] uids = _accounts.Accounts.Select(a => a.Uid).ToArray();
        var facts = new Dictionary<string, PlayerBaseSnapshot?>(StringComparer.Ordinal);
        try
        {
            foreach (string uid in uids)
            {
                facts[uid] = (await _userData.LoadRoleSnapshotAsync(uid))?.Base;
            }
        }
        catch (Exception ex)
        {
            _errors.Report(ex, _localization["Msg_ReadingLocalRole"], notifyUser: false);
        }
        if (version != _version) return;
        _facts.Clear();
        foreach (var pair in facts) _facts[pair.Key] = pair.Value;
        Rebuild();
    }

    private void Rebuild()
    {
        string none = _localization["Common_Unavailable"];
        Cards.Clear();
        foreach (AccountSummary account in _accounts.Accounts)
        {
            _facts.TryGetValue(account.Uid, out PlayerBaseSnapshot? b);
            Cards.Add(new AccountCard
            {
                Account = account,
                Name = string.IsNullOrWhiteSpace(account.Name) ? account.Uid : account.Name,
                UidText = "UID " + MaskUid(account.Uid),
                IsCurrent = account.Uid == _accounts.Current?.Uid,
                Server = string.IsNullOrWhiteSpace(account.Region) ? none : account.Region,
                CreateDate = b is null || b.CreatTime <= 0 ? none : FromTimestamp(b.CreatTime).LocalDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ActiveDays = b is null ? none : b.ActiveDays.ToString(CultureInfo.CurrentCulture),
                LastSynced = account.LastSyncedAtUtc is { } at ? FormatSyncTime(at) : _localization["Guide_NeverSynced"],
            });
        }
        int count = Cards.Count;
        Cards.Add(AddAccountTile.Instance);
        Summary = string.Format(_localization["Accounts_Summary"], count);
    }

    private string FormatSyncTime(DateTimeOffset utc)
    {
        DateTime local = utc.ToLocalTime().DateTime;
        return local.Date == DateTime.Today
            ? string.Format(_localization["Accounts_Today"], local.ToString("HH:mm", CultureInfo.InvariantCulture))
            : local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    private static DateTimeOffset FromTimestamp(long value) =>
        value > 20_000_000_000 ? DateTimeOffset.FromUnixTimeMilliseconds(value) : DateTimeOffset.FromUnixTimeSeconds(value);

    private static string MaskUid(string uid) => uid.Length <= 4 ? uid : uid[..4] + new string('●', uid.Length - 4);
}
