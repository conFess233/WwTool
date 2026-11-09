using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using WwTool.Core.Localization;
using WwTool.UI.Services;

namespace WwTool.UI.Shell;

/// <summary>
/// 主窗口 Shell：页头（悬浮岛、账号胶囊）与内容区。页面切换在导航服务接入后由其负责。
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>应用绘制的标题按钮（3 × 46）宽度，页头右侧为其留白。</summary>
    private const double CaptionButtonsWidth = 138;

    private const string MaximizeGlyphData = "M0.5,0.5 L9.5,0.5 L9.5,9.5 L0.5,9.5 Z";
    private const string RestoreGlyphData = "M2.5,2.5 L9.5,2.5 L9.5,9.5 L2.5,9.5 Z M2.5,0.5 L0.5,0.5 M0.5,0.5 L0.5,7.5 M2.5,0.5 L9.5,0.5";

    private readonly LocalizationService? _localization;

    /// <summary>仅供 Avalonia 设计器使用。</summary>
    public MainWindow() => InitializeComponent();

    public MainWindow(ThemeService theme, LocalizationService localization)
    {
        _localization = localization;
        InitializeComponent();
        bool drawCaptionButtons = !OperatingSystem.IsMacOS();
        CaptionButtons.IsVisible = drawCaptionButtons;
        CaptionButtonsSpacer.Width = drawCaptionButtons ? CaptionButtonsWidth : 0;

        Island.Items.Add(new NavItem("home", "Nav_Home", "avares://WwTool/Assets/Images/Home.png", localization));
        Island.Items.Add(new NavItem("pulls", "Nav_Statistics", "avares://WwTool/Assets/Images/Statistics.png", localization));
        Island.Items.Add(new NavItem("roles", "Nav_RoleData", "avares://WwTool/Assets/Images/Role.png", localization));
        Island.Items.Add(new NavItem("explore", "Nav_Exploration", "avares://WwTool/Assets/Images/Chest.png", localization));
        Island.Items.Add(new NavItem("motor", "Nav_Motor", "avares://WwTool/Assets/Images/Motor.png", localization));
        Island.Items.Add(new NavItem("settings", "Nav_Settings", "avares://WwTool/Assets/Images/Settings.png", localization, startsGroup: true));
        foreach (NavItem item in Island.Items)
        {
            item.PropertyChanged += OnNavItemChanged;
        }
        localization.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == LocalizationService.IndexerName) UpdatePageTitle();
        };
        Island.Items[0].IsSelected = true;

        PageHost.Content = new Stage0PreviewView(theme, localization);
    }

    protected override void OnPropertyChanged(Avalonia.AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WindowStateProperty && MaximizeGlyph is not null)
        {
            MaximizeGlyph.Data = Avalonia.Media.Geometry.Parse(
                WindowState == WindowState.Maximized ? RestoreGlyphData : MaximizeGlyphData);
        }
    }

    private void OnMinimizeClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();

    private void OnNavItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NavItem.IsSelected) && sender is NavItem { IsSelected: true })
        {
            UpdatePageTitle();
        }
    }

    private void UpdatePageTitle()
    {
        NavItem? selected = Island.Items.FirstOrDefault(x => x.IsSelected);
        PageTitle.Text = selected?.Title ?? string.Empty;
    }

    /// <summary>
    /// 页头空白处：单击拖动窗口（走系统移动循环，保留拖到屏幕边缘的贴靠），双击切换最大化。
    /// 岛上按钮与账号胶囊会先处理指针事件，这里只收到真正的空白区域。
    /// </summary>
    private void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            e.Handled = true;
            return;
        }
        BeginMoveDrag(e);
        e.Handled = true;
    }
}
