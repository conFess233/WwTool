using Avalonia.Threading;
using WwTool.UI.Navigation;

namespace WwTool.UI.Services;

/// <summary>
/// 切换页面后空闲 2 秒做一次后台完整回收。
/// </summary>
/// <remarks>
/// 页面每次进入都新建视图，离开后旧视图里的位图只剩托管包装对象，像素在 Skia 的原生内存里。
/// 托管堆很小（约 20 MB），GC 感知不到这些原生内存，可能很久都不回收，私有内存因此多出约 25 MB。
/// 回收不阻塞界面线程；连续切换页面时只在最后一次之后执行。
/// </remarks>
public sealed class MemoryTrimmer
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };

    public MemoryTrimmer(INavigationService navigation)
    {
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: false);
        };
        navigation.Navigated += (_, _) =>
        {
            _timer.Stop();
            _timer.Start();
        };
    }
}
