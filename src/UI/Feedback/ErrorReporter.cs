using WwTool.Core.Abstractions;
using WwTool.Core.Enums;
using WwTool.Core.Exceptions;
using WwTool.Core.Localization;
using WwTool.Core.Services.Gacha;

namespace WwTool.UI.Feedback;

/// <summary>报告给用户的错误：标题、消息（含错误编号时已附上）与严重程度。</summary>
public sealed record ErrorReport(string Title, string Message, NotificationType Type);

/// <summary>
/// 把操作失败转换为日志 + 简短 Toast（规则沿用 1.x ExceptionHelper）：业务类异常显示其消息，
/// 网络 / 数据库 / 配置 / 未知异常只显示“某操作失败”与错误编号，详细信息只写日志。
/// 取消（<see cref="OperationCanceledException"/>）不报告。
/// </summary>
public sealed class ErrorReporter(ILoggerService logger, IUIStateService uiState, LocalizationService localization)
{
    /// <param name="operation">已本地化的操作名，例如“同步抽卡记录”。</param>
    /// <returns>报告内容；取消时为 null。</returns>
    public ErrorReport? Report(Exception ex, string operation, bool notifyUser = true)
    {
        if (ex is OperationCanceledException) return null;
        string code = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        string failed = string.Format(localization["Exc_OperationFailed"], operation);
        string log = $"[{code}] {operation} 失败";

        (string title, string message, NotificationType type) = ex switch
        {
            GachaLinkAccountMismatchException m =>
                (localization["Toast_Warning"], string.Format(localization["Msg_GachaLinkAccountMismatch"], m.LinkUidMasked, m.AccountUidMasked), NotificationType.Warning),
            WwToolAuthException => (localization["Exc_LoginFailedTitle"], ex.Message, NotificationType.Warning),
            WwToolGamePathException => (localization["Exc_PathErrorTitle"], ex.Message, NotificationType.Warning),
            WwToolApiException => (localization["Exc_NetworkErrorTitle"], WithCode(failed, code), NotificationType.Error),
            WwToolDatabaseException => (localization["Exc_DbErrorTitle"], WithCode(failed, code), NotificationType.Error),
            WwToolConfigException => (localization["Exc_ConfigErrorTitle"], WithCode(failed, code), NotificationType.Error),
            WwToolException => (localization["Toast_Warning"], ex.Message, NotificationType.Warning),
            _ => (localization["Exc_SystemErrorTitle"], WithCode(failed, code), NotificationType.Error),
        };

        if (type == NotificationType.Error) logger.Error(log, ex);
        else logger.Warn($"{log}：{ex.Message}");

        if (notifyUser)
        {
            uiState.ShowToast(new NotificationRequest
            {
                Title = title,
                Message = message,
                Type = type,
                Source = "Error",
                DedupeKey = $"{operation}:{ex.GetType().FullName}",
            });
        }
        return new ErrorReport(title, message, type);
    }

    private string WithCode(string message, string code) => $"{message}（{localization["Toast_ErrorCode"]} {code}）";
}
