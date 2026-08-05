using System;
using System.Security.Cryptography.Xml;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WwTool.Common.Models;
using WwTool.Common.Models.Entities;
using WwTool.Common.Utils;

namespace WwTool.UI.Components
{
    /// <summary>
    /// 导航栏控件
    /// </summary>
    public class NavigationBar : ListBox
    {
        private FrameworkElement? _circle;
        private DoubleAnimation _animation = null!;
        private TranslateTransform _transform = new();
        private FrameworkElement? _pushTarget;
        private TranslateTransform? _pushTransform;
        private ColumnDefinition? _layoutColumn;
        private int _transitionVersion;
        private const double CollapsedWidth = 50;
        /// <summary>
        /// 展开宽度
        /// </summary>
        public static readonly DependencyProperty ExpandedWidthProperty =
            DependencyProperty.Register(
                nameof(ExpandedWidth),
                typeof(double),
                typeof(NavigationBar),
                new PropertyMetadata(143.0));

        public double ExpandedWidth
        {
            get => (double)GetValue(ExpandedWidthProperty);
            set => SetValue(ExpandedWidthProperty, value);
        }

        /// <summary>
        /// 高亮显示宽度
        /// </summary>
        public static readonly DependencyProperty HighlightWidthProperty =
            DependencyProperty.Register(
                nameof(HighlightWidth),
                typeof(double),
                typeof(NavigationBar),
                new PropertyMetadata(130.0));

        public double HighlightWidth
        {
            get => (double)GetValue(HighlightWidthProperty);
            set => SetValue(HighlightWidthProperty, value);
        }

