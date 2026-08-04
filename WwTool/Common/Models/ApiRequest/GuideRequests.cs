using System.Text.Json.Serialization;

namespace WwTool.Common.Models.ApiRequest;

public sealed class GuideLoginRequest
{
    [JsonPropertyName("cUid")]
    public string CUid { get; set; } = string.Empty;
    [JsonPropertyName("cName")]
    public string CName { get; set; } = string.Empty;
    [JsonPropertyName("accessToken")]
    public string AccessToken { get; set; } = string.Empty;
}

public sealed class GuideChoosePlayerRequest
{
    [JsonPropertyName("playerId")]
    public long PlayerId { get; set; }
    [JsonPropertyName("serverId")]
    public string ServerId { get; set; } = string.Empty;
}
