using System.IO;
using WwTool.Core.Enums;

namespace WwTool.Core.Models.Config
{
    public class AppConfig
    {

        public string GameLogPath { get; set; } = @"Client\Saved\Logs\";
        public string GameLogFile { get; set; } = "Client.log";
        public string CatalogDirectory { get; set; } = Path.Combine("Local", "Data");

        public string GameLauncherFile { get; set; } = "Wuthering Waves.exe";
        public string AppVersion { get; set; } = "1.2.0";
        public string AppAuther { get; set; } = "告白";

        // 日志系统配置
        public string LogFolderPath { get; set; } = Path.Combine("Local", "Logs");
        public int LogRetentionDays { get; set; } = 7;
        public bool EnableFileLogging { get; set; } = true;
        public long LogMaxSizeBytes { get; set; } = 10 * 1024 * 1024; // 默认限制为 10 MB。
        public int LogMaxFileCount { get; set; } = 5;

        // 低于此级别的日志不写入文件；默认全部记录（与 1.x 一致）。
        public LogLevel MinimumLogLevel { get; set; } = LogLevel.Debug;

        // 极验本地验证服务配置。旧配置里保存的 5000 仍可用：被占用时 GeetestServer 会顺延端口。
        public int GeetestPort { get; set; } = AppConfigDefaults.GeetestPort;
    }
}
