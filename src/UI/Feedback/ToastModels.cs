using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using WwTool.Core.Enums;

namespace WwTool.UI.Feedback;

/// <summary>
/// 一次 Toast 请求。显示与否、合并与排队由 <see cref="IUIStateService"/> 按用户的通知强度设置决定。
/// </summary>
public sealed class NotificationRequest
{
    public required string Title { get; init; }
    public required string Message { get; init; }
    public NotificationType Type { get; init; } = NotificationType.Info;
    public NotificationPriority Priority { get; init; } = NotificationPriority.Normal;

    /// <summary>来源模块，参与去重键，避免不同模块的同文案消息被误合并。</summary>
    public string Source { get; init; } = "Application";

    /// <summary>去重键；3 秒内相同键的消息合并为一条并计数。为空时用标题 + 正文。</summary>
    public string? DedupeKey { get; init; }

    /// <summary>任务键；同一任务的进度更新原地刷新同一条 Toast。</summary>
    public string? TaskKey { get; init; }

    public string? ActionText { get; init; }
    public Action? Action { get; init; }
    public TimeSpan? Duration { get; init; }

    /// <summary>0–1 的进度；不为空且任务未完成时 Toast 不自动消失。</summary>
    public double? Progress { get; init; }

    public bool IsTaskCompleted { get; init; }
    public int RepeatCount { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 正在显示的一条 Toast。
/// </summary>
public sealed partial class ToastItem : ObservableObject
{
    public Guid Id { get; } = Guid.NewGuid();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSuccess), nameof(IsError), nameof(IsWarning), nameof(IsInfo))]
    private NotificationType _type;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRepeat))]
    private int _repeatCount = 1;

    /// <summary>剩余显示时间比例（1 → 0），驱动底部倒计时条。</summary>
    [ObservableProperty]
    private double _remainingRatio = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProgress))]
    private double? _progress;

    /// <summary>指针悬停时暂停倒计时。</summary>
    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private bool _isClosing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAction))]
    private string? _actionText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAction))]
    private ICommand? _actionCommand;

    public ICommand? CloseCommand { get; set; }

    public NotificationPriority Priority { get; init; }
    public string DedupeKey { get; init; } = string.Empty;
    public string? TaskKey { get; init; }
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    public TimeSpan Duration { get; set; }
    public TimeSpan Remaining { get; set; }

    public bool IsProgress => Progress is not null;
    public bool HasRepeat => RepeatCount > 1;
    public bool HasAction => ActionCommand is not null && !string.IsNullOrWhiteSpace(ActionText);
    public bool IsSuccess => Type == NotificationType.Success;
    public bool IsError => Type == NotificationType.Error;
    public bool IsWarning => Type == NotificationType.Warning;
    public bool IsInfo => Type == NotificationType.Info;
}
