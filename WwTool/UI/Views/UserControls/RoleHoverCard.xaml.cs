using System.Windows.Controls;
using System.Windows.Input;

namespace WwTool.UI.Views.UserControls;

/// <summary>角色悬浮卡片，只承载快照展示和信息区滚动。</summary>
public partial class RoleHoverCard : UserControl
{
    public RoleHoverCard() => InitializeComponent();

    /// <summary>焦点留在头像时也允许使用键盘阅读全部属性。</summary>
    public void Scroll(Key key)
    {
        switch (key)
        {
            case Key.Up: DetailsScroller.LineUp(); break;
            case Key.Down: DetailsScroller.LineDown(); break;
            case Key.PageUp: DetailsScroller.PageUp(); break;
            case Key.PageDown: DetailsScroller.PageDown(); break;
        }
    }

    /// <summary>浮层滚轮由内部消费，避免带动角色列表。</summary>
    private void OnDetailsMouseWheel(object sender, MouseWheelEventArgs e)
    {
        DetailsScroller.ScrollToVerticalOffset(DetailsScroller.VerticalOffset - e.Delta);
        e.Handled = true;
    }
}
