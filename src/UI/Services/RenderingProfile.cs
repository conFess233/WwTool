using System.Text.Json;
using Avalonia;
using WwTool.Core.Infrastructure;

namespace WwTool.UI.Services;

/// <summary>
/// Windows 渲染方式：启动时按用户的“窗口毛玻璃”设置选择一次，运行中不再改变。
/// </summary>
/// <remarks>
/// 毛玻璃（Mica / 亚克力）需要 GPU 合成；软件渲染下系统材质不可用。
/// GPU 渲染要加载显卡用户态驱动，私有内存多约 100 MB（RTX 4060 实测：启动 180 MB 对 78 MB），
/// 而两种方式浏览全部页面的 CPU 时间相当。所以关闭毛玻璃时改用软件渲染。
/// </remarks>
public static class RenderingProfile
{
    /// <summary>本次进程能否显示毛玻璃（Windows 上即是否用了 GPU 渲染）。非 Windows 由平台自行处理，恒为 true。</summary>
    public static bool GlassCapable { get; private set; } = true;

    /// <summary>在 <see cref="Program.Main"/> 里调用；会读一次用户配置，所以不放进设计器也会用的 BuildAvaloniaApp。</summary>
    public static AppBuilder UseGlassAwareRendering(this AppBuilder builder)
    {
        if (!OperatingSystem.IsWindows()) return builder;
        GlassCapable = ReadGlassPreference();
        return builder.With(new Win32PlatformOptions
        {
            RenderingMode = GlassCapable
                ? [Win32RenderingMode.AngleEgl, Win32RenderingMode.Software]
                : [Win32RenderingMode.Software],
        });
    }

    /// <summary>只读毛玻璃开关；文件缺失或读不出时按默认值（开启）处理，正式的配置加载与报错仍由组合根负责。</summary>
    private static bool ReadGlassPreference()
    {
        try
        {
            string file = Path.Combine(new AppPaths().ConfigDirectory, "UserConfig.json");
            if (!File.Exists(file)) return true;
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file),
                new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            return !(document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("IsGlassEffectEnabled", out JsonElement value)
                && value.ValueKind == JsonValueKind.False);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return true;
        }
    }
}
