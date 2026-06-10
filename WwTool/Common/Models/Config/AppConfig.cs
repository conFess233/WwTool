using System.IO;

namespace WwTool.Common.Models.Config
{
    public class AppConfig
    {

        public string GameLogPath { get; set; } = @"Client\Saved\Logs\";
        public string GameLogFile { get; set; } = "Client.log";
        public string GameItemsResourcesPath { get; set; } = Path.Combine("Local/Data", "GameItemsResources.json");

        public string GameLauncherFile { get; set; } = "Wuthering Waves.exe";
        public string AppVersion { get; set; } = "1.0.0";
        public string AppAuther { get; set; } = "告白";

        // 日志系统配置
        public string LogFolderPath { get; set; } = Path.Combine("Local", "Logs");
        public int LogRetentionDays { get; set; } = 7;
        public bool EnableFileLogging { get; set; } = true;
        public long LogMaxSizeBytes { get; set; } = 10 * 1024 * 1024; // 10MB
        public int LogMaxFileCount { get; set; } = 5;

        // 极验本地验证服务配置
        public int GeetestPort { get; set; } = 5000;
    }
}
