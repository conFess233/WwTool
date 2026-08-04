using System.Diagnostics;
using System.IO;
using WwTool.Common.Enums;
using WwTool.Common.Exceptions;
using WwTool.Common.Models;
using WwTool.Services.Interfaces;

namespace WwTool.Common.Utils
{
    public static class ExceptionHelper
    {
        private static ILoggerService? _logger;
        private static IUIStateService? _uiStateService;
        private static IConfigService? _configService;

        public static void Initialize(
            ILoggerService logger,
            IUIStateService uiStateService,
            IConfigService? configService = null)
        {
            _logger = logger;
            _uiStateService = uiStateService;
            _configService = configService;
        }

        public static void Execute(Action action, string? contextMessage = null, Action<Exception>? onError = null)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                HandleException(ex, contextMessage);
                onError?.Invoke(ex);
            }
        }

        public static async Task ExecuteAsync(
            Func<Task> action,
            string? contextMessage = null,
            Action<Exception>? onError = null,
            bool notifyUser = true)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                HandleException(ex, contextMessage, notifyUser);
                onError?.Invoke(ex);
            }
        }

        public static T? Execute<T>(Func<T> action, string? contextMessage = null, Action<Exception>? onError = null)
        {
            try
            {
                return action();
            }
            catch (Exception ex)
            {
                HandleException(ex, contextMessage);
                onError?.Invoke(ex);
                return default;
            }
        }

        public static async Task<T?> ExecuteAsync<T>(Func<Task<T>> action, string? contextMessage = null, Action<Exception>? onError = null)
        {
            try
            {
                return await action();
            }
            catch (Exception ex)
            {
                HandleException(ex, contextMessage);
                onError?.Invoke(ex);
                return default;
            }
        }

        public static void HandleException(Exception ex, string? contextMessage = null, bool notifyUser = true)
        {
            string errorId = $"{DateTime.UtcNow:yyMMddHHmmss}-{Guid.NewGuid():N}"[..20];
            string logMessage = string.IsNullOrEmpty(contextMessage)
                ? LanguageManager.Instance["Exc_UnhandledSys"]
                : string.Format(LanguageManager.Instance["Exc_OperationFailed"], contextMessage);

            string title;
            string message;
            NotificationType type;

            switch (ex)
            {
                case WwToolAuthException authException:
                    _logger?.Warn($"[{errorId}] {logMessage} - 登录认证失败: {authException.Message}");
                    title = LanguageManager.Instance["Exc_LoginFailedTitle"];
                    message = authException.Message;
                    type = NotificationType.Warning;
                    break;
                case WwToolGamePathException pathException:
                    _logger?.Warn($"[{errorId}] {logMessage} - 游戏路径错误: {pathException.Message}");
                    title = LanguageManager.Instance["Exc_PathErrorTitle"];
                    message = pathException.Message;
                    type = NotificationType.Warning;
                    break;
                case WwToolApiException apiException:
                    _logger?.Error($"[{errorId}] {logMessage} - 网络接口异常", apiException);
                    title = LanguageManager.Instance["Exc_NetworkErrorTitle"];
                    message = logMessage;
                    type = NotificationType.Error;
                    break;
                case WwToolDatabaseException databaseException:
                    _logger?.Error($"[{errorId}] {logMessage} - 本地数据库异常", databaseException);
                    title = LanguageManager.Instance["Exc_DbErrorTitle"];
                    message = logMessage;
                    type = NotificationType.Error;
                    break;
                case WwToolConfigException configException:
                    _logger?.Error($"[{errorId}] {logMessage} - 配置文件异常", configException);
                    title = LanguageManager.Instance["Exc_ConfigErrorTitle"];
                    message = logMessage;
                    type = NotificationType.Error;
                    break;
                case WwToolException businessException:
                    _logger?.Warn($"[{errorId}] {logMessage} - 业务提示: {businessException.Message}");
                    title = LanguageManager.Instance["Toast_Warning"];
                    message = businessException.Message;
                    type = NotificationType.Warning;
                    break;
                default:
                    _logger?.Error($"[{errorId}] {logMessage}", ex);
                    title = LanguageManager.Instance["Exc_SystemErrorTitle"];
                    message = string.IsNullOrWhiteSpace(contextMessage)
                        ? LanguageManager.Instance["Exc_UnhandledSys"]
                        : string.Format(LanguageManager.Instance["Exc_OperationFailed"], contextMessage);
                    type = NotificationType.Error;
                    break;
            }

            if (!notifyUser) return;

            _uiStateService?.ShowToast(new NotificationRequest
            {
                Title = title,
                Message = $"{message}  {LanguageManager.Instance["Toast_ErrorCode"]}: {errorId}",
                Type = type,
                Priority = NotificationPriority.Important,
                Source = "ExceptionHelper",
                DedupeKey = $"exception:{ex.GetType().FullName}:{contextMessage}",
                ActionText = LanguageManager.Instance["Action_ViewLogs"],
                Action = OpenLogFolder
            });
        }

        private static void OpenLogFolder()
        {
            try
            {
                string configuredFolder = _configService?.App.LogFolderPath ?? string.Empty;
                string folder = string.IsNullOrWhiteSpace(configuredFolder)
                    ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Local", "Logs")
                    : configuredFolder;
                if (!Path.IsPathRooted(folder)) folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folder);
                Directory.CreateDirectory(folder);
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
            }
            catch (Exception openException)
            {
                _logger?.Error("打开日志目录失败", openException);
            }
        }
    }
}
