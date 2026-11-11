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

    /// <summary>窗口毛玻璃：开启时窗口透明并请求系统模糊材质，背景层按 <see cref="BackdropOpacity"/> 半透明。</summary>
    /// <remarks>本次启动用了软件渲染（启动时毛玻璃为关）时系统材质不可用，运行中打开开关也按不透明处理，重启后生效，见 <see cref="RenderingProfile"/>。</remarks>
    public bool IsGlassEnabled => _config.User.IsGlassEffectEnabled && RenderingProfile.GlassCapable;

    /// <summary>背景层不透明度；未开启毛玻璃时为 1。</summary>
    public double BackdropOpacity => IsGlassEnabled ? _config.User.GlassOpacity / 100.0 : 1;

    public ShellViewModel(
        INavigationService navigation,
        LocalizationService localization,
        FabMenuViewModel fab,
        DialogService dialogs,
        IUIStateService uiState,
        AccountContext accounts,
        IConfigService config,
        LauncherBackgroundService launcherBackground)
    {
        _config = config;
        _launcherBackground = launcherBackground;
        config.User.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(UserConfig.IsGlassEffectEnabled) or nameof(UserConfig.GlassOpacity))
            {
                OnPropertyChanged(nameof(IsGlassEnabled));
                OnPropertyChanged(nameof(BackdropOpacity));
            }
            if (e.PropertyName is nameof(UserConfig.UseCustomBackground) or nameof(UserConfig.CustomBackgroundPath))
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(LoadBackground);
            }
        };
        Accounts = accounts;
        _navigation = navigation;
        _localization = localization;
        LoadBackground();
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

    /// <summary>每次重新选择背景时递增，丢弃过期的异步结果。</summary>
    private int _backgroundVersion;

    /// <summary>本次运行已成功刷新过启动器背景的语言；同一语言不重复请求。</summary>
    private string? _launcherRefreshedLanguage;

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

        string language = _localization.CurrentLanguage.GetCode();
        if (_launcherRefreshedLanguage == language) return;
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
        _launcherRefreshedLanguage = language;
        if (version == _backgroundVersion) await ApplyLauncherAsync(files, version);
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
