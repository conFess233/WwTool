using Avalonia.Controls;
using Avalonia.Input;

namespace WwTool.UI.Feedback;

public partial class ToastHost : UserControl
{
    public ToastHost() => InitializeComponent();

    // 悬停暂停倒计时，方便阅读较长的错误信息。
    private void OnToastPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Control { DataContext: ToastItem toast }) toast.IsPaused = true;
    }

    private void OnToastPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is Control { DataContext: ToastItem toast }) toast.IsPaused = false;
    }
}
