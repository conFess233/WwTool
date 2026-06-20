using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Threading;
using WwTool.Common.Models.Config;
using WwTool.Extensions;
using WwTool.Services.Interfaces;

namespace WwTool.Services
{
    /// <summary>
    /// 配置服务
    /// </summary>
    public class ConfigService : IConfigService
    {
        // 配置文件目录
        private readonly string _configFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
        private readonly string _appConfigPath;
        private readonly string _apiConfigPath;
        private readonly string _userConfigPath;
        private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
        private readonly DispatcherTimer _autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };

        /// <summary>
        /// 应用配置
        /// </summary>
        public AppConfig App { get; private set; } = new();
        /// <summary>
        /// Api配置，包含固定参数和Url
        /// </summary>
        public ApiConfig Api { get; private set; } = new();
        /// <summary>
        /// 用户配置
        /// </summary>
        public UserConfig User { get; private set; } = new();

        public ConfigService()
        {
            _appConfigPath = Path.Combine(_configFolder, "AppConfig.json");
            _apiConfigPath = Path.Combine(_configFolder, "ApiConfig.json");
            _userConfigPath = Path.Combine(_configFolder, "UserConfig.json");

            // 自动保存用户配置
            _autoSaveTimer.Tick += async (_, _) =>
            {
                _autoSaveTimer.Stop();

                try
                {
                    await SaveUserAsync();
                }
                catch (Exception ex)
                {

                }
            };
        }



        /// <summary>
        /// 程序启动时，一次性加载所有配置
        /// </summary>
        public void LoadAll()
        {
            Directory.CreateDirectory(_configFolder);

            Load<AppConfig>(_appConfigPath).CopyTo(App);
            Load<ApiConfig>(_apiConfigPath).CopyTo(Api);
            Load<UserConfig>(_userConfigPath).CopyTo(User);

            User.PropertyChanged -= OnUserConfigChanged;
            User.PropertyChanged += OnUserConfigChanged;
        }

        /// <summary>
        /// 保存所有配置
        /// </summary>
        /// <returns></returns>
        public async Task SaveAllAsync()
        {
            await Task.WhenAll(SaveAppAsync(), SaveApiAsync(), SaveUserAsync());
        }

        /// <summary>
        /// 同步保存所有配置
        /// </summary>
        public void SaveAll()
        {
            SaveSync(App, _appConfigPath);
            SaveSync(Api, _apiConfigPath);
            SaveSync(User, _userConfigPath);
        }

        private void SaveSync<T>(T config, string path)
        {
            try
            {
                if (!Directory.Exists(_configFolder)) Directory.CreateDirectory(_configFolder);
                File.WriteAllText(path, JsonSerializer.Serialize(config, _jsonOptions));
            }
            catch
            {
                // 忽略高频写入时的文件锁定异常
            }
        }

        /// <summary>
        /// 用户配置变化时，触发自动保存
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnUserConfigChanged(object? sender, PropertyChangedEventArgs e)
        {
            AutoSaveUser();
        }

        public async Task SaveAppAsync() => await SaveAsync(App, _appConfigPath);
        public async Task SaveApiAsync() => await SaveAsync(Api, _apiConfigPath);
        public async Task SaveUserAsync() => await SaveAsync(User, _userConfigPath);


        /// <summary>
        /// 加载配置文件
        /// </summary>
        private T Load<T>(string path) where T : new()
        {
            try
            {
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var data = JsonSerializer.Deserialize<T>(json, _jsonOptions) ?? new();
                    if (data != null)
                    {
                        return data;
                    }
                }
            }
            catch
            {
            }

            var defaultConfig = new T();
            SaveAsync(defaultConfig, path);
            return defaultConfig;
        }

        /// <summary>
        /// 保存配置文件
        /// </summary>
        private async Task SaveAsync<T>(T config, string path)
        {
            try
            {
                // 确保文件夹存在
                if (!Directory.Exists(_configFolder)) Directory.CreateDirectory(_configFolder);
                await File.WriteAllTextAsync(path, JsonSerializer.Serialize(config, _jsonOptions));
            }
            catch
            {
                // 忽略高频写入时的文件锁定异常
            }
        }

        /// <summary>
        /// 触发用户配置的自动保存
        /// </summary>
        private void AutoSaveUser()
        {
            _autoSaveTimer.Stop();
            _autoSaveTimer.Start();
        }
    }
}
