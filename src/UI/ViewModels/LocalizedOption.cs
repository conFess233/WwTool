using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WwTool.Core.Localization;

namespace WwTool.UI.ViewModels;

/// <summary>
/// 下拉框选项：保存值与语言资源键，显示文字随界面语言刷新。
/// 不用泛型：Avalonia XAML 的 x:DataType 无法引用泛型类型。
/// </summary>
public sealed class LocalizedOption : ObservableObject
{
    private readonly LocalizationService _localization;

    public LocalizedOption(object value, string titleKey, LocalizationService localization)
    {
        Value = value;
        TitleKey = titleKey;
        _localization = localization;
        // 选项与设置页同生命周期，设置页缓存于导航服务，因此与本地化服务同为应用生命周期。
        _localization.PropertyChanged += OnLocalizationChanged;
    }

    public object Value { get; }

    public string TitleKey { get; }

    public string Title => _localization[TitleKey];

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == LocalizationService.IndexerName) OnPropertyChanged(nameof(Title));
    }
}
