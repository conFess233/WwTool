using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using WwTool.Core.Enums;
using WwTool.Core.Localization;
using WwTool.UI.Services;

namespace WwTool.UI.Shell;

/// <summary>
/// 阶段 0 原型：主题令牌与语言切换预览。正式页面完成后删除。
/// </summary>
public partial class Stage0PreviewView : UserControl
{
    private static readonly string[] SwatchKeys =
    [
        "Background", "Surface", "SurfaceElevated", "Border", "TextPrimary", "TextSecondary", "TextMuted",
        "Accent", "AccentContainer", "Danger", "Success", "Info", "SegmentTrack", "NavIsland"
    ];

    private readonly ThemeService _theme;

    /// <summary>仅供 Avalonia 设计器使用。</summary>
    public Stage0PreviewView()
    {
        _theme = new ThemeService();
        InitializeComponent();
    }

    public Stage0PreviewView(ThemeService theme, LocalizationService localization)
    {
        _theme = theme;
        InitializeComponent();

        foreach (LanguageType language in Enum.GetValues<LanguageType>())
        {
            LanguageButtons.Children.Add(MakeButton($"lang-{language}", language.ToString(), () => localization.ChangeLanguage(language)));
        }

        foreach (string preset in ThemeService.Presets)
        {
            PresetButtons.Children.Add(MakeButton($"preset-{preset}", preset, () => _theme.Apply(preset, _theme.CurrentAccent)));
        }
        foreach (string accent in ThemeService.Accents)
        {
            AccentButtons.Children.Add(MakeButton($"accent-{accent}", accent, () => _theme.Apply(_theme.CurrentPreset, accent)));
        }
        foreach (string key in SwatchKeys)
        {
            var swatch = new Border { Width = 40, Height = 40, CornerRadius = new(9), BorderThickness = new(1) };
            swatch.Bind(Border.BackgroundProperty, this.GetResourceObservable(key + "Brush"));
            swatch.Bind(Border.BorderBrushProperty, this.GetResourceObservable("BorderBrush"));
            var label = new TextBlock { Text = key, FontSize = 12 };
            label.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("TextMutedBrush"));
            Swatches.Children.Add(new StackPanel { Spacing = 4, Width = 110, HorizontalAlignment = HorizontalAlignment.Left, Children = { swatch, label } });
        }
    }

    private static Button MakeButton(string automationId, string text, Action onClick)
    {
        var button = new Button { Content = text };
        AutomationProperties.SetAutomationId(button, automationId);
        button.Click += (_, _) => onClick();
        return button;
    }
}
