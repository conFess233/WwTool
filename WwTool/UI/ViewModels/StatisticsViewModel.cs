using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.Win32;
using SkiaSharp;
using SQLitePCL;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using WwTool.Common.Enums;
using WwTool.Common.Exceptions;
using WwTool.Common.Models;
using WwTool.Common.Models.ApiResponse;
using WwTool.Common.Utils;
using WwTool.Extensions;
using WwTool.Services;
using WwTool.Services.Interfaces;
using WwTool.Services.Repositories;
using ExceptionHelper = WwTool.Common.Utils.ExceptionHelper;

namespace WwTool.UI.ViewModels
{
    /// <summary>
    /// 抽卡数据统计视图模型，处理抽卡记录的获取和统计计算
    /// </summary>
    public class StatisticsViewModel : BindableBase, INavigationAware
    {
        private readonly IGetDataService _getDataService;
        private readonly IDialogService _dialogService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IUIStateService _uiStateService;
        private readonly IConfigService _configService;
        private readonly GameDataService _gameData;
        private readonly IGachaRepository _gachaRepository;
        private readonly IUserRepository _userRepository;
        private readonly IGachaStatisticsService _gachaStatisticsService;
        private readonly IChartBuilderService _chartBuilderService;
        private readonly ILoggerService _logger;

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext) { }

