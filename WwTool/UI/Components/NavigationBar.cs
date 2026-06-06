using System;
using System.Security.Cryptography.Xml;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WwTool.Common.Models;
using WwTool.Common.Utils;

namespace WwTool.UI.Components
{
    /// <summary>
    /// 导航栏控件
    /// </summary>
    public class NavigationBar : ListBox
    {
        private FrameworkElement _circle;
        private DoubleAnimation _animation;
        private TranslateTransform _transform;
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
                Duration = TimeSpan.FromMilliseconds(250),
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
                            12, // FontSize
                            Brushes.Black,
                            dpi);

                        if (formattedText.Width > maxTextWidth)
                        {
                            maxTextWidth = formattedText.Width;
                        }
                    }
                }
            }

            // Width: left margin (8) + icon width (30) + margin (3) + text width + safety padding (24) = 65 + maxTextWidth
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
            DoubleAnimation navAnim = new DoubleAnimation
            {
                To = ExpandedWidth,
                Duration = TimeSpan.FromSeconds(0.25),
                EasingFunction = new PowerEase { EasingMode = EasingMode.EaseInOut, Power = 2 }
            };
            this.BeginAnimation(WidthProperty, navAnim);

            if (_circle != null)
            {
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
            DoubleAnimation navAnim = new DoubleAnimation
            {
                To = 50,
                Duration = TimeSpan.FromSeconds(0.2),
                EasingFunction = new PowerEase { EasingMode = EasingMode.EaseInOut, Power = 2 }
            };
            this.BeginAnimation(WidthProperty, navAnim);

            if (_circle != null)
            {
                DoubleAnimation rectAnim = new DoubleAnimation
                {
                    To = 35,
                    Duration = TimeSpan.FromSeconds(0.15),
                    EasingFunction = new PowerEase { EasingMode = EasingMode.EaseInOut, Power = 2 }
                };
                _circle.BeginAnimation(WidthProperty, rectAnim);
            }
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

            transform?.BeginAnimation(TranslateTransform.YProperty, _animation);
        }
    }
}
