using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using WwTool.Common.Enums;
using WwTool.Common.Exceptions;
using WwTool.Common.Models;
using WwTool.Common.Models.Config;
using WwTool.Common.Utils;
using WwTool.Services;
using WwTool.Services.Interfaces;
using WwTool.Common.Enums;
using WwTool.Common.Utils;
using WwTool.Common.Utils;
using ExceptionHelper = WwTool.Common.Utils.ExceptionHelper;

namespace WwTool.UI.ViewModels
{
    /// <summary>
    /// 设置页面视图模型
    /// </summary>
    public class SettingsViewModel : BindableBase, INavigationAware
    {
        /// <summary>
        /// UI 状态服务（Toast / Loading）
        /// </summary>
        private readonly IUIStateService _uiStateService;
        /// <summary>
        /// 弹窗服务
        /// </summary>
        private readonly IDialogService _dialogService;
        /// <summary>
        /// 配置服务
        /// </summary>
        private readonly IConfigService _configService;
        /// <summary>
        /// 本地数据库服务
        /// </summary>
        private readonly LocalDataService _localDb;
        /// <summary>
        /// 日志服务
        /// </summary>
        private readonly ILoggerService _logger;
        /// <summary>
        /// 用户配置对象，直接绑定到前端 UI
        /// </summary>
        public UserConfig User { get; private set; }

        private bool _isLoaded = false;

        /// <summary>
        /// 本地用户账号列表
        /// </summary>
        private ObservableCollection<UserAccount> _users;
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
                if (_configService.User.LastUserId != value?.Uid)
                {
                    if(value != null) _configService.User.LastUserId = value.Uid;
                }
                _selectedUser = value;
                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// 选择游戏安装路径命令
        /// </summary>
        public DelegateCommand SelectGamePathCommand { get; }
        /// <summary>
        /// 选择日志保存路径命令
        /// </summary>
        public DelegateCommand SelectLogPathCommand { get; }
        /// <summary>
        /// 保存所有配置命令
        /// </summary>
        public DelegateCommand SaveCommand { get; }
        /// <summary>
        /// 刷新本地账号列表命令
        /// </summary>
        public DelegateCommand RefreshLocalAccountCommand { get; }
        /// <summary>
        /// 删除本地账号命令
        /// </summary>
        public DelegateCommand DeleteLocalAccountCommand { get; }


        public SettingsViewModel(IUIStateService uIStateService, IConfigService configService, IDialogService dialogService, LocalDataService localDb, ILoggerService logger)
        {
            _uiStateService = uIStateService;
            _dialogService = dialogService;
            _configService = configService;
            _localDb = localDb;
            _logger = logger;

            SelectGamePathCommand = new DelegateCommand(SelectGamePath);
            SelectLogPathCommand = new DelegateCommand(SelectLogPath);
            SaveCommand = new DelegateCommand(async () => await SaveAsync());
            RefreshLocalAccountCommand = new DelegateCommand(async () => await RefreshLocalAccountAsync());
            DeleteLocalAccountCommand = new DelegateCommand(DeleteLocalAccount);
            User = _configService.User;
            Users = new();

        }

        #region 属性

        /// <summary>
        /// 是否启用文件日志记录
        /// </summary>
        public bool EnableFileLogging
        {
            get => _configService.App.EnableFileLogging;
            set
            {
                if (_configService.App.EnableFileLogging != value)
                {
                    _configService.App.EnableFileLogging = value;
                    RaisePropertyChanged();
                }
            }
        }

        /// <summary>
        /// 可选的语言类型枚举数组，用于 ComboBox 绑定
        /// </summary>
        public Array LanguageTypes => Enum.GetValues(typeof(LanguageType));

        /// <summary>
        /// 当前应用语言，切换时会立即用新语言刷新界面
        /// </summary>
        public LanguageType AppLanguage
        {
            get => _configService.User.AppLanguage;
            set
            {
                if (_configService.User.AppLanguage != value)
                {
                    _configService.User.AppLanguage = value;
                    LanguageManager.Instance.ChangeLanguage(value);
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(LanguageTypes));
                }
            }
        }

        /// <summary>
        /// 日志保留天数
        /// </summary>
        public int LogRetentionDays
        {
            get => _configService.App.LogRetentionDays;
            set
            {
                if (_configService.App.LogRetentionDays != value)
                {
                    _configService.App.LogRetentionDays = value;
                    RaisePropertyChanged();
                }
            }
        }

        /// <summary>
        /// 单个日志文件最大大小（MB）
        /// </summary>
        public int LogMaxSizeBytesMb
        {
            get => (int)(_configService.App.LogMaxSizeBytes / (1024 * 1024));
            set
            {
                long bytes = (long)value * 1024 * 1024;
                if (_configService.App.LogMaxSizeBytes != bytes)
                {
                    _configService.App.LogMaxSizeBytes = bytes;
                    RaisePropertyChanged();
                }
            }
        }

        /// <summary>
        /// 日志文件最大数量
        /// </summary>
        public int LogMaxFileCount
        {
            get => _configService.App.LogMaxFileCount;
            set
            {
                if (_configService.App.LogMaxFileCount != value)
                {
                    _configService.App.LogMaxFileCount = value;
                    RaisePropertyChanged();
                }
            }
        }

        /// <summary>
        /// 日志文件保存目录路径
        /// </summary>
        public string LogFolderPath
        {
            get => _configService.App.LogFolderPath;
            set
            {
                if (_configService.App.LogFolderPath != value)
                {
                    _configService.App.LogFolderPath = value;
                    RaisePropertyChanged();
                }
            }
        }

        /// <summary>
        /// 极验本地服务端口号
        /// </summary>
        public int GeetestPort
        {
            get => _configService.App.GeetestPort;
            set
            {
                if (_configService.App.GeetestPort != value)
                {
                    _configService.App.GeetestPort = value;
                    RaisePropertyChanged();
                }
            }
        }

        /// <summary>
        /// API 请求延迟时间（毫秒）
        /// </summary>
        public int DelayMs
        {
            get => _configService.Api.DelayMs;
            set
            {
                if (_configService.Api.DelayMs != value)
                {
                    _configService.Api.DelayMs = value;
                    RaisePropertyChanged();

                }
            }
        }

        /// <summary>
        /// API 请求最大重试次数
        /// </summary>
        public int MaxRetries
        {
            get => _configService.Api.MaxRetries;
            set
            {
                if (_configService.Api.MaxRetries != value)
                {
                    _configService.Api.MaxRetries = value;
                    RaisePropertyChanged();
                }
            }
        }


        #endregion


        /// <summary>
        /// 处理游戏路径的选择
        /// </summary>
        private void SelectGamePath()
        {
            ExceptionHelper.Execute(() =>
            {
                var dialog = new OpenFolderDialog
                {
                    Title = LanguageManager.Instance["SelectFolder_GamePathTitle"],
                    InitialDirectory = string.IsNullOrEmpty(_configService.User.GamePath) ? "C:\\" : _configService.User.GamePath
                };

                if (dialog.ShowDialog() == true)
                {
                    string folder = dialog.FolderName;

                    if (!File.Exists(Path.Combine(folder, _configService.App.GameLauncherFile)))
                    {
                        throw new WwToolGamePathException(string.Format(LanguageManager.Instance["Toast_GameLauncherNotFound"], _configService.App.GameLauncherFile));
                    }

                    _configService.User.GamePath = folder;
                    _uiStateService.ShowToast(LanguageManager.Instance["Toast_Success"], LanguageManager.Instance["Toast_GamePathUpdated"], NotificationType.Success);
                }
            }, "选择游戏安装路径");
        }

        /// <summary>
        /// 处理日志保存目录的选择
        /// </summary>
        private void SelectLogPath()
        {
            ExceptionHelper.Execute(() =>
            {
                var dialog = new OpenFolderDialog
                {
                    Title = LanguageManager.Instance["SelectFolder_LogDirTitle"],
                    InitialDirectory = string.IsNullOrEmpty(_configService.App.LogFolderPath) ? AppDomain.CurrentDomain.BaseDirectory : _configService.App.LogFolderPath
                };

                if (dialog.ShowDialog() == true)
                {
                    _configService.App.LogFolderPath = dialog.FolderName;
                    _uiStateService.ShowToast(LanguageManager.Instance["Toast_Success"], LanguageManager.Instance["Toast_LogDirUpdated"], NotificationType.Success);
                }
            }, "选择日志保存路径");
        }

        /// <summary>
        /// 异步保存所有配置到磁盘
        /// </summary>
        private async Task SaveAsync()
        {
            _logger.Info("用户触发了保存设置命令。");
            await ExceptionHelper.ExecuteAsync(async () =>
            {
                await _configService.SaveAllAsync();
                _logger.Debug("设置已成功保存到磁盘。");
                _uiStateService.ShowToast(LanguageManager.Instance["Toast_Success"], LanguageManager.Instance["Toast_SettingsSaved"], NotificationType.Success);
            }, "保存设置");
        }

        /// <summary>
        /// 刷新本地用户账号列表
        /// </summary>
        private async Task RefreshLocalAccountAsync()
        {
            _logger.Debug("在设置视图中刷新本地用户账号");
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
            _uiStateService.ShowToast(LanguageManager.Instance["Toast_Success"], string.Format(LanguageManager.Instance["Toast_UsersFetched"], Users.Count), NotificationType.Success);
        }

        /// <summary>
        /// 删除本地选中的用户账号，删除前会弹出确认对话框
        /// </summary>
        private void DeleteLocalAccount()
        {
            if (SelectedUser == null)
            {
                _uiStateService.ShowToast(LanguageManager.Instance["Toast_Error"], LanguageManager.Instance["Toast_NoUidSelected"], NotificationType.Error);
                return;
            }

            var p = new DialogParameters
            {
                { "Title", LanguageManager.Instance["Dialog_DeleteUser"] },
                { "Message", string.Format(LanguageManager.Instance["Dialog_DeleteUserConfirm"], SelectedUser.Uid, SelectedUser.Name) },
                { "ShowCancel", true }
            };

            _dialogService.Show("AlertView", p, async result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    await _localDb.DeleteUserAccountAsync(SelectedUser.Uid);
                    _uiStateService.ShowToast(LanguageManager.Instance["Toast_Success"], string.Format(LanguageManager.Instance["Toast_UserDeleted"], SelectedUser.Uid), NotificationType.Success);
                    await RefreshLocalAccountAsync();
                }
            });

        }

        /// <summary>
        /// 页面导航进入时触发，刷新属性通知并初次加载本地账号列表
        /// </summary>
        public async void OnNavigatedTo(NavigationContext navigationContext)
        {
            // 进入页面时刷新一次属性通知，确保前端显示最新值
            RaisePropertyChanged(nameof(User));
            RaisePropertyChanged(nameof(EnableFileLogging));
            RaisePropertyChanged(nameof(LogRetentionDays));
            RaisePropertyChanged(nameof(LogMaxSizeBytesMb));
            RaisePropertyChanged(nameof(LogMaxFileCount));
            RaisePropertyChanged(nameof(LogFolderPath));
            RaisePropertyChanged(nameof(GeetestPort));
            RaisePropertyChanged(nameof(DelayMs));
            RaisePropertyChanged(nameof(MaxRetries));

            if (!_isLoaded)
            {
                _isLoaded = true;
                await RefreshLocalAccountAsync();
            }
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;
        public void OnNavigatedFrom(NavigationContext navigationContext) { }
    }
}
