using System.Text.Json.Serialization;

namespace WwTool.Common.Models.ApiResponse;

public sealed class GuideEnvelope<T>
{
    [JsonPropertyName("code")]
    public int Code { get; set; }
    [JsonPropertyName("message")]
    public string? Message { get; set; }
    [JsonPropertyName("data")]
    public T? Data { get; set; }
}

public sealed class GuideLoginData
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;
}

public sealed class GuidePlayer
{
    [JsonPropertyName("playerId")]
    public long? PlayerId { get; set; }
    [JsonPropertyName("playerName")]
    public string? PlayerName { get; set; }
    [JsonPropertyName("serverId")]
    public string ServerId { get; set; } = string.Empty;
    [JsonPropertyName("serverName")]
    public string? ServerName { get; set; }
    [JsonPropertyName("level")]
    public int? Level { get; set; }
}

public sealed class GuideProfile
{
    [JsonPropertyName("cUid")]
    public string CUid { get; set; } = string.Empty;
    [JsonPropertyName("channelId")]
    public int ChannelId { get; set; }
    [JsonPropertyName("chosenPlayer")]
    public GuidePlayer? ChosenPlayer { get; set; }
}

public sealed class GuideChooseData
{
    [JsonPropertyName("profile")]
    public GuideProfile? Profile { get; set; }
}

public sealed class GuideAvatar
{
    [JsonPropertyName("roleGbId")]
    public string RoleGbId { get; set; } = string.Empty;
    [JsonPropertyName("cardPictureUrl")]
    public string? CardPictureUrl { get; set; }
    [JsonPropertyName("illustrationPictureUrl")]
    public string? IllustrationPictureUrl { get; set; }
    [JsonPropertyName("star")]
    public int Star { get; set; }
    [JsonPropertyName("roleStatus")]
    public int RoleStatus { get; set; }
    [JsonPropertyName("sequence")]
    public int Sequence { get; set; }
    [JsonPropertyName("isAcquired")]
    public bool IsAcquired { get; set; }
    [JsonPropertyName("mayRoleGbId")]
    public string? MayRoleGbId { get; set; }
}

public sealed class GuideIntroductionSummary
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
    [JsonPropertyName("texts")]
    public List<GuideText> Texts { get; set; } = [];
    [JsonPropertyName("modifiedAt")]
    public long? ModifiedAt { get; set; }
}

public sealed class GuideText
{
    [JsonPropertyName("language")]
    public string? Language { get; set; }
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    [JsonPropertyName("introductionName")]
    public string? IntroductionName { get; set; }
    [JsonPropertyName("introductionSource")]
    public string? IntroductionSource { get; set; }
    [JsonPropertyName("roleDescription")]
    public string? RoleDescription { get; set; }
    [JsonPropertyName("introductionSynopsis")]
    public string? IntroductionSynopsis { get; set; }
    [JsonPropertyName("introductionDescription")]
    public string? IntroductionDescription { get; set; }
    [JsonPropertyName("introductionDetail")]
    public string? IntroductionDetail { get; set; }
    [JsonPropertyName("skillDisplayDescription")]
    public string? SkillDisplayDescription { get; set; }
}

public sealed class GuideIntroductionDetail
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
    [JsonPropertyName("role")]
    public GuideRoleAsset? Role { get; set; }
    [JsonIgnore]
    public List<GuideText> BaseTexts { get; set; } = [];
    [JsonPropertyName("displayVideoUrl")]
    public string? DisplayVideoUrl { get; set; }
    [JsonPropertyName("roleAttribute")]
    public GuideRoleAttribute? RoleAttribute { get; set; }
    [JsonPropertyName("echo")]
    public GuideEchoSection? Echo { get; set; }
    [JsonIgnore]
    public List<GuideText> EchoTexts { get; set; } = [];
    [JsonPropertyName("roleSkill")]
    public GuideRoleSkill? RoleSkill { get; set; }
    [JsonPropertyName("roleResonance")]
    public GuideRoleResonance? RoleResonance { get; set; }
    [JsonIgnore]
    public List<GuideText> RoleResonanceTexts { get; set; } = [];
    [JsonPropertyName("weapon")]
    public GuideWeaponSection? Weapon { get; set; }
    [JsonIgnore]
    public List<GuideText> WeaponTexts { get; set; } = [];
    [JsonPropertyName("teammate")]
    public GuideTeammateSection? Teammate { get; set; }
    [JsonPropertyName("likeCount")]
    public int LikeCount { get; set; }
    [JsonPropertyName("collectCount")]
    public int CollectCount { get; set; }
    [JsonPropertyName("isLiked")]
    public bool IsLiked { get; set; }
    [JsonPropertyName("isCollected")]
    public bool IsCollected { get; set; }
    [JsonPropertyName("grade")]
    public string? Grade { get; set; }
}

