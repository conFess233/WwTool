using System.ComponentModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using WwTool.Core.Abstractions;
using WwTool.Core.Enums;
using WwTool.Core.Exceptions;
using WwTool.Core.Extensions;
using WwTool.Core.Infrastructure;
using WwTool.Core.Localization;
using WwTool.Core.Models.Config;
using WwTool.Core.Services.Gacha;
using WwTool.UI.Feedback;
using WwTool.UI.Services;

namespace WwTool.UI.Pages.Pulls;

/// <summary>
/// 抽卡统计各页面共享的状态：当前账号的统计快照、抽卡链接与服务器、读取 / 同步 / 导入操作。
/// 总览、卡池记录与详细图表三个视图模型只读取这里，彼此不直接依赖。只在 UI 线程使用。
/// </summary>
public sealed partial class PullsSession : ObservableObject
{
    private static readonly Regex UrlPattern = new(@"https?://[^\s""'\n\r]+", RegexOptions.Compiled);

    private readonly IPullsService _pulls;
    private readonly IGachaStatisticsService _statistics;
    private readonly AccountContext _accounts;
    private readonly IConfigService _config;
    private readonly IGachaLogLocator _logLocator;
    private readonly IUIStateService _uiState;
    private readonly ErrorReporter _errors;
    private readonly LocalizationService _localization;
    private CancellationTokenSource? _loadCts;
    private int _loadVersion;

    public PullsSession(
        IPullsService pulls,
        IGachaStatisticsService statistics,
        AccountContext accounts,
        IConfigService config,
        IGachaLogLocator logLocator,
        IUIStateService uiState,
        ErrorReporter errors,
        LocalizationService localization)
    {
        _pulls = pulls;
        _statistics = statistics;
        _accounts = accounts;
        _config = config;
        _logLocator = logLocator;
        _uiState = uiState;
        _errors = errors;
        _localization = localization;
        _region = config.User.GachaServerRegion;

        _accounts.PropertyChanged += OnAccountChanged;
        _config.User.PropertyChanged += OnUserConfigChanged;
    }

    /// <summary>当前账号的统计；没有账号时为 null，未加载完时保留上一份。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasData))]
    private PullsSnapshot? _snapshot;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isSyncing;

    /// <summary>最近一次加载失败的提示；成功后清空。</summary>
    [ObservableProperty]
    private string? _loadError;

    /// <summary>当前抽卡链接（已从整行日志中提取出 URL）。</summary>
    [ObservableProperty]
    private string? _queryUrl;

    /// <summary>同步使用的服务器：链接能识别时以链接为准，否则用设置里的选择。</summary>
    [ObservableProperty]
    private GachaServerRegion _region;

    public bool HasData => Snapshot is { IsEmpty: false };

    public string? Uid => _accounts.Current?.Uid;

    private string LanguageCode => _config.User.AppLanguage.GetCode();

    /// <summary>快照被替换（加载、同步、清空）后触发。</summary>
    public event EventHandler? SnapshotReplaced;

    /// <summary>首次进入页面时调用：还没有快照就读取一次本地记录。</summary>
    public Task EnsureLoadedAsync() => Snapshot is null || Snapshot.Uid != Uid ? ReloadAsync() : Task.CompletedTask;

    /// <summary>读取当前账号的本地记录并统计。切换账号时自动调用；较早的未完成读取会被取消。</summary>
    public async Task ReloadAsync(bool showMessage = false)
    {
        string? uid = Uid;
        _loadCts?.Cancel();
        int version = ++_loadVersion;
        if (uid is null)
        {
            Replace(null);
            return;
        }

        using var cts = new CancellationTokenSource();
        _loadCts = cts;
        IsLoading = true;
        try
        {
            PullsSnapshot snapshot = await _pulls.LoadAsync(uid, LanguageCode, cts.Token);
            if (version != _loadVersion) return;
            LoadError = null;
            Replace(snapshot);
            if (showMessage)
            {
                _uiState.ShowToast(new NotificationRequest
                {
                    Title = _localization[snapshot.IsEmpty ? "Toast_Info" : "Toast_Success"],
                    Message = _localization[snapshot.IsEmpty ? "Msg_ActionNoNewData" : "Msg_LoadedLocalGacha"],
                    Type = snapshot.IsEmpty ? NotificationType.Info : NotificationType.Success,
                    Priority = NotificationPriority.Important,
                    Source = "Pulls",
                    DedupeKey = "load-local",
                });
            }
        }
        catch (Exception ex) when (version == _loadVersion)
        {
            LoadError = _errors.Report(ex, _localization["Stat_LoadLocalData"], notifyUser: showMessage)?.Message;
        }
        finally
        {
            if (version == _loadVersion)
            {
                IsLoading = false;
                _loadCts = null;
            }
        }
    }

