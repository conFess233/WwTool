using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using WwTool.UI.Theming;

namespace WwTool.UI.Pages.Accounts;

public partial class LoginDialogView : UserControl
{
    private LoginDialogViewModel? _vm;

    public LoginDialogView() => InitializeComponent();

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        EmailBox.Focus();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm is not null) _vm.ErrorRaised -= OnErrorRaised;
        _vm = DataContext as LoginDialogViewModel;
        if (_vm is not null) _vm.ErrorRaised += OnErrorRaised;
    }

    /// <summary>动效「错误抖动」：对应输入框左右轻摆，红色提示从上方 4 落下。</summary>
    private void OnErrorRaised(object? sender, EventArgs e)
    {
        ErrorText.Classes.Remove("shown");
        Dispatcher.UIThread.Post(() => ErrorText.Classes.Add("shown"), DispatcherPriority.Render);
        TextBox? box = _vm?.ErrorField switch
        {
            LoginField.Email => EmailBox,
            LoginField.Password => PasswordBox,
            _ => null,
        };
        if (box is null) return;
        box.Focus();
        _ = Motion.ShakeAsync(box);
    }
}
