using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WwTool.Core.Localization;
using WwTool.UI.Feedback;

namespace WwTool.UI.Shell;

/// <summary>
/// 悬浮按钮里的一个动作。首项为页面主操作；危险动作执行前由悬浮按钮弹出确认框。
/// </summary>
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

    /// <summary>图标资源地址（avares://），作为不透明度遮罩着色。</summary>
    public string Icon { get; }

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
/// 页面视图模型实现此接口来声明悬浮按钮动作；未实现或列表为空的页面不显示悬浮按钮。
/// </summary>
public interface IFabProvider
{
    IReadOnlyList<FabAction> FabActions { get; }
}

/// <summary>
/// 右下悬浮按钮：多个动作时点击展开菜单（内容区蒙半透明遮罩），只有一个动作时显示为带文字的扩展按钮直接执行。
/// </summary>
public sealed partial class FabMenuViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;
    private readonly LocalizationService _localization;

    public FabMenuViewModel(IDialogService dialogs, LocalizationService localization)
    {
        _dialogs = dialogs;
        _localization = localization;
        _localization.PropertyChanged += (_, e) =>
        {
                if (e.PropertyName == LocalizationService.IndexerName)
            {
                OnPropertyChanged(nameof(ToggleName));
                OnPropertyChanged(nameof(PrimaryTitle));
            }
        };
    }

    public ObservableCollection<FabAction> Actions { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleName))]
    private bool _isExpanded;

    /// <summary>有动作时显示。</summary>
    public bool IsVisible => Actions.Count > 0;

    /// <summary>只有一个动作：显示为扩展按钮（图标 + 文字），不展开菜单。</summary>
    public bool IsExtended => Actions.Count == 1;

    /// <summary>多个动作：圆钮 + 菜单。</summary>
    public bool IsMenu => Actions.Count > 1;

    public FabAction? Primary => Actions.Count > 0 ? Actions[0] : null;

    /// <summary>扩展按钮显示的文字。</summary>
    public string PrimaryTitle => Primary?.Title ?? string.Empty;

    /// <summary>扩展按钮显示的图标。</summary>
    public string? PrimaryIcon => Primary?.Icon;

    /// <summary>展开按钮的可访问名称。</summary>
    public string ToggleName => _localization[IsExpanded ? "Fab_Close" : "Fab_Open"];

    /// <summary>切换页面时由 Shell 调用。</summary>
    public void SetActions(IEnumerable<FabAction>? actions)
    {
        IsExpanded = false;
        Actions.Clear();
        foreach (FabAction action in actions ?? []) Actions.Add(action);
        for (int i = 0; i < Actions.Count; i++) Actions[i].IsPrimary = i == 0;
        OnPropertyChanged(nameof(IsVisible));
        OnPropertyChanged(nameof(IsExtended));
        OnPropertyChanged(nameof(IsMenu));
        OnPropertyChanged(nameof(Primary));
        OnPropertyChanged(nameof(PrimaryTitle));
        OnPropertyChanged(nameof(PrimaryIcon));
    }

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;

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
