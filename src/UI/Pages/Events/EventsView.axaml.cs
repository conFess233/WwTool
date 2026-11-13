using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;

namespace WwTool.UI.Pages.Events;

public partial class EventsView : UserControl
{
    private EventsViewModel? _viewModel;

    public EventsView()
    {
        InitializeComponent();
        // 整页铺满可视区域（左右两栏各自滚动）；窗口过矮时按最小高度排布并允许整页滚动。
        Scroller.PropertyChanged += (_, e) =>
        {
            if (e.Property != BoundsProperty) return;
            double available = Scroller.Bounds.Height - Scroller.Padding.Top - Scroller.Padding.Bottom - Root.Margin.Top - Root.Margin.Bottom;
            if (available > 0) Root.Height = Math.Max(Root.MinHeight, available);
        };
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null) _viewModel.PropertyChanged -= OnViewModelChanged;
        _viewModel = DataContext as EventsViewModel;
        if (_viewModel is not null) _viewModel.PropertyChanged += OnViewModelChanged;
    }

    // 换一条公告时详情回到顶部。
    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EventsViewModel.Selected)) DetailScroller.Offset = new Vector(0, 0);
    }
}
