using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using WwTool.UI.Services;
using WwTool.UI.Shell;

namespace WwTool.UI;

/// <summary>
/// 应用程序与唯一组合根。服务注册在后续模块迁移时逐步加入。
/// </summary>
public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 阶段 0：主题服务先手动创建，正式组合根（Generic Host）在 Shell 模块中接入。
            var theme = new ThemeService();
            theme.Apply("WarmWhite", ThemeService.FollowTheme);
            desktop.MainWindow = new MainWindow(theme);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
