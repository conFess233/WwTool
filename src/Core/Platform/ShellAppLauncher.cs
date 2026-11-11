using System.Diagnostics;
using WwTool.Core.Abstractions.Platform;

namespace WwTool.Core.Platform;

/// <summary>
/// 通过系统外壳打开网址与文件夹：Windows 用 ShellExecute，macOS 用 <c>open</c>，Linux 用 <c>xdg-open</c>。
/// </summary>
public sealed class ShellAppLauncher : IAppLauncher
{
    public void OpenUrl(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("只允许打开 http/https 网址。", nameof(url));
        }

        Launch(url.AbsoluteUri);
    }

    public void OpenFolder(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(fullPath);
        Launch(fullPath);
    }

    private static void Launch(string target)
    {
        ProcessStartInfo info;
        if (OperatingSystem.IsWindows())
        {
            info = new ProcessStartInfo(target) { UseShellExecute = true };
        }
        else
        {
            info = new ProcessStartInfo(OperatingSystem.IsMacOS() ? "open" : "xdg-open") { UseShellExecute = false };
            // 作为独立参数传递，避免路径或网址中的空格、引号被解释。
            info.ArgumentList.Add(target);
        }

        using Process? _ = Process.Start(info);
    }
}
