using System.ComponentModel;
using Avalonia.Threading;
using WwTool.Core.Abstractions;
using WwTool.Core.Localization;
using WwTool.Core.Models.Config;
using WwTool.UI.Theming;

namespace WwTool.UI.Services;

/// <summary>
/// 让主题与界面语言始终跟随用户配置：启动时应用一次，之后配置项变化即应用。
/// 设置页只改配置，不直接调用主题或语言服务。
/// </summary>
public sealed class AppearanceCoordinator
{
    private readonly IConfigService _config;
    private readonly ThemeService _theme;
    private readonly LocalizationService _localization;
    private readonly ILoggerService _logger;

    public AppearanceCoordinator(IConfigService config, ThemeService theme, LocalizationService localization, ILoggerService logger)
    {
        _config = config;
        _theme = theme;
        _localization = localization;
        _logger = logger;
    }

    /// <summary>主题资源加载失败（已保留当前外观）时触发，供界面提示。</summary>
    public event EventHandler? ThemeApplyFailed;

    /// <summary>按当前配置应用语言与主题，并开始跟随配置变化。必须在 UI 线程调用，且只调用一次。</summary>
    public void Start()
    {
        _localization.ChangeLanguage(_config.User.AppLanguage);
        ApplyTheme();
        Motion.Reduced = _config.User.IsReducedMotionEnabled;
        _config.User.PropertyChanged += OnUserConfigChanged;
    }

    private void OnUserConfigChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(UserConfig.BaseTheme):
            case nameof(UserConfig.AccentTheme):
                Dispatcher.UIThread.Post(ApplyTheme);
                break;
            case nameof(UserConfig.IsReducedMotionEnabled):
                Dispatcher.UIThread.Post(() => Motion.Reduced = _config.User.IsReducedMotionEnabled);
                break;
            case nameof(UserConfig.AppLanguage):
                Dispatcher.UIThread.Post(() => _localization.ChangeLanguage(_config.User.AppLanguage));
                break;
        }
    }

    private void ApplyTheme()
    {
        try
        {
            _theme.Apply(_config.User.BaseTheme, ThemeService.AccentFromConfig(_config.User.AccentTheme));
        }
        catch (InvalidDataException ex)
        {
            _logger.Error("主题资源加载失败，已保留当前外观", ex);
            ThemeApplyFailed?.Invoke(this, EventArgs.Empty);
        }
    }
}
