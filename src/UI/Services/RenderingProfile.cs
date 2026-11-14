using System.Text.Json;
using Avalonia;
using WwTool.Core.Infrastructure;

namespace WwTool.UI.Services;

/// <summary>
/// Windows 渲染方式：启动时按「动态背景」设置选择一次，运行中不再改变。
/// </summary>
/// <remarks>
/// GPU 渲染要把显卡用户态驱动加载进进程，私有内存多约 200 MB（RTX 4060 实测：启动 340 MB 对 121 MB）；
/// 空闲时两者的 CPU 相当（约 4–6%）。但播放动态背景时每一帧都整窗重绘，软件渲染约占一个核心的 110%，GPU 约 63%。
/// 所以只在开启动态背景（且没有选自定义图片）时用 GPU 渲染，其余情况用软件渲染。
/// </remarks>
public static class RenderingProfile
{
    /// <summary>本次进程是否用了 GPU 渲染。非 Windows 由平台自行选择，恒为 true。</summary>
    public static bool UsesGpu { get; private set; } = true;

    /// <summary>按这组设置，应当使用 GPU 渲染（播放动态背景）。</summary>
    public static bool WantsGpu(bool useDynamicBackground, bool useCustomBackground) => useDynamicBackground && !useCustomBackground;

    /// <summary>在 <see cref="Program.Main"/> 里调用；会读一次用户配置，所以不放进设计器也会用的 BuildAvaloniaApp。</summary>
    public static AppBuilder UseConfiguredRendering(this AppBuilder builder)
    {
        if (!OperatingSystem.IsWindows()) return builder;
        UsesGpu = ReadPreference();
        return builder.With(new Win32PlatformOptions
        {
            RenderingMode = UsesGpu
                ? [Win32RenderingMode.AngleEgl, Win32RenderingMode.Software]
                : [Win32RenderingMode.Software],
        });
    }

    /// <summary>只读两个背景开关；文件缺失或读不出时按默认值（关闭动态背景）处理，正式的配置加载与报错仍由组合根负责。</summary>
    private static bool ReadPreference()
    {
        try
        {
            string file = Path.Combine(new AppPaths().ConfigDirectory, "UserConfig.json");
            if (!File.Exists(file)) return false;
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file),
                new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;
            return WantsGpu(IsTrue(root, "UseDynamicBackground"), IsTrue(root, "UseCustomBackground"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return false;
        }
    }

    private static bool IsTrue(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.True;
}
