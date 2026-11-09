namespace WwTool.Core.Abstractions.Platform;

/// <summary>
/// 用系统默认程序打开网址或文件夹。
/// </summary>
public interface IAppLauncher
{
    /// <summary>用默认浏览器打开网址。只接受 http/https。</summary>
    void OpenUrl(Uri url);

    /// <summary>用系统文件管理器打开文件夹；不存在时先创建。</summary>
    void OpenFolder(string path);
}
