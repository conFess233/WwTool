using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WwTool.Core.Abstractions;
using WwTool.Core.Abstractions.Platform;
using WwTool.Core.Composition;
using WwTool.Core.Infrastructure;
using WwTool.Core.Localization;
using WwTool.Core.Platform;
using WwTool.Core.Services;
using WwTool.UI.Localization;
using WwTool.UI.Navigation;
using WwTool.UI.Services;
using WwTool.UI.Shell;
using WwTool.UI.ViewModels;
using WwTool.UI.Views;

namespace WwTool.UI;

/// <summary>
/// 应用程序与唯一组合根：构建 Generic Host、注册服务与页面，按规范 01 §5.1 的顺序启动。
/// </summary>
public partial class App : Application
{
    private readonly CancellationTokenSource _shutdown = new();
    private IHost? _host;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Start(desktop);
        }
        base.OnFrameworkInitializationCompleted();
    }

    private void Start(IClassicDesktopStyleApplicationLifetime desktop)
    {
        // 1. 数据目录：不可写就停止，绝不换目录。
        var paths = new AppPaths();
        try
        {
            paths.EnsureWritable();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var fallback = new LocalizationService(paths);
            ShowStartupError(desktop, fallback, string.Format(fallback["Startup_DataDirNotWritable"], paths.DataRoot) + "\n\n" + ex.Message);
            return;
        }

        _host = BuildHost(paths);
        IServiceProvider services = _host.Services;

        // 2. 配置（损坏时由配置服务备份并回退默认值）。
        var config = services.GetRequiredService<IConfigService>();
        config.LoadAll();
        config.App.AppVersion = GetProductVersion();

        // 3. 日志与全局异常处理。
        var logger = services.GetRequiredService<ILoggerService>();
        RegisterGlobalExceptionHandlers(logger);
        logger.Info("WwTool 2.0 启动中...");

        // 6. 语言、主题（先于窗口创建，首帧即为正确外观）。
        TrExtension.Service = services.GetRequiredService<LocalizationService>();
        services.GetRequiredService<AppearanceCoordinator>().Start();
        DataTemplates.Add(services.GetRequiredService<ViewLocator>());

        // 7. Shell 与首屏；4、5 两步（数据库、静态资料）在窗口显示后异步完成，完成前页面区显示加载状态。
        var shell = services.GetRequiredService<ShellViewModel>();
        desktop.MainWindow = new MainWindow(shell);
        desktop.ShutdownRequested += (_, _) => OnShutdown();
        Dispatcher.UIThread.Post(() => _ = InitializeDataAsync(desktop, services, shell));
    }

    private static IHost BuildHost(AppPaths paths)
    {
        // 不使用默认配置源与日志提供程序：配置与日志都由应用自己的服务负责。
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        IServiceCollection services = builder.Services;

        // 平台实现
        services.AddSingleton<IAppPaths>(paths);
        services.AddSingleton<ISecretProtector>(new VersionedSecretProtector(Path.Combine(paths.ConfigDirectory, "secret.key")));
        services.AddSingleton<IAppLauncher, ShellAppLauncher>();

        services.AddWwToolCore(paths);

        // 界面基础服务
        services.AddSingleton<LocalizationService>(sp => new LocalizationService(sp.GetRequiredService<IAppPaths>(), sp.GetRequiredService<ILoggerService>()));
        services.AddSingleton<ILocalizer>(sp => sp.GetRequiredService<LocalizationService>());
        services.AddSingleton<ThemeService>();
        services.AddSingleton<AppearanceCoordinator>();
        services.AddSingleton<AvaloniaCatalogImageService>();
        services.AddSingleton<ICatalogImageCache>(sp => sp.GetRequiredService<AvaloniaCatalogImageService>());

        // 页面登记（顺序即悬浮岛顺序）与视图映射
        const string icons = "avares://WwTool/Assets/Images/";
        services.AddSingleton(Placeholder("home", "Nav_Home", icons + "Home.png"));
        services.AddSingleton(Placeholder("pulls", "Nav_Statistics", icons + "Statistics.png"));
        services.AddSingleton(Placeholder("roles", "Nav_RoleData", icons + "Role.png"));
        services.AddSingleton(Placeholder("explore", "Nav_Exploration", icons + "Chest.png"));
        services.AddSingleton(Placeholder("motor", "Nav_Motor", icons + "Motor.png"));
        services.AddSingleton(new PageDescriptor("settings", "Nav_Settings", icons + "Settings.png",
            sp => ActivatorUtilities.CreateInstance<SettingsViewModel>(sp), StartsGroup: true));
        services.AddSingleton(new ViewLocator()
            .Map<PlaceholderPageViewModel, PlaceholderPageView>()
            .Map<SettingsViewModel, SettingsView>());
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<ShellViewModel>();

        return builder.Build();
    }

    private static PageDescriptor Placeholder(string key, string titleKey, string icon) =>
        new(key, titleKey, icon, sp => new PlaceholderPageViewModel(titleKey, sp.GetRequiredService<LocalizationService>()));

    private async Task InitializeDataAsync(IClassicDesktopStyleApplicationLifetime desktop, IServiceProvider services, ShellViewModel shell)
    {
        var logger = services.GetRequiredService<ILoggerService>();
        try
        {
            // 4. 数据库（识别旧库、备份、迁移）；5. 静态游戏资料。
            await services.GetRequiredService<GameDataService>().InitializeAsync();
            await services.GetRequiredService<LocalDataService>().InitializeAsync();
            await services.GetRequiredService<CatalogSyncService>().InitializeAsync();
        }
        catch (Exception ex)
        {
            logger.Error("后台数据服务初始化时发生错误", ex);
            var localization = services.GetRequiredService<LocalizationService>();
            Window? main = desktop.MainWindow;
            ShowStartupError(desktop, localization, localization["Startup_InitFailed"]);
            main?.Close();
            return;
        }

        services.GetRequiredService<INavigationService>().NavigateTo("home");
        shell.IsReady = true;
        logger.Info("WwTool 2.0 初始化完成");
        _ = services.GetRequiredService<CatalogSyncService>().SyncAsync(false, _shutdown.Token);
    }

    private static void ShowStartupError(IClassicDesktopStyleApplicationLifetime desktop, LocalizationService localization, string message)
    {
        var window = new StartupErrorWindow(localization["Startup_ErrorTitle"], message, localization["Common_Ok"]);
        window.Closed += (_, _) => desktop.Shutdown(1);
        desktop.MainWindow = window;
        window.Show();
    }

    private void OnShutdown()
    {
        _shutdown.Cancel();
        if (_host is null) return;
        try
        {
            _host.Services.GetRequiredService<IConfigService>().SaveAll();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"退出时保存配置失败: {ex}");
        }
        _host.Dispose();
        _host = null;
    }

    private static void RegisterGlobalExceptionHandlers(ILoggerService logger)
    {
        // 最后防线：只记录并阻止进程因非致命异常退出；用户提示在 Toast 服务接入后补上。
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            logger.Fatal("主 UI 线程捕获到未处理的异常", e.Exception);
            e.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) logger.Fatal("后台线程捕获到未处理的致命异常", ex);
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            logger.Error("未观察的 Task 异步任务抛出异常", e.Exception);
            e.SetObserved();
        };
    }

    private static string GetProductVersion()
    {
        string version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.0";
        version = version.Split('+')[0];
        return version.StartsWith('v') || version.StartsWith('V') ? version[1..] : version;
    }
}
