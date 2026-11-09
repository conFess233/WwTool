using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WwTool.Core.Localization;
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

    public ShellViewModel(
        INavigationService navigation,
        LocalizationService localization,
        FabMenuViewModel fab,
        DialogService dialogs,
        IUIStateService uiState,
        AccountContext accounts)
    {
        Accounts = accounts;
        _navigation = navigation;
        _localization = localization;
        Fab = fab;
        Dialogs = dialogs;
        UiState = uiState;

        foreach (PageDescriptor page in navigation.Pages.Where(p => p.IsTopLevel))
        {
            var item = new NavItem(page.Key, page.TitleKey, page.Icon, localization, page.StartsGroup);
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
        Fab.SetActions((e.ViewModel as IFabProvider)?.FabActions);
        UpdatePageTitle();
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == LocalizationService.IndexerName) UpdatePageTitle();
    }

    /// <summary>页头页名上方的小字：一级页面为应用名，子页面为父页面标题。</summary>
    [ObservableProperty]
    private string _pageEyebrow = string.Empty;

    private void UpdatePageTitle()
    {
        PageDescriptor? page = _navigation.Current;
        PageTitle = page is null ? string.Empty : _localization[page.TitleKey];
        PageDescriptor? parent = page?.ParentKey is { } key ? _navigation.Pages.FirstOrDefault(p => p.Key == key) : null;
        PageEyebrow = parent is null ? _localization["Shell_Eyebrow"] : _localization[parent.TitleKey];
    }
}