public sealed class GuideRoleAttribute
{
    [JsonPropertyName("items")]
    public List<GuideAttributeItem> Items { get; set; } = [];
    [JsonPropertyName("isFinished")]
    public bool IsFinished { get; set; }
}

public sealed class GuideAttributeItem
{
    [JsonPropertyName("gbId")]
    public string GbId { get; set; } = string.Empty;
    [JsonPropertyName("pictureUrl")]
    public string? PictureUrl { get; set; }
    [JsonPropertyName("recommendAmount")]
    public string? RecommendAmount { get; set; }
    [JsonPropertyName("currentAmount")]
    public string? CurrentAmount { get; set; }
    [JsonPropertyName("isFinished")]
    public bool IsFinished { get; set; }
}

public sealed class GuideRoleSkill
{
    [JsonPropertyName("fixedSkills")]
    public List<GuideSkillItem> FixedSkills { get; set; } = [];
    [JsonPropertyName("addPointTarget")]
    public List<GuideSkillItem> AddPointTarget { get; set; } = [];
    [JsonPropertyName("addPointSequence")]
    public List<GuideSkillItem> AddPointSequence { get; set; } = [];
    [JsonPropertyName("isFinished")]
    public bool IsFinished { get; set; }
}

public sealed class GuideSkillItem
{
    [JsonPropertyName("gbId")]
    public string GbId { get; set; } = string.Empty;
    [JsonPropertyName("pictureUrl")]
    public string? PictureUrl { get; set; }
    [JsonPropertyName("skillType")]
    public GuideAsset? SkillType { get; set; }
    [JsonPropertyName("recommendLevel")]
    public int? RecommendLevel { get; set; }
    [JsonPropertyName("currentLevel")]
    public int? CurrentLevel { get; set; }
}

public sealed class GuideRoleResonance
{
    [JsonPropertyName("items")]
    public List<GuideResonanceItem> Items { get; set; } = [];
    [JsonPropertyName("isFinished")]
    public bool IsFinished { get; set; }
}

public sealed class GuideResonanceItem
{
    [JsonPropertyName("gbId")]
    public string GbId { get; set; } = string.Empty;
    [JsonPropertyName("pictureUrl")]
    public string? PictureUrl { get; set; }
    [JsonPropertyName("resonanceSequence")]
    public int ResonanceSequence { get; set; }
    [JsonPropertyName("status")]
    public int Status { get; set; }
    [JsonPropertyName("isAcquired")]
    public bool IsAcquired { get; set; }
}

public sealed class GuideWeaponSection
{
    [JsonPropertyName("current")]
    public GuideWeapon? Current { get; set; }
    [JsonPropertyName("items")]
    public List<GuideWeapon> Items { get; set; } = [];
    [JsonPropertyName("isFinished")]
    public bool IsFinished { get; set; }
}

public sealed class GuideWeapon
{
    [JsonPropertyName("gbId")]
    public string GbId { get; set; } = string.Empty;
    [JsonPropertyName("pictureUrl")]
    public string? PictureUrl { get; set; }
    [JsonPropertyName("star")]
    public int Star { get; set; }
    [JsonPropertyName("weaponType")]
    public GuideAsset? WeaponType { get; set; }
    [JsonIgnore]
    public List<GuideText> Texts { get; set; } = [];
}

