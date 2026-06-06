using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media;
using WwTool.Common.Enums;

namespace WwTool.Common.Models
{
    public class NotificationModel
    {
        public string Title { get; set; }
        public string Message { get; set; }
        public NotificationType Type { get; set; }

        public DelegateCommand CloseCommand { get; set; }

        public SolidColorBrush ThemeBrush
        {
            get
            {
                var brush = Type switch
                {
                    NotificationType.Success => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#12B76A")), // 绿色
                    NotificationType.Error => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F04438")),   // 红色
                    NotificationType.Warning => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B")), // 黄色/橙色
                    _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"))                         // 蓝色 (Info 默认)
                };

                brush.Freeze();
                return brush;
            }
        }
    }
}
