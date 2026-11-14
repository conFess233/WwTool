using Avalonia.Media.Imaging;
using WwTool.Core.Services;
using WwTool.Core.Services.Challenges;
using WwTool.Core.Services.Notices;

namespace WwTool.UI.Services;

/// <summary>游戏内公告的图片（横幅、日历图、正文配图）。</summary>
public sealed class NoticeImages(GameNoticeService notices) : CachedImages(notices.Images, 96, 32L * 1024 * 1024);

/// <summary>encore.moe 的图片（敌人、信物、角色头像与立绘）：图标数量多但都很小，立绘每张约 3 MB。</summary>
public sealed class EncoreImages(EncoreService encore) : CachedImages(encore.Images, 256, 32L * 1024 * 1024);

/// <summary>
/// 网络图片的界面侧加载：先给出本地缓存（立即显示），再交给 <see cref="RemoteImageCache"/> 向服务器校验，
/// 图片有变化或原来没有缓存时再解码一次。解码结果按「文件 + 宽度 + 修改时间」复用，
/// 最多 <paramref name="maxEntries"/> 张、像素合计不超过 <paramref name="maxBytes"/>，超出时丢弃最久没用的。
/// </summary>
public abstract class CachedImages(RemoteImageCache cache, int maxEntries, long maxBytes)
{
    private readonly Dictionary<string, (Bitmap Bitmap, DateTime Stamp, long Used)> _decoded = new(StringComparer.Ordinal);
    private long _bytes;
    private readonly Lock _lock = new();
    private long _clock;

    /// <summary>
    /// 加载图片：有缓存时先回调 <paramref name="onImage"/> 一次，校验后图片有变化（或原来没有）再回调一次。
    /// <paramref name="force"/> 为 true 时总是向服务器校验（版本日历图每次刷新后都校验）。
    /// </summary>
    public async Task LoadAsync(string? url, int decodeWidth, Action<Bitmap> onImage, bool force = false, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        string? cached = cache.Cached(url);
        Bitmap? shown = cached is null ? null : await Task.Run(() => Decode(cached, decodeWidth), token).ConfigureAwait(true);
        if (shown is not null) onImage(shown);

        (string? path, bool changed) = await cache.GetAsync(url, force, token).ConfigureAwait(true);
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
            if (_decoded.Remove(key, out var replaced)) _bytes -= Bytes(replaced.Bitmap);
            _decoded[key] = (bitmap, stamp, ++_clock);
            _bytes += Bytes(bitmap);
            // 只丢弃引用，不主动 Dispose：界面可能仍在显示，交给 GC。刚解码的这张总是保留。
            while (_decoded.Count > 1 && (_decoded.Count > maxEntries || _bytes > maxBytes))
            {
                string oldest = _decoded.MinBy(kv => kv.Value.Used).Key;
                if (_decoded.Remove(oldest, out var dropped)) _bytes -= Bytes(dropped.Bitmap);
            }
        }
        return bitmap;
    }

    /// <summary>位图像素占用（BGRA 每像素 4 字节）。</summary>
    private static long Bytes(Bitmap bitmap) => (long)bitmap.PixelSize.Width * bitmap.PixelSize.Height * 4;
}
