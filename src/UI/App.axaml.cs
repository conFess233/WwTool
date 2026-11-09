using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using WwTool.Core.Enums;
using WwTool.Core.Infrastructure;
using WwTool.Core.Localization;
using WwTool.UI.Localization;
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
            // 阶段 0：服务先手动创建，正式组合根（Generic Host）在 Shell 模块中接入。
            var paths = new AppPaths();
            var localization = new LocalizationService(paths);
            localization.ChangeLanguage(LanguageType.ZhHans);
            TrExtension.Service = localization;
            Resources["Loc"] = localization;

            var theme = new ThemeService();
            theme.Apply("WarmWhite", ThemeService.FollowTheme);
            desktop.MainWindow = new MainWindow(theme, localization);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
