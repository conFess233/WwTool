using WwTool.Common.Enums;

namespace WwTool.Common.Models
{
    public sealed class NotificationRequest
    {
        public required string Title { get; init; }
        public required string Message { get; init; }
        public NotificationType Type { get; init; } = NotificationType.Info;
        public NotificationPriority Priority { get; init; } = NotificationPriority.Normal;
        public string Source { get; init; } = "Application";
        public string? DedupeKey { get; init; }
        public string? TaskKey { get; init; }
        public string? ActionText { get; init; }
        public Action? Action { get; init; }
        public TimeSpan? Duration { get; init; }
        public double? Progress { get; init; }
        public bool IsTaskCompleted { get; init; }
        public int RepeatCount { get; set; } = 1;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
