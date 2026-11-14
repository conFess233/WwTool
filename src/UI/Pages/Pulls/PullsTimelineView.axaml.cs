using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace WwTool.UI.Pages.Pulls;

/// <summary>时间线视图：滚轮横向滚动；数据重建后滚到最右（最新）。</summary>
public partial class PullsTimelineView : UserControl
{
    private const double WheelStep = 96;
    private PullsTimeline? _timeline;

    public PullsTimelineView()
    {
        InitializeComponent();
        TrackScroller.AddHandler(PointerWheelChangedEvent, OnWheel, RoutingStrategies.Tunnel);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_timeline is not null) _timeline.Rebuilt -= OnRebuilt;
        _timeline = DataContext as PullsTimeline;
        if (_timeline is not null) _timeline.Rebuilt += OnRebuilt;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ScrollToLatest();
    }

    private void OnRebuilt(object? sender, EventArgs e) => ScrollToLatest();

    private void ScrollToLatest() => Dispatcher.UIThread.Post(() => TrackScroller.ScrollToEnd(), DispatcherPriority.Background);

    // 竖直滚轮转为横向滚动（触控板的横向滑动保持原样）。
    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        if (e.Delta.Y == 0 || e.Delta.X != 0) return;
        double max = Math.Max(0, TrackScroller.Extent.Width - TrackScroller.Viewport.Width);
        double x = Math.Clamp(TrackScroller.Offset.X - e.Delta.Y * WheelStep, 0, max);
        TrackScroller.Offset = new Vector(x, TrackScroller.Offset.Y);
        e.Handled = true;
    }
}
