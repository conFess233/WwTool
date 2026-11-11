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
using WwTool.UI.Pages.Pulls;
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

/// <summary>“游戏资料与图片缓存”里的一类资料：名称与条数。</summary>
public sealed record CatalogRow(string Name, string Count);

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
    private readonly IUserDataService _userData;
    private readonly PullsSession _pullsSession;
    private readonly GamePathPicker _gamePath;
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
        IUserDataService userData,
        PullsSession pullsSession,
        GamePathPicker gamePath)
    {
        _gamePath = gamePath;
        _userData = userData;
        _pullsSession = pullsSession;
        _config = config;
        _uiState = uiState;
        _logger = logger;
        _localization = localization;
        _catalogSync = catalogSync;
        _images = images;
        _launcher = launcher;
        Accounts = accounts;
        FabActions =
        [
            new FabAction("save", "Fab_SaveSettings", "Icon.Save", SaveCommand, localization),
        ];

        Presets = ThemeService.Presets.Select(p =>
        {
            string file = $"Presets/{p}.axaml";
            return new ThemeSwatch(new LocalizedOption(p, "Theme_" + p, localization),
                Brush(file, "BackgroundColor"), Brush(file, "SurfaceColor"), Brush(file, "AccentColor"));
        }).ToArray();
        // 定稿色点顺序：蓝、绿、紫、红、橙、黄；「跟随主题」单独为胶囊。
        string[] accentOrder = [ThemeService.FollowTheme, "Blue", "Green", "Purple", "Red", "Orange", "Yellow"];
        Accents = accentOrder.Where(ThemeService.Accents.Contains).Select(a =>
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
            if (e.PropertyName == nameof(Core.Models.Config.UserConfig.IsGlassEffectEnabled))
            {
                OnPropertyChanged(nameof(IsGlassEnabled));
                OnPropertyChanged(nameof(GlassNeedsRestartToShow));
                OnPropertyChanged(nameof(GlassNeedsRestartToSave));
            }
        };
        _catalogSync.Changed += (_, _) => Dispatcher.UIThread.Post(RefreshCatalog);
        _localization.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == LocalizationService.IndexerName) RefreshCatalog();
        };
        RefreshCatalog();
    }

    public IReadOnlyList<FabAction> FabActions { get; }

    /// <summary>扩展态「保存设置」，点击直接保存。</summary>
    public FabTrigger? FabTrigger { get; } = new("Fab_SaveSettings", "Icon.Save", ExecutesPrimary: true);
    public AccountContext Accounts { get; }

    /// <summary>有语言资源文件的界面语言。</summary>
    public static IReadOnlyList<LanguageType> SupportedLanguages { get; } = [LanguageType.ZhHans, LanguageType.En, LanguageType.Ja];

    public IReadOnlyList<ThemeSwatch> Presets { get; }

    /// <summary>强调色色板，值为配置格式（<c>BlueAccent</c>），与 1.2.x 的配置文件兼容。</summary>
    public IReadOnlyList<ThemeSwatch> Accents { get; }

    /// <summary>「跟随主题」胶囊。</summary>
    public ThemeSwatch FollowAccent => Accents[0];

    /// <summary>六个强调色点。</summary>
    public IEnumerable<ThemeSwatch> AccentColors => Accents.Skip(1);

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
    private Task SelectGamePathAsync() => _gamePath.PickAsync();

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

    // 窗口背景：默认底纹 / 自定义图片。

    public bool UseCustomBackground => _config.User.UseCustomBackground;

    [RelayCommand]
    private void SetBackgroundMode(string mode)
    {
        bool custom = mode == "custom";
        if (_config.User.UseCustomBackground == custom) return;
        _config.User.UseCustomBackground = custom;
        OnPropertyChanged(nameof(UseCustomBackground));
        PickBackgroundImageCommand.NotifyCanExecuteChanged();
        if (custom && string.IsNullOrWhiteSpace(_config.User.CustomBackgroundPath)) _ = PickBackgroundImageAsync();
    }

    /// <summary>动态背景只在 Windows 上提供（系统自带的 Media Foundation 解码）。</summary>
    public bool IsDynamicBackgroundSupported => OperatingSystem.IsWindows();

    /// <summary>动态背景：使用启动器背景时播放其背景视频；选自定义图片时不可用。</summary>
    public bool UseDynamicBackground
    {
        get => _config.User.UseDynamicBackground;
        set
        {
            if (_config.User.UseDynamicBackground == value) return;
            _config.User.UseDynamicBackground = value;
            OnPropertyChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(UseCustomBackground))]
    private async Task PickBackgroundImageAsync()
    {
        IStorageProvider? storage = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow?.StorageProvider;
        if (storage is null) return;
        IReadOnlyList<IStorageFile> picked = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = _localization["SelectFile_BackgroundTitle"],
            AllowMultiple = false,
            FileTypeFilter = [FilePickerFileTypes.ImageAll],
        });
        string? file = picked.FirstOrDefault()?.TryGetLocalPath();
        if (file is null) return;
        _config.User.CustomBackgroundPath = file;
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

    /// <summary>本次启动用的是软件渲染，打开毛玻璃要重启才显示（见 <see cref="RenderingProfile"/>）。</summary>
    public bool GlassNeedsRestartToShow => IsGlassEnabled && !RenderingProfile.GlassCapable;

    /// <summary>本次启动用的是 GPU 渲染，关掉毛玻璃后重启才改用软件渲染、释放显卡驱动内存。</summary>
    public bool GlassNeedsRestartToSave => !IsGlassEnabled && RenderingProfile.GlassCapable && OperatingSystem.IsWindows();

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

    // 数据目录、日志目录的打开入口在首页「便捷操作」。

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

    /// <summary>「按住清空抽卡数据」按满 1.5 秒后执行：删除当前账号的全部本地抽卡记录（账号与凭据保留）。</summary>
    [RelayCommand]
    private async Task ClearGachaAsync()
    {
        string? uid = Accounts.Current?.Uid;
        if (string.IsNullOrWhiteSpace(uid)) return;
        try
        {
            int removed = await _userData.ClearGachaAsync(uid);
            _uiState.ShowToast(new NotificationRequest
            {
                Title = _localization["Toast_Success"],
                Message = string.Format(_localization["Settings_GachaClearedToast"], removed),
                Type = NotificationType.Success,
                Priority = NotificationPriority.Important,
                Source = "Settings",
            });
            await _pullsSession.ReloadAsync();
        }
        catch (Exception ex) when (ex is WwToolException or IOException)
        {
            _logger.Error("清空抽卡数据失败", ex);
            _uiState.ShowToast(_localization["Toast_Error"], _localization["Settings_GachaClearFailed"], NotificationType.Error);
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
        Core.Models.Domain.CatalogVersion? version = null;
        foreach (var status in _catalogSync.Statuses)
        {
            CatalogRows.Add(new CatalogRow(
                _localization["Catalog_" + status.Category],
                string.Format(_localization["Catalog_Count"], status.Count)));
            if (status.LastSuccessUtc is { } at && (last is null || at > last)) last = at;
            if (version is null || status.Version.CompareTo(version.Value) > 0) version = status.Version;
        }
        // 状态行：「资料版本 3.7.0，今天 09:12 同步」。
        if (_catalogSync.IsBusy || IsCatalogSyncing)
        {
            CatalogLastSuccess = _localization["Catalog_Syncing"];
        }
        else if (last is { } when)
        {
            DateTime local = when.ToLocalTime().DateTime;
            string time = local.Date == DateTime.Today
                ? string.Format(_localization["Catalog_Today"], local.ToString("HH:mm", CultureInfo.InvariantCulture))
                : local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            CatalogLastSuccess = string.Format(_localization["Catalog_StatusLine"], version?.ToString() ?? "-", time);
        }
        else
        {
            CatalogLastSuccess = _localization["Catalog_StatusNever"];
        }
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

    public string AboutLine => string.Format(_localization["Settings_AboutVersion"], Version.TrimStart('v'), _config.App.AppAuther);

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
                Title = _localization["Toast_Success"],
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
