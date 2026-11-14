using System.Collections.Concurrent;
using WwTool.Core.Abstractions;
using WwTool.Core.Services;
using WwTool.Core.Services.Notices;

namespace WwTool.Web.Public;

/// <summary>
/// 公开公告：所有访客共用后端缓存，同一区服与语言在 <see cref="RefreshInterval"/> 内只向库洛请求一次。
/// 刷新失败时继续返回上一次的结果（与桌面端“失败保留上一次完整数据”一致）。
/// </summary>
public sealed class PublicNoticeService(GameNoticeService notices, ILoggerService logger)
{
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(10);

    private sealed class Slot
    {
        public readonly SemaphoreSlim Gate = new(1, 1);
        public NoticeFeedDto? Feed;
        public DateTimeOffset CheckedAt = DateTimeOffset.MinValue;
    }

    private readonly ConcurrentDictionary<(LauncherRegion, string), Slot> _slots = new();

    public async Task<NoticeFeedDto?> GetAsync(LauncherRegion region, string uiLanguage, CancellationToken token)
    {
        string language = region == LauncherRegion.China ? "zh-Hans" : GameNoticeService.RequestLanguage(uiLanguage);
        Slot slot = _slots.GetOrAdd((region, language), _ => new Slot());
        if (slot.Feed is not null && DateTimeOffset.UtcNow - slot.CheckedAt < RefreshInterval) return slot.Feed;

        await slot.Gate.WaitAsync(token);
        try
        {
            if (slot.Feed is not null && DateTimeOffset.UtcNow - slot.CheckedAt < RefreshInterval) return slot.Feed;
            // 首次请求先用磁盘缓存兜底，再尝试刷新
            if (slot.Feed is null && notices.LoadCached(region, language) is { } cached) slot.Feed = NoticeMapper.Map(cached);
            try
            {
                // 刷新不随单个访客取消，避免请求中断导致缓存半途而废
                if (await notices.RefreshAsync(region, language, CancellationToken.None) is { } fresh) slot.Feed = NoticeMapper.Map(fresh);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                logger.Warn($"公开公告刷新失败（{region}/{language}）：{ex.GetType().Name}");
            }
            slot.CheckedAt = DateTimeOffset.UtcNow;
            return slot.Feed;
        }
        finally
        {
            slot.Gate.Release();
        }
    }
}
