using CommunityToolkit.Mvvm.ComponentModel;
using WwTool.Core.Abstractions;
using WwTool.Core.Enums;
using WwTool.Core.Localization;
using WwTool.UI.Services;

namespace WwTool.UI.ViewModels;

/// <summary>
/// 设置页。当前只迁移了“界面设置”（主题外观、强调色、显示语言）；改动直接写入用户配置，
/// 由 <see cref="AppearanceCoordinator"/> 应用并由配置服务自动保存。其余设置项随设置页迁移补上。
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IConfigService _config;

    public SettingsViewModel(IConfigService config, LocalizationService localization)
    {
        _config = config;
        Presets = ThemeService.Presets.Select(p => new LocalizedOption(p, "Theme_" + p, localization)).ToArray();
        Accents = ThemeService.Accents
            .Select(a => new LocalizedOption(ThemeService.AccentToConfig(a), a == ThemeService.FollowTheme ? "Theme_Follow" : "Theme_" + a, localization))
            .ToArray();
        Languages = SupportedLanguages
            .Select(l => new LocalizedOption(l, "Enum_LanguageType_" + l, localization))
            .ToArray();

        _selectedPreset = Presets.FirstOrDefault(o => Equals(o.Value, config.User.BaseTheme)) ?? Presets[0];
        string accent = ThemeService.AccentToConfig(ThemeService.AccentFromConfig(config.User.AccentTheme));
        _selectedAccent = Accents.FirstOrDefault(o => Equals(o.Value, accent)) ?? Accents[0];
        _selectedLanguage = Languages.FirstOrDefault(o => Equals(o.Value, config.User.AppLanguage)) ?? Languages[0];
    }

    /// <summary>有语言资源文件的界面语言。</summary>
    public static IReadOnlyList<LanguageType> SupportedLanguages { get; } = [LanguageType.ZhHans, LanguageType.En, LanguageType.Ja];

    public IReadOnlyList<LocalizedOption> Presets { get; }

    /// <summary>强调色选项，值为配置格式（<c>BlueAccent</c>），与 1.2.x 的配置文件兼容。</summary>
    public IReadOnlyList<LocalizedOption> Accents { get; }

    public IReadOnlyList<LocalizedOption> Languages { get; }

    [ObservableProperty]
    private LocalizedOption _selectedPreset;

    [ObservableProperty]
    private LocalizedOption _selectedAccent;

    [ObservableProperty]
    private LocalizedOption _selectedLanguage;

    partial void OnSelectedPresetChanged(LocalizedOption value)
    {
        if (value is not null) _config.User.BaseTheme = (string)value.Value;
    }

    partial void OnSelectedAccentChanged(LocalizedOption value)
    {
        if (value is not null) _config.User.AccentTheme = (string)value.Value;
    }

    partial void OnSelectedLanguageChanged(LocalizedOption value)
    {
        if (value is not null) _config.User.AppLanguage = (LanguageType)value.Value;
    }
}
