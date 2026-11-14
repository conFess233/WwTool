using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WwTool.Core.Abstractions.Platform;
using WwTool.Core.Localization;
using WwTool.Core.Services.Challenges;
using WwTool.UI.Feedback;
using WwTool.UI.Pages.Events;
using WwTool.UI.Pages.Pulls;
using WwTool.UI.Services;

namespace WwTool.UI.Pages.Roles;

/// <summary>详情弹窗打开时停在哪个页签。</summary>
public enum CharacterDetailTab
{
    Overview,
    Skills,
    Chain,
    Build,
    Lore,
}

/// <summary>打开详情时调用方已知的信息。</summary>
public sealed record CharacterDetailRequest(int RoleId, string Name, string IconPath, int Star)
{
    public CharacterDetailTab Tab { get; init; } = CharacterDetailTab.Overview;

    /// <summary>共鸣链数（攻略站同步或按抽卡记录推算）；未知时为 null。</summary>
    public int? Sequence { get; init; }

    /// <summary>「二链」这类显示文字，与角色页一致。</summary>
    public string? SequenceText { get; init; }

    /// <summary>「我的养成」页签的内容；没有时不显示该页签。</summary>
    public CharacterBuild? Build { get; init; }
}

/// <summary>一次抽到该角色的记录。</summary>
public sealed record CharacterPullRow(string Time, int Pity, string PoolName, IBrush RatingBrush, string Rating, double Ratio,
    bool IsGuaranteed, bool IsSoftPity, bool IsLost, bool IsStandardPool);

public sealed record LabeledValue(string Label, string Value);

/// <summary>定位标签：名称与游戏里的颜色。</summary>
public sealed record CharacterTagItem(string Name, string? Description, IBrush Foreground, IBrush Background);

public sealed partial class CharacterSkinItem(EncoreSkin skin) : ObservableObject
{
    public EncoreSkin Skin { get; } = skin;
    public string Name => Skin.Name;
    [ObservableProperty] private bool _isSelected;
}

public sealed partial class CharacterSkillItem(EncoreSkill skill) : ChallengeImageItem(skill.IconUrl)
{
    public EncoreSkill Skill { get; } = skill;
    public string Type => Skill.Type;
    public string Name => Skill.Name;
    [ObservableProperty] private bool _isSelected;
}

public sealed class CharacterChainItem(EncoreChainNode node, bool? unlocked) : ChallengeImageItem(node.IconUrl)
{
    public int Index { get; } = node.Index;
    public string Name { get; } = node.Name;
    public RichText Description { get; } = RichText.Of(node.Description);
    public bool IsUnlocked { get; } = unlocked == true;
    public bool IsLocked { get; } = unlocked == false;
}

public sealed partial class CharacterLoreItem(string title, RichText content) : ObservableObject
{
    public string Title { get; } = title;
    public RichText Content { get; } = content;
    [ObservableProperty] private bool _isExpanded;
}

/// <summary>
/// 角色详情弹窗：资料来自 encore.moe（先显示缓存再刷新），「我的抽取」来自本地抽卡记录，
/// 「我的养成」来自攻略站快照（由调用方传入）。抽卡统计的时间线与角色页共用。
/// </summary>
public sealed partial class CharacterDetailViewModel : DialogViewModel<bool>, ILargeDialog
{
    private const int PortraitWidth = 720;
    private const int IconWidth = 96;

    private readonly EncoreService _encore;
    private readonly EncoreImages _images;
    private readonly LocalizationService _localization;
    private readonly IAppLauncher _launcher;
    private readonly string _language;
    private readonly CancellationTokenSource _cts = new();
    private EncoreCharacter? _character;

