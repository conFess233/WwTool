using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.VisualTree;

namespace WwTool.UI.Navigation;

/// <summary>
/// 视图模型 → 视图的显式映射表（不用按命名约定反射）。加入 <c>Application.DataTemplates</c> 后，
/// <c>ContentControl</c> 的内容是已登记的视图模型时自动创建对应视图。
/// <para>
/// 同一个视图模型的视图只创建一次、之后复用：页面视图模型是单例，每次切换页面都重新加载整页 XAML 并生成列表会明显卡顿。
/// 只有上一个视图还挂在界面上（例如切换动画还没结束就切回来）时才另建一个。
/// </para>
/// </summary>
public sealed class ViewLocator : IDataTemplate
{
    private readonly Dictionary<Type, Func<Control>> _views = [];
    private readonly ConditionalWeakTable<object, Control> _built = new();

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
        if (_built.TryGetValue(param, out Control? cached) && cached.Parent is null && cached.GetVisualParent() is null)
            return cached;
        Control view = factory();
        view.DataContext = param;
        _built.AddOrUpdate(param, view);
        return view;
    }
}
