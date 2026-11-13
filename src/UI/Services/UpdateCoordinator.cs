using System.Diagnostics;
using System.Reflection;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using WwTool.Core.Abstractions;
using WwTool.Core.Abstractions.Platform;
using WwTool.Core.Enums;
using WwTool.Core.Localization;
using WwTool.Core.Services.Update;
using WwTool.UI.Feedback;

namespace WwTool.UI.Services;

/// <summary>
/// 检查更新的界面协调：启动后延迟在后台自动检查（设置开关，每天最多一次），发现新版本时弹提示、
/// 关于卡显示「有新版本」；手动检查立即请求。能确定自己的发布包类型、且程序目录可写时，可在程序内下载、校验、替换并重启。界面线程使用。
/// </summary>
public sealed partial class UpdateCoordinator : ObservableObject
{
    private readonly UpdateService _updates;
    private readonly IConfigService _config;
    private readonly IUIStateService _uiState;
    private readonly IDialogService _dialogs;
    private readonly IAppLauncher _launcher;
    private readonly LocalizationService _localization;
    private readonly ILoggerService _logger;
    private readonly UpdateInstaller _installer;
    private CancellationTokenSource? _installCts;

    public UpdateCoordinator(UpdateService updates, UpdateInstaller installer, IConfigService config, IUIStateService uiState, IDialogService dialogs,
        IAppLauncher launcher, LocalizationService localization, ILoggerService logger)
    {
        _updates = updates;
        _installer = installer;
        _config = config;
        _uiState = uiState;
        _dialogs = dialogs;
        _launcher = launcher;
        _localization = localization;
        _logger = logger;
        // 先用上次的检查结果显示「有新版本」，不等联网。
        Apply(_updates.LoadCached(CurrentVersion));
    }

    /// <summary>程序版本（如 v2.0.0；开发版带 -dev）。</summary>
    public static string CurrentVersion { get; } = ReadVersion();

    private static string ReadVersion()
    {
#if DEBUG
        // 本地验证更新流程时冒充正式版。
        if (Environment.GetEnvironmentVariable("WWTOOL_FAKE_VERSION") is { Length: > 0 } fake) return fake;
#endif
        return typeof(UpdateCoordinator).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "v2.0.0";
    }

    /// <summary>编译时写入的发布包类型（publish.ps1 传入）；本地编译为空。</summary>
    public static string? BuildPackage { get; } = ReadBuildPackage();

