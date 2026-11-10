using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using WwTool.Core.Abstractions;
using WwTool.Core.Exceptions;
using WwTool.Core.Localization;

namespace WwTool.UI.Services;

/// <summary>首页「启动游戏」按钮的三种状态。</summary>
public enum GameRunState
{
    Idle,
    Launching,
    Running,
}

/// <summary>
/// 启动游戏并跟踪游戏进程：启动后显示「正在启动…」，检测到游戏窗口即切到「游戏运行中」，进程退出后还原。
/// 只在 Windows 上可用（游戏没有 macOS 客户端）。
/// </summary>
public sealed partial class GameLauncherService : ObservableObject
{
    // 游戏本体与启动壳的进程名。
    private static readonly string[] ProcessNames = ["Client-Win64-Shipping", "Wuthering Waves"];
    private static readonly TimeSpan LaunchTimeout = TimeSpan.FromSeconds(90);

    private readonly IConfigService _config;
    private readonly LocalizationService _localization;
    private readonly DispatcherTimer _poll;
    private DateTimeOffset _launchedAt;

    public GameLauncherService(IConfigService config, LocalizationService localization)
    {
        _config = config;
        _localization = localization;
        _poll = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _poll.Tick += (_, _) => Poll();
    }

    public bool IsSupported => OperatingSystem.IsWindows();

    [ObservableProperty]
    private GameRunState _state;

    /// <summary>首页打开时检查一次游戏是否已经在运行。</summary>
    public void Refresh()
    {
        if (!IsSupported) return;
        if (State == GameRunState.Idle && HasGameWindow())
        {
            State = GameRunState.Running;
            _poll.Start();
        }
    }

    /// <summary>启动游戏；路径未设置或程序不存在时抛出 <see cref="WwToolGamePathException"/>。</summary>
    public void Launch()
    {
        if (!IsSupported) throw new PlatformNotSupportedException(_localization["Home_LaunchUnsupported"]);
        if (State != GameRunState.Idle) return;
        string? root = _config.User.GamePath;
        if (string.IsNullOrWhiteSpace(root)) throw new WwToolGamePathException(_localization["Msg_NoGamePath"]);
        string exe = Path.Combine(root, _config.App.GameLauncherFile);
        if (!File.Exists(exe)) throw new WwToolGamePathException(string.Format(_localization["Home_GameExeMissing"], exe));

        using (Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = root })) { }
        _launchedAt = DateTimeOffset.Now;
        State = GameRunState.Launching;
        _poll.Start();
    }

    private void Poll()
    {
        bool window = HasGameWindow();
        switch (State)
        {
            case GameRunState.Launching when window:
                State = GameRunState.Running;
                break;
            case GameRunState.Launching when DateTimeOffset.Now - _launchedAt > LaunchTimeout:
            case GameRunState.Running when !window:
                State = GameRunState.Idle;
                break;
        }
        if (State == GameRunState.Idle) _poll.Stop();
    }

    private static bool HasGameWindow()
    {
        foreach (string name in ProcessNames)
        {
            Process[] processes = Process.GetProcessesByName(name);
            try
            {
                if (processes.Any(p => SafeHasWindow(p))) return true;
            }
            finally
            {
                foreach (Process p in processes) p.Dispose();
            }
        }
        return false;
    }

    private static bool SafeHasWindow(Process process)
    {
        try
        {
            return process.MainWindowHandle != IntPtr.Zero;
        }
        catch (InvalidOperationException)
        {
            return false; // 进程已退出。
        }
    }
}
