using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Threading;
using WwTool.Common.Enums;
using WwTool.Common.Models;
using WwTool.Common.Models.Entities;
using WwTool.Common.Models.Domain;
using WwTool.Common.Models.ApiResponse;
using WwTool.Services;
using WwTool.Services.Interfaces;
using WwTool.Services.Repositories;
using WwTool.Common.Utils;
using ExceptionHelper = WwTool.Common.Utils.ExceptionHelper;

namespace WwTool.UI.ViewModels
{
    /// <summary>
    /// 角色数据视图模型，负责处理角色数据的展示逻辑
    /// </summary>
    public class RoleDataViewModel : BindableBase, INavigationAware
    {
        private const string AccountDataNavigationTarget = "AccountDataView";
        private CancellationTokenSource _navigationCts = new();
        private readonly DispatcherTimer _recoveryTimer;
        /// <summary>
        /// 数据获取服务
        /// </summary>
        private readonly IGetDataService _getDataService;
        /// <summary>
        /// UI 状态服务（Toast / Loading）
        /// </summary>
        private readonly IUIStateService _uiStateService;
        /// <summary>
        /// 本地数据库服务
        /// </summary>
        private readonly IUserDataService _userDataService;
        /// <summary>
        /// 配置服务
        /// </summary>
        private readonly IConfigService _configService;

        /// <summary>
        /// 当前加载的角色详情数据
        /// </summary>
        private RoleDetailInfo? _roleDetail;

        /// <summary>
        /// 本地用户账号列表
        /// </summary>
        private ObservableCollection<AccountSummary> _users = new();
        /// <summary>
        /// 页面是否已初次加载的标记
        /// </summary>
        private bool _isLoaded = false;

        public ObservableCollection<AccountSummary> Users
        {
            get => _users;
            set
            {
                _users = value;
                RaisePropertyChanged();
            }
        }

        private AccountSummary _selectedUser = null!;

        public AccountSummary SelectedUser
        {
            get => _selectedUser;
            set
            {
                if (SetProperty(ref _selectedUser, value))
                {
                    OnSelectedUserChanged(value);
                }
            }
        }

        private async void OnSelectedUserChanged(AccountSummary? newUser)
        {
            if (newUser == null || string.IsNullOrEmpty(newUser.Uid)) return;

            try
            {
                _configService.User.LastUserId = newUser.Uid;
                await _configService.SaveAllAsync();

                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                _logger.Error($"切换账号并加载角色数据失败(UID: {newUser.Uid})", ex);
            }
        }


        public RoleDetailInfo? RoleDetail
        {
            get => _roleDetail;
            set
            {
                if (SetProperty(ref _roleDetail, value))
                {
                    RaiseAccountOverviewProperties();
                }
            }
        }

        public string CreatedDateDisplay => FormatCreatedDate(RoleDetail?.Base?.CreatTime ?? 0);
        public string EnergyRecoveryDisplay => FormatRecoveryTime(RoleDetail?.Base?.EnergyRecoverTime ?? 0);
        public string StoreEnergyRecoveryDisplay => FormatRecoveryTime(RoleDetail?.Base?.StoreEnergyRecoverTime ?? 0);
        public string ChapterIdDisplay => RoleDetail?.Base?.ChapterId > 0
            ? RoleDetail.Base.ChapterId.ToString(GetDisplayCulture())
            : UnavailableText;
        public string BoxesTotalDisplay => FormatCollectionTotal(RoleDetail?.Base?.Boxes);
        public string BasicBoxesTotalDisplay => FormatCollectionTotal(RoleDetail?.Base?.BasicBoxes);
        public string PhantomBoxesTotalDisplay => FormatCollectionTotal(RoleDetail?.Base?.PhantomBoxes);
        public string MusicProgressDisplay => FormatMusicProgress(RoleDetail?.MusicData);
        public string BattlePassProgressDisplay => RoleDetail?.BattlePass is { } battlePass
            ? string.Format(GetDisplayCulture(), LanguageManager.Instance["Role_CollectionProgress"], battlePass.Exp, battlePass.ExpLimit)
            : UnavailableText;
        public string BattlePassOpenDisplay => RoleDetail?.BattlePass is { } battlePass
            ? LanguageManager.Instance[battlePass.IsOpen ? "Role_StatusOpen" : "Role_StatusClosed"]
            : UnavailableText;
        public string BattlePassUnlockDisplay => RoleDetail?.BattlePass is { } battlePass
            ? LanguageManager.Instance[battlePass.IsUnlock ? "Role_RadioPremium" : "Role_RadioUnopened"]
            : UnavailableText;

