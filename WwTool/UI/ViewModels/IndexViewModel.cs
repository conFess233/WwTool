using Microsoft.EntityFrameworkCore;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using WwTool.Common.Enums;
using WwTool.Common.Exceptions;
using WwTool.Common.Models;
using WwTool.Common.Models.ApiRequest;
using WwTool.Common.Models.ApiResponse;
using WwTool.Common.Utils;
using WwTool.Services;
using WwTool.Services.Interfaces;
using WwTool.Common.Utils;
using ExceptionHelper = WwTool.Common.Utils.ExceptionHelper;

namespace WwTool.UI.ViewModels
{
    /// <summary>
    /// 首页视图模型
    /// </summary>
    public class IndexViewModel : BindableBase, INavigationAware
    {
        private readonly IConfigService _configService;
        private readonly ILoginService _loginService;
        private readonly IDialogService _dialogService;
        private readonly IUIStateService _uiStateService;
        private readonly IGetDataService _getDataService;
        private readonly LocalDataService _localDb;
        private readonly ILoggerService _logger;

        /// <summary>
        /// 显示登录弹窗命令
        /// </summary>
        public DelegateCommand ShowLoginDialogCommand { get; }
        /// <summary>
        /// 刷新本地账号列表命令
        /// </summary>
        public DelegateCommand RefreshLocalAccountCommand { get; }
        /// <summary>
        /// 从服务器拉取最新角色数据命令
        /// </summary>
        public DelegateCommand RefreshInfoCommand { get; }
        /// <summary>
        /// 加载本地已选账号的角色信息命令
        /// </summary>
        public DelegateCommand LoadLocalAccountInfoCommand { get; }

        public IndexViewModel(IConfigService configService, ILoginService loginService, IDialogService dialogService, IUIStateService uiStateService, IGetDataService getDataService, LocalDataService localDb, ILoggerService logger)
        {
            _configService = configService;
            _loginService = loginService;
            _dialogService = dialogService;
            _uiStateService = uiStateService;
            _getDataService = getDataService;
            _localDb = localDb;
            _logger = logger;

            ShowLoginDialogCommand = new DelegateCommand(ShowLoginDialog);
            RefreshInfoCommand = new DelegateCommand(async () => await RefreshInfo());
            RefreshLocalAccountCommand = new DelegateCommand(async () => await RefreshLocalAccount());
            LoadLocalAccountInfoCommand = new DelegateCommand(async () => await LoadSelectedAccountInfoAsync());
            Users = new();
        }

        /// <summary>
        /// 刷新本地账号列表
        /// </summary>
        private async Task RefreshLocalAccount()
        {
            _logger.Debug("在 IndexViewModel 中调用了 RefreshLocalAccount 命令");
            await LoadLocalAccountAsync();
        }

        /// <summary>
        /// 从服务器拉取并同步当前账号的角色数据
        /// </summary>
        private async Task RefreshInfo()
        {
            _logger.Info("在 IndexViewModel 中调用了 RefreshInfo 命令");
            await FeachUserInfoAsync();
        }

        /// <summary>
        /// 加载当前选中账号的本地角色信息
        /// </summary>
        private async Task LoadSelectedAccountInfoAsync()
        {
            if (SelectedUser == null)
            {
                _uiStateService.ShowToast(LanguageManager.Instance["Toast_Error"], LanguageManager.Instance["Msg_NoAccountSelected"], NotificationType.Error);
                return;
            }
            await LoadLocalAccountInfoAsync(SelectedUser.Uid);
        }

        /// <summary>
        /// 页面是否已初次加载的标记，避免重复加载
        /// </summary>
        private bool _isLoaded = false;
        #region 字段


        private string? _roleName;
        private string? _uid;
        private int _level;
        private long _createTime;
        private int _acticeDays;
        private int _worldLevel;
        private int _roleNum;
        private int _energy;
        private int _maxEnergy;
        private int _storgeEnergy;
        private long _StorgeEnergyRecoverTime;
        private int _maxStorgeEnergy;
        private long _energyRecoverTime;
        private int _liveness;
        private int _livenessMaxCount;
        private bool _livenessUnlock;
        private int _weeklyInstCount;
        private int _weeklyInstMaxCount = 3;
        private int _birthMon;
        private int _birthDay;
        private int _bpLevel;
        private int _bpWeekUp;
        private string _region;



        private int _bpWeekMaxUp;
        private bool _bpIsUnlock;
        private bool _bpIsOpen;
        private int _bpExp;
        private int _bpExpLimit;