    private static string? ReadBuildPackage()
    {
#if DEBUG
        if (Environment.GetEnvironmentVariable("WWTOOL_FAKE_PACKAGE") is { Length: > 0 } fake) return fake;
#endif
        return typeof(UpdateCoordinator).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "WwToolPackage")?.Value;
    }

    /// <summary>正在下载 / 安装更新。</summary>
    [ObservableProperty] private bool _isInstalling;

    /// <summary>可以在程序内更新到 <paramref name="release"/>：Windows、知道包类型且发布里有带校验值的同类型压缩包、程序目录可写。</summary>
    public ReleaseAsset? InstallableAsset(ReleaseInfo release)
    {
        if (!OperatingSystem.IsWindows()) return null;
        ReleaseAsset? asset = UpdateInstaller.FindAsset(release, _installer.ResolvePackage(BuildPackage));
        return asset is { Sha256: not null } && _installer.CanWriteAppDirectory() ? asset : null;
    }

    /// <summary>启动时清理上次更新留下的旧文件。</summary>
    public void CleanupPreviousUpdate() => _installer.CleanupPreviousUpdate();

    /// <summary>有比当前更新的正式版本（不论是否跳过）。</summary>
    [ObservableProperty] private ReleaseInfo? _available;

    [ObservableProperty] private bool _isChecking;

    public bool HasUpdate => Available is not null;

    public string BadgeText => Available is { } r ? string.Format(_localization["Update_Badge"], r.Version) : string.Empty;

    partial void OnAvailableChanged(ReleaseInfo? value)
    {
        OnPropertyChanged(nameof(HasUpdate));
        OnPropertyChanged(nameof(BadgeText));
    }

    private void Apply(UpdateCheckResult? result) =>
        Available = result is { Status: UpdateCheckStatus.UpdateAvailable, Latest: { } latest } ? latest : null;

    /// <summary>启动完成后调用：延迟 10 秒在后台检查，不打扰启动。</summary>
    public async Task RunAutomaticCheckAsync(CancellationToken token)
    {
        if (!_config.User.AutoCheckUpdates) return;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), token);
            UpdateCheckResult result = await Task.Run(() => _updates.CheckAsync(CurrentVersion, force: false, token), token);
            Apply(result);
            if (Available is { } release && !string.Equals(_config.User.SkippedUpdateVersion, release.Version.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                _uiState.ShowToast(new NotificationRequest
                {
                    Title = _localization["Update_ToastTitle"],
                    Message = string.Format(_localization["Update_ToastMessage"], release.Version),
                    Type = NotificationType.Info,
                    Priority = NotificationPriority.Important,
                    Source = "Update",
                    DedupeKey = "update-available",
                    ActionText = _localization["Update_View"],
                    Action = () => _ = ShowDialogAsync(),
                    Duration = TimeSpan.FromSeconds(12),
                });
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.Warn($"自动检查更新失败：{ex.GetType().Name} {ex.Message}");
        }
    }

    /// <summary>关于卡「检查更新」：立即请求；有新版本时直接打开更新对话框，否则提示结果。</summary>
    public async Task CheckNowAsync()
    {
        if (IsChecking) return;
        IsChecking = true;
        UpdateCheckResult result;
        try
        {
            result = await Task.Run(() => _updates.CheckAsync(CurrentVersion, force: true));
        }
        finally
        {
            IsChecking = false;
        }
        Apply(result);
        switch (result.Status)
        {
            case UpdateCheckStatus.UpdateAvailable:
                await ShowDialogAsync();
                break;
            case UpdateCheckStatus.UpToDate:
                _uiState.ShowToast(_localization["Toast_Success"], string.Format(_localization["Update_UpToDate"], CurrentVersion), NotificationType.Success);
                break;
            case UpdateCheckStatus.DevelopmentBuild:
                _uiState.ShowToast(_localization["Toast_Info"], _localization["Update_DevBuild"]);
                break;
            default:
                _uiState.ShowToast(_localization["Toast_Warning"], _localization["Update_Failed"], NotificationType.Warning);
                break;
        }
    }

    /// <summary>更新对话框：说明、跳过此版本、稍后、前往下载。</summary>
    public async Task ShowDialogAsync()
    {
        if (Available is not { } release) return;
        ReleaseAsset? asset = IsInstalling ? null : InstallableAsset(release);
        var dialog = new UpdateDialogViewModel(release, CurrentVersion, _localization, canInstall: asset is not null);
        UpdateDialogResult choice = await _dialogs.ShowAsync(dialog);
        switch (choice)
        {
            case UpdateDialogResult.Install when asset is not null:
                await InstallAsync(release, asset);
                break;
            case UpdateDialogResult.Skip:
                _config.User.SkippedUpdateVersion = release.Version.ToString();
                break;
            case UpdateDialogResult.OpenPage when Uri.TryCreate(release.PageUrl, UriKind.Absolute, out Uri? page):
                _launcher.OpenUrl(page);
                break;
        }
    }

    /// <summary>下载（进度提示可取消）→ 校验 → 解压 → 替换文件 → 询问是否立即重启。失败时提示原因并提供「前往下载」。</summary>
    private async Task InstallAsync(ReleaseInfo release, ReleaseAsset asset)
    {
        if (IsInstalling) return;
        IsInstalling = true;
        using var cts = new CancellationTokenSource();
        _installCts = cts;
        const string taskKey = "update-install";
        void ShowProgress(double value, string? message = null, bool done = false) => _uiState.ShowToast(new NotificationRequest
        {
            Title = string.Format(_localization["Update_InstallTitle"], release.Version),
            Message = message ?? string.Format(_localization["Update_Downloading"], (int)Math.Round(value * 100)),
            Type = NotificationType.Info,
            Priority = NotificationPriority.Important,
            Source = "Update",
            TaskKey = taskKey,
            Progress = value,
            IsTaskCompleted = done,
            ActionText = done ? null : _localization["Common_Cancel"],
            Action = done ? null : () => _installCts?.Cancel(),
        });
        try
        {
            ShowProgress(0);
            int lastPercent = -1;
            var progress = new Progress<double>(v =>
            {
                int percent = (int)(v * 100);
                if (percent == lastPercent || cts.IsCancellationRequested) return;
                lastPercent = percent;
                ShowProgress(v);
            });
            string zip = await Task.Run(() => _installer.DownloadAsync(asset, progress, cts.Token), cts.Token);
            ShowProgress(1, _localization["Update_Installing"]);
            await Task.Run(() => _installer.Apply(_installer.Extract(zip, release.Version)), cts.Token);
            ShowProgress(1, _localization["Update_Installed"], done: true);
            _logger.Info($"已更新到 {release.Version}，等待重启");
        }
        catch (OperationCanceledException)
        {
            ShowProgress(0, _localization["Update_Canceled"], done: true);
            return;
        }
        catch (UpdateInstallException ex)
        {
            _logger.Warn($"程序内更新失败（{ex.Failure}）：{ex.Message}");
            ShowProgress(0, _localization["Update_InstallFailedShort"], done: true);
            _uiState.ShowToast(new NotificationRequest
            {
                Title = _localization["Update_InstallFailedTitle"],
                Message = _localization["Update_Fail_" + ex.Failure],
                Type = NotificationType.Warning,
                Source = "Update",
                ActionText = _localization["Update_OpenPage"],
                Action = () => { if (Uri.TryCreate(release.PageUrl, UriKind.Absolute, out Uri? page)) _launcher.OpenUrl(page); },
                Duration = TimeSpan.FromSeconds(15),
            });
            return;
        }
        finally
        {
            _installCts = null;
            IsInstalling = false;
        }

        bool restart = await _dialogs.ConfirmAsync(new ConfirmDialogViewModel
        {
            Title = _localization["Update_ReadyTitle"],
            Message = string.Format(_localization["Update_ReadyMessage"], release.Version),
            ConfirmText = _localization["Update_RestartNow"],
            CancelText = _localization["Update_RestartLater"],
        });
        if (restart) Restart();
    }

    /// <summary>启动新版本（它会等本进程退出后再继续），然后正常退出（保存设置等照常执行）。</summary>
    private void Restart()
    {
        string? exe = Environment.ProcessPath;
        if (exe is null) return;
        try
        {
            var start = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe)! };
            start.ArgumentList.Add(WaitPidArgument);
            start.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Process.Start(start);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            _logger.Error("更新后重启失败", ex);
            _uiState.ShowToast(_localization["Toast_Warning"], _localization["Update_RestartFailed"], NotificationType.Warning);
            return;
        }
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }

    /// <summary>重启参数：新进程等这个进程号退出后再启动，避免两个实例同时读写设置与数据库。</summary>
    public const string WaitPidArgument = "--wait-pid";
}
