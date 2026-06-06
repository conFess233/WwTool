using System.IO;
using System.Text.Json;
using WwTool.Common.Context;
using WwTool.Common.Models;
using WwTool.Services.Interfaces;

namespace WwTool.Services
{
    /// <summary>
    /// 游戏数据服务（仅用于加载本地搜集的游戏物品数据等）
    /// </summary>
    public class GameDataService
    {
        private readonly string _gameItemsResourcesPath;
        private readonly IConfigService _configService;

        private readonly ILoggerService _logger;

        public Dictionary<int, GameItemInfo> Items { get; private set; } = new();

        public GameDataService(IConfigService configService, ILoggerService logger)
        {
            _configService = configService;
            _logger = logger;
            _gameItemsResourcesPath = _configService.App.GameItemsResourcesPath;

        }

        public async Task InitializeAsync()
        {
            await LoadResourcesAsync();
        }

        /// <summary>
        /// 异步加载资源
        /// </summary>
        public async Task LoadResourcesAsync()
        {
            if (!File.Exists(_gameItemsResourcesPath))
            {
                _logger.Warn($"未找到游戏物品资源文件: {_gameItemsResourcesPath}");
                return;
            }

            try
            {
                _logger.Info($"正在加载游戏物品资源: {_gameItemsResourcesPath}");
                string json = await File.ReadAllTextAsync(_gameItemsResourcesPath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                var data = JsonSerializer.Deserialize<Dictionary<int, GameItemInfo>>(json, options);
                if (data != null)
                {
                    Items = data;
                    _logger.Debug($"成功加载 {Items.Count} 个游戏物品。");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("读取或解析游戏物品资源失败", ex);
            }
        }

        /// <summary>
        /// 同步加载资源
        /// </summary>
        public void LoadResources()
        {
            LoadResourcesAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// 通过 ID 获取物品信息
        /// </summary>
        public GameItemInfo? GetItemById(int id)
        {
            return Items.TryGetValue(id, out var item) ? item : null;
        }
    }
}
