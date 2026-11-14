using System.ComponentModel;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Rendering.Composition;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
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
            TopDragBand.Height = 26;
        }
        else
        {
            // Windows：窗口按钮贴右上角（高 32），页头整体下移 12。
            Frame.Margin = new Thickness(40, 26 + 12, 40, 0);
            TopDragBand.Height = 26 + 12;
            HomeDecor.TopOffset = 12;
        }
        Classes.Set("reduced", Motion.Reduced);
        Motion.ReducedChanged += (_, _) =>
        {
            Classes.Set("reduced", Motion.Reduced);
            UpdateVideoPlayback();
        };
        Opened += (_, _) => UpdateVideoPlayback();
        // 激活事件触发时 IsActive 可能还是旧值，以事件本身为准。
        Activated += (_, _) => { _foreground = true; UpdateVideoPlayback(); };
        Deactivated += (_, _) => { _foreground = false; UpdateVideoPlayback(); };
        BackdropVideo.PlaybackFailed += (_, ex) => (DataContext as ShellViewModel)?.ReportVideoFailure(ex);
        AddHandler(PointerMovedEvent, OnParallaxPointerMoved, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerMovedEvent, OnPeekPointerMoved, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        PointerExited += (_, _) =>
        {
#if DEBUG
            // 离屏截图验证时只发送窗口消息、真实鼠标不在窗口上，系统会立刻补发「离开」；忽略它才能看到淡出效果。
            if (Environment.GetEnvironmentVariable("WWTOOL_QUIET_VERIFY") == "1") return;
#endif
            SetPeeking(false);
        };
        Deactivated += (_, _) => SetPeeking(false);
        _peekDelay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _peekDelay.Tick += (_, _) => { _peekDelay.Stop(); SetPeeking(true); };
    }

    // ── 空白处查看背景：鼠标在空白处停留片刻后，内容层淡出到设置的透明度；移到任何组件上立即恢复 ──

    private readonly DispatcherTimer _peekDelay;
    private bool _peeking;

    private void OnPeekPointerMoved(object? sender, PointerEventArgs e)
    {
        if (DataContext is not ShellViewModel { BackgroundPeekOpacity: < 1 })
        {
            SetPeeking(false);
            return;
        }
        bool empty = IsOverEmptyArea(e.GetPosition(this));
        if (!empty)
        {
            _peekDelay.Stop();
            SetPeeking(false);
        }
        else if (!_peeking && !_peekDelay.IsEnabled)
        {
            // 稍作停留再淡出，从一张卡片移到另一张时不会闪动。
            _peekDelay.Start();
        }
    }

    private void SetPeeking(bool peeking)
    {
        if (!peeking) _peekDelay.Stop();
        double opacity = peeking && DataContext is ShellViewModel vm ? vm.BackgroundPeekOpacity : 1;
        _peeking = peeking && opacity < 1;
        ContentLayer.Opacity = opacity;
    }

    /// <summary>
    /// 指针下是否为空白：命中背景层、四周拖动留白，或内容区里只有透明的布局容器（页面滚动区、网格间隙等）。
    /// 落在卡片、按钮、文字、图片等任何可见组件上，或落在悬浮按钮、提示、对话框、启动画面上，都不算空白。
    /// </summary>
    private bool IsOverEmptyArea(Point point)
    {
        if (this.InputHitTest(point) is not Visual hit || ReferenceEquals(hit, this)) return true;
        bool inFrame = false;
        for (Visual? v = hit; v is not null; v = v.GetVisualParent())
        {
            if (ReferenceEquals(v, DragEdges)) return true;
            if (ReferenceEquals(v, Frame))
            {
                inFrame = true;
                break;
            }
        }
        // 悬浮按钮、提示、对话框、启动画面、窗口按钮等其他层：不算空白。
        if (!inFrame) return false;
        for (Visual? v = hit; v is not null && !ReferenceEquals(v, Frame); v = v.GetVisualParent())
        {
            if (!IsTransparentContainer(v)) return false;
        }
        return true;
    }

    /// <summary>没有可见底色的布局容器（面板、边框、内容呈现器、滚动区、页面）；其他控件（文字、图片、按钮、自绘控件等）都算组件。</summary>
    private static bool IsTransparentContainer(Visual v)
    {
        IBrush? background;
        switch (v)
        {
            case Border b: background = b.Background; break;
            case Panel p: background = p.Background; break;
            case Avalonia.Controls.Presenters.ContentPresenter cp: background = cp.Background; break;
            case Avalonia.Controls.Presenters.ItemsPresenter: background = null; break;
            case Button or Avalonia.Controls.Primitives.ToggleButton: return false;
            case ScrollViewer or ItemsControl or ContentControl when v is Avalonia.Controls.Primitives.TemplatedControl t: background = t.Background; break;
            default: return false;
        }
        return background is null || background is ISolidColorBrush { Color.A: 0 } || background.Opacity <= 0;
    }

    // ── 主题切换：旧主题截图 + 圆形扩散 ─────────────────────────

    private Bitmap? _themeSnapshot;
    private int _revealToken;

    /// <summary>
    /// 动效「主题切换」：在应用新主题之前调用并等待完成。用合成器截下当前真实画面盖在最上层，
    /// 调用方随后切换主题；截图从 <paramref name="origin"/>（窗口坐标）挖出一个 700ms 内扩大到覆盖全窗口的圆，
    /// 露出下面已换成新主题的界面。截图层不接收指针，期间可照常操作。
    /// </summary>
    public async Task BeginThemeRevealAsync(Point origin)
    {
        if (Motion.Reduced || Content is not Visual root || Bounds.Width < 1 || Bounds.Height < 1) return;
        if (ElementComposition.GetElementVisual(root) is not { } visual) return;
        Bitmap snapshot;
        try
        {
            ThemeSnapshot.IsVisible = false;
            snapshot = await visual.Compositor.CreateCompositionVisualSnapshot(visual, RenderScaling);
        }
        catch (Exception)
        {
            return;
        }
#if DEBUG
        if (Environment.GetEnvironmentVariable("WWTOOL_DUMP_SNAPSHOT") is { Length: > 0 } dump) snapshot.Save(dump, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
#endif
        _themeSnapshot?.Dispose();
        _themeSnapshot = snapshot;
        ThemeSnapshot.Source = snapshot;
        ThemeSnapshot.Clip = new RectangleGeometry(new Rect(Bounds.Size));
        ThemeSnapshot.IsVisible = true;
        _ = RunRevealAsync(origin, ++_revealToken);
    }

    private async Task RunRevealAsync(Point origin, int token)
    {
        var size = Bounds.Size;
        double max = new[] { new Point(0, 0), new Point(size.Width, 0), new Point(0, size.Height), new Point(size.Width, size.Height) }
            .Max(c => Math.Sqrt(Math.Pow(c.X - origin.X, 2) + Math.Pow(c.Y - origin.Y, 2)));
        var full = new RectangleGeometry(new Rect(size));
        var clock = System.Diagnostics.Stopwatch.StartNew();
        double duration = 700;
#if DEBUG
        // 本地验证：WWTOOL_SLOW_MOTION=N 时动效放慢 N 倍，便于截取中间帧。
        if (double.TryParse(Environment.GetEnvironmentVariable("WWTOOL_SLOW_MOTION"), out double slow) && slow > 1) duration *= slow;
#endif
        while (token == _revealToken)
        {
            double t = Math.Min(1, clock.Elapsed.TotalMilliseconds / duration);
            double r = max * Motion.StandardEasing.Ease(t);
            ThemeSnapshot.Clip = new CombinedGeometry(GeometryCombineMode.Exclude, full,
                new EllipseGeometry(new Rect(origin.X - r, origin.Y - r, r * 2, r * 2)));
            if (t >= 1) break;
            await Task.Delay(16);
        }
        if (token != _revealToken) return;
        ThemeSnapshot.IsVisible = false;
        ThemeSnapshot.Source = null;
        ThemeSnapshot.Clip = null;
        _themeSnapshot?.Dispose();
        _themeSnapshot = null;
    }

    // ── 背景视差：自定义背景图随指针反向移动，最多 14 ─────────────

    private Vector _parallaxTarget;
    private Vector _parallaxCurrent;
    private bool _parallaxTicking;

    private void OnParallaxPointerMoved(object? sender, PointerEventArgs e)
    {
        if (Motion.Reduced || DataContext is not ShellViewModel { HasBackgroundImage: true }) return;
        Point p = e.GetPosition(this);
        double nx = Bounds.Width > 0 ? p.X / Bounds.Width * 2 - 1 : 0;
        double ny = Bounds.Height > 0 ? p.Y / Bounds.Height * 2 - 1 : 0;
        _parallaxTarget = new Vector(-Math.Clamp(nx, -1, 1) * 14, -Math.Clamp(ny, -1, 1) * 14);
        if (_parallaxTicking) return;
        _parallaxTicking = true;
        RequestAnimationFrame(ParallaxTick);
    }

    private void ParallaxTick(TimeSpan _)
    {
        _parallaxCurrent += (_parallaxTarget - _parallaxCurrent) * 0.12;
        foreach (Control layer in new Control[] { BackdropImage, BackdropVideo, BackdropSlogan })
        {
            if (layer.RenderTransform is TranslateTransform translate)
            {
                translate.X = _parallaxCurrent.X;
                translate.Y = _parallaxCurrent.Y;
            }
        }
        if ((_parallaxTarget - _parallaxCurrent).Length > 0.05)
        {
            RequestAnimationFrame(ParallaxTick);
        }
        else
        {
            _parallaxTicking = false;
        }
    }

    public MainWindow(ShellViewModel viewModel) : this()
    {
        DataContext = viewModel;
        ApplyGlass(viewModel.IsGlassEnabled);
        viewModel.PropertyChanged += OnViewModelChanged;
        RegisterShortcuts(viewModel);
        viewModel.Accounts.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Services.AccountContext.Current)) _ = FlipAvatarAsync();
        };
        if (viewModel.IsReady) Splash.IsVisible = false;
