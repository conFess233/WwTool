using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using WwTool.Core.Abstractions;
using WwTool.Core.Enums;
using WwTool.Core.Localization;
using WwTool.UI.Feedback;

namespace WwTool.UI.Services;

/// <summary>选择游戏安装目录（设置页与抽卡统计空状态共用）：校验目录里有启动器文件后写入用户配置。</summary>
public sealed class GamePathPicker(IConfigService config, LocalizationService localization, IUIStateService uiState)
{
    /// <summary>当前配置的目录里能找到启动器文件。</summary>
    public bool HasValidPath =>
        !string.IsNullOrWhiteSpace(config.User.GamePath) && File.Exists(Path.Combine(config.User.GamePath, config.App.GameLauncherFile));

    /// <summary>打开文件夹选择框；选中有效目录并保存后返回 true。</summary>
    public async Task<bool> PickAsync()
    {
        IStorageProvider? storage = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow?.StorageProvider;
        if (storage is null) return false;
        IStorageFolder? start = string.IsNullOrWhiteSpace(config.User.GamePath) ? null : await storage.TryGetFolderFromPathAsync(config.User.GamePath);
        IReadOnlyList<IStorageFolder> picked = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = localization["SelectFolder_GamePathTitle"],
            AllowMultiple = false,
            SuggestedStartLocation = start,
        });
        string? folder = picked.FirstOrDefault()?.TryGetLocalPath();
        if (folder is null) return false;
        if (!File.Exists(Path.Combine(folder, config.App.GameLauncherFile)))
        {
            uiState.ShowToast(localization["Toast_Warning"],
                string.Format(localization["Toast_GameLauncherNotFound"], config.App.GameLauncherFile), NotificationType.Warning);
            return false;
        }
        config.User.GamePath = folder;
        uiState.ShowToast(localization["Toast_Success"], localization["Toast_GamePathUpdated"], NotificationType.Success);
        return true;
    }
}
