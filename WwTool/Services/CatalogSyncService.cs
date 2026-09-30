using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using WwTool.Common.Models.Domain;
using WwTool.Services.Interfaces;

namespace WwTool.Services;

/// <summary>仅从仓库主分支按类别独立同步，失败保留旧数据。</summary>
public sealed class CatalogSyncService(GameDataService data,
    IHttpClientFactory factory, CatalogImageService images, ILoggerService logger, CatalogRepositorySource repository)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private IReadOnlyList<CatalogSyncStatus> statuses = [];
    public IReadOnlyList<CatalogSyncStatus> Statuses => statuses;
    public bool IsBusy { get; private set; }
    public event EventHandler? Changed;

    public async Task InitializeAsync()
    {
        string path = Path.Combine(data.DirectoryPath, "CatalogSyncState.json");
        try
        {
            if (File.Exists(path))
                statuses = JsonNode.Parse(await File.ReadAllTextAsync(path))?["statuses"]?.Deserialize<List<CatalogSyncStatus>>(CatalogJson.Options) ?? [];
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or InvalidOperationException or FormatException)
        { logger.Warn("同步状态不可读，将重新检查资料。", ex); }
        statuses = CatalogJson.Categories.Select(category =>
        {
            var old = statuses.FirstOrDefault(x => x.Category == category);
            // 旧官方同步结果不能作为仓库检查成功时间，启动后重新检查该类别。
            if (old?.StatusKey is "Catalog_OfficialUpdated" or "Catalog_Partial")
                old = null;
            var doc = data.GetDocument(category);
            return new CatalogSyncStatus(category, old?.StatusKey ?? "Catalog_NotSynced", CatalogJson.ReadVersion(doc),
                doc["items"]!.AsObject().Count, old?.LastSuccessUtc);
        }).ToArray();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task SyncAsync(bool force, CancellationToken token = default)
    {
        if (!await gate.WaitAsync(0, token)) return;
        try
        {
            string[] due = CatalogJson.Categories.Where(c => force ||
                statuses.FirstOrDefault(s => s.Category == c)?.LastSuccessUtc is not DateTimeOffset last ||
                DateTimeOffset.UtcNow - last >= TimeSpan.FromHours(24)).ToArray();
            if (due.Length == 0) return;
            IsBusy = true;
            Changed?.Invoke(this, EventArgs.Empty);
            using var client = factory.CreateClient("CatalogClient");
            string? branch = await repository.GetDefaultBranchAsync(force, token);

            foreach (string category in due)
            {
                token.ThrowIfCancellationRequested();
                var previous = statuses.FirstOrDefault(x => x.Category == category);
                var local = data.GetDocument(category);
                CatalogVersion localVersion = CatalogJson.ReadVersion(local);
                var candidate = local;
                bool repositoryApplied = false, succeeded = false;
                int added = 0, missing = 0;
                string statusKey = "Catalog_Failed";
                if (!string.IsNullOrWhiteSpace(branch))
                {
                    try
                    {
                        string url = CatalogRepositorySource.FileUrl(branch, $"WwTool/Local/Data/{category}.json");
                        var remote = CatalogJson.Validate(JsonNode.Parse(await client.GetStringAsync(url, token)) as JsonObject
                            ?? throw new InvalidDataException("Invalid repository catalog root."), category);
                        if (CatalogJson.ReadVersion(remote) > localVersion)
                        {
                            var oldIds = local["items"]!.AsObject().Select(x => x.Key).ToHashSet();
                            var remoteIds = remote["items"]!.AsObject().Select(x => x.Key).ToHashSet();
                            added = remoteIds.Except(oldIds).Count();
                            missing = oldIds.Except(remoteIds).Count();
                            logger.Info($"仓库目录 {category} 比较：本地 {oldIds.Count}，仓库 {remoteIds.Count}，新增 {added}，保留缺项 {missing}。");
                            candidate = CatalogJson.Merge(local, remote);
                            repositoryApplied = true;
                        }
                        if (!JsonNode.DeepEquals(candidate, local))
                            await data.SaveAsync(category, candidate, token);
                        succeeded = true;
                        statusKey = repositoryApplied ? "Catalog_RepositoryUpdated" : "Catalog_RepositoryOnly";
                    }
                    catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or JsonException or InvalidOperationException or FormatException or TaskCanceledException && !token.IsCancellationRequested)
                    { logger.Warn($"仓库资料 {category} 无法使用，保留本地资料。", ex); }
                }
                var current = data.GetDocument(category);
                var result = new CatalogSyncStatus(category, statusKey, CatalogJson.ReadVersion(current),
                    current["items"]!.AsObject().Count, succeeded ? DateTimeOffset.UtcNow : previous?.LastSuccessUtc, added, missing);
                statuses = CatalogJson.Categories.Select(c => c == category ? result : statuses.First(x => x.Category == c)).ToArray();
                Changed?.Invoke(this, EventArgs.Empty);
            }
            // 只刷新已经缓存过的图片，不在资料同步时下载整个图鉴。
            await images.RefreshCachedAsync(token, resetFailures: force);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { logger.Info("资料同步已取消，已完成的类别保留。"); }
        finally
        {
            try
            {
                var state = JsonSerializer.SerializeToNode(statuses, CatalogJson.Options)!;
                // 状态数组使用与配置相同的原子写入流程。
                var wrapper = new JsonObject { ["statuses"] = state };
                await CatalogJson.WriteAsync(Path.Combine(data.DirectoryPath, "CatalogSyncState.json"), wrapper);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { logger.Warn("保存资料同步状态失败。", ex); }
            IsBusy = false;
            gate.Release();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
