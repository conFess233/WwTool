using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WwTool.Core.Abstractions;
using WwTool.Core.Abstractions.Platform;
using WwTool.Core.Extensions;
using WwTool.Core.Localization;
using WwTool.Core.Services;
using WwTool.Core.Services.Notices;
using WwTool.UI.Feedback;
using WwTool.UI.Navigation;
using WwTool.UI.Services;
using WwTool.UI.Shell;

namespace WwTool.UI.Pages.Events;

/// <summary>
/// 活动页：游戏内公告（接口文档 <c>docs/API/GameNotice.md</c>）的版本信息、卡池与活动时间线、官方版本日历图、全部公告与详情，
/// 以及国服的前瞻兑换码。先显示缓存，再在后台条件请求刷新（运行中每 30 分钟检查一次）；图片同样每次显示前向服务器校验。
/// </summary>
public sealed partial class EventsViewModel : ObservableObject, INavigationAware, IFabProvider
{
    private const string Icons = "avares://WwTool/Assets/Images/";

    /// <summary>运行中重新获取公告的间隔。</summary>
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(30);

    private readonly GameNoticeService _notices;
    private readonly KuroBbsCodeService _codes;
    private readonly LocalizationService _localization;
    private readonly IConfigService _config;
    private readonly AccountContext _accounts;
    private readonly IAppLauncher _launcher;
    private readonly ClipboardService _clipboard;
    private readonly ErrorReporter _errors;
    private readonly DispatcherTimer _clock;

    private GameNoticeFeed? _feed;
    private LivestreamFeed? _livestream;
    private readonly Dictionary<string, DateTime> _attempted = [];
    private DateTime _codesAttempted = DateTime.MinValue;
    private int _version;
    private bool _loaded;
    private bool _visible;
    private LauncherRegion _shownRegion;
    private string _shownLanguage = string.Empty;
    private string? _calendarUrl;
    private bool _calendarValidated;
    private CancellationTokenSource? _detailLoad;