        private ObservableCollection<UserAccount> _users;
        private UserAccount _selectedUser;
        #endregion

        #region 属性

        /// <summary>
        /// 本地账号列表
        /// </summary>
        public ObservableCollection<UserAccount> Users
        {
            get => _users; set
            {
                _users = value;
                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// 当前选中的用户账号
        /// </summary>
        public UserAccount SelectedUser
        {
            get => _selectedUser;
            set
            {
                _selectedUser = value;
                RaisePropertyChanged();
            }
        }

        public string Region
        {
            get => _region;
            set => SetProperty(ref _region, value);
        }
        public long CreateTime
        {
            get => _createTime;
            set => SetProperty(ref _createTime, value);
        }

        public int ActiceDays
        {
            get => _acticeDays;
            set => SetProperty(ref _acticeDays, value);
        }

        public int WorldLevel
        {
            get => _worldLevel;
            set => SetProperty(ref _worldLevel, value);
        }

        public int RoleNum
        {
            get => _roleNum;
            set => SetProperty(ref _roleNum, value);
        }

        public int Energy
        {
            get => _energy;
            set => SetProperty(ref _energy, value);
        }

        public int MaxEnergy
        {
            get => _maxEnergy;
            set => SetProperty(ref _maxEnergy, value);
        }

        public int StorgeEnergy
        {
            get => _storgeEnergy;
            set => SetProperty(ref _storgeEnergy, value);
        }

        public long StorgeEnergyRecoverTime
        {
            get => _StorgeEnergyRecoverTime;
            set => SetProperty(ref _StorgeEnergyRecoverTime, value);
        }

        public int MaxStorgeEnergy
        {
            get => _maxStorgeEnergy;
            set => SetProperty(ref _maxStorgeEnergy, value);
        }

        public long EnergyRecoverTime
        {
            get => _energyRecoverTime;
            set => SetProperty(ref _energyRecoverTime, value);
        }

        public int Liveness
        {
            get => _liveness;
            set => SetProperty(ref _liveness, value);
        }

        public int LivenessMaxCount
        {
            get => _livenessMaxCount;
            set => SetProperty(ref _livenessMaxCount, value);
        }

        public bool LivenessUnlock
        {
            get => _livenessUnlock;
            set => SetProperty(ref _livenessUnlock, value);
        }

        public int WeeklyInstCount
        {
            get => _weeklyInstCount;
            set => SetProperty(ref _weeklyInstCount, value);
        }

        public int WeeklyInstMaxCount
        {
            get => _weeklyInstMaxCount;
            set => SetProperty(ref _weeklyInstMaxCount, value);
        }

        public int BirthMon
        {
            get => _birthMon;
            set => SetProperty(ref _birthMon, value);
        }

        public int BirthDay
        {
            get => _birthDay;
            set => SetProperty(ref _birthDay, value);
        }


        public int BpLevel
        {
            get => _bpLevel;
            set => SetProperty(ref _bpLevel, value);
        }

        public int BpWeekUp
        {
            get => _bpWeekUp;
            set => SetProperty(ref _bpWeekUp, value);
        }

        public int BpWeekMaxUp
        {
            get => _bpWeekMaxUp;
            set => SetProperty(ref _bpWeekMaxUp, value);
        }

        public bool BpIsUnlock
        {
            get => _bpIsUnlock;
            set => SetProperty(ref _bpIsUnlock, value);
        }

        public bool BpIsOpen
        {
            get => _bpIsOpen;
            set => SetProperty(ref _bpIsOpen, value);
        }

        public int BpExp
        {
            get => _bpExp;
            set => SetProperty(ref _bpExp, value);
        }

        public int BpExpLimit
        {
            get => _bpExpLimit;
            set => SetProperty(ref _bpExpLimit, value);
        }

        public string? RoleName { get => _roleName; set => SetProperty(ref _roleName, value); }
        public string? Uid { get => _uid; set => SetProperty(ref _uid, value); }
        public int Level { get => _level; set => SetProperty(ref _level, value); }

        #endregion

        /// <summary>
        /// 显示登录弹窗，登录成功后刷新本地账号列表
        /// </summary>
        private void ShowLoginDialog()
        {
            _dialogService.ShowDialog("LoginView", null, async result =>
            {
                await LoadLocalAccountAsync();
            });
        }

        /// <summary>
        /// 从服务器同步并加载当前账号的角色详细数据
        /// </summary>
        /// <param name="showMessage">是否显示操作结果的 Toast 提示</param>
        private async Task FeachUserInfoAsync(bool showMessage = true)
        {
            try
            {
                _uiStateService.ShowLoading(LanguageManager.Instance["Msg_FetchingRoleData"]);

                if (SelectedUser == null)
                {
                    if (showMessage)
                        _uiStateService.ShowToast(LanguageManager.Instance["Toast_Error"], LanguageManager.Instance["Msg_NoAccountSelected"], NotificationType.Error);
                    return;
                }

                if (string.IsNullOrEmpty(SelectedUser.Uid))
                {
                    if (showMessage)
                        _uiStateService.ShowToast(LanguageManager.Instance["Toast_Error"], LanguageManager.Instance["Msg_UidEmpty"], NotificationType.Error);
                    return;
                }

                await ExceptionHelper.ExecuteAsync(async () =>
                {
                    var oauthCode = await _localDb.GetOauthCodeAsync(SelectedUser.Uid);
                    await _getDataService.SyncAllUserDataAsync(SelectedUser.Uid, oauthCode);
                    var roleDetail = await _localDb.LoadPlayerRoleDataAsync(SelectedUser.Uid);
                    if (roleDetail != null)
                    {
                        RoleName = SelectedUser.Name;
                        Uid = SelectedUser.Uid;
                        Level = SelectedUser.Level;
                        CreateTime = roleDetail.Base.CreatTime;
                        ActiceDays = roleDetail.Base.ActiveDays;
                        BirthDay = roleDetail.Base.BirthDay;
                        BirthMon = roleDetail.Base.BirthMon;
                        Energy = roleDetail.Base.Energy;
                        EnergyRecoverTime = roleDetail.Base.EnergyRecoverTime;
                        StorgeEnergyRecoverTime = roleDetail.Base.StoreEnergyRecoverTime;
                        StorgeEnergy = roleDetail.Base.StoreEnergy;
                        MaxEnergy = roleDetail.Base.MaxEnergy;
                        MaxStorgeEnergy = roleDetail.Base.MaxStoreEnergy;
                        Liveness = roleDetail.Base.Liveness;
                        LivenessMaxCount = roleDetail.Base.LivenessMaxCount;
                        LivenessUnlock = roleDetail.Base.LivenessUnlock;
                        WeeklyInstCount = roleDetail.Base.WeeklyInstCount;
                        WorldLevel = roleDetail.Base.WorldLevel;
                        RoleNum = roleDetail.Base.RoleNum;
                        BpExp = roleDetail.BattlePass.Exp;
                        BpExpLimit = roleDetail.BattlePass.ExpLimit;
                        BpIsOpen = roleDetail.BattlePass.IsOpen;
                        BpIsUnlock = roleDetail.BattlePass.IsUnlock;
                        BpLevel = roleDetail.BattlePass.Level;
                        BpWeekUp = roleDetail.BattlePass.WeekExp;
                        BpWeekMaxUp = roleDetail.BattlePass.WeekMaxExp;
                        Region = SelectedUser.Region;

                        if (showMessage)
                            _uiStateService.ShowToast(LanguageManager.Instance["Toast_Success"], LanguageManager.Instance["Msg_FetchRoleSuccess"], NotificationType.Success);
                    }
                    else
                    {
                        if (showMessage)
                            _uiStateService.ShowToast(LanguageManager.Instance["Toast_Error"], LanguageManager.Instance["Msg_FetchRoleEmpty"], NotificationType.Warning);
                    }
                }, "获取角色数据");
            }
            catch
            {
            }
            finally
            {
                _uiStateService.HideLoading();
            }
        }

        /// <summary>
        /// 从本地数据库加载指定 UID 的角色详细数据（不发起网络请求）
        /// </summary>
        /// <param name="uid">用户唯一标识</param>
        /// <param name="showLoading">是否显示加载动画</param>
        /// <param name="showMessage">是否显示操作结果的 Toast 提示</param>
        private async Task LoadLocalAccountInfoAsync(string uid, bool showLoading = true, bool showMessage = true)
        {
            if (string.IsNullOrEmpty(uid))
            {
                if (showMessage)
                    _uiStateService.ShowToast(LanguageManager.Instance["Toast_Error"], LanguageManager.Instance["Msg_UidEmpty"], NotificationType.Error);
                return;
            }
            try
            {
                if (showLoading)
                    _uiStateService.ShowLoading(LanguageManager.Instance["Msg_ReadingLocalRole"]);

                await ExceptionHelper.ExecuteAsync(async () =>
                {
                    var roleDetail = await _localDb.LoadPlayerRoleDataAsync(uid);
                    if (roleDetail != null)
                    {
                        RoleName = SelectedUser.Name;
                        Uid = SelectedUser.Uid;
                        Level = SelectedUser.Level;
                        CreateTime = roleDetail.Base.CreatTime;
                        ActiceDays = roleDetail.Base.ActiveDays;
                        BirthDay = roleDetail.Base.BirthDay;
                        BirthMon = roleDetail.Base.BirthMon;
                        Energy = roleDetail.Base.Energy;
                        EnergyRecoverTime = roleDetail.Base.EnergyRecoverTime;
                        StorgeEnergyRecoverTime = roleDetail.Base.StoreEnergyRecoverTime;
                        StorgeEnergy = roleDetail.Base.StoreEnergy;
                        MaxEnergy = roleDetail.Base.MaxEnergy;
                        MaxStorgeEnergy = roleDetail.Base.MaxStoreEnergy;
                        Liveness = roleDetail.Base.Liveness;
                        LivenessMaxCount = roleDetail.Base.LivenessMaxCount;
                        LivenessUnlock = roleDetail.Base.LivenessUnlock;
                        WeeklyInstCount = roleDetail.Base.WeeklyInstCount;
                        WorldLevel = roleDetail.Base.WorldLevel;
                        RoleNum = roleDetail.Base.RoleNum;
                        BpExp = roleDetail.BattlePass.Exp;
                        BpExpLimit = roleDetail.BattlePass.ExpLimit;
                        BpIsOpen = roleDetail.BattlePass.IsOpen;
                        BpIsUnlock = roleDetail.BattlePass.IsUnlock;
                        BpLevel = roleDetail.BattlePass.Level;
                        BpWeekUp = roleDetail.BattlePass.WeekExp;
                        BpWeekMaxUp = roleDetail.BattlePass.WeekMaxExp;
                        Region = SelectedUser.Region;

                        if (showMessage)
                            _uiStateService.ShowToast(LanguageManager.Instance["Toast_Success"], LanguageManager.Instance["Msg_FetchRoleSuccess"], NotificationType.Success);
                    }
                    else
                    {
                        if (showMessage)
                            _uiStateService.ShowToast(LanguageManager.Instance["Toast_Error"], LanguageManager.Instance["Msg_LocalRoleEmpty"], NotificationType.Warning);
                    }
                }, "获取本地角色数据");
            }
            catch
            {
            }
            finally
            {
                _uiStateService.HideLoading();
            }
        }

        /// <summary>
        /// 从本地数据库读取所有用户账号，并根据配置自动选中上次使用的账号
        /// </summary>
        private async Task LoadLocalAccountAsync()
        {
            try
            {
                _uiStateService.ShowLoading(LanguageManager.Instance["Msg_ReadingLocalAccounts"]);

                await ExceptionHelper.ExecuteAsync(async () =>
                {
                    var users = await Task.Run(async () => await _localDb.GetAllUserAccountAsync());

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Users.Clear();
                        foreach (var user in users)
                        {
                            Users.Add(user);
                        }

                        if (Users.Any())
                        {
                            if (!string.IsNullOrEmpty(_configService.User.LastUserId))
                            {
                                SelectedUser = Users.FirstOrDefault(u => u.Uid == _configService.User.LastUserId);
                            }
                            else
                            {
                                SelectedUser = Users.First();
                            }
                        }
                    });

                    if (_uiStateService != null)
                        _uiStateService.ShowToast(LanguageManager.Instance["Toast_Success"], string.Format(LanguageManager.Instance["Msg_ReadAccountsSuccess"], users.Count), NotificationType.Success);
                }, "获取本地账号");
            }
            finally
            {
                _uiStateService.HideLoading();
            }
        }


        /// <summary>
        /// 页面导航进入时触发，初次加载本地账号和角色数据
        /// </summary>
        public async void OnNavigatedTo(NavigationContext navigationContext)
        {
            if (!_isLoaded)
            {
                _isLoaded = true;
                await LoadLocalAccountAsync();
                await LoadSelectedAccountInfoAsync();
            }

        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;
        public void OnNavigatedFrom(NavigationContext navigationContext) { }
    }
}
