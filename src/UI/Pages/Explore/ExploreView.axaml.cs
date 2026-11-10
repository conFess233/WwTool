using Avalonia.Controls;

namespace WwTool.UI.Pages.Explore;

public partial class ExploreView : UserControl
{
    public ExploreView()
    {
        InitializeComponent();
        // 按定稿铺满可视区域；窗口过矮时按最小高度排布并允许滚动。
        Scroller.PropertyChanged += (_, e) =>
        {
            if (e.Property != BoundsProperty) return;
            double available = Scroller.Bounds.Height - Root.Margin.Top - Root.Margin.Bottom;
            if (available > 0) Root.Height = Math.Max(Root.MinHeight, available);
        };
    }
}
