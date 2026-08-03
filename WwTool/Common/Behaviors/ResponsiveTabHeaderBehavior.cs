using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WwTool.Common.Utils;

namespace WwTool.Common.Behaviors
{
    public static class ResponsiveTabHeaderBehavior
    {
        public static readonly DependencyProperty EnableResponsiveHeadersProperty =
            DependencyProperty.RegisterAttached(
                "EnableResponsiveHeaders",
                typeof(bool),
                typeof(ResponsiveTabHeaderBehavior),
                new PropertyMetadata(false, OnEnableResponsiveHeadersChanged));

        private static readonly DependencyPropertyKey IsCompactPropertyKey =
            DependencyProperty.RegisterAttachedReadOnly(
                "IsCompact",
                typeof(bool),
                typeof(ResponsiveTabHeaderBehavior),
                new PropertyMetadata(false));

        public static readonly DependencyProperty IsCompactProperty = IsCompactPropertyKey.DependencyProperty;

        private static readonly DependencyProperty StateProperty =
            DependencyProperty.RegisterAttached(
                "State",
                typeof(ResponsiveState),
                typeof(ResponsiveTabHeaderBehavior));

        public static void SetEnableResponsiveHeaders(DependencyObject element, bool value)
            => element.SetValue(EnableResponsiveHeadersProperty, value);

        public static bool GetEnableResponsiveHeaders(DependencyObject element)
            => (bool)element.GetValue(EnableResponsiveHeadersProperty);

        public static bool GetIsCompact(DependencyObject element)
            => (bool)element.GetValue(IsCompactProperty);

        private static void OnEnableResponsiveHeadersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TabControl tabControl) return;

