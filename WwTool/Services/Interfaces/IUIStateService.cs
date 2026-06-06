using System.Collections.ObjectModel;
using WwTool.Common.Enums;
using WwTool.Common.Models;

namespace WwTool.Services.Interfaces
{
    public interface IUIStateService
    {
        bool IsLoading { get; }
        string LoadingMessage { get; }
        ObservableCollection<NotificationModel> Notifications { get; }
        bool IsDialogVisible { get; }
        object? CurrentDialogView { get; }
        void ShowLoading(string message = "正在处理中...");
        void HideLoading();
        void ShowToast(string title, string message, NotificationType type = NotificationType.Info);
        void RemoveToast(NotificationModel notification);
        void ShowDialog(object view);
        void CloseDialog();
    }
}
