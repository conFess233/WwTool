using Avalonia.Controls;
using Avalonia.Interactivity;

namespace WwTool.UI.Pages.Pulls;

public partial class ImportUrlDialogView : UserControl
{
    public ImportUrlDialogView() => InitializeComponent();

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        UrlBox.Focus();
        UrlBox.SelectAll();
    }
}
