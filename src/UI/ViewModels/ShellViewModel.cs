using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WwTool.Core.Abstractions;
using WwTool.Core.Localization;
using WwTool.Core.Models.Config;
using WwTool.Core.Services;
using WwTool.Core.Extensions;
using WwTool.UI.Feedback;
using WwTool.UI.Navigation;
using WwTool.UI.Services;
using WwTool.UI.Shell;

namespace WwTool.UI.ViewModels;

/// <summary>
/// 主窗口：悬浮岛导航项、当前页面与页名，以及启动初始化状态。
/// </summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly INavigationService _navigation;
    private readonly LocalizationService _localization;
    private readonly IConfigService _config;
    private readonly LauncherBackgroundService _launcherBackground;
    private readonly ILoggerService _logger;

    public ShellViewModel(
        INavigationService navigation,
        LocalizationService localization,
        FabMenuViewModel fab,
        DialogService dialogs,
        IUIStateService uiState,
        AccountContext accounts,
        IConfigService config,
        LauncherBackgroundService launcherBackground,
        ILoggerService logger)
    {
        _config = config;
        _launcherBackground = launcherBackground;
        _logger = logger;
        config.User.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(UserConfig.UseCustomBackground) or nameof(UserConfig.CustomBackgroundPath))
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(LoadBackground);
            }
            if (e.PropertyName is nameof(UserConfig.BackgroundPeekTransparency))
            {
                OnPropertyChanged(nameof(BackgroundPeekOpacity));
            }
            if (e.PropertyName is nameof(UserConfig.UseDynamicBackground))
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(UpdateVideo);
            }
        };
        Accounts = accounts;
        _navigation = navigation;
        _localization = localization;
        LoadBackground();
        // 运行中定期检查启动器背景是否过期（游戏更新版本后不必重启程序）。
        _launcherClock = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
        _launcherClock.Tick += (_, _) => RefreshLauncherIfStale();
        _launcherClock.Start();
        Fab = fab;
        Dialogs = dialogs;
        UiState = uiState;

        string modifier = OperatingSystem.IsMacOS() ? "⌘" : "Ctrl";
        int number = 0;
        foreach (PageDescriptor page in navigation.Pages.Where(p => p.IsTopLevel && p.ShowInIsland))
        {
            // 主导航依次为 1…5，设置（分隔线后）为「,」。
            string keyText = page.StartsGroup ? "," : (++number).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var item = new NavItem(page.Key, page.TitleKey, page.Icon, localization, page.StartsGroup, $"{modifier} + {keyText}");
            item.PropertyChanged += OnNavItemChanged;
            NavItems.Add(item);
        }

        navigation.Navigated += OnNavigated;
        localization.PropertyChanged += OnLocalizationChanged;
    }

    /// <summary>未选自定义图片时使用的启动器背景（国际服按界面语言 → 国服 → 默认底纹）。</summary>
    private bool UsesLauncherBackground =>
        !_config.User.UseCustomBackground || string.IsNullOrWhiteSpace(_config.User.CustomBackgroundPath) || !File.Exists(_config.User.CustomBackgroundPath);

    /// <summary>
    /// 窗口背景图：设置「窗口背景」选自定义且图片可读时为自定义图片；否则为启动器背景首帧图（先用缓存，后台刷新）；
    /// 都没有时为空，显示默认底纹。
    /// </summary>
    public Avalonia.Media.Imaging.Bitmap? BackgroundImage
    {
        get => _backgroundImage;
        private set
        {
            if (ReferenceEquals(_backgroundImage, value)) return;
            Avalonia.Media.Imaging.Bitmap? old = _backgroundImage;
            _backgroundImage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasBackgroundImage));
            old?.Dispose();
        }
    }

    private Avalonia.Media.Imaging.Bitmap? _backgroundImage;

    public bool HasBackgroundImage => _backgroundImage is not null;

    /// <summary>鼠标停在空白处查看背景时界面组件的不透明度；设置为 0% 时为 1（不变）。</summary>
    public double BackgroundPeekOpacity => 1 - _config.User.BackgroundPeekTransparency / 100.0;

    /// <summary>启动器标语图：与背景同比例的透明整幅叠加层，只随启动器背景显示。</summary>
    public Avalonia.Media.Imaging.Bitmap? BackgroundSlogan
    {
        get => _backgroundSlogan;
        private set
        {
            if (ReferenceEquals(_backgroundSlogan, value)) return;
            Avalonia.Media.Imaging.Bitmap? old = _backgroundSlogan;
            _backgroundSlogan = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasBackgroundSlogan));
            old?.Dispose();
        }
    }

    private Avalonia.Media.Imaging.Bitmap? _backgroundSlogan;

    public bool HasBackgroundSlogan => _backgroundSlogan is not null;

    /// <summary>
    /// 动态背景视频（本地缓存的启动器背景视频）：仅 Windows、设置开启「动态背景」且正在使用启动器背景时有值；
    /// 是否真正播放还取决于窗口状态，由主窗口决定。
    /// </summary>
    [ObservableProperty]
    private string? _backgroundVideo;

    /// <summary>本次运行中播放失败过：不再尝试，保持静态首帧图。</summary>
    private bool _videoFailed;

    private bool WantsVideo =>
        OperatingSystem.IsWindows() && _config.User.UseDynamicBackground && UsesLauncherBackground && !_videoFailed;

    private int _videoVersion;

    /// <summary>按当前设置决定是否需要视频；需要时先用已缓存的，没有再后台下载（约 11MB）。</summary>
    private void UpdateVideo()
    {
        int version = ++_videoVersion;
        if (!WantsVideo)
        {
            BackgroundVideo = null;
            return;
        }
        string? cached = _launcherBackground.LoadCachedVideo();
        if (cached is not null)
        {
            BackgroundVideo = cached;
            return;
        }
        _ = DownloadVideoAsync(version);
    }

    private async Task DownloadVideoAsync(int version)
    {
        string? path;
        try
        {
            path = await Task.Run(() => _launcherBackground.EnsureVideoAsync());
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            path = null;
        }
        if (version == _videoVersion && WantsVideo && path is not null) BackgroundVideo = path;
    }

    /// <summary>视频解码失败（如系统缺少 H.264 解码器）：记录日志，本次运行不再播放。</summary>
    public void ReportVideoFailure(Exception ex)
    {
        _logger.Warn($"动态背景播放失败，改用静态背景：{ex.GetType().Name} {ex.Message}");
        _videoFailed = true;
        BackgroundVideo = null;
    }

    /// <summary>每次重新选择背景时递增，丢弃过期的异步结果。</summary>
    private int _backgroundVersion;

    /// <summary>上次开始从接口获取启动器背景的语言与时间；同一语言在 <see cref="LauncherRefreshInterval"/> 内不重复请求。</summary>
    private string? _launcherAttemptedLanguage;
    private DateTime _launcherAttemptedAt;

    private static readonly TimeSpan LauncherRefreshInterval = TimeSpan.FromMinutes(30);

    private readonly Avalonia.Threading.DispatcherTimer _launcherClock;

    private bool IsLauncherStale(string language) =>
        _launcherAttemptedLanguage != language || DateTime.UtcNow - _launcherAttemptedAt >= LauncherRefreshInterval;

    /// <summary>使用启动器背景且上次获取已超过间隔时，在后台重新获取；背景没变时不会重新解码。</summary>
    private void RefreshLauncherIfStale()
    {
        if (!UsesLauncherBackground) return;
        string language = _localization.CurrentLanguage.GetCode();
        if (!IsLauncherStale(language)) return;
        _launcherAttemptedLanguage = language;
        _launcherAttemptedAt = DateTime.UtcNow;
        _ = RefreshLauncherAsync(language, _backgroundVersion);
    }

    private string? _shownLauncherImage;
    private string? _shownLauncherSlogan;

    private void LoadBackground()
    {
        int version = ++_backgroundVersion;
        if (!UsesLauncherBackground)
        {
            _shownLauncherImage = _shownLauncherSlogan = null;
            BackgroundSlogan = null;
            BackgroundImage = Decode(_config.User.CustomBackgroundPath!, 2560);
            UpdateVideo();
            return;
        }

        // 先显示上次缓存（若有），再后台刷新；刷新全部失败时保留缓存，没有缓存则为默认底纹。
        LauncherBackgroundFiles? cached = _launcherBackground.LoadCached();
        if (cached is null)
        {
            _shownLauncherImage = _shownLauncherSlogan = null;
            BackgroundImage = null;
            BackgroundSlogan = null;
        }
        else
        {
            _ = ApplyLauncherAsync(cached, version);
        }
        UpdateVideo();

        string language = _localization.CurrentLanguage.GetCode();
        if (!IsLauncherStale(language)) return;
        _launcherAttemptedLanguage = language;
        _launcherAttemptedAt = DateTime.UtcNow;
        _ = RefreshLauncherAsync(language, version);
    }

    private async Task RefreshLauncherAsync(string language, int version)
    {
        LauncherBackgroundFiles? files;
        try
        {
            files = await Task.Run(() => _launcherBackground.RefreshAsync(language));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            files = null;
        }
        if (files is null) return;
        if (version != _backgroundVersion) return;
        await ApplyLauncherAsync(files, version);
        // 背景换了（如新版本）时视频地址也随之变化，重新确认视频。
        // 首次启动时刷新前还没有视频地址，刷新后补上。
        if (BackgroundVideo is null || _launcherBackground.LoadCachedVideo() != BackgroundVideo) UpdateVideo();
    }

    /// <summary>后台解码后在界面线程替换；文件没变时不重新解码。</summary>
    private async Task ApplyLauncherAsync(LauncherBackgroundFiles files, int version)
    {
        bool sameImage = files.ImagePath == _shownLauncherImage && _backgroundImage is not null;
        bool sameSlogan = files.SloganPath == _shownLauncherSlogan;
        if (sameImage && sameSlogan) return;

        var (image, slogan) = await Task.Run(() => (
            sameImage ? null : Decode(files.ImagePath, 2560),
            sameSlogan || files.SloganPath is null ? null : Decode(files.SloganPath, 1920)));

        if (version != _backgroundVersion || !UsesLauncherBackground)
        {
            image?.Dispose();
            slogan?.Dispose();
            return;
        }
        if (!sameImage)
        {
            if (image is null) { slogan?.Dispose(); return; }
            BackgroundImage = image;
            _shownLauncherImage = files.ImagePath;
        }
        if (!sameSlogan)
        {
            BackgroundSlogan = slogan;
            _shownLauncherSlogan = slogan is null ? null : files.SloganPath;
        }
    }

    /// <summary>按宽度解码，大图不至于占满内存；读不了返回 null。</summary>
    private static Avalonia.Media.Imaging.Bitmap? Decode(string path, int width)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            return Avalonia.Media.Imaging.Bitmap.DecodeToWidth(stream, width);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>当前账号（页头账号胶囊）。</summary>
    public AccountContext Accounts { get; }

    /// <summary>右下悬浮按钮，动作随当前页面变化。</summary>
    public FabMenuViewModel Fab { get; }

    /// <summary>窗口内对话框覆盖层。</summary>
    public DialogService Dialogs { get; }

    /// <summary>当前语言的文案（供窗口代码使用）。</summary>
    public string Text(string key) => _localization[key];

    /// <summary>Toast 栈。</summary>
    public IUIStateService UiState { get; }

    /// <summary>悬浮岛导航项，顺序同页面登记顺序。</summary>
    public ObservableCollection<NavItem> NavItems { get; } = [];

    /// <summary>当前页面视图模型，由 <see cref="ViewLocator"/> 生成视图。</summary>
    [ObservableProperty]
    private object? _currentPage;

    /// <summary>页头左侧的页名。</summary>
    [ObservableProperty]
    private string _pageTitle = string.Empty;

    /// <summary>后台数据（数据库、静态资料）初始化完成前为 false，页面区域显示加载状态。</summary>
    [ObservableProperty]
    private bool _isReady;

    /// <summary>账号数据页打开时，页头账号胶囊显示选中态。</summary>
    [ObservableProperty]
    private bool _isAccountsPage;

    /// <summary>首页打开时显示首页专属底纹装饰。</summary>
    [ObservableProperty]
    private bool _isHomePage;

    [RelayCommand]
    private void OpenAccounts() => _navigation.NavigateTo(Pages.Accounts.AccountsViewModel.PageKey);

    /// <summary>快捷键导航（参数为页面键）。</summary>
    [RelayCommand]
    private void Navigate(string? key)
    {
        if (key is not null) _navigation.NavigateTo(key);
    }

    /// <summary>导航快捷键的按键：主导航 1…5，设置为「,」。</summary>
    public IEnumerable<(NavItem Item, Avalonia.Input.Key Key)> ShortcutTargets()
    {
        int number = 0;
        foreach (NavItem item in NavItems)
        {
            if (item.StartsGroup) yield return (item, Avalonia.Input.Key.OemComma);
            else if (++number <= 9) yield return (item, Avalonia.Input.Key.D0 + number);
        }
    }

    /// <summary>启动画面进度条（0–1），由组合根在初始化各步之间推进。</summary>
    [ObservableProperty]
    private double _startupProgress;

    private void OnNavItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 悬浮岛点击只改 IsSelected，真正的切换交给导航服务；导航服务再回写选中状态。
        // 子页面打开时悬浮岛仍选中父页面；只有切到别的一级页面才导航。
        if (e.PropertyName == nameof(NavItem.IsSelected) && sender is NavItem { IsSelected: true } item
            && item.Key != (_navigation.Current?.ParentKey ?? _navigation.Current?.Key))
        {
            _navigation.NavigateTo(item.Key);
        }
    }

    private void OnNavigated(object? sender, NavigatedEventArgs e)
    {
        string islandKey = e.Page.ParentKey ?? e.Page.Key;
        foreach (NavItem item in NavItems)
        {
            item.IsSelected = item.Key == islandKey;
        }
        CurrentPage = e.ViewModel;
        IsAccountsPage = e.Page.Key == Pages.Accounts.AccountsViewModel.PageKey;
        IsHomePage = e.Page.Key == "home";
        Fab.SetProvider(e.ViewModel as IFabProvider);
        UpdatePageTitle();
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == LocalizationService.IndexerName) UpdatePageTitle();
        // 国际服标语图按语言区分，换语言后重新获取。
        if (e.PropertyName == nameof(LocalizationService.CurrentLanguage) && UsesLauncherBackground)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(LoadBackground);
        }
    }

    /// <summary>子页面在页名上方显示父级名（详细图表 → 抽卡统计）；一级页面不显示。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasParent))]
    private string? _parentTitle;

    public bool HasParent => !string.IsNullOrEmpty(ParentTitle);

    private void UpdatePageTitle()
    {
        PageDescriptor? page = _navigation.Current;
        PageTitle = page is null ? string.Empty : _localization[page.TitleKey];
        PageDescriptor? parent = page?.ParentKey is { } key ? _navigation.Pages.FirstOrDefault(p => p.Key == key) : null;
        ParentTitle = parent is null ? null : _localization[parent.TitleKey];
    }
}
