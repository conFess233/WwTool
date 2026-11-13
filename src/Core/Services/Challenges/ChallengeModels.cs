namespace WwTool.Core.Services.Challenges;

/// <summary>encore.moe 提供数据的三个周期挑战（接口文档 <c>docs/API/Encore.md</c>）。</summary>
public enum ChallengeKind
{
    /// <summary>逆境深塔（<c>toa</c>）。</summary>
    Tower,

    /// <summary>冥歌海墟（<c>whiwa</c>）。</summary>
    Wastes,

    /// <summary>终焉矩阵（<c>dpmatrix</c>）。</summary>
    Matrix,
}

/// <summary>
/// 一期挑战。深塔与海墟有起止时间（接口只给日期，按当天服务器时间 04:00、UTC+8 计）；矩阵没有日期，
/// 只有所属赛季与赛季结束的版本号。
/// </summary>
public sealed record ChallengeSeason(
    ChallengeKind Kind,
    int Id,
    string Name,
    DateTimeOffset? Start,
    DateTimeOffset? End,
    string? SeasonName = null,
    string? EndVersion = null)
{
    public bool IsDated => Start is not null && End is not null;
    public bool IsActive(DateTimeOffset now) => IsDated && Start <= now && now < End;
    public bool IsUpcoming(DateTimeOffset now) => Start is { } s && s > now;
    public bool IsEnded(DateTimeOffset now) => End is { } e && e <= now;
}

/// <summary>属性（元素）：0 物理、1 冷凝、2 热熔、3 导电、4 气动、5 衍射、6 湮灭。</summary>
public sealed record ChallengeElement(int Id, string Name, string? Icon);

/// <summary>某一属性的伤害抗性（百分比）。<see cref="ElementId"/> 与 <see cref="ChallengeElement.Id"/> 相同。</summary>
public sealed record ChallengeResistance(int ElementId, string Name, double Percent);

/// <summary>深塔、海墟的敌人。</summary>
public sealed record ChallengeMonster(
    long Id,
    string Name,
    string? Icon,
    IReadOnlyList<ChallengeElement> Elements,
    int? Level,
    double? Life,
    IReadOnlyList<ChallengeResistance> Resistances,
    string? Rarity = null);

// ── 逆境深塔 ──────────────────────────────────────────

/// <summary>星级条件：<see cref="Text"/> 为富文本（游戏的 span 着色）。</summary>
public sealed record TowerTarget(int Score, string Text);

/// <summary>一层：每位角色消耗 <see cref="Cost"/> 点疲劳；<see cref="Buffs"/> 为增益 / 敌方强化说明。</summary>
public sealed record TowerFloor(int Floor, int Cost, IReadOnlyList<ChallengeMonster> Monsters, IReadOnlyList<string> Buffs, IReadOnlyList<TowerTarget> Targets);

/// <summary>区域：1 残响之塔、2 深境之塔、3 回音之塔。</summary>
public sealed record TowerArea(int Number, string Name, IReadOnlyList<TowerFloor> Floors);

public sealed record TowerDetail(int Season, IReadOnlyList<TowerArea> Areas);

// ── 冥歌海墟 ──────────────────────────────────────────

/// <summary>关卡的一段：<see cref="Description"/> 为这一段的附加规则（富文本）。</summary>
public sealed record WastesStage(string Description, IReadOnlyList<ChallengeMonster> Monsters);

/// <summary>评级分数线，如 S 2500。</summary>
public sealed record WastesGrade(string Grade, int Score);

/// <summary>一关：<see cref="Description"/> 为本关机制（富文本）；<see cref="Endless"/> 为无尽关。</summary>
public sealed record WastesLevel(
    int Id,
    string Title,
    string Description,
    bool Endless,
    int PassScore,
    IReadOnlyList<WastesGrade> Grades,
    IReadOnlyList<WastesStage> Stages);

/// <summary>区域分组，如「再生海域-海隙」。</summary>
public sealed record WastesGroup(string Name, IReadOnlyList<WastesLevel> Levels);

/// <summary>本期可选的信物（接口只有名字和图标）。</summary>
public sealed record WastesToken(string Name, string? Icon, int Quality);

public sealed record WastesDetail(int Season, IReadOnlyList<WastesGroup> Groups, IReadOnlyList<WastesToken> Tokens);

// ── 终焉矩阵 ──────────────────────────────────────────

/// <summary>可选增益：<see cref="Description"/> 为富文本。</summary>
public sealed record MatrixBuff(string Name, string Description, string? Icon);

/// <summary>敌人机制，如「危机应对·乘胜追击」。</summary>
public sealed record MatrixSkill(string Title, string Description);

public sealed record MatrixEnemy(
    long MonsterId,
    string Name,
    string? Icon,
    int ElementId,
    int KillScore,
    IReadOnlyList<string> Tags,
    IReadOnlyList<MatrixSkill> Skills,
    IReadOnlyList<string> Features);

/// <summary>一轮的敌人；稳态协议只有 1 轮，奇点扩张 3 轮，每轮敌人等级更高。</summary>
public sealed record MatrixRound(int Round, int Level, IReadOnlyList<MatrixEnemy> Enemies);

/// <summary>模式：<see cref="Difficulty"/> 0 稳态协议、1 奇点扩张；<see cref="TeamLimit"/> 0 表示不限。</summary>
public sealed record MatrixMode(
    int Id,
    string Name,
    int Difficulty,
    int TeamLimit,
    IReadOnlyList<int> Thresholds,
    IReadOnlyList<MatrixBuff> Buffs,
    IReadOnlyList<MatrixRound> Rounds);

/// <summary>本期加成角色：<see cref="Description"/> 为加成说明（富文本）。</summary>
public sealed record MatrixRole(int Id, string Name, string? Icon, int Quality, int ElementId, string Description);

public sealed record MatrixDetail(
    int Season,
    string Name,
    string? SeasonName,
    string? EndVersion,
    IReadOnlyList<MatrixMode> Modes,
    IReadOnlyList<MatrixRole> Roles);
