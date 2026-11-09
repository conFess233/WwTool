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
using WwTool.Core.Enums;
using WwTool.UI.Feedback;
using WwTool.UI.Localization;
using WwTool.UI.Navigation;
using WwTool.UI.Pages.Pulls;
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
        var uiState = services.GetRequiredService<IUIStateService>();
        var localization = services.GetRequiredService<LocalizationService>();
        RegisterGlobalExceptionHandlers(logger, uiState, localization);
        config.UserAutoSaveFailed += (_, _) => uiState.ShowToast(new NotificationRequest
        {
            Title = localization["Toast_Error"],
            Message = localization["Settings_AutoSaveFailed"],
            Type = NotificationType.Warning,
            Source = "Config",
            DedupeKey = "autosave-failed",
        });
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
        services.AddSingleton<IUIStateService, UIStateService>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<IDialogService>(sp => sp.GetRequiredService<DialogService>());
        services.AddSingleton<FabMenuViewModel>();
        services.AddSingleton<ErrorReporter>();
        services.AddSingleton<AccountContext>();

        // 抽卡统计：三个视图模型共享 PullsSession，彼此不直接依赖。
        services.AddSingleton<PullsSession>();
        services.AddSingleton<PoolRecordsViewModel>();
        services.AddSingleton<PullsOverviewViewModel>();
        services.AddSingleton<PullsChartsViewModel>();

        // 页面登记（顺序即悬浮岛顺序）与视图映射
        const string icons = "avares://WwTool/Assets/Images/";
        services.AddSingleton(Placeholder("home", "Nav_Home", icons + "Home.png",
            ("refresh", "Index_RefreshAccountList", icons + "Refresh.png", null),
            ("add-account", "Index_AddAccount", icons + "Add.png", null)));
        services.AddSingleton(new PageDescriptor("pulls", "Nav_Statistics", icons + "Statistics.png",
            sp => sp.GetRequiredService<PullsOverviewViewModel>()));
        services.AddSingleton(Placeholder("roles", "Nav_RoleData", icons + "Role.png"));
        services.AddSingleton(Placeholder("explore", "Nav_Exploration", icons + "Chest.png"));
        services.AddSingleton(Placeholder("motor", "Nav_Motor", icons + "Motor.png"));
        services.AddSingleton(new PageDescriptor("settings", "Nav_Settings", icons + "Settings.png",
            sp => ActivatorUtilities.CreateInstance<SettingsViewModel>(sp), StartsGroup: true));
        services.AddSingleton(new PageDescriptor(PullsChartsViewModel.PageKey, "Pulls_DetailedCharts", icons + "Graph.png",
            sp => sp.GetRequiredService<PullsChartsViewModel>(), ParentKey: "pulls"));
        services.AddSingleton(new ViewLocator()
            .Map<ConfirmDialogViewModel, ConfirmDialogView>()
            .Map<PlaceholderPageViewModel, PlaceholderPageView>()
            .Map<SettingsViewModel, SettingsView>()
            .Map<PullsOverviewViewModel, PullsOverviewView>()
            .Map<PullsChartsViewModel, PullsChartsView>());
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<ShellViewModel>();

        return builder.Build();
    }

    /// <summary>
    /// 尚未迁移页面的登记项。悬浮按钮动作先按定稿放好（第四项为危险动作时给出确认文案键），
    /// 执行时只提示“正在迁移”；页面迁移后换成真实视图模型。
    /// </summary>
    private static PageDescriptor Placeholder(string key, string titleKey, string icon,
        params (string Key, string TitleKey, string Icon, string? DangerConfirmKey)[] actions) =>
        new(key, titleKey, icon, sp =>
        {
            var localization = sp.GetRequiredService<LocalizationService>();
            var uiState = sp.GetRequiredService<IUIStateService>();
            FabAction[] fab = actions.Select(a => new FabAction(a.Key, a.TitleKey, a.Icon,
                new CommunityToolkit.Mvvm.Input.RelayCommand(() => uiState.ShowToast(new NotificationRequest
                {
                    Title = localization[a.TitleKey],
                    Message = localization["Toast_FeatureMigrating"],
                    Priority = NotificationPriority.Important,
                    Source = "Placeholder",
                })),
                localization, isDanger: a.DangerConfirmKey is not null, confirmMessageKey: a.DangerConfirmKey)).ToArray();
            return new PlaceholderPageViewModel(titleKey, localization, fab);
        });

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

        try
        {
            await services.GetRequiredService<AccountContext>().RefreshAsync();
        }
        catch (Exception ex)
        {
            // 账号列表读不出来不阻止启动：页面会显示“未选择账号”，并可从胶囊重试。
            services.GetRequiredService<ErrorReporter>().Report(ex, services.GetRequiredService<LocalizationService>()["Msg_LoadingLocalAccount"]);
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

    /// <summary>
    /// 最后防线：记录完整异常并附带错误编号；界面只显示简短错误 Toast 与编号，不暴露异常细节。
    /// UI 线程异常标记为已处理，避免非致命错误导致进程退出。
    /// </summary>
    private static void RegisterGlobalExceptionHandlers(ILoggerService logger, IUIStateService uiState, LocalizationService localization)
    {
        void Report(string context, Exception ex, bool fatal)
        {
            string code = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            string message = $"{context}（错误编号 {code}）";
            if (fatal) logger.Fatal(message, ex);
            else logger.Error(message, ex);
            uiState.ShowToast(new NotificationRequest
            {
                Title = localization["Exc_SystemErrorTitle"],
                Message = string.Format(localization["Exc_UnknownSystemError"], code),
                Type = NotificationType.Error,
                Source = "Unhandled",
                DedupeKey = ex.GetType().FullName,
            });
        }

        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            Report("主 UI 线程捕获到未处理的异常", e.Exception, fatal: true);
            e.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) Report("后台线程捕获到未处理的致命异常", ex, fatal: true);
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Report("未观察的 Task 异步任务抛出异常", e.Exception, fatal: false);
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
