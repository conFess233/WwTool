using WwTool.Core.Abstractions;
using WwTool.Core.Enums;
using WwTool.Core.Exceptions;
using WwTool.Core.Infrastructure;
using WwTool.Core.Models.Api.Requests;
using WwTool.Core.Models.Domain;

namespace WwTool.Core.Services.Gacha;

/// <summary>
/// 一个账号的全部抽卡统计结果。各卡池结果与 1.x 统计页使用同一计算链，受本地统计基线约束。
/// </summary>
public sealed class PullsSnapshot
{
    public required string Uid { get; init; }

    /// <summary>每种卡池一项（含无记录的卡池）。</summary>
    public required IReadOnlyDictionary<CardPoolType, GachaStatisticsResult> Pools { get; init; }

    public required GlobalStatisticsResult Global { get; init; }

    public required GachaInsights Insights { get; init; }

    /// <summary>全部记录，按卡池枚举顺序拼接、卡池内为存储顺序。</summary>
    public required IReadOnlyList<GachaPull> AllRecords { get; init; }

    /// <summary>各卡池的记录（存储顺序）。</summary>
    public required IReadOnlyDictionary<CardPoolType, IReadOnlyList<GachaPull>> RecordsByPool { get; init; }

    public int RecordCount => AllRecords.Count;

    public bool IsEmpty => AllRecords.Count == 0;

    public static PullsSnapshot Empty(string uid, IGachaStatisticsService statistics, string languageCode) =>
        PullsService.Calculate(uid, new Dictionary<CardPoolType, IReadOnlyList<GachaPull>>(), statistics, languageCode);
}

/// <summary>抽卡链接属于另一个账号。消息不含完整 UID。</summary>
public sealed class GachaLinkAccountMismatchException(string linkUidMasked, string accountUidMasked)
    : WwToolException($"抽卡链接属于账号 {linkUidMasked}，与当前账号 {accountUidMasked} 不一致。")
{
    public string LinkUidMasked { get; } = linkUidMasked;

    public string AccountUidMasked { get; } = accountUidMasked;
}

/// <summary>抽卡数据的应用服务：读取并统计本地记录、从服务端同步。</summary>
public interface IPullsService
{
    /// <summary>读取账号全部卡池（按存储顺序）并计算统计；在线程池执行。</summary>
    Task<PullsSnapshot> LoadAsync(string uid, string languageCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// 用抽卡链接同步全部卡池到本地，返回新增记录数。请求任何卡池之前先校验链接属于该账号，
    /// 避免把别的账号的记录写进当前 UID。<paramref name="progress"/> 在开始每个卡池时报告。
    /// </summary>
    Task<int> SyncFromServerAsync(
        string uid,
        string queryUrl,
        GachaServerRegion region,
        string languageCode,
        IProgress<CardPoolType>? progress = null,
        CancellationToken cancellationToken = default);
}

public sealed class PullsService(
    IUserDataService userData,
    IGetDataService getData,
    IGachaStatisticsService statistics,
    ILoggerService logger) : IPullsService
{
    public Task<PullsSnapshot> LoadAsync(string uid, string languageCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uid);
        return Task.Run(async () =>
        {
            var pools = new Dictionary<CardPoolType, IReadOnlyList<GachaPull>>();
            foreach (CardPoolType type in Enum.GetValues<CardPoolType>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                pools[type] = await userData.ReadGachaInSourceOrderAsync(uid, (int)type, cancellationToken);
            }
            return Calculate(uid, pools, statistics, languageCode);
        }, cancellationToken);
    }

    public async Task<int> SyncFromServerAsync(
        string uid,
        string queryUrl,
        GachaServerRegion region,
        string languageCode,
        IProgress<CardPoolType>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uid);
        ArgumentException.ThrowIfNullOrWhiteSpace(queryUrl);
        GachaRequest template = GachaUrlParser.Parse(queryUrl);
        EnsureLinkBelongsToAccount(template, uid);

        int added = 0;
        foreach (CardPoolType type in Enum.GetValues<CardPoolType>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(type);
            GachaRequest request = GachaUrlParser.Parse(queryUrl);
            request.LanguageCode = languageCode;
            request.CardPoolType = (int)type;
            IEnumerable<GachaPull> records = await getData.GetGachaLogAsync(request, region, cancellationToken);
            added += await userData.ImportGachaAsync(uid, (int)type, records, "remote", cancellationToken);
        }
        logger.Info($"抽卡同步完成，新增 {added} 条。");
        return added;
    }

    /// <summary>
    /// 与 1.x 统计页相同的计算链：各卡池 OrganizeData → 角色活动池的赢/歪/限定计数进入全局统计 → 全部记录计算图表数据。
    /// </summary>
    internal static PullsSnapshot Calculate(
        string uid,
        IReadOnlyDictionary<CardPoolType, IReadOnlyList<GachaPull>> recordsByPool,
        IGachaStatisticsService statistics,
        string languageCode)
    {
        var all = new List<GachaPull>();
        var results = new Dictionary<CardPoolType, GachaStatisticsResult>();
        var byPool = new Dictionary<CardPoolType, IReadOnlyList<GachaPull>>();
        foreach (CardPoolType type in Enum.GetValues<CardPoolType>())
        {
            IReadOnlyList<GachaPull> records = recordsByPool.TryGetValue(type, out var r) ? r : [];
            byPool[type] = records;
            all.AddRange(records);
            results[type] = statistics.OrganizeData(records, type, languageCode);
        }

        GachaStatisticsResult character = results[CardPoolType.CharacterEvent];
        GlobalStatisticsResult global = statistics.CalculateGlobalStatistics(
            results.Values.Select(x => x.PoolStatistics), character.SuccessCount, character.MissCount, character.FeaturedCount);

        return new PullsSnapshot
        {
            Uid = uid,
            Pools = results,
            Global = global,
            Insights = statistics.CalculateInsights(all),
            AllRecords = all,
            RecordsByPool = byPool,
        };
    }

    private static void EnsureLinkBelongsToAccount(GachaRequest request, string uid)
    {
        if (!string.Equals(request.PlayerId, uid, StringComparison.Ordinal))
        {
            throw new GachaLinkAccountMismatchException(MaskUid(request.PlayerId), MaskUid(uid));
        }
    }

    internal static string MaskUid(string? uid) =>
        string.IsNullOrEmpty(uid) || uid.Length <= 4 ? "****" : uid[..^4] + "****";
}
