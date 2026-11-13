using Avalonia.Media.Imaging;
using WwTool.Core.Services.Notices;

namespace WwTool.UI.Services;

/// <summary>
/// 游戏内公告图片的界面侧加载：先给出本地缓存（立即显示），再交给 <see cref="GameNoticeService"/> 向服务器校验，
/// 图片有变化或原来没有缓存时再解码一次。解码结果按「文件 + 宽度 + 修改时间」复用。
/// </summary>
public sealed class NoticeImages(GameNoticeService notices)
{
    private const int MaxEntries = 96;
    private readonly Dictionary<string, (Bitmap Bitmap, DateTime Stamp, long Used)> _decoded = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();
    private long _clock;

    /// <summary>
    /// 加载图片：有缓存时先回调 <paramref name="onImage"/> 一次，校验后图片有变化（或原来没有）再回调一次。
    /// <paramref name="force"/> 为 true 时总是向服务器校验（版本日历图每次刷新后都校验）。
    /// </summary>
    public async Task LoadAsync(string? url, int decodeWidth, Action<Bitmap> onImage, bool force = false, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        string? cached = notices.CachedImage(url);
        Bitmap? shown = cached is null ? null : await Task.Run(() => Decode(cached, decodeWidth), token).ConfigureAwait(true);
        if (shown is not null) onImage(shown);

        (string? path, bool changed) = await notices.GetImageAsync(url, force, token).ConfigureAwait(true);
        if (path is null || (!changed && shown is not null)) return;
        Bitmap? fresh = await Task.Run(() => Decode(path, decodeWidth), token).ConfigureAwait(true);
        if (fresh is not null && !ReferenceEquals(fresh, shown)) onImage(fresh);
    }

    private Bitmap? Decode(string path, int width)
    {
        DateTime stamp;
        try
        {
            stamp = File.GetLastWriteTimeUtc(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
        string key = path + "|" + width;
        lock (_lock)
        {
            if (_decoded.TryGetValue(key, out var hit) && hit.Stamp == stamp)
            {
                _decoded[key] = hit with { Used = ++_clock };
                return hit.Bitmap;
            }
        }
        Bitmap bitmap;
        try
        {
            using FileStream stream = File.OpenRead(path);
            bitmap = Bitmap.DecodeToWidth(stream, width, BitmapInterpolationMode.HighQuality);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
        catch (Exception)
        {
            // 图片损坏时 Skia 可能抛出其他异常；当作没有图。
            return null;
        }
        lock (_lock)
        {
            _decoded[key] = (bitmap, stamp, ++_clock);
            // 只丢弃引用，不主动 Dispose：界面可能仍在显示，交给 GC。
            while (_decoded.Count > MaxEntries)
                _decoded.Remove(_decoded.MinBy(kv => kv.Value.Used).Key);
        }
        return bitmap;
    }
}
