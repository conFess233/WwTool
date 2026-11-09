using Avalonia.Controls;
using Avalonia.Interactivity;

namespace WwTool.UI.Pages.Accounts;

public partial class LoginDialogView : UserControl
{
    public LoginDialogView() => InitializeComponent();

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        EmailBox.Focus();
    }
}