    public EventsViewModel(
        GameNoticeService notices,
        KuroBbsCodeService codes,
        NoticeImages images,
        LocalizationService localization,
        IConfigService config,
        AccountContext accounts,
        IAppLauncher launcher,
        ClipboardService clipboard,
        ErrorReporter errors,
        INavigationService navigation)
    {
        _notices = notices;
        _codes = codes;
        Images = images;
        _localization = localization;
        _config = config;
        _accounts = accounts;
        _launcher = launcher;
        _clipboard = clipboard;
        _errors = errors;

        FabActions =
        [
            new FabAction("refresh", "Events_Refresh", Icons + "Refresh.png", new AsyncRelayCommand(() => RefreshAsync(force: true)), localization),
            new FabAction("back", "Fab_Back", "Icon.Back", new RelayCommand(() => navigation.GoBack()), localization),
        ];

        // 倒计时、进度与「今天」竖线每 30 秒更新；页面可见时每 5 分钟检查一次是否该重新获取。
        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        int ticks = 0;
        _clock.Tick += (_, _) =>
        {
            UpdateLive();
            if (++ticks % 10 == 0) RefreshIfStale();
        };

        _localization.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LocalizationService.CurrentLanguage) && _loaded)
            {
                Load();
                return;
            }
            if (e.PropertyName == LocalizationService.IndexerName && _feed is not null) Apply(_feed);
        };
        _accounts.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AccountContext.Current) && _loaded && Region != _shownRegion) OnRegionChanged();
        };
    }

    public IReadOnlyList<FabAction> FabActions { get; }
    public NoticeImages Images { get; }

    // ── 区服 ──────────────────────────────────────────────

    public LauncherRegion Region => AnnouncementRegions.Resolve(_config, _accounts, _localization);

    public bool IsChinaRegion
    {
        get => Region == LauncherRegion.China;
        set { if (value) SetRegion(LauncherRegion.China); }
    }

    public bool IsGlobalRegion
    {
        get => Region == LauncherRegion.Global;
        set { if (value) SetRegion(LauncherRegion.Global); }
    }

    private void SetRegion(LauncherRegion region)
    {
        if (_config.User.AnnouncementRegion is not null && Region == region) return;
        if (!AnnouncementRegions.Save(_config, _accounts, _localization, region)) return;
        OnRegionChanged();
    }

    private void OnRegionChanged()
    {
        OnPropertyChanged(nameof(Region));
        OnPropertyChanged(nameof(IsChinaRegion));
        OnPropertyChanged(nameof(IsGlobalRegion));
        if (_loaded) Load();
    }

    // ── 视图切换 ──────────────────────────────────────────

    [ObservableProperty] private bool _isTimelineView = true;
    [ObservableProperty] private bool _isCalendarView;
    [ObservableProperty] private bool _isNoticesView;

    partial void OnIsCalendarViewChanged(bool value)
    {
        if (value) EnsureCalendarImage();
    }

    partial void OnIsNoticesViewChanged(bool value)
    {
        if (value) LoadThumbnails();
    }

    // ── 状态 ──────────────────────────────────────────────

    [ObservableProperty] private bool _hasData;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private bool _failed;
    [ObservableProperty] private string _updatedText = string.Empty;

    public bool ShowLoading => !HasData && IsLoading;
    public bool ShowFailed => !HasData && !IsLoading && Failed;
    public bool ShowEmpty => !HasData && !IsLoading && !Failed;

    partial void OnHasDataChanged(bool value) => NotifyStates();
    partial void OnIsLoadingChanged(bool value) => NotifyStates();
    partial void OnFailedChanged(bool value) => NotifyStates();

    private void NotifyStates()
    {
        OnPropertyChanged(nameof(ShowLoading));
        OnPropertyChanged(nameof(ShowFailed));
        OnPropertyChanged(nameof(ShowEmpty));
    }

    // ── 版本 ──────────────────────────────────────────────

    [ObservableProperty] private string _versionTitle = string.Empty;
    [ObservableProperty] private string _versionLabel = string.Empty;
    [ObservableProperty] private string _versionRange = string.Empty;
    [ObservableProperty] private string _versionRemaining = string.Empty;
    [ObservableProperty] private double _versionProgress;
    [ObservableProperty] private bool _hasVersionRange;

    // ── 时间线 ────────────────────────────────────────────

    public ObservableCollection<TimelineRow> TimelineRows { get; } = [];
    [ObservableProperty] private IReadOnlyList<TimelineTick> _timelineTicks = [];
    [ObservableProperty] private double _todayRatio;
    [ObservableProperty] private bool _showToday;
    [ObservableProperty] private string _unpublishedText = string.Empty;
    [ObservableProperty] private bool _hasUnpublished;
    [ObservableProperty] private bool _hasTimeline;

    private DateTimeOffset _windowStart, _windowEnd;

    // ── 版本日历 ──────────────────────────────────────────

    [ObservableProperty] private Bitmap? _calendarImage;
    [ObservableProperty] private IReadOnlyList<CalendarHotspot> _calendarHotspots = [];
    [ObservableProperty] private bool _hasCalendar;
    [ObservableProperty] private bool _isCalendarLoading;

    public bool ShowCalendarLoading => HasCalendar && CalendarImage is null && IsCalendarLoading;
    public bool ShowCalendarMissing => !HasCalendar || (CalendarImage is null && !IsCalendarLoading);

    partial void OnCalendarImageChanged(Bitmap? value) => NotifyCalendar();
    partial void OnHasCalendarChanged(bool value) => NotifyCalendar();
    partial void OnIsCalendarLoadingChanged(bool value) => NotifyCalendar();

    private void NotifyCalendar()
    {
        OnPropertyChanged(nameof(ShowCalendarLoading));
        OnPropertyChanged(nameof(ShowCalendarMissing));
    }

    // ── 全部公告 ──────────────────────────────────────────

    [ObservableProperty] private IReadOnlyList<NoticeGroup> _noticeGroups = [];
    private List<NoticeItem> _items = [];

    // ── 详情 ──────────────────────────────────────────────

    [ObservableProperty] private NoticeItem? _selected;
    [ObservableProperty] private IReadOnlyList<NoticeBlock>? _selectedBlocks;
    [ObservableProperty] private Bitmap? _selectedBanner;
    [ObservableProperty] private string _selectedTimeHint = string.Empty;
    [ObservableProperty] private bool _hasSelectedTimeHint;

    public bool HasSelection => Selected is not null;

    partial void OnSelectedChanged(NoticeItem? oldValue, NoticeItem? newValue)
    {
        if (oldValue is not null) oldValue.IsSelected = false;
        if (newValue is not null) newValue.IsSelected = true;
        OnPropertyChanged(nameof(HasSelection));
        ShowDetail(newValue);
    }

    // ── 兑换码 ────────────────────────────────────────────

    [ObservableProperty] private IReadOnlyList<RedeemCode> _codesList = [];
    [ObservableProperty] private bool _hasCodes;
    [ObservableProperty] private string _codesTitle = string.Empty;
    [ObservableProperty] private string _codesExpiry = string.Empty;
    [ObservableProperty] private bool _codesExpired;
    [ObservableProperty] private string _previewText = string.Empty;
    [ObservableProperty] private bool _hasPreview;
    [ObservableProperty] private bool _hasLivestream;
    private string? _codesPostUrl;
    private string? _previewPostUrl;

    // ── 导航 ──────────────────────────────────────────────

    public void OnNavigatedTo(object? parameter)
    {
        _visible = true;
        _clock.Start();
        if (!_loaded) Load();
        else if (Region != _shownRegion || GameNoticeService.RequestLanguage(Language) != _shownLanguage) Load();
        else
        {
            UpdateLive();
            RefreshIfStale();
        }
        if (IsCalendarView) EnsureCalendarImage();
    }

    public void OnNavigatedFrom()
    {
        _visible = false;
        _clock.Stop();
    }

    private string Language => _localization.CurrentLanguage.GetCode();

    private string Key(LauncherRegion region) => $"{region}|{GameNoticeService.RequestLanguage(Language)}";

    /// <summary>先显示缓存，再在后台刷新。</summary>
    private void Load()
    {
        _loaded = true;
        int version = ++_version;
        LauncherRegion region = Region;
        _shownRegion = region;
        _shownLanguage = GameNoticeService.RequestLanguage(Language);
        GameNoticeFeed? cached = null;
        try
        {
            cached = _notices.LoadCached(region, Language);
        }
        catch (Exception ex)
        {
            _errors.Report(ex, _localization["Nav_Events"], notifyUser: false);
        }
        Failed = false;
        Selected = null;
        _calendarUrl = null;
        CalendarImage = null;
        if (cached is not null) Apply(cached);
        else Clear();
        LoadCodes(force: false);
        _attempted[Key(region)] = DateTime.UtcNow;
        _ = RefreshCoreAsync(region, version, hasCache: cached is not null);
    }

    private void RefreshIfStale()
    {
        if (!_loaded || !_visible || IsRefreshing) return;
        if (_attempted.TryGetValue(Key(Region), out DateTime at) && DateTime.UtcNow - at < RefreshInterval) return;
        _ = RefreshAsync(force: false);
    }

    private Task RefreshAsync(bool force)
    {
        if (!_loaded || Region != _shownRegion) { Load(); return Task.CompletedTask; }
        _attempted[Key(Region)] = DateTime.UtcNow;
        LoadCodes(force);
        return RefreshCoreAsync(Region, _version, hasCache: _feed is not null);
    }

    [RelayCommand]
    private void Retry() => Load();

    private async Task RefreshCoreAsync(LauncherRegion region, int version, bool hasCache)
    {
        IsLoading = !hasCache;
        IsRefreshing = true;
        GameNoticeFeed? fresh = null;
        try
        {
            string language = Language;
            fresh = await Task.Run(() => _notices.RefreshAsync(region, language));
        }
        catch (Exception ex)
        {
            _errors.Report(ex, _localization["Nav_Events"], notifyUser: false);
        }
        if (version != _version) return;
        IsLoading = false;
        IsRefreshing = false;
        if (fresh is not null)
        {
            // 公告重新获取过：版本日历图也要再向服务器校验一次。
            _calendarValidated = false;
            Apply(fresh);
            if (IsCalendarView) EnsureCalendarImage();
        }
        else if (!hasCache)
        {
            Failed = true;
        }
    }

    private void LoadCodes(bool force)
    {
        if (Region != LauncherRegion.China)
        {
            ApplyLivestream(null);
            return;
        }
        _livestream ??= _codes.LoadCached();
        ApplyLivestream(_livestream);
        if (!force && DateTime.UtcNow - _codesAttempted < RefreshInterval) return;
        _codesAttempted = DateTime.UtcNow;
        _ = Task.Run(() => _codes.RefreshAsync()).ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully && t.Result is { } feed) _livestream = feed;
            Dispatcher.UIThread.Post(() => ApplyLivestream(Region == LauncherRegion.China ? _livestream : null));
        }, TaskScheduler.Default);
    }

    private void Clear()
    {
        _feed = null;
        _items = [];
        HasData = false;
        TimelineRows.Clear();
        TimelineTicks = [];
        HasTimeline = false;
        NoticeGroups = [];
        CalendarHotspots = [];
        HasCalendar = false;
        VersionTitle = VersionLabel = VersionRange = VersionRemaining = UpdatedText = string.Empty;
        HasVersionRange = false;
        HasUnpublished = false;
    }

    // ── 构建 ──────────────────────────────────────────────

    private void Apply(GameNoticeFeed feed)
    {
        _feed = feed;
        string? selectedId = Selected?.Id;
        var previous = _items.ToDictionary(i => i.Id);
        _items = feed.Notices.Select(n =>
        {
            var item = new NoticeItem(n);
            // 保留已加载的缩略图，列表刷新不闪。
            if (previous.TryGetValue(n.Id, out NoticeItem? old) && old.Notice.Banner == n.Banner)
            {
                item.Thumbnail = old.Thumbnail;
                item.ThumbnailRequested = old.ThumbnailRequested;
            }
            return item;
        }).ToList();
        HasData = _items.Count > 0;

        VersionTitle = feed.VersionName ?? _localization["Nav_Events"];
        VersionLabel = feed.VersionNumber is { } number ? string.Format(_localization["Events_VersionLabel"], number) : string.Empty;
        HasVersionRange = feed.VersionStart is not null && feed.VersionEnd is not null;
        VersionRange = HasVersionRange ? Range(feed.VersionStart!.Value, feed.VersionEnd) : string.Empty;
        UpdatedText = string.Format(_localization["Events_Updated"], feed.FetchedAtUtc.ToLocalTime().ToString("MM/dd HH:mm", CultureInfo.InvariantCulture));

        BuildTimeline(feed);
        BuildGroups();
        BuildCalendar(feed);
        UpdateLive();

        NoticeItem? keep = selectedId is null ? null : _items.FirstOrDefault(i => i.Id == selectedId);
        Selected = keep ?? DefaultSelection();
        if (IsNoticesView) LoadThumbnails();
    }

    /// <summary>默认显示当前版本的「版本内容说明」，没有时显示第一条进行中的唤取。</summary>
    private NoticeItem? DefaultSelection()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        // 版本内容说明是最长的一篇更新公告。
        return _items.Where(i => i.IsUpdate && i.Notice.IsActive(now)).OrderByDescending(i => i.Notice.Content.Length).FirstOrDefault()
               ?? _items.FirstOrDefault(i => i.IsGacha && i.Notice.IsActive(now))
               ?? _items.FirstOrDefault();
    }

    private void BuildTimeline(GameNoticeFeed feed)
    {
        TimelineRows.Clear();
        var bars = _items.Where(i => (i.IsGacha || i.IsEvent) && i.Notice.End is not null).ToList();
        DateTimeOffset start = feed.VersionStart ?? (bars.Count > 0 ? bars.Min(b => b.Notice.Start) : DateTimeOffset.UtcNow);
        DateTimeOffset end = feed.VersionEnd ?? (bars.Count > 0 ? bars.Max(b => b.Notice.End!.Value) : start.AddDays(42));
        if (end <= start) end = start.AddDays(42);
        _windowStart = start;
        _windowEnd = end;
        double total = (end - start).TotalSeconds;
        double Ratio(DateTimeOffset t) => Math.Clamp((t - start).TotalSeconds / total, 0, 1);

        // 只放与版本周期有交集的条目；常驻玩法（没有结束时间）不画。
        bars = bars.Where(b => b.Notice.End!.Value > start && b.Notice.Start < end).ToList();
        (string Lane, string Title, Func<NoticeItem, bool> Match)[] lanes =
        [
            ("character", _localization["Events_PoolCharacter"], i => i.Notice.Pool == GachaPoolType.Character),
            ("weapon", _localization["Events_PoolWeapon"], i => i.Notice.Pool == GachaPoolType.Weapon),
            ("reverb", _localization["Events_PoolReverb"], i => i.Notice.Pool is GachaPoolType.CharacterReverb or GachaPoolType.WeaponReverb),
            ("event", _localization["Events_GroupEvent"], i => i.IsEvent),
        ];
        foreach (var lane in lanes)
        {
            var rows = bars.Where(lane.Match).OrderBy(b => b.Notice.Start).ThenBy(b => b.Notice.End).ToList();
            if (rows.Count == 0) continue;
            TimelineRows.Add(new TimelineRow(lane.Title));
            foreach (NoticeItem item in rows)
            {
                TimelineRows.Add(new TimelineRow(item, lane.Lane, Ratio(item.Notice.Start), Ratio(item.Notice.End!.Value))
                {
                    RunsPastEnd = item.Notice.End!.Value > end.AddHours(1),
                });
            }
        }
        HasTimeline = TimelineRows.Count > 0;

        // 刻度：起点、每 7 天、终点。
        var ticks = new List<TimelineTick>();
        DateTime localStart = start.ToLocalTime().Date;
        for (DateTime day = localStart.AddDays(7); new DateTimeOffset(day) < end.AddDays(-2); day = day.AddDays(7))
            ticks.Add(new TimelineTick(Ratio(new DateTimeOffset(day)), day.ToString("MM/dd", CultureInfo.InvariantCulture), false));
        ticks.Insert(0, new TimelineTick(0, start.ToLocalTime().ToString("MM/dd", CultureInfo.InvariantCulture), true));
        ticks.Add(new TimelineTick(1, end.ToLocalTime().ToString("MM/dd", CultureInfo.InvariantCulture), true));
        TimelineTicks = ticks;

        int unpublished = feed.UnpublishedCount;
        HasUnpublished = unpublished > 0;
        UnpublishedText = HasUnpublished ? string.Format(_localization["Events_Unpublished"], unpublished) : string.Empty;
    }

    private void BuildGroups()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        IEnumerable<NoticeItem> Ordered(IEnumerable<NoticeItem> items) => items
            .OrderBy(i => i.Notice.IsEnded(now) ? 1 : 0)
            .ThenByDescending(i => i.Notice.Start);
        (NoticeKind Kind, string Key)[] kinds =
        [
            (NoticeKind.Update, "Events_GroupUpdate"),
            (NoticeKind.Gacha, "Events_GroupGacha"),
            (NoticeKind.Event, "Events_GroupEvent"),
            (NoticeKind.Shop, "Events_GroupShop"),
            (NoticeKind.Info, "Events_GroupInfo"),
        ];
        NoticeGroups = kinds
            .Select(k => new NoticeGroup(_localization[k.Key], Ordered(_items.Where(i => i.Notice.Kind == k.Kind)).ToList()))
            .Where(g => g.Items.Count > 0)
            .ToList();
    }

    private void BuildCalendar(GameNoticeFeed feed)
    {
        NoticeCalendar? calendar = feed.Calendar;
        HasCalendar = calendar?.BackgroundUrl is not null;
        if (calendar is null)
        {
            CalendarHotspots = [];
            return;
        }
        var byId = _items.ToDictionary(i => i.Id);
        CalendarHotspots = calendar.Nodes.Select(n =>
        {
            NoticeItem? item = n.Published && byId.TryGetValue(n.NoticeId, out NoticeItem? found) ? found : null;
            string tip = item?.Title ?? _localization["Events_NotPublished"];
            return new CalendarHotspot(n.NoticeId, item, n.X1 / 100, n.Y1 / 100, n.X2 / 100, n.Y2 / 100, tip);
        }).ToList();
        if (calendar.BackgroundUrl != _calendarUrl)
        {
            _calendarUrl = calendar.BackgroundUrl;
            _calendarValidated = false;
            CalendarImage = null;
        }
    }

    /// <summary>版本日历图：先显示缓存，再向服务器校验（每次重新获取公告后都校验一次），变了就换。</summary>
    private void EnsureCalendarImage()
    {
        if (_calendarUrl is not { } url || (_calendarValidated && CalendarImage is not null)) return;
        _calendarValidated = true;
        int version = _version;
        IsCalendarLoading = true;
        _ = LoadCalendarAsync(url, version);
    }

    private async Task LoadCalendarAsync(string url, int version)
    {
        try
        {
            await Images.LoadAsync(url, 1440, bitmap =>
            {
                if (version == _version && url == _calendarUrl) CalendarImage = bitmap;
            }, force: true);
        }
        catch (Exception ex)
        {
            _errors.Report(ex, _localization["Events_ViewCalendar"], notifyUser: false);
            _calendarValidated = false;
        }
        finally
        {
            if (version == _version) IsCalendarLoading = false;
        }
    }

    private void LoadThumbnails()
    {
        foreach (NoticeItem item in _items)
        {
            if (item.ThumbnailRequested || item.Notice.Banner is null) continue;
            item.ThumbnailRequested = true;
            _ = Images.LoadAsync(item.Notice.Banner, 240, bitmap => item.Thumbnail = bitmap);
        }
    }

    // ── 详情 ──────────────────────────────────────────────

    private void ShowDetail(NoticeItem? item)
    {
        _detailLoad?.Cancel();
        SelectedBanner = null;
        if (item is null)
        {
            SelectedBlocks = null;
            HasSelectedTimeHint = false;
            return;
        }
        SelectedBlocks = NoticeHtml.Parse(item.Notice.Content);
        UpdateTimeHint(item);
        var cts = new CancellationTokenSource();
        _detailLoad = cts;
        if (item.Notice.Banner is { } banner)
        {
            _ = Images.LoadAsync(banner, 880, bitmap =>
            {
                if (ReferenceEquals(Selected, item)) SelectedBanner = bitmap;
            }, token: cts.Token);
        }
    }

    private void UpdateTimeHint(NoticeItem item)
    {
        GameNotice n = item.Notice;
        string hint = n.Kind is NoticeKind.Gacha or NoticeKind.Event or NoticeKind.Shop && !n.TimeFromContent
            ? _localization["Events_TimeFromShelf"]
            : n.TimeText is { Length: > 0 } text && !IsChineseUi ? string.Format(_localization["Events_OriginalTime"], text) : string.Empty;
        if (_shownRegion == LauncherRegion.Global && n.TimeFromContent)
            hint = hint.Length > 0 ? hint + "\n" + _localization["Events_ServerTimeHint"] : _localization["Events_ServerTimeHint"];
        SelectedTimeHint = hint;
        HasSelectedTimeHint = hint.Length > 0;
    }

    private bool IsChineseUi => Language == "zh-Hans";

    [RelayCommand]
    private void Select(NoticeItem? item)
    {
        if (item is not null) Selected = item;
    }

    [RelayCommand]
    private void SelectRow(TimelineRow? row)
    {
        if (row?.Item is { } item) Selected = item;
    }

    [RelayCommand]
    private void SelectHotspot(CalendarHotspot? spot)
    {
        if (spot?.Item is { } item) Selected = item;
    }

    [RelayCommand]
    private void OpenLink(string? url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
            _launcher.OpenUrl(uri);
    }

    // ── 兑换码 ────────────────────────────────────────────

    private void ApplyLivestream(LivestreamFeed? feed)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        // 兑换码过期后就没用了，不再占位置。
        RedeemCodeSet? set = feed?.Codes.FirstOrDefault(c => !c.IsExpired(now));
        HasCodes = set is not null;
        CodesList = set?.Codes.Select(c => new RedeemCode(c)).ToList() ?? [];
        CodesTitle = set is null ? string.Empty
            : set.Version is { } v ? string.Format(_localization["Events_CodesVersion"], v) : _localization["Events_Codes"];
        CodesExpired = set?.IsExpired(now) == true;
        CodesExpiry = set?.ExpiresAt is { } at
            ? string.Format(_localization[CodesExpired ? "Events_CodesExpired" : "Events_CodesExpires"], at.ToLocalTime().ToString("MM/dd HH:mm", CultureInfo.InvariantCulture))
            : string.Empty;
        _codesPostUrl = set?.PostUrl;

        LivestreamPreview? preview = feed?.Preview;
        // 直播开始后 3 小时内仍显示，之后由兑换码接替。
        HasPreview = preview is not null && preview.StartsAt.AddHours(3) > now;
        PreviewText = HasPreview
            ? string.Format(_localization["Events_Preview"], preview!.Version ?? string.Empty, preview.StartsAt.ToLocalTime().ToString("MM/dd HH:mm", CultureInfo.InvariantCulture)).Trim()
            : string.Empty;
        _previewPostUrl = preview?.PostUrl;
        HasLivestream = HasCodes || HasPreview;
    }

    [RelayCommand]
    private async Task CopyCodeAsync(RedeemCode? code)
    {
        if (code is null) return;
        try
        {
            await _clipboard.SetTextAsync(code.Code);
        }
        catch (Exception ex)
        {
            _errors.Report(ex, _localization["Events_Copy"]);
            return;
        }
        foreach (RedeemCode other in CodesList) other.IsCopied = ReferenceEquals(other, code);
        await Task.Delay(TimeSpan.FromSeconds(2));
        code.IsCopied = false;
    }

    [RelayCommand]
    private void OpenCodesPost() => OpenLink(_codesPostUrl);

    [RelayCommand]
    private void OpenPreviewPost() => OpenLink(_previewPostUrl);

    // ── 实时文字 ──────────────────────────────────────────

    private void UpdateLive()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string separator = _localization["Events_ListSeparator"];
        foreach (NoticeItem item in _items)
        {
            GameNotice n = item.Notice;
            item.Featured = string.Join(separator, n.Featured);
            item.KindText = _localization[n.Kind switch
            {
                NoticeKind.Update => "Events_KindUpdate",
                NoticeKind.Gacha => n.Pool switch
                {
                    GachaPoolType.Weapon => "Events_PoolWeapon",
                    GachaPoolType.CharacterReverb or GachaPoolType.WeaponReverb => "Events_PoolReverb",
                    _ => "Events_PoolCharacter",
                },
                NoticeKind.Event => "Events_KindEvent",
                NoticeKind.Shop => "Events_KindShop",
                _ => "Events_KindInfo",
            }];
            item.DateText = n.End is null && n.EndText is { } rel
                ? Range(n.Start, null) + " – " + string.Format(_localization["Events_UntilVersion"], rel.Split('~').Last().Trim())
                : Range(n.Start, n.End);
            item.IsActive = n.IsActive(now);
            item.IsEnded = n.IsEnded(now);
            item.IsUpcoming = n.IsUpcoming(now);
            item.StatusText = n.IsUpcoming(now) ? string.Format(_localization["Events_StartsIn"], Duration(n.Start - now))
                : n.End is not { } end ? (n.EndText is null ? _localization["Events_Permanent"] : _localization["Events_Ongoing"])
                : end <= now ? _localization["Events_Ended"]
                : string.Format(_localization["Events_Remaining"], Duration(end - now));
        }

        if (_feed is { VersionStart: { } vs, VersionEnd: { } ve } && ve > vs)
        {
            VersionProgress = Math.Clamp((now - vs).TotalSeconds / (ve - vs).TotalSeconds, 0, 1);
            VersionRemaining = now >= ve ? _localization["Events_VersionEnded"]
                : now < vs ? string.Format(_localization["Events_StartsIn"], Duration(vs - now))
                : string.Format(_localization["Events_VersionRemaining"], Duration(ve - now));
        }
        else
        {
            VersionProgress = 0;
            VersionRemaining = string.Empty;
        }

        if (_windowEnd > _windowStart)
        {
            ShowToday = now >= _windowStart && now <= _windowEnd;
            TodayRatio = Math.Clamp((now - _windowStart).TotalSeconds / (_windowEnd - _windowStart).TotalSeconds, 0, 1);
        }
        if (Selected is { } selected) UpdateTimeHint(selected);
    }

    private static string Range(DateTimeOffset start, DateTimeOffset? end)
    {
        string a = start.ToLocalTime().ToString("MM/dd HH:mm", CultureInfo.InvariantCulture);
        return end is { } e ? a + " – " + e.ToLocalTime().ToString("MM/dd HH:mm", CultureInfo.InvariantCulture) : a;
    }

    /// <summary>「3 天 5 小时」「5 小时」「12 分钟」。</summary>
    private string Duration(TimeSpan span)
    {
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;
        if (span.TotalDays >= 1)
            return span.Hours > 0
                ? string.Format(_localization["Events_DaysHours"], (int)span.TotalDays, span.Hours)
                : string.Format(_localization["Events_Days"], (int)span.TotalDays);
        if (span.TotalHours >= 1) return string.Format(_localization["Events_Hours"], (int)span.TotalHours);
        return string.Format(_localization["Events_Minutes"], Math.Max(1, (int)Math.Ceiling(span.TotalMinutes)));
    }
}
