namespace WwTool.UI.Navigation;

/// <summary>
/// 页面的登记信息。视图模型由 <see cref="CreateViewModel"/> 创建，首次进入时创建一次并缓存，
/// 切换页面时保留状态。<see cref="ParentKey"/> 不为空的是子页面（如详细图表）：不出现在悬浮岛上，
/// 悬浮岛保持选中其父页面，页头小字显示父页面标题。
/// </summary>
public sealed record PageDescriptor(
    string Key,
    string TitleKey,
    string Icon,
    Func<IServiceProvider, object> CreateViewModel,
    bool StartsGroup = false,
    string? ParentKey = null)
{
    public bool IsTopLevel => ParentKey is null;
}

/// <summary>
/// 页面视图模型可选实现：进入时收到导航参数，离开时停止本页发起的后台工作。
/// </summary>
public interface INavigationAware
{
    void OnNavigatedTo(object? parameter);

    void OnNavigatedFrom();
}

public sealed class NavigatedEventArgs(PageDescriptor page, object viewModel) : EventArgs
{
    public PageDescriptor Page { get; } = page;

    public object ViewModel { get; } = viewModel;
}

/// <summary>
/// Shell 内的页面导航。页面键与视图模型的对应关系在组合根中显式登记，不用反射查找。
/// </summary>
public interface INavigationService
{
    IReadOnlyList<PageDescriptor> Pages { get; }

    PageDescriptor? Current { get; }

    event EventHandler<NavigatedEventArgs>? Navigated;

    /// <summary>导航到 <paramref name="key"/>。键未登记时抛出；已在该页且没有参数时不做任何事并返回 false。</summary>
    bool NavigateTo(string key, object? parameter = null);
}

/// <summary>
/// <see cref="INavigationService"/> 的实现。只在 UI 线程调用。
/// 这里持有 <see cref="IServiceProvider"/> 只为按登记表创建页面视图模型，属于组合基础设施，
/// 视图模型与普通服务仍禁止当作服务定位器使用。
/// </summary>
public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _services;
    private readonly Dictionary<string, PageDescriptor> _byKey;
    private readonly Dictionary<string, object> _viewModels = new(StringComparer.Ordinal);
    private object? _currentViewModel;

    public NavigationService(IEnumerable<PageDescriptor> pages, IServiceProvider services)
    {
        _services = services;
        Pages = pages.ToArray();
        _byKey = Pages.ToDictionary(p => p.Key, StringComparer.Ordinal);
    }

    public IReadOnlyList<PageDescriptor> Pages { get; }

    public PageDescriptor? Current { get; private set; }

    public event EventHandler<NavigatedEventArgs>? Navigated;

    public bool NavigateTo(string key, object? parameter = null)
    {
        if (!_byKey.TryGetValue(key, out PageDescriptor? page))
        {
            throw new ArgumentException($"未登记的页面：{key}", nameof(key));
        }
        if (ReferenceEquals(page, Current) && parameter is null)
        {
            return false;
        }

        if (!ReferenceEquals(page, Current) && _currentViewModel is INavigationAware leaving)
        {
            leaving.OnNavigatedFrom();
        }

        if (!_viewModels.TryGetValue(key, out object? viewModel))
        {
            viewModel = page.CreateViewModel(_services);
            _viewModels[key] = viewModel;
        }

        Current = page;
        _currentViewModel = viewModel;
        (viewModel as INavigationAware)?.OnNavigatedTo(parameter);
        Navigated?.Invoke(this, new NavigatedEventArgs(page, viewModel));
        return true;
    }
}
