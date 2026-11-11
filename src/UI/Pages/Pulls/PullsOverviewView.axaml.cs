using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using WwTool.UI.Controls;

namespace WwTool.UI.Pages.Pulls;

/// <summary>
/// 抽卡统计视图行为：总览滚动时筛选栏下方阴影渐显；记录行点击展开、右键菜单（行同时高亮）与导出分享图片。
/// </summary>
public partial class PullsOverviewView : UserControl
{
    public PullsOverviewView()
    {
        InitializeComponent();
        OverviewScroller.ScrollChanged += (_, _) => BarShadow.Opacity = Math.Clamp(OverviewScroller.Offset.Y / 24, 0, 1);

        // 精简视图：磁贴最小宽 96、列距 12（宿主右侧含角标留白 7，故按 103 / 5 计算），按可用宽度自适应列数（auto-fill）；底部 36 渐隐。
        CompactGrid.SizeChanged += (_, e) => UpdateCompactColumns(e.NewSize.Width);
        CompactGrid.LayoutUpdated += (_, _) => UpdateCompactColumns(CompactGrid.Bounds.Width);
        CompactScroller.SizeChanged += (_, e) => UpdateCompactFade(e.NewSize.Height);
        AddHandler(TappedEvent, OnTapped);
        AddHandler(ContextRequestedEvent, OnContextRequested);

        // 拖拽导入：拖入链接文字或日志文件。
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragOverEvent, OnDragOverPage);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private const double TileMinWidth = 96 + 7, TileGap = 5, FadeHeight = 36;

    private void UpdateCompactColumns(double width)
    {
        if (width <= 0 || CompactGrid.ItemsPanelRoot is not UniformGrid grid) return;
        int columns = Math.Max(1, (int)Math.Floor((width + TileGap) / (TileMinWidth + TileGap)));
        if (grid.Columns != columns) grid.Columns = columns;
    }

    private void UpdateCompactFade(double height)
    {
        if (height <= FadeHeight) { CompactScroller.OpacityMask = null; return; }
        double start = 1 - FadeHeight / height;
        CompactScroller.OpacityMask = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Colors.Black, 0), new GradientStop(Colors.Black, start), new GradientStop(Colors.Transparent, 1) },
        };
    }

    private static bool CanImport(DragEventArgs e) =>
        e.DataTransfer.Contains(DataFormat.Text) || e.DataTransfer.Contains(DataFormat.File);

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        e.DragEffects = CanImport(e) ? DragDropEffects.Copy : DragDropEffects.None;
        if (CanImport(e)) ViewModel?.DragOver(true);
    }

    private void OnDragOverPage(object? sender, DragEventArgs e) =>
        e.DragEffects = CanImport(e) ? DragDropEffects.Copy : DragDropEffects.None;

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        // 移到子元素上也会触发离开；只在指针真正离开本页时收起。
        Point p = e.GetPosition(this);
        if (p.X <= 0 || p.Y <= 0 || p.X >= Bounds.Width || p.Y >= Bounds.Height) ViewModel?.DragOver(false);
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (ViewModel is not { } vm) return;
        string? file = e.DataTransfer.TryGetFile()?.TryGetLocalPath();
        string? text = file is null ? e.DataTransfer.TryGetText() : null;
        if (file is null && string.IsNullOrWhiteSpace(text))
        {
            vm.DragOver(false);
            return;
        }
        await vm.DropAsync(text, file);
    }

    private PullsOverviewViewModel? ViewModel => DataContext as PullsOverviewViewModel;

    /// <summary>从事件源向上找到记录行。</summary>
    private static (Control Row, FiveStarCard Card)? FindRow(object? source)
    {
        for (Visual? v = source as Visual; v is not null; v = v.GetVisualParent())
        {
            if (v is Border { DataContext: FiveStarCard card } row && row.Classes.Contains("rec-row")) return (row, card);
        }
        return null;
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        if (FindRow(e.Source) is { Card: var card })
        {
            card.IsExpanded = !card.IsExpanded;
            e.Handled = true;
        }
    }

    private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (FindRow(e.Source) is not { } hit || ViewModel is not { } vm) return;
        (Control row, FiveStarCard card) = hit;

        var export = MenuItem(vm.Text("Pulls_CtxExport"), "Icon.Image", null, null, null);
        export.Click += async (_, _) => await ExportAsync(row, card, vm);
        var menu = new ContextMenu
        {
            ItemsSource = new Control[]
            {
                MenuItem(vm.Text("Pulls_CtxCopy"), "Icon.Copy", vm.CopyRecordCommand, card, new KeyGesture(Key.C, KeyModifiers.Control)),
                MenuItem(vm.Text("Pulls_CtxViewRole"), "Icon.User", vm.ViewRoleCommand, card, null),
                MenuItem(vm.Text("Pulls_CtxLocate"), "Icon.Chart", vm.LocateInChartsCommand, card, null),
                new Separator(),
                export,
            },
        };
        card.IsHighlighted = true;
        menu.Closed += (_, _) => card.IsHighlighted = false;
        menu.Open(row);
        e.Handled = true;
    }

    private static MenuItem MenuItem(string header, string icon, ICommand? command, object? parameter, KeyGesture? gesture)
    {
        var item = new MenuItem
        {
            Header = header,
            Command = command,
            CommandParameter = parameter,
            InputGesture = gesture,
        };
        if (Application.Current?.TryGetResource(icon, null, out object? geometry) == true && geometry is Avalonia.Media.Geometry g)
        {
            item.Icon = new StrokeIcon { Data = g };
        }
        return item;
    }

    /// <summary>把记录行渲染成 2 倍图并另存为 PNG。</summary>
    private async Task ExportAsync(Control row, FiveStarCard card, PullsOverviewViewModel vm)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return;
        IStorageFile? file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = vm.Text("Pulls_ExportTitle"),
            SuggestedFileName = $"{card.Name}-{card.Pity}.png",
            DefaultExtension = "png",
            FileTypeChoices = [FilePickerFileTypes.ImagePng],
        });
        if (file is null) return;

        card.IsHighlighted = true;
        try
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { }, Avalonia.Threading.DispatcherPriority.Render);
            var size = new PixelSize((int)Math.Ceiling(row.Bounds.Width * 2), (int)Math.Ceiling(row.Bounds.Height * 2));
            using var bitmap = new RenderTargetBitmap(size, new Vector(192, 192));
            bitmap.Render(row);
            await using Stream stream = await file.OpenWriteAsync();
            bitmap.Save(stream);
            vm.ReportExported(file.Name);
        }
        catch (Exception ex)
        {
            vm.ReportExportFailed(ex);
        }
        finally
        {
            card.IsHighlighted = false;
        }
    }
}
