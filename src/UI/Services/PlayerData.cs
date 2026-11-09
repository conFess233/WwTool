using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WwTool.Core.Abstractions;
using WwTool.Core.Enums;
using WwTool.Core.Localization;
using WwTool.Core.Models.Domain;
using WwTool.UI.Feedback;

namespace WwTool.UI.Services;

/// <summary>
/// 当前账号的游戏数据快照（基础信息、摩托、车载音乐、电台），首页、探索、摩托页共用。
/// 账号切换时清空并重读；“同步”从服务器拉取后读回本地快照。只在 UI 线程读写。
/// </summary>
public sealed partial class PlayerData : ObservableObject
{
    private readonly AccountContext _accounts;
    private readonly IUserDataService _userData;
    private readonly IGetDataService _getData;
    private readonly LocalizationService _localization;
    private readonly IUIStateService _uiState;
    private readonly ErrorReporter _errors;
    private CancellationTokenSource? _loadCts;
    private string? _loadedUid;

    public PlayerData(
        AccountContext accounts,
        IUserDataService userData,
        IGetDataService getData,
        LocalizationService localization,
        IUIStateService uiState,
        ErrorReporter errors)
    {
        _accounts = accounts;
        _userData = userData;
        _getData = getData;
        _localization = localization;
        _uiState = uiState;
        _errors = errors;
        accounts.PropertyChanged += OnAccountChanged;
    }

    /// <summary>快照替换（含清空）后触发。</summary>
    public event EventHandler? SnapshotChanged;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSnapshot))]
    private PlayerSnapshot? _snapshot;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isSyncing;

    public bool HasSnapshot => Snapshot?.Base is not null;

    /// <summary>当前账号还没读过时读一次。</summary>
    public Task EnsureLoadedAsync() =>
        _accounts.Current is { } account && _loadedUid != account.Uid ? ReloadAsync() : Task.CompletedTask;

    public async Task ReloadAsync()
    {
        string? uid = _accounts.Current?.Uid;
        if (uid is null) return;
        _loadCts?.Cancel();
        var cts = new CancellationTokenSource();
        _loadCts = cts;
        IsLoading = true;
        try
        {
            PlayerSnapshot? snapshot = await _userData.LoadRoleSnapshotAsync(uid, cts.Token);
            if (cts.IsCancellationRequested || _accounts.Current?.Uid != uid) return;
            _loadedUid = uid;
            Replace(snapshot);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // 切换账号时取消旧的读取。
        }
        catch (Exception ex)
        {
            _errors.Report(ex, _localization["Msg_ReadingLocalRole"]);
        }
        finally
        {
            if (_loadCts == cts) IsLoading = false;
        }
    }

    /// <summary>从服务器同步当前账号的游戏数据（需要登录凭据），再读回本地快照。</summary>
    public async Task SyncAsync()
    {
        if (IsSyncing) return;
        string? uid = _accounts.Current?.Uid;
        if (uid is null)
        {
            _uiState.ShowToast(_localization["Toast_Error"], _localization["Msg_NoAccountSelected"], NotificationType.Error);
            return;
        }
        IsSyncing = true;
        try
        {
            string? credential = await _userData.GetCredentialAsync(uid);
            await _getData.SyncAllUserDataAsync(uid, credential);
            await _accounts.RefreshAsync();
            if (_accounts.Current?.Uid != uid) return;
            await ReloadAsync();
            if (HasSnapshot)
            {
                _uiState.ShowToast(new NotificationRequest
                {
                    Title = _localization["Toast_Success"],
                    Message = _localization["Msg_FetchRoleSuccess"],
                    Type = NotificationType.Success,
                    Priority = NotificationPriority.Important,
                    Source = "PlayerData",
                    DedupeKey = "player-sync",
                });
            }
            else
            {
                _uiState.ShowToast(_localization["Toast_Warning"], _localization["Msg_FetchRoleEmpty"], NotificationType.Warning);
            }
        }
        catch (Exception ex)
        {
            _errors.Report(ex, _localization["Home_Sync"]);
        }
        finally
        {
            IsSyncing = false;
        }
    }

    private void OnAccountChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AccountContext.Current)) return;
        // 刷新账号列表会换成新的同 UID 对象；UID 没变时快照不必重读。
        if (_accounts.Current?.Uid == _loadedUid) return;
        _loadCts?.Cancel();
        _loadedUid = null;
        Replace(null);
        if (_accounts.Current is not null) _ = ReloadAsync();
    }

    private void Replace(PlayerSnapshot? snapshot)
    {
        Snapshot = snapshot;
        SnapshotChanged?.Invoke(this, EventArgs.Empty);
    }
}
