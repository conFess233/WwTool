using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace WwTool.UI.Controls;

/// <summary>
/// 空状态与异常（定稿「空状态与异常」）：居中布局，76 圆形图标底（Accent Soft；<see cref="IsDanger"/> 时 Danger Soft）、
/// 标题 17 Bold、说明 13（最宽 300）、按钮组（<see cref="ContentControl.Content"/>）。
/// 设置 <see cref="Graphic"/> 时用它代替圆形图标（如软件 Logo、加载形象）。外观见 Controls.axaml。
/// </summary>
public sealed class EmptyState : ContentControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(Description));

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<EmptyState, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<object?> GraphicProperty =
        AvaloniaProperty.Register<EmptyState, object?>(nameof(Graphic));

    public static readonly StyledProperty<bool> IsDangerProperty =
        AvaloniaProperty.Register<EmptyState, bool>(nameof(IsDanger));

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public object? Graphic { get => GetValue(GraphicProperty); set => SetValue(GraphicProperty, value); }
    public bool IsDanger { get => GetValue(IsDangerProperty); set => SetValue(IsDangerProperty, value); }

    static EmptyState()
    {
        IsDangerProperty.Changed.AddClassHandler<EmptyState>((s, e) => s.PseudoClasses.Set(":danger", (bool)e.NewValue!));
    }
}
