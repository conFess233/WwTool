using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace WwTool.UI.Shell;

/// <summary>
/// 启动失败时的独立提示窗口（主题与 Shell 可能尚未就绪，只用系统默认外观）。关闭后应用退出。
/// </summary>
public sealed class StartupErrorWindow : Window
{
    public StartupErrorWindow(string title, string message, string okText)
    {
        Title = title;
        Width = 520;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var ok = new Button { Content = okText, HorizontalAlignment = HorizontalAlignment.Right, MinWidth = 88 };
        AutomationProperties.SetAutomationId(ok, "startup-error-ok");
        ok.Click += (_, _) => Close();

        Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(24),
            Spacing = 20,
            Children =
            {
                new TextBlock { Text = title, FontSize = 18, FontWeight = FontWeight.SemiBold },
                new SelectableTextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                ok,
            },
        };
    }
}
