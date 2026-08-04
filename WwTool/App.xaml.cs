using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using WwTool.Common;
using WwTool.Common.Context;
using WwTool.Common.Utils;
using WwTool.Services;
using WwTool.Services.Interfaces;
using WwTool.Services.Repositories;
using WwTool.UI.ViewModels;
using WwTool.UI.ViewModels.Dialogs;
using WwTool.UI.Views;
using WwTool.UI.Views.Dialogs;

namespace WwTool
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : PrismApplication
    {

        public App()
        {
            // 强制全局开启高清字体渲染
            TextOptions.TextFormattingModeProperty.OverrideMetadata(typeof(TextElement), new FrameworkPropertyMetadata(TextFormattingMode.Display));
        }

        protected override Window CreateShell()
        {
            DatabaseOptions databaseOptions = Container.Resolve<DatabaseOptions>();
            try
            {
                LocalDataService.EnsureWritableDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Local"));
                LocalDataService.EnsureWritableDirectory(Path.GetDirectoryName(databaseOptions.DatabasePath)!);
                LocalDataService.EnsureWritableDirectory(databaseOptions.BackupDirectory);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"程序目录不可写，无法安全加载配置或数据库。\n\n{ex.Message}",
                    "WwTool",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                throw;
            }

            var configService = Container.Resolve<IConfigService>();
            configService.LoadAll();

            var themeService = Container.Resolve<IThemeService>();
            themeService.Initialize();

            string productVersion = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion ?? "1.0.0";
            if (productVersion.Contains('+'))
            {
                productVersion = productVersion.Split('+')[0];
            }
            if (productVersion.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                productVersion = productVersion.Substring(1);
            }
            configService.App.AppVersion = productVersion;

            // 初始化多语言
            LanguageManager.Instance.ChangeLanguage(configService.User.AppLanguage);

            return Container.Resolve<MainView>();
        }

        protected override async void OnInitialized()
        {
            base.OnInitialized();

            // 初始化全局异常包装类
            var logger = Container.Resolve<ILoggerService>();
            var uiState = Container.Resolve<IUIStateService>();
            WwTool.Common.Utils.ExceptionHelper.Initialize(logger, uiState, Container.Resolve<IConfigService>());

            logger.Info("WwTool 客户端应用程序初始化中...");

            var localDb = Container.Resolve<LocalDataService>();
            var gameData = Container.Resolve<GameDataService>();
            try
            {
                await gameData.InitializeAsync();
                await localDb.InitializeAsync();
            }
            catch (Exception ex)
            {
                logger.Error("后台数据服务初始化时发生错误", ex);
                MessageBox.Show(
                    "本地数据初始化失败。原数据库和迁移备份已保留，请查看脱敏日志后重试。",
                    "WwTool",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(-1);
                return;
            }

            if (App.Current.MainWindow.DataContext is IConfigureService service)
            {
                await service.ConfigureAsync();
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            // 注册全局异常捕获
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            base.OnStartup(e);
        }

        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                var logger = Container.Resolve<ILoggerService>();
                logger.Fatal("主 UI 线程捕获到未处理的致命异常", e.Exception);
                WwTool.Common.Utils.ExceptionHelper.HandleException(e.Exception, "主UI线程异常");
            }
            catch (Exception loggingException)
            {
                Trace.TraceError($"记录 UI 未处理异常失败: {loggingException}");
            }

            // 标记异常已处理，防止程序崩溃退出
            e.Handled = true;
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                var logger = Container.Resolve<ILoggerService>();
                if (e.ExceptionObject is Exception ex)
                {
                    logger.Fatal("后台工作线程捕获到未处理的致命异常", ex);
                    WwTool.Common.Utils.ExceptionHelper.HandleException(ex, "后台线程异常");
                }
            }
            catch (Exception loggingException)
            {
                Trace.TraceError($"记录后台未处理异常失败: {loggingException}");
            }
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            try
            {
                var logger = Container.Resolve<ILoggerService>();
                logger.Error("未观察的 Task 异步任务抛出异常", e.Exception);
                WwTool.Common.Utils.ExceptionHelper.HandleException(e.Exception, "异步任务异常");
            }
            catch (Exception loggingException)
            {
                Trace.TraceError($"记录未观察任务异常失败: {loggingException}");
            }

            // 标记已被观察，阻止进程由于未捕获 Task 异常而终止
            e.SetObserved();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            var configService = Container.Resolve<IConfigService>();
            try
            {
                configService.SaveAll();
            }
            catch (Exception ex)
            {
                Trace.TraceError($"退出时保存配置失败: {ex}");
            }
            base.OnExit(e);
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<MainView, MainViewModel>();
            containerRegistry.RegisterForNavigation<IndexView, IndexViewModel>();
            containerRegistry.RegisterForNavigation<StatisticsView, StatisticsViewModel>();
            containerRegistry.RegisterForNavigation<SettingsView, SettingsViewModel>();
            containerRegistry.RegisterForNavigation<AboutView, AboutViewModel>();
            containerRegistry.RegisterSingleton<RoleDataViewModel>();
            containerRegistry.RegisterSingleton<GuideRoleDataViewModel>();
            containerRegistry.RegisterForNavigation<RoleDataView, GuideRoleDataViewModel>();
            containerRegistry.RegisterForNavigation<ExplorationDataView, RoleDataViewModel>();
            containerRegistry.RegisterForNavigation<MotorDataView, RoleDataViewModel>();
            containerRegistry.RegisterDialog<AlertView, AlertViewModel>();
            containerRegistry.RegisterDialog<LoginView, LoginViewModel>();

            var services = new ServiceCollection();

            services.AddHttpClient("WwToolClient")
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    AutomaticDecompression =
                        DecompressionMethods.GZip |
                        DecompressionMethods.Deflate |
                        DecompressionMethods.Brotli
                });

            var provider = services.BuildServiceProvider();

            containerRegistry.RegisterInstance(provider.GetRequiredService<IHttpClientFactory>());
            DatabaseOptions databaseOptions = DatabaseOptions.CreateDefault();
            var contextFactory = new AppDbContextFactory(databaseOptions);
            containerRegistry.RegisterInstance(databaseOptions);
            containerRegistry.RegisterInstance<IDbContextFactory<AppDbContext>>(contextFactory);
            containerRegistry.RegisterSingleton<LocalDataService>();
            containerRegistry.RegisterSingleton<GameDataService>();

            // 注册数据仓储。
            containerRegistry.RegisterSingleton<IGachaRepository, GachaRepository>();
            containerRegistry.RegisterSingleton<IUserRepository, UserRepository>();
            containerRegistry.RegisterSingleton<IPlayerInfoRepository, PlayerInfoRepository>();
            containerRegistry.RegisterSingleton<IDatabaseWriteCoordinator, DatabaseWriteCoordinator>();
            containerRegistry.RegisterSingleton<IUserDataService, UserDataService>();
            containerRegistry.RegisterSingleton<IGuideRepository, GuideRepository>();
            containerRegistry.RegisterSingleton<IGachaLogLocator, GachaLogLocator>();

            // 注册业务服务。
            containerRegistry.RegisterSingleton<IGachaStatisticsService, GachaStatisticsService>();
            containerRegistry.RegisterSingleton<IChartBuilderService, ChartBuilderService>();
            containerRegistry.RegisterSingleton<IGuideApiClient, GuideApiClient>();
            containerRegistry.RegisterSingleton<IGuideSyncService, GuideSyncService>();

            containerRegistry.RegisterSingleton<IConfigService, ConfigService>();
            containerRegistry.RegisterSingleton<ILoggerService, LoggerService>();
            containerRegistry.RegisterSingleton<IGetDataService, GetDataService>();
            containerRegistry.RegisterSingleton<IHttpService, HttpService>();
            containerRegistry.RegisterSingleton<ILoginService, LoginService>();
            containerRegistry.RegisterSingleton<IThemeService, ThemeService>();


            containerRegistry.RegisterSingleton<IUIStateService, UIStateService>();
            containerRegistry.RegisterSingleton<IDialogService, MyDialogService>();
        }
    }

}
