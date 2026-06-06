using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace WwTool.Extensions
{
    public static class ProgressBarExtensions
    {
        public static readonly DependencyProperty SmoothValueProperty =
            DependencyProperty.RegisterAttached(
                "SmoothValue",
                typeof(double),
                typeof(ProgressBarExtensions),
                new PropertyMetadata(0.0, OnSmoothValuePercentageChanged));

        public static double GetSmoothValue(DependencyObject obj) => (double)obj.GetValue(SmoothValueProperty);
        public static void SetSmoothValue(DependencyObject obj, double value) => obj.SetValue(SmoothValueProperty, value);

        private static void OnSmoothValuePercentageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ProgressBar progressBar)
            {
                // 创建平滑渐变动画
                var animation = new DoubleAnimation
                {
                    To = (double)e.NewValue,
                    Duration = TimeSpan.FromSeconds(0.4),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } // 平滑淡出效果
                };
                progressBar.BeginAnimation(ProgressBar.ValueProperty, animation);
            }
        }
    }
}
