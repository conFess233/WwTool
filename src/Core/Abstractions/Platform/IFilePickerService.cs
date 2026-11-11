namespace WwTool.Core.Abstractions.Platform;

/// <summary>
/// 文件与文件夹选择。实现由界面层提供（Avalonia StorageProvider）。
/// </summary>
public interface IFilePickerService
{
    /// <summary>选择一个文件夹；用户取消时返回 <c>null</c>。</summary>
    Task<string?> PickFolderAsync(string title, string? initialDirectory = null, CancellationToken cancellationToken = default);

    /// <summary>选择一个文件；用户取消时返回 <c>null</c>。<paramref name="patterns"/> 形如 <c>*.log</c>。</summary>
    Task<string?> PickFileAsync(string title, IReadOnlyList<string> patterns, string? initialDirectory = null, CancellationToken cancellationToken = default);
}
