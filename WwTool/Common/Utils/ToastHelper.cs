using WwTool.Common.Enums;
using WwTool.Common.Models;
using WwTool.Services.Interfaces;

namespace WwTool.Common.Utils
{
    /// <summary>
    /// 为用户主动触发的操作创建统一的结果 Toast。
    /// </summary>
    public static class ToastHelper
    {
        /// <summary>
        /// 显示重要操作结果，并交由通知服务执行模式过滤和重复合并。
        /// </summary>
        public static void ShowActionResult(
            IUIStateService uiStateService,
            string title,
            string message,
            NotificationType type,
            string source,
            string dedupeKey)
        {
            uiStateService.ShowToast(new NotificationRequest
            {
                Title = title,
                Message = message,
                Type = type,
                Priority = NotificationPriority.Important,
                Source = source,
                DedupeKey = dedupeKey
            });
        }
    }
}
