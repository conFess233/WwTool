using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WwTool.Core.Abstractions;
using WwTool.Core.Extensions;
using WwTool.Core.Localization;
using WwTool.Core.Models.Domain;
using WwTool.Core.Services;
using WwTool.UI.Navigation;
using WwTool.UI.Pages.Roles;
using WwTool.UI.Services;

namespace WwTool.UI.Pages.Motor;

/// <summary>外观网格里的一格（皮肤、贴纸、挂饰、车架、专辑）。</summary>
public sealed class MotorTile
{
    public required string Id { get; init; }
    public required string IconPath { get; init; }
    public required string Name { get; init; }
    public required string Tag { get; init; }
    public bool IsEquipped { get; init; }

    /// <summary>贴纸 / 挂饰的安装部位图标（随包资源）。</summary>
    public string? PartIcon { get; init; }

    /// <summary>专辑收集进度（0–1）；其他类别为空。</summary>
    public double? Progress { get; init; }

    public bool HasPart => PartIcon is not null;
    public bool HasProgress => Progress is not null;
    public string AutomationId => "motor-" + Id;
}

/// <summary>左栏“当前装备”的一行。</summary>
public sealed record MotorFact(string Name, string Value);

/// <summary>右上角的解锁数量卡。</summary>
public sealed record UnlockCard(string Icon, string Title, string Value);

/// <summary>
/// 摩托页：载具等级与经验、当前装备、各类外观与车载音乐专辑（来自 <see cref="PlayerData"/> 快照）。
/// 快照只含已解锁的外观，所以网格只列已解锁项。
/// </summary>
public sealed partial class MotorViewModel : ObservableObject, INavigationAware
{
    private const string Icons = "avares://WwTool/Assets/Images/";

    private readonly PlayerData _player;
    private readonly GameDataService _catalog;
    private readonly IConfigService _config;
    private readonly LocalizationService _localization;

