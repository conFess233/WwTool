using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using WwTool.Core.Services.Notices;

namespace WwTool.UI.Pages.Events;

/// <summary>带一张网络图片的条目：图片由 <see cref="ChallengesViewModel"/> 统一按需加载。</summary>
public abstract partial class ChallengeImageItem(string? imageUrl) : ObservableObject
{
    public string? ImageUrl { get; } = imageUrl;
    [ObservableProperty] private Bitmap? _image;
}

/// <summary>属性（元素）的显示：名称与游戏里的代表色。</summary>
public static class ChallengeElements
{
    // 物理、冷凝、热熔、导电、气动、衍射、湮灭：取游戏里各属性的代表色。
    private static readonly IBrush[] Brushes =
    [
        new SolidColorBrush(Color.Parse("#8F9BA6")),
        new SolidColorBrush(Color.Parse("#3FA9F5")),
        new SolidColorBrush(Color.Parse("#F0703C")),
        new SolidColorBrush(Color.Parse("#A86BF0")),
        new SolidColorBrush(Color.Parse("#2FCB91")),
        new SolidColorBrush(Color.Parse("#E3C33A")),
        new SolidColorBrush(Color.Parse("#D9488F")),
    ];

    public static IBrush Brush(int id) => id >= 0 && id < Brushes.Length ? Brushes[id] : Avalonia.Media.Brushes.Transparent;
}

/// <summary>富文本说明（游戏的 span 着色、换行），交给 <c>NoticeContent</c> 渲染。</summary>
public sealed record RichText(IReadOnlyList<NoticeBlock> Blocks)
{
    public static RichText Of(string? html) => new(NoticeHtml.Parse(html?.Replace("\n", "<br>")));
}

/// <summary>深塔 / 海墟的敌人头像格：悬停显示等级、生命与抗性。</summary>
public sealed class MonsterItem(string? imageUrl, string name, IBrush elementBrush, string elementName, string tip, string? highlight) : ChallengeImageItem(imageUrl)
{
    public string Name { get; } = name;
    public IBrush ElementBrush { get; } = elementBrush;
    public string ElementName { get; } = elementName;
    public string Tip { get; } = tip;

    /// <summary>明显高于其他属性的抗性（如「衍射抗性 60%」）；没有时为 null。</summary>
    public string? Highlight { get; } = highlight;
    public bool HasHighlight => Highlight is not null;
}

// ── 逆境深塔 ──────────────────────────────────────────

public sealed record TowerTargetItem(string Stars, RichText Text);

public sealed record TowerFloorItem(
    string Title,
    string CostText,
    IReadOnlyList<MonsterItem> Monsters,
    IReadOnlyList<RichText> Buffs,
    IReadOnlyList<TowerTargetItem> Targets)
{
    public bool HasBuffs => Buffs.Count > 0;
}

/// <summary>一个区域：<see cref="SharedBuffs"/> 为各层相同的增益（只显示一次），各层只列自己多出的。</summary>
public sealed record TowerAreaItem(string Name, IReadOnlyList<RichText> SharedBuffs, IReadOnlyList<TowerFloorItem> Floors)
{
    public bool HasSharedBuffs => SharedBuffs.Count > 0;
}

// ── 冥歌海墟 ──────────────────────────────────────────

public sealed record WastesStageItem(string Title, RichText Description, bool HasDescription, IReadOnlyList<MonsterItem> Monsters);

public sealed record WastesLevelItem(string Title, bool IsEndless, string GradesText, RichText Description, bool HasDescription, IReadOnlyList<WastesStageItem> Stages);

public sealed record WastesGroupItem(string Name, IReadOnlyList<WastesLevelItem> Levels);

public sealed class TokenItem(string? imageUrl, string name, bool isGold) : ChallengeImageItem(imageUrl)
{
    public string Name { get; } = name;
    public bool IsGold { get; } = isGold;
}

// ── 终焉矩阵 ──────────────────────────────────────────

public sealed class MatrixBuffItem(string? imageUrl, string name, RichText description) : ChallengeImageItem(imageUrl)
{
    public string Name { get; } = name;
    public RichText Description { get; } = description;
}

public sealed class MatrixEnemyItem(string? imageUrl, string name, IBrush elementBrush, string elementName, IReadOnlyList<string> tags,
    RichText skills, bool hasSkills, string? scoreText, string? featureText) : ChallengeImageItem(imageUrl)
{
    public string Name { get; } = name;
    public IBrush ElementBrush { get; } = elementBrush;
    public string ElementName { get; } = elementName;
    public IReadOnlyList<string> Tags { get; } = tags;
    public RichText Skills { get; } = skills;
    public bool HasSkills { get; } = hasSkills;
    public string? ScoreText { get; } = scoreText;
    public bool HasScore => ScoreText is not null;
    public string? FeatureText { get; } = featureText;
    public bool HasFeature => FeatureText is not null;
}

public sealed record MatrixRoundItem(string Title, IReadOnlyList<MatrixEnemyItem> Enemies)
{
    public bool HasTitle => Title.Length > 0;
}

public sealed record MatrixModeItem(string Name, string TeamText, string GradesText, IReadOnlyList<MatrixBuffItem> Buffs, IReadOnlyList<MatrixRoundItem> Rounds);

public sealed class MatrixRoleItem(string? imageUrl, string name, bool isGold, RichText description) : ChallengeImageItem(imageUrl)
{
    public string Name { get; } = name;
    public bool IsGold { get; } = isGold;
    public RichText Description { get; } = description;
}
