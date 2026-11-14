using System.Collections.Concurrent;
using WwTool.Core.Abstractions;
using WwTool.Core.Data.Repositories;
using WwTool.Core.Enums;
using WwTool.Core.Exceptions;
using WwTool.Core.Infrastructure;
using WwTool.Core.Models.Api.Requests;
using WwTool.Core.Models.Api.Responses;
using WwTool.Core.Models.Domain;

namespace WwTool.Web.Uids;

public sealed record GachaImportResult(string Uid, string Region, int Added, bool Transferred);

/// <summary>一次完整拉取的结果：每个卡池的记录保持接口返回顺序（从新到旧）。</summary>
public sealed record GachaFetchResult(string Uid, string Region, IReadOnlyDictionary<CardPoolType, IReadOnlyList<GachaPull>> Pools);

/// <summary>抽卡链接无效、过期或与账号不符。</summary>
public sealed class GachaLinkException(string message) : UserFacingException(message);

/// <summary>
/// 抽卡链接：验证 UID 所有权并导入、为未登录访客临时拉取、上传浏览器本地保存的记录。
/// <list type="bullet">
/// <item>后端从不请求用户提交的 URL：只用 <see cref="GachaUrlParser"/> 取参数，主机只用来区分国服 / 国际服，请求固定的抽卡接口（防 SSRF）。</item>
/// <item>链接只在内存中使用，不落库、不写日志。</item>
/// <item>写入走 Core 的 <see cref="IGachaRepository"/>，与桌面端同一套 SourceOrder 规则。</item>
/// </list>
/// </summary>
public sealed class GachaImportService(
    IHttpService http,
    IConfigService config,
    IGachaRepository gacha,
    UidBindingService bindings,
    ILoggerService logger)
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> UidLocks = new();

    /// <summary>验证链接并把 UID 绑定到 <paramref name="userId"/>（必要时从原账号转移），然后导入全部卡池。</summary>
    public async Task<GachaImportResult> VerifyAndImportAsync(Guid userId, string url, string? expectedUid, CancellationToken token)
    {
        (GachaRequest template, GachaServerRegion region) = Parse(url);
        string uid = template.PlayerId;
        if (expectedUid is not null && expectedUid != uid)
            throw new GachaLinkException($"这个抽卡链接属于 UID {UidBindingService.Mask(uid)}，不是当前选择的 UID。");

        SemaphoreSlim gate = UidLocks.GetOrAdd(uid, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(token);
        try
        {
            // 先请求一个卡池作为所有权证明：链接无效或过期时接口返回失败
            IReadOnlyList<GachaPull> first = await FetchPoolAsync(template, CardPoolType.CharacterEvent, region, token);

            bool transferred = await bindings.BindUidAsync(userId, uid, RegionKey(region), "gacha", token);

            int added = await gacha.SyncGachaDataAsync(uid, (int)CardPoolType.CharacterEvent, first, "remote", token);
            foreach (CardPoolType pool in Enum.GetValues<CardPoolType>().Where(p => p != CardPoolType.CharacterEvent))
            {
                token.ThrowIfCancellationRequested();
                IReadOnlyList<GachaPull> records = await FetchPoolAsync(template, pool, region, token);
                added += await gacha.SyncGachaDataAsync(uid, (int)pool, records, "remote", token);
            }

            await bindings.MarkSourceAsync(uid, "gacha", "server", token);
            logger.Info($"Web 抽卡导入完成：UID {UidBindingService.Mask(uid)}，新增 {added} 条。");
            return new GachaImportResult(uid, RegionKey(region), added, transferred);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>未登录访客：拉取全部卡池后直接返回，后端不保存任何内容。</summary>
    public async Task<GachaFetchResult> FetchAllAsync(string url, CancellationToken token)
    {
        (GachaRequest template, GachaServerRegion region) = Parse(url);
        var pools = new Dictionary<CardPoolType, IReadOnlyList<GachaPull>>();
        foreach (CardPoolType pool in Enum.GetValues<CardPoolType>())
        {
            token.ThrowIfCancellationRequested();
            pools[pool] = await FetchPoolAsync(template, pool, region, token);
        }
        return new GachaFetchResult(template.PlayerId, RegionKey(region), pools);
    }

    /// <summary>
    /// 把浏览器本地保存的记录合并进已绑定的 UID。每个卡池按本地存储顺序作为一批写入，
    /// 已有的记录按指纹跳过，与桌面端导入相同。返回新增条数。
    /// </summary>
    public async Task<int> UploadAsync(string uid, IReadOnlyDictionary<CardPoolType, IReadOnlyList<GachaPull>> pools, CancellationToken token)
    {
        SemaphoreSlim gate = UidLocks.GetOrAdd(uid, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(token);
        try
        {
            int added = 0;
            foreach ((CardPoolType pool, IReadOnlyList<GachaPull> records) in pools)
            {
                try
                {
                    added += await gacha.SyncGachaDataAsync(uid, (int)pool, records, "import", token);
                }
                catch (InvalidDataException)
                {
                    throw new UserFacingException("上传的抽卡记录格式不正确。");
                }
            }
            if (added > 0) await bindings.MarkSourceAsync(uid, "gacha", "import", token);
            logger.Info($"Web 抽卡上传完成：UID {UidBindingService.Mask(uid)}，新增 {added} 条。");
            return added;
        }
        finally
        {
            gate.Release();
        }
    }

    private static (GachaRequest, GachaServerRegion) Parse(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || url.Length > 4096) throw new GachaLinkException("请粘贴完整的抽卡链接。");
        if (!GachaServerDetector.TryDetect(url, out GachaServerRegion region))
            throw new GachaLinkException("无法识别的抽卡链接：请从游戏内的唤取记录页面复制。");
        if (!GachaUrlParser.TryParse(url, out GachaRequest? request, out _) || request is null)
            throw new GachaLinkException("抽卡链接缺少必要参数，请重新从游戏内复制。");
        if (!IsUid(request.PlayerId)) throw new GachaLinkException("抽卡链接中的 UID 格式不正确。");
        return (request, region);
    }

    public static bool IsUid(string? uid) => uid is { Length: >= 6 and <= 12 } && uid.All(char.IsAsciiDigit);

    private async Task<IReadOnlyList<GachaPull>> FetchPoolAsync(GachaRequest template, CardPoolType pool, GachaServerRegion region, CancellationToken token)
    {
        var request = new GachaRequest
        {
            PlayerId = template.PlayerId,
            ServerId = template.ServerId,
            LanguageCode = template.LanguageCode,
            RecordId = template.RecordId,
            CardPoolType = (int)pool,
        };
        string endpoint = region == GachaServerRegion.International ? config.Api.Urls.GachaUrlNET : config.Api.Urls.GachaUrlCN;
        GachaResponse<List<GachaData>>? response;
        try
        {
            response = await http.PostAsync<GachaRequest, GachaResponse<List<GachaData>>>(endpoint, request, cancellationToken: token);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or WwToolException && !token.IsCancellationRequested)
        {
            throw new GachaLinkException("暂时无法连接抽卡服务器，请稍后重试。");
        }
        if (response is null || response.Code != 0)
            throw new GachaLinkException("抽卡链接已失效，请在游戏内重新打开唤取记录后复制新链接。");

        // 与 GetDataService.GetGachaLogAsync 相同的映射
        return (response.Data ?? []).Select(x => new GachaPull
        {
            CardPoolType = x.CardPoolType, ResourceId = x.ResourceId, ResourceType = x.ResourceType,
            Name = x.Name, Count = x.Count, Time = x.Time, QualityLevel = x.QualityLevel,
        }).ToList();
    }

    public static string RegionKey(GachaServerRegion region) => region == GachaServerRegion.International ? "global" : "china";
}

public static class Limits
{
    public const int MaxUidsPerUser = 10;
    public const int MaxProfileBytes = 512 * 1024;
    /// <summary>访客统计与上传请求体上限（约 3 万条记录）。</summary>
    public const long MaxRecordsBody = 6 * 1024 * 1024;
    public const int MaxRecordsPerPool = 20_000;
}
