using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WwTool.Core.Abstractions;
using WwTool.Core.Enums;

namespace WwTool.UI.Feedback;

/// <summary>
/// Toast 与全局忙碌状态。所有方法可在任意线程调用，内部切回 UI 线程。
/// </summary>
public interface IUIStateService
{
    ObservableCollection<ToastItem> Toasts { get; }

    bool IsLoading { get; }

    string LoadingMessage { get; }

    void ShowLoading(string message);

    void HideLoading();

    void ShowToast(string title, string message, NotificationType type = NotificationType.Info);

    void ShowToast(NotificationRequest request);

    void RemoveToast(ToastItem toast);
}

/// <summary>
/// <see cref="IUIStateService"/> 的实现，规则沿用 1.x：
/// 按通知强度过滤；3 秒内同键消息合并计数；最多同时显示 3 条，其余最多排队 5 条（错误优先，
/// 排队超过 15 秒丢弃）；满额时错误可顶替信息/成功类消息；带操作按钮的至少显示 8 秒；悬停暂停倒计时。
/// </summary>
public sealed partial class UIStateService : ObservableObject, IUIStateService
{
    private const int MaxVisible = 3;
    private const int MaxQueued = 5;
    private static readonly TimeSpan DedupeWindow = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan QueueExpiry = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(50);

    /// <summary>关闭动画时长（与 ToastHost 样式中的过渡一致）。</summary>
    private static readonly TimeSpan CloseAnimation = TimeSpan.FromMilliseconds(ToastHost.ExitMs + 20);

    private readonly IConfigService _config;
    private readonly List<NotificationRequest> _queue = [];
    private readonly DispatcherTimer _timer;
    private DateTime _lastTick = DateTime.UtcNow;

    public UIStateService(IConfigService config)
    {
        _config = config;
        _timer = new DispatcherTimer(TickInterval, DispatcherPriority.Background, OnTick);
        _timer.Stop();
        Toasts.CollectionChanged += (_, _) => UpdateTimer();
    }

    public ObservableCollection<ToastItem> Toasts { get; } = [];

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _loadingMessage = string.Empty;

    public void ShowLoading(string message) => OnUiThread(() =>
    {
        LoadingMessage = message;
        IsLoading = true;
    });

    public void HideLoading() => OnUiThread(() => IsLoading = false);

    public void ShowToast(string title, string message, NotificationType type = NotificationType.Info) =>
        ShowToast(new NotificationRequest { Title = title, Message = message, Type = type });

    public void ShowToast(NotificationRequest request)
    {
        if (!ShouldDisplay(request)) return;
        OnUiThread(() => EnqueueOrShow(request));
    }

    public void RemoveToast(ToastItem toast) => OnUiThread(() => BeginRemove(toast));

    private bool ShouldDisplay(NotificationRequest request) => _config.User.NotificationDisplayMode switch
    {
        NotificationDisplayMode.Full => true,
        NotificationDisplayMode.ExceptionsOnly => request.Type is NotificationType.Warning or NotificationType.Error,
        _ => request.Priority == NotificationPriority.Important || request.Type is NotificationType.Warning or NotificationType.Error,
    };

    private void EnqueueOrShow(NotificationRequest request)
    {
        ToastItem? existing = FindExisting(request);
        if (existing is not null)
        {
            UpdateExisting(existing, request);
            return;
        }
        if (Toasts.Count < MaxVisible)
        {
            Toasts.Add(CreateItem(request));
            return;
        }
        if (request.Type == NotificationType.Error)
        {
            ToastItem? replaceable = Toasts
                .Where(t => t.Type is NotificationType.Info or NotificationType.Success && !t.IsClosing)
                .OrderBy(t => t.Priority)
                .ThenBy(t => t.CreatedAt)
                .FirstOrDefault();
            if (replaceable is not null)
            {
                Toasts.Remove(replaceable);
                Toasts.Add(CreateItem(request));
                return;
            }
        }
        Enqueue(request);
    }