    public CharacterDetailViewModel(CharacterDetailRequest request, IReadOnlyList<CharacterPullRow> pulls, string language,
        EncoreService encore, EncoreImages images, LocalizationService localization, IAppLauncher launcher)
    {
        Request = request;
        _encore = encore;
        _images = images;
        _localization = localization;
        _launcher = launcher;
        _language = language;

        Name = request.Name;
        StarsText = new string('★', Math.Clamp(request.Star, 1, 5));
        Pulls = pulls;
        Build = request.Build;

        Tabs =
        [
            new ChipOption("overview") { Title = localization["CharDetail_TabOverview"] },
            new ChipOption("skills") { Title = localization["CharDetail_TabSkills"] },
            new ChipOption("chain") { Title = localization["CharDetail_TabChain"] },
        ];
        if (Build is not null) Tabs.Add(new ChipOption("build") { Title = localization["CharDetail_TabBuild"] });
        Tabs.Add(new ChipOption("lore") { Title = localization["CharDetail_TabLore"] });
        string start = request.Tab switch
        {
            CharacterDetailTab.Skills => "skills",
            CharacterDetailTab.Chain => "chain",
            CharacterDetailTab.Build when Build is not null => "build",
            CharacterDetailTab.Lore => "lore",
            _ => "overview",
        };
        SelectTab(Tabs.First(t => t.Key == start));

        PullsSummary = pulls.Count == 0 ? localization["CharDetail_NoPulls"]
            : string.Format(CultureInfo.CurrentCulture, localization["CharDetail_PullsSummary"], pulls.Count,
                Math.Round(pulls.Average(p => p.Pity), 1).ToString("0.#", CultureInfo.InvariantCulture));

        EncoreData<EncoreCharacter>? cached = encore.LoadCharacter(request.RoleId, language);
        if (cached is not null) Apply(cached.Value);
        // 缓存还新时不联网（「重试」仍会强制刷新）。
        if (!encore.IsCharacterFresh(cached)) _ = RefreshAsync();
    }

    public CharacterDetailRequest Request { get; }

    // ── 头部 ──────────────────────────────────────────────

