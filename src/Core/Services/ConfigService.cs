using WwTool.Core.Abstractions.Platform;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using WwTool.Core.Models.Config;
using WwTool.Core.Extensions;
using WwTool.Core.Infrastructure;
using WwTool.Core.Abstractions;

namespace WwTool.Core.Services
{
    /// <summary>
    /// 读取并保存应用配置。
    /// </summary>
    public class ConfigService : IConfigService
    {
        public event EventHandler? UserAutoSaveFailed;

        private readonly string _configFolder;
        private readonly string _appConfigPath;
        private readonly string _apiConfigPath;
        private readonly string _userConfigPath;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
        /// <summary>用户设置变更后延迟保存的时间，连续修改只保存一次。</summary>
        private static readonly TimeSpan AutoSaveDelay = TimeSpan.FromSeconds(1);

        // 线程池计时器，不依赖 UI 线程。UserAutoSaveFailed 在线程池触发，订阅方自行切回 UI 线程。
        private readonly Timer _autoSaveTimer;
        private readonly SemaphoreSlim _saveLock = new(1, 1);

        public AppConfig App { get; private set; } = new();
        public ApiConfig Api { get; private set; } = new();
        public UserConfig User { get; private set; } = new();

        public ConfigService(IAppPaths paths)
            : this(paths.ConfigDirectory)
        {
        }

        internal ConfigService(string configFolder)
        {
            _configFolder = configFolder;
            _appConfigPath = Path.Combine(_configFolder, "AppConfig.json");
            _apiConfigPath = Path.Combine(_configFolder, "ApiConfig.json");
            _userConfigPath = Path.Combine(_configFolder, "UserConfig.json");
            _autoSaveTimer = new Timer(_ => _ = AutoSaveUserNowAsync(), null, Timeout.Infinite, Timeout.Infinite);
        }

        private async Task AutoSaveUserNowAsync()
        {
            try
            {
                await SaveUserAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Failed to auto-save user configuration: {ex}");
                UserAutoSaveFailed?.Invoke(this, EventArgs.Empty);
            }
        }

        public void LoadAll()
        {
            Directory.CreateDirectory(_configFolder);

            Load<AppConfig>(_appConfigPath).CopyTo(App);
            Load<ApiConfig>(_apiConfigPath).CopyTo(Api);
            Load<UserConfig>(_userConfigPath).CopyTo(User);
            if (MigrateLegacyThemeSettings())
            {
                SaveSync(User, _userConfigPath);
            }

            User.PropertyChanged -= OnUserConfigChanged;
            User.PropertyChanged += OnUserConfigChanged;
        }

        private bool MigrateLegacyThemeSettings()
        {
            bool changed = false;
            try
            {
                bool hasBaseTheme = false;
                bool hasAccentTheme = false;
                if (File.Exists(_userConfigPath))
                {
                    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(_userConfigPath, Encoding.UTF8));
                    hasBaseTheme = document.RootElement.TryGetProperty(nameof(UserConfig.BaseTheme), out _);
                    hasAccentTheme = document.RootElement.TryGetProperty(nameof(UserConfig.AccentTheme), out _);
                }

                string sourceTheme = hasBaseTheme ? User.BaseTheme : User.CurrentTheme;
                if (string.IsNullOrWhiteSpace(sourceTheme)) sourceTheme = User.CurrentTheme;
                HashSet<string> recognizedThemes =
                [
                    "WarmWhite", "DeepBlue", "SoftGreen", "SoftPink", "BlackGold",
                    "LightTheme", "GlassTheme", "DarkTheme", "BlueTheme", "GreenTheme",
                    "RedTheme", "PurpleTheme", "OrangeTheme", "YellowTheme"
                ];
                bool sourceThemeWasRecognized = recognizedThemes.Contains(sourceTheme);
                string migratedTheme = sourceTheme switch
                {
                    "WarmWhite" or "DeepBlue" or "SoftGreen" or "SoftPink" or "BlackGold" => sourceTheme,
                    "LightTheme" or "GlassTheme" => "WarmWhite",
                    "GreenTheme" => "SoftGreen",
                    "RedTheme" or "PurpleTheme" => "SoftPink",
                    "OrangeTheme" or "YellowTheme" => "WarmWhite",
                    _ => "DeepBlue"
                };
                if (User.BaseTheme != migratedTheme) { User.BaseTheme = migratedTheme; changed = true; }
                if (sourceTheme == "GlassTheme" && !User.IsGlassEffectEnabled) { User.IsGlassEffectEnabled = true; changed = true; }

                if (!hasAccentTheme)
                {
                    User.AccentTheme = User.CurrentTheme switch
                    {
                        "RedTheme" => "RedAccent",
                        "GreenTheme" => "GreenAccent",
                        "PurpleTheme" => "PurpleAccent",
                        "OrangeTheme" => "OrangeAccent",
                        "YellowTheme" => "YellowAccent",
                        _ => "FollowTheme"
                    };
                    changed = true;
                }

                HashSet<string> validAccents = ["FollowTheme", "BlueAccent", "GreenAccent", "PurpleAccent", "RedAccent", "OrangeAccent", "YellowAccent"];
                if (!validAccents.Contains(User.AccentTheme)) { User.AccentTheme = "FollowTheme"; changed = true; }
                if (!sourceThemeWasRecognized && User.AccentTheme != "FollowTheme") { User.AccentTheme = "FollowTheme"; changed = true; }
                int opacity = Math.Clamp(User.GlassOpacity, 10, 95);
                if (User.GlassOpacity != opacity) { User.GlassOpacity = opacity; changed = true; }
            }
            catch (Exception ex)
            {
                Trace.TraceError($"迁移旧主题配置失败，将使用安全默认值: {ex}");
                User.BaseTheme = "DeepBlue";
                User.AccentTheme = "FollowTheme";
                User.GlassOpacity = 80;
                changed = true;
            }
            return changed;
        }