public class GuideAsset
{
    [JsonPropertyName("gbId")]
    public string GbId { get; set; } = string.Empty;
    [JsonPropertyName("pictureUrl")]
    public string? PictureUrl { get; set; }
    [JsonPropertyName("secondPictureUrl")]
    public string? SecondPictureUrl { get; set; }
    [JsonIgnore]
    public List<GuideText> Texts { get; set; } = [];
}

public sealed class GuideRoleAsset
{
    [JsonPropertyName("roleGbId")]
    public string RoleGbId { get; set; } = string.Empty;
    [JsonPropertyName("cardPictureUrl")]
    public string? CardPictureUrl { get; set; }
    [JsonPropertyName("illustrationPictureUrl")]
    public string? IllustrationPictureUrl { get; set; }
    [JsonPropertyName("star")]
    public int Star { get; set; }
    [JsonIgnore]
    public List<GuideText> Texts { get; set; } = [];
    [JsonPropertyName("element")]
    public GuideAsset? Element { get; set; }
    [JsonPropertyName("rolePlays")]
    public List<GuideAsset> RolePlays { get; set; } = [];
}

public sealed class GuideEchoSection
{
    [JsonPropertyName("current")]
    public GuideEchoBuild? Current { get; set; }
    [JsonPropertyName("main")]
    public GuideEchoBuild? Main { get; set; }
    [JsonPropertyName("spare")]
    public GuideEchoBuild? Spare { get; set; }
    [JsonPropertyName("isFinished")]
    public bool IsFinished { get; set; }
}

public sealed class GuideEchoBuild
{
    [JsonPropertyName("echoProps")]
    public GuideEchoProperties? EchoProperties { get; set; }
    [JsonPropertyName("echoSetEffects")]
    public List<GuideEchoSetEffect> EchoSetEffects { get; set; } = [];
    [JsonPropertyName("echoAttributes")]
    public List<GuideEchoAttribute> EchoAttributes { get; set; } = [];
}

public sealed class GuideEchoProperties : GuideAsset
{
    [JsonPropertyName("monsterTypeGameBusinessId")]
    public string? MonsterTypeGameBusinessId { get; set; }
    [JsonPropertyName("star")]
    public int Star { get; set; }
    [JsonPropertyName("cost")]
    public int Cost { get; set; }
}

public sealed class GuideEchoSetEffect : GuideAsset
{
    [JsonPropertyName("piece")]
    public int? Piece { get; set; }
}

public sealed class GuideEchoAttribute
{
    [JsonPropertyName("cost")]
    public int Cost { get; set; }
    [JsonPropertyName("currentLevel")]
    public int? CurrentLevel { get; set; }
    [JsonPropertyName("attribute")]
    public GuideAsset? Attribute { get; set; }
    [JsonPropertyName("attribute2")]
    public GuideAsset? Attribute2 { get; set; }
    [JsonPropertyName("isFinishedMaxLevel")]
    public bool? IsFinishedMaxLevel { get; set; }
    [JsonPropertyName("isFinished")]
    public bool? IsFinished { get; set; }
}

public sealed class GuideTeammateSection
{
    [JsonPropertyName("items")]
    public List<GuideTeammateItem> Items { get; set; } = [];
}

public sealed class GuideTeammateItem
{
    [JsonPropertyName("main")]
    public GuideRoleAsset? Main { get; set; }
    [JsonPropertyName("spares")]
    public List<GuideRoleAsset> Spares { get; set; } = [];
    [JsonPropertyName("weapon")]
    public GuideWeapon? Weapon { get; set; }
    [JsonPropertyName("echoProps")]
    public GuideEchoProperties? EchoProperties { get; set; }
    [JsonPropertyName("echoSetEffect2")]
    public GuideEchoSetEffect? EchoSetEffect2 { get; set; }
    [JsonPropertyName("echoSetEffect5")]
    public GuideEchoSetEffect? EchoSetEffect5 { get; set; }
    [JsonPropertyName("echoAttributes")]
    public List<GuideEchoAttribute> EchoAttributes { get; set; } = [];
}