    [ObservableProperty] private string _name;
    [ObservableProperty] private string? _nickName;
    [ObservableProperty] private string? _introduction;
    [ObservableProperty] private string? _elementName;
    [ObservableProperty] private IBrush _elementBrush = Brushes.Transparent;
    [ObservableProperty] private string? _weaponType;
    [ObservableProperty] private Bitmap? _portrait;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowError), nameof(ShowContent))]
    private bool _isLoading = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowError), nameof(ShowContent))]
    private bool _hasData;

    public string StarsText { get; }
    public string IconPath => Request.IconPath;
    public string? SequenceText => Request.SequenceText;
    public bool HasSequence => Request.SequenceText is not null;
    public bool IsMidSequence => Request.Sequence is >= 3 and <= 5;
    public bool IsFullSequence => Request.Sequence >= 6;
    public bool IsFiveStar => Request.Star >= 5;
    public ObservableCollection<CharacterTagItem> Tags { get; } = [];
    public ObservableCollection<CharacterSkinItem> Skins { get; } = [];
    public bool HasSkinChoice => Skins.Count > 1;

    [ObservableProperty] private string? _skinName;

    public bool ShowError => !IsLoading && !HasData;
    public bool ShowContent => HasData;

    // ── 页签 ──────────────────────────────────────────────

    public ObservableCollection<ChipOption> Tabs { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOverview), nameof(IsSkills), nameof(IsChain), nameof(IsBuild), nameof(IsLore))]
    private string _tab = "overview";

    public bool IsOverview => Tab == "overview";
    public bool IsSkills => Tab == "skills";
    public bool IsChain => Tab == "chain";
    public bool IsBuild => Tab == "build";
    public bool IsLore => Tab == "lore";

    [RelayCommand]
    private void SelectTab(ChipOption tab)
    {
        foreach (ChipOption t in Tabs) t.IsSelected = t == tab;
        Tab = tab.Key;
    }

    // ── 概览 ──────────────────────────────────────────────

    public IReadOnlyList<CharacterPullRow> Pulls { get; }
    public bool HasPulls => Pulls.Count > 0;
    public string PullsSummary { get; }

    public IReadOnlyList<int> StatLevels { get; } = [1, 20, 40, 50, 60, 70, 80, 90];

    [ObservableProperty] private int _statLevel = 90;
    public ObservableCollection<LabeledValue> Stats { get; } = [];
    public ObservableCollection<LabeledValue> Profile { get; } = [];
    public bool HasProfile => Profile.Count > 0;

    partial void OnStatLevelChanged(int value) => RebuildStats();

    // ── 技能 ──────────────────────────────────────────────

    public ObservableCollection<CharacterSkillItem> Skills { get; } = [];
    public IReadOnlyList<int> SkillLevels { get; } = Enumerable.Range(1, 10).ToList();

    [ObservableProperty] private int _skillLevel = 10;
    [ObservableProperty] private CharacterSkillItem? _selectedSkill;
    [ObservableProperty] private RichText? _skillDescription;
    public ObservableCollection<LabeledValue> Multipliers { get; } = [];
    public bool HasMultipliers => Multipliers.Count > 0;

    partial void OnSkillLevelChanged(int value) => RebuildMultipliers();

    partial void OnSelectedSkillChanged(CharacterSkillItem? value)
    {
        foreach (CharacterSkillItem s in Skills) s.IsSelected = s == value;
        SkillDescription = value is null ? null : RichText.Of(value.Skill.Description);
        RebuildMultipliers();
    }

    [RelayCommand]
    private void PickSkill(CharacterSkillItem skill) => SelectedSkill = skill;

    // ── 共鸣链 ────────────────────────────────────────────

    public ObservableCollection<CharacterChainItem> Chain { get; } = [];
    public string ChainSummary => Request.Sequence is { } s
        ? string.Format(CultureInfo.CurrentCulture, _localization["CharDetail_ChainUnlocked"], Math.Clamp(s, 0, 6))
        : _localization["CharDetail_ChainUnknown"];

    // ── 我的养成 ──────────────────────────────────────────

    public CharacterBuild? Build { get; }

    // ── 故事与语音 ────────────────────────────────────────

    public ObservableCollection<CharacterLoreItem> Stories { get; } = [];
    public ObservableCollection<CharacterLoreItem> Voices { get; } = [];

    [RelayCommand]
    private static void ToggleLore(CharacterLoreItem item) => item.IsExpanded = !item.IsExpanded;

    // ── 命令 ──────────────────────────────────────────────

    [RelayCommand]
    private void Dismiss() => Cancel();

    [RelayCommand]
    private void OpenSite()
    {
        if (Uri.TryCreate(_encore.CharacterPageUrl(Request.RoleId, _language), UriKind.Absolute, out Uri? uri)) _launcher.OpenUrl(uri);
    }

    [RelayCommand]
    private Task Retry() => RefreshAsync();

    [RelayCommand]
    private void PickSkin(CharacterSkinItem skin)
    {
        foreach (CharacterSkinItem s in Skins) s.IsSelected = s == skin;
        SkinName = skin.Name;
        LoadPortrait(skin.Skin.PortraitUrl ?? _character?.PortraitUrl);
    }

    protected override bool CancelResult => false;

    public override bool TryAccept() => false;

    public override void Cancel()
    {
        _cts.Cancel();
        base.Cancel();
    }

    // ── 加载 ──────────────────────────────────────────────

    private async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            EncoreData<EncoreCharacter>? fresh = await _encore.RefreshCharacterAsync(Request.RoleId, _language, _cts.Token);
            if (fresh is not null && (fresh.Changed || _character is null)) Apply(fresh.Value);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void Apply(EncoreCharacter c)
    {
        _character = c;
        Name = c.Name;
        NickName = c.NickName;
        Introduction = c.Introduction;
        ElementName = c.ElementId is >= 0 and <= 6
            ? _localization["Challenge_Element_" + c.ElementId.ToString(CultureInfo.InvariantCulture)]
            : c.ElementName;
        ElementBrush = ChallengeElements.Brush(c.ElementId);
        WeaponType = c.WeaponTypeName;

        Tags.Clear();
        foreach (EncoreTag t in c.Tags)
        {
            Color color = Color.TryParse("#" + t.Color, out Color parsed) ? parsed : Colors.Gray;
            Tags.Add(new CharacterTagItem(t.Name, t.Description, new SolidColorBrush(Darken(color)), new SolidColorBrush(color, 0.16)));
        }

        Skins.Clear();
        foreach (EncoreSkin s in c.Skins) Skins.Add(new CharacterSkinItem(s));
        if (Skins.Count > 0) Skins[0].IsSelected = true;
        SkinName = Skins.FirstOrDefault()?.Name;
        OnPropertyChanged(nameof(HasSkinChoice));

        Profile.Clear();
        foreach (EncoreProfileField f in c.Profile)
            Profile.Add(new LabeledValue(_localization["CharDetail_Profile_" + f.Key], f.Value));
        OnPropertyChanged(nameof(HasProfile));
        RebuildStats();

        string? selected = SelectedSkill?.Name;
        Skills.Clear();
        foreach (EncoreSkill s in c.Skills) Skills.Add(new CharacterSkillItem(s));
        SelectedSkill = Skills.FirstOrDefault(s => s.Name == selected) ?? Skills.FirstOrDefault();

        Chain.Clear();
        foreach (EncoreChainNode n in c.Chain)
            Chain.Add(new CharacterChainItem(n, Request.Sequence is { } seq ? n.Index <= seq : null));

        Stories.Clear();
        foreach (EncoreText s in c.Stories) Stories.Add(new CharacterLoreItem(s.Title, RichText.Of(s.Content)));
        Voices.Clear();
        foreach (EncoreText v in c.Voices) Voices.Add(new CharacterLoreItem(v.Title, RichText.Of(v.Content)));

        HasData = true;
        LoadPortrait(c.Skins.FirstOrDefault()?.PortraitUrl ?? c.PortraitUrl);
        LoadIcons();
    }

    private void RebuildStats()
    {
        Stats.Clear();
        if (_character is null) return;
        foreach (EncoreStat s in _character.Stats)
        {
            if (s.ValueAt(StatLevel) is { } value) Stats.Add(new LabeledValue(s.Name, value));
        }
    }

    private void RebuildMultipliers()
    {
        Multipliers.Clear();
        if (SelectedSkill is { } skill)
        {
            foreach (EncoreSkillAttribute a in skill.Skill.Attributes)
            {
                if (a.ValueAt(SkillLevel) is { } value) Multipliers.Add(new LabeledValue(a.Name, value));
            }
        }
        OnPropertyChanged(nameof(HasMultipliers));
    }

    private void LoadPortrait(string? url)
    {
        if (url is null) return;
        _ = _images.LoadAsync(url, PortraitWidth, bitmap => Portrait = bitmap, token: _cts.Token);
    }

    private void LoadIcons()
    {
        var items = Skills.Cast<ChallengeImageItem>().Concat(Chain).Concat(Build?.ImageItems ?? []).ToList();
        foreach (ChallengeImageItem item in items)
        {
            // 「我的养成」的技能优先用 encore.moe 的同一技能图标（白色线稿，和技能页一致）；攻略站的图标在深色底上看不见。
            string? url = item.ImageUrl;
            if (item is BuildSkillItem skill && EncoreIconFor(skill) is { } encoreIcon)
            {
                url = encoreIcon;
                skill.UsesEncoreIcon = true;
            }
            if (url is null || item.Image is not null && item is not BuildSkillItem) continue;
            _ = _images.LoadAsync(url, IconWidth, bitmap => item.Image = bitmap, token: _cts.Token);
        }
    }

    /// <summary>按技能名匹配 encore.moe 的技能；名称对不上时按技能类型匹配（只有一项该类型时）。</summary>
    private string? EncoreIconFor(BuildSkillItem skill)
    {
        if (_character is not { } c) return null;
        EncoreSkill? match = skill.Name is { } name ? c.Skills.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.Ordinal)) : null;
        if (match is null && skill.Type is { } type)
        {
            var sameType = c.Skills.Where(s => string.Equals(s.Type, type, StringComparison.Ordinal)).Take(2).ToList();
            if (sameType.Count == 1) match = sameType[0];
        }
        return match?.IconUrl;
    }

    /// <summary>标签文字用压暗的游戏色，浅色底上也能看清。</summary>
    private static Color Darken(Color c) => Color.FromRgb((byte)(c.R * 0.72), (byte)(c.G * 0.72), (byte)(c.B * 0.72));
}
