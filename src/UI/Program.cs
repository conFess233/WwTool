using Avalonia;
using WwTool.UI.Services;

namespace WwTool.UI;

/// <summary>
/// 程序入口：只负责启动 Avalonia，组合根在 <see cref="App"/>。
/// </summary>
internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        WaitForPreviousInstance(args);
        BuildAvaloniaApp().UseConfiguredRendering().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>程序内更新后的重启：等旧进程退出（最多 20 秒）再启动，避免两个实例同时读写设置与数据库。</summary>
    private static void WaitForPreviousInstance(string[] args)
    {
        int index = Array.IndexOf(args, UpdateCoordinator.WaitPidArgument);
        if (index < 0 || index + 1 >= args.Length || !int.TryParse(args[index + 1], out int pid) || pid == Environment.ProcessId) return;
        try
        {
            using var previous = System.Diagnostics.Process.GetProcessById(pid);
            previous.WaitForExit(TimeSpan.FromSeconds(20));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // 旧进程已经退出。
        }
    }

    /// <summary>
    /// Avalonia 设计器也会调用此方法，不得在这里做 I/O 或服务初始化。
    /// </summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
