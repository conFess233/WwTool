using System.Windows;

namespace WwTool.Common.Behaviors
{
    public static class TabItemIconBehavior
    {
        public static readonly DependencyProperty UseIconMaskProperty =
            DependencyProperty.RegisterAttached(
                "UseIconMask",
                typeof(bool),
                typeof(TabItemIconBehavior),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.Inherits));

        public static readonly DependencyProperty IconSizeProperty =
            DependencyProperty.RegisterAttached(
                "IconSize",
                typeof(double),
                typeof(TabItemIconBehavior),
                new FrameworkPropertyMetadata(24d, FrameworkPropertyMetadataOptions.Inherits));

        public static void SetUseIconMask(DependencyObject element, bool value)
            => element.SetValue(UseIconMaskProperty, value);

        public static bool GetUseIconMask(DependencyObject element)
            => (bool)element.GetValue(UseIconMaskProperty);

        public static void SetIconSize(DependencyObject element, double value)
            => element.SetValue(IconSizeProperty, value);

        public static double GetIconSize(DependencyObject element)
            => (double)element.GetValue(IconSizeProperty);
    }
}
