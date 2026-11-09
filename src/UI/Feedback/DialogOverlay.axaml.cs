using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace WwTool.UI.Feedback;

/// <summary>
/// 对话框覆盖层视图：Escape 取消、Enter 确认（焦点在按钮上时交给按钮自身）、点遮罩取消，以及进入动画。
/// </summary>
public partial class DialogOverlay : UserControl
{
    private DialogService? _service;

    public DialogOverlay()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, handledEventsToo: false);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_service is not null) _service.PropertyChanged -= OnServiceChanged;
        _service = DataContext as DialogService;
        if (_service is not null) _service.PropertyChanged += OnServiceChanged;
    }

    private void OnServiceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(DialogService.Current)) return;
        Card.Classes.Remove("shown");
        if (_service?.Current is not null)
        {
            // 下一帧再加类名，让过渡从初始状态开始。
            Dispatcher.UIThread.Post(() => Card.Classes.Add("shown"), DispatcherPriority.Render);
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_service?.Current is not { } dialog) return;
        if (e.Key == Key.Escape)
        {
            dialog.Cancel();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && e.Source is not Button)
        {
            e.Handled = dialog.TryAccept();
        }
    }

    private void OnScrimPressed(object? sender, PointerPressedEventArgs e)
    {
        _service?.Current?.Cancel();
        e.Handled = true;
    }
}
