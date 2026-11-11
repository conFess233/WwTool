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

    /// <summary>
    /// 用户配置里的强调色值（与 1.2.x 共用的格式：<c>BlueAccent</c>、<c>FollowTheme</c>）转换为强调色名。
    /// 无法识别时返回 <see cref="FollowTheme"/>。
    /// </summary>
    public static string AccentFromConfig(string? configValue)
    {
        string name = configValue is not null && configValue.EndsWith("Accent", StringComparison.Ordinal)
            ? configValue[..^"Accent".Length]
            : configValue ?? FollowTheme;
        return Accents.Contains(name) ? name : FollowTheme;
    }

    /// <summary>强调色名转换为用户配置里保存的值，见 <see cref="AccentFromConfig"/>。</summary>
    public static string AccentToConfig(string accent) => accent == FollowTheme ? FollowTheme : accent + "Accent";

    private static readonly HashSet<string> DarkPresets = ["DeepBlue", "BlackGold"];

    /// <summary>每套预设都必须提供的键；缺失时视为资源损坏，拒绝应用以免出现回退色。</summary>
    private static readonly string[] RequiredPresetKeys =
    [
        "BackgroundColor", "SurfaceColor", "SurfaceRaisedColor", "SurfaceSunkenColor", "BorderColor", "DividerColor",
        "BorderStrongColor", "InputBorderColor", "InputBackgroundColor", "TrackColor", "TextPrimaryColor", "TextSecondaryColor",
        "LabelColor", "TextMutedColor", "NavIslandColor", "NavIslandBorderColor", "AccentColor", "TextOnAccentColor",
        "AccentTextColor", "AccentContainerColor", "AccentLineColor", "HighlightColor", "HighlightStrongColor",
        "DangerColor", "DangerContainerColor", "DangerLineColor", "SuccessColor", "SuccessContainerColor",
        "FourStarColor", "FourStarContainerColor", "InfoColor", "InfoContainerColor", "ScrimColor", "GridLineColor",
        "ShadowIsland", "ShadowFab", "ShadowMenu", "ShadowDialog", "ShadowHover",
        "CornerRadiusControl", "CornerRadiusCard", "CornerRadiusInner", "CornerRadiusDialog"
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
            ApplyAccent(merged, Load($"Accents/{accentName}.axaml"), DarkPresets.Contains(presetName));
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

        // Fluent 内置控件（开关、滑块、输入框焦点等）用系统强调色；指向当前主题强调色，避免出现系统蓝。
        if (merged.TryGetValue("AccentColor", out object? accentValue) && accentValue is Color accentColor)
        {
            foreach (string key in new[] { "SystemAccentColor", "SystemAccentColorDark1", "SystemAccentColorDark2", "SystemAccentColorDark3",
                         "SystemAccentColorLight1", "SystemAccentColorLight2", "SystemAccentColorLight3" })
            {
                _live[key] = accentColor;
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

    /// <summary>读取某个预设或强调色文件里的颜色（设置页色板预览用），不影响当前主题。</summary>
    /// <param name="relative">如 <c>Presets/WarmWhite.axaml</c>、<c>Accents/Blue.axaml</c>。</param>
    public static Color? PeekColor(string relative, string key)
    {
        try
        {
            return Load(relative).TryGetValue(key, out object? value) && value is Color color ? color : null;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or XamlLoadException)
        {
            return null;
        }
    }

    /// <summary>
    /// 强调色联动（开发文档「强调色联动」）：填充与填充上文字固定，文字与淡底按主题深浅取值；
    /// 描边、悬停、高亮底、底纹光晕等由填充与淡底派生，危险、成功、四星紫不变。
    /// </summary>
    private static void ApplyAccent(Dictionary<object, object?> merged, ResourceDictionary accent, bool dark)
    {
        Color Get(string key) => accent.TryGetValue(key, out object? v) && v is Color c
            ? c : throw new InvalidDataException($"强调色资源缺少 {key}");
        Color fill = Get("AccentColor");
        Color soft = Get(dark ? "AccentContainerDarkColor" : "AccentContainerLightColor");
        Color surface = merged.TryGetValue("SurfaceColor", out object? s) && s is Color sc ? sc : Colors.White;
        merged["AccentColor"] = fill;
        merged["TextOnAccentColor"] = Get("TextOnAccentColor");
        merged["AccentTextColor"] = Get(dark ? "AccentTextDarkColor" : "AccentTextLightColor");
        merged["AccentContainerColor"] = soft;
        merged["AccentLineColor"] = Mix(soft, fill, 0.45);
        merged["AccentHoverColor"] = Mix(fill, Colors.White, 0.1);
        merged["AccentPressedColor"] = Mix(fill, Colors.Black, 0.1);
        merged["AccentContainerHoverColor"] = Mix(soft, fill, 0.2);
        merged["FocusRingColor"] = fill;
        merged["ChartPrimaryColor"] = fill;
        merged["GlowColor"] = fill;
        merged["HighlightColor"] = Mix(surface, soft, 0.5);
        merged["HighlightStrongColor"] = Mix(surface, soft, 0.35);
    }

    /// <summary>按比例混合两种颜色（0 为 a，1 为 b）。</summary>
    public static Color Mix(Color a, Color b, double t) => Color.FromArgb(
        (byte)Math.Round(a.A + (b.A - a.A) * t), (byte)Math.Round(a.R + (b.R - a.R) * t),
        (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t));

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
