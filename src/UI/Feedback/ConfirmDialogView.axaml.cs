using Avalonia.Controls;
using Avalonia.Interactivity;

namespace WwTool.UI.Feedback;

public partial class ConfirmDialogView : UserControl
{
    public ConfirmDialogView() => InitializeComponent();

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        // 默认焦点：危险操作落在“取消”上，避免回车误删；其余落在确认按钮。
        if (DataContext is ConfirmDialogViewModel { IsDanger: true, HasCancel: true }) CancelButton.Focus();
        else ConfirmButton.Focus();
    }
}
