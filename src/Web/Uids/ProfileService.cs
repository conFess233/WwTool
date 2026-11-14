using System.Text.Json;
using WwTool.Core.Data.Repositories;
using WwTool.Core.Models.Api.Responses;
using WwTool.Core.Models.Domain;
using WwTool.Core.Services;

namespace WwTool.Web.Uids;

/// <summary>浏览器转交的账号资料：queryPlayerInfo 与 queryRole 中该大区的 JSON 字符串（data[region] 的值）。</summary>
public sealed record ProfileUpload(string Region, string PlayerInfo, string RoleDetail);

/// <summary>
/// 账号资料：由浏览器用 OauthCode 直连库洛取得后，把不含凭据的原始响应交给后端，
/// 后端用桌面端同一个 <see cref="PlayerSnapshotMapper"/> 映射后落库。数据来源标记为 client（只给本人看）。
/// </summary>
public sealed class ProfileService(IPlayerInfoRepository players, UidBindingService bindings)
{
    public sealed record Parsed(string Uid, string Region, PlayerRegionSummary Summary, PlayerSnapshot Snapshot);

    /// <summary>校验并解析上传内容；<paramref name="expectedUid"/> 不为空时要求资料属于这个 UID。</summary>
    public static Parsed Parse(ProfileUpload body, string? expectedUid)
    {
        if (body.PlayerInfo.Length + body.RoleDetail.Length > Limits.MaxProfileBytes) throw new UserFacingException("数据过大。");
        if (string.IsNullOrWhiteSpace(body.Region) || body.Region.Length > 32) throw new UserFacingException("大区无效。");
        if (ContainsCredential(body.PlayerInfo) || ContainsCredential(body.RoleDetail)) throw new UserFacingException("请求中不能包含登录凭据。");

        PlayerRegionInfo? info;
        RoleDetailInfo? detail;
        try
        {
            info = JsonSerializer.Deserialize<PlayerRegionInfo>(body.PlayerInfo);
            detail = JsonSerializer.Deserialize<RoleDetailInfo>(body.RoleDetail);
        }
        catch (JsonException)
        {
            throw new UserFacingException("账号资料格式无效。");
        }
        PlayerRegionSummary? summary = PlayerSnapshotMapper.Map(info);
        PlayerSnapshot? snapshot = PlayerSnapshotMapper.Map(detail);
        if (summary is null || snapshot is null || !GachaImportService.IsUid(summary.RoleId)) throw new UserFacingException("账号资料格式无效。");
        string uid = summary.RoleId;
        if (snapshot.Base is { Id: var id } && id != 0 && id.ToString(System.Globalization.CultureInfo.InvariantCulture) != uid)
            throw new UserFacingException("账号资料前后不一致。");
        if (expectedUid is not null && expectedUid != uid) throw new UserFacingException("账号资料与当前 UID 不一致。");
        return new Parsed(uid, body.Region, summary, snapshot);
    }

    public async Task SaveAsync(Parsed p, CancellationToken token)
    {
        // 与桌面端 GetDataService 相同的两步写入；OauthCode 传空，Web 端的加密器拒绝任何非空凭据
        await players.SavePlayerRegionInfoAsync(p.Summary, p.Region, string.Empty, token);
        await players.SavePlayerRoleDataAsync(p.Uid, p.Snapshot, p.Region, p.Summary, token);
        await bindings.MarkSourceAsync(p.Uid, "profile", "client", token);
    }

    /// <summary>防止前端误把 OauthCode 等凭据一并转交。</summary>
    private static bool ContainsCredential(string json) =>
        json.Contains("oauthCode", StringComparison.OrdinalIgnoreCase)
        || json.Contains("access_token", StringComparison.OrdinalIgnoreCase)
        || json.Contains("autoToken", StringComparison.OrdinalIgnoreCase);
}
