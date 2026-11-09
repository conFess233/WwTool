using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace WwTool.UI.Shell;

/// <summary>
/// 悬浮按钮视图：点遮罩或 Escape 收起；展开时焦点移到首项，收起后回到圆钮，便于键盘逐项操作。
/// </summary>
public partial class FabMenu : UserControl
{
    private FabMenuViewModel? _viewModel;

    public FabMenu()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null) _viewModel.PropertyChanged -= OnViewModelChanged;
        _viewModel = DataContext as FabMenuViewModel;
        if (_viewModel is not null) _viewModel.PropertyChanged += OnViewModelChanged;
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FabMenuViewModel.IsExpanded) || _viewModel is null) return;
        bool expanded = _viewModel.IsExpanded;
        Dispatcher.UIThread.Post(() =>
        {
            if (expanded)
            {
                Control? first = ActionList.ContainerFromIndex(0);
                (first as Button ?? first?.FindDescendantOfType<Button>())?.Focus(NavigationMethod.Tab);
            }
            else if (ToggleButton.IsVisible && IsKeyboardFocusWithin)
            {
                ToggleButton.Focus(NavigationMethod.Tab);
            }
        }, DispatcherPriority.Input);
    }

    private void OnActionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: FabAction action } && _viewModel is not null)
        {
            _viewModel.ExecuteCommand.Execute(action);
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _viewModel is { IsExpanded: true })
        {
            _viewModel.IsExpanded = false;
            e.Handled = true;
        }
    }

    private void OnScrimPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_viewModel is not null) _viewModel.IsExpanded = false;
        e.Handled = true;
    }
}
