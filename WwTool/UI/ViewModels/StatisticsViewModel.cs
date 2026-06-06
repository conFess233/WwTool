using Microsoft.Win32;
using SQLitePCL;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using WwTool.Common.Enums;
using WwTool.Common.Exceptions;
using WwTool.Common.Models;
using WwTool.Common.Models.ApiResponse;
using WwTool.Common.Utils;
using WwTool.Common.Utils;
using WwTool.Extensions;
using WwTool.Services;
using WwTool.Services.Interfaces;
using ExceptionHelper = WwTool.Common.Utils.ExceptionHelper;

namespace WwTool.UI.ViewModels
{
    /// <summary>
    /// 抽卡数据统计视图模型，处理抽卡记录的获取和统计计算
    /// </summary>
    public class StatisticsViewModel : BindableBase, INavigationAware
    {
        /// <summary>
        /// 数据获取服务，负责与服务器通信
        /// </summary>
        private readonly IGetDataService _getDataService;
        /// <summary>
        /// Prism 弹窗服务
        /// </summary>
        private readonly IDialogService _dialogService;
        /// <summary>
        /// Prism 事件聚合器
        /// </summary>
        private readonly IEventAggregator _eventAggregator;
        /// <summary>
        /// UI 状态服务（Toast / Loading）
        /// </summary>
        private readonly IUIStateService _uiStateService;
        /// <summary>
        /// 配置服务
        /// </summary>
        private readonly IConfigService _configService;
        /// <summary>
        /// 游戏物品数据服务，提供物品 ID 到名称的映射
        /// </summary>
        private readonly GameDataService _gameData;
        /// <summary>
        /// 本地数据库服务
        /// </summary>
        private readonly LocalDataService _localDb;
        /// <summary>
        /// 日志服务
        /// </summary>
        private readonly ILoggerService _logger;

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext) { }

        public StatisticsViewModel(IEventAggregator eventAggregator, IUIStateService uIStateService, IGetDataService getDataService, IDialogService dialogService, IConfigService configService, GameDataService gameData, LocalDataService localDb, ILoggerService logger)
        {
            _uiStateService = uIStateService;
            _eventAggregator = eventAggregator;
            _getDataService = getDataService;
            _dialogService = dialogService;
            _configService = configService;
            _gameData = gameData;
            _localDb = localDb;
            _logger = logger;

            PoolStatistics = new ObservableCollection<CardPoolStatistics>(Enum.GetValues<CardPoolType>().Select(x => new CardPoolStatistics { PoolType = x }));
            Users = new();
            AutoImportUrlCommand = new DelegateCommand(AutoImportUrl);
            ClearDataCommand = new DelegateCommand(ClearData);
            GetGachaLogCommand = new DelegateCommand(async () => await StatisticsDatas());
            LoadLocalDataCommand = new DelegateCommand(LoadLocalData);
            RefreshUsersCommand = new DelegateCommand(RefreshLocalData);
            ImportUrlCommand = new DelegateCommand(RefreshQueryData);

        }

        /// <summary>
        /// 页面是否已初始化的标记
        /// </summary>
        private bool _isInitialized = false;

        /// <summary>
        /// 页面导航进入时触发，执行初始化流程
        /// </summary>
        async void IRegionAware.OnNavigatedTo(NavigationContext navigationContext)
        {
            await setUp();
        }

        /// <summary>
        /// 自动从游戏日志导入抽卡 URL 命令
        /// </summary>
        public DelegateCommand AutoImportUrlCommand { get; set; }
        /// <summary>
        /// 清空当前显示的抽卡统计数据命令
        /// </summary>
        public DelegateCommand ClearDataCommand { get; set; }
        /// <summary>
        /// 获取抽卡记录并同步命令
        /// </summary>
        public DelegateCommand GetGachaLogCommand { get; set; }
        /// <summary>
        /// 加载本地抽卡数据命令
        /// </summary>
        public DelegateCommand LoadLocalDataCommand { get; set; }
        /// <summary>
        /// 刷新本地用户列表命令
        /// </summary>
        public DelegateCommand RefreshUsersCommand { get; set; }
        /// <summary>
        /// 手动导入 URL 命令
        /// </summary>
        public DelegateCommand ImportUrlCommand { get; set; }