        public StatisticsViewModel(IEventAggregator eventAggregator, IUIStateService uIStateService, IGetDataService getDataService, IDialogService dialogService, IConfigService configService, GameDataService gameData, IGachaRepository gachaRepository, IUserRepository userRepository, IGachaStatisticsService gachaStatisticsService, IChartBuilderService chartBuilderService, ILoggerService logger)
        {
            _uiStateService = uIStateService;
            _eventAggregator = eventAggregator;
            _getDataService = getDataService;
            _dialogService = dialogService;
            _configService = configService;
            _gameData = gameData;
            _gachaRepository = gachaRepository;
            _userRepository = userRepository;
            _gachaStatisticsService = gachaStatisticsService;
            _chartBuilderService = chartBuilderService;
            _logger = logger;

            PoolStatistics = new ObservableCollection<CardPoolStatistics>(Enum.GetValues<CardPoolType>().Select(x => new CardPoolStatistics { PoolType = x }));
            Users = new();
            AutoImportUrlCommand = new DelegateCommand(AutoImportUrl);
            ClearDataCommand = new DelegateCommand(ClearData);
            GetGachaLogCommand = new DelegateCommand(async () => await StatisticsDatas());
            LoadLocalDataCommand = new DelegateCommand(LoadLocalData);
            RefreshUsersCommand = new DelegateCommand(RefreshLocalData);
            ImportUrlCommand = new DelegateCommand(RefreshQueryData);

            foreach (var type in Enum.GetValues<CardPoolType>())
            {
                var filter = new PoolTypeFilterItem { PoolType = type, IsSelected = true };
                filter.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(PoolTypeFilterItem.IsSelected)) _ = UpdateChartsAsync(); };
                PoolFilters.Add(filter);
            }

            _configService.User.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_configService.User.CurrentTheme))
                {
                    Task.Delay(50).ContinueWith(_ =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            _ = UpdateChartsAsync();
                        });
                    });
                }
            };
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
                if (SetProperty(ref _selectedUser, value))
                {
                    OnSelectedUserChanged(value);
                }
            }
        }

        private async void OnSelectedUserChanged(UserAccount? newUser)
        {
            if (newUser == null || string.IsNullOrEmpty(newUser.Uid)) return;

            try
            {
                _configService.User.LastUserId = newUser.Uid;
                await _configService.SaveAllAsync();

                await LoadLocalGachaLog();
            }
            catch (Exception ex)
            {
                _logger.Error($"切换账号并加载抽卡数据失败(UID: {newUser.Uid})", ex);
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

        #region 全局数据源与看板过滤属性
        private List<GachaData> _allCachedGachaDatas = new();

        private int _selectedDateRangeIndex = 0; // 0=全部, 1=最近1个月, 2=最近3个月
        public int SelectedDateRangeIndex
        {
            get => _selectedDateRangeIndex;
            set { if (SetProperty(ref _selectedDateRangeIndex, value)) _ = UpdateChartsAsync(); }
        }

        public ObservableCollection<PoolTypeFilterItem> PoolFilters { get; set; } = new();

        private string _selectedGoldName = "";
        public string SelectedGoldName
        {
            get => _selectedGoldName;
            set { if (SetProperty(ref _selectedGoldName, value)) _ = UpdateChartsAsync(); }
        }

        public ObservableCollection<string> AllGotGoldNames { get; set; } = new();
        #endregion

        #region 图表数据绑定
        private ISeries[] _globalPoolCompareSeries;
        public ISeries[] GlobalPoolCompareSeries { get => _globalPoolCompareSeries; set { _globalPoolCompareSeries = value; RaisePropertyChanged(); } }

        private Axis[] _globalPoolXAxes;
        public Axis[] GlobalPoolXAxes { get => _globalPoolXAxes; set { _globalPoolXAxes = value; RaisePropertyChanged(); } }

        private Axis[] _globalPoolYAxes;
        public Axis[] GlobalPoolYAxes { get => _globalPoolYAxes; set { _globalPoolYAxes = value; RaisePropertyChanged(); } }

        private ISeries[] _successRatePieSeries;
        public ISeries[] SuccessRatePieSeries { get => _successRatePieSeries; set { _successRatePieSeries = value; RaisePropertyChanged(); } }

        private ObservableCollection<HitGoldData> _filteredHitGoldFlow = new();
        public ObservableCollection<HitGoldData> FilteredHitGoldFlow { get => _filteredHitGoldFlow; set { _filteredHitGoldFlow = value; RaisePropertyChanged(); } }
        #endregion

        #region 旧图表属性
        private ObservableCollection<CardPoolChartData> _poolCharts = new();
        public ObservableCollection<CardPoolChartData> PoolCharts
        {
            get => _poolCharts;
            set { _poolCharts = value; RaisePropertyChanged(); }
        }

        private ISeries[] _fourStarPieSeries;
        public ISeries[] FourStarPieSeries
        {
            get => _fourStarPieSeries;
            set { _fourStarPieSeries = value; RaisePropertyChanged(); }
        }

        private ISeries[] _dailyPullLineSeries;
        public ISeries[] DailyPullLineSeries
        {
            get => _dailyPullLineSeries;
            set { _dailyPullLineSeries = value; RaisePropertyChanged(); }
        }

        private Axis[] _dailyXAxes;
        public Axis[] DailyXAxes
        {
            get => _dailyXAxes;
            set { _dailyXAxes = value; RaisePropertyChanged(); }
        }
        #endregion

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
                if (string.IsNullOrEmpty(_configService.User.GamePath))
                {
                    throw new WwToolGamePathException(LanguageManager.Instance["Msg_NoGamePath"]);
                }
                var path = System.IO.Path.Combine(_configService.User.GamePath, "Wuthering Waves Game/" + _configService.App.GameLogPath, _configService.App.GameLogFile);

                if (_configService.User.GamePath.EndsWith("Wuthering Waves Game"))
                    path = System.IO.Path.Combine(_configService.User.GamePath, _configService.App.GameLogPath, _configService.App.GameLogFile);

                if (!File.Exists(path))
                {
                    throw new FileNotFoundException(string.Format(LanguageManager.Instance["Msg_LogFileNotFound"], path));
                }

                var keyword = _configService.User.SearchGachaApiUrl;
                var result = ReadLines.ReadLinesDecrypt(path).LastOrDefault(x => x.Contains(keyword));
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
                        await _gachaRepository.SyncGachaDataAsync(SelectedUser.Uid, (int)type, gachaData);
                    }

                    _uiStateService.ShowLoading(LanguageManager.Instance["Msg_SyncFinishedProcessing"]);
                    var allGachaDatas = new List<GachaData>();
                    await Task.Run(async () =>
                    {
                        foreach (var type in Enum.GetValues<CardPoolType>())
                        {
                            var data = await _gachaRepository.GetPoolRecordsByUid(SelectedUser.Uid, (int)type);
                            if (data != null)
                            {
                                allGachaDatas.AddRange(data);
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    var pool = PoolStatistics.FirstOrDefault(x => x.PoolType == type);
                                    if (pool != null)
                                    {
                                        var res = _gachaStatisticsService.OrganizeData(data, type, LanguageTypeExtensions.GetCode(_configService.User.AppLanguage));

                                        pool.HitGoldDatas.Clear();
                                        foreach (var d in res.PoolStatistics.HitGoldDatas) pool.HitGoldDatas.Add(d);

                                        pool.Calculate = res.PoolStatistics.Calculate;

                                        if (type == CardPoolType.CharacterEvent)
                                        {
                                            SuccessCount = res.SuccessCount;
                                            MissCount = res.MissCount;
                                        }

                                        var chartData = PoolCharts.FirstOrDefault(x => x.PoolType == pool.PoolType);
                                        if (chartData == null)
                                        {
                                            chartData = new CardPoolChartData { PoolType = pool.PoolType };
                                            PoolCharts.Add(chartData);
                                        }

                                        chartData.GoldHistorySeries = _chartBuilderService.BuildGoldHistorySeries(res.GoldValues, LanguageManager.Instance["Msg_Pity"], chartData.GoldHistorySeries);
                                        chartData.XAxes = _chartBuilderService.BuildGoldHistoryXAxes(res.GoldLabels, chartData.XAxes);
                                    }
                                });
                            }
                        }
                    });

                    _uiStateService.ShowLoading(LanguageManager.Instance["Msg_CalculatingData"]);
                    await Statistics(allGachaDatas);

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
        private async Task Statistics(List<GachaData> allGachaDatas = null)
        {
            var globalStats = _gachaStatisticsService.CalculateGlobalStatistics(PoolStatistics, SuccessCount);

            TotalTides = globalStats.TotalTides;
            TotalAstrites = globalStats.TotalAstrites;
            TotalHitGold = globalStats.TotalHitGold;
            SuccessRate = globalStats.SuccessRate;
            LimitedGoldCount = globalStats.LimitedGoldCount;
            AvgCharaTide = globalStats.AvgCharaTide;
            AvgLimitCharaTide = globalStats.AvgLimitCharaTide;

            if (allGachaDatas != null && allGachaDatas.Any())
            {
                _allCachedGachaDatas = allGachaDatas;
            }

            await UpdateChartsAsync();
        }

        private bool _isUpdatingCharts = false;
        private async Task UpdateChartsAsync()
        {
            if (_isUpdatingCharts) return;
            _isUpdatingCharts = true;

            try
            {
                if (_allCachedGachaDatas == null || !_allCachedGachaDatas.Any()) return;

                var filteredDatas = _allCachedGachaDatas.Where(x =>
            {
                if (SelectedDateRangeIndex == 1 && DateTime.TryParse(x.Time, out var dt1) && dt1 < DateTime.Now.AddMonths(-1)) return false;
                if (SelectedDateRangeIndex == 2 && DateTime.TryParse(x.Time, out var dt2) && dt2 < DateTime.Now.AddMonths(-3)) return false;

                if (!PoolFilters.Any(f => f.IsSelected && (int)f.PoolType == ParsePoolType(x.CardPoolType))) return false;
                return true;
            }).ToList();

                // 如果选择了特定角色，再次过滤
                var flowDatas = new List<HitGoldData>();
                foreach (var pool in PoolStatistics)
                {
                    if (!PoolFilters.Any(f => f.IsSelected && f.PoolType == pool.PoolType)) continue;

                    foreach (var hit in pool.HitGoldDatas)
                    {
                        if (SelectedDateRangeIndex == 1 && DateTime.TryParse(hit.GachaData.Time, out var dt1) && dt1 < DateTime.Now.AddMonths(-1)) continue;
                        if (SelectedDateRangeIndex == 2 && DateTime.TryParse(hit.GachaData.Time, out var dt2) && dt2 < DateTime.Now.AddMonths(-3)) continue;

                        if (hit.GachaData.ResourceId == 0) continue;

                        if (!string.IsNullOrEmpty(SelectedGoldName) && SelectedGoldName != (LanguageManager.Instance["Stat_All"] ?? "全部") && hit.GachaData.Name != SelectedGoldName && hit.GachaData.ResourceId != 0) continue;

                        flowDatas.Add(hit);
                    }
                }

                // 更新明细
                Application.Current.Dispatcher.Invoke(() =>
                {
                    FilteredHitGoldFlow = new ObservableCollection<HitGoldData>(flowDatas.OrderByDescending(x => x.GachaData.Time));
                });

                // 提取所有的五星供下拉框选择
                var allGolds = _allCachedGachaDatas.Where(x => x.QualityLevel == 5).Select(x => x.Name).Distinct().ToList();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var curSelected = SelectedGoldName;
                    AllGotGoldNames.Clear();
                    AllGotGoldNames.Add(LanguageManager.Instance["Stat_All"] ?? "全部");
                    foreach (var g in allGolds) AllGotGoldNames.Add(g);
                    if (!string.IsNullOrEmpty(curSelected) && AllGotGoldNames.Contains(curSelected)) SelectedGoldName = curSelected;
                    else SelectedGoldName = LanguageManager.Instance["Stat_All"] ?? "全部";
                });

                // 四星及歪率
                int fourStarCharacterCount = 0;
                int fourStarWeaponCount = 0;
                int success = 0, miss = 0;

                foreach (var item in filteredDatas)
                {
                    if (item.QualityLevel == 4)
                    {
                        var itemInfo = _gameData.GetItemById(item.ResourceId);
                        string typeStr = itemInfo?.Type ?? item.ResourceType;
                        if (typeStr.Contains("角色") || typeStr.Contains("Role") || typeStr.Contains("Character")) fourStarCharacterCount++;
                        else fourStarWeaponCount++;
                    }

                    if (item.QualityLevel == 5 && ParsePoolType(item.CardPoolType) == (int)CardPoolType.CharacterEvent)
                    {
                        var itemInfo = _gameData.GetItemById(item.ResourceId);
                        if (itemInfo != null)
                        {
                            if (itemInfo.IsUp) success++;
                            else miss++;
                        }
                    }
                }

                // 比较图表
                var compareXLabels = new List<string>();
                var tidesData = new List<int>();
                var astritesData = new List<int>();
                var avgTideData = new List<double>();

                foreach (var type in Enum.GetValues<CardPoolType>())
                {
                    if (!PoolFilters.Any(f => f.IsSelected && f.PoolType == type)) continue;

                    var pData = filteredDatas.Where(x => ParsePoolType(x.CardPoolType) == (int)type).ToList();
                    if (!pData.Any()) continue;

                    int tides = pData.Count;
                    int goldCount = pData.Count(x => x.QualityLevel == 5);

                    compareXLabels.Add(EnumExtensions.GetDescription(type));
                    tidesData.Add(tides);
                    astritesData.Add(tides * 160);
                    avgTideData.Add(goldCount > 0 ? (double)tides / goldCount : 0);
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var textColorObj = (System.Windows.Media.Color)Application.Current.Resources["TextMainColor"];
                    var textColor = new SKColor(textColorObj.R, textColorObj.G, textColorObj.B, textColorObj.A);
                    var textPaint = new SolidColorPaint(textColor);

                    var strokeColorObj = (System.Windows.Media.Color)Application.Current.Resources["StrokeColor"];
                    var strokeColor = new SKColor(strokeColorObj.R, strokeColorObj.G, strokeColorObj.B, strokeColorObj.A);
                    var separatorPaint = new SolidColorPaint(strokeColor) { StrokeThickness = 1 };

                    var primaryColorObj = (System.Windows.Media.Color)Application.Current.Resources["PrimaryColor"];
                    var primaryColor = new SKColor(primaryColorObj.R, primaryColorObj.G, primaryColorObj.B, primaryColorObj.A);

                    var warningColorObj = (System.Windows.Media.Color)Application.Current.Resources["WarningColor"];
                    var warningColor = new SKColor(warningColorObj.R, warningColorObj.G, warningColorObj.B, warningColorObj.A);

                    if (SuccessRatePieSeries == null)
                    {
                        SuccessRatePieSeries = new ISeries[]
                        {
                            new PieSeries<int> { Values = new[] { success }, Name = LanguageManager.Instance["Stat_SuccessCount"] ?? "不歪", InnerRadius = 40, Fill = new SolidColorPaint(primaryColor) },
                            new PieSeries<int> { Values = new[] { miss }, Name = LanguageManager.Instance["Stat_MissCount"] ?? "歪卡", InnerRadius = 40, Fill = new SolidColorPaint(warningColor) }
                        };
                    }
                    else
                    {
                        ((PieSeries<int>)SuccessRatePieSeries[0]).Values = new[] { success };
                        ((PieSeries<int>)SuccessRatePieSeries[1]).Values = new[] { miss };
                    }

                    if (FourStarPieSeries == null)
                    {
                        FourStarPieSeries = new ISeries[]
                        {
                            new PieSeries<int> { Values = new[] { fourStarCharacterCount }, Name = LanguageManager.Instance["Role"] ?? "角色", InnerRadius = 25, Fill = new SolidColorPaint(primaryColor) },
                            new PieSeries<int> { Values = new[] { fourStarWeaponCount }, Name = LanguageManager.Instance["Weapon"] ?? "武器", InnerRadius = 25, Fill = new SolidColorPaint(warningColor) }
                        };
                    }
                    else
                    {
                        ((PieSeries<int>)FourStarPieSeries[0]).Values = new[] { fourStarCharacterCount };
                        ((PieSeries<int>)FourStarPieSeries[1]).Values = new[] { fourStarWeaponCount };
                    }

                    if (GlobalPoolCompareSeries == null)
                    {
                        GlobalPoolCompareSeries = new ISeries[]
                        {
                            new ColumnSeries<int> { Values = tidesData, Name = LanguageManager.Instance["Stat_TotalTides"] ?? "抽数", ScalesYAt = 0, Fill = new SolidColorPaint(primaryColor) },
                            new LineSeries<double> { Values = avgTideData, Name = LanguageManager.Instance["Stat_AvgGold"] ?? "平均水位", ScalesYAt = 1, GeometrySize = 10, Stroke = new SolidColorPaint(warningColor) { StrokeThickness = 3 }, GeometryFill = new SolidColorPaint(warningColor), GeometryStroke = new SolidColorPaint(warningColor) }
                        };
                        
                        GlobalPoolXAxes = new[] { new Axis { Labels = compareXLabels, LabelsRotation = 15, LabelsPaint = textPaint, SeparatorsPaint = separatorPaint } };
                        GlobalPoolYAxes = new[]
                        {
                            new Axis { Position = LiveChartsCore.Measure.AxisPosition.Start, Name = LanguageManager.Instance["Stat_TotalTides"] ?? "Count", LabelsPaint = textPaint, NamePaint = textPaint, SeparatorsPaint = separatorPaint },
                            new Axis { Position = LiveChartsCore.Measure.AxisPosition.End, Name = LanguageManager.Instance["Stat_AvgGold"] ?? "Avg Tide", ShowSeparatorLines = false, LabelsPaint = textPaint, NamePaint = textPaint }
                        };
                    }
                    else
                    {
                        ((ColumnSeries<int>)GlobalPoolCompareSeries[0]).Values = tidesData;
                        ((LineSeries<double>)GlobalPoolCompareSeries[1]).Values = avgTideData;
                        GlobalPoolXAxes[0].Labels = compareXLabels;
                    }
                });
            }
            finally
            {
                _isUpdatingCharts = false;
            }
        }

        private int ParsePoolType(string poolStr)
        {
            if (string.IsNullOrEmpty(poolStr)) return 0;
            if (int.TryParse(poolStr, out int result)) return result;

            foreach (var type in Enum.GetValues<CardPoolType>())
            {
                if (EnumExtensions.GetDescription(type) == poolStr)
                {
                    return (int)type;
                }
            }
            return 0;
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
                    var allGachaDatas = new List<GachaData>();
                    // 后台读取数据库文件
                    await Task.Run(async () =>
                    {
                        foreach (var type in Enum.GetValues<CardPoolType>())
                        {
                            var localData = await _gachaRepository.GetPoolRecordsByUid(SelectedUser.Uid, (int)type);

                            if (localData != null)
                            {
                                allGachaDatas.AddRange(localData);
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    var pool = PoolStatistics.FirstOrDefault(x => x.PoolType == type);
                                    if (pool != null)
                                    {
                                        var res = _gachaStatisticsService.OrganizeData(localData, type, LanguageTypeExtensions.GetCode(_configService.User.AppLanguage));

                                        pool.HitGoldDatas.Clear();
                                        foreach (var d in res.PoolStatistics.HitGoldDatas) pool.HitGoldDatas.Add(d);

                                        pool.Calculate = res.PoolStatistics.Calculate;

                                        if (type == CardPoolType.CharacterEvent)
                                        {
                                            SuccessCount = res.SuccessCount;
                                            MissCount = res.MissCount;
                                        }

                                        var chartData = PoolCharts.FirstOrDefault(x => x.PoolType == pool.PoolType);
                                        if (chartData == null)
                                        {
                                            chartData = new CardPoolChartData { PoolType = pool.PoolType };
                                            PoolCharts.Add(chartData);
                                        }

                                        chartData.GoldHistorySeries = _chartBuilderService.BuildGoldHistorySeries(res.GoldValues, LanguageManager.Instance["Msg_Pity"], chartData.GoldHistorySeries);
                                        chartData.XAxes = _chartBuilderService.BuildGoldHistoryXAxes(res.GoldLabels, chartData.XAxes);
                                    }
                                });
                            }
                        }
                    });
                    await Statistics(allGachaDatas);

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
                    var users = await Task.Run(async () => await _userRepository.GetAllUserAccountAsync());

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