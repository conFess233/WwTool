using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using WwTool.Core.Abstractions;
using WwTool.Core.Abstractions.Platform;
using WwTool.Core.Composition;
using WwTool.Core.Services;
using WwTool.Web.Auth;
using WwTool.Web.Data;
using WwTool.Web.Dev;
using WwTool.Web.Platform;
using WwTool.Web.Public;
using WwTool.Web.Roles;
using WwTool.Core.Data.Repositories;
using WwTool.Web.Uids;

var builder = WebApplication.CreateBuilder(args);

// 目录：启动时决定一次，不可写直接失败（与桌面端 IAppPaths 规则一致）
var paths = new WebAppPaths(builder.Configuration["WwTool:DataRoot"]);
paths.EnsureWritable();
builder.Services.AddSingleton<IAppPaths>(paths);
builder.Services.AddSingleton<ISecretProtector, RejectingSecretProtector>();
builder.Services.AddSingleton<ILocalizer, WebLocalizer>();

// 只注册与游戏凭据无关的 Core 服务
builder.Services.AddWwToolCoreInfrastructure();
builder.Services.AddWwToolPublicFeeds();
builder.Services.AddWwToolCoreData(paths);

// Web 专属数据库：站点账号、UID 绑定
string webDbPath = Path.Combine(paths.DataDirectory, "WebSite.db");
builder.Services.AddDbContextFactory<WebDbContext>(o => o.UseSqlite($"Data Source={webDbPath}"));
builder.Services.AddDbContext<WebDbContext>(o => o.UseSqlite($"Data Source={webDbPath}"), optionsLifetime: ServiceLifetime.Singleton);

builder.Services.AddSingleton<PublicNoticeService>();
builder.Services.AddSingleton<PublicFeedCache>();
builder.Services.AddSingleton<UidBindingService>();
builder.Services.AddSingleton<GachaImportService>();
builder.Services.AddScoped<ProfileService>();
// 角色数据：攻略站接口由后端无状态转发（Guide Token 不落库），只保存角色快照
builder.Services.AddSingleton<IGuideRepository, GuideRepository>();
builder.Services.AddSingleton<IGuideApiClient, GuideApiClient>();
builder.Services.AddSingleton<RoleService>();
builder.Services.AddScoped<GachaQueries>();
builder.Services.AddScoped<UidDataService>();

// 站点会话：HttpOnly + SameSite=Strict；接口未登录返回 401 而不是跳转
// 登录 Cookie 的加密密钥放在数据目录：容器或 Linux 部署重启后会话仍然有效（Windows 默认在用户目录，其他平台默认只在内存）
builder.Services.AddDataProtection()
    .SetApplicationName("WwTool.Web")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(paths.DataDirectory, "keys")));
builder.Services.AddAuthentication(AuthEndpoints.Scheme).AddCookie(AuthEndpoints.Scheme, o =>
{
    o.Cookie.Name = "wwtool.session";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    o.ExpireTimeSpan = TimeSpan.FromDays(30);
    o.SlidingExpiration = true;
    o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
    o.Events.OnValidatePrincipal = AuthEndpoints.ValidatePrincipalAsync;
    o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
});
builder.Services.AddAuthorization();

// 限流：按站点账号（未登录按 IP）分区
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    static string Partition(HttpContext c) => c.User.UserId()?.ToString() ?? c.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    o.AddPolicy(RateLimits.Gacha, c => RateLimitPartition.GetFixedWindowLimiter(Partition(c), _ => new FixedWindowRateLimiterOptions { PermitLimit = 6, Window = TimeSpan.FromMinutes(10) }));
    o.AddPolicy(RateLimits.Profile, c => RateLimitPartition.GetFixedWindowLimiter(Partition(c), _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(10) }));
    // 访客统计：每次查看抽卡页、切换图表筛选都会请求，额度宽一些
    o.AddPolicy(RateLimits.Compute, c => RateLimitPartition.GetFixedWindowLimiter(Partition(c), _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(10) }));
    o.AddPolicy(RateLimits.Export, c => RateLimitPartition.GetFixedWindowLimiter(Partition(c), _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(10) }));
    o.AddPolicy(RateLimits.Auth, c => RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(5) }));
});

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});
// 请求体上限：默认 1 MB（账号资料最大 512 KB）；访客统计与上传记录的接口单独放宽
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 1024 * 1024);

