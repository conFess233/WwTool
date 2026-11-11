using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WwTool.Core.Localization;
using WwTool.UI.Feedback;

namespace WwTool.UI.Shell;

/// <summary>
/// 悬浮按钮里的一个操作胶囊。离按钮最远的第一项为主操作（强调色底）；危险操作用危险色文字，执行前确认。
/// </summary>
/// <remarks>图标为 <c>Icon.*</c>（线性图标几何资源键）或 <c>avares://</c> 位图地址。</remarks>
public sealed class FabAction : ObservableObject
{
    private readonly LocalizationService _localization;

    public FabAction(string key, string titleKey, string icon, ICommand command, LocalizationService localization,
        bool isDanger = false, string? confirmMessageKey = null)
    {
        Key = key;
        TitleKey = titleKey;
        Icon = icon;
        Command = command;
        IsDanger = isDanger;
        ConfirmMessageKey = confirmMessageKey;
        _localization = localization;
        _localization.PropertyChanged += OnLocalizationChanged;
    }

    public string Key { get; }

    public string TitleKey { get; }

    public string Icon { get; }

    /// <summary>线性图标几何；位图图标时为 null。</summary>
    public Geometry? IconGeometry => FabIcons.Geometry(Icon);

    /// <summary>位图图标地址；线性图标时为 null。</summary>
    public string? IconAsset => FabIcons.Geometry(Icon) is null ? Icon : null;

    public ICommand Command { get; }

    public bool IsDanger { get; }

    /// <summary>危险动作的确认文案键；为空时用标题。</summary>
    public string? ConfirmMessageKey { get; }

    public string Title => _localization[TitleKey];

    public string AutomationId => "fab-" + Key;

    private bool _isPrimary;

    /// <summary>是否为首项（页面主操作），由悬浮按钮设置。</summary>
    public bool IsPrimary
    {
        get => _isPrimary;
        internal set => SetProperty(ref _isPrimary, value);
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == LocalizationService.IndexerName) OnPropertyChanged(nameof(Title));
    }
}

/// <summary>
/// 扩展态悬浮按钮的外观（高 56、图标 + 文字）。<see cref="ExecutesPrimary"/> 为 true 时点击直接执行首项
/// （设置页「保存设置」）；否则点击展开菜单（抽卡统计「获取记录」）。
/// </summary>
public sealed record FabTrigger(string TitleKey, string Icon, bool ExecutesPrimary);

/// <summary>
/// 页面视图模型实现此接口来声明悬浮按钮；未实现或列表为空的页面不显示悬浮按钮。
/// </summary>
public interface IFabProvider
{
    IReadOnlyList<FabAction> FabActions { get; }

    /// <summary>扩展态外观；为 null 时是收起态的圆形加号。</summary>
    FabTrigger? FabTrigger => null;

    /// <summary>主操作进行中（按钮内显示加载形象）。实现 <see cref="INotifyPropertyChanged"/> 时会跟随变化。</summary>
    bool IsFabBusy => false;
}

internal static class FabIcons
{
    public static Geometry? Geometry(string icon) =>
        icon.StartsWith("Icon.", StringComparison.Ordinal)
        && Application.Current?.TryGetResource(icon, null, out object? value) == true
            ? value as Geometry
            : null;
}

/// <summary>
/// 右下悬浮按钮（开发文档「悬浮按钮」）：收起态圆形加号、扩展态图标 + 文字、展开态操作胶囊菜单与遮罩。
/// </summary>
public sealed partial class FabMenuViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;
    private readonly LocalizationService _localization;
    private IFabProvider? _provider;

    public FabMenuViewModel(IDialogService dialogs, LocalizationService localization)
    {
        _dialogs = dialogs;
        _localization = localization;
        _localization.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == LocalizationService.IndexerName)
            {
                OnPropertyChanged(nameof(ToggleName));
                OnPropertyChanged(nameof(TriggerTitle));
            }
        };
    }

    /// <summary>操作胶囊，自上而下的显示顺序：首项（主操作）离按钮最远。</summary>
    public ObservableCollection<FabAction> Actions { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleName))]
    private bool _isExpanded;

    /// <summary>主操作进行中，扩展按钮显示加载形象。</summary>
    [ObservableProperty]
    private bool _isBusy;

    public bool IsVisible => Actions.Count > 0;

    public FabTrigger? Trigger { get; private set; }

    /// <summary>扩展态（带文字）。</summary>
    public bool IsExtended => Trigger is not null;

    /// <summary>收起态（圆形加号）。</summary>
    public bool IsRound => Trigger is null;

    public string TriggerTitle => Trigger is null ? string.Empty : _localization[Trigger.TitleKey];

    public Geometry? TriggerGeometry => Trigger is null ? null : FabIcons.Geometry(Trigger.Icon);

    public FabAction? Primary => Actions.Count > 0 ? Actions[0] : null;

    /// <summary>展开按钮的可访问名称。</summary>
    public string ToggleName => IsExtended && !IsExpanded ? TriggerTitle : _localization[IsExpanded ? "Fab_Close" : "Fab_Open"];

    /// <summary>切换页面时由 Shell 调用。</summary>
    public void SetProvider(IFabProvider? provider)
    {
        if (_provider is INotifyPropertyChanged oldNotify) oldNotify.PropertyChanged -= OnProviderChanged;
        _provider = provider;
        if (provider is INotifyPropertyChanged notify) notify.PropertyChanged += OnProviderChanged;

        IsExpanded = false;
        Actions.Clear();
        foreach (FabAction action in provider?.FabActions ?? []) Actions.Add(action);
        for (int i = 0; i < Actions.Count; i++) Actions[i].IsPrimary = i == 0;
        Trigger = provider?.FabTrigger;
        IsBusy = provider?.IsFabBusy ?? false;
        OnPropertyChanged(nameof(IsVisible));
        OnPropertyChanged(nameof(Trigger));
        OnPropertyChanged(nameof(IsExtended));
        OnPropertyChanged(nameof(IsRound));
        OnPropertyChanged(nameof(TriggerTitle));
        OnPropertyChanged(nameof(TriggerGeometry));
        OnPropertyChanged(nameof(Primary));
        OnPropertyChanged(nameof(ToggleName));
    }

    private void OnProviderChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IFabProvider.IsFabBusy)) IsBusy = _provider?.IsFabBusy ?? false;
    }

    /// <summary>点击主按钮：扩展态且直接执行时执行首项，否则展开 / 收起菜单。</summary>
    [RelayCommand]
    private async Task ToggleAsync()
    {
        if (!IsExpanded && Trigger is { ExecutesPrimary: true } && Primary is { } primary)
        {
            await ExecuteAsync(primary);
            return;
        }
        IsExpanded = !IsExpanded;
    }

    [RelayCommand]
    private void Collapse() => IsExpanded = false;

    [RelayCommand]
    private async Task ExecuteAsync(FabAction? action)
    {
        if (action is null) return;
        IsExpanded = false;
        if (action.IsDanger)
        {
            string message = _localization[action.ConfirmMessageKey ?? action.TitleKey];
            if (!await _dialogs.ConfirmAsync(action.Title, message, isDanger: true, confirmText: action.Title)) return;
        }
        if (action.Command.CanExecute(null)) action.Command.Execute(null);
    }
}
