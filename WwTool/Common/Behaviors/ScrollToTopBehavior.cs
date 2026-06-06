using Microsoft.Xaml.Behaviors;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace WwTool.Common.Behaviors
{
    public class ScrollToTopBehavior : Behavior<Button>
    {
        public ScrollViewer TargetScrollViewer
        {
            get { return (ScrollViewer)GetValue(TargetScrollViewerProperty); }
            set { SetValue(TargetScrollViewerProperty, value); }
        }

        public static readonly DependencyProperty TargetScrollViewerProperty =
            DependencyProperty.Register("TargetScrollViewer", typeof(ScrollViewer), typeof(ScrollToTopBehavior), new PropertyMetadata(null, OnTargetChanged));

        private static void OnTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var behavior = (ScrollToTopBehavior)d;
            if (e.OldValue is ScrollViewer oldScroll)
                oldScroll.ScrollChanged -= behavior.OnScrollChanged;
            if (e.NewValue is ScrollViewer newScroll)
                newScroll.ScrollChanged += behavior.OnScrollChanged;
        }

        public double VerticalOffsetProxy
        {
            get { return (double)GetValue(VerticalOffsetProxyProperty); }
            set { SetValue(VerticalOffsetProxyProperty, value); }
        }

        public static readonly DependencyProperty VerticalOffsetProxyProperty =
            DependencyProperty.Register("VerticalOffsetProxy", typeof(double), typeof(ScrollToTopBehavior), new PropertyMetadata(0.0, OnVerticalOffsetProxyChanged));

        private static void OnVerticalOffsetProxyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var behavior = (ScrollToTopBehavior)d;
            if (behavior.TargetScrollViewer != null)
            {
                behavior.TargetScrollViewer.ScrollToVerticalOffset((double)e.NewValue);
            }
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            this.AssociatedObject.Click += OnButtonClick;
            this.AssociatedObject.Visibility = Visibility.Collapsed; // 初始隐藏
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            this.AssociatedObject.Click -= OnButtonClick;
            if (TargetScrollViewer != null)
                TargetScrollViewer.ScrollChanged -= OnScrollChanged;
        }

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (this.AssociatedObject == null || TargetScrollViewer == null)
            {
                return;
            }
            // 滚动超过 100 像素显示回到顶部按钮
            this.AssociatedObject.Visibility = TargetScrollViewer.VerticalOffset > 100 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnButtonClick(object sender, RoutedEventArgs e)
        {
            if (TargetScrollViewer == null) return;

            // 将代理属性设置为当前的实际滚动位置
            this.VerticalOffsetProxy = TargetScrollViewer.VerticalOffset;

            // 创建平滑动画
            DoubleAnimation verticalAnimation = new DoubleAnimation
            {
                From = TargetScrollViewer.VerticalOffset,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(350)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            Storyboard storyboard = new Storyboard();
            storyboard.Children.Add(verticalAnimation);

            // 设置动画作用
            Storyboard.SetTarget(verticalAnimation, this);
            Storyboard.SetTargetProperty(verticalAnimation, new PropertyPath(VerticalOffsetProxyProperty));

            // 播放动画
            storyboard.Begin();
        }
    }
}