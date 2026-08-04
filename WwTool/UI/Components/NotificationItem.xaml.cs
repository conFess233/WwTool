using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.ComponentModel;
using WwTool.Common.Models;

namespace WwTool.UI.Components
{
    public partial class NotificationItem : UserControl
    {
        public NotificationItem()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            bool reducedMotion = DataContext is NotificationModel { IsReducedMotion: true };
            TimeSpan duration = reducedMotion ? TimeSpan.FromMilliseconds(80) : TimeSpan.FromMilliseconds(200);
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration));

            if (reducedMotion) return;
            var transform = new TranslateTransform(24, 0);
            RenderTransform = transform;
            transform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(24, 0, duration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
        }

        private void OnMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (DataContext is NotificationModel model) model.IsPaused = true;
        }

        private void OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (DataContext is NotificationModel model) model.IsPaused = false;
        }

        private void OnGotKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            if (DataContext is NotificationModel model) model.IsPaused = true;
        }

        private void OnLostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            if (DataContext is NotificationModel model && !IsMouseOver) model.IsPaused = false;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is NotificationModel oldModel) oldModel.PropertyChanged -= OnModelPropertyChanged;
            if (e.NewValue is NotificationModel newModel) newModel.PropertyChanged += OnModelPropertyChanged;
        }

        private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(NotificationModel.IsClosing) ||
                sender is not NotificationModel { IsClosing: true } model) return;

            TimeSpan duration = model.IsReducedMotion ? TimeSpan.FromMilliseconds(80) : TimeSpan.FromMilliseconds(150);
            BeginAnimation(OpacityProperty, new DoubleAnimation(Opacity, 0, duration));
            if (!model.IsReducedMotion && RenderTransform is TranslateTransform transform)
            {
                transform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(transform.X, 12, duration));
            }
        }
    }
}
