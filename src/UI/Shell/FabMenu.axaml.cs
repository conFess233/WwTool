using System.ComponentModel;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WwTool.UI.Theming;

namespace WwTool.UI.Shell;

/// <summary>
/// 悬浮按钮视图（动效「悬浮按钮展开」）：展开时遮罩淡入，操作胶囊自下而上从 94% 缩放、下方 18 处浮现，
/// 间隔 60ms、各 420ms；收起时淡出。点遮罩或 Escape 收起；展开时焦点移到首项，收起后回到按钮。
/// </summary>
public partial class FabMenu : UserControl
{
    private FabMenuViewModel? _viewModel;
    private CancellationTokenSource? _animation;

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
        UpdateContent();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(FabMenuViewModel.IsExpanded):
                UpdateContent();
                _ = AnimateAsync(_viewModel!.IsExpanded);
                break;
            case nameof(FabMenuViewModel.IsExtended):
                UpdateContent();
                break;
        }
    }

    /// <summary>扩展态未展开时显示「图标 + 文字」，其余时候显示加号 / 关闭。</summary>
    private void UpdateContent()
    {
        bool extended = _viewModel is { IsExtended: true, IsExpanded: false };
        ExtendedContent.IsVisible = extended;
        Plus.IsVisible = !extended;
        ToggleButton.Classes.Set("extended", extended);
    }

    private async Task AnimateAsync(bool expand)
    {
        _animation?.Cancel();
        var cts = new CancellationTokenSource();
        _animation = cts;
        CancellationToken token = cts.Token;

        if (expand)
        {
            Scrim.IsVisible = true;
            ActionList.IsVisible = true;
            _ = Fade(Scrim, 0, 1, Motion.Relaxed, token);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            Control[] items = Containers();
            foreach (Control item in items) item.Opacity = 0;
            // 自下而上：离按钮最近的最后一项最先出现。
            var tasks = items.Reverse().Select((item, i) => Rise(item, TimeSpan.FromMilliseconds(60 * i), token)).ToArray();
            FocusFirst();
            await Task.WhenAll(tasks);
        }
        else
        {
            var tasks = new List<Task> { Fade(Scrim, Scrim.Opacity, 0, Motion.Standard, token) };
            tasks.AddRange(Containers().Select(item => Fade(item, item.Opacity, 0, Motion.Fast, token)));
            await Task.WhenAll(tasks);
            if (token.IsCancellationRequested) return;
            Scrim.IsVisible = false;
            ActionList.IsVisible = false;
            if (IsKeyboardFocusWithin) ToggleButton.Focus(NavigationMethod.Tab);
        }
    }

    private Control[] Containers() =>
        Enumerable.Range(0, ActionList.ItemCount).Select(i => ActionList.ContainerFromIndex(i)).OfType<Control>().ToArray();

    private static Task Fade(Visual target, double from, double to, TimeSpan duration, CancellationToken token)
    {
        var animation = new Animation
        {
            Duration = duration,
            Easing = Motion.StandardEasing,
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(OpacityProperty, from) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OpacityProperty, to) } },
            },
        };
        return animation.RunAsync(target, token);
    }

    private static Task Rise(Control item, TimeSpan delay, CancellationToken token)
    {
        bool reduced = Motion.Reduced;
        item.RenderTransformOrigin = new RelativePoint(1, 1, RelativeUnit.Relative);
        item.RenderTransform = new TransformGroup { Children = { new ScaleTransform(), new TranslateTransform() } };
        var animation = new Animation
        {
            Duration = Motion.Relaxed,
            Delay = delay,
            Easing = Motion.StandardEasing,
            FillMode = FillMode.Both,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0),
                    Setters =
                    {
                        new Setter(OpacityProperty, 0d),
                        new Setter(ScaleTransform.ScaleXProperty, reduced ? 1d : 0.94),
                        new Setter(ScaleTransform.ScaleYProperty, reduced ? 1d : 0.94),
                        new Setter(TranslateTransform.YProperty, reduced ? 0d : 18d),
                    },
                },
                new KeyFrame
                {
                    Cue = new Cue(1),
                    Setters =
                    {
                        new Setter(OpacityProperty, 1d),
                        new Setter(ScaleTransform.ScaleXProperty, 1d),
                        new Setter(ScaleTransform.ScaleYProperty, 1d),
                        new Setter(TranslateTransform.YProperty, 0d),
                    },
                },
            },
        };
        return animation.RunAsync(item, token);
    }

    private void FocusFirst()
    {
        Control? first = ActionList.ContainerFromIndex(0);
        (first as Button ?? first?.FindDescendantOfType<Button>())?.Focus(NavigationMethod.Unspecified);
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
