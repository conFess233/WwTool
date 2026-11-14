using System.Net;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WwTool.Core.Abstractions;
using WwTool.Core.Abstractions.Platform;
using WwTool.Core.Data;
using WwTool.Core.Data.Repositories;
using WwTool.Core.Services;
using WwTool.Core.Services.Gacha;

namespace WwTool.Core.Composition;

/// <summary>
/// 注册与界面无关的全部 Core 服务。平台实现（<see cref="IAppPaths"/>、<see cref="ISecretProtector"/> 等）、
/// <see cref="ILocalizer"/> 与 <see cref="ICatalogImageCache"/> 由界面层组合根提供。
/// </summary>
public static class CoreServiceCollectionExtensions
{
    /// <summary>主 API 客户端名。</summary>
    public const string ApiClient = "WwToolClient";

    /// <summary>资料仓库客户端名。</summary>
    public const string CatalogClient = "CatalogClient";

    /// <summary>资料图片客户端名。</summary>
    public const string CatalogImageClient = "CatalogImageClient";

    /// <summary>
    /// 桌面端与 Web 后端共用的基础设施：HTTP 客户端、配置、日志。
    /// 不含任何需要游戏凭据或桌面平台能力的服务。
    /// </summary>
    public static IServiceCollection AddWwToolCoreInfrastructure(this IServiceCollection services)
    {
        services.AddHttpClient(ApiClient)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            });
        services.AddHttpClient(CatalogClient, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WwTool-Catalog/1.0");
            client.DefaultRequestHeaders.Add("x-language", "zh-Hans");
        });
        services.AddHttpClient(CatalogImageClient, client => client.Timeout = TimeSpan.FromSeconds(10));
        services.AddSingleton<IConfigService, ConfigService>();
        services.AddSingleton<ILoggerService, LoggerService>();
        return services;
    }

    /// <summary>公开资讯（公告、启动器资讯、兑换码、深塔等挑战）：只请求无需凭据的公开接口。</summary>
    public static IServiceCollection AddWwToolPublicFeeds(this IServiceCollection services)
    {
        services.AddSingleton<LauncherBackgroundService>();
        services.AddSingleton<LauncherNewsService>();
        services.AddSingleton<WwTool.Core.Services.Notices.GameNoticeService>();
        services.AddSingleton<WwTool.Core.Services.Notices.KuroBbsCodeService>();
        services.AddSingleton<WwTool.Core.Services.Challenges.EncoreService>();
        return services;
    }

    /// <summary>
    /// 本地数据与统计：数据库、抽卡 / 账号仓储、静态资料、抽卡统计、通用 HTTP。桌面端与 Web 后端共用，
    /// 保证抽卡记录的 SourceOrder 规则与统计口径两端一致。不含登录、攻略站等需要游戏凭据的服务。
    /// 使用方还需提供 <see cref="ISecretProtector"/> 与 <see cref="ILocalizer"/>。
    /// </summary>
    public static IServiceCollection AddWwToolCoreData(this IServiceCollection services, IAppPaths paths)
    {
        // 数据库
        DatabaseOptions databaseOptions = DatabaseOptions.FromPaths(paths);
        services.AddSingleton(databaseOptions);
        services.AddSingleton<IDbContextFactory<AppDbContext>>(new AppDbContextFactory(databaseOptions));
        services.AddSingleton<IDatabaseWriteCoordinator, DatabaseWriteCoordinator>();
        services.AddSingleton<LocalDataService>();

        // 仓储
        services.AddSingleton<IGachaRepository, GachaRepository>();
        services.AddSingleton<IUserRepository, UserRepository>();
        services.AddSingleton<IPlayerInfoRepository, PlayerInfoRepository>();

        // 静态资料（随程序发布的内置资料；从仓库同步资料与图片只在桌面端注册）
        services.AddSingleton<GameDataService>();

        services.AddSingleton<IGachaStatisticsService, GachaStatisticsService>();
        services.AddSingleton<IHttpService, HttpService>();
        return services;
    }

    /// <summary>桌面端使用的全部 Core 服务。</summary>
    public static IServiceCollection AddWwToolCore(this IServiceCollection services, IAppPaths paths)
    {
        services.AddWwToolCoreInfrastructure();
        services.AddWwToolPublicFeeds();
        services.AddWwToolCoreData(paths);
        // 检查与下载更新：GitHub 接口要求 User-Agent；下载大文件时由各请求自己控制超时。
        services.AddHttpClient(WwTool.Core.Services.Update.UpdateService.ClientName, client =>
        {
            client.Timeout = Timeout.InfiniteTimeSpan;
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WwTool-Updater/2.0");
        });

        services.AddSingleton<IGuideRepository, GuideRepository>();
        services.AddSingleton<CatalogRepositorySource>();
        services.AddSingleton<CatalogSyncService>();
        services.AddSingleton<WwTool.Core.Services.Update.UpdateService>();
        services.AddSingleton<WwTool.Core.Services.Update.UpdateInstaller>();

        // 业务服务（含需要游戏凭据的登录与攻略站同步，只在桌面端注册）
        services.AddSingleton<IUserDataService, UserDataService>();
        services.AddSingleton<IGachaLogLocator, GachaLogLocator>();
        services.AddSingleton<IGuideApiClient, GuideApiClient>();
        services.AddSingleton<IGuideSyncService, GuideSyncService>();
        services.AddSingleton<ILoginService, LoginService>();
        services.AddSingleton<IGetDataService, GetDataService>();
        services.AddSingleton<IAccountLoginService, AccountLoginService>();
        services.AddSingleton<IPullsService, PullsService>();
        return services;
    }
}
