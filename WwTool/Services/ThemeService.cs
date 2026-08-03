using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media.Animation;
using WwTool.Common.Enums;
using WwTool.Common.Utils;
using WwTool.Services.Interfaces;

namespace WwTool.Services;

/// <summary>
/// Applies validated theme presets, optional accent overrides, material opacity and motion preferences.
/// </summary>
public sealed class ThemeService : IThemeService
{
    private const string FollowTheme = "FollowTheme";
    private static readonly HashSet<string> Presets = ["WarmWhite", "DeepBlue", "SoftGreen", "SoftPink", "BlackGold"];
    private static readonly HashSet<string> Accents = [FollowTheme, "BlueAccent", "GreenAccent", "PurpleAccent", "RedAccent", "OrangeAccent", "YellowAccent"];
    private static readonly string[] RequiredPresetKeys =
    [
        "BackgroundColor", "SurfaceColor", "SurfaceElevatedColor", "SurfaceMutedColor", "SurfaceHoverColor",
        "BorderColor", "BorderStrongColor", "DividerColor", "TextPrimaryColor", "TextSecondaryColor", "TextMutedColor",
        "TextOnAccentColor", "AccentColor", "AccentHoverColor", "AccentPressedColor", "AccentContainerColor",
        "AccentContainerHoverColor", "DangerColor", "WarningColor", "SuccessColor", "InfoColor", "FocusRingColor",
        "ChartPrimaryColor", "ChartSecondaryColor", "ChartTertiaryColor", "ChartFourStarColor", "ChartGridColor",
        "CornerRadiusSmall", "CornerRadiusControl", "CornerRadiusCard", "CornerRadiusDialog",
        "BackgroundTextureColor", "BackgroundTextureOpacity", "AtmosphereColor", "AtmosphereOpacity"
    ];
    private static readonly string[] RequiredAccentKeys =
    [
        "AccentColor", "AccentHoverColor", "AccentPressedColor", "AccentContainerColor",
        "AccentContainerHoverColor", "FocusRingColor", "TextOnAccentColor", "ChartPrimaryColor"
    ];

    private readonly IConfigService _configService;
    private readonly ILoggerService _logger;
    private readonly IUIStateService _uiStateService;

    public ThemeService(IConfigService configService, ILoggerService logger, IUIStateService uiStateService)
    {
        _configService = configService;
        _logger = logger;
        _uiStateService = uiStateService;
    }

    public void Initialize()
    {
        ApplyTheme(_configService.User.BaseTheme, _configService.User.AccentTheme);
        ApplyMaterialPreference();
        ApplyMotionPreference(_configService.User.IsReducedMotionEnabled);
        _configService.User.PropertyChanged += OnUserConfigChanged;
        _configService.UserAutoSaveFailed += OnUserAutoSaveFailed;
        SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;
    }

