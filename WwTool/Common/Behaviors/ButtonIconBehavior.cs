using System.Windows;

namespace WwTool.Common.Behaviors
{
    public static class ButtonIconBehavior
    {
        public static readonly DependencyProperty UseNavigationColorsProperty =
            DependencyProperty.RegisterAttached(
                "UseNavigationColors",
                typeof(bool),
                typeof(ButtonIconBehavior),
                new FrameworkPropertyMetadata(false));

        public static void SetUseNavigationColors(DependencyObject element, bool value)
            => element.SetValue(UseNavigationColorsProperty, value);

        public static bool GetUseNavigationColors(DependencyObject element)
            => (bool)element.GetValue(UseNavigationColorsProperty);
    }
}
