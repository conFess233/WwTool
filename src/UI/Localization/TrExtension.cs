using Avalonia.Data;
using Avalonia.Markup.Xaml;
using WwTool.Core.Localization;

namespace WwTool.UI.Localization;

/// <summary>
/// 本地化标记扩展：<c>Text="{loc:Tr Nav_Home}"</c>。
/// 生成一个指向 <see cref="LocalizationService"/> 索引器的单向绑定，语言切换（<c>Item[]</c> 通知）时自动刷新。
/// 服务实例由组合根在启动时设置到 <see cref="Service"/>。
/// </summary>
public sealed class TrExtension : MarkupExtension
{
    public TrExtension()
    {
    }

    public TrExtension(string key) => Key = key;

    /// <summary>当前应用的本地化服务；必须在加载任何界面之前设置。</summary>
    public static LocalizationService? Service { get; set; }

    /// <summary>语言资源键。</summary>
    public string Key { get; set; } = string.Empty;

    // 绑定按字符串路径访问索引器（反射绑定），裁剪发布时必须保留 LocalizationService 的公共属性。
    [System.Diagnostics.CodeAnalysis.DynamicDependency(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties, typeof(LocalizationService))]
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        LocalizationService service = Service
            ?? throw new InvalidOperationException("TrExtension.Service 尚未设置，请在组合根中初始化本地化服务。");
        return new Binding($"[{Key}]") { Source = service, Mode = BindingMode.OneWay };
    }
}
