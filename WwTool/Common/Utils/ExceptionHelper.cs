using System;
using System.Threading.Tasks;
using WwTool.Common.Enums;
using WwTool.Common.Exceptions;
using WwTool.Services.Interfaces;

namespace WwTool.Common.Utils
{
    /// <summary>
    /// 异常处理工具类
    /// </summary>
    public static class ExceptionHelper
    {
        private static ILoggerService? _logger;
        private static IUIStateService? _uiStateService;

        public static void Initialize(ILoggerService logger, IUIStateService uiStateService)
        {
            _logger = logger;
            _uiStateService = uiStateService;
        }

        /// <summary>
        /// 执行同步操作，并捕获和记录异常
        /// </summary>
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

        /// <summary>
        /// 执行异步操作，并捕获和记录异常
        /// </summary>
        public static async Task ExecuteAsync(Func<Task> action, string? contextMessage = null, Action<Exception>? onError = null)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                HandleException(ex, contextMessage);
                onError?.Invoke(ex);
            }
        }

        /// <summary>
        /// 执行带有返回值的同步操作，并捕获和记录异常
        /// </summary>
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

        /// <summary>
        /// 执行带有返回值的异步操作，并捕获和记录异常
        /// </summary>
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

        /// <summary>
        /// 统一异常处理逻辑（日志+弹窗）
        /// </summary>
        public static void HandleException(Exception ex, string? contextMessage = null)
        {
            string logMsg = string.IsNullOrEmpty(contextMessage) ? LanguageManager.Instance["Exc_UnhandledSys"] : string.Format(LanguageManager.Instance["Exc_OperationFailed"], contextMessage);

            if (ex is WwToolAuthException authEx)
            {
                // 登录认证类异常：用 Warn 记录，显示为 Warning 级别的“登录失败”
                _logger?.Warn($"{logMsg} - 登录认证失败: {authEx.Message}");
                _uiStateService?.ShowToast(LanguageManager.Instance["Exc_LoginFailedTitle"], authEx.Message, NotificationType.Warning);
            }
            else if (ex is WwToolGamePathException pathEx)
            {
                // 路径配置类异常：用 Warn 记录，显示为 Warning 级别的“路径错误”
                _logger?.Warn($"{logMsg} - 游戏路径错误: {pathEx.Message}");
                _uiStateService?.ShowToast(LanguageManager.Instance["Exc_PathErrorTitle"], pathEx.Message, NotificationType.Warning);
            }
            else if (ex is WwToolApiException apiEx)
            {
                // 接口调用类异常：用 Error 记录，显示为 Error 级别的“网络请求失败”
                _logger?.Error($"{logMsg} - 网络接口异常", apiEx);
                _uiStateService?.ShowToast(LanguageManager.Instance["Exc_NetworkErrorTitle"], apiEx.Message, NotificationType.Error);
            }
            else if (ex is WwToolDatabaseException dbEx)
            {
                // 数据库类异常：用 Error 记录，显示为 Error 级别的“数据库错误”
                _logger?.Error($"{logMsg} - 本地数据库异常", dbEx);
                _uiStateService?.ShowToast(LanguageManager.Instance["Exc_DbErrorTitle"], dbEx.Message, NotificationType.Error);
            }
            else if (ex is WwToolConfigException cfgEx)
            {
                // 配置文件类异常：用 Error 记录，显示为 Error 级别的“配置加载/保存失败”
                _logger?.Error($"{logMsg} - 配置文件异常", cfgEx);
                _uiStateService?.ShowToast(LanguageManager.Instance["Exc_ConfigErrorTitle"], cfgEx.Message, NotificationType.Error);
            }
            else if (ex is WwToolException businessEx)
            {
                // 通用业务级异常：用 Warn 记录，显示为 Warning 级别的“提示”
                _logger?.Warn($"{logMsg} - 业务提示: {businessEx.Message}");
                _uiStateService?.ShowToast(LanguageManager.Instance["Toast_Warning"], businessEx.Message, NotificationType.Warning);
            }
            else
            {
                // 未知系统致命异常：用 Error 记录并记录堆栈，显示为 Error 级别的“系统错误”
                _logger?.Error(logMsg, ex);
                string friendlyMessage = string.IsNullOrEmpty(contextMessage)
                    ? string.Format(LanguageManager.Instance["Exc_UnknownSystemError"], ex.Message)
                    : string.Format(LanguageManager.Instance["Exc_FailedReason"], contextMessage, ex.Message);
                _uiStateService?.ShowToast(LanguageManager.Instance["Exc_SystemErrorTitle"], friendlyMessage, NotificationType.Error);
            }
        }
    }
}