// 反向代理（Caddy、Nginx、Cloudflare Tunnel 等）：只信任配置里的代理地址发来的转发头，
// 据此取得访客真实 IP（限流按 IP 分区）与原始协议（https 时会话 Cookie 才带 Secure）。未开启时忽略转发头，防止直连时伪造。
var proxy = builder.Configuration.GetSection("WwTool:ReverseProxy");
bool behindProxy = proxy.GetValue<bool>("Enabled");
if (behindProxy)
{
    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        o.ForwardLimit = proxy.GetValue("ForwardLimit", 1);
        // 默认只信任本机（127.0.0.0/8 与 ::1）；代理在其他机器或容器网络里时在 KnownNetworks 里写它的网段
        foreach (string cidr in proxy.GetSection("KnownNetworks").Get<string[]>() ?? [])
            o.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(cidr));
        // 例如 Cloudflare 的 CF-Connecting-IP：只含访客 IP，不受客户端自带的 X-Forwarded-For 影响
        if (proxy["ClientIpHeader"] is { Length: > 0 } header) o.ForwardedForHeaderName = header;
    });
}

var app = builder.Build();

if (behindProxy) app.UseForwardedHeaders();
// 只在 https 请求上发送；本机 http 开发不受影响
if (!app.Environment.IsDevelopment()) app.UseHsts();

// 启动初始化：配置 → Core 数据库迁移 → 静态资料 → Web 数据库
app.Services.GetRequiredService<IConfigService>().LoadAll();
await app.Services.GetRequiredService<LocalDataService>().InitializeAsync();
await app.Services.GetRequiredService<GameDataService>().InitializeAsync();
await using (var scope = app.Services.CreateAsyncScope())
{
    // TODO 上线前改为 EF 迁移；规划稿阶段用 EnsureCreated
    await scope.ServiceProvider.GetRequiredService<WebDbContext>().Database.EnsureCreatedAsync();
}

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["Referrer-Policy"] = "no-referrer";
    headers["X-Frame-Options"] = "DENY";
    headers["Content-Security-Policy"] = app.Configuration["WwTool:ContentSecurityPolicy"];
    await next();
});

// CSRF：所有改动数据的 /api 请求必须带自定义头。跨站表单无法设置自定义头，跨站脚本又被同源策略拦住，
// 再配合 SameSite=Strict 的会话 Cookie。
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api")
        && !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method)
        && context.Request.Headers["X-WwTool"] != "1")
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }
    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

var api = app.MapGroup("/api");
var pub = api.MapGroup("/public");

pub.MapGet("/notices", async (string? region, string? lang, PublicNoticeService notices, CancellationToken token) =>
{
    LauncherRegion launcherRegion = string.Equals(region, "china", StringComparison.OrdinalIgnoreCase) ? LauncherRegion.China : LauncherRegion.Global;
    NoticeFeedDto? feed = await notices.GetAsync(launcherRegion, lang ?? "zh-Hans", token);
    return feed is null ? Results.Problem("公告暂时无法获取", statusCode: StatusCodes.Status503ServiceUnavailable)
        : Results.Ok(new { feed, recurring = PublicFeedEndpoints.Recurring(feed.VersionStart, feed.VersionEnd) });
});
pub.MapPublicFeeds();
pub.MapBackground();
pub.MapLauncherNews();

var authGroup = api.MapGroup("").RequireRateLimiting(RateLimits.Auth);
authGroup.MapAuth();
api.MapMe();
api.MapUids();
api.MapGuest();
api.MapRoles();
api.MapDevTools(app.Environment, app.Configuration);

api.MapFallback(() => Results.NotFound());
// 前端路由：非 /api 的未知路径回到 index.html
app.MapFallbackToFile("index.html");

app.Run();
