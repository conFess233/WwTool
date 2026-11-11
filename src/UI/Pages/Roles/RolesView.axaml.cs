using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WwTool.UI.Controls;
using WwTool.UI.Theming;

namespace WwTool.UI.Pages.Roles;

/// <summary>
/// 角色页视图行为：点击角色卡打开详情浮层（动效「角色详情飞入」）。
/// 头像从格子飞到浮层立绘区中央并变圆（520ms），同时浮层从右侧 16 处滑入淡入；落定后飞行头像淡出露出立绘。
/// 浮层放在卡片右侧（右侧放不下时放左侧），左 / 右边缘带指向卡片的小箭头。
/// </summary>
public partial class RolesView : UserControl
{
    private const double PopoverWidth = 620;
    private const double Gap = 14;
    private const double FlyerSize = 96;
    private static readonly TimeSpan FlyDuration = TimeSpan.FromMilliseconds(520);

    private RoleTile? _open;
    private CancellationTokenSource? _animation;

    public RolesView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Escape && _open is not null)
            {
                Close();
                e.Handled = true;
            }
        });
        Scroller.ScrollChanged += (_, _) =>
        {
            if (_open is not null) Close();
        };
    }

    private void OnRoleClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: RoleTile tile } host) return;
        e.Handled = true;
        if (ReferenceEquals(_open, tile))
        {
            Close();
            return;
        }
        Open(tile, host);
    }

    private void OnDetailLayerPressed(object? sender, PointerPressedEventArgs e)
    {
        Close();
        e.Handled = true;
    }

    private void OnPopoverPressed(object? sender, PointerPressedEventArgs e) => e.Handled = true;

    private void Open(RoleTile tile, Control host)
    {
        if (_open is not null) _open.IsSelected = false;
        _open = tile;
        tile.IsSelected = true;

        DetailLayer.IsVisible = true;
        PopoverContent.Content = tile.Hover;
        Popover.Opacity = 0;
        Popover.Measure(Size.Infinity);
        double popHeight = Popover.DesiredSize.Height;

        // 卡片在浮层画布中的位置。
        // 浮层层刚显示、尚未排版，按页面自身坐标换算（浮层层左右各外扩 40）。
        Point? cardTopLeft = host.TranslatePoint(default, this);
        if (cardTopLeft is not { } inView) return;
        var card = new Point(inView.X + 40, inView.Y);
        double canvasW = Bounds.Width + 80, canvasH = Bounds.Height;
        Size cardSize = host.Bounds.Size;
        bool right = card.X + cardSize.Width + Gap + PopoverWidth <= canvasW - 24;
        double x = right ? card.X + cardSize.Width + Gap : card.X - Gap - PopoverWidth;
        x = Math.Clamp(x, 24, Math.Max(24, canvasW - PopoverWidth - 24));
        double y = Math.Clamp(card.Y - 8, 0, Math.Max(0, canvasH - popHeight - 8));
        Canvas.SetLeft(Popover, x);
        Canvas.SetTop(Popover, y);

        // 指向卡片的箭头：贴在浮层靠近卡片的一侧，高度对着卡片中线。
        double arrowY = Math.Clamp(card.Y + cardSize.Height / 2 - 6, y + 18, y + popHeight - 30);
        Canvas.SetLeft(Arrow, right ? x - 6 : x + PopoverWidth - 6);
        Canvas.SetTop(Arrow, arrowY);
        Arrow.RenderTransform = TransformOperations(right ? 45 : 225);
        Arrow.Opacity = 0;

        _animation?.Cancel();
        var cts = new CancellationTokenSource();
        _animation = cts;
        Control? avatar = host.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Name == "Avatar");
        _ = AnimateOpenAsync(tile, avatar, new Point(x, y), right, cts.Token);
    }

    private static ITransform TransformOperations(double degrees) => new RotateTransform(degrees);

    private async Task AnimateOpenAsync(RoleTile tile, Control? avatar, Point popover, bool fromRight, CancellationToken token)
    {
        bool reduced = Motion.Reduced;
        // 立绘区中心：浮层内边距 20，左栏占 (620 - 42 - 22) 的 2/5，立绘高 290。
        double leftColumn = (PopoverWidth - 42 - 22) * 2 / 5;
        var target = new Rect(popover.X + 20 + leftColumn / 2 - FlyerSize / 2, popover.Y + 20 + 145 - FlyerSize / 2, FlyerSize, FlyerSize);
        Rect start = avatar?.TranslatePoint(default, this) is { } p ? new Rect(p.X + 40, p.Y, avatar.Bounds.Width, avatar.Bounds.Height) : target;

        Flyer.IsVisible = !reduced && avatar is not null;
        FlyerImage.Path = tile.IconPath;
        Flyer.Opacity = 1;
        double slide = fromRight ? 16 : -16;
        var popTransform = new TranslateTransform(reduced ? 0 : slide, 0);
        Popover.RenderTransform = popTransform;

        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (!token.IsCancellationRequested)
        {
            double t = Math.Clamp(clock.Elapsed.TotalMilliseconds / FlyDuration.TotalMilliseconds, 0, 1);
            double e = Motion.StandardEasing.Ease(t);
            if (Flyer.IsVisible)
            {
                double left = start.X + (target.X - start.X) * e;
                double top = start.Y + (target.Y - start.Y) * e;
                double w = start.Width + (target.Width - start.Width) * e;
                double h = start.Height + (target.Height - start.Height) * e;
                Canvas.SetLeft(Flyer, left);
                Canvas.SetTop(Flyer, top);
                Flyer.Width = w;
                Flyer.Height = h;
                // 圆角 12 → 正圆。
                Flyer.CornerRadius = new CornerRadius(12 + (Math.Min(w, h) / 2 - 12) * e);
            }
            // 浮层稍晚一点开始滑入。
            double pt = Motion.StandardEasing.Ease(Math.Clamp((clock.Elapsed.TotalMilliseconds - 80) / (FlyDuration.TotalMilliseconds - 80), 0, 1));
            Popover.Opacity = pt;
            Arrow.Opacity = pt;
            popTransform.X = slide * (1 - pt) * (reduced ? 0 : 1);
            if (t >= 1) break;
            await Task.Delay(16, CancellationToken.None);
        }
        if (token.IsCancellationRequested) return;

        // 落定后飞行头像淡出，露出立绘。
        var fade = System.Diagnostics.Stopwatch.StartNew();
        while (!token.IsCancellationRequested && Flyer.IsVisible)
        {
            double t = Math.Clamp(fade.Elapsed.TotalMilliseconds / 220, 0, 1);
            Flyer.Opacity = 1 - t;
            if (t >= 1) break;
            await Task.Delay(16, CancellationToken.None);
        }
        Flyer.IsVisible = false;
    }

    private void Close()
    {
        _animation?.Cancel();
        if (_open is not null) _open.IsSelected = false;
        _open = null;
        Flyer.IsVisible = false;
        var cts = new CancellationTokenSource();
        _animation = cts;
        _ = AnimateCloseAsync(cts.Token);
    }

    private async Task AnimateCloseAsync(CancellationToken token)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        double from = Popover.Opacity;
        while (!token.IsCancellationRequested)
        {
            double t = Math.Clamp(clock.Elapsed.TotalMilliseconds / 150, 0, 1);
            Popover.Opacity = Arrow.Opacity = from * (1 - t);
            if (t >= 1) break;
            await Task.Delay(16, CancellationToken.None);
        }
        if (token.IsCancellationRequested) return;
        DetailLayer.IsVisible = false;
        PopoverContent.Content = null;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _animation?.Cancel();
        if (_open is not null) _open.IsSelected = false;
        _open = null;
    }
}
