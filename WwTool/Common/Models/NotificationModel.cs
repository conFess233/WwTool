using Prism.Commands;
using Prism.Mvvm;
using WwTool.Common.Enums;

namespace WwTool.Common.Models
{
    public sealed class NotificationModel : BindableBase
    {
        private string _title = string.Empty;
        private string _message = string.Empty;
        private NotificationType _type;
        private int _repeatCount = 1;
        private double _remainingRatio = 1;
        private double? _progress;
        private bool _isPaused;
        private bool _isClosing;

        public Guid Id { get; } = Guid.NewGuid();
        public string Title { get => _title; set => SetProperty(ref _title, value); }
        public string Message { get => _message; set => SetProperty(ref _message, value); }
        public NotificationType Type
        {
            get => _type;
            set
            {
                if (SetProperty(ref _type, value))
                {
                    RaisePropertyChanged(nameof(IconGlyph));
                }
            }
        }

        public int RepeatCount
        {
            get => _repeatCount;
            set
            {
                if (SetProperty(ref _repeatCount, value))
                {
                    RaisePropertyChanged(nameof(RepeatText));
                    RaisePropertyChanged(nameof(HasRepeats));
                }
            }
        }

        public string RepeatText => $"×{RepeatCount}";
        public bool HasRepeats => RepeatCount > 1;
        public string IconGlyph => Type switch
        {
            NotificationType.Success => "✓",
            NotificationType.Warning => "!",
            NotificationType.Error => "×",
            _ => "i"
        };

        public double RemainingRatio { get => _remainingRatio; set => SetProperty(ref _remainingRatio, Math.Clamp(value, 0, 1)); }
        public double? Progress
        {
            get => _progress;
            set
            {
                if (SetProperty(ref _progress, value is null ? null : Math.Clamp(value.Value, 0, 1)))
                {
                    RaisePropertyChanged(nameof(IsProgress));
                }
            }
        }
        public bool IsProgress => Progress.HasValue;
        public bool IsPaused { get => _isPaused; set => SetProperty(ref _isPaused, value); }
        public bool IsClosing { get => _isClosing; set => SetProperty(ref _isClosing, value); }
        public bool IsReducedMotion { get; init; }
        public string? DedupeKey { get; init; }
        public string? TaskKey { get; init; }
        public NotificationPriority Priority { get; init; }
        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
        public TimeSpan Duration { get; set; }
        public TimeSpan Remaining { get; set; }
        public string? ActionText { get; set; }
        public bool HasAction => !string.IsNullOrWhiteSpace(ActionText) && ActionCommand is not null;
        public DelegateCommand? ActionCommand { get; set; }
        public DelegateCommand CloseCommand { get; set; } = null!;

        public void NotifyActionChanged()
        {
            RaisePropertyChanged(nameof(ActionText));
            RaisePropertyChanged(nameof(HasAction));
        }
    }
}
