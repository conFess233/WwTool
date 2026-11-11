using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WwTool.Core.Localization;

namespace WwTool.UI.Shell;

/// <summary>
/// 悬浮岛中的一个导航项。标题随界面语言变化自动刷新。
/// </summary>
public sealed partial class NavItem : ObservableObject
{
    private readonly LocalizationService _localization;

    public NavItem(string key, string titleKey, string icon, LocalizationService localization, bool startsGroup = false,
        string? shortcut = null)
    {
        Key = key;
        TitleKey = titleKey;
        Icon = icon;
        StartsGroup = startsGroup;
        Shortcut = shortcut;
        _localization = localization;
        // 导航项与本地化服务同为应用生命周期，不需要弱事件。
        _localization.PropertyChanged += OnLocalizationChanged;
    }

    /// <summary>页面键，导航服务据此找到页面。</summary>
    public string Key { get; }

    /// <summary>界面自动化标识（nav-键），供 UIA 测试定位。</summary>
    public string AutomationId => "nav-" + Key;

    /// <summary>标题的语言资源键。</summary>
    public string TitleKey { get; }

    /// <summary>图标资源地址（avares://），作为不透明度遮罩着色。</summary>
    public string Icon { get; }

    /// <summary>是否在此项之前画分隔线（设置与主导航之间）。</summary>
    public bool StartsGroup { get; }

    /// <summary>悬停提示第二行的快捷键文字（Windows「Ctrl + 1」，macOS「⌘ + 1」）。</summary>
    public string? Shortcut { get; }

    public bool HasShortcut => Shortcut is not null;

    /// <summary>当前语言的标题。</summary>
    public string Title => _localization[TitleKey];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowTip))]
    private bool _isSelected;

    /// <summary>当前选中项不显示悬停提示。</summary>
    public bool ShowTip => !IsSelected;

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == LocalizationService.IndexerName)
        {
            OnPropertyChanged(nameof(Title));
        }
    }
}
