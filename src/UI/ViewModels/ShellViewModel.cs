using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WwTool.Core.Localization;
using WwTool.UI.Navigation;
using WwTool.UI.Shell;

namespace WwTool.UI.ViewModels;

/// <summary>
/// 主窗口：悬浮岛导航项、当前页面与页名，以及启动初始化状态。
/// </summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly INavigationService _navigation;
    private readonly LocalizationService _localization;

    public ShellViewModel(INavigationService navigation, LocalizationService localization)
    {
        _navigation = navigation;
        _localization = localization;

        foreach (PageDescriptor page in navigation.Pages)
        {
            var item = new NavItem(page.Key, page.TitleKey, page.Icon, localization, page.StartsGroup);
            item.PropertyChanged += OnNavItemChanged;
            NavItems.Add(item);
        }

        navigation.Navigated += OnNavigated;
        localization.PropertyChanged += OnLocalizationChanged;
    }

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
        if (e.PropertyName == nameof(NavItem.IsSelected) && sender is NavItem { IsSelected: true } item)
        {
            _navigation.NavigateTo(item.Key);
        }
    }

    private void OnNavigated(object? sender, NavigatedEventArgs e)
    {
        foreach (NavItem item in NavItems)
        {
            item.IsSelected = item.Key == e.Page.Key;
        }
        CurrentPage = e.ViewModel;
        UpdatePageTitle();
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == LocalizationService.IndexerName) UpdatePageTitle();
    }

    private void UpdatePageTitle() =>
        PageTitle = _navigation.Current is { } page ? _localization[page.TitleKey] : string.Empty;
}
