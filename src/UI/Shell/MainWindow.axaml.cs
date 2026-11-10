using System.ComponentModel;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using WwTool.UI.Theming;
using WwTool.UI.ViewModels;

namespace WwTool.UI.Shell;

/// <summary>
/// 主窗口。状态全部在 <see cref="ShellViewModel"/>，这里只处理窗口行为：
/// 平台外框差异、页头拖动与双击、窗口按钮、导航快捷键、启动画面退场。
/// </summary>
public partial class MainWindow : Window
{
    private const string MaximizeGlyphData = "M1.5,1.5 L10.5,1.5 L10.5,10.5 L1.5,10.5 Z";
    private const string RestoreGlyphData = "M3.5,3.5 L10.5,3.5 L10.5,10.5 L3.5,10.5 Z M3.5,1.5 L1.5,1.5 L1.5,8.5 M3.5,1.5 L10.5,1.5";

    /// <summary>仅供 Avalonia 设计器使用。</summary>
    public MainWindow()
    {
        InitializeComponent();
        if (OperatingSystem.IsMacOS())
        {
            // 标题栏高度设为页头区域，系统红绿灯在页头内垂直居中；Logo 与页名右移让出红绿灯，右上角不放窗口按钮。
            ExtendClientAreaTitleBarHeightHint = 26 + 56 + 26;
            Frame.Margin = new Thickness(40, 26, 40, 0);
            Brand.Margin = new Thickness(72, 0, 0, 0);
            CaptionButtons.IsVisible = false;
        }
        else
        {
            // Windows：窗口按钮贴右上角（高 32），页头整体下移 12。
            Frame.Margin = new Thickness(40, 26 + 12, 40, 0);
        }
        Classes.Set("reduced", Motion.Reduced);
        Motion.ReducedChanged += (_, _) => Classes.Set("reduced", Motion.Reduced);
    }

    public MainWindow(ShellViewModel viewModel) : this()
    {
        DataContext = viewModel;
        ApplyGlass(viewModel.IsGlassEnabled);
        viewModel.PropertyChanged += OnViewModelChanged;
        RegisterShortcuts(viewModel);
        if (viewModel.IsReady) Splash.IsVisible = false;
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ShellViewModel viewModel) return;
        switch (e.PropertyName)
        {
            case nameof(ShellViewModel.IsGlassEnabled):
                ApplyGlass(viewModel.IsGlassEnabled);
                break;
            case nameof(ShellViewModel.IsReady) when viewModel.IsReady:
                Dispatcher.UIThread.Post(() => _ = DismissSplashAsync(), DispatcherPriority.Loaded);
                break;
        }
    }

    /// <summary>启动画面退场（动效「启动画面」）：放大 4% 淡出，内容从 97% 淡入，约 700ms。</summary>
    private async Task DismissSplashAsync()
    {
        if (!Splash.IsVisible) return;
        var duration = TimeSpan.FromMilliseconds(700);
        bool reduced = Motion.Reduced;
        Frame.RenderTransformOrigin = RelativePoint.Center;
        var splashOut = new Animation
        {
            Duration = duration,
            Easing = Motion.StandardEasing,
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(OpacityProperty, 1d), new Setter(ScaleTransform.ScaleXProperty, 1d), new Setter(ScaleTransform.ScaleYProperty, 1d) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OpacityProperty, 0d), new Setter(ScaleTransform.ScaleXProperty, reduced ? 1d : 1.04), new Setter(ScaleTransform.ScaleYProperty, reduced ? 1d : 1.04) } },
            },
        };
        var frameIn = new Animation
        {
            Duration = duration,
            Easing = Motion.StandardEasing,
            FillMode = FillMode.Both,
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(OpacityProperty, 0d), new Setter(ScaleTransform.ScaleXProperty, reduced ? 1d : 0.97), new Setter(ScaleTransform.ScaleYProperty, reduced ? 1d : 0.97) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OpacityProperty, 1d), new Setter(ScaleTransform.ScaleXProperty, 1d), new Setter(ScaleTransform.ScaleYProperty, 1d) } },
            },
        };
        Splash.RenderTransform = new ScaleTransform();
        Frame.RenderTransform = new ScaleTransform();
        await Task.WhenAll(splashOut.RunAsync(Splash), frameIn.RunAsync(Frame));
        Splash.IsVisible = false;
        Frame.RenderTransform = null;
    }

    /// <summary>导航快捷键：Windows Ctrl + 1…5、Ctrl + ,；macOS 用 ⌘。</summary>
    private void RegisterShortcuts(ShellViewModel viewModel)
    {
        KeyModifiers modifier = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        foreach ((NavItem item, Key key) in viewModel.ShortcutTargets())
        {
            KeyBindings.Add(new KeyBinding
            {
                Gesture = new KeyGesture(key, modifier),
                Command = viewModel.NavigateCommand,
                CommandParameter = item.Key,
            });
        }
    }

    // 系统不支持模糊材质时退回不透明，背景层仍按不透明度绘制。
    private void ApplyGlass(bool enabled) => TransparencyLevelHint = enabled
        ? [WindowTransparencyLevel.Mica, WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Blur, WindowTransparencyLevel.None]
        : [WindowTransparencyLevel.None];

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WindowStateProperty && MaximizeGlyph is not null)
        {
            MaximizeGlyph.Data = Geometry.Parse(WindowState == WindowState.Maximized ? RestoreGlyphData : MaximizeGlyphData);
        }
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    /// <summary>Logo 与页名不参与拖动。</summary>
    private void OnBrandPointerPressed(object? sender, PointerPressedEventArgs e) => e.Handled = true;

    /// <summary>
    /// 页头空白处：单击拖动窗口（走系统移动循环，保留贴靠），双击切换最大化（Windows；macOS 双击由系统按设置缩放）。
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
