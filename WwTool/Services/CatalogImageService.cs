using System.IO;
using System.Net.Http;
using System.Windows.Media.Imaging;
using WwTool.Core.Abstractions;
using WwTool.Core.Abstractions.Platform;
using WwTool.Core.Services;

namespace WwTool.Services;

/// <summary>
/// 迁移期 WPF 适配：缓存逻辑在 Core 的 <see cref="CatalogImageService{TImage}"/>，此处只提供 WPF 位图解码。
/// 保留原类型名，旧界面中的引用无需修改。
/// </summary>
public sealed class CatalogImageService(
    IHttpClientFactory factory,
    ILoggerService logger,
    CatalogRepositorySource repository,
    IAppPaths paths)
    : CatalogImageService<BitmapSource>(factory, logger, repository, new WpfImageDecoder(), paths);

/// <summary>
/// WPF 位图解码。返回的位图已 Freeze，可跨线程使用。
/// </summary>
public sealed class WpfImageDecoder : IImageDecoder<BitmapSource>
{
    private const string PackPrefix = "pack://application:,,,/";

    public string DefaultImagePath => PackPrefix + "UI/Resources/Images/Default.png";

    public BitmapSource Decode(byte[] bytes, int width)
    {
        using var stream = new MemoryStream(bytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = Math.Clamp(width, 16, 2048);
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    public BitmapSource? DecodeBundled(string path, int width)
    {
        string uri = path.StartsWith("pack://", StringComparison.OrdinalIgnoreCase)
            ? path
            : PackPrefix + path.Replace('\\', '/');
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = Math.Clamp(width, 16, 2048);
            image.UriSource = new Uri(uri, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UriFormatException)
        {
            // 打包资源不存在或不是图片：按接口约定返回 null，由调用方回退。
            return null;
        }
    }
}