        public async Task SaveAllAsync()
        {
            await SaveAppAsync();
            await SaveApiAsync();
            await SaveUserAsync();
        }

        public async Task FlushAsync()
        {
            _autoSaveTimer.Change(Timeout.Infinite, Timeout.Infinite);
            User.PropertyChanged -= OnUserConfigChanged;
            try { await SaveAllAsync(); }
            catch
            {
                User.PropertyChanged += OnUserConfigChanged;
                throw;
            }
        }

        public void SaveAll()
        {
            SaveSync(App, _appConfigPath);
            SaveSync(Api, _apiConfigPath);
            SaveSync(User, _userConfigPath);
        }

        private void OnUserConfigChanged(object? sender, PropertyChangedEventArgs e)
        {
            AutoSaveUser();
        }

        public Task SaveAppAsync() => SaveAsync(App, _appConfigPath);
        public Task SaveApiAsync() => SaveAsync(Api, _apiConfigPath);
        public Task SaveUserAsync() => SaveAsync(User, _userConfigPath);

        private T Load<T>(string path) where T : new()
        {
            if (!File.Exists(path))
            {
                var defaultConfig = new T();
                SaveSync(defaultConfig, path);
                return defaultConfig;
            }

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                return JsonSerializer.Deserialize<T>(json, _jsonOptions)
                    ?? throw new JsonException("Configuration content resolved to null.");
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                string backupPath = BuildCorruptBackupPath(path);
                File.Move(path, backupPath);
                Trace.TraceWarning($"Invalid configuration moved to '{backupPath}': {ex.Message}");

                var defaultConfig = new T();
                SaveSync(defaultConfig, path);
                return defaultConfig;
            }
        }

        private void SaveSync<T>(T config, string path)
        {
            _saveLock.Wait();
            try
            {
                Directory.CreateDirectory(_configFolder);
                string json = JsonSerializer.Serialize(config, _jsonOptions);
                AtomicFile.WriteText(path, json);
            }
            finally
            {
                _saveLock.Release();
            }
        }

        private async Task SaveAsync<T>(T config, string path)
        {
            string json = JsonSerializer.Serialize(config, _jsonOptions);
            await _saveLock.WaitAsync().ConfigureAwait(false);
            try
            {
                Directory.CreateDirectory(_configFolder);
                await AtomicFile.WriteTextAsync(path, json).ConfigureAwait(false);
            }
            finally
            {
                _saveLock.Release();
            }
        }

        private static string BuildCorruptBackupPath(string path)
        {
            string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            return $"{path}.corrupt.{timestamp}";
        }

        private void AutoSaveUser() =>
            _autoSaveTimer.Change(AutoSaveDelay, Timeout.InfiniteTimeSpan);
    }
}