        private static string UnavailableText => LanguageManager.Instance["Common_Unavailable"];

        /// <summary>
        /// 刷新本地角色数据命令
        /// </summary>
        public DelegateCommand RefreshCommand { get; }
        /// <summary>
        /// 刷新本地账号列表命令
        /// </summary>
        public DelegateCommand RefreshLocalAccountCommand { get; }
        /// <summary>
        /// 从服务器同步角色数据命令
        /// </summary>
        public DelegateCommand SyncDataCommand { get; }

        private readonly ILoggerService _logger;
        public RoleDataViewModel(
            IGetDataService getDataService,
            IUIStateService uiStateService,
            IUserDataService userDataService,
            ILoggerService logger,
            IConfigService configService)
        {
            _getDataService = getDataService;
            _uiStateService = uiStateService;
            _userDataService = userDataService;
            _logger = logger;
            _configService = configService;
            _recoveryTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMinutes(1)
            };
            _recoveryTimer.Tick += (_, _) => RaiseRecoveryTimeProperties();
            LanguageManager.Instance.PropertyChanged += OnLanguageChanged;
            Users = new();
            RefreshCommand = new DelegateCommand(async () => await LoadDataAsync(showMessage: true));
            RefreshLocalAccountCommand = new DelegateCommand(async () => await RefreshLocalAccount(showMessage: true));
            SyncDataCommand = new DelegateCommand(async () => await SyncDataAsync());
        }

