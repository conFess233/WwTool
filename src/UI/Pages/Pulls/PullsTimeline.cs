using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WwTool.Core.Enums;
using WwTool.Core.Localization;
using WwTool.Core.Services;
using WwTool.Core.Services.Gacha;
using WwTool.UI.Pages.Roles;

namespace WwTool.UI.Pages.Pulls;

/// <summary>
/// 时间线上的一个节点：一次五星（或末尾的「未出金」）。节点上下交替排列；外圈、连线与轨道点按抽数评价着色。
/// </summary>
public sealed class TimelineNode
{
    public required FiveStarCard? Card { get; init; }
    public required bool IsUp { get; init; }
    public required IBrush Brush { get; init; }
    public required string DateText { get; init; }
    public required string RatingText { get; init; }

    /// <summary>与上一个节点不在同一个月时显示的月份，如「2026-07」。</summary>
    public string? MonthLabel { get; init; }

    /// <summary>末尾「未出金」节点：已垫抽数与下一金的保底类型。</summary>
    public int PendingPity { get; init; }
    public string? PendingText { get; init; }

    /// <summary>悬停提示：卡池、获得时间与抽数评价。</summary>
    public string Tip { get; init; } = string.Empty;

    public bool IsDown => !IsUp;
    public bool IsPending => Card is null;
    public bool IsRecord => Card is not null;
    public bool HasMonth => MonthLabel is not null;
    public int Pity => Card?.Pity ?? PendingPity;
    public bool IsCharacter => Card is { } c && !FiveStarHistory.IsWeapon(c.ResourceId);
}

/// <summary>
/// 抽卡统计的时间线视图：总览为全部卡池（可切角色 / 武器），单卡池只显示该卡池并在末尾显示未出金进度。
/// 最新在右；点击角色头像打开角色详情弹窗。
/// </summary>
public sealed partial class PullsTimeline(LocalizationService localization, CharacterDetails details, GameDataService catalog) : ObservableObject
{
    private PullsSnapshot? _snapshot;
    private CardPoolType? _pool;

    public ObservableCollection<TimelineNode> Nodes { get; } = [];

    /// <summary>总览下显示武器（否则显示角色）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsRoles))]
    private bool _showsWeapons;

    [ObservableProperty] private bool _isOverview = true;
    [ObservableProperty] private int _roleCount;
    [ObservableProperty] private int _weaponCount;
    [ObservableProperty] private bool _isEmpty;

    public bool ShowsRoles => !ShowsWeapons;

    /// <summary>节点重建后触发，视图据此滚到最右。</summary>
    public event EventHandler? Rebuilt;

    [RelayCommand]
    private void ShowRoles()
    {
        ShowsWeapons = false;
        Rebuild();
    }

    [RelayCommand]
    private void ShowWeapons()
    {
        ShowsWeapons = true;
        Rebuild();
    }

    [RelayCommand]
    private async Task OpenDetail(TimelineNode node)
    {
        if (node.Card is not { } card || !node.IsCharacter) return;
        int star = catalog.GetItemById(card.ResourceId)?.qualityLevel ?? 5;
        await details.ShowAsync(new CharacterDetailRequest(card.ResourceId, card.Name, card.IconPath, star));
    }

    /// <summary>更新数据：<paramref name="pool"/> 为 null 时为总览。</summary>
    public void Update(PullsSnapshot? snapshot, CardPoolType? pool)
    {
        _snapshot = snapshot;
        _pool = pool;
        IsOverview = pool is null;
        Rebuild();
    }

    private void Rebuild()
    {
        Nodes.Clear();
        if (_snapshot is not { } s)
        {
            IsEmpty = true;
            return;
        }

        IReadOnlyList<FiveStarCard> cards;
        if (_pool is { } pool)
        {
            List<FiveStarCard> list = PoolRecordsViewModel.BuildCards(s.Pools[pool].PoolStatistics, pool);
            list.Reverse();
            cards = list;
        }
        else
        {
            IReadOnlyList<FiveStarCard> all = FiveStarHistory.All(s);
            RoleCount = all.Count(c => !FiveStarHistory.IsWeapon(c.ResourceId));
            WeaponCount = all.Count - RoleCount;
            cards = all.Where(c => FiveStarHistory.IsWeapon(c.ResourceId) == ShowsWeapons).ToList();
        }

        string? lastMonth = null;
        foreach (FiveStarCard card in cards)
        {
            string? month = card.Time?.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            PityRating rating = PityRatings.Of(card.Pity);
            string ratingText = localization[PityRatings.NameKey(rating)];
            string poolName = localization[PoolNames.TitleKey(card.Pool)];
            Nodes.Add(new TimelineNode
            {
                Card = card,
                IsUp = Nodes.Count % 2 == 0,
                Brush = PityRatings.Brush(rating),
                DateText = card.Time?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "—",
                RatingText = ratingText,
                MonthLabel = month is not null && month != lastMonth ? month : null,
                Tip = string.Format(CultureInfo.CurrentCulture, localization["Pulls_TimelineTip"], poolName, card.TimeText, card.Pity, ratingText),
            });
            if (month is not null) lastMonth = month;
        }

        // 单卡池：末尾显示自上一个五星起的进度。
        if (_pool is { } p && s.Pools[p].PoolStatistics.HitGoldDatas.Count > 0)
        {
            PoolProgress progress = PullsSummaryCalculator.Progress(s, p);
            if (progress.CurrentPity > 0)
            {
                string next = progress.NextIsGuaranteed switch
                {
                    true => localization["Pulls_NextGuaranteed"],
                    false => localization["Pulls_NextSoftPity"],
                    null => string.Empty,
                };
                Nodes.Add(new TimelineNode
                {
                    Card = null,
                    IsUp = Nodes.Count % 2 == 0,
                    Brush = PityRatings.Brush(progress.CurrentPity),
                    DateText = string.Empty,
                    RatingText = string.Empty,
                    PendingPity = progress.CurrentPity,
                    PendingText = next,
                });
            }
        }

        IsEmpty = Nodes.Count == 0;
        Rebuilt?.Invoke(this, EventArgs.Empty);
    }
}
