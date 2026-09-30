using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WwTool.UI.ViewModels;
using WwTool.UI.Views.UserControls;

namespace WwTool.UI.Views;

/// <summary>管理角色浮层的延时、位置、焦点与页面生命周期。</summary>
public partial class RoleDataView : UserControl
{
    private readonly DispatcherTimer enterTimer = new();
    private readonly DispatcherTimer leaveTimer = new();
    private Popup? hoverPopup;
    private RoleHoverCard? hoverCard;
    private FrameworkElement? hoverOwner;
    private Window? hostWindow;
    private GuideRoleDataViewModel? observedModel;
    private bool closing;
    private bool keyboardOpened;
    private int animationVersion;

    public RoleDataView()
    {
        InitializeComponent();
        enterTimer.Interval = TimeSpan.FromMilliseconds((double)FindResource("RoleHoverDelayMs"));
        leaveTimer.Interval = TimeSpan.FromMilliseconds((double)FindResource("RoleHoverLeaveDelayMs"));
        enterTimer.Tick += OnEnterTimer;
        leaveTimer.Tick += OnLeaveTimer;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
        PreviewKeyDown += OnHoverKeyDown;
        AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnPageScroll));
    }

    /// <summary>仅在页面可见期间订阅窗口和账号状态。</summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        hostWindow = Window.GetWindow(this);
        if (hostWindow is not null)
        {
            hostWindow.Deactivated += OnWindowChanged;
            hostWindow.LocationChanged += OnWindowChanged;
            hostWindow.SizeChanged += OnWindowSizeChanged;
        }
        ObserveModel(DataContext as GuideRoleDataViewModel);
    }

    /// <summary>离开页面终止计时、动画和订阅，避免残留浮层。</summary>
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        CloseHover(false);
        ObserveModel(null);
        if (hostWindow is not null)
        {
            hostWindow.Deactivated -= OnWindowChanged;
            hostWindow.LocationChanged -= OnWindowChanged;
            hostWindow.SizeChanged -= OnWindowSizeChanged;
            hostWindow = null;
        }
    }

    /// <summary>页面复用时关闭旧账号详情。</summary>
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        CloseHover(false);
        if (IsLoaded) ObserveModel(e.NewValue as GuideRoleDataViewModel);
    }

    /// <summary>账号切换、同步替换和排序均使旧浮层失效。</summary>
    private void ObserveModel(GuideRoleDataViewModel? model)
    {
        if (observedModel is not null)
        {
            observedModel.PropertyChanged -= OnModelChanged;
            observedModel.Roles.CollectionChanged -= OnRolesChanged;
        }
        observedModel = model;
        if (model is not null)
        {
            model.PropertyChanged += OnModelChanged;
            model.Roles.CollectionChanged += OnRolesChanged;
        }
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GuideRoleDataViewModel.SelectedUser)) CloseHover(false);
    }
    private void OnRolesChanged(object? sender, NotifyCollectionChangedEventArgs e) => CloseHover(false);
    private void OnWindowChanged(object? sender, EventArgs e) => CloseHover(false);
    private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e) => CloseHover(false);

    /// <summary>头像作为唯一触发区域，重新进入可以中断淡出。</summary>
    private void OnRoleEnter(object sender, MouseEventArgs e) => StartHover((FrameworkElement)sender, false);
    private void OnRoleFocus(object sender, KeyboardFocusChangedEventArgs e) => StartHover((FrameworkElement)sender, true);
    private void OnRoleLeave(object sender, MouseEventArgs e)
    {
        if (ReferenceEquals(sender, hoverOwner)) ScheduleLeave();
    }
    private void OnRoleBlur(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (ReferenceEquals(sender, hoverOwner) && keyboardOpened) CloseHover(true);
    }

    /// <summary>切换目标重新等待，不复用前一个头像的计时。</summary>
    private void StartHover(FrameworkElement owner, bool fromKeyboard)
    {
        if (owner.DataContext is not GuideRoleCardViewModel) return;
        leaveTimer.Stop();
        if (ReferenceEquals(owner, hoverOwner) && hoverPopup?.IsOpen == true)
        {
            keyboardOpened |= fromKeyboard;
            AnimateHover(true);
            return;
        }
        CloseHover(false);
        hoverOwner = owner;
        keyboardOpened = fromKeyboard;
        enterTimer.Start();
    }

    /// <summary>延迟结束后检查目标仍有效，再打开唯一浮层。</summary>
    private void OnEnterTimer(object? sender, EventArgs e)
    {
        enterTimer.Stop();
        if (hoverOwner is null || !hoverOwner.IsLoaded || !hoverOwner.IsVisible ||
            (!hoverOwner.IsMouseOver && !hoverOwner.IsKeyboardFocusWithin)) return;
        if (hoverOwner.DataContext is not GuideRoleCardViewModel role || hostWindow is null) return;
        EnsurePopup();
        if (hoverCard is null || hoverPopup is null) return;
        double gap = (double)FindResource("SpacingSm");
        hoverCard.Width = Math.Min((double)FindResource("RoleHoverWidth"), Math.Max(1, hostWindow.ActualWidth - gap * 2));
        hoverCard.MaxHeight = Math.Max(1, hostWindow.ActualHeight - gap * 2);
        hoverCard.DataContext = role;
        hoverCard.Measure(new Size(hoverCard.Width, hoverCard.MaxHeight));
        hoverPopup.PlacementTarget = hoverOwner;
        hoverPopup.IsOpen = true;
        AnimateHover(true);
    }

    /// <summary>按需创建浮层，继承页面资源并保持头像键盘焦点。</summary>
    private void EnsurePopup()
    {
        if (hoverPopup is not null) return;
        hoverCard = new RoleHoverCard { Opacity = 0 };
        hoverCard.MouseEnter += (_, _) => { leaveTimer.Stop(); AnimateHover(true); };
        hoverCard.MouseLeave += (_, _) => ScheduleLeave();
        hoverCard.PreviewKeyDown += OnHoverKeyDown;
        hoverPopup = new Popup
        {
            Child = hoverCard,
            AllowsTransparency = true,
            StaysOpen = true,
            Placement = PlacementMode.Custom,
            CustomPopupPlacementCallback = PlaceHover
        };
        AddLogicalChild(hoverPopup);
    }

    /// <summary>右侧不足翻左，再限制在窗口可用内容边界内。</summary>
    private CustomPopupPlacement[] PlaceHover(Size popupSize, Size targetSize, Point offset)
    {
        if (hoverOwner is null || hostWindow is null) return [];
        double gap = (double)FindResource("SpacingSm");
        Point anchor = hoverOwner.TranslatePoint(new Point(), hostWindow);
        double x = anchor.X + targetSize.Width + gap;
        if (x + popupSize.Width > hostWindow.ActualWidth - gap)
            x = anchor.X - popupSize.Width - gap;
        x = Math.Clamp(x, gap, Math.Max(gap, hostWindow.ActualWidth - popupSize.Width - gap));
        double y = Math.Clamp(anchor.Y, gap, Math.Max(gap, hostWindow.ActualHeight - popupSize.Height - gap));
        return [new CustomPopupPlacement(new Point(x - anchor.X, y - anchor.Y), PopupPrimaryAxis.None)];
    }

    /// <summary>为头像与浮层间的间隙保留短暂通过时间。</summary>
    private void ScheduleLeave()
    {
        if (keyboardOpened && hoverOwner?.IsKeyboardFocusWithin == true) return;
        enterTimer.Stop();
        leaveTimer.Stop();
        leaveTimer.Start();
    }

    private void OnLeaveTimer(object? sender, EventArgs e)
    {
        leaveTimer.Stop();
        if (hoverOwner?.IsMouseOver == true || hoverCard?.IsMouseOver == true) return;
        CloseHover(true);
    }

    /// <summary>中断动画从当前透明度继续，旧回调不能关闭新的卡片。</summary>
    private void AnimateHover(bool show)
    {
        if (hoverCard is null || hoverPopup?.IsOpen != true) return;
        closing = !show;
        int version = ++animationVersion;
        double current = hoverCard.Opacity;
        hoverCard.BeginAnimation(OpacityProperty, null);
        hoverCard.Opacity = current;
        var animation = new DoubleAnimation(current, show ? 1 : 0,
            (Duration)FindResource(show ? "MotionRoleHoverEnter" : "MotionRoleHoverExit"))
        { EasingFunction = (IEasingFunction)FindResource("MotionEaseOut") };
        if (!show) animation.Completed += (_, _) => { if (closing && version == animationVersion) CloseHover(false); };
        hoverCard.BeginAnimation(OpacityProperty, animation);
    }

    /// <summary>中止等待，根据页面生命周期选择立即关闭或淡出。</summary>
    private void CloseHover(bool animate)
    {
        enterTimer.Stop();
        leaveTimer.Stop();
        if (animate && hoverPopup?.IsOpen == true) { AnimateHover(false); return; }
        ++animationVersion;
        closing = false;
        if (hoverPopup is not null)
        {
            hoverPopup.IsOpen = false;
            hoverPopup.PlacementTarget = null;
        }
        if (hoverCard is not null)
        {
            hoverCard.BeginAnimation(OpacityProperty, null);
            hoverCard.Opacity = 0;
            hoverCard.DataContext = null;
        }
        hoverOwner = null;
        keyboardOpened = false;
    }

    /// <summary>头像保留焦点时转发信息区阅读按键。</summary>
    private void OnHoverKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && (hoverOwner is not null || hoverPopup?.IsOpen == true))
        {
            CloseHover(true);
            e.Handled = true;
        }
        else if (hoverPopup?.IsOpen == true && e.Key is Key.Up or Key.Down or Key.PageUp or Key.PageDown)
        {
            hoverCard?.Scroll(e.Key);
            e.Handled = true;
        }
    }

    /// <summary>页面本体滚动关闭浮层，浮层内部滚动单独消费。</summary>
    private void OnPageScroll(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalChange != 0 || e.HorizontalChange != 0) CloseHover(false);
    }
}