        private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Item[]")
            {
                RaiseAccountOverviewProperties();
            }
        }

        private void RaiseAccountOverviewProperties()
        {
            RaisePropertyChanged(nameof(CreatedDateDisplay));
            RaiseRecoveryTimeProperties();
            RaisePropertyChanged(nameof(ChapterIdDisplay));
            RaisePropertyChanged(nameof(BoxesTotalDisplay));
            RaisePropertyChanged(nameof(BasicBoxesTotalDisplay));
            RaisePropertyChanged(nameof(PhantomBoxesTotalDisplay));
            RaisePropertyChanged(nameof(MusicProgressDisplay));
            RaisePropertyChanged(nameof(BattlePassProgressDisplay));
            RaisePropertyChanged(nameof(BattlePassOpenDisplay));
            RaisePropertyChanged(nameof(BattlePassUnlockDisplay));
        }

        private void RaiseRecoveryTimeProperties()
        {
            RaisePropertyChanged(nameof(EnergyRecoveryDisplay));
            RaisePropertyChanged(nameof(StoreEnergyRecoveryDisplay));
        }

        private static string FormatCreatedDate(long unixMilliseconds)
        {
            if (unixMilliseconds <= 0)
            {
                return UnavailableText;
            }

            try
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds)
                    .ToLocalTime()
                    .ToString("d", GetDisplayCulture());
            }
            catch (ArgumentOutOfRangeException)
            {
                return UnavailableText;
            }
        }

        private static string FormatRecoveryTime(long unixMilliseconds)
        {
            if (unixMilliseconds <= 0)
            {
                return UnavailableText;
            }

            DateTimeOffset target;
            try
            {
                target = DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds);
            }
            catch (ArgumentOutOfRangeException)
            {
                return UnavailableText;
            }

            TimeSpan remaining = target - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                return UnavailableText;
            }

            int totalMinutes = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
            int days = totalMinutes / (24 * 60);
            int hours = totalMinutes % (24 * 60) / 60;
            int minutes = totalMinutes % 60;
            CultureInfo culture = GetDisplayCulture();

            if (days > 0)
            {
                return string.Format(culture, LanguageManager.Instance["Role_RecoveryDaysHours"], days, hours);
            }

            if (hours > 0)
            {
                return string.Format(culture, LanguageManager.Instance["Role_RecoveryHoursMinutes"], hours, minutes);
            }

            return string.Format(culture, LanguageManager.Instance["Role_RecoveryMinutes"], minutes);
        }

        private static string FormatCollectionTotal(IReadOnlyDictionary<string, int>? values) =>
            values is null
                ? UnavailableText
                : values.Values.Sum(x => (long)x).ToString("N0", GetDisplayCulture());

        private static string FormatMusicProgress(IReadOnlyCollection<RoleMusicData>? music)
        {
            if (music is null)
            {
                return UnavailableText;
            }

            long collected = music.Sum(x => (long)x.Count);
            long total = music.Sum(x => (long)x.TotalCount);
            return string.Format(GetDisplayCulture(), LanguageManager.Instance["Role_CollectionProgress"], collected, total);
        }

        private static CultureInfo GetDisplayCulture() => LanguageManager.Instance.CurrentLanguage switch
        {
            LanguageType.En => CultureInfo.GetCultureInfo("en-US"),
            LanguageType.Ja => CultureInfo.GetCultureInfo("ja-JP"),
            _ => CultureInfo.GetCultureInfo("zh-CN")
        };

        /// <summary>
        /// 从本地数据库加载指定账号的角色数据
        /// </summary>
        /// <param name="showMessage">是否显示用户主动加载的结果提示</param>
        private async Task LoadDataAsync(bool showMessage = false)
        {
            if (SelectedUser == null)
            {
                if (showMessage)
                    _uiStateService.ShowToast(LanguageManager.Instance["Toast_Error"], LanguageManager.Instance["Msg_NoAccountSelected"], NotificationType.Error);

                return;
            }

            _logger.Info("在 RoleDataViewModel 中调用了 LoadDataAsync 命令");

            try
            {
                _uiStateService.ShowLoading(LanguageManager.Instance["Msg_LoadingRoleData"]);
                await ExceptionHelper.ExecuteAsync(async () =>
                {
                    var roleDetail = await _userDataService.LoadRoleSnapshotAsync(SelectedUser.Uid, _navigationCts.Token);
                    if (roleDetail != null)
                    {
                        RoleDetail = roleDetail;
                        if (showMessage)
                        {
                            ToastHelper.ShowActionResult(
                                _uiStateService,
                                LanguageManager.Instance["Toast_Success"],
                                LanguageManager.Instance["Msg_LoadRoleSuccess"],
                                NotificationType.Success,
                                nameof(RoleDataViewModel),
                                "role:load-local-data");
                        }
                    }
                    else
                    {
                        if (showMessage)
                            _uiStateService.ShowToast(LanguageManager.Instance["Toast_Warning"], LanguageManager.Instance["Msg_ReturnRoleEmpty"], NotificationType.Warning);
                    }
                }, "加载角色数据", notifyUser: showMessage);


            }
            finally
            {
                _uiStateService.HideLoading();
            }

        }

        /// <summary>
        /// 从服务器同步当前账号的角色数据并更新本地存储
        /// </summary>
        private async Task SyncDataAsync()
        {
            try
            {
                _uiStateService.ShowLoading(LanguageManager.Instance["Msg_FetchingRoleData"]);

                if (SelectedUser == null)
                {
                    _uiStateService.ShowToast(LanguageManager.Instance["Toast_Error"], LanguageManager.Instance["Msg_NoAccountSelected"], NotificationType.Error);
                    return;
                }

                var uid = SelectedUser.Uid;
                if (string.IsNullOrEmpty(uid))
                {
                    _uiStateService.ShowToast(LanguageManager.Instance["Toast_Error"], LanguageManager.Instance["Msg_UidEmpty"], NotificationType.Error);
                    return;
                }

                await ExceptionHelper.ExecuteAsync(async () =>
                {
                    var oauthCode = await _userDataService.GetCredentialAsync(SelectedUser.Uid, _navigationCts.Token);
                    await _getDataService.SyncAllUserDataAsync(SelectedUser.Uid, oauthCode, _navigationCts.Token);
                    var roleDetail = await _userDataService.LoadRoleSnapshotAsync(SelectedUser.Uid, _navigationCts.Token);
                    if (roleDetail != null)
                    {
                        RoleDetail = roleDetail;
                        ToastHelper.ShowActionResult(
                            _uiStateService,
                            LanguageManager.Instance["Toast_Success"],
                            LanguageManager.Instance["Msg_FetchRoleSuccess"],
                            NotificationType.Success,
                            nameof(RoleDataViewModel),
                            "role:sync-data");
                    }
                    else
                        _uiStateService.ShowToast(LanguageManager.Instance["Toast_Error"], LanguageManager.Instance["Msg_FetchRoleEmpty"], NotificationType.Warning);
                }, "获取角色数据");
            }
            finally
            {
                _uiStateService.HideLoading();
            }
        }

        /// <summary>
        /// 刷新本地用户账号列表并优先选中上次选择的用户
        /// </summary>
        /// <param name="showMessage">是否显示用户主动刷新账号的结果提示</param>
        private async Task RefreshLocalAccount(bool showMessage = false)
        {
            var localAccounts = await _userDataService.ListAccountsAsync(_navigationCts.Token);
            Users.Clear();
            foreach (var user in localAccounts ?? [])
            {
                Users.Add(user);
            }

            if (Users != null && Users.Any())
            {
                if (!string.IsNullOrEmpty(_configService.User.LastUserId))
                {
                    SelectedUser = Users.FirstOrDefault(u => u.Uid == _configService.User.LastUserId) ?? Users.First();
                }
                else
                {
                    SelectedUser = Users.First();
                }
            }
            if (showMessage)
            {
                int userCount = Users?.Count ?? 0;
                NotificationType resultType = userCount == 0 ? NotificationType.Info : NotificationType.Success;
                ToastHelper.ShowActionResult(
                    _uiStateService,
                    LanguageManager.Instance[resultType == NotificationType.Info ? "Toast_Info" : "Toast_Success"],
                    userCount == 0
                        ? LanguageManager.Instance["Msg_ActionNoNewData"]
                        : string.Format(LanguageManager.Instance["Msg_ReadAccountsSuccess"], userCount),
                    resultType,
                    nameof(RoleDataViewModel),
                    "role:refresh-accounts");
            }
        }

        /// <summary>
        /// 页面导航进入时触发，初次加载本地账号和角色数据
        /// </summary>
        public async void OnNavigatedTo(NavigationContext navigationContext)
        {
            ResetNavigationCancellation();
            if (navigationContext.Uri.ToString().EndsWith(AccountDataNavigationTarget, StringComparison.Ordinal))
            {
                RaiseRecoveryTimeProperties();
                _recoveryTimer.Start();
            }
            else
            {
                _recoveryTimer.Stop();
            }
            if (!_isLoaded)
            {
                _isLoaded = true;
                try
                {
                    await RefreshLocalAccount(); // 刷新本地用户信息
                    await LoadDataAsync();
                }
                catch (Exception ex)
                {
                    _logger.Error("进入角色数据页面并进行初始化数据加载时捕获到异常", ex);
                }
            }

        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _recoveryTimer.Stop();
            _navigationCts.Cancel();
        }

        private void ResetNavigationCancellation()
        {
            if (!_navigationCts.IsCancellationRequested) return;
            _navigationCts.Dispose();
            _navigationCts = new CancellationTokenSource();
        }
    }
}
