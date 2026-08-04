using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using Prism.Commands;
using Prism.Mvvm;
using WwTool.Common.Enums;
using WwTool.Common.Models;
using WwTool.Services.Interfaces;

namespace WwTool.Services
{
    public class UIStateService : BindableBase, IUIStateService
    {
        private const int MaxVisibleNotifications = 3;
        private const int MaxQueuedNotifications = 5;
        private static readonly TimeSpan DedupeWindow = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(50);

        private readonly IConfigService _configService;
        private readonly List<NotificationRequest> _notificationQueue = [];
        private readonly HashSet<Guid> _closingNotifications = [];
        private readonly DispatcherTimer _notificationTimer;
        private DateTime _lastTick = DateTime.UtcNow;
        private bool _isLoading;
        private string _loadingMessage = string.Empty;
        private bool _isDialogVisible;
        private object? _currentDialogView;

        public UIStateService(IConfigService configService)
        {
            _configService = configService;
            _notificationTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TickInterval
            };
            _notificationTimer.Tick += OnNotificationTimerTick;
            _notificationTimer.Start();
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public string LoadingMessage
        {
            get => _loadingMessage;
            private set => SetProperty(ref _loadingMessage, value);
        }

        public bool IsDialogVisible
        {
            get => _isDialogVisible;
            private set => SetProperty(ref _isDialogVisible, value);
        }

        public object? CurrentDialogView
        {
            get => _currentDialogView;
            private set => SetProperty(ref _currentDialogView, value);
        }

        public ObservableCollection<NotificationModel> Notifications { get; } = [];

        public void ShowLoading(string message = "正在处理中...")
        {
            RunOnUiThread(() =>
            {
                LoadingMessage = message;
                IsLoading = true;
            });
        }

        public void HideLoading()
        {
            RunOnUiThread(() => IsLoading = false);
        }

        public void ShowToast(string title, string message, NotificationType type = NotificationType.Info)
        {
            ShowToast(new NotificationRequest
            {
                Title = title,
                Message = message,
                Type = type,
                DedupeKey = $"legacy:{type}:{title}:{message}"
            });
        }

        public void ShowToast(NotificationRequest request)
        {
            if (!ShouldDisplay(request)) return;
            RunOnUiThread(() => EnqueueOrShow(request));
        }

        public void RemoveToast(NotificationModel notification)
        {
            RunOnUiThread(() => BeginRemove(notification));
        }

        public void ShowDialog(object view)
        {
            RunOnUiThread(() =>
            {
                CurrentDialogView = view;
                IsDialogVisible = true;
            });
        }

        public void CloseDialog()
        {
            RunOnUiThread(() =>
            {
                IsDialogVisible = false;
                CurrentDialogView = null;
            });
        }

        private bool ShouldDisplay(NotificationRequest request)
        {
            return _configService.User.NotificationDisplayMode switch
            {
                NotificationDisplayMode.Full => true,
                NotificationDisplayMode.ExceptionsOnly => request.Type is NotificationType.Warning or NotificationType.Error,
                _ => request.Priority == NotificationPriority.Important ||
                     request.Type is NotificationType.Warning or NotificationType.Error
            };
        }

        private void EnqueueOrShow(NotificationRequest request)
        {
            NotificationModel? existing = FindExisting(request);
            if (existing is not null)
            {
                UpdateExisting(existing, request);
                return;
            }

            if (Notifications.Count < MaxVisibleNotifications)
            {
                Notifications.Add(CreateModel(request));
                return;
            }

            if (request.Type == NotificationType.Error)
            {
                NotificationModel? replaceable = Notifications
                    .Where(item => item.Type is NotificationType.Info or NotificationType.Success)
                    .OrderBy(item => item.Priority)
                    .ThenBy(item => item.CreatedAt)
                    .FirstOrDefault();
                if (replaceable is not null)
                {
                    Notifications.Remove(replaceable);
                    Notifications.Add(CreateModel(request));
                    return;
                }
            }

            Enqueue(request);
        }

        private NotificationModel? FindExisting(NotificationRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.TaskKey))
            {
                NotificationModel? task = Notifications.FirstOrDefault(item => item.TaskKey == request.TaskKey);
                if (task is not null) return task;
            }

