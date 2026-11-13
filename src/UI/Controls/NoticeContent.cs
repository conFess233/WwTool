using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using WwTool.Core.Services.Notices;
using WwTool.UI.Services;

namespace WwTool.UI.Controls;

/// <summary>
/// 渲染游戏内公告正文（<see cref="NoticeHtml"/> 的块模型）：段落可选中复制，链接交给 <see cref="LinkCommand"/>，
/// 图片经 <see cref="Images"/> 加载（先缓存后校验）。颜色按语义取主题资源，任何主题下都清晰。
/// </summary>
public sealed class NoticeContent : StackPanel
{
    public static readonly StyledProperty<IReadOnlyList<NoticeBlock>?> BlocksProperty =
        AvaloniaProperty.Register<NoticeContent, IReadOnlyList<NoticeBlock>?>(nameof(Blocks));

    public static readonly StyledProperty<ICommand?> LinkCommandProperty =
        AvaloniaProperty.Register<NoticeContent, ICommand?>(nameof(LinkCommand));

    public static readonly StyledProperty<NoticeImages?> ImagesProperty =
        AvaloniaProperty.Register<NoticeContent, NoticeImages?>(nameof(Images));

    private CancellationTokenSource? _loads;

    static NoticeContent()
    {
        BlocksProperty.Changed.AddClassHandler<NoticeContent>((c, _) => c.Rebuild());
    }

    public NoticeContent() => Spacing = 4;

    public IReadOnlyList<NoticeBlock>? Blocks
    {
        get => GetValue(BlocksProperty);
        set => SetValue(BlocksProperty, value);
    }

    public ICommand? LinkCommand
    {
        get => GetValue(LinkCommandProperty);
        set => SetValue(LinkCommandProperty, value);
    }

    public NoticeImages? Images
    {
        get => GetValue(ImagesProperty);
        set => SetValue(ImagesProperty, value);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _loads?.Cancel();
    }

    private void Rebuild()
    {
        _loads?.Cancel();
        _loads = new CancellationTokenSource();
        Children.Clear();
        foreach (NoticeBlock block in Blocks ?? []) Children.Add(Build(block, _loads.Token));
    }

    private Control Build(NoticeBlock block, CancellationToken token) => block switch
    {
        NoticeParagraph p => Paragraph(p),
        NoticeSpacer => new Border { Height = 8 },
        NoticeImage image => Image(image, token),
        NoticeTable table => Table(table),
        NoticeDetails details => Details(details, token),
        _ => new Border(),
    };

