using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;

namespace WwTool.UI.Services;

/// <summary>
/// 主题服务：明暗预设与强调色是两个独立维度（窗口材质另由窗口外观服务负责）。
/// <para>
/// 实现方式：Application.Resources 中常驻一个“实时主题字典”。切换时把“预设 + 强调色覆盖”合并后写入该字典，
/// 并为每个 <c>XxxColor</c> 维护同名的 <c>XxxBrush</c>。画刷实例只创建一次、切换时改它的 Color，
/// 所以 DynamicResource 与已持有画刷的控件都会立即更新，不会残留旧颜色。
/// </para>
/// </summary>
public sealed class ThemeService
{
    /// <summary>强调色“跟随主题”时使用的名称。</summary>
    public const string FollowTheme = "FollowTheme";

    public static readonly IReadOnlyList<string> Presets = ["WarmWhite", "DeepBlue", "SoftGreen", "SoftPink", "BlackGold"];
    public static readonly IReadOnlyList<string> Accents = [FollowTheme, "Blue", "Green", "Orange", "Purple", "Red", "Yellow"];

    private static readonly HashSet<string> DarkPresets = ["DeepBlue", "BlackGold"];

    /// <summary>每套预设都必须提供的键；缺失时视为资源损坏，拒绝应用以免出现回退色。</summary>
    private static readonly string[] RequiredPresetKeys =
    [
        "BackgroundColor", "SurfaceColor", "SurfaceElevatedColor", "SurfaceMutedColor", "SurfaceHoverColor",
        "BorderColor", "BorderStrongColor", "DividerColor", "TextPrimaryColor", "TextSecondaryColor", "TextMutedColor",
        "TextOnAccentColor", "AccentColor", "AccentHoverColor", "AccentPressedColor", "AccentContainerColor",
        "AccentContainerHoverColor", "DangerColor", "WarningColor", "SuccessColor", "InfoColor", "FocusRingColor",
        "ChartPrimaryColor", "ChartSecondaryColor", "ChartTertiaryColor", "ChartFourStarColor", "ChartGridColor",
        "CornerRadiusSmall", "CornerRadiusControl", "CornerRadiusCard", "CornerRadiusDialog",
        "SegmentTrackColor", "NavIslandColor", "TextOnNavIslandColor"
    ];

    private readonly ResourceDictionary _live = new();
    private readonly Dictionary<string, SolidColorBrush> _brushes = new(StringComparer.Ordinal);
    private bool _attached;

    public string CurrentPreset { get; private set; } = Presets[0];
    public string CurrentAccent { get; private set; } = FollowTheme;

    /// <summary>主题应用完成后触发（在 UI 线程）。</summary>
    public event EventHandler? ThemeChanged;

    /// <summary>
    /// 应用主题。名称无效时回退到默认值；资源缺键时抛出 <see cref="InvalidDataException"/> 并保持当前主题不变。
    /// 必须在 UI 线程调用。
    /// </summary>
    public void Apply(string? preset, string? accent)
    {
        string presetName = Presets.Contains(preset ?? string.Empty) ? preset! : Presets[0];
        string accentName = Accents.Contains(accent ?? string.Empty) ? accent! : FollowTheme;

        var merged = new Dictionary<object, object?>();
        Merge(merged, Load($"Presets/{presetName}.axaml"));
        string[] missing = RequiredPresetKeys.Where(k => !merged.ContainsKey(k)).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidDataException($"主题预设 {presetName} 缺少资源：{string.Join(", ", missing)}");
        }
        if (accentName != FollowTheme)
        {
            Merge(merged, Load($"Accents/{accentName}.axaml"));
        }

        AttachOnce();
        foreach (var (key, value) in merged)
        {
            _live[key] = value;
            if (key is string name && value is Color color && name.EndsWith("Color", StringComparison.Ordinal))
            {
                string brushKey = name[..^"Color".Length] + "Brush";
                if (_brushes.TryGetValue(brushKey, out var brush))
                {
                    brush.Color = color;
                }
                else
                {
                    brush = new SolidColorBrush(color);
                    _brushes[brushKey] = brush;
                    _live[brushKey] = brush;
                }
            }
        }

        if (Application.Current is { } app)
        {
            // 让 Fluent 内置控件（滚动条、输入框等）的明暗与预设一致。
            app.RequestedThemeVariant = DarkPresets.Contains(presetName) ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        CurrentPreset = presetName;
        CurrentAccent = accentName;
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AttachOnce()
    {
        if (_attached || Application.Current is null) return;
        Application.Current.Resources.MergedDictionaries.Add(_live);
        _attached = true;
    }

    private static ResourceDictionary Load(string relative) =>
        AvaloniaXamlLoader.Load(new Uri($"avares://WwTool/Theming/{relative}")) as ResourceDictionary
        ?? throw new InvalidDataException($"主题资源 {relative} 不是 ResourceDictionary。");

    private static void Merge(Dictionary<object, object?> target, ResourceDictionary source)
    {
        foreach (var (key, value) in source)
        {
            target[key] = value;
        }
    }
}
