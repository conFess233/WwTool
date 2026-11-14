using Avalonia.Threading;
using WwTool.UI.Feedback;
using WwTool.UI.Navigation;

namespace WwTool.UI.Services;

/// <summary>
/// 切换页面或关闭弹窗后空闲 2 秒做一次后台完整回收。
/// </summary>
/// <remarks>
/// 程序空闲时几乎不再分配内存，GC 不会自己运行，上一阵操作留下的垃圾会一直占着：
/// <list type="bullet">
/// <item>位图的像素在 Skia 的原生内存里，托管堆只有包装对象，GC 感知不到，可能很久都不回收；</item>
/// <item>角色详情这类弹窗加载时会产生十几 MB 的大对象（解析约 400 KB 的 JSON），关掉后就是垃圾。</item>
/// </list>
/// 回收不阻塞界面线程（实测改成阻塞式整理只多省约 14 MB，却要停顿 20–60 ms）；连续操作时只在最后一次之后执行。
/// </remarks>
public sealed class MemoryTrimmer
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };

    public MemoryTrimmer(INavigationService navigation, DialogService dialogs)
    {
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: false);
        };
        navigation.Navigated += (_, _) => Schedule();
        dialogs.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DialogService.Current) && dialogs.Current is null) Schedule();
        };
    }

    private void Schedule()
    {
        _timer.Stop();
        _timer.Start();
    }
}
