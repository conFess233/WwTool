using Avalonia.Controls;

namespace WwTool.UI.Pages.Pulls;

public partial class PullsOverviewView : UserControl
{
    /// <summary>距底部多少像素时加载下一页。</summary>
    private const double LoadMoreThreshold = 320;

    public PullsOverviewView()
    {
        InitializeComponent();
        Scroller.ScrollChanged += OnScrollChanged;
    }

    // 纯视图行为：滚动到接近底部时触发视图模型的分页加载。
    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (DataContext is not PullsOverviewViewModel { IsPoolView: true, Records.HasMore: true } vm) return;
        if (Scroller.Offset.Y + Scroller.Viewport.Height >= Scroller.Extent.Height - LoadMoreThreshold)
        {
            vm.Records.LoadMore();
        }
    }
}
