using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using WwTool.Common.Enums;
using WwTool.Common.Models;
using WwTool.Services.Interfaces;

namespace WwTool.Services
{
    /// <summary>
    /// UI状态服务
    /// </summary>
    public class UIStateService : BindableBase, IUIStateService
    {
        private bool _isLoading;
        /// <summary>
        /// 是否处于加载中
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        private string _loadingMessage;
        /// <summary>
        /// 加载中消息
        /// </summary>
        public string LoadingMessage
        {
            get => _loadingMessage;
            private set => SetProperty(ref _loadingMessage, value);
        }

        /// <summary>
        /// 弹窗列表
        /// </summary>
        public ObservableCollection<NotificationModel> Notifications { get; } = new ObservableCollection<NotificationModel>();

        /// <summary>
        /// 显示加载弹窗
        /// </summary>
        /// <param name="message"></param>
        public void ShowLoading(string message = "正在处理中...")
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                LoadingMessage = message;
                IsLoading = true;
            });
        }

        /// <summary>
        /// 隐藏加载弹窗
        /// </summary>
        public void HideLoading()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsLoading = false;
            });
        }

        /// <summary>
        /// 弹窗
        /// </summary>
        /// <param name="title">标题</param>
        /// <param name="message">消息</param>
        /// <param name="type">严重等级</param>
        public void ShowToast(string title, string message, NotificationType type = NotificationType.Info)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var model = new NotificationModel { Title = title, Message = message, Type = type };
                // 绑定自身的移除逻辑
                model.CloseCommand = new DelegateCommand(() => RemoveToast(model));
                Notifications.Add(model);
            });
        }

        /// <summary>
        /// 删除弹窗
        /// </summary>
        /// <param name="notification"></param>
        public void RemoveToast(NotificationModel notification)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Notifications.Remove(notification);
            });
        }
    }
}
