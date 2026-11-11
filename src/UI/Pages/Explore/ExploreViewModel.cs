using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using WwTool.Core.Localization;
using WwTool.Core.Models.Domain;
using WwTool.UI.Navigation;
using WwTool.UI.Services;
using WwTool.UI.Shell;

namespace WwTool.UI.Pages.Explore;

/// <summary>一种宝箱或潮汐之遗的数量卡。</summary>
public sealed record CountCard(string Icon, string Title, string Value, string? Caption, double Ratio);

/// <summary>
/// 探索页：声匣、奇藏箱、潮汐之遗的收集数量（来自 <see cref="PlayerData"/> 快照）。
/// </summary>
public sealed partial class ExploreViewModel : ObservableObject, INavigationAware, IFabProvider
{
    private const string Icons = "avares://WwTool/Assets/Images/";

    // 快照字典的键：1–4 依次为朴素、基准、精密、辉光；潮汐之遗 1–3 为初、中、高级。
    private static readonly (string Key, string Title, string Icon)[] BoxKinds =
    [
        ("1", "Exploration_BoxPlain", "Chest1.png"),
        ("2", "Exploration_BoxBasic", "Chest2.png"),
        ("3", "Exploration_BoxAdvanced", "Chest3.png"),
        ("4", "Exploration_BoxPremium", "Chest4.png"),
    ];

    private static readonly (string Key, string Title, string Icon)[] PhantomKinds =
    [
        ("1", "Explore_PhantomLow", "Phantom1.png"),
        ("2", "Explore_PhantomMid", "Phantom2.png"),
        ("3", "Explore_PhantomHigh", "Phantom3.png"),
    ];

    private readonly PlayerData _player;
    private readonly LocalizationService _localization;

    public ExploreViewModel(PlayerData player, AccountContext accounts, LocalizationService localization, INavigationService navigation)
    {
        // 悬浮按钮：收起态加号，展开为「刷新数据」（主，同步游戏数据）与「返回上一页」。
        FabActions =
        [
            new FabAction("refresh", "Fab_RefreshData", Icons + "Refresh.png", new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(player.SyncAsync), localization),
            new FabAction("back", "Fab_Back", "Icon.Back", new CommunityToolkit.Mvvm.Input.RelayCommand(() => navigation.GoBack()), localization),
        ];
        _player = player;
        _localization = localization;
        Accounts = accounts;
        _player.SnapshotChanged += (_, _) => Refresh();
        _localization.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == LocalizationService.IndexerName) Refresh();
        };
        Refresh();
    }

    public AccountContext Accounts { get; }
    public IReadOnlyList<FabAction> FabActions { get; }

    [ObservableProperty] private bool _hasData;
    [ObservableProperty] private string _soundBoxes = "--";
    [ObservableProperty] private string _boxesTotal = "--";
    [ObservableProperty] private string _basicSuffix = string.Empty;
    [ObservableProperty] private string _phantomTotal = "--";
    [ObservableProperty] private string _basicCaption = string.Empty;
    [ObservableProperty] private string _phantomCaption = string.Empty;
    [ObservableProperty] private IReadOnlyList<CountCard> _boxes = [];
    [ObservableProperty] private IReadOnlyList<CountCard> _phantoms = [];

    public void OnNavigatedTo(object? parameter) => _ = _player.EnsureLoadedAsync();

    public void OnNavigatedFrom()
    {
    }

    private void Refresh()
    {
        PlayerBaseSnapshot? b = _player.Snapshot?.Base;
        HasData = b is not null;
        if (b is null)
        {
            Boxes = [];
            Phantoms = [];
            return;
        }

        int boxes = Sum(b.Boxes);
        int basic = Sum(b.BasicBoxes);
        int phantoms = Sum(b.PhantomBoxes);
        SoundBoxes = Format(b.SoundBox);
        BoxesTotal = Format(boxes);
        BasicSuffix = string.Format(_localization["Explore_BasicSuffix"], Format(basic));
        PhantomTotal = Format(phantoms);
        BasicCaption = string.Format(_localization["Explore_BasicCaption"], Format(basic));
        PhantomCaption = string.Format(_localization["Explore_PhantomCaption"], Format(phantoms));

        Boxes = BoxKinds.Select(k =>
        {
            int count = Get(b.Boxes, k.Key);
            double ratio = boxes > 0 ? (double)count / boxes : 0;
            return new CountCard(Icons + k.Icon, _localization[k.Title], Format(count),
                string.Format(_localization["Explore_Share"], ratio.ToString("P0", CultureInfo.CurrentCulture)), ratio);
        }).ToArray();
        Phantoms = PhantomKinds.Select(k =>
            new CountCard(Icons + k.Icon, _localization[k.Title], Format(Get(b.PhantomBoxes, k.Key)), null, 0)).ToArray();
    }

    private static int Sum(Dictionary<string, int>? values) => values?.Values.Sum() ?? 0;

    private static int Get(Dictionary<string, int>? values, string key) =>
        values is not null && values.TryGetValue(key, out int value) ? value : 0;

    private static string Format(int value) => value.ToString("N0", CultureInfo.CurrentCulture);
}
