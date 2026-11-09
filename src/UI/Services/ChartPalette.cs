using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using SkiaSharp;

namespace WwTool.UI.Services;

/// <summary>
/// 图表配色：从当前主题资源读取（规范 02 §8），主题切换后需重新生成图表。
/// </summary>
public sealed record ChartPalette(SKColor Primary, SKColor Secondary, SKColor Tertiary, SKColor FourStar, SKColor Grid, SKColor Text, SKColor Danger)
{
    /// <summary>卡池序列色（角色活动、武器活动、角色常驻、武器常驻、其他）。</summary>
    public IReadOnlyList<SKColor> Series => [Primary, Secondary, Tertiary, FourStar, Grid.WithAlpha(0xFF)];

    public static ChartPalette FromResources()
    {
        return new ChartPalette(
            Get("ChartPrimaryColor"), Get("ChartSecondaryColor"), Get("ChartTertiaryColor"), Get("ChartFourStarColor"),
            Get("ChartGridColor"), Get("TextMutedColor"), Get("DangerColor"));
    }

    private static SKColor Get(string key)
    {
        Application? app = Application.Current;
        if (app is not null && app.TryGetResource(key, app.ActualThemeVariant ?? ThemeVariant.Default, out object? value) && value is Color c)
        {
            return new SKColor(c.R, c.G, c.B, c.A);
        }
        return SKColors.Gray;
    }

    /// <summary>
    /// 全局字体：选一个能显示中文和日文的系统字体，否则图表中的中日文标签会显示为方块。进程内只配置一次。
    /// </summary>
    public static void ConfigureFonts()
    {
        SKTypeface typeface = SKFontManager.Default.MatchCharacter('统') ?? SKTypeface.Default;
        LiveCharts.Configure(settings => settings.HasGlobalSKTypeface(typeface));
    }
}
