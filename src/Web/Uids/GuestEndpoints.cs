using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using WwTool.Core.Abstractions;
using WwTool.Core.Enums;
using WwTool.Core.Models.Domain;
using WwTool.Core.Services.Gacha;
using WwTool.Web.Auth;

namespace WwTool.Web.Uids;

/// <summary>
/// 不登录站点也能用的抽卡统计。记录只保存在访客自己的浏览器里，后端不落库：
/// <c>fetch</c> 用抽卡链接拉取一次后原样返回；<c>overview</c> / <c>charts</c> 对浏览器传来的记录
/// 运行与桌面端相同的统计（<see cref="PullsService.Calculate"/>、<see cref="PullsChartBuilder"/>）后返回。
/// </summary>
public static class GuestEndpoints
{
    public sealed record FetchRequest(string Url);
    public sealed record StatsRequest(string Uid, Dictionary<int, List<RecordDto>> Pools);
    public sealed record ChartsRequest(string Uid, Dictionary<int, List<RecordDto>> Pools, int[]? Filter, string? Since);

    public static void MapGuest(this IEndpointRouteBuilder api)
    {
        var guest = api.MapGroup("/guest/gacha");

        guest.MapPost("/fetch", async (FetchRequest body, GachaImportService import, CancellationToken token) =>
        {
            try
            {
                GachaFetchResult result = await import.FetchAllAsync(body.Url, token);
                return Results.Ok(new
                {
                    result.Uid,
                    result.Region,
                    // 接口返回顺序（从新到旧），浏览器按同样的指纹规则合并
                    pools = result.Pools.ToDictionary(p => ((int)p.Key).ToString(CultureInfo.InvariantCulture), p => p.Value.Select(GachaMapper.Record)),
                });
            }
            catch (UserFacingException ex)
            {
                return AuthEndpoints.Problem(ex.Message);
            }
        }).RequireRateLimiting(RateLimits.Gacha);

        guest.MapPost("/overview", (StatsRequest body, string? lang, IGachaStatisticsService statistics) =>
        {
            if (Validate(body.Uid, body.Pools) is { } error) return error;
            var snapshot = PullsService.Calculate(body.Uid, ToPools(body.Uid, body.Pools), statistics, Language(lang));
            return Results.Ok(GachaMapper.Overview(snapshot));
        }).RequireRateLimiting(RateLimits.Compute).WithMetadata(new RequestSizeLimitAttribute(Limits.MaxRecordsBody));

        guest.MapPost("/charts", (ChartsRequest body, string? lang, IGachaStatisticsService statistics) =>
        {
            if (Validate(body.Uid, body.Pools) is { } error) return error;
            var snapshot = PullsService.Calculate(body.Uid, ToPools(body.Uid, body.Pools), statistics, Language(lang));
            HashSet<CardPoolType>? filter = body.Filter is { Length: > 0 }
                ? body.Filter.Where(v => Enum.IsDefined(typeof(CardPoolType), v)).Select(v => (CardPoolType)v).ToHashSet()
                : null;
            DateTime? since = DateTime.TryParse(body.Since, CultureInfo.InvariantCulture, out DateTime d) ? d : null;
            return Results.Ok(GachaMapper.Chart(PullsChartBuilder.Build(snapshot, new PullsChartFilter(filter, since))));
        }).RequireRateLimiting(RateLimits.Compute).WithMetadata(new RequestSizeLimitAttribute(Limits.MaxRecordsBody));
    }

    private static IResult? Validate(string uid, Dictionary<int, List<RecordDto>>? pools)
    {
        if (!GachaImportService.IsUid(uid)) return AuthEndpoints.Problem("UID 格式不正确。");
        if (pools is null) return AuthEndpoints.Problem("缺少抽卡记录。");
        if (pools.Any(p => !Enum.IsDefined(typeof(CardPoolType), p.Key) || p.Value.Count > Limits.MaxRecordsPerPool))
            return AuthEndpoints.Problem("抽卡记录格式不正确。");
        return null;
    }

    private static string Language(string? lang) => lang is "en" or "ja" ? lang : "zh-Hans";

    /// <summary>浏览器传来的记录还原成 Core 的 <see cref="GachaPull"/>；每条记录就是一抽（Count = 1）。</summary>
    public static IReadOnlyDictionary<CardPoolType, IReadOnlyList<GachaPull>> ToPools(string uid, Dictionary<int, List<RecordDto>> pools)
    {
        var result = new Dictionary<CardPoolType, IReadOnlyList<GachaPull>>();
        foreach (CardPoolType type in Enum.GetValues<CardPoolType>())
        {
            List<RecordDto> list = pools.GetValueOrDefault((int)type) ?? [];
            if (list.Count > Limits.MaxRecordsPerPool) throw new UserFacingException("抽卡记录过多。");
            result[type] = list.Select(r => new GachaPull
            {
                CardPoolType = ((int)type).ToString(CultureInfo.InvariantCulture),
                ResourceId = r.ResourceId,
                Name = Clip(r.Name, 64),
                ResourceType = Clip(r.ResourceType, 32),
                QualityLevel = r.Quality,
                Count = 1,
                Time = Clip(r.Time, 32),
            }).ToList();
        }
        return result;
    }

    private static string Clip(string? text, int max) => text is null ? string.Empty : text.Length <= max ? text : text[..max];
}