    private void OnUserAutoSaveFailed(object? sender, EventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() => _uiStateService.ShowToast(
            LanguageManager.Instance["Toast_Error"],
            LanguageManager.Instance["Settings_AutoSaveFailed"],
            NotificationType.Error));
    }

    private void OnUserConfigChanged(object? sender, PropertyChangedEventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (e.PropertyName is nameof(_configService.User.BaseTheme) or nameof(_configService.User.AccentTheme))
            {
                ApplyTheme(_configService.User.BaseTheme, _configService.User.AccentTheme);
            }
            else if (e.PropertyName is nameof(_configService.User.IsGlassEffectEnabled) or nameof(_configService.User.GlassOpacity))
            {
                ApplyMaterialPreference();
            }
            else if (e.PropertyName == nameof(_configService.User.IsReducedMotionEnabled))
            {
                ApplyMotionPreference(_configService.User.IsReducedMotionEnabled);
            }
        });
    }

    private void OnSystemParametersChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.HighContrast))
        {
            Application.Current.Dispatcher.Invoke(ApplyMaterialPreference);
        }
    }

    private void ApplyTheme(string presetName, string accentName)
    {
        presetName = Presets.Contains(presetName) ? presetName : "DeepBlue";
        accentName = Accents.Contains(accentName) ? accentName : FollowTheme;

        try
        {
            var preset = LoadDictionary($"/UI/Resources/Themes/Presets/{presetName}.xaml");
            ValidatePreset(preset, presetName);
            ResourceDictionary? accent = accentName == FollowTheme
                ? null
                : LoadDictionary($"/UI/Resources/Themes/AccentOverrides/{accentName}.xaml");
            if (accent is not null)
            {
                ValidateDictionary(accent, accentName, RequiredAccentKeys);
            }
            var brushes = LoadDictionary("/UI/Resources/Themes/ThemeBrushes.xaml");

            var dictionaries = Application.Current.Resources.MergedDictionaries;
            int insertionIndex = FindThemeInsertionIndex(dictionaries);
            var obsolete = dictionaries.Where(IsThemeRuntimeDictionary).ToList();
            foreach (var dictionary in obsolete)
            {
                dictionaries.Remove(dictionary);
            }

            insertionIndex = Math.Clamp(insertionIndex, 0, dictionaries.Count);
            dictionaries.Insert(insertionIndex, preset);
            if (accent is not null)
            {
                dictionaries.Insert(++insertionIndex, accent);
            }
            dictionaries.Insert(++insertionIndex, brushes);
        }
        catch (Exception ex)
        {
            _logger.Error($"应用主题失败（主题: {presetName}, 强调色: {accentName}）", ex);
            _uiStateService.ShowToast(
                LanguageManager.Instance["Toast_Error"],
                LanguageManager.Instance["Theme_ApplyFailed"],
                NotificationType.Error);
        }
    }

    private static ResourceDictionary LoadDictionary(string relativeUri) => new()
    {
        Source = new Uri(relativeUri, UriKind.Relative)
    };

    private static void ValidatePreset(ResourceDictionary preset, string presetName)
    {
        ValidateDictionary(preset, presetName, RequiredPresetKeys);
    }

    private static void ValidateDictionary(ResourceDictionary dictionary, string name, IEnumerable<string> requiredKeys)
    {
        string[] missingKeys = requiredKeys.Where(key => !dictionary.Contains(key)).ToArray();
        if (missingKeys.Length > 0)
        {
            throw new InvalidDataException($"Theme resource '{name}' is missing: {string.Join(", ", missingKeys)}");
        }
    }

    private static int FindThemeInsertionIndex(IList<ResourceDictionary> dictionaries)
    {
        for (int index = 0; index < dictionaries.Count; index++)
        {
            if (IsThemeRuntimeDictionary(dictionaries[index])) return index;
        }
        return Math.Min(2, dictionaries.Count);
    }

    private static bool IsThemeRuntimeDictionary(ResourceDictionary dictionary)
    {
        string source = dictionary.Source?.OriginalString ?? string.Empty;
        return source.Contains("/Themes/Presets/", StringComparison.Ordinal)
            || source.Contains("/Themes/AccentOverrides/", StringComparison.Ordinal)
            || source.Contains("/Themes/Colors/", StringComparison.Ordinal)
            || source.Contains("/Themes/Accents/", StringComparison.Ordinal)
            || source.Contains("/Themes/ThemeBrushes.xaml", StringComparison.Ordinal);
    }

    private void ApplyMaterialPreference()
    {
        bool useGlass = _configService.User.IsGlassEffectEnabled && !SystemParameters.HighContrast;
        double opacity = useGlass ? Math.Clamp(_configService.User.GlassOpacity, 65, 95) / 100d : 1d;
        Application.Current.Resources["MaterialOpacity"] = opacity;
        Application.Current.Resources["ElevatedMaterialOpacity"] = useGlass ? Math.Min(1d, opacity + 0.1d) : 1d;

        if (SystemParameters.HighContrast)
        {
            Application.Current.Resources["BackgroundTextureOpacity"] = 0d;
            Application.Current.Resources["AtmosphereOpacity"] = 0d;
        }
        else
        {
            Application.Current.Resources.Remove("BackgroundTextureOpacity");
            Application.Current.Resources.Remove("AtmosphereOpacity");
        }
    }

    private static void ApplyMotionPreference(bool isReducedMotionEnabled)
    {
        Application.Current.Resources["MotionInstant"] = new Duration(TimeSpan.FromMilliseconds(80));
        Application.Current.Resources["MotionFast"] = new Duration(TimeSpan.FromMilliseconds(isReducedMotionEnabled ? 80 : 150));
        Application.Current.Resources["MotionNormal"] = new Duration(TimeSpan.FromMilliseconds(isReducedMotionEnabled ? 80 : 220));
        Application.Current.Resources["MotionPage"] = new Duration(TimeSpan.FromMilliseconds(isReducedMotionEnabled ? 100 : 280));
    }
}
