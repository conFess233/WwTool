using System.Net.Http;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using WwTool.Core.Abstractions;
using WwTool.Core.Abstractions.Platform;
using WwTool.Core.Services;

namespace WwTool.UI.Services;

/// <summary>
/// 资料图片缓存的 Avalonia 版本：缓存逻辑在 Core，这里只提供位图解码。
/// </summary>
public sealed class AvaloniaCatalogImageService(
    IHttpClientFactory factory,
    ILoggerService logger,
    CatalogRepositorySource repository,
    IAppPaths paths)
    : CatalogImageService<Bitmap>(factory, logger, repository, new AvaloniaImageDecoder(), paths);

/// <summary>
/// Avalonia 位图解码。Avalonia 的 <see cref="Bitmap"/> 创建后不可变，可跨线程使用。
/// </summary>
public sealed class AvaloniaImageDecoder : IImageDecoder<Bitmap>
{
    private const string AssetPrefix = "avares://WwTool/";

    public string DefaultImagePath => AssetPrefix + "Assets/Images/Default.png";

    public Bitmap Decode(byte[] bytes, int width)
    {
        using var stream = new MemoryStream(bytes);
        return Bitmap.DecodeToWidth(stream, Math.Clamp(width, 16, 2048));
    }

    public Bitmap? DecodeBundled(string path, int width)
    {
        string uri = path.StartsWith("avares://", StringComparison.OrdinalIgnoreCase)
            ? path
            : AssetPrefix + path.Replace('\\', '/').TrimStart('/');
        try
        {
            using Stream stream = AssetLoader.Open(new Uri(uri));
            return Bitmap.DecodeToWidth(stream, Math.Clamp(width, 16, 2048));
        }
        catch (Exception ex) when (ex is IOException or UriFormatException or ArgumentException or InvalidOperationException)
        {
            // 资源不存在或不是图片：按接口约定返回 null，由调用方回退。
            return null;
        }
    }
}
