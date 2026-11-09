using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WwTool.Core.Localization;

namespace WwTool.UI.ViewModels;

/// <summary>
/// 尚未迁移的一级页面的占位。页面迁移完成后，对应登记项换成真实视图模型，本类随最后一页迁完删除。
/// </summary>
public sealed class PlaceholderPageViewModel : ObservableObject
{
    private readonly LocalizationService _localization;

    public PlaceholderPageViewModel(string titleKey, LocalizationService localization)
    {
        TitleKey = titleKey;
        _localization = localization;
        _localization.PropertyChanged += OnLocalizationChanged;
    }

    public string TitleKey { get; }

    public string Title => _localization[TitleKey];

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == LocalizationService.IndexerName) OnPropertyChanged(nameof(Title));
    }
}
