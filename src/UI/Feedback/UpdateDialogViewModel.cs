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
}

/// <summary>发现新版本：版本对比、发布日期、更新说明（原文，可滚动）与「跳过此版本 / 稍后 / 前往下载」。</summary>
public sealed partial class UpdateDialogViewModel(ReleaseInfo release, string currentVersion, LocalizationService localization)
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

    public override bool TryAccept()
    {
        Close(UpdateDialogResult.OpenPage);
        return true;
    }

    [RelayCommand]
    private void OpenPage() => Close(UpdateDialogResult.OpenPage);

    [RelayCommand]
    private void Skip() => Close(UpdateDialogResult.Skip);

    [RelayCommand]
    private void Later() => Close(UpdateDialogResult.Later);
}
