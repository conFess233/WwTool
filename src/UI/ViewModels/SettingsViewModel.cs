using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WwTool.Core.Abstractions;
using WwTool.Core.Abstractions.Platform;
using WwTool.Core.Enums;
using WwTool.Core.Exceptions;
using WwTool.Core.Localization;
using WwTool.Core.Services;
using WwTool.UI.Feedback;
using WwTool.UI.Navigation;
using WwTool.UI.Services;
using WwTool.UI.Shell;

namespace WwTool.UI.ViewModels;

/// <summary>主题预设 / 强调色色板上的一格。</summary>
public sealed partial class ThemeSwatch(LocalizedOption option, IBrush background, IBrush surface, IBrush accent) : ObservableObject
{
    public LocalizedOption Option { get; } = option;
    public IBrush Background { get; } = background;
    public IBrush Surface { get; } = surface;
    public IBrush Accent { get; } = accent;
    public string AutomationId => "swatch-" + Option.Value;

    [ObservableProperty] private bool _isSelected;
}

/// <summary>“游戏资料与图片缓存”里的一类资料。</summary>
public sealed record CatalogRow(string Name, string Version, string Status);

/// <summary>
/// 设置页：常规游戏设置、界面、运行日志、网络与验证、游戏资料与图片缓存、关于。
/// 用户配置（UserConfig）改动即自动保存；程序 / 接口配置没有变更通知，改动后延迟写盘。
/// 悬浮按钮「保存设置」立即写盘并给出结果。
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject, IFabProvider, INavigationAware
{
    private const string RepositoryUrl = "https://github.com/conFess233/WwTool";

    private readonly IConfigService _config;
    private readonly IUIStateService _uiState;
    private readonly ILoggerService _logger;
    private readonly LocalizationService _localization;
    private readonly CatalogSyncService _catalogSync;
    private readonly ICatalogImageCache _images;
    private readonly IAppLauncher _launcher;
    private readonly IAppPaths _paths;
    private readonly DispatcherTimer _saveDebounce;
    private CancellationTokenSource? _catalogCts;
    private bool _clearingCache;

    public SettingsViewModel(
        IConfigService config,
        LocalizationService localization,
        IUIStateService uiState,
        ILoggerService logger,
        AccountContext accounts,
        CatalogSyncService catalogSync,
        ICatalogImageCache images,
        IAppLauncher launcher,
        IAppPaths paths)
    {
        _config = config;
        _uiState = uiState;
        _logger = logger;
        _localization = localization;
        _catalogSync = catalogSync;
        _images = images;
        _launcher = launcher;
        _paths = paths;
        Accounts = accounts;
        FabActions =
        [
            new FabAction("save", "Action_SaveSettings", "avares://WwTool/Assets/Images/Save.png", SaveCommand, localization),
        ];

        Presets = ThemeService.Presets.Select(p =>
        {
            string file = $"Presets/{p}.axaml";
            return new ThemeSwatch(new LocalizedOption(p, "Theme_" + p, localization),
                Brush(file, "BackgroundColor"), Brush(file, "SurfaceColor"), Brush(file, "AccentColor"));
        }).ToArray();
        Accents = ThemeService.Accents.Select(a =>
        {
            var option = new LocalizedOption(ThemeService.AccentToConfig(a), a == ThemeService.FollowTheme ? "Theme_Follow" : "Theme_" + a, localization);
            IBrush accent = a == ThemeService.FollowTheme ? Brushes.Transparent : Brush($"Accents/{a}.axaml", "AccentColor");
            return new ThemeSwatch(option, Brushes.Transparent, Brushes.Transparent, accent);
        }).ToArray();
        Languages = SupportedLanguages.Select(l => new LocalizedOption(l, "Enum_LanguageType_" + l, localization)).ToArray();
        NotificationModes = Enum.GetValues<NotificationDisplayMode>()
            .Select(m => new LocalizedOption(m, "NotificationMode_" + m, localization)).ToArray();
        LogLevels = new[] { LogLevel.Debug, LogLevel.Info, LogLevel.Warn, LogLevel.Error }
            .Select(l => new LocalizedOption(l, "LogLevel_" + l, localization)).ToArray();

        SelectSwatch(Presets, config.User.BaseTheme);
        SelectSwatch(Accents, ThemeService.AccentToConfig(ThemeService.AccentFromConfig(config.User.AccentTheme)));
        _selectedLanguage = Languages.FirstOrDefault(o => Equals(o.Value, config.User.AppLanguage)) ?? Languages[0];
        _selectedNotificationMode = NotificationModes.FirstOrDefault(o => Equals(o.Value, config.User.NotificationDisplayMode)) ?? NotificationModes[0];
        _selectedLogLevel = LogLevels.FirstOrDefault(o => Equals(o.Value, config.App.MinimumLogLevel)) ?? LogLevels[0];

        _saveDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
        _saveDebounce.Tick += async (_, _) =>
        {
            _saveDebounce.Stop();
            await SaveQuietlyAsync();
        };

        config.User.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Core.Models.Config.UserConfig.GamePath)) OnPropertyChanged(nameof(GamePath));
            if (e.PropertyName == nameof(Core.Models.Config.UserConfig.IsGlassEffectEnabled)) OnPropertyChanged(nameof(IsGlassEnabled));
        };
        _catalogSync.Changed += (_, _) => Dispatcher.UIThread.Post(RefreshCatalog);
        _localization.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == LocalizationService.IndexerName) RefreshCatalog();
        };
        RefreshCatalog();
    }

    public IReadOnlyList<FabAction> FabActions { get; }
    public AccountContext Accounts { get; }

    /// <summary>有语言资源文件的界面语言。</summary>
    public static IReadOnlyList<LanguageType> SupportedLanguages { get; } = [LanguageType.ZhHans, LanguageType.En, LanguageType.Ja];

    public IReadOnlyList<ThemeSwatch> Presets { get; }

    /// <summary>强调色色板，值为配置格式（<c>BlueAccent</c>），与 1.2.x 的配置文件兼容。</summary>
    public IReadOnlyList<ThemeSwatch> Accents { get; }

    public IReadOnlyList<LocalizedOption> Languages { get; }
    public IReadOnlyList<LocalizedOption> NotificationModes { get; }
    public IReadOnlyList<LocalizedOption> LogLevels { get; }
    public ObservableCollection<CatalogRow> CatalogRows { get; } = [];

    // ── 常规游戏设置 ──────────────────────────────────────────

    public string GamePath => string.IsNullOrWhiteSpace(_config.User.GamePath) ? _localization["Settings_GamePathEmpty"] : _config.User.GamePath;

    public bool AutoLoadLocalData
    {
        get => _config.User.AutoLoadLocalData;
        set
        {
            if (_config.User.AutoLoadLocalData == value) return;
            _config.User.AutoLoadLocalData = value;
            OnPropertyChanged();
        }
    }

    [RelayCommand]
    private async Task SelectGamePathAsync()
    {
        IStorageProvider? storage = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow?.StorageProvider;
        if (storage is null) return;
        IStorageFolder? start = string.IsNullOrWhiteSpace(_config.User.GamePath) ? null : await storage.TryGetFolderFromPathAsync(_config.User.GamePath);
        IReadOnlyList<IStorageFolder> picked = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = _localization["SelectFolder_GamePathTitle"],
            AllowMultiple = false,
            SuggestedStartLocation = start,
        });
        string? folder = picked.FirstOrDefault()?.TryGetLocalPath();
        if (folder is null) return;
        if (!File.Exists(Path.Combine(folder, _config.App.GameLauncherFile)))
        {
            _uiState.ShowToast(_localization["Toast_Warning"],
                string.Format(_localization["Toast_GameLauncherNotFound"], _config.App.GameLauncherFile), NotificationType.Warning);
            return;
        }
        _config.User.GamePath = folder;
        _uiState.ShowToast(_localization["Toast_Success"], _localization["Toast_GamePathUpdated"], NotificationType.Success);
    }

    // ── 界面设置 ──────────────────────────────────────────────

    [RelayCommand]
    private void SelectPreset(ThemeSwatch swatch)
    {
        SelectSwatch(Presets, (string)swatch.Option.Value);
        _config.User.BaseTheme = (string)swatch.Option.Value;
    }

    [RelayCommand]
    private void SelectAccent(ThemeSwatch swatch)
    {
        SelectSwatch(Accents, (string)swatch.Option.Value);
        _config.User.AccentTheme = (string)swatch.Option.Value;
    }

    public bool IsGlassEnabled
    {
        get => _config.User.IsGlassEffectEnabled;
        set
        {
            if (_config.User.IsGlassEffectEnabled == value) return;
            _config.User.IsGlassEffectEnabled = value;
            OnPropertyChanged();
        }
    }

    public double GlassOpacity
    {
        get => _config.User.GlassOpacity;
        set
        {
            int rounded = (int)Math.Round(value);
            if (_config.User.GlassOpacity == rounded) return;
            _config.User.GlassOpacity = rounded;
            OnPropertyChanged();
            OnPropertyChanged(nameof(GlassOpacityText));
        }
    }

    public string GlassOpacityText => _config.User.GlassOpacity.ToString(CultureInfo.InvariantCulture) + "%";

    public bool IsReducedMotion
    {
        get => _config.User.IsReducedMotionEnabled;
        set
        {
            if (_config.User.IsReducedMotionEnabled == value) return;
            _config.User.IsReducedMotionEnabled = value;
            OnPropertyChanged();
        }
    }

    [ObservableProperty] private LocalizedOption _selectedLanguage;
    [ObservableProperty] private LocalizedOption _selectedNotificationMode;

    partial void OnSelectedLanguageChanged(LocalizedOption value)
    {
        if (value is not null) _config.User.AppLanguage = (LanguageType)value.Value;
    }

    partial void OnSelectedNotificationModeChanged(LocalizedOption value)
    {
        if (value is not null) _config.User.NotificationDisplayMode = (NotificationDisplayMode)value.Value;
    }

    // ── 运行日志 ──────────────────────────────────────────────

    public bool EnableFileLogging
    {
        get => _config.App.EnableFileLogging;
        set
        {
            if (_config.App.EnableFileLogging == value) return;
            _config.App.EnableFileLogging = value;
            OnPropertyChanged();
            ScheduleSave();
        }
    }

    [ObservableProperty] private LocalizedOption _selectedLogLevel;

    partial void OnSelectedLogLevelChanged(LocalizedOption value)
    {
        if (value is null || Equals(_config.App.MinimumLogLevel, value.Value)) return;
        _config.App.MinimumLogLevel = (LogLevel)value.Value;
        ScheduleSave();
    }

    /// <summary>当前数据根目录（启动时确定，便携模式为程序目录）。</summary>
    public string DataRoot => _paths.DataRoot;

    public bool IsPortable => _paths.IsPortable;

    [RelayCommand]
    private void OpenDataFolder() => _launcher.OpenFolder(_paths.DataRoot);

    [RelayCommand]
    private void OpenLogFolder()
    {
        string folder = _config.App.LogFolderPath;
        string path = string.IsNullOrWhiteSpace(folder) ? _paths.LogDirectory
            : Path.IsPathRooted(folder) ? folder : Path.Combine(_paths.DataRoot, folder);
        Directory.CreateDirectory(path);
        _launcher.OpenFolder(path);
    }

    // ── 网络与验证 ────────────────────────────────────────────

    public string ApiKeyword
    {
        get => _config.User.SearchGachaApiUrl ?? string.Empty;
        set
        {
            if (_config.User.SearchGachaApiUrl == value) return;
            _config.User.SearchGachaApiUrl = value;
            OnPropertyChanged();
        }
    }

    public decimal? GeetestPort
    {
        get => _config.App.GeetestPort;
        set => SetAppValue(value, 1024, 65000, v => _config.App.GeetestPort = v, _config.App.GeetestPort);
    }

    public decimal? MaxRetries
    {
        get => _config.Api.MaxRetries;
        set => SetAppValue(value, 0, 10, v => _config.Api.MaxRetries = v, _config.Api.MaxRetries);
    }

    public decimal? RetryDelay
    {
        get => _config.Api.DelayMs;
        set => SetAppValue(value, 0, 60000, v => _config.Api.DelayMs = v, _config.Api.DelayMs);
    }

    private void SetAppValue(decimal? value, int min, int max, Action<int> apply, int current, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (value is null) return;
        int clamped = Math.Clamp((int)value.Value, min, max);
        if (clamped == current) return;
        apply(clamped);
        OnPropertyChanged(name);
        ScheduleSave();
    }

    // ── 游戏资料与图片缓存 ────────────────────────────────────

    [ObservableProperty] private string? _catalogMessage;
    [ObservableProperty] private string _catalogLastSuccess = string.Empty;
    [ObservableProperty] private bool _isCatalogSyncing;

    [RelayCommand(CanExecute = nameof(CanUseCatalog))]
    private async Task SyncCatalogAsync()
    {
        _catalogCts = new CancellationTokenSource();
        CatalogMessage = null;
        UpdateCatalogCommands();
        try
        {
            await _catalogSync.SyncAsync(true, _catalogCts.Token);
        }
        catch (OperationCanceledException) when (_catalogCts.IsCancellationRequested)
        {
            // 用户取消。
        }
        catch (Exception ex)
        {
            _logger.Warn("手动同步资料失败。", ex);
            CatalogMessage = _localization["Catalog_Failed"];
        }
        finally
        {
            _catalogCts.Dispose();
            _catalogCts = null;
            RefreshCatalog();
        }
    }

    [RelayCommand(CanExecute = nameof(IsCatalogSyncing))]
    private void CancelCatalog() => _catalogCts?.Cancel();

    [RelayCommand(CanExecute = nameof(CanUseCatalog))]
    private async Task ClearImageCacheAsync()
    {
        _clearingCache = true;
        UpdateCatalogCommands();
        try
        {
            await _images.ClearAsync();
            CatalogMessage = _localization["Catalog_CacheCleared"];
        }
        catch (Exception ex)
        {
            _logger.Warn("清理图片缓存失败。", ex);
            CatalogMessage = _localization["Catalog_CacheClearFailed"];
        }
        finally
        {
            _clearingCache = false;
            RefreshCatalog();
        }
    }

    private bool CanUseCatalog() => !_catalogSync.IsBusy && !_clearingCache && _catalogCts is null;

    private void RefreshCatalog()
    {
        CatalogRows.Clear();
        DateTimeOffset? last = null;
        foreach (var status in _catalogSync.Statuses)
        {
            CatalogRows.Add(new CatalogRow(
                _localization["Catalog_" + status.Category],
                string.Format(_localization["Catalog_VersionCount"], status.Version, status.Count),
                _localization[_catalogSync.IsBusy ? "Catalog_Syncing" : status.StatusKey]));
            if (status.LastSuccessUtc is { } at && (last is null || at > last)) last = at;
        }
        CatalogLastSuccess = string.Format(_localization["Catalog_LastSuccess"],
            last?.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? _localization["Catalog_NotSynced"]);
        UpdateCatalogCommands();
    }

    private void UpdateCatalogCommands()
    {
        IsCatalogSyncing = _catalogCts is not null || _catalogSync.IsBusy;
        SyncCatalogCommand.NotifyCanExecuteChanged();
        CancelCatalogCommand.NotifyCanExecuteChanged();
        ClearImageCacheCommand.NotifyCanExecuteChanged();
    }

    // ── 关于 ──────────────────────────────────────────────────

    public string AboutLine => string.Format(_localization["Settings_AboutLine"], Version, _config.App.AppAuther, "github.com/conFess233/WwTool");

    public static string Version { get; } =
        typeof(SettingsViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "v2.0.0";

    [RelayCommand]
    private void OpenRepository() => _launcher.OpenUrl(new Uri(RepositoryUrl));

    // ── 保存 ──────────────────────────────────────────────────

    public void OnNavigatedTo(object? parameter) => RefreshCatalog();

    public void OnNavigatedFrom()
    {
        // 离开页面时把未写盘的程序 / 接口配置写掉。
        if (!_saveDebounce.IsEnabled) return;
        _saveDebounce.Stop();
        _ = SaveQuietlyAsync();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        _saveDebounce.Stop();
        try
        {
            await _config.SaveAllAsync();
            _uiState.ShowToast(new NotificationRequest
            {
                Title = _localization["Action_SaveSettings"],
                Message = _localization["Toast_SettingsSaved"],
                Type = NotificationType.Success,
                Priority = NotificationPriority.Important,
                Source = "Settings",
                DedupeKey = "settings-saved",
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or WwToolException)
        {
            _logger.Error("保存设置失败", ex);
            _uiState.ShowToast(_localization["Toast_Error"], _localization["Settings_AutoSaveFailed"], NotificationType.Error);
        }
    }

    private void ScheduleSave()
    {
        _saveDebounce.Stop();
        _saveDebounce.Start();
    }

    private async Task SaveQuietlyAsync()
    {
        try
        {
            await _config.SaveAllAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or WwToolException)
        {
            _logger.Error("自动保存设置失败", ex);
            _uiState.ShowToast(_localization["Toast_Error"], _localization["Settings_AutoSaveFailed"], NotificationType.Error);
        }
    }

    private static void SelectSwatch(IEnumerable<ThemeSwatch> swatches, string? value)
    {
        ThemeSwatch[] all = swatches.ToArray();
        ThemeSwatch selected = all.FirstOrDefault(s => Equals(s.Option.Value, value)) ?? all[0];
        foreach (ThemeSwatch s in all) s.IsSelected = s == selected;
    }

    private static IBrush Brush(string file, string key) =>
        ThemeService.PeekColor(file, key) is { } color ? new SolidColorBrush(color) : Brushes.Transparent;
}
