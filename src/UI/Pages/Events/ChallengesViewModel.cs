using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WwTool.Core.Abstractions.Platform;
using WwTool.Core.Extensions;
using WwTool.Core.Localization;
using WwTool.Core.Services.Challenges;
using WwTool.UI.Feedback;
using WwTool.UI.Services;

namespace WwTool.UI.Pages.Events;

/// <summary>
/// 活动页的「挑战」栏：逆境深塔、冥歌海墟、终焉矩阵当期（可前后翻）的敌人、增益与评分规则，数据来自 encore.moe（<c>docs/API/Encore.md</c>）。
/// <para>
/// 接口不支持条件请求，所以每次打开都先显示缓存，再重新获取并与缓存比较，有变化才替换；同一项在本次运行
/// <see cref="RefreshInterval"/> 内获取过的不再重复请求（手动刷新除外）。图片同样先显示缓存再向服务器校验。
/// </para>
/// </summary>
public sealed partial class ChallengesViewModel : ObservableObject
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(30);

    private readonly EncoreService _encore;
    private readonly EncoreImages _images;
    private readonly LocalizationService _localization;
    private readonly IAppLauncher _launcher;
    private readonly ErrorReporter _errors;

    private readonly Dictionary<ChallengeKind, IReadOnlyList<ChallengeSeason>> _seasons = [];
    private readonly Dictionary<ChallengeKind, int> _chosen = [];
    private readonly Dictionary<string, DateTime> _attempted = [];
    private string _language = string.Empty;
    private int _version;
    private bool _active;
    private CancellationTokenSource? _imageLoads;

    public ChallengesViewModel(EncoreService encore, EncoreImages images, LocalizationService localization, IAppLauncher launcher, ErrorReporter errors)
    {
        _encore = encore;
        _images = images;
        _localization = localization;
        _launcher = launcher;
        _errors = errors;
        _localization.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LocalizationService.CurrentLanguage) && _active) Show(force: false);
        };
    }

    // ── 玩法切换 ──────────────────────────────────────────

    [ObservableProperty] private bool _isTower = true;
    [ObservableProperty] private bool _isWastes;
    [ObservableProperty] private bool _isMatrix;

    partial void OnIsTowerChanged(bool value) { if (value && _active) Show(force: false); }
    partial void OnIsWastesChanged(bool value) { if (value && _active) Show(force: false); }
    partial void OnIsMatrixChanged(bool value) { if (value && _active) Show(force: false); }

    private ChallengeKind Kind => IsWastes ? ChallengeKind.Wastes : IsMatrix ? ChallengeKind.Matrix : ChallengeKind.Tower;

    // ── 当前一期 ──────────────────────────────────────────

    [ObservableProperty] private ChallengeSeason? _season;
    [ObservableProperty] private string _seasonTitle = string.Empty;
    [ObservableProperty] private string _seasonInfo = string.Empty;
    [ObservableProperty] private string _seasonStatus = string.Empty;
    [ObservableProperty] private bool _isActiveSeason;
    [ObservableProperty] private bool _isUpcomingSeason;
    [ObservableProperty] private bool _canPrevious;
    [ObservableProperty] private bool _canNext;
    [ObservableProperty] private string _updatedText = string.Empty;

    // ── 状态 ──────────────────────────────────────────────

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private bool _failed;
    [ObservableProperty] private bool _hasContent;

    public bool ShowLoading => !HasContent && IsLoading;
    public bool ShowFailed => !HasContent && !IsLoading && Failed;
    public bool ShowEmpty => !HasContent && !IsLoading && !Failed;

    partial void OnIsLoadingChanged(bool value) => NotifyStates();
    partial void OnFailedChanged(bool value) => NotifyStates();
    partial void OnHasContentChanged(bool value) => NotifyStates();

    private void NotifyStates()
    {
        OnPropertyChanged(nameof(ShowLoading));
        OnPropertyChanged(nameof(ShowFailed));
        OnPropertyChanged(nameof(ShowEmpty));
    }

    // ── 内容 ──────────────────────────────────────────────

    public ObservableCollection<TowerAreaItem> TowerAreas { get; } = [];
    public ObservableCollection<WastesGroupItem> WastesGroups { get; } = [];
    public ObservableCollection<TokenItem> WastesTokens { get; } = [];
    public ObservableCollection<MatrixModeItem> MatrixModes { get; } = [];
    public ObservableCollection<MatrixRoleItem> MatrixRoles { get; } = [];

    [ObservableProperty] private bool _hasTower;
    [ObservableProperty] private bool _hasWastes;
    [ObservableProperty] private bool _hasTokens;
    [ObservableProperty] private bool _hasMatrix;
    [ObservableProperty] private bool _hasRoles;

    // ── 入口 ──────────────────────────────────────────────

    /// <summary>栏目可见时调用：先显示缓存，过期则后台重新获取。</summary>
    public void Activate()
    {
        _active = true;
        Show(force: false);
    }

    public void Deactivate() => _active = false;

    /// <summary>手动刷新：当前玩法的各期列表与这一期的详情都重新获取。</summary>
    public Task RefreshAsync() => ShowAsync(force: true);

    [RelayCommand]
    private void Retry() => Show(force: true);

    [RelayCommand]
    private void Previous() => Step(-1);

    [RelayCommand]
    private void Next() => Step(1);

    [RelayCommand]
    private void OpenSite()
    {
        if (Uri.TryCreate(_encore.PageUrl(Kind, Language), UriKind.Absolute, out Uri? uri)) _launcher.OpenUrl(uri);
    }

    private void Step(int delta)
    {
        if (Season is not { } current || !_seasons.TryGetValue(Kind, out var seasons)) return;
        int index = IndexOf(seasons, current.Id) + delta;
        if (index < 0 || index >= seasons.Count) return;
        _chosen[Kind] = seasons[index].Id;
        _ = ShowDetailAsync(Kind, seasons[index], force: false, ++_version);
    }

    private static int IndexOf(IReadOnlyList<ChallengeSeason> seasons, int id)
    {
        for (int i = 0; i < seasons.Count; i++)
        {
            if (seasons[i].Id == id) return i;
        }
        return -1;
    }

    private void Show(bool force) => _ = ShowAsync(force);

    private string Language => _localization.CurrentLanguage.GetCode();

    private bool Due(string key, bool force)
    {
        if (!force && _attempted.TryGetValue(key, out DateTime at) && DateTime.UtcNow - at < RefreshInterval) return false;
        _attempted[key] = DateTime.UtcNow;
        return true;
    }

    /// <summary>显示当前玩法：各期列表（缓存 → 必要时刷新）→ 选定一期 → 详情。</summary>
    private async Task ShowAsync(bool force)
    {
        ChallengeKind kind = Kind;
        int version = ++_version;
        string language = EncoreService.RequestLanguage(Language);
        if (language != _language)
        {
            _language = language;
            _seasons.Clear();
            _attempted.Clear();
        }

        if (!_seasons.ContainsKey(kind))
        {
            try
            {
                if (_encore.LoadSeasons(kind, language) is { } cached) _seasons[kind] = cached.Value;
            }
            catch (Exception ex)
            {
                _errors.Report(ex, _localization["Events_ViewChallenges"], notifyUser: false);
            }
        }
        if (_seasons.TryGetValue(kind, out var known)) await ShowDetailAsync(kind, Pick(kind, known), force, version);
        else ClearContent();

        if (!Due($"{language}|{kind}", force)) return;
        IsLoading = !_seasons.ContainsKey(kind);
        IsRefreshing = true;
        EncoreData<IReadOnlyList<ChallengeSeason>>? fresh = null;
        try
        {
            fresh = await Task.Run(() => _encore.RefreshSeasonsAsync(kind, language));
        }
        catch (Exception ex)
        {
            _errors.Report(ex, _localization["Events_ViewChallenges"], notifyUser: false);
        }
        IsRefreshing = false;
        if (version != _version) return;
        if (fresh is not null && (fresh.Changed || !_seasons.ContainsKey(kind)))
        {
            _seasons[kind] = fresh.Value;
            await ShowDetailAsync(kind, Pick(kind, fresh.Value), force, version);
        }
        else if (fresh is null && !_seasons.ContainsKey(kind))
        {
            IsLoading = false;
            Failed = true;
        }
        else if (Season is { } season && version == _version)
        {
            // 列表没变：刷新时间与「上一期 / 下一期」仍要更新。
            ApplySeason(season, _seasons[kind]);
        }
    }

    /// <summary>用户翻过期数时保持选择；否则取正在进行的一期（矩阵取最新一期）。</summary>
    private ChallengeSeason? Pick(ChallengeKind kind, IReadOnlyList<ChallengeSeason> seasons)
    {
        if (_chosen.TryGetValue(kind, out int id) && seasons.FirstOrDefault(s => s.Id == id) is { } chosen) return chosen;
        return EncoreParser.Current(seasons, DateTimeOffset.UtcNow);
    }

    private async Task ShowDetailAsync(ChallengeKind kind, ChallengeSeason? season, bool force, int version)
    {
        if (season is null)
        {
            Season = null;
            ClearContent();
            IsLoading = false;
            return;
        }
        string language = EncoreService.RequestLanguage(Language);
        ApplySeason(season, _seasons.GetValueOrDefault(kind) ?? [season]);
        Failed = false;

        EncoreData<object>? cached = null;
        try
        {
            cached = _encore.LoadDetail(kind, season.Id, language);
        }
        catch (Exception ex)
        {
            _errors.Report(ex, _localization["Events_ViewChallenges"], notifyUser: false);
        }
        if (cached is not null) ApplyDetail(cached);
        else ClearContent();

        if (!Due($"{language}|{kind}|{season.Id}", force))
        {
            IsLoading = false;
            if (cached is null) Failed = true;
            return;
        }
        IsLoading = cached is null;
        IsRefreshing = true;
        EncoreData<object>? fresh = null;
        try
        {
            fresh = await Task.Run(() => _encore.RefreshDetailAsync(kind, season.Id, language));
        }
        catch (Exception ex)
        {
            _errors.Report(ex, _localization["Events_ViewChallenges"], notifyUser: false);
        }
        IsRefreshing = false;
        if (version != _version) return;
        IsLoading = false;
        if (fresh is not null)
        {
            if (fresh.Changed || cached is null) ApplyDetail(fresh);
            else UpdatedText = Updated(fresh.FetchedAtUtc);
        }
        else if (cached is null)
        {
            Failed = true;
        }
    }

    // ── 期数信息 ──────────────────────────────────────────

    private void ApplySeason(ChallengeSeason season, IReadOnlyList<ChallengeSeason> seasons)
    {
        Season = season;
        int index = IndexOf(seasons, season.Id);
        CanPrevious = index > 0;
        CanNext = index >= 0 && index < seasons.Count - 1;
        UpdateSeasonText(DateTimeOffset.UtcNow);
    }

    /// <summary>期数标题、起止时间与剩余时间（每 30 秒由活动页的时钟调用）。</summary>
    public void UpdateLive()
    {
        if (Season is not null) UpdateSeasonText(DateTimeOffset.UtcNow);
    }

    private void UpdateSeasonText(DateTimeOffset now)
    {
        if (Season is not { } s) return;
        if (s.Kind == ChallengeKind.Matrix)
        {
            SeasonTitle = s.Name;
            SeasonInfo = s.SeasonName is { } name && s.EndVersion is { } v
                ? string.Format(_localization["Challenge_MatrixInfo"], name, v)
                : s.SeasonName ?? string.Empty;
            SeasonStatus = _localization["Challenge_MatrixNoDate"];
            IsActiveSeason = IsUpcomingSeason = false;
            return;
        }
        SeasonTitle = string.Format(_localization["Challenge_Season"], s.Id);
        string Fmt(DateTimeOffset t) => t.ToLocalTime().ToString("MM/dd HH:mm", CultureInfo.InvariantCulture);
        SeasonInfo = s.Start is { } start && s.End is { } end ? $"{Fmt(start)} – {Fmt(end)}" : string.Empty;
        IsActiveSeason = s.IsActive(now);
        IsUpcomingSeason = s.IsUpcoming(now);
        SeasonStatus = IsActiveSeason ? string.Format(_localization["Events_Remaining"], EventsText.Duration(_localization, s.End!.Value - now))
            : IsUpcomingSeason ? string.Format(_localization["Challenge_Upcoming"], EventsText.Duration(_localization, s.Start!.Value - now))
            : _localization["Events_Ended"];
    }

    private string Updated(DateTimeOffset at) =>
        string.Format(_localization["Events_Updated"], at.ToLocalTime().ToString("MM/dd HH:mm", CultureInfo.InvariantCulture));

    // ── 内容构建 ──────────────────────────────────────────

    private void ClearContent()
    {
        _imageLoads?.Cancel();
        TowerAreas.Clear();
        WastesGroups.Clear();
        WastesTokens.Clear();
        MatrixModes.Clear();
        MatrixRoles.Clear();
        HasTower = HasWastes = HasTokens = HasMatrix = HasRoles = HasContent = false;
        UpdatedText = string.Empty;
    }

    private void ApplyDetail(EncoreData<object> data)
    {
        ClearContent();
        UpdatedText = Updated(data.FetchedAtUtc);
        var images = new List<ChallengeImageItem>();
        switch (data.Value)
        {
            case TowerDetail tower:
                foreach (TowerArea area in tower.Areas) TowerAreas.Add(BuildArea(area, images));
                HasTower = TowerAreas.Count > 0;
                break;
            case WastesDetail wastes:
                foreach (WastesGroup group in wastes.Groups) WastesGroups.Add(BuildGroup(group, images));
                foreach (WastesToken token in wastes.Tokens)
                {
                    var item = new TokenItem(token.Icon, token.Name, token.Quality >= 5);
                    WastesTokens.Add(item);
                    images.Add(item);
                }
                HasWastes = WastesGroups.Count > 0;
                HasTokens = WastesTokens.Count > 0;
                break;
            case MatrixDetail matrix:
                foreach (MatrixMode mode in matrix.Modes) MatrixModes.Add(BuildMode(mode, images));
                foreach (MatrixRole role in matrix.Roles)
                {
                    var item = new MatrixRoleItem(role.Icon, role.Name, role.Quality >= 5, RichText.Of(role.Description));
                    MatrixRoles.Add(item);
                    images.Add(item);
                }
                HasMatrix = MatrixModes.Count > 0;
                HasRoles = MatrixRoles.Count > 0;
                break;
        }
        HasContent = HasTower || HasWastes || HasMatrix;
        LoadImages(images);
    }

    private void LoadImages(List<ChallengeImageItem> items)
    {
        _imageLoads?.Cancel();
        var cts = new CancellationTokenSource();
        _imageLoads = cts;
        _ = LoadImagesAsync(items, cts.Token);
    }

    private async Task LoadImagesAsync(List<ChallengeImageItem> items, CancellationToken token)
    {
        // 同一张图（同一敌人出现在多层）只请求一次；几张并行。
        var byUrl = items.Where(i => i.ImageUrl is not null).GroupBy(i => i.ImageUrl!).ToList();
        using var gate = new SemaphoreSlim(6);
        await Task.WhenAll(byUrl.Select(async group =>
        {
            await gate.WaitAsync(token).ConfigureAwait(true);
            try
            {
                if (token.IsCancellationRequested) return;
                await _images.LoadAsync(group.Key, 128, bitmap =>
                {
                    if (token.IsCancellationRequested) return;
                    foreach (ChallengeImageItem item in group) item.Image = bitmap;
                }, token: token).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                gate.Release();
            }
        })).ConfigureAwait(true);
    }

    private string ElementName(int id) => id is >= 0 and <= 6 ? _localization["Challenge_Element_" + id.ToString(CultureInfo.InvariantCulture)] : string.Empty;

    private MonsterItem Monster(ChallengeMonster m, List<ChallengeImageItem> images)
    {
        ChallengeElement? element = m.Elements.FirstOrDefault();
        int elementId = element?.Id ?? -1;
        string elementName = element is { Name.Length: > 0 } ? element.Name : ElementName(elementId);
        var lines = new List<string> { m.Name };
        var head = new List<string>();
        if (m.Level is { } level) head.Add(string.Format(_localization["Challenge_Level"], level));
        if (m.Life is { } life) head.Add(string.Format(_localization["Challenge_Life"], life.ToString("N0", CultureInfo.CurrentCulture)));
        if (elementName.Length > 0) head.Add(elementName);
        if (head.Count > 0) lines.Add(string.Join(" · ", head));
        foreach (ChallengeResistance r in m.Resistances) lines.Add($"{r.Name} {r.Percent:0.#}%");

        // 某一属性抗性明显高于其余（如衍射 60%、其他 20%）时，在头像下标出来。
        string? highlight = null;
        if (m.Resistances.Count >= 3)
        {
            double common = m.Resistances.GroupBy(r => r.Percent).OrderByDescending(g => g.Count()).First().Key;
            var high = m.Resistances.Where(r => r.Percent > common).ToList();
            if (high.Count is > 0 and <= 2) highlight = string.Join(" ", high.Select(r => $"{ElementName(r.ElementId)} {r.Percent:0.#}%"));
        }
        var item = new MonsterItem(m.Icon, m.Name, ChallengeElements.Brush(elementId), elementName, string.Join(Environment.NewLine, lines), highlight);
        images.Add(item);
        return item;
    }

    private TowerAreaItem BuildArea(TowerArea area, List<ChallengeImageItem> images)
    {
        // 各层都有的增益只在区域上显示一次。
        var shared = area.Floors.Count > 1
            ? area.Floors.Skip(1).Aggregate(area.Floors[0].Buffs.ToList(), (acc, f) => acc.Where(f.Buffs.Contains).ToList())
            : [];
        var floors = area.Floors.Select(f => new TowerFloorItem(
            string.Format(_localization["Challenge_Floor"], f.Floor),
            string.Format(_localization["Challenge_Cost"], f.Cost),
            f.Monsters.Select(m => Monster(m, images)).ToList(),
            f.Buffs.Where(b => !shared.Contains(b)).Select(RichText.Of).ToList(),
            f.Targets.Select((t, i) => new TowerTargetItem(new string('★', i + 1), RichText.Of(t.Text))).ToList())).ToList();
        return new TowerAreaItem(area.Name, shared.Select(RichText.Of).ToList(), floors);
    }

    private WastesGroupItem BuildGroup(WastesGroup group, List<ChallengeImageItem> images)
    {
        var levels = group.Levels.Select(l =>
        {
            string grades = string.Join(" · ", l.Grades.Select(g => $"{g.Grade} {g.Score.ToString("N0", CultureInfo.CurrentCulture)}"));
            var stages = l.Stages.Select((s, i) => new WastesStageItem(
                string.Format(_localization["Challenge_Stage"], i + 1),
                RichText.Of(s.Description),
                s.Description.Length > 0,
                s.Monsters.Select(m => Monster(m, images)).ToList())).ToList();
            return new WastesLevelItem(l.Title, l.Endless, grades, RichText.Of(l.Description), l.Description.Length > 0, stages);
        }).ToList();
        return new WastesGroupItem(group.Name, levels);
    }

    private MatrixModeItem BuildMode(MatrixMode mode, List<ChallengeImageItem> images)
    {
        string team = mode.TeamLimit > 0 ? string.Format(_localization["Challenge_TeamLimit"], mode.TeamLimit) : _localization["Challenge_TeamUnlimited"];
        string grades = mode.Thresholds.Count > 0
            ? string.Format(_localization["Challenge_Grades"], string.Join(" / ", mode.Thresholds.Select(t => t.ToString("N0", CultureInfo.CurrentCulture))))
            : string.Empty;
        var buffs = mode.Buffs.Select(b =>
        {
            var item = new MatrixBuffItem(b.Icon, b.Name, RichText.Of(b.Description));
            images.Add(item);
            return item;
        }).ToList();

        MatrixEnemyItem Enemy(MatrixEnemy e)
        {
            string skills = string.Join("<br>", e.Skills.Select(s => s.Title.Length > 0 ? $"<b>{s.Title}</b>：{s.Description}" : s.Description));
            var item = new MatrixEnemyItem(e.Icon, e.Name, ChallengeElements.Brush(e.ElementId), ElementName(e.ElementId), e.Tags,
                RichText.Of(skills), skills.Length > 0,
                e.KillScore > 0 ? string.Format(_localization["Challenge_KillScore"], e.KillScore) : null,
                e.Features.Count > 0 ? string.Format(_localization["Challenge_Feature"], string.Join(_localization["Events_ListSeparator"], e.Features)) : null);
            images.Add(item);
            return item;
        }

        // 多轮的敌人相同、只是等级不同时合成一组，标出各轮等级。
        var rounds = new List<MatrixRoundItem>();
        bool sameEnemies = mode.Rounds.Count > 1 && mode.Rounds.All(r => r.Enemies.Select(e => e.Name).SequenceEqual(mode.Rounds[0].Enemies.Select(e => e.Name)));
        if (mode.Rounds.Count == 1 || sameEnemies)
        {
            string title = string.Format(_localization["Challenge_Levels"], string.Join(" / ", mode.Rounds.Select(r => r.Level)));
            if (mode.Rounds.Count > 0) rounds.Add(new MatrixRoundItem(title, mode.Rounds[0].Enemies.Select(Enemy).ToList()));
        }
        else
        {
            foreach (MatrixRound r in mode.Rounds)
                rounds.Add(new MatrixRoundItem(string.Format(_localization["Challenge_Round"], r.Round, r.Level), r.Enemies.Select(Enemy).ToList()));
        }
        return new MatrixModeItem(mode.Name, team, grades, buffs, rounds);
    }
}

/// <summary>活动页共用的时间文字。</summary>
internal static class EventsText
{
    public static string Duration(LocalizationService localization, TimeSpan span)
    {
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;
        if (span.TotalDays >= 1)
            return span.Hours > 0
                ? string.Format(localization["Events_DaysHours"], (int)span.TotalDays, span.Hours)
                : string.Format(localization["Events_Days"], (int)span.TotalDays);
        if (span.TotalHours >= 1) return string.Format(localization["Events_Hours"], (int)span.TotalHours);
        return string.Format(localization["Events_Minutes"], Math.Max(1, (int)Math.Ceiling(span.TotalMinutes)));
    }
}