        static NavigationBar()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(NavigationBar),
                new FrameworkPropertyMetadata(typeof(NavigationBar)));
        }

        public NavigationBar()
        {
            LanguageManager.Instance.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "Item[]")
                {
                    Dispatcher.BeginInvoke(new Action(RecalculateWidths), System.Windows.Threading.DispatcherPriority.Loaded);
                }
            };
        }

        /// <summary>
        /// 配置主内容的视觉推移动画。动画期间只更新合成属性，结束时提交一次真实列宽。
        /// </summary>
        public void ConfigurePushTransition(FrameworkElement pushTarget, ColumnDefinition layoutColumn)
        {
            _pushTarget = pushTarget;
            _layoutColumn = layoutColumn;
            _pushTransform = new TranslateTransform();
            _pushTarget.RenderTransform = _pushTransform;
            ApplyFinalLayout(CollapsedWidth);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _circle = GetTemplateChild("PART_Rectangle") as FrameworkElement;

            _transform = new TranslateTransform();

            if (_circle != null)
            {
                _circle.RenderTransform = _transform;
            }

            InitAnimation();
            RecalculateWidths();
        }

        private void InitAnimation()
        {
            _animation = new DoubleAnimation
            {
                Duration = Application.Current.TryFindResource("MotionNormal") is Duration duration
                    ? duration
                    : new Duration(TimeSpan.FromMilliseconds(220)),
                EasingFunction = new QuinticEase
                {
                    EasingMode = EasingMode.EaseInOut
                }
            };
        }

        /// <summary>
        /// 数据源改变事件
        /// </summary>
        /// <param name="oldValue"></param>
        /// <param name="newValue"></param>
        protected override void OnItemsSourceChanged(System.Collections.IEnumerable oldValue, System.Collections.IEnumerable newValue)
        {
            base.OnItemsSourceChanged(oldValue, newValue);

            if (oldValue is System.Collections.Specialized.INotifyCollectionChanged oldCol)
            {
                oldCol.CollectionChanged -= Items_CollectionChanged;
            }

            if (newValue is System.Collections.Specialized.INotifyCollectionChanged newCol)
            {
                newCol.CollectionChanged += Items_CollectionChanged;
            }

            RecalculateWidths();
        }

        /// <summary>
        /// 列表项改变事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Items_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            RecalculateWidths();
        }

        /// <summary>
        /// 重新计算宽度
        /// </summary>
        private void RecalculateWidths()
        {
            double maxTextWidth = 0;

            if (ItemsSource != null)
            {
                var typeface = new Typeface(this.FontFamily ?? new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                double dpi = 1.0;
                try
                {
                    dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
                }
                catch
                {
                    // 防止版本过低导致无法获取DPI
                }

                foreach (var item in ItemsSource)
                {
                    string title = "";
                    if (item is NavItem navItem)
                    {
                        title = navItem.Title ?? "";
                    }
                    else if (item != null)
                    {
                        title = item.ToString() ?? "";
                    }

                    if (!string.IsNullOrEmpty(title))
                    {
                        var formattedText = new FormattedText(
                            title,
                            System.Globalization.CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            typeface,
                            12, // 设置字体大小。
                            Brushes.Black,
                            dpi);

                        if (formattedText.Width > maxTextWidth)
                        {
                            maxTextWidth = formattedText.Width;
                        }
                    }
                }
            }

            // 根据图标、文字和边距计算导航栏宽度。
            double targetExpanded = Math.Max(143, Math.Ceiling(65 + maxTextWidth));
            double targetHighlight = targetExpanded - 15;

            ExpandedWidth = targetExpanded;
            HighlightWidth = targetHighlight;

            if (IsMouseOver)
            {
                AnimateToExpanded();
            }
        }

        /// <summary>
        /// 鼠标移入事件
        /// </summary>
        /// <param name="e"></param>
        protected override void OnMouseEnter(System.Windows.Input.MouseEventArgs e)
        {
            base.OnMouseEnter(e);
            AnimateToExpanded();
        }

        /// <summary>
        /// 鼠标移出事件
        /// </summary>
        /// <param name="e"></param>
        protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            AnimateToCollapsed();
        }

        /// <summary>
        /// 动画到展开状态
        /// </summary>
        private void AnimateToExpanded()
        {
            AnimateLayoutTransition(ExpandedWidth, TimeSpan.FromMilliseconds(250));

            if (_circle != null)
            {
                if (IsReducedMotionEnabled())
                {
                    _circle.BeginAnimation(WidthProperty, null);
                    _circle.Width = HighlightWidth;
                    return;
                }

                DoubleAnimation rectAnim = new DoubleAnimation
                {
                    To = HighlightWidth,
                    Duration = TimeSpan.FromSeconds(0.3),
                    EasingFunction = new PowerEase { EasingMode = EasingMode.EaseInOut, Power = 2 }
                };
                _circle.BeginAnimation(WidthProperty, rectAnim);
            }
        }

        /// <summary>
        /// 动画到折叠状态
        /// </summary>
        private void AnimateToCollapsed()
        {
            AnimateLayoutTransition(CollapsedWidth, TimeSpan.FromMilliseconds(200));

            if (_circle != null)
            {
                if (IsReducedMotionEnabled())
                {
                    _circle.BeginAnimation(WidthProperty, null);
                    _circle.Width = 35;
                    return;
                }

                DoubleAnimation rectAnim = new DoubleAnimation
                {
                    To = 35,
                    Duration = TimeSpan.FromSeconds(0.15),
                    EasingFunction = new PowerEase { EasingMode = EasingMode.EaseInOut, Power = 2 }
                };
                _circle.BeginAnimation(WidthProperty, rectAnim);
            }
        }

        private void AnimateLayoutTransition(double targetWidth, TimeSpan duration)
        {
            int version = ++_transitionVersion;

            if (_layoutColumn == null || _pushTarget == null || _pushTransform == null || IsReducedMotionEnabled())
            {
                ApplyFinalLayout(targetWidth);
                return;
            }

            double currentWidth = Width;
            if (double.IsNaN(currentWidth) || currentWidth <= 0)
            {
                currentWidth = ActualWidth > 0 ? ActualWidth : CollapsedWidth;
            }

            double currentPush = _pushTransform.X;
            BeginAnimation(WidthProperty, null);
            _pushTransform.BeginAnimation(TranslateTransform.XProperty, null);
            Width = currentWidth;
            _pushTransform.X = currentPush;

            double committedWidth = _layoutColumn.Width.IsAbsolute
                ? _layoutColumn.Width.Value
                : CollapsedWidth;
            var easing = new PowerEase { EasingMode = EasingMode.EaseInOut, Power = 2 };
            var navAnimation = new DoubleAnimation(targetWidth, duration)
            {
                EasingFunction = easing,
                FillBehavior = FillBehavior.HoldEnd
            };
            var pushAnimation = new DoubleAnimation(targetWidth - committedWidth, duration)
            {
                EasingFunction = easing,
                FillBehavior = FillBehavior.HoldEnd
            };
            pushAnimation.Completed += (_, _) =>
            {
                if (version == _transitionVersion)
                {
                    ApplyFinalLayout(targetWidth);
                }
            };

            BeginAnimation(WidthProperty, navAnimation, HandoffBehavior.SnapshotAndReplace);
            _pushTransform.BeginAnimation(
                TranslateTransform.XProperty,
                pushAnimation,
                HandoffBehavior.SnapshotAndReplace);
        }

        private void ApplyFinalLayout(double width)
        {
            BeginAnimation(WidthProperty, null);
            Width = width;

            if (_layoutColumn != null)
            {
                _layoutColumn.Width = new GridLength(width);
            }

            if (_pushTransform != null)
            {
                _pushTransform.BeginAnimation(TranslateTransform.XProperty, null);
                _pushTransform.X = 0;
            }
        }

        private static bool IsReducedMotionEnabled()
        {
            if (!SystemParameters.ClientAreaAnimation)
            {
                return true;
            }

            return Application.Current.TryFindResource("MotionNormal") is Duration duration &&
                   duration.HasTimeSpan &&
                   duration.TimeSpan <= TimeSpan.FromMilliseconds(100);
        }

        /// <summary>
        /// 选中项改变事件
        /// </summary>
        /// <param name="e"></param>
        protected override void OnSelectionChanged(SelectionChangedEventArgs e)
        {
            base.OnSelectionChanged(e);

            if (_circle == null || SelectedIndex < 0)
                return;

            var item = ItemContainerGenerator.ContainerFromIndex(SelectedIndex) as ListBoxItem;

            if (item == null)
                return;

            Point point = item.TranslatePoint(new Point(0, 0), this);

            _animation.To = point.Y;

            var transform = _circle.RenderTransform as TranslateTransform;
            if (transform == null)
            {
                return;
            }

            if (IsReducedMotionEnabled())
            {
                transform.BeginAnimation(TranslateTransform.YProperty, null);
                transform.Y = point.Y;
            }
            else
            {
                transform.BeginAnimation(TranslateTransform.YProperty, _animation);
            }
        }
    }
}