        public string? GamePath => _configService.User.GamePath;

        private string? _logUrl;
        public string? LogUrl
        {
            get => _logUrl;
            set
            {
                _logUrl = value;
                RaisePropertyChanged();
            }
        }

        private string? _userId;
        public string? UserId
        {
            get => _userId;
            set
            {
                _userId = value;
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

        private ObservableCollection<UserAccount> _users;
        public ObservableCollection<UserAccount> Users
        {
            get => _users; set
            {
                _users = value;
                RaisePropertyChanged();
            }
        }

        // 卡池数据统计
        private ObservableCollection<CardPoolStatistics> _poolStatistics;
        public ObservableCollection<CardPoolStatistics> PoolStatistics
        {
            get => _poolStatistics; set
            {
                _poolStatistics = value;
                RaisePropertyChanged();
            }
        }

        #region 统计数据
        private int _totalTides;            // 总抽数
        private int _totalAstrites;         // 总星声花费
        private int _totalHitGold;          // 总出金数
        private int _missCount;             // 角色限定池歪卡次数
        private int _successCount;          // 角色限定池不歪次数
        private int _limitedGoldCount;      // 角色限定池出金数
        private double _successRate;        // 不歪率
        private double _avgLimitCharaTide;  // 角色限定池每限定金平均抽数
        private double _avgCharaTide;       // 角色限定池每金平均抽数

        public double AvgLimitCharaTide
        {
            get => _avgLimitCharaTide;
            set
            {
                _avgLimitCharaTide = value;
                RaisePropertyChanged();
            }
        }
        public double AvgCharaTide
        {
            get => _avgCharaTide;
            set
            {
                _avgCharaTide = value;
                RaisePropertyChanged();
            }
        }
        public int TotalTides
        {
            get => _totalTides;
            set
            {
                _totalTides = value;
                RaisePropertyChanged();
            }
        }
        public int TotalAstrites
        {
            get => _totalAstrites;
            set
            {
                _totalAstrites = value;
                RaisePropertyChanged();
            }
        }
        public int TotalHitGold
        {
            get => _totalHitGold;
            set
            {
                _totalHitGold = value;
                RaisePropertyChanged();
            }
        }

        public int MissCount
        {
            get => _missCount;
            set
            {
                _missCount = value;
                RaisePropertyChanged();
            }
        }
        public int SuccessCount
        {
            get => _successCount;
            set
            {
                _successCount = value;
                RaisePropertyChanged();
            }
        }
        public double SuccessRate
        {
            get
            {
                return _successRate;
            }
            set
            {
                _successRate = value;
                RaisePropertyChanged();
            }
        }

        public int LimitedGoldCount
        {
            get
            {
                return _limitedGoldCount;
            }
            set
            {
                _limitedGoldCount = value;
                RaisePropertyChanged();
            }
        }
        #endregion

        /// <summary>
        /// 刷新查询数据，从 URL 解析参数并尝试匹配本地账号
        /// </summary>
        void RefreshQueryData()
        {
            if (!string.IsNullOrEmpty(_logUrl))
            {

                var info = GachaUrlParser.Parse(_logUrl);
                _configService.User.LastUserId = UserId;

                // 如果提取到了新的 UID，尝试让 UI 下拉框选中对应的账号
                var matchUser = Users.FirstOrDefault(u => u.Uid == info.PlayerId);
                if (matchUser != null)
                {
                    SelectedUser = matchUser;
                }
                else
                {
                    var newUser = new UserAccount { Uid = info.PlayerId };
                    Users.Add(newUser);
                    SelectedUser = newUser;
                }
            }
        }

        /// <summary>
        /// 页面初始化流程：加载本地账号并可选自动加载本地抽卡数据
        /// </summary>
        private async Task setUp()
        {
            if (!_isInitialized)
            {
                await Task.Delay(50);

                await LoadLocalAccount();

                if (_configService.User.AutoLoadLocalData && SelectedUser != null)
                {
                    await LoadLocalGachaLog();
                }
                _isInitialized = true;
            }

        }

        /// <summary>
        /// 从游戏日志中自动提取抽卡查询 URL
        /// </summary>
        private void AutoImportUrl()
        {
            _logger.Info("在 StatisticsViewModel 中调用了 AutoImportUrl 命令");
            ExceptionHelper.Execute(() =>
            {
                if (string.IsNullOrEmpty(GamePath))
                {
                    throw new WwToolGamePathException(LanguageManager.Instance["Msg_NoGamePath"]);
                }

                var path = System.IO.Path.Combine(GamePath, _configService.App.GameLogPath, _configService.App.GameLogFile);
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException(string.Format(LanguageManager.Instance["Msg_LogFileNotFound"], path));
                }

                var keyword = _configService.User.SearchGachaApiUrl;
                var result = ReadLines.ReadLinesShared(path).LastOrDefault(x => x.Contains(keyword));
                if (!string.IsNullOrEmpty(result))
                {
                    Match match = Regex.Match(result, @"""url""\s*:\s*""(.*?)""");
                    if (match.Success)
                    {
                        var url = match.Groups[1].Value;
                        LogUrl = url;
                        RefreshQueryData();
                        _uiStateService.ShowToast(LanguageManager.Instance["Msg_AutoImportSuccessTitle"], LanguageManager.Instance["Msg_AutoImportSuccess"], NotificationType.Success);
                        return;
                    }
                    LogUrl = result;
                }
                else
                {
                    throw new WwToolException(LanguageManager.Instance["Msg_ApiUrlNotFound"]);
                }
            }, "自动导入 API 地址");
        }

        /// <summary>
        /// 清空当前显示的统计数据（弹出确认对话框）
        /// </summary>
        private void ClearData()
        {

            var parameters = new DialogParameters
            {
                { "Title", LanguageManager.Instance["Dialog_Confirm"] },
                { "Message", LanguageManager.Instance["Msg_ConfirmClearData"] },
                { "ShowCancel", true }
            };

            _dialogService.Show("AlertView", parameters, result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    foreach (var pool in PoolStatistics)
                    {
                        pool.HitGoldDatas.Clear();
                        pool.Calculate.Clear();
                    }

                    _uiStateService.ShowToast(LanguageManager.Instance["Toast_Success"], LanguageManager.Instance["Msg_ClearedData"], NotificationType.Success);
                }
            });

        }

        /// <summary>
        /// 获取指定卡池类型的抽卡记录
        /// </summary>
        /// <param name="poolType">卡池类型枚举值</param>
        /// <returns>抽卡记录集合</returns>
        private async Task<IEnumerable<GachaData>> GetGachaLog(int poolType)
        {
            if (string.IsNullOrEmpty(_logUrl))
                return null;
            var param = GachaUrlParser.Parse(_logUrl);
            param.LanguageCode = LanguageTypeExtensions.GetCode(_configService.User.AppLanguage);
            param.CardPoolType = poolType;

            var data = await _getDataService.GetGachaLogAsync(param);

            return data;

        }

        /// <summary>
        /// 整理抽卡数据，计算保底次数、歪卡次数等统计信息
        /// </summary>
        /// <param name="data">原始抽卡数据</param>
        /// <param name="pool">卡池统计对象</param>
        /// <param name="isCharacterEventPool">是否为角色限定池（需额外统计歪卡率）</param>
        private void OrganizeData(IEnumerable<GachaData> data, CardPoolStatistics pool, bool isCharacterEventPool)
        {
            int pity = 0;
            int successCount = 0;
            int missCount = 0;
            int hitGoldCount = 0;

            // 临时列表
            var tempDatas = new List<HitGoldData>();

            foreach (var item in data.Reverse())
            {
                pity++;

                var itemInfo = _gameData.GetItemById(item.ResourceId);
                if (itemInfo != null)
                {
                }

                if (item.QualityLevel == 5)
                {
                    tempDatas.Add(new HitGoldData
                    {
                        GachaData = item,
                        Pity = pity
                    });

                    if (isCharacterEventPool && itemInfo != null)
                    {
                        if (itemInfo.IsUp) successCount++;
                        else missCount++;
                    }

                    hitGoldCount++;
                    pity = 0;
                }
            }

            if (pity > 0 && data.Any())
            {
                tempDatas.Add(new HitGoldData
                {
                    GachaData = new GachaData
                    {
                        CardPoolType = data.First().CardPoolType,
                        ResourceId = 0,
                        Count = 1,
                        Name = LanguageManager.Instance["Msg_Pity"],
                        QualityLevel = 1,
                        ResourceType = LanguageManager.Instance["Msg_Pity"],
                        Time = data.First().Time
                    },
                    Pity = pity
                });
            }

            pool.HitGoldDatas.Clear();
            for (int i = tempDatas.Count - 1; i >= 0; i--)
            {
                pool.HitGoldDatas.Add(tempDatas[i]);
            }

            pool.Calculate.Tides = data.Count();
            pool.Calculate.HitGoldCount = hitGoldCount;
            pool.Calculate.AvgGoldTide = hitGoldCount != 0 ? (double)pool.Calculate.Tides / hitGoldCount : 0;

            if (isCharacterEventPool)
            {
                SuccessCount = successCount;
                MissCount = missCount;
            }
        }

        /// <summary>
        /// 从服务器同步所有卡池的抽卡数据，并更新统计结果
        /// </summary>
        private async Task StatisticsDatas()
        {
            _logger.Info("在 StatisticsViewModel 中调用了 StatisticsDatas 命令");
            try
            {
                _uiStateService.ShowLoading(LanguageManager.Instance["Msg_SyncingGacha"]);
                await Task.Delay(50);
                RefreshQueryData();

                await ExceptionHelper.ExecuteAsync(async () =>
                {
                    foreach (var type in Enum.GetValues<CardPoolType>())
                    {
                        _uiStateService.ShowLoading(string.Format(LanguageManager.Instance["Msg_SyncingPool"], EnumExtensions.GetDescription(type)));
                        var gachaData = await GetGachaLog((int)type);
                        await _localDb.SyncGachaDataAsync(SelectedUser.Uid, (int)type, gachaData);
                    }

                    _uiStateService.ShowLoading(LanguageManager.Instance["Msg_SyncFinishedProcessing"]);
                    await Task.Run(async () =>
                    {
                        foreach (var type in Enum.GetValues<CardPoolType>())
                        {
                            var data = await _localDb.GetPoolRecordsByUid(SelectedUser.Uid, (int)type);
                            if (data != null)
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    var pool = PoolStatistics.FirstOrDefault(x => x.PoolType == type);
                                    if (pool != null)
                                    {
                                        bool isCharacterEvent = type == CardPoolType.CharacterEvent;
                                        OrganizeData(data, pool, isCharacterEvent);
                                    }
                                });
                            }
                        }
                    });

                    _uiStateService.ShowLoading(LanguageManager.Instance["Msg_CalculatingData"]);
                    await Statistics();

                    _uiStateService.ShowToast(LanguageManager.Instance["Toast_Success"], LanguageManager.Instance["Msg_SyncSuccess"], NotificationType.Success);
                    UserId = SelectedUser.Uid;
                }, "同步抽卡记录");
            }
            finally
            {
                _uiStateService.HideLoading();
            }
        }

        /// <summary>
        /// 汇总计算所有卡池的统计数据（总抽数、总花费、不歪率等）
        /// </summary>
        private async Task Statistics()
        {
            int totalTides = 0;
            int totalAstrites = 0;
            int totalHitGold = 0;
            int limitedGoldCount = 0;
            double successRate = 0;
            double avgLimitCharaTide = 0;
            double avgCharaTide = 0;

            foreach (var pool in PoolStatistics)
            {
                totalTides += pool.Calculate.Tides;
                totalAstrites += pool.Calculate.Astrites;
                totalHitGold += pool.Calculate.HitGoldCount;

                if (pool.PoolType == CardPoolType.CharacterEvent)
                {
                    if (pool.Calculate.HitGoldCount > 0)
                    {
                        successRate = (double)SuccessCount / pool.Calculate.HitGoldCount;
                        avgCharaTide = (double)pool.Calculate.Tides / pool.Calculate.HitGoldCount;
                    }

                    if (SuccessCount > 0)
                    {
                        avgLimitCharaTide = (double)pool.Calculate.Tides / SuccessCount;
                    }

                    limitedGoldCount = pool.Calculate.HitGoldCount;
                }
            }

            TotalTides = totalTides;
            TotalAstrites = totalAstrites;
            TotalHitGold = totalHitGold;
            SuccessRate = successRate;
            LimitedGoldCount = limitedGoldCount;
            AvgCharaTide = avgCharaTide;
            AvgLimitCharaTide = avgLimitCharaTide;
        }

        /// <summary>
        /// 从本地数据库加载当前账号的抽卡记录并重新统计
        /// </summary>
        public async Task LoadLocalGachaLog()
        {
            if (string.IsNullOrEmpty(SelectedUser?.Uid))
            {
                _uiStateService.ShowToast(LanguageManager.Instance["Toast_Error"], LanguageManager.Instance["Msg_UidEmptyForGacha"], NotificationType.Warning);
                return;
            }

            _logger.Info("在 StatisticsViewModel 中调用了 LoadLocalGachaLog 命令");

            try
            {
                _uiStateService.ShowLoading(LanguageManager.Instance["Msg_LoadingLocalGacha"]);

                await ExceptionHelper.ExecuteAsync(async () =>
                {
                    // 后台读取数据库文件
                    await Task.Run(async () =>
                    {
                        foreach (var type in Enum.GetValues<CardPoolType>())
                        {
                            var localData = await _localDb.GetPoolRecordsByUid(SelectedUser.Uid, (int)type);

                            if (localData != null)
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    var pool = PoolStatistics.FirstOrDefault(x => x.PoolType == type);
                                    if (pool != null)
                                    {
                                        bool isCharacterEvent = type == CardPoolType.CharacterEvent;
                                        OrganizeData(localData, pool, isCharacterEvent);
                                    }
                                });
                            }
                        }
                    });
                    await Statistics();

                    _uiStateService.ShowToast(LanguageManager.Instance["Toast_Success"], LanguageManager.Instance["Msg_LoadedLocalGacha"], NotificationType.Success);
                    UserId = SelectedUser.Uid;
                }, "加载本地数据");
            }
            finally
            {
                _uiStateService.HideLoading();
            }
        }


        /// <summary>
        /// LoadLocalGachaLog 的命令包装方法
        /// </summary>
        private async void LoadLocalData()
        {
            await LoadLocalGachaLog();
        }


        /// <summary>
        /// 从本地数据库加载所有用户账号并自动选中上次使用的账号
        /// </summary>
        private async Task LoadLocalAccount()
        {
            try
            {
                _uiStateService.ShowLoading(LanguageManager.Instance["Msg_LoadingLocalAccount"]);

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

                    _uiStateService.ShowToast(LanguageManager.Instance["Toast_Success"], string.Format(LanguageManager.Instance["Msg_LoadedLocalAccount"], users.Count), NotificationType.Success);
                }, "获取本地账号");
            }
            finally
            {
                _uiStateService.HideLoading();
            }
        }

        /// <summary>
        /// LoadLocalAccount 的命令包装方法
        /// </summary>
        private async void RefreshLocalData()
        {
            await LoadLocalAccount();
        }
    }
}