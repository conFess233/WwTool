using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Threading.Tasks;
using WwTool.Common.Enums;
using WwTool.Common.Models;
using WwTool.Common.Models.ApiResponse;
using WwTool.Services;
using WwTool.Services.Interfaces;
using WwTool.Common.Utils;
using ExceptionHelper = WwTool.Common.Utils.ExceptionHelper;

namespace WwTool.UI.ViewModels
{
    /// <summary>
    /// 角色数据视图模型，负责处理角色数据的展示逻辑
    /// </summary>
    public class RoleDataViewModel : BindableBase, INavigationAware
    {
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
        private readonly LocalDataService _localDb;

        /// <summary>
        /// 当前加载的角色详情数据
        /// </summary>
        private RoleDetailInfo? _roleDetail;

        /// <summary>
        /// 本地用户账号列表
        /// </summary>
        private ObservableCollection<UserAccount> _users;
        /// <summary>
        /// 页面是否已初次加载的标记
        /// </summary>
        private bool _isLoaded = false;

        public ObservableCollection<UserAccount> Users
        {
            get => _users;
            set
            {
                _users = value;
                RaisePropertyChanged();
            }
        }

        private UserAccount _selectedUser;

        public UserAccount SelectedUser
        {
            get => _selectedUser;
            set
            {
                _selectedUser = value;
                RaisePropertyChanged();
            }
        }


        public RoleDetailInfo? RoleDetail
        {
            get => _roleDetail;
            set
            {
                _roleDetail = value;
                RaisePropertyChanged();
            }
        }

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
        public RoleDataViewModel(IGetDataService getDataService, IUIStateService uiStateService, LocalDataService localDb, ILoggerService logger)
        {
            _getDataService = getDataService;
            _uiStateService = uiStateService;
            _localDb = localDb;
            _logger = logger;
            Users = new();
            RefreshCommand = new DelegateCommand(async () => await LoadDataAsync());
            RefreshLocalAccountCommand = new DelegateCommand(async () => await RefreshLocalAccount());
            SyncDataCommand = new DelegateCommand(async () => await SyncDataAsync());
        }

        /// <summary>
        /// 从本地数据库加载指定账号的角色数据
        /// </summary>
        private async Task LoadDataAsync()
        {
            if (SelectedUser == null)
            {
                _uiStateService.ShowToast(LanguageManager.Instance["Toast_Error"], LanguageManager.Instance["Msg_NoAccountSelected"], NotificationType.Error);

                return;
            }

            _logger.Info("在 RoleDataViewModel 中调用了 LoadDataAsync 命令");

            try
            {
                _uiStateService.ShowLoading(LanguageManager.Instance["Msg_LoadingRoleData"]);
                await ExceptionHelper.ExecuteAsync(async () =>
                {
                    var roleDetail = await _localDb.LoadPlayerRoleDataAsync(SelectedUser.Uid);
                    if (roleDetail != null)
                    {
                        RoleDetail = roleDetail;
                        _uiStateService.ShowToast(LanguageManager.Instance["Toast_Success"], LanguageManager.Instance["Msg_LoadRoleSuccess"], NotificationType.Success);
                    }
                    else
                    {
                        _uiStateService.ShowToast(LanguageManager.Instance["Toast_Warning"], LanguageManager.Instance["Msg_ReturnRoleEmpty"], NotificationType.Warning);
                    }
                }, "加载角色数据");


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
                    var oauthCode = await _localDb.GetOauthCodeAsync(SelectedUser.Uid);
                    await _getDataService.SyncAllUserDataAsync(SelectedUser.Uid, oauthCode);
                    var roleDetail = await _localDb.LoadPlayerRoleDataAsync(SelectedUser.Uid);
                    if (roleDetail != null)
                    {
                        RoleDetail = roleDetail;
                        _uiStateService.ShowToast(LanguageManager.Instance["Toast_Success"], LanguageManager.Instance["Msg_FetchRoleSuccess"], NotificationType.Success);
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
        /// 刷新本地用户账号列表并默认选中第一个用户
        /// </summary>
        private async Task RefreshLocalAccount()
        {
            var localAccounts = await _localDb.GetAllUserAccountAsync();
            if (localAccounts != null)
                Users.Clear();
            foreach (var user in localAccounts)
            {
                Users.Add(user);
            }

            if (Users != null && Users.Any())
            {
                SelectedUser = Users.First(); // 默认选中第一个用户
            }
            _uiStateService.ShowToast(LanguageManager.Instance["Toast_Success"], string.Format(LanguageManager.Instance["Msg_ReadAccountsSuccess"], Users.Count), NotificationType.Success);
        }

        /// <summary>
        /// 页面导航进入时触发，初次加载本地账号和角色数据
        /// </summary>
        public async void OnNavigatedTo(NavigationContext navigationContext)
        {
            if (!_isLoaded)
            {
                _isLoaded = true;
                await RefreshLocalAccount(); // 刷新本地用户信息
                await LoadDataAsync();
            }

        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext) { }
    }
}
