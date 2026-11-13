using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using WwTool.Core.Localization;
using WwTool.Core.Services.Update;

namespace WwTool.UI.Feedback;

public enum UpdateDialogResult
{
    Later,
    Skip,
    OpenPage,
    Install,
}

/// <summary>发现新版本：版本对比、发布日期、更新说明（可滚动）与「跳过此版本 / 稍后 / 前往下载」；能在程序内更新时主按钮为「下载并更新」。</summary>
public sealed partial class UpdateDialogViewModel(ReleaseInfo release, string currentVersion, LocalizationService localization, bool canInstall = false)
    : DialogViewModel<UpdateDialogResult>
{
    public string Title => string.Format(localization["Update_DialogTitle"], release.Version);

    public string VersionLine => string.Format(localization["Update_VersionLine"], currentVersion, release.Version);

    public string PublishedLine => release.PublishedAt is { } at
        ? string.Format(localization["Update_Published"], at.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
        : string.Empty;

    public bool HasPublished => release.PublishedAt is not null;

    public string Notes => ReleaseNotes.ToPlainText(release.Notes) is { Length: > 0 } text ? text : localization["Update_NoNotes"];

    protected override UpdateDialogResult CancelResult => UpdateDialogResult.Later;

    /// <summary>能在程序内下载并替换（知道包类型、有校验值、程序目录可写）。</summary>
    public bool CanInstall => canInstall;

    public override bool TryAccept()
    {
        Close(canInstall ? UpdateDialogResult.Install : UpdateDialogResult.OpenPage);
        return true;
    }

    [RelayCommand]
    private void Install() => Close(UpdateDialogResult.Install);

    [RelayCommand]
    private void OpenPage() => Close(UpdateDialogResult.OpenPage);

    [RelayCommand]
    private void Skip() => Close(UpdateDialogResult.Skip);

    [RelayCommand]
    private void Later() => Close(UpdateDialogResult.Later);
}
