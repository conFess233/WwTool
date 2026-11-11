namespace WwTool.Core.Models.Config;

/// <summary>
/// 应用配置的默认值，供配置模型与回退逻辑共用。
/// </summary>
public static class AppConfigDefaults
{
    /// <summary>
    /// 极验本地验证服务的默认端口。不用 5000：macOS 12 起被 AirPlay 接收器占用。
    /// </summary>
    public const int GeetestPort = 51730;
}
