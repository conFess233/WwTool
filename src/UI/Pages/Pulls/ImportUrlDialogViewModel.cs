using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WwTool.Core.Enums;
using WwTool.Core.Infrastructure;
using WwTool.Core.Localization;
using WwTool.UI.Feedback;

namespace WwTool.UI.Pages.Pulls;

/// <summary>
/// 导入抽卡链接对话框：粘贴链接 / 整行日志，或从游戏日志读取；链接不能识别服务器时由用户选择。
/// 确认后交给 <see cref="PullsSession.ImportUrl"/>，结果为是否已导入。
/// </summary>
public sealed partial class ImportUrlDialogViewModel : DialogViewModel<bool>
{
    private readonly PullsSession _session;
    private readonly LocalizationService _localization;

    // 用户自己选的服务器；链接识别出服务器时临时覆盖显示，识别失效后恢复。
    private bool _userChoseChina;

    public ImportUrlDialogViewModel(PullsSession session, LocalizationService localization, GachaServerRegion defaultRegion)
    {
        _session = session;
        _localization = localization;
        _isChina = _userChoseChina = defaultRegion == GachaServerRegion.China;
        _text = session.QueryUrl ?? string.Empty;
        Analyze();
    }

    [ObservableProperty]
    private string _text;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInternational))]
    private bool _isChina;

    public bool IsInternational
    {
        get => !IsChina;
        set => IsChina = !value;
    }

    /// <summary>链接已能识别服务器时为 true，服务器选项锁定。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChooseServer))]
    private bool _isServerDetected;

    public bool CanChooseServer => !IsServerDetected;

    /// <summary>识别结果（账号）或错误提示。</summary>
    [ObservableProperty]
    private string? _status;

    [ObservableProperty]
    private bool _isStatusError;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReadFromLogCommand))]
    private bool _isReading;

    protected override bool CancelResult => false;

    partial void OnTextChanged(string value) => Analyze();

    partial void OnIsChinaChanged(bool value)
    {
        if (!IsServerDetected) _userChoseChina = value;
    }

    public override bool TryAccept()
    {
        Import();
        return true;
    }

    [RelayCommand(CanExecute = nameof(CanRead))]
    private async Task ReadFromLogAsync()
    {
        IsReading = true;
        try
        {
            string? url = await _session.ReadUrlFromGameLogAsync();
            if (url is not null) Text = url;
        }
        finally
        {
            IsReading = false;
        }
    }

    private bool CanRead() => !IsReading;

    [RelayCommand]
    private void Import()
    {
        if (PullsSession.ExtractUrl(Text, out _) is null)
        {
            Status = _localization["Pulls_ImportInvalid"];
            IsStatusError = true;
            return;
        }
        if (_session.ImportUrl(Text, IsServerDetected ? null : IsChina ? GachaServerRegion.China : GachaServerRegion.International))
        {
            Close(true);
        }
    }

    [RelayCommand]
    private void Dismiss() => Cancel();

    private void Analyze()
    {
        string? url = PullsSession.ExtractUrl(Text, out string? playerId);
        if (url is null)
        {
            IsServerDetected = false;
            IsChina = _userChoseChina;
            Status = string.IsNullOrWhiteSpace(Text) ? null : _localization["Pulls_ImportInvalid"];
            IsStatusError = Status is not null;
            return;
        }
        IsServerDetected = GachaServerDetector.TryDetect(url, out GachaServerRegion region);
        IsChina = IsServerDetected ? region == GachaServerRegion.China : _userChoseChina;
        Status = string.Format(_localization["Pulls_ImportAccount"], playerId);
        IsStatusError = false;
    }
}