    public MotorViewModel(PlayerData player, GameDataService catalog, IConfigService config, LocalizationService localization)
    {
        _player = player;
        _catalog = catalog;
        _config = config;
        _localization = localization;
        Tabs =
        [
            new ChipOption("skins") { IsSelected = true },
            new ChipOption("stickers"),
            new ChipOption("decorations"),
            new ChipOption("frames"),
            new ChipOption("albums"),
        ];
        _player.SnapshotChanged += (_, _) => Refresh();
        _catalog.Changed += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(Refresh);
        _localization.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == LocalizationService.IndexerName) Refresh();
        };
        Refresh();
    }

    public ObservableCollection<ChipOption> Tabs { get; }

    [ObservableProperty] private bool _hasData;
    [ObservableProperty] private string _levelText = "--";
    [ObservableProperty] private double _exp;
    [ObservableProperty] private double _nextExp = 1;
    [ObservableProperty] private string _expText = string.Empty;
    [ObservableProperty] private IReadOnlyList<MotorFact> _equipment = [];
    [ObservableProperty] private IReadOnlyList<UnlockCard> _unlocks = [];
    [ObservableProperty] private IReadOnlyList<MotorTile> _tiles = [];
    [ObservableProperty] private bool _isTabEmpty;

    public void OnNavigatedTo(object? parameter) => _ = _player.EnsureLoadedAsync();

    public void OnNavigatedFrom()
    {
    }

    [RelayCommand]
    private void SelectTab(ChipOption tab)
    {
        foreach (ChipOption t in Tabs) t.IsSelected = t == tab;
        BuildTiles();
    }

    private void Refresh()
    {
        MotorSnapshot? motor = _player.Snapshot?.MotorData;
        HasData = motor is not null;
        string none = _localization["Common_Unavailable"];

        Tabs[0].Title = _localization["Motor_Tab_Skin"];
        Tabs[1].Title = _localization["Motor_Tab_Sticker"];
        Tabs[2].Title = _localization["Motor_Tab_Pendant"];
        Tabs[3].Title = _localization["Motor_Tab_Frame"];
        Tabs[4].Title = _localization["Motor_Tab_Album"];
        Tabs[0].Count = Count(motor?.Skins);
        Tabs[1].Count = Count(motor?.Stickers);
        Tabs[2].Count = Count(motor?.Decorations);
        Tabs[3].Count = Count(motor?.Frames);
        Tabs[4].Count = Count(_player.Snapshot?.MusicData);

        if (motor is null)
        {
            LevelText = none;
            Exp = 0;
            ExpText = string.Empty;
            Equipment = [];
            Unlocks = [];
            Tiles = [];
            return;
        }

        LevelText = "Lv." + motor.Level.ToString(CultureInfo.InvariantCulture);
        // 满级时接口给出的下一级经验为 0。
        bool max = motor.NextExp <= 0;
        NextExp = max ? 1 : motor.NextExp;
        Exp = max ? 1 : motor.Exp;
        ExpText = max ? _localization["Motor_MaxLevel"] : string.Format(_localization["Role_CollectionProgress"], motor.Exp, motor.NextExp);

        MotorSkinSnapshot? skin = motor.EquipSkin;
        Equipment =
        [
            new(_localization["Motor_Tab_Skin"], skin is null || skin.SkinId == 0 ? _localization["Motor_DefaultSkin"] : ItemName(skin.SkinId)),
            new(Trim(_localization["Motor_Quality"]), skin is null ? none : skin.Quality.ToString(CultureInfo.CurrentCulture)),
        ];
        Unlocks =
        [
            new(Icons + "Motor.png", _localization["Motor_UnlockedSkins"], Count(motor.Skins)),
            new(Icons + "Sticker.png", _localization["Motor_UnlockedStickers"], Count(motor.Stickers)),
            new(Icons + "Decorations.png", _localization["Motor_UnlockedOrnaments"], Count(motor.Decorations)),
            new(Icons + "MotorFrame.png", _localization["Motor_UnlockedFrames"], Count(motor.Frames)),
        ];
        BuildTiles();
    }

    private void BuildTiles()
    {
        MotorSnapshot? motor = _player.Snapshot?.MotorData;
        if (motor is null)
        {
            Tiles = [];
            IsTabEmpty = false;
            return;
        }
        int equipped = motor.EquipSkin?.SkinId ?? 0;
        string key = Tabs.First(t => t.IsSelected).Key;
        Tiles = key switch
        {
            "stickers" => (motor.Stickers ?? []).Select(x => Appearance(x.Id, x.IconPath, x.Quality, Icons + $"Part{Math.Clamp(x.PartId, 1, 3)}.png")).ToArray(),
            "decorations" => (motor.Decorations ?? []).Select(x => Appearance(x.Id, x.IconPath, x.Quality, Icons + $"Part{Math.Clamp(x.PartId + 4, 5, 6)}.png")).ToArray(),
            "frames" => (motor.Frames ?? []).Select(x => Appearance(x.Id, x.IconPath, x.Quality, null)).ToArray(),
            "albums" => (_player.Snapshot?.MusicData ?? []).Select(x => new MotorTile
            {
                Id = x.Id.ToString(CultureInfo.InvariantCulture),
                IconPath = x.IconPath,
                Name = ItemName(x.Id),
                Tag = string.Format(_localization["Role_CollectionProgress"], x.Count, x.TotalCount),
                Progress = x.TotalCount > 0 ? (double)x.Count / x.TotalCount : 0,
            }).ToArray(),
            _ => (motor.Skins ?? []).Select(x => new MotorTile
            {
                Id = x.SkinId.ToString(CultureInfo.InvariantCulture),
                IconPath = x.IconPath,
                Name = ItemName(x.SkinId),
                IsEquipped = x.SkinId == equipped,
                Tag = x.SkinId == equipped ? _localization["Motor_Equipped"] : string.Format(_localization["Motor_QualityValue"], x.Quality),
            }).ToArray(),
        };
        IsTabEmpty = Tiles.Count == 0;
    }

    private MotorTile Appearance(int id, string icon, int quality, string? part) => new()
    {
        Id = id.ToString(CultureInfo.InvariantCulture),
        IconPath = icon,
        Name = ItemName(id),
        Tag = string.Format(_localization["Motor_QualityValue"], quality),
        PartIcon = part,
    };

    private string ItemName(int id) =>
        _catalog.GetItemById(id)?.GetName(_config.User.AppLanguage.GetCode()) ?? id.ToString(CultureInfo.InvariantCulture);

    private static string Count<T>(IReadOnlyCollection<T>? items) => (items?.Count ?? 0).ToString(CultureInfo.CurrentCulture);

    // 1.x 文案带“: ”后缀（用作行内标签），这里作为键值行的名称去掉。
    private static string Trim(string label) => label.TrimEnd(' ', ':', '：');
}
