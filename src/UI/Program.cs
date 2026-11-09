using Avalonia;
using WwTool.UI.Services;

namespace WwTool.UI;

/// <summary>
/// 程序入口：只负责启动 Avalonia，组合根在 <see cref="App"/>。
/// </summary>
internal static class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().UseGlassAwareRendering().StartWithClassicDesktopLifetime(args);

    /// <summary>
    /// Avalonia 设计器也会调用此方法，不得在这里做 I/O 或服务初始化。
    /// </summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
