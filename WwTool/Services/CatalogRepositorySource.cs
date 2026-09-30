using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using WwTool.Services.Interfaces;

namespace WwTool.Services;

/// <summary>资料与图片共用的仓库默认分支查询，合并并发请求并缓存检查结果。</summary>
public sealed class CatalogRepositorySource(IHttpClientFactory factory, ILoggerService logger)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private string? branch;
    private DateTimeOffset? checkedAtUtc;

    /// <summary>常规读取缓存一天；手动同步可以提前重新查询，网络失败保留已知分支。</summary>
    public async Task<string?> GetDefaultBranchAsync(bool force, CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            if (!force && checkedAtUtc is DateTimeOffset last && DateTimeOffset.UtcNow - last < TimeSpan.FromHours(24))
                return branch;
            try
            {
                using var client = factory.CreateClient("CatalogClient");
                var repo = JsonNode.Parse(await client.GetStringAsync("https://api.github.com/repos/conFess233/WwTool", token));
                string? value = repo?["default_branch"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(value)) throw new JsonException("Missing repository default branch.");
                branch = value;
                checkedAtUtc = DateTimeOffset.UtcNow;
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or FormatException or TaskCanceledException && !token.IsCancellationRequested)
            {
                checkedAtUtc = DateTimeOffset.UtcNow;
                logger.Debug($"仓库默认分支查询失败，保留已有来源：{ex.GetType().Name}");
            }
            return branch;
        }
        finally { gate.Release(); }
    }

    /// <summary>缓存清理允许下一次按需下载重新发现仓库分支。</summary>
    public async Task ResetAsync(CancellationToken token)
    {
        await gate.WaitAsync(token);
        try { checkedAtUtc = null; }
        finally { gate.Release(); }
    }

    /// <summary>按已查明的默认分支生成仓库文件地址。</summary>
    public static string FileUrl(string branch, string relativePath) =>
        $"https://raw.githubusercontent.com/conFess233/WwTool/{Uri.EscapeDataString(branch)}/{relativePath}";
}