            string key = GetDedupeKey(request);
            DateTime threshold = DateTime.UtcNow - DedupeWindow;
            return Notifications.FirstOrDefault(item =>
                item.DedupeKey == key && item.LastUpdatedAt >= threshold);
        }

        private void UpdateExisting(NotificationModel model, NotificationRequest request)
        {
            model.Title = request.Title;
            model.Message = request.Message;
            model.Type = request.Type;
            if (string.IsNullOrWhiteSpace(request.TaskKey)) model.RepeatCount++;
            model.LastUpdatedAt = DateTime.UtcNow;
            model.Progress = request.IsTaskCompleted ? null : request.Progress;
            model.ActionText = request.ActionText;
            model.ActionCommand = CreateActionCommand(request, model);
            model.NotifyActionChanged();

            if (request.IsTaskCompleted || request.Progress is null)
            {
                model.Duration = GetEffectiveDuration(request);
                model.Remaining = model.Duration;
                model.RemainingRatio = 1;
            }
        }

        private void Enqueue(NotificationRequest request)
        {
            string key = GetDedupeKey(request);
            int duplicateIndex = _notificationQueue.FindIndex(item =>
                GetDedupeKey(item) == key && DateTime.UtcNow - item.CreatedAt <= DedupeWindow);
            if (duplicateIndex >= 0)
            {
                request.RepeatCount = string.IsNullOrWhiteSpace(request.TaskKey)
                    ? _notificationQueue[duplicateIndex].RepeatCount + 1
                    : 1;
                request.CreatedAt = DateTime.UtcNow;
                _notificationQueue[duplicateIndex] = request;
                return;
            }

            if (_notificationQueue.Count >= MaxQueuedNotifications)
            {
                int discardIndex = _notificationQueue.FindIndex(item => item.Type is NotificationType.Info or NotificationType.Success);
                if (discardIndex >= 0)
                {
                    _notificationQueue.RemoveAt(discardIndex);
                }
                else if (request.Type is NotificationType.Info or NotificationType.Success)
                {
                    return;
                }
                else
                {
                    _notificationQueue.RemoveAt(0);
                }
            }

            _notificationQueue.Add(request);
            _notificationQueue.Sort((left, right) => GetSortWeight(right).CompareTo(GetSortWeight(left)));
        }

        private NotificationModel CreateModel(NotificationRequest request)
        {
            TimeSpan duration = GetEffectiveDuration(request);
            var model = new NotificationModel
            {
                Title = request.Title,
                Message = request.Message,
                Type = request.Type,
                Priority = request.Priority,
                DedupeKey = GetDedupeKey(request),
                TaskKey = request.TaskKey,
                Duration = duration,
                Remaining = duration,
                Progress = request.IsTaskCompleted ? null : request.Progress,
                IsReducedMotion = _configService.User.IsReducedMotionEnabled || !SystemParameters.ClientAreaAnimation,
                ActionText = request.ActionText,
                RepeatCount = request.RepeatCount
            };
            model.CloseCommand = new DelegateCommand(() => RemoveToast(model));
            model.ActionCommand = CreateActionCommand(request, model);
            return model;
        }

        private DelegateCommand? CreateActionCommand(NotificationRequest request, NotificationModel model)
        {
            if (request.Action is null || string.IsNullOrWhiteSpace(request.ActionText)) return null;
            return new DelegateCommand(() =>
            {
                request.Action();
                RemoveToast(model);
            });
        }

        private void OnNotificationTimerTick(object? sender, EventArgs e)
        {
            DateTime now = DateTime.UtcNow;
            TimeSpan elapsed = now - _lastTick;
            _lastTick = now;

            foreach (NotificationModel item in Notifications.ToArray())
            {
                if (item.IsPaused || item.IsProgress || item.IsClosing) continue;
                item.Remaining -= elapsed;
                item.RemainingRatio = item.Duration <= TimeSpan.Zero
                    ? 0
                    : item.Remaining.TotalMilliseconds / item.Duration.TotalMilliseconds;
                if (item.Remaining <= TimeSpan.Zero)
                {
                    BeginRemove(item);
                }
            }

            DrainQueue();
        }

        private void DrainQueue()
        {
            while (Notifications.Count < MaxVisibleNotifications && _notificationQueue.Count > 0)
            {
                NotificationRequest next = _notificationQueue[0];
                _notificationQueue.RemoveAt(0);
                if (DateTime.UtcNow - next.CreatedAt > TimeSpan.FromSeconds(15)) continue;
                Notifications.Add(CreateModel(next));
            }
        }

        private async void BeginRemove(NotificationModel notification)
        {
            if (!_closingNotifications.Add(notification.Id)) return;
            notification.IsClosing = true;
            await Task.Delay(notification.IsReducedMotion ? 80 : 150);
            RunOnUiThread(() =>
            {
                Notifications.Remove(notification);
                _closingNotifications.Remove(notification.Id);
                DrainQueue();
            });
        }

        private static string GetDedupeKey(NotificationRequest request) =>
            $"{request.Source}:{request.Type}:{request.DedupeKey ?? request.TaskKey ?? $"{request.Title}:{request.Message}"}";

        private static int GetSortWeight(NotificationRequest request) => request.Type switch
        {
            NotificationType.Error => 40,
            NotificationType.Warning => 30,
            NotificationType.Success when request.Priority == NotificationPriority.Important => 20,
            _ => 10
        };

        private static TimeSpan GetDefaultDuration(NotificationType type) => type switch
        {
            NotificationType.Warning => TimeSpan.FromSeconds(6),
            NotificationType.Error => TimeSpan.FromSeconds(10),
            _ => TimeSpan.FromSeconds(3)
        };

        /// <summary>
        /// 带操作按钮的通知至少保留八秒，确保用户有足够时间进行操作。
        /// </summary>
        private static TimeSpan GetEffectiveDuration(NotificationRequest request)
        {
            TimeSpan duration = request.Duration ?? GetDefaultDuration(request.Type);
            return request.Action is not null && duration < TimeSpan.FromSeconds(8)
                ? TimeSpan.FromSeconds(8)
                : duration;
        }

        private static void RunOnUiThread(Action action)
        {
            Dispatcher dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            if (dispatcher.CheckAccess()) action();
            else dispatcher.Invoke(action);
        }
    }
}
