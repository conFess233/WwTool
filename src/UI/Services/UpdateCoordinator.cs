using System.Reflection;
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
/// 关于卡显示「有新版本」；手动检查立即请求。界面线程使用。
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

    public UpdateCoordinator(UpdateService updates, IConfigService config, IUIStateService uiState, IDialogService dialogs,
        IAppLauncher launcher, LocalizationService localization, ILoggerService logger)
    {
        _updates = updates;
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
        var dialog = new UpdateDialogViewModel(release, CurrentVersion, _localization);
        UpdateDialogResult choice = await _dialogs.ShowAsync(dialog);
        switch (choice)
        {
            case UpdateDialogResult.Skip:
                _config.User.SkippedUpdateVersion = release.Version.ToString();
                break;
            case UpdateDialogResult.OpenPage when Uri.TryCreate(release.PageUrl, UriKind.Absolute, out Uri? page):
                _launcher.OpenUrl(page);
                break;
        }
    }
}
