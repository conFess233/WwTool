using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WwTool.Core.Abstractions;
using WwTool.Core.Localization;
using WwTool.Core.Models.Config;
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
        IConfigService config)
    {
        _config = config;
        config.User.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(UserConfig.IsGlassEffectEnabled) or nameof(UserConfig.GlassOpacity))
            {
                OnPropertyChanged(nameof(IsGlassEnabled));
                OnPropertyChanged(nameof(BackdropOpacity));
            }
        };
        Accounts = accounts;
        _navigation = navigation;
        _localization = localization;
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

    /// <summary>当前账号（页头账号胶囊）。</summary>
    public AccountContext Accounts { get; }

    /// <summary>右下悬浮按钮，动作随当前页面变化。</summary>
    public FabMenuViewModel Fab { get; }

    /// <summary>窗口内对话框覆盖层。</summary>
    public DialogService Dialogs { get; }

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
        Fab.SetProvider(e.ViewModel as IFabProvider);
        UpdatePageTitle();
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == LocalizationService.IndexerName) UpdatePageTitle();
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
