using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace WwTool.UI.Navigation;

/// <summary>
/// 视图模型 → 视图的显式映射表（不用按命名约定反射）。加入 <c>Application.DataTemplates</c> 后，
/// <c>ContentControl</c> 的内容是已登记的视图模型时自动创建对应视图。
/// </summary>
public sealed class ViewLocator : IDataTemplate
{
    private readonly Dictionary<Type, Func<Control>> _views = [];

    public ViewLocator Map<TViewModel, TView>()
        where TView : Control, new()
    {
        _views[typeof(TViewModel)] = static () => new TView();
        return this;
    }

    public bool Match(object? data) => data is not null && _views.ContainsKey(data.GetType());

    public Control? Build(object? param)
    {
        if (param is null) return null;
        if (!_views.TryGetValue(param.GetType(), out Func<Control>? factory))
        {
            return new TextBlock { Text = $"未登记视图：{param.GetType().Name}" };
        }
        Control view = factory();
        view.DataContext = param;
        return view;
    }
}
