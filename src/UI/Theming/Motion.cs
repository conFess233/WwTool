using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using Avalonia.Styling;

namespace WwTool.UI.Theming;

/// <summary>
/// 动效规范（开发文档「动效规范」）：时长档位、缓动曲线与“减少动画”开关。
/// 代码里的动画一律从这里取参数；XAML 过渡在 <c>Controls.axaml</c> 里用相同数值。
/// </summary>
public static class Motion
{
    /// <summary>快：悬停、按压、开关。</summary>
    public static readonly TimeSpan Fast = TimeSpan.FromMilliseconds(150);

    /// <summary>标准：导航切换、标签页。</summary>
    public static readonly TimeSpan Standard = TimeSpan.FromMilliseconds(240);

    /// <summary>舒缓：弹窗、悬浮按钮、卡片入场。</summary>
    public static readonly TimeSpan Relaxed = TimeSpan.FromMilliseconds(420);

    /// <summary>展示：出金、数字滚动。</summary>
    public static readonly TimeSpan Showcase = TimeSpan.FromMilliseconds(1200);

    /// <summary>标准缓动 cubic-bezier(.2, .8, .2, 1)：大多数进入、展开。</summary>
    public static readonly Easing StandardEasing = new SplineEasing(0.2, 0.8, 0.2, 1);

    /// <summary>回弹 cubic-bezier(.34, 1.56, .64, 1)：导航胶囊、开关、头像弹出、提示滑入。</summary>
    public static readonly Easing BounceEasing = new SplineEasing(0.34, 1.56, 0.64, 1);

    /// <summary>舒缓 cubic-bezier(.45, 0, .55, 1)：循环、呼吸、慢速位移。</summary>
    public static readonly Easing SmoothEasing = new SplineEasing(0.45, 0, 0.55, 1);

    /// <summary>数字滚动 cubic-bezier(.16, .84, .24, 1)：先快后慢。</summary>
    public static readonly Easing CountEasing = new SplineEasing(0.16, 0.84, 0.24, 1);

    private static bool _reduced;

    /// <summary>减少动画：关闭循环与位移，只保留淡入淡出。</summary>
    public static bool Reduced
    {
        get => _reduced;
        set
        {
            if (_reduced == value) return;
            _reduced = value;
            ReducedChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    public static event EventHandler? ReducedChanged;

    /// <summary>
    /// 入场：从 <paramref name="offsetX"/>/<paramref name="offsetY"/> 位移并淡入。减少动画时只淡入。
    /// 目标的 RenderTransform 会被替换为 TranslateTransform。
    /// </summary>
    public static Task EnterAsync(Visual target, double offsetX, double offsetY, TimeSpan duration, TimeSpan delay,
        Easing? easing = null, CancellationToken token = default)
    {
        if (Reduced) { offsetX = 0; offsetY = 0; }
        var animation = new Animation
        {
            Duration = duration,
            Delay = delay,
            Easing = easing ?? StandardEasing,
            FillMode = FillMode.Both,
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(Visual.OpacityProperty, 0d), new Setter(TranslateTransform.XProperty, offsetX), new Setter(TranslateTransform.YProperty, offsetY) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(Visual.OpacityProperty, 1d), new Setter(TranslateTransform.XProperty, 0d), new Setter(TranslateTransform.YProperty, 0d) } },
            },
        };
        if (target.RenderTransform is not TranslateTransform) target.RenderTransform = new TranslateTransform();
        return animation.RunAsync(target, token);
    }

    /// <summary>离场：位移并淡出。减少动画时只淡出。</summary>
    public static Task ExitAsync(Visual target, double offsetX, double offsetY, TimeSpan duration,
        Easing? easing = null, CancellationToken token = default)
    {
        if (Reduced) { offsetX = 0; offsetY = 0; }
        var animation = new Animation
        {
            Duration = duration,
            Easing = easing ?? StandardEasing,
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(Visual.OpacityProperty, 1d), new Setter(TranslateTransform.XProperty, 0d), new Setter(TranslateTransform.YProperty, 0d) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(Visual.OpacityProperty, 0d), new Setter(TranslateTransform.XProperty, offsetX), new Setter(TranslateTransform.YProperty, offsetY) } },
            },
        };
        if (target.RenderTransform is not TranslateTransform) target.RenderTransform = new TranslateTransform();
        return animation.RunAsync(target, token);
    }
}
