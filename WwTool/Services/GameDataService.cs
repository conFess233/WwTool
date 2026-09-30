using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using WwTool.Common.Models;
using WwTool.Services.Interfaces;

namespace WwTool.Services;

/// <summary>加载四类静态资料，以完整快照发布变更；不再读取旧的单文件格式。</summary>
public class GameDataService : INotifyPropertyChanged
{
    private readonly ILoggerService _logger;
    private Dictionary<string, JsonObject> _documents = new();
    public string DirectoryPath { get; }
    public Dictionary<int, GameItemInfo> Items { get; private set; } = new();
    public int Revision { get; private set; }
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? Changed;

    public GameDataService(IConfigService configService, ILoggerService logger)
    {
        _logger = logger;
        DirectoryPath = Path.GetFullPath(configService.App.CatalogDirectory, AppContext.BaseDirectory);
    }

    public Task InitializeAsync() => LoadResourcesAsync();

    public async Task LoadResourcesAsync()
    {
        var documents = new Dictionary<string, JsonObject>();
        foreach (string category in CatalogJson.Categories)
        {
            string path = Path.Combine(DirectoryPath, category + ".json");
            JsonObject document;
            try
            {
                document = JsonNode.Parse(await File.ReadAllTextAsync(path)) as JsonObject
                    ?? throw new InvalidDataException("Invalid local catalog root.");
                CatalogJson.Validate(document, category);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or InvalidOperationException or FormatException)
            {
                _logger.Warn($"资料 {category} 不可读，使用随程序发布的配置。", ex);
                if (File.Exists(path)) File.Copy(path, path + "." + DateTime.UtcNow.ToString("yyyyMMddHHmmssffff") + ".bak");
                using var stream = typeof(GameDataService).Assembly.GetManifestResourceStream($"WwTool.Catalogs.{category}.json")
                    ?? throw new InvalidDataException("Missing bundled catalog.");
                document = (await JsonNode.ParseAsync(stream))!.AsObject();
                CatalogJson.Validate(document, category);
                await CatalogJson.WriteAsync(path, document);
            }
            documents.Add(category, document);
        }
        Publish(documents);
    }

    public JsonObject GetDocument(string category) => (JsonObject)_documents[category].DeepClone();

    public async Task SaveAsync(string category, JsonObject document, CancellationToken token)
    {
        CatalogJson.Validate(document, category);
        await CatalogJson.WriteAsync(Path.Combine(DirectoryPath, category + ".json"), document, token);
        var documents = new Dictionary<string, JsonObject>(_documents) { [category] = document };
        Publish(documents);
    }

    private void Publish(Dictionary<string, JsonObject> documents)
    {
        var items = new Dictionary<int, GameItemInfo>();
        foreach (var (category, document) in documents)
        {
            int skipped = 0;
            foreach (var (id, value) in document["items"]!.AsObject())
            {
                if (!CatalogJson.HasDisplayName(value!.AsObject())) { skipped++; continue; }
                var item = value.Deserialize<GameItemInfo>(CatalogJson.Options)!;
                item.ResourceId = int.Parse(id);
                items.Add(item.ResourceId, item);
            }
            if (skipped > 0) _logger.Warn($"资料 {category} 保留了 {skipped} 条无名称记录，暂不显示，等待后续补全。");
        }
        _documents = documents;
        Items = items;
        Revision++;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Revision)));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public GameItemInfo? GetItemById(int id) => Items.TryGetValue(id, out var item) ? item : null;

    /// <summary>四星角色非限定；未知五星角色默认限定，未知武器不作推断。</summary>
    public bool? GetLimitedStatus(int id, int rarity, bool isCharacter)
    {
        if (isCharacter && rarity == 4) return false;
        return GetItemById(id)?.IsLimited ?? (isCharacter && rarity == 5 ? true : null);
    }
}
