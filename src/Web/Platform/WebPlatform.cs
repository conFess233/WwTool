using System.Text.Json;
using WwTool.Core.Abstractions;
using WwTool.Core.Abstractions.Platform;

namespace WwTool.Web.Platform;

/// <summary>
/// Web 后端不保存任何游戏凭据：Core 仓储写入 OauthCode 时只允许空值，否则直接失败，
/// 防止将来有人误把凭据传进后端。
/// </summary>
public sealed class RejectingSecretProtector : ISecretProtector
{
    public string Protect(string plainText) =>
        string.IsNullOrEmpty(plainText)
            ? string.Empty
            : throw new InvalidOperationException("Web 后端不保存游戏凭据。");

    public bool TryUnprotect(string cipherText, out string plainText)
    {
        plainText = string.Empty;
        return false;
    }
}

/// <summary>
/// 后端只有统计服务会取文案（“已垫”标签），统一用简体中文；界面文案由前端按访客语言显示。
/// </summary>
public sealed class WebLocalizer : ILocalizer
{
    private readonly Dictionary<string, string> _table;

    public WebLocalizer(IAppPaths paths)
    {
        string file = Path.Combine(paths.BundledResourceDirectory, "Language", "zh-Hans.json");
        _table = File.Exists(file)
            ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file)) ?? []
            : [];
    }

    public string this[string key] => _table.TryGetValue(key, out string? value) ? value : key;
}
