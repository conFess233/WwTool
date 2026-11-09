using System.Net;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WwTool.Core.Abstractions;
using WwTool.Core.Abstractions.Platform;
using WwTool.Core.Data;
using WwTool.Core.Data.Repositories;
using WwTool.Core.Services;

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

    public static IServiceCollection AddWwToolCore(this IServiceCollection services, IAppPaths paths)
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
        services.AddSingleton<IGuideRepository, GuideRepository>();

        // 配置、日志、静态资料
        services.AddSingleton<IConfigService, ConfigService>();
        services.AddSingleton<ILoggerService, LoggerService>();
        services.AddSingleton<GameDataService>();
        services.AddSingleton<CatalogRepositorySource>();
        services.AddSingleton<CatalogSyncService>();

        // 业务服务
        services.AddSingleton<IUserDataService, UserDataService>();
        services.AddSingleton<IGachaLogLocator, GachaLogLocator>();
        services.AddSingleton<IGachaStatisticsService, GachaStatisticsService>();
        services.AddSingleton<IGuideApiClient, GuideApiClient>();
        services.AddSingleton<IGuideSyncService, GuideSyncService>();
        services.AddSingleton<IHttpService, HttpService>();
        services.AddSingleton<ILoginService, LoginService>();
        services.AddSingleton<IGetDataService, GetDataService>();
        return services;
    }
}
