namespace WwTool.Core.Abstractions;

/// <summary>
/// 把图片字节或随程序发布的图片资源解码为界面框架的位图类型（WPF BitmapSource、Avalonia Bitmap）。
/// 实现必须是线程安全的，返回的位图可跨线程使用（WPF 需 Freeze）。
/// </summary>
public interface IImageDecoder<TImage> where TImage : class
{
    /// <summary>缺图时使用的默认图片资源路径（框架资源地址或程序目录下的相对路径）。</summary>
    string DefaultImagePath { get; }

    /// <summary>解码图片字节，宽度按 <paramref name="width"/> 缩放；不是有效图片时抛出异常。</summary>
    TImage Decode(byte[] bytes, int width);

    /// <summary>
    /// 解码随程序打包的图片资源（如 <c>pack://</c>、<c>avares://</c> 或相对资源路径）。
    /// 资源不存在时返回 <c>null</c>，不抛出。
    /// </summary>
    TImage? DecodeBundled(string path, int width);
}