    /// <summary>用当前链接从服务端同步全部卡池，再重新统计。</summary>
    public async Task SyncAsync()
    {
        if (IsSyncing) return;
        if (string.IsNullOrWhiteSpace(QueryUrl))
        {
            _uiState.ShowToast(_localization["Toast_Warning"], _localization["Msg_ApiUrlNotFound"], NotificationType.Warning);
            return;
        }
        string? uid = Uid;
        if (uid is null)
        {
            _uiState.ShowToast(_localization["Toast_Warning"], _localization["Msg_UidEmptyForGacha"], NotificationType.Warning);
            return;
        }

        IsSyncing = true;
        const string task = "gacha-sync";
        var progress = new Progress<CardPoolType>(pool => _uiState.ShowToast(new NotificationRequest
        {
            Title = _localization["Stat_GetCloudData"],
            Message = string.Format(_localization["Msg_SyncingPool"], _localization[PoolNames.TitleKey(pool)]),
            Priority = NotificationPriority.Important,
            Source = "Pulls",
            TaskKey = task,
            Progress = ((int)pool - 1) / (double)Enum.GetValues<CardPoolType>().Length,
        }));
        try
        {
            int added = await _pulls.SyncFromServerAsync(uid, QueryUrl, Region, LanguageCode, progress);
            _uiState.ShowToast(new NotificationRequest
            {
                Title = _localization[added == 0 ? "Toast_Info" : "Toast_Success"],
                Message = added == 0 ? _localization["Msg_ActionNoNewData"] : string.Format(_localization["Msg_GachaSyncAdded"], added),
                Type = added == 0 ? NotificationType.Info : NotificationType.Success,
                Priority = NotificationPriority.Important,
                Source = "Pulls",
                TaskKey = task,
                IsTaskCompleted = true,
            });
            if (Uid == uid) await ReloadAsync();
        }
        catch (Exception ex)
        {
            // 把进度 Toast 原地改为失败结果（日志由 ErrorReporter 记录）。
            ErrorReport? report = _errors.Report(ex, _localization["Stat_GetCloudData"], notifyUser: false);
            _uiState.ShowToast(new NotificationRequest
            {
                Title = report?.Title ?? _localization["Stat_GetCloudData"],
                Message = report?.Message ?? _localization["Msg_ActionNoNewData"],
                Type = report?.Type ?? NotificationType.Info,
                Priority = NotificationPriority.Important,
                Source = "Pulls",
                TaskKey = task,
                IsTaskCompleted = true,
            });
        }
        finally
        {
            IsSyncing = false;
        }
    }

    /// <summary>从游戏日志读取最近一次打开抽卡记录时的链接，并切换到链接所属账号。</summary>
    public async Task AutoImportUrlAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_config.User.GamePath))
            {
                throw new WwToolGamePathException(_localization["Msg_NoGamePath"]);
            }
            string url = await _logLocator.FindLatestQueryUrlAsync(
                _config.User.GamePath,
                _config.App.GameLogPath,
                _config.App.GameLogFile,
                _config.User.SearchGachaApiUrl ?? string.Empty);
            if (!ImportUrl(url)) return;
            _uiState.ShowToast(new NotificationRequest
            {
                Title = _localization["Msg_AutoImportSuccessTitle"],
                Message = _localization["Msg_AutoImportSuccess"],
                Type = NotificationType.Success,
                Priority = NotificationPriority.Important,
                Source = "Pulls",
                DedupeKey = "auto-import",
            });
        }
        catch (Exception ex)
        {
            _errors.Report(ex, _localization["Stat_AutoImportUrl"]);
        }
    }

    /// <summary>
    /// 使用一条抽卡链接（可以是整行日志，自动提取其中的 URL），并切换到链接所属账号。链接无效时提示并返回 false。
    /// </summary>
    public bool ImportUrl(string text)
    {
        string url = UrlPattern.Match(text) is { Success: true } m ? m.Value : text.Trim();
        if (!GachaUrlParser.TryParse(url, out var request, out _) || request is null || string.IsNullOrEmpty(request.PlayerId))
        {
            _uiState.ShowToast(_localization["Toast_Warning"], _localization["Msg_ApiUrlNotFound"], NotificationType.Warning);
            return false;
        }

        QueryUrl = url;
        Region = GachaServerDetector.TryDetect(url, out GachaServerRegion detected) ? detected : _config.User.GachaServerRegion;
        if (Uid != request.PlayerId) _accounts.Select(request.PlayerId);
        return true;
    }

    /// <summary>清空当前显示的统计（不删除本地记录）。</summary>
    public void ClearDisplay()
    {
        _loadCts?.Cancel();
        ++_loadVersion;
        IsLoading = false;
        Replace(Uid is null ? null : PullsSnapshot.Empty(Uid, _statistics, LanguageCode));
        _uiState.ShowToast(new NotificationRequest
        {
            Title = _localization["Toast_Success"],
            Message = _localization["Msg_ClearedData"],
            Type = NotificationType.Success,
            Priority = NotificationPriority.Important,
            Source = "Pulls",
            DedupeKey = "cleared",
        });
    }

    private void Replace(PullsSnapshot? snapshot)
    {
        Snapshot = snapshot;
        SnapshotReplaced?.Invoke(this, EventArgs.Empty);
    }

    private void OnAccountChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AccountContext.Current)) return;
        OnPropertyChanged(nameof(Uid));
        _ = ReloadAsync();
    }

    private void OnUserConfigChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 五星名称按界面语言取自资料，语言变化后重新统计。
        if (e.PropertyName == nameof(UserConfig.AppLanguage) && Snapshot is not null) _ = ReloadAsync();
        if (e.PropertyName == nameof(UserConfig.GachaServerRegion) && !GachaServerDetector.TryDetect(QueryUrl, out _))
        {
            Region = _config.User.GachaServerRegion;
        }
    }
}
