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

    /// <summary>一次「获取云端数据」成功结束，参数为新增条数（动效「刷新完成」据此显示「已是最新」或新增条数）。</summary>
    public event EventHandler<int>? SyncCompleted;

    /// <summary>用当前链接从服务端同步全部卡池，再重新统计。成功时返回新增条数，未执行或失败时返回 null。</summary>
    public async Task<int?> SyncAsync()
    {
        if (IsSyncing) return null;
        if (string.IsNullOrWhiteSpace(QueryUrl))
        {
            _uiState.ShowToast(_localization["Toast_Warning"], _localization["Msg_ApiUrlNotFound"], NotificationType.Warning);
            return null;
        }
        string? uid = Uid;
        if (uid is null)
        {
            _uiState.ShowToast(_localization["Toast_Warning"], _localization["Msg_UidEmptyForGacha"], NotificationType.Warning);
            return null;
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
            SyncCompleted?.Invoke(this, added);
            return added;
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
            return null;
        }
        finally
        {
            IsSyncing = false;
        }
    }

    /// <summary>
    /// 拖拽导入（动效「拖拽导入」）：拖入的文字或文件（Client.log 等）里找最近一条抽卡链接，导入后立即获取云端数据。
    /// 返回新增条数；没找到链接或获取失败时返回 null（已提示）。
    /// </summary>
    public async Task<int?> ImportDroppedAsync(string? text, string? filePath)
    {
        if (filePath is not null)
        {
            try
            {
                text = await ReadSharedAsync(filePath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _errors.Report(ex, _localization["Stat_AutoImportUrl"]);
                return null;
            }
        }
        string? url = LatestUrlIn(text);
        if (url is null || !ImportUrl(url)) return null;
        return await SyncAsync();
    }

    /// <summary>游戏运行时日志被占用写入，按共享方式读取。</summary>
    private static async Task<string> ReadSharedAsync(string path)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    /// <summary>文本里最后一条有效的抽卡链接（日志从旧到新，最后一条最新）。</summary>
    private static string? LatestUrlIn(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        foreach (System.Text.RegularExpressions.Match m in UrlPattern.Matches(text).Reverse())
        {
            if (ExtractUrl(m.Value, out _) is { } url) return url;
        }
        return ExtractUrl(text, out _);
    }

    /// <summary>从游戏日志读取最近一次打开抽卡记录时的链接，并切换到链接所属账号。</summary>
    public async Task AutoImportUrlAsync()
    {
        string? url = await ReadUrlFromGameLogAsync();
        if (url is not null && ImportUrl(url)) ShowImported();
    }

    /// <summary>链接导入成功的提示。</summary>
    public void ShowImported()
    {
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

    /// <summary>从游戏日志读取最近的抽卡链接；失败时提示（<paramref name="quiet"/> 时只记日志）并返回 null。</summary>
    public async Task<string?> ReadUrlFromGameLogAsync(bool quiet = false)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_config.User.GamePath))
            {
                throw new WwToolGamePathException(_localization["Msg_NoGamePath"]);
            }
            return await _logLocator.FindLatestQueryUrlAsync(
                _config.User.GamePath,
                _config.App.GameLogPath,
                _config.App.GameLogFile,
                _config.User.SearchGachaApiUrl ?? string.Empty);
        }
        catch (Exception ex)
        {
            _errors.Report(ex, _localization["Stat_AutoImportUrl"], notifyUser: !quiet);
            return null;
        }
    }

    /// <summary>
    /// 「获取记录」：已有链接时直接获取云端数据；没有链接时先静默从游戏日志找链接，
    /// 找不到再由 <paramref name="askForUrl"/> 弹出导入链接窗口（返回是否已导入），导入成功后接着获取。
    /// </summary>
    public async Task<int?> GetRecordsAsync(Func<Task<bool>> askForUrl)
    {
        if (IsSyncing) return null;
        if (string.IsNullOrWhiteSpace(QueryUrl))
        {
            string? url = string.IsNullOrWhiteSpace(_config.User.GamePath) ? null : await ReadUrlFromGameLogAsync(quiet: true);
            bool imported = url is not null && ImportUrl(url);
            if (!imported && !await askForUrl()) return null;
            ShowImported();
        }
        return await SyncAsync();
    }

    /// <summary>从一段文字（链接或整行日志）中取出抽卡链接；不是有效链接时返回 null。</summary>
    public static string? ExtractUrl(string? text, out string? playerId)
    {
        playerId = null;
        if (string.IsNullOrWhiteSpace(text)) return null;
        string url = UrlPattern.Match(text) is { Success: true } m ? m.Value : text.Trim();
        if (!GachaUrlParser.TryParse(url, out var request, out _) || request is null || string.IsNullOrEmpty(request.PlayerId)) return null;
        playerId = request.PlayerId;
        return url;
    }

    /// <summary>
    /// 使用一条抽卡链接（可以是整行日志，自动提取其中的 URL），并切换到链接所属账号。
    /// 链接不能识别服务器时使用 <paramref name="fallbackRegion"/>（并记为设置中的选择），否则用设置中的服务器。
    /// 链接无效时提示并返回 false。
    /// </summary>
    public bool ImportUrl(string text, GachaServerRegion? fallbackRegion = null)
    {
        string? url = ExtractUrl(text, out string? playerId);
        if (url is null || playerId is null)
        {
            _uiState.ShowToast(_localization["Toast_Warning"], _localization["Msg_ApiUrlNotFound"], NotificationType.Warning);
            return false;
        }

        QueryUrl = url;
        if (GachaServerDetector.TryDetect(url, out GachaServerRegion detected))
        {
            Region = detected;
        }
        else
        {
            if (fallbackRegion is { } chosen) _config.User.GachaServerRegion = chosen;
            Region = _config.User.GachaServerRegion;
        }
        if (Uid != playerId) _accounts.Select(playerId);
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
