using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;

namespace WwTool.UI.Services;

/// <summary>把文字写入系统剪贴板（经由主窗口）。</summary>
public sealed class ClipboardService
{
    public async Task SetTextAsync(string text)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } window }
            || TopLevel.GetTopLevel(window)?.Clipboard is not { } clipboard)
        {
            throw new InvalidOperationException("Clipboard is not available.");
        }
        await clipboard.SetTextAsync(text);
    }
}