#if DEBUG
        // 本地截图验证：窗口放到屏幕外、不抢焦点、不进任务栏，不打扰正在用电脑的人。
        if (Environment.GetEnvironmentVariable("WWTOOL_QUIET_VERIFY") == "1")
        {
            // 显示时系统会把窗口拉回屏幕内，所以先全透明显示，打开后再移到屏幕外、恢复不透明。
            ShowActivated = false;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = new PixelPoint(-6000, 100);
            Opacity = 0;
            Opened += (_, _) =>
            {
                Position = new PixelPoint(-6000, 100);
                Dispatcher.UIThread.Post(() =>
                {
                    Position = new PixelPoint(-6000, 100);
                    Opacity = 1;
                }, DispatcherPriority.Background);
            };
        }
#endif
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

    /// <summary>账号切换：页头头像绕竖轴从侧面翻到正面（480ms）。</summary>
    private async Task FlipAvatarAsync()
    {
        if (Motion.Reduced || !IsVisible) return;
        var rotate = new Rotate3DTransform { Depth = 300 };
        CapsuleAvatar.RenderTransform = rotate;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        const double duration = 480;
        while (clock.Elapsed.TotalMilliseconds < duration)
        {
            double t = clock.Elapsed.TotalMilliseconds / duration;
            rotate.AngleY = -90 * (1 - Motion.StandardEasing.Ease(t));
            await Task.Delay(16);
        }
        CapsuleAvatar.RenderTransform = null;
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
        if (change.Property == IsActiveProperty) _foreground = IsActive;
        if (change.Property == WindowStateProperty || change.Property == IsVisibleProperty || change.Property == IsActiveProperty) UpdateVideoPlayback();
    }

    /// <summary>
    /// 动态背景只在窗口可见、未最小化、处于前台（获得焦点）且未开启「减少动画」时播放，否则暂停在当前画面。
    /// 窗口被其他窗口完全挡住时它通常也已失去焦点，因此同样暂停。
    /// </summary>
    /// <summary>窗口是否在前台（最近一次激活 / 失活事件）。</summary>
    private bool _foreground;

    private void UpdateVideoPlayback()
    {
        bool foreground = _foreground;
#if DEBUG
        // 离屏截图验证时窗口不抢焦点，允许忽略焦点条件。
        if (Environment.GetEnvironmentVariable("WWTOOL_VIDEO_IGNORE_FOCUS") == "1") foreground = true;
#endif
        BackdropVideo.IsPlaying = IsVisible && WindowState != WindowState.Minimized && foreground && !Motion.Reduced;
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private bool _exitConfirmed;

    /// <summary>关闭窗口（窗口按钮、Alt+F4、macOS 红灯）先弹「确认退出」。</summary>
    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (_exitConfirmed || e.IsProgrammatic && e.CloseReason == WindowCloseReason.ApplicationShutdown
            || DataContext is not ShellViewModel vm || !vm.IsReady)
        {
            return;
        }
        e.Cancel = true;
        if (vm.Dialogs.IsOpen) return;
        bool ok = await vm.Dialogs.ConfirmAsync(vm.Text("Dialog_ExitTitle"), vm.Text("Dialog_ExitMessage"));
        if (!ok) return;
        _exitConfirmed = true;
        Close();
    }

    /// <summary>Logo 与页名不参与拖动。</summary>
    private void OnBrandPointerPressed(object? sender, PointerPressedEventArgs e) => e.Handled = true;

    /// <summary>
    /// 页头空白处与窗口四周留白：单击拖动窗口（走系统移动循环，保留贴靠），双击切换最大化（Windows；macOS 双击由系统按设置缩放）。
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
