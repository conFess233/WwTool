using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WwTool.UI.Theming;

namespace WwTool.UI.Shell;

/// <summary>
/// 页面切换（动效「页面切换」）：旧页向左 24 淡出，新页从右侧 24 淡入，320ms；
/// 新页里的卡片随后依次上移 16 并淡入（动效「卡片依次浮入」，每张 420ms，间隔 70ms）。
/// 减少动画时只交叉淡入淡出。
/// </summary>
public sealed class PageSlideTransition : IPageTransition
{
    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(320);

    /// <summary>参与依次浮入的卡片上限，超出的直接显示。</summary>
    private const int MaxStaggered = 12;

    public async Task Start(Visual? from, Visual? to, bool forward, CancellationToken cancellationToken)
    {
        var tasks = new List<Task>();
        if (from is not null) tasks.Add(Motion.ExitAsync(from, -24, 0, Duration, Motion.StandardEasing, cancellationToken));
        if (to is not null)
        {
            to.IsVisible = true;
            tasks.Add(Motion.EnterAsync(to, 24, 0, Duration, TimeSpan.Zero, Motion.StandardEasing, cancellationToken));
            // 等新页完成首次布局（模板与列表项生成）再收集卡片。
            Dispatcher.UIThread.Post(() => StaggerCards(to, cancellationToken), DispatcherPriority.Loaded);
        }
        await Task.WhenAll(tasks);
        if (from is not null && !cancellationToken.IsCancellationRequested) from.IsVisible = false;
    }

    /// <summary>按视觉树顺序让可见的 <c>Border.card</c> 依次浮入（隐藏的卡片不参与，免得停在透明状态）。</summary>
    public static void StaggerCards(Visual root, CancellationToken token = default)
    {
        int index = 0;
        foreach (Border card in root.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("card") && b.IsEffectivelyVisible))
        {
            if (index >= MaxStaggered) break;
            _ = Motion.EnterAsync(card, 0, 16, Motion.Relaxed, TimeSpan.FromMilliseconds(70 * index), Motion.StandardEasing, token);
            index++;
        }
    }
}

/// <summary>页头页名切换：旧标题上移淡出，新标题从下方 10 淡入（与悬浮岛切换同为 320ms）。</summary>
public sealed class TitleSwapTransition : IPageTransition
{
    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(320);

    public async Task Start(Visual? from, Visual? to, bool forward, CancellationToken cancellationToken)
    {
        var tasks = new List<Task>();
        if (from is not null) tasks.Add(Motion.ExitAsync(from, 0, -10, Duration, Motion.StandardEasing, cancellationToken));
        if (to is not null)
        {
            to.IsVisible = true;
            tasks.Add(Motion.EnterAsync(to, 0, 10, Duration, TimeSpan.Zero, Motion.StandardEasing, cancellationToken));
        }
        await Task.WhenAll(tasks);
        if (from is not null && !cancellationToken.IsCancellationRequested) from.IsVisible = false;
    }
}
