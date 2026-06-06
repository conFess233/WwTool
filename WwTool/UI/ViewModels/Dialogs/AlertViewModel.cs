using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Media.Effects;
using WwTool.Common.Events;
using WwTool.Common.Utils;

namespace WwTool.UI.ViewModels.Dialogs
{
    /// <summary>
    /// 警告/确认弹窗视图模型
    /// 用于显示模态弹出框，支持确认/取消操作和自定义数据回传
    /// </summary>
    public class AlertViewModel : BindableBase, IDialogAware
    {
        /// <summary>
        /// 事件聚合器，用于控制全局背景模糊状态
        /// </summary>
        private readonly IEventAggregator _eventAggregator;

        /// <summary>
        /// 弹窗标题
        /// </summary>
        private string _title = LanguageManager.Instance["Dialog_Prompt"];
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        /// <summary>
        /// 弹窗消息正文
        /// </summary>
        private string _message;
        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        /// <summary>
        /// 是否显示取消按钮
        /// </summary>
        private bool _showCancel;
        public bool ShowCancel
        {
            get => _showCancel;
            set => SetProperty(ref _showCancel, value);
        }

        /// <summary>
        /// 自定义数据，可用于弹窗与调用方之间传递额外信息
        /// </summary>
        private object _customData;
        public object CustomData
        {
            get => _customData;
            set => SetProperty(ref _customData, value);
        }

        /// <summary>
        /// 确认按钮命令
        /// </summary>
        public DelegateCommand OkCommand { get; }
        /// <summary>
        /// 取消按钮命令
        /// </summary>
        public DelegateCommand CancelCommand { get; }

        private DialogCloseListener RequestClose { get; }

        DialogCloseListener IDialogAware.RequestClose => RequestClose;

        /// <summary>
        /// 构造函数，初始化确认/取消命令
        /// </summary>
        public AlertViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            // 确定按钮
            OkCommand = new DelegateCommand(() =>
            {
                // 返回数据
                var parameters = new DialogParameters();
                parameters.Add("CustomData", CustomData);

                // 关闭弹窗并返回 OK 结果，携带参数
                RequestClose.Invoke(new DialogResult { Parameters = parameters, Result = ButtonResult.OK });
            });

            // 取消按钮
            CancelCommand = new DelegateCommand(() =>
            {
                RequestClose.Invoke(new DialogResult(ButtonResult.Cancel));
            });
        }

        // 弹窗能否被关闭
        public bool CanCloseDialog() => true;

        // 弹窗打开时触发，用于接收从主窗口传过来的参数
        public void OnDialogOpened(IDialogParameters parameters)
        {
            Title = parameters.GetValue<string>("Title") ?? LanguageManager.Instance["Dialog_Prompt"];
            Message = parameters.GetValue<string>("Message");
            ShowCancel = parameters.GetValue<bool>("ShowCancel");
            CustomData = parameters.GetValue<object>("CustomData"); // 接收自定义数据

            _eventAggregator.GetEvent<GlobalBlurEvent>().Publish(true);
        }

        /// <summary>
        /// 弹窗关闭时触发，取消全局背景模糊效果
        /// </summary>
        public void OnDialogClosed()
        {
            _eventAggregator.GetEvent<GlobalBlurEvent>().Publish(false);
        }
    }
}
