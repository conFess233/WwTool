using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WwTool.Core.Localization;
using WwTool.UI.Shell;

namespace WwTool.UI.ViewModels;

/// <summary>
/// 尚未迁移的一级页面的占位。页面迁移完成后，对应登记项换成真实视图模型，本类随最后一页迁完删除。
/// </summary>
public sealed class PlaceholderPageViewModel : ObservableObject, IFabProvider
{
    private readonly LocalizationService _localization;

    public PlaceholderPageViewModel(string titleKey, LocalizationService localization, IReadOnlyList<FabAction>? fabActions = null)
    {
        TitleKey = titleKey;
        FabActions = fabActions ?? [];
        _localization = localization;
        _localization.PropertyChanged += OnLocalizationChanged;
    }

    public string TitleKey { get; }

    /// <summary>页面迁移后保留的悬浮按钮动作；迁移前执行时只提示“正在迁移”。</summary>
    public IReadOnlyList<FabAction> FabActions { get; }

    public string Title => _localization[TitleKey];

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == LocalizationService.IndexerName) OnPropertyChanged(nameof(Title));
    }
}