            if ((bool)e.NewValue)
            {
                Attach(tabControl);
            }
            else
            {
                Detach(tabControl);
            }
        }

        private static void Attach(TabControl tabControl)
        {
            var state = (ResponsiveState?)tabControl.GetValue(StateProperty) ?? new ResponsiveState();
            tabControl.SetValue(StateProperty, state);
            if (state.IsAttached) return;

            state.IsAttached = true;
            tabControl.Loaded -= OnLoaded;
            tabControl.Loaded += OnLoaded;
            tabControl.Unloaded += OnUnloaded;
            tabControl.SizeChanged += OnSizeChanged;
            tabControl.SelectionChanged += OnSelectionChanged;
            tabControl.PreviewMouseMove += OnPreviewMouseMove;
            tabControl.MouseLeave += OnMouseLeave;
            tabControl.PreviewMouseWheel += OnPreviewMouseWheel;
            LanguageManager.Instance.PropertyChanged += state.OnLanguageChanged;

            if (tabControl.Items is INotifyCollectionChanged collection)
            {
                state.Collection = collection;
                collection.CollectionChanged += state.OnItemsChanged;
            }

            state.Schedule(tabControl, forceFullMeasure: true);
        }

        private static void Detach(TabControl tabControl)
        {
            if (tabControl.GetValue(StateProperty) is not ResponsiveState state) return;

            state.IsAttached = false;
            tabControl.Loaded -= OnLoaded;
            tabControl.Unloaded -= OnUnloaded;
            tabControl.SizeChanged -= OnSizeChanged;
            tabControl.SelectionChanged -= OnSelectionChanged;
            tabControl.PreviewMouseMove -= OnPreviewMouseMove;
            tabControl.MouseLeave -= OnMouseLeave;
            tabControl.PreviewMouseWheel -= OnPreviewMouseWheel;
            LanguageManager.Instance.PropertyChanged -= state.OnLanguageChanged;

            if (state.Collection != null)
            {
                state.Collection.CollectionChanged -= state.OnItemsChanged;
                state.Collection = null;
            }

            tabControl.ClearValue(IsCompactPropertyKey);
        }

        private static void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is TabControl tabControl && tabControl.GetValue(StateProperty) is ResponsiveState state)
            {
                if (!state.IsAttached)
                {
                    Attach(tabControl);
                }
                else
                {
                    state.Schedule(tabControl, forceFullMeasure: true);
                }
            }
        }

        private static void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (sender is not TabControl tabControl ||
                tabControl.GetValue(StateProperty) is not ResponsiveState state)
            {
                return;
            }

            state.IsAttached = false;
            tabControl.Unloaded -= OnUnloaded;
            tabControl.SizeChanged -= OnSizeChanged;
            tabControl.SelectionChanged -= OnSelectionChanged;
            tabControl.PreviewMouseMove -= OnPreviewMouseMove;
            tabControl.MouseLeave -= OnMouseLeave;
            tabControl.PreviewMouseWheel -= OnPreviewMouseWheel;
            LanguageManager.Instance.PropertyChanged -= state.OnLanguageChanged;

            if (state.Collection != null)
            {
                state.Collection.CollectionChanged -= state.OnItemsChanged;
                state.Collection = null;
            }

            tabControl.ClearValue(IsCompactPropertyKey);
        }

        private static void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is not TabControl tabControl ||
                tabControl.GetValue(StateProperty) is not ResponsiveState state)
            {
                return;
            }

            bool compact = GetIsCompact(tabControl);
            bool canRestoreFullHeaders = compact && tabControl.ActualWidth >= state.RequiredExpandedWidth;
            state.Schedule(tabControl, forceFullMeasure: !compact || canRestoreFullHeaders);
        }

        private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not TabControl tabControl || !GetIsCompact(tabControl)) return;

            ScrollViewer? headerScroller = FindVisualChild<ScrollViewer>(tabControl, "PART_HeaderScrollViewer");
            if (headerScroller is not { IsMouseOver: true } || headerScroller.ScrollableWidth <= 0) return;

            headerScroller.ScrollToHorizontalOffset(headerScroller.HorizontalOffset - e.Delta);
            e.Handled = true;
        }

        private static void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is TabControl tabControl && GetIsCompact(tabControl))
            {
                BringSelectedHeaderIntoView(tabControl);
            }
        }

        private static void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (sender is not TabControl tabControl ||
                !GetIsCompact(tabControl) ||
                tabControl.GetValue(StateProperty) is not ResponsiveState state ||
                e.OriginalSource is not DependencyObject source)
            {
                return;
            }

            TabItem? hoveredTab = FindVisualParent<TabItem>(source);
            if (hoveredTab == null ||
                ItemsControl.ItemsControlFromItemContainer(hoveredTab) != tabControl ||
                ReferenceEquals(state.LastHoveredTab, hoveredTab))
            {
                return;
            }

            state.LastHoveredTab = hoveredTab;
            tabControl.Dispatcher.BeginInvoke(DispatcherPriority.Render, () => hoveredTab.BringIntoView());
        }

        private static void OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is TabControl tabControl && tabControl.GetValue(StateProperty) is ResponsiveState state)
            {
                state.LastHoveredTab = null;
            }
        }

        private static void BringSelectedHeaderIntoView(TabControl tabControl)
        {
            tabControl.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                if (tabControl.ItemContainerGenerator.ContainerFromItem(tabControl.SelectedItem) is TabItem selectedTab)
                {
                    selectedTab.BringIntoView();
                }
            });
        }

        private static void Evaluate(TabControl tabControl, ResponsiveState state, bool forceFullMeasure)
        {
            if (!tabControl.IsLoaded || tabControl.ActualWidth <= 0) return;

            if (forceFullMeasure)
            {
                tabControl.SetValue(IsCompactPropertyKey, false);
                tabControl.UpdateLayout();

                double requiredWidth = 8;
                foreach (object item in tabControl.Items)
                {
                    if (tabControl.ItemContainerGenerator.ContainerFromItem(item) is TabItem tabItem)
                    {
                        requiredWidth += tabItem.DesiredSize.Width;
                    }
                }

                state.RequiredExpandedWidth = requiredWidth;
            }

            ScrollViewer? headerScroller = FindVisualChild<ScrollViewer>(tabControl, "PART_HeaderScrollViewer");
            double availableWidth = headerScroller?.ViewportWidth > 0
                ? headerScroller.ViewportWidth
                : tabControl.ActualWidth;
            bool shouldCompact = state.RequiredExpandedWidth > availableWidth;
            tabControl.SetValue(IsCompactPropertyKey, shouldCompact);
            if (shouldCompact) BringSelectedHeaderIntoView(tabControl);
        }

        private static T? FindVisualChild<T>(DependencyObject parent, string? name = null)
            where T : FrameworkElement
        {
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, index);
                if (child is T match && (name == null || match.Name == name)) return match;

                T? nested = FindVisualChild<T>(child, name);
                if (nested != null) return nested;
            }

            return null;
        }

        private static T? FindVisualParent<T>(DependencyObject child)
            where T : DependencyObject
        {
            DependencyObject? current = child;
            while (current != null)
            {
                if (current is T match) return match;
                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private sealed class ResponsiveState
        {
            private TabControl? _tabControl;
            private bool _forceFullMeasure;
            private bool _isScheduled;

            public bool IsAttached { get; set; }
            public double RequiredExpandedWidth { get; set; }
            public INotifyCollectionChanged? Collection { get; set; }
            public TabItem? LastHoveredTab { get; set; }

            public void Schedule(TabControl tabControl, bool forceFullMeasure)
            {
                _tabControl = tabControl;
                _forceFullMeasure |= forceFullMeasure;
                if (_isScheduled) return;

                _isScheduled = true;
                tabControl.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
                {
                    _isScheduled = false;
                    if (_tabControl == null || !IsAttached) return;

                    bool force = _forceFullMeasure;
                    _forceFullMeasure = false;
                    Evaluate(_tabControl, this, force);
                });
            }

            public void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
            {
                if (_tabControl != null) Schedule(_tabControl, forceFullMeasure: true);
            }

            public void OnLanguageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
            {
                if (_tabControl != null) Schedule(_tabControl, forceFullMeasure: true);
            }
        }
    }
}