    private Control Paragraph(NoticeParagraph p, double fontSize = 13.5)
    {
        bool hasLink = p.Runs.Any(r => r.Link is not null);
        // 有链接的段落用普通 TextBlock（链接是内嵌的可点文字）；其他段落可选中复制。
        TextBlock text = hasLink ? new TextBlock() : new SelectableTextBlock();
        text.TextWrapping = TextWrapping.Wrap;
        text.FontSize = fontSize;
        text.LineHeight = fontSize * 1.65;
        text.TextAlignment = p.Align switch
        {
            NoticeAlign.Center => TextAlignment.Center,
            NoticeAlign.Right => TextAlignment.Right,
            _ => TextAlignment.Left,
        };
        text.Inlines ??= [];
        foreach (NoticeRun run in p.Runs)
        {
            if (run.Link is { } link)
            {
                var anchor = new TextBlock
                {
                    Text = run.Text,
                    FontSize = fontSize,
                    TextDecorations = TextDecorations.Underline,
                    Cursor = new Cursor(StandardCursorType.Hand),
                };
                anchor.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("AccentTextBrush"));
                ToolTip.SetTip(anchor, link);
                anchor.PointerReleased += (_, e) =>
                {
                    if (e.InitialPressMouseButton != MouseButton.Left) return;
                    if (LinkCommand?.CanExecute(link) == true) LinkCommand.Execute(link);
                };
                text.Inlines.Add(new InlineUIContainer(anchor) { BaselineAlignment = BaselineAlignment.TextBottom });
                continue;
            }
            text.Inlines.Add(Run(run));
        }
        if (p.Bullet is null) return text;
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 6, Margin = new Thickness(6, 0, 0, 0) };
        var bullet = new TextBlock { Text = p.Bullet, FontSize = fontSize, LineHeight = fontSize * 1.65, MinWidth = 12 };
        bullet.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("TextMutedBrush"));
        grid.Children.Add(bullet);
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        return grid;
    }

    private Run Run(NoticeRun r)
    {
        var run = new Run(r.Text)
        {
            FontWeight = r.Bold ? FontWeight.Bold : FontWeight.Normal,
            FontStyle = r.Italic ? FontStyle.Italic : FontStyle.Normal,
        };
        if (r.Underline || r.Strike)
        {
            var decorations = new TextDecorationCollection();
            if (r.Underline) decorations.AddRange(TextDecorations.Underline);
            if (r.Strike) decorations.AddRange(TextDecorations.Strikethrough);
            run.TextDecorations = decorations;
        }
        string? brush = r.Tone switch
        {
            NoticeTone.Accent => "AccentTextBrush",
            NoticeTone.Danger => "DangerBrush",
            NoticeTone.Muted => "TextMutedBrush",
            _ => null,
        };
        if (brush is not null) run.Bind(TextElement.ForegroundProperty, this.GetResourceObservable(brush));
        return run;
    }

    private Control Image(NoticeImage block, CancellationToken token)
    {
        var image = new Image
        {
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            HorizontalAlignment = HorizontalAlignment.Left,
            MaxWidth = block.Width ?? double.PositiveInfinity,
        };
        var host = new Border { CornerRadius = new CornerRadius(8), ClipToBounds = true, Margin = new Thickness(0, 4), Child = image, MinHeight = 24 };
        host.Bind(Border.BackgroundProperty, this.GetResourceObservable("SurfaceSunkenBrush"));
        if (Images is { } images)
        {
            _ = images.LoadAsync(block.Url, 880, bitmap =>
            {
                image.Source = bitmap;
                host.MinHeight = 0;
                host.Background = Brushes.Transparent;
            }, token: token);
        }
        return host;
    }

    private Control Table(NoticeTable table)
    {
        int columns = table.Rows.Max(r => r.Count);
        var grid = new Grid();
        for (int c = 0; c < columns; c++) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (int r = 0; r < table.Rows.Count; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            IReadOnlyList<IReadOnlyList<NoticeParagraph>> row = table.Rows[r];
            for (int c = 0; c < row.Count; c++)
            {
                var stack = new StackPanel { Spacing = 2 };
                foreach (NoticeParagraph p in row[c]) stack.Children.Add(Paragraph(p, 12.5));
                var cell = new Border
                {
                    Padding = new Thickness(8, 5),
                    BorderThickness = new Thickness(c == 0 ? 1 : 0, r == 0 ? 1 : 0, 1, 1),
                    Child = stack,
                };
                cell.Bind(Border.BorderBrushProperty, this.GetResourceObservable("BorderBrush"));
                if (r == 0) cell.Bind(Border.BackgroundProperty, this.GetResourceObservable("SurfaceMutedBrush"));
                Grid.SetRow(cell, r);
                Grid.SetColumn(cell, c);
                // 不足列数的行，最后一格占满剩余列。
                if (c == row.Count - 1 && row.Count < columns) Grid.SetColumnSpan(cell, columns - c);
                grid.Children.Add(cell);
            }
        }
        return new Border { Margin = new Thickness(0, 4), Child = grid };
    }

    private Control Details(NoticeDetails details, CancellationToken token)
    {
        var body = new NoticeContent { Images = Images, LinkCommand = LinkCommand, Margin = new Thickness(14, 4, 0, 4), IsVisible = false };
        body.Blocks = details.Body;
        var chevron = new StrokeIcon { Width = 14, Height = 14, Data = this.FindResource("Icon.ChevronDown") as Geometry, RenderTransformOrigin = RelativePoint.Center };
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        header.Children.Add(chevron);
        header.Children.Add(new TextBlock
        {
            Text = string.Concat(details.Summary.Select(r => r.Text)),
            FontWeight = FontWeight.SemiBold,
            FontSize = 13.5,
            TextWrapping = TextWrapping.Wrap,
        });
        var toggle = new ToggleButton
        {
            Content = header,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0, 4),
            Cursor = new Cursor(StandardCursorType.Hand),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
        };
        chevron.RenderTransform = new RotateTransform(-90);
        toggle.IsCheckedChanged += (_, _) =>
        {
            body.IsVisible = toggle.IsChecked == true;
            chevron.RenderTransform = new RotateTransform(toggle.IsChecked == true ? 0 : -90);
        };
        var stack = new StackPanel();
        stack.Children.Add(toggle);
        stack.Children.Add(body);
        return stack;
    }
}
