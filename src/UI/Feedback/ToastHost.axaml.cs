using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using WwTool.UI.Theming;

namespace WwTool.UI.Feedback;

public partial class ToastHost : UserControl
{
    /// <summary>入场：从右侧带回弹滑入（360ms）。</summary>
    private const double EnterMs = 360;

    /// <summary>退场：上移 12 并淡出，须不长于 <see cref="UIStateService"/> 的移除延时。</summary>
    internal const double ExitMs = 240;

    public ToastHost() => InitializeComponent();

    private void OnToastAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not Border { DataContext: ToastItem item } border) return;
        _ = AnimateAsync(border, EnterMs, t =>
        {
            // 位移用回弹缓动，透明度在前 40% 内完成。
            double x = Motion.Reduced ? 0 : 380 * (1 - Motion.BounceEasing.Ease(t));
            return (x, 0, Math.Min(1, t / 0.4));
        });
        PropertyChangedEventHandler? handler = null;
        handler = (_, args) =>
        {
            if (args.PropertyName != nameof(ToastItem.IsClosing) || !item.IsClosing) return;
            item.PropertyChanged -= handler;
            _ = AnimateAsync(border, ExitMs, t =>
            {
                double eased = Motion.StandardEasing.Ease(t);
                return (0, Motion.Reduced ? 0 : -12 * eased, 1 - eased);
            });
        };
        item.PropertyChanged += handler;
    }

    private static async Task AnimateAsync(Border border, double duration, Func<double, (double X, double Y, double Opacity)> frame)
    {
        if (border.RenderTransform is not TranslateTransform translate) border.RenderTransform = translate = new TranslateTransform();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            double t = Math.Min(1, clock.Elapsed.TotalMilliseconds / duration);
            (double x, double y, double opacity) = frame(t);
            translate.X = x;
            translate.Y = y;
            border.Opacity = opacity;
            if (t >= 1) break;
            await Task.Delay(16);
        }
    }

    // 悬停暂停倒计时，方便阅读较长的错误信息。
    private void OnToastPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Control { DataContext: ToastItem toast }) toast.IsPaused = true;
    }

    private void OnToastPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is Control { DataContext: ToastItem toast }) toast.IsPaused = false;
    }
}
