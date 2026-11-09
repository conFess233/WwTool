using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using WwTool.UI.ViewModels;

namespace WwTool.UI.Shell;

/// <summary>
/// 主窗口 Shell。状态全部在 <see cref="ShellViewModel"/>，这里只处理窗口行为：
/// 页头拖动、双击最大化与应用绘制的标题按钮。
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>应用绘制的标题按钮（3 × 46）宽度，页头右侧为其留白。</summary>
    private const double CaptionButtonsWidth = 138;

    private const string MaximizeGlyphData = "M0.5,0.5 L9.5,0.5 L9.5,9.5 L0.5,9.5 Z";
    private const string RestoreGlyphData = "M2.5,2.5 L9.5,2.5 L9.5,9.5 L2.5,9.5 Z M2.5,0.5 L0.5,0.5 M0.5,0.5 L0.5,7.5 M2.5,0.5 L9.5,0.5";

    /// <summary>仅供 Avalonia 设计器使用。</summary>
    public MainWindow()
    {
        InitializeComponent();
        // macOS 保留系统红绿灯，不画这组按钮。
        bool drawCaptionButtons = !OperatingSystem.IsMacOS();
        CaptionButtons.IsVisible = drawCaptionButtons;
        CaptionButtonsSpacer.Width = drawCaptionButtons ? CaptionButtonsWidth : 0;
    }

    public MainWindow(ShellViewModel viewModel) : this() => DataContext = viewModel;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WindowStateProperty && MaximizeGlyph is not null)
        {
            MaximizeGlyph.Data = Geometry.Parse(
                WindowState == WindowState.Maximized ? RestoreGlyphData : MaximizeGlyphData);
        }
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

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