    private ToastItem? FindExisting(NotificationRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.TaskKey))
        {
            ToastItem? task = Toasts.FirstOrDefault(t => t.TaskKey == request.TaskKey && !t.IsClosing);
            if (task is not null) return task;
        }
        string key = DedupeKeyOf(request);
        DateTime threshold = DateTime.UtcNow - DedupeWindow;
        return Toasts.FirstOrDefault(t => t.DedupeKey == key && t.LastUpdatedAt >= threshold && !t.IsClosing);
    }

    private void UpdateExisting(ToastItem item, NotificationRequest request)
    {
        item.Title = request.Title;
        item.Message = request.Message;
        item.Type = request.Type;
        if (string.IsNullOrWhiteSpace(request.TaskKey)) item.RepeatCount++;
        item.LastUpdatedAt = DateTime.UtcNow;
        item.Progress = request.IsTaskCompleted ? null : request.Progress;
        item.ActionText = request.ActionText;
        item.ActionCommand = CreateActionCommand(request, item);
        if (request.IsTaskCompleted || request.Progress is null)
        {
            item.Duration = EffectiveDuration(request);
            item.Remaining = item.Duration;
            item.RemainingRatio = 1;
        }
    }

    private void Enqueue(NotificationRequest request)
    {
        string key = DedupeKeyOf(request);
        int duplicate = _queue.FindIndex(q => DedupeKeyOf(q) == key && DateTime.UtcNow - q.CreatedAt <= DedupeWindow);
        if (duplicate >= 0)
        {
            request.RepeatCount = string.IsNullOrWhiteSpace(request.TaskKey) ? _queue[duplicate].RepeatCount + 1 : 1;
            request.CreatedAt = DateTime.UtcNow;
            _queue[duplicate] = request;
            return;
        }
        if (_queue.Count >= MaxQueued)
        {
            int discard = _queue.FindIndex(q => q.Type is NotificationType.Info or NotificationType.Success);
            if (discard >= 0) _queue.RemoveAt(discard);
            else if (request.Type is NotificationType.Info or NotificationType.Success) return;
            else _queue.RemoveAt(0);
        }
        _queue.Add(request);
        // 稳定排序：权重高的在前，同权重保持先来先显示。
        List<NotificationRequest> sorted = _queue.OrderByDescending(SortWeight).ToList();
        _queue.Clear();
        _queue.AddRange(sorted);
    }

    private ToastItem CreateItem(NotificationRequest request)
    {
        TimeSpan duration = EffectiveDuration(request);
        var item = new ToastItem
        {
            Type = request.Type,
            Title = request.Title,
            Message = request.Message,
            Priority = request.Priority,
            DedupeKey = DedupeKeyOf(request),
            TaskKey = request.TaskKey,
            Duration = duration,
            Remaining = duration,
            Progress = request.IsTaskCompleted ? null : request.Progress,
            ActionText = request.ActionText,
            RepeatCount = request.RepeatCount,
        };
        item.CloseCommand = new RelayCommand(() => RemoveToast(item));
        item.ActionCommand = CreateActionCommand(request, item);
        return item;
    }

    private RelayCommand? CreateActionCommand(NotificationRequest request, ToastItem item)
    {
        if (request.Action is null || string.IsNullOrWhiteSpace(request.ActionText)) return null;
        Action action = request.Action;
        return new RelayCommand(() =>
        {
            action();
            RemoveToast(item);
        });
    }

    /// <summary>只在有可见 Toast 时驱动倒计时，空闲时不唤醒 Dispatcher。</summary>
    private void UpdateTimer()
    {
        if (Toasts.Count == 0)
        {
            _timer.Stop();
        }
        else if (!_timer.IsEnabled)
        {
            _lastTick = DateTime.UtcNow;
            _timer.Start();
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        DateTime now = DateTime.UtcNow;
        TimeSpan elapsed = now - _lastTick;
        _lastTick = now;
        foreach (ToastItem item in Toasts.ToArray())
        {
            if (item.IsPaused || item.IsProgress || item.IsClosing) continue;
            item.Remaining -= elapsed;
            item.RemainingRatio = item.Duration <= TimeSpan.Zero
                ? 0
                : Math.Max(0, item.Remaining.TotalMilliseconds / item.Duration.TotalMilliseconds);
            if (item.Remaining <= TimeSpan.Zero) BeginRemove(item);
        }
        DrainQueue();
    }

    private void DrainQueue()
    {
        while (Toasts.Count(t => !t.IsClosing) < MaxVisible && _queue.Count > 0)
        {
            NotificationRequest next = _queue[0];
            _queue.RemoveAt(0);
            if (DateTime.UtcNow - next.CreatedAt > QueueExpiry) continue;
            Toasts.Add(CreateItem(next));
        }
    }

    private void BeginRemove(ToastItem item)
    {
        if (item.IsClosing || !Toasts.Contains(item)) return;
        item.IsClosing = true;
        DispatcherTimer.RunOnce(() =>
        {
            Toasts.Remove(item);
            DrainQueue();
        }, CloseAnimation);
    }

    private static string DedupeKeyOf(NotificationRequest r) =>
        $"{r.Source}:{r.Type}:{r.DedupeKey ?? r.TaskKey ?? $"{r.Title}:{r.Message}"}";

    private static int SortWeight(NotificationRequest r) => r.Type switch
    {
        NotificationType.Error => 40,
        NotificationType.Warning => 30,
        NotificationType.Success when r.Priority == NotificationPriority.Important => 20,
        _ => 10,
    };

    private static TimeSpan EffectiveDuration(NotificationRequest r)
    {
        TimeSpan duration = r.Duration ?? r.Type switch
        {
            NotificationType.Warning => TimeSpan.FromSeconds(6),
            NotificationType.Error => TimeSpan.FromSeconds(10),
            _ => TimeSpan.FromSeconds(3),
        };
        // 带操作按钮的至少保留 8 秒，给用户足够时间点击。
        return r.Action is not null && duration < TimeSpan.FromSeconds(8) ? TimeSpan.FromSeconds(8) : duration;
    }

    private static void OnUiThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess()) action();
        else Dispatcher.UIThread.Post(action);
    }
}
