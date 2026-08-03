using LiveChartsCore;
using LiveChartsCore.Defaults;
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
using WwTool.Common.Models.Entities;
using WwTool.Common.Models.Domain;
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
        private CancellationTokenSource _navigationCts = new();
        private readonly IGetDataService _getDataService;
        private readonly IDialogService _dialogService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IUIStateService _uiStateService;
        private readonly IConfigService _configService;
        private readonly GameDataService _gameData;
        private readonly IUserDataService _userDataService;
        private readonly IGachaStatisticsService _gachaStatisticsService;
        private readonly IChartBuilderService _chartBuilderService;
        private readonly ILoggerService _logger;
        private readonly IGachaLogLocator _gachaLogLocator;

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext) => _navigationCts.Cancel();

        public StatisticsViewModel(IEventAggregator eventAggregator, IUIStateService uIStateService, IGetDataService getDataService, IDialogService dialogService, IConfigService configService, GameDataService gameData, IUserDataService userDataService, IGachaStatisticsService gachaStatisticsService, IChartBuilderService chartBuilderService, ILoggerService logger, IGachaLogLocator gachaLogLocator)
        {
            _uiStateService = uIStateService;
            _eventAggregator = eventAggregator;
            _getDataService = getDataService;
            _dialogService = dialogService;
            _configService = configService;
            _gameData = gameData;
            _userDataService = userDataService;
            _gachaStatisticsService = gachaStatisticsService;
            _chartBuilderService = chartBuilderService;
            _logger = logger;
            _gachaLogLocator = gachaLogLocator;
            _selectedGachaServerRegion = _configService.User.GachaServerRegion;

            PoolStatistics = new ObservableCollection<CardPoolStatistics>(Enum.GetValues<CardPoolType>().Select(x => new CardPoolStatistics { PoolType = x }));
            Users = new();
            AutoImportUrlCommand = new DelegateCommand(async () => await AutoImportUrlAsync());
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
                if (e.PropertyName == nameof(_configService.User.BaseTheme) ||
                    e.PropertyName == nameof(_configService.User.AccentTheme) ||
                    e.PropertyName == nameof(_configService.User.AppLanguage))
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
        private bool _isSelectingInitialAccount;

        /// <summary>
        /// 页面导航进入时触发，执行初始化流程
        /// </summary>
        async void IRegionAware.OnNavigatedTo(NavigationContext navigationContext)
        {
            ResetNavigationCancellation();
            SelectedStatisticsTabIndex = 0;
            SelectedPoolStatisticsIndex = 0;
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

        private bool _importFullLine = false;
        public bool ImportFullLine
        {
            get => _importFullLine;
            set
            {
                if (SetProperty(ref _importFullLine, value))
                {
                    if (!value && !string.IsNullOrEmpty(LogUrl))
                    {
                        LogUrl = LogUrl;
                    }
                }
            }
        }

        private string? _logUrl;
        public string? LogUrl
        {
            get => _logUrl;
            set
            {
                var processedValue = value;
                if (!ImportFullLine && !string.IsNullOrEmpty(processedValue))
                {
                    var match = Regex.Match(processedValue, @"https?://[^\s""'\n\r]+");
                    if (match.Success)
                    {
                        processedValue = match.Value;
                    }
                }
                _logUrl = processedValue;
                RaisePropertyChanged();
                UpdateGachaServerSelection(processedValue);
            }
        }

        private GachaServerRegion _selectedGachaServerRegion;
        private bool _isGachaServerLockedByUrl;
        private bool _isGachaImportInProgress;

        public bool IsChinaGachaServer
        {
            get => _selectedGachaServerRegion == GachaServerRegion.China;
            set
            {
                if (value && IsGachaServerSelectionEnabled)
                {
                    SetManualGachaServer(GachaServerRegion.China);
                }
            }
        }

        public bool IsInternationalGachaServer
        {
            get => _selectedGachaServerRegion == GachaServerRegion.International;
            set
            {
                if (value && IsGachaServerSelectionEnabled)
                {
                    SetManualGachaServer(GachaServerRegion.International);
                }
            }
        }

        public bool IsGachaServerSelectionEnabled => !_isGachaServerLockedByUrl && !_isGachaImportInProgress;

        private void SetManualGachaServer(GachaServerRegion region)
        {
            _configService.User.GachaServerRegion = region;
            SetEffectiveGachaServer(region);
        }

        private void SetEffectiveGachaServer(GachaServerRegion region)
        {
            if (_selectedGachaServerRegion == region)
            {
                return;
            }

            _selectedGachaServerRegion = region;
            RaisePropertyChanged(nameof(IsChinaGachaServer));
            RaisePropertyChanged(nameof(IsInternationalGachaServer));
        }

        private void UpdateGachaServerSelection(string? input)
        {
            bool wasEnabled = IsGachaServerSelectionEnabled;
            if (GachaServerDetector.TryDetect(input, out GachaServerRegion detectedRegion))
            {
                _isGachaServerLockedByUrl = true;
                SetEffectiveGachaServer(detectedRegion);
            }
            else
            {
                _isGachaServerLockedByUrl = false;
                SetEffectiveGachaServer(_configService.User.GachaServerRegion);
            }

            if (wasEnabled != IsGachaServerSelectionEnabled)
            {
                RaisePropertyChanged(nameof(IsGachaServerSelectionEnabled));
            }
        }

        private void SetGachaImportInProgress(bool value)
        {
            if (_isGachaImportInProgress == value)
            {
                return;
            }

            _isGachaImportInProgress = value;
            RaisePropertyChanged(nameof(IsGachaServerSelectionEnabled));
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
            if (_isSelectingInitialAccount) return;

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

        private ObservableCollection<AccountSummary> _users = new();
        public ObservableCollection<AccountSummary> Users
        {
            get => _users; set
            {
                _users = value;
                RaisePropertyChanged();
            }
        }

        // 卡池数据统计
        private ObservableCollection<CardPoolStatistics> _poolStatistics = new();
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
        private ISeries[] _globalPoolCompareSeries = [];
        public ISeries[] GlobalPoolCompareSeries { get => _globalPoolCompareSeries; set { _globalPoolCompareSeries = value; RaisePropertyChanged(); } }

        private Axis[] _globalPoolXAxes = [];
        public Axis[] GlobalPoolXAxes { get => _globalPoolXAxes; set { _globalPoolXAxes = value; RaisePropertyChanged(); } }

        private Axis[] _globalPoolYAxes = [];
        public Axis[] GlobalPoolYAxes { get => _globalPoolYAxes; set { _globalPoolYAxes = value; RaisePropertyChanged(); } }

        private ISeries[] _successRatePieSeries = [];
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

        private ISeries[] _fourStarPieSeries = [];
        public ISeries[] FourStarPieSeries
        {
            get => _fourStarPieSeries;
            set { _fourStarPieSeries = value; RaisePropertyChanged(); }
        }

        private ISeries[] _dailyPullLineSeries = [];
        public ISeries[] DailyPullLineSeries
        {
            get => _dailyPullLineSeries;
            set { _dailyPullLineSeries = value; RaisePropertyChanged(); }
        }

        private int _selectedStatisticsTabIndex;
        public int SelectedStatisticsTabIndex
        {
            get => _selectedStatisticsTabIndex;
            set => SetProperty(ref _selectedStatisticsTabIndex, value);
        }

        private int _selectedPoolStatisticsIndex;
        public int SelectedPoolStatisticsIndex
        {
            get => _selectedPoolStatisticsIndex;
            set => SetProperty(ref _selectedPoolStatisticsIndex, value);
        }

        private bool _includeIncompleteFeaturedSegment;
        public bool IncludeIncompleteFeaturedSegment
        {
            get => _includeIncompleteFeaturedSegment;
            set { if (SetProperty(ref _includeIncompleteFeaturedSegment, value)) _ = UpdateChartsAsync(); }
        }

        private ISeries[] _pityDistributionSeries = [];
        public ISeries[] PityDistributionSeries { get => _pityDistributionSeries; set => SetProperty(ref _pityDistributionSeries, value); }
        public Axis[] PityDistributionXAxes { get; set; } = [];
        public Axis[] PityDistributionYAxes { get; set; } = [];
        private ISeries[] _fiveStarTimelineSeries = [];
        public ISeries[] FiveStarTimelineSeries { get => _fiveStarTimelineSeries; set => SetProperty(ref _fiveStarTimelineSeries, value); }
        public Axis[] FiveStarTimelineXAxes { get; set; } = [];
        public Axis[] FiveStarTimelineYAxes { get; set; } = [];
        private ISeries[] _rarityStackedSeries = [];
        public ISeries[] RarityStackedSeries { get => _rarityStackedSeries; set => SetProperty(ref _rarityStackedSeries, value); }
        public Axis[] RarityStackedXAxes { get; set; } = [];
        public Axis[] RarityStackedYAxes { get; set; } = [];
        private ISeries[] _activityHeatSeries = [];
        public ISeries[] ActivityHeatSeries { get => _activityHeatSeries; set => SetProperty(ref _activityHeatSeries, value); }
        public Axis[] ActivityHeatXAxes { get; set; } = [];
        public Axis[] ActivityHeatYAxes { get; set; } = [];
        private ISeries[] _cumulativeTrendSeries = [];
        public ISeries[] CumulativeTrendSeries { get => _cumulativeTrendSeries; set => SetProperty(ref _cumulativeTrendSeries, value); }
        public Axis[] CumulativeTrendXAxes { get; set; } = [];
        public Axis[] CumulativeTrendYAxes { get; set; } = [];
        private ISeries[] _currentPityGaugeSeries = [];
        public ISeries[] CurrentPityGaugeSeries { get => _currentPityGaugeSeries; set => SetProperty(ref _currentPityGaugeSeries, value); }
        private int _currentCharacterPity;
        public int CurrentCharacterPity { get => _currentCharacterPity; set => SetProperty(ref _currentCharacterPity, value); }
        private ISeries[] _featuredExpectationSeries = [];
        public ISeries[] FeaturedExpectationSeries { get => _featuredExpectationSeries; set => SetProperty(ref _featuredExpectationSeries, value); }
        public Axis[] FeaturedExpectationXAxes { get; set; } = [];
        public Axis[] FeaturedExpectationYAxes { get; set; } = [];

        private SolidColorPaint _chartLegendTextPaint = new(SKColors.Black);
        public SolidColorPaint ChartLegendTextPaint
        {
            get => _chartLegendTextPaint;
            set => SetProperty(ref _chartLegendTextPaint, value);
        }

        private SolidColorPaint _chartLegendBackgroundPaint = new(SKColors.Transparent);
        public SolidColorPaint ChartLegendBackgroundPaint
        {
            get => _chartLegendBackgroundPaint;
            set => SetProperty(ref _chartLegendBackgroundPaint, value);
        }

        private SolidColorPaint _chartTooltipTextPaint = new(SKColors.Black);
        public SolidColorPaint ChartTooltipTextPaint
        {
            get => _chartTooltipTextPaint;
            set => SetProperty(ref _chartTooltipTextPaint, value);
        }

        private SolidColorPaint _chartTooltipBackgroundPaint = new(SKColors.White);
        public SolidColorPaint ChartTooltipBackgroundPaint
        {
            get => _chartTooltipBackgroundPaint;
            set => SetProperty(ref _chartTooltipBackgroundPaint, value);
        }

        private Axis[] _dailyXAxes = [];
        public Axis[] DailyXAxes
        {
            get => _dailyXAxes;
            set { _dailyXAxes = value; RaisePropertyChanged(); }
        }

        private Axis[] _dailyYAxes = [];
        public Axis[] DailyYAxes
        {
            get => _dailyYAxes;
            set { _dailyYAxes = value; RaisePropertyChanged(); }
        }

        private const int TrendViewportSize = 30;
        private double _trendViewportStart;
        public double TrendViewportStart
        {
            get => _trendViewportStart;
            set
            {
                if (SetProperty(ref _trendViewportStart, value))
                {
                    ApplyTrendViewport();
                }
            }
        }

        private double _trendViewportMaximum;
        public double TrendViewportMaximum
        {
            get => _trendViewportMaximum;
            set => SetProperty(ref _trendViewportMaximum, value);
        }

        private bool _isTrendViewportEnabled;
        public bool IsTrendViewportEnabled
        {
            get => _isTrendViewportEnabled;
            set => SetProperty(ref _isTrendViewportEnabled, value);
        }

        private int _filteredPullCount;
        public int FilteredPullCount
        {
            get => _filteredPullCount;
            set => SetProperty(ref _filteredPullCount, value);
        }

        private int _filteredGoldCount;
        public int FilteredGoldCount
        {
            get => _filteredGoldCount;
            set => SetProperty(ref _filteredGoldCount, value);
        }

        private double _filteredAveragePity;
        public double FilteredAveragePity
        {
            get => _filteredAveragePity;
            set => SetProperty(ref _filteredAveragePity, value);
        }

        private int _filteredActiveDays;
        public int FilteredActiveDays
        {
            get => _filteredActiveDays;
            set => SetProperty(ref _filteredActiveDays, value);
        }

        private string _peakDaySummary = "-";
        public string PeakDaySummary
        {
            get => _peakDaySummary;
            set => SetProperty(ref _peakDaySummary, value);
        }
        #endregion

        #region 统计数据
        private int _totalTides;            // 总抽数
        private int _totalAstrites;         // 总星声花费
        private int _totalHitGold;          // 总出金数
        private int _missCount;             // 角色限定池歪卡次数
        private int _successCount;          // 角色限定池不歪次数
        private int _featuredCharacterCount; // 角色限定池 UP 五星总数（含大保底）
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
                    var newUser = new AccountSummary { Uid = info.PlayerId };
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

                _isSelectingInitialAccount = true;
                try
                {
                    await LoadLocalAccount();
                }
                finally
                {
                    _isSelectingInitialAccount = false;
                }

                if (SelectedUser != null && !_allCachedGachaDatas.Any())
                {
                    await LoadLocalGachaLog();
                }
                _isInitialized = true;
                return;
            }

            if (_allCachedGachaDatas.Any())
            {
                await Statistics();
            }
        }

        /// <summary>
        /// 从游戏日志中自动提取抽卡查询 URL
        /// </summary>
        private async Task AutoImportUrlAsync()
        {
            _logger.Info("在 StatisticsViewModel 中调用了 AutoImportUrl 命令");
            await ExceptionHelper.ExecuteAsync(async () =>
            {
                if (string.IsNullOrEmpty(_configService.User.GamePath))
                {
                    throw new WwToolGamePathException(LanguageManager.Instance["Msg_NoGamePath"]);
                }
                var keyword = _configService.User.SearchGachaApiUrl ?? string.Empty;
                LogUrl = await _gachaLogLocator.FindLatestQueryUrlAsync(
                    _configService.User.GamePath,
                    _configService.App.GameLogPath,
                    _configService.App.GameLogFile,
                    keyword,
                    _navigationCts.Token);
                RefreshQueryData();
                _uiStateService.ShowToast(LanguageManager.Instance["Msg_AutoImportSuccessTitle"], LanguageManager.Instance["Msg_AutoImportSuccess"], NotificationType.Success);
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
        private async Task<IEnumerable<GachaData>> GetGachaLog(int poolType, GachaServerRegion serverRegion)
        {
            if (string.IsNullOrEmpty(_logUrl))
                return [];
            var param = GachaUrlParser.Parse(_logUrl);
            param.LanguageCode = LanguageTypeExtensions.GetCode(_configService.User.AppLanguage);
            param.CardPoolType = poolType;

            var data = await _getDataService.GetGachaLogAsync(param, serverRegion, _navigationCts.Token);

            return data;

        }

        /// <summary>
        /// 从服务器同步所有卡池的抽卡数据，并更新统计结果
        /// </summary>
        private async Task StatisticsDatas()
        {
            GachaServerRegion serverRegion = _selectedGachaServerRegion;
            SetGachaImportInProgress(true);
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
                        _uiStateService.ShowLoading(string.Format(LanguageManager.Instance["Msg_SyncingPool"], type.GetLocalizedDescription()));
                        var gachaData = await GetGachaLog((int)type, serverRegion);
                        await _userDataService.ImportGachaAsync(SelectedUser.Uid, (int)type, gachaData, "remote", _navigationCts.Token);
                    }

                    _uiStateService.ShowLoading(LanguageManager.Instance["Msg_SyncFinishedProcessing"]);
                    var allGachaDatas = new List<GachaData>();
                    await Task.Run(async () =>
                    {
                        foreach (var type in Enum.GetValues<CardPoolType>())
                        {
                            var data = await _userDataService.ReadGachaInSourceOrderAsync(SelectedUser.Uid, (int)type, _navigationCts.Token);
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
                                            _featuredCharacterCount = res.FeaturedCount;
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
                SetGachaImportInProgress(false);
                UpdateGachaServerSelection(_logUrl);
            }
        }

        /// <summary>
        /// 汇总计算所有卡池的统计数据（总抽数、总花费、不歪率等）
        /// </summary>
        private async Task Statistics(List<GachaData>? allGachaDatas = null)
        {
            var globalStats = _gachaStatisticsService.CalculateGlobalStatistics(
                PoolStatistics,
                SuccessCount,
                _featuredCharacterCount);

            TotalTides = globalStats.TotalTides;
            TotalAstrites = globalStats.TotalAstrites;
            TotalHitGold = globalStats.TotalHitGold;
            SuccessRate = globalStats.SuccessRate;
            LimitedGoldCount = globalStats.LimitedGoldCount;
            AvgCharaTide = globalStats.AvgCharaTide;
            AvgLimitCharaTide = globalStats.AvgLimitCharaTide;

            if (allGachaDatas != null)
            {
                _allCachedGachaDatas = allGachaDatas;
            }

            await UpdateChartsAsync();
        }

        private bool _isUpdatingCharts = false;
        private bool _isLoadingLocalGachaLog;
        private bool _isStatisticsLoading;
        public bool IsStatisticsLoading { get => _isStatisticsLoading; set => SetProperty(ref _isStatisticsLoading, value); }
        private bool _hasStatisticsData;
        public bool HasStatisticsData { get => _hasStatisticsData; set => SetProperty(ref _hasStatisticsData, value); }
        private string? _statisticsErrorMessage;
        public string? StatisticsErrorMessage { get => _statisticsErrorMessage; set => SetProperty(ref _statisticsErrorMessage, value); }
        private async Task UpdateChartsAsync()
        {
            if (_isUpdatingCharts) return;
            _isUpdatingCharts = true;

            try
            {
                if (_allCachedGachaDatas == null || !_allCachedGachaDatas.Any())
                {
                    FilteredPullCount = 0;
                    FilteredGoldCount = 0;
                    FilteredAveragePity = 0;
                    FilteredActiveDays = 0;
                    PeakDaySummary = "-";
                    FilteredHitGoldFlow = new();
                    DailyPullLineSeries = [];
                    DailyXAxes = [];
                    DailyYAxes = [];
                    TrendViewportMaximum = 0;
                    TrendViewportStart = 0;
                    IsTrendViewportEnabled = false;
                    SuccessRatePieSeries = [];
                    FourStarPieSeries = [];
                    GlobalPoolCompareSeries = [];
                    GlobalPoolXAxes = [];
                    GlobalPoolYAxes = [];
                    ClearInsightCharts();
                    return;
                }

                var filteredDatas = _allCachedGachaDatas.Where(x =>
                {
                    if (SelectedDateRangeIndex == 1 && DateTime.TryParse(x.Time, out var dt1) && dt1 < DateTime.Now.AddMonths(-1)) return false;
                    if (SelectedDateRangeIndex == 2 && DateTime.TryParse(x.Time, out var dt2) && dt2 < DateTime.Now.AddMonths(-3)) return false;

                    if (!PoolFilters.Any(f => f.IsSelected && (int)f.PoolType == ParsePoolType(x.CardPoolType))) return false;
                    return true;
                }).ToList();

                GachaInsights insights = _gachaStatisticsService.CalculateInsights(
                    filteredDatas,
                    IncludeIncompleteFeaturedSegment);

                var dailyBuckets = new SortedDictionary<DateTime, (int Pulls, int Golds)>();
                foreach (var item in filteredDatas)
                {
                    if (!DateTime.TryParse(item.Time, out var pullTime)) continue;

                    var day = pullTime.Date;
                    dailyBuckets.TryGetValue(day, out var bucket);
                    dailyBuckets[day] = (
                        bucket.Pulls + 1,
                        bucket.Golds + (item.QualityLevel == 5 ? 1 : 0));
                }

                int labelStep = Math.Max(1, (int)Math.Ceiling(dailyBuckets.Count / 12d));
                var dailyLabels = dailyBuckets.Keys
                    .Select((date, index) => index % labelStep == 0 || index == dailyBuckets.Count - 1
                        ? date.ToString("MM-dd")
                        : string.Empty)
                    .ToList();
                var dailyPulls = dailyBuckets.Values.Select(x => x.Pulls).ToList();
                var dailyGolds = dailyBuckets.Values.Select(x => x.Golds).ToList();
                var peakDay = dailyBuckets.OrderByDescending(x => x.Value.Pulls).FirstOrDefault();

                IsTrendViewportEnabled = dailyBuckets.Count > TrendViewportSize;
                TrendViewportMaximum = Math.Max(0, dailyBuckets.Count - TrendViewportSize);
                TrendViewportStart = TrendViewportMaximum;

                FilteredPullCount = filteredDatas.Count;
                FilteredGoldCount = filteredDatas.Count(x => x.QualityLevel == 5);
                FilteredAveragePity = FilteredGoldCount == 0
                    ? 0
                    : (double)FilteredPullCount / FilteredGoldCount;
                FilteredActiveDays = dailyBuckets.Count;
                PeakDaySummary = dailyBuckets.Count == 0
                    ? "-"
                    : $"{peakDay.Key:MM-dd} · {peakDay.Value.Pulls}";

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

                        string localizedGoldName = GetLocalizedItemName(hit.GachaData.ResourceId, hit.GachaData.Name);
                        if (!string.IsNullOrEmpty(SelectedGoldName) && SelectedGoldName != (LanguageManager.Instance["Stat_All"] ?? "全部") && localizedGoldName != SelectedGoldName && hit.GachaData.ResourceId != 0) continue;

                        flowDatas.Add(hit);
                    }
                }

                // 更新明细
                Application.Current.Dispatcher.Invoke(() =>
                {
                    FilteredHitGoldFlow = new ObservableCollection<HitGoldData>(flowDatas.OrderByDescending(x => x.GachaData.Time));
                });

                // 提取所有的五星供下拉框选择并进行翻译
                var goldItems = _allCachedGachaDatas
                    .Where(x => x.QualityLevel == 5)
                    .GroupBy(x => x.ResourceId)
                    .Select(g => new { ResourceId = g.Key, OriginalName = g.First().Name })
                    .ToList();

                var allGolds = goldItems.Select(x => GetLocalizedItemName(x.ResourceId, x.OriginalName)).Distinct().ToList();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var curSelected = SelectedGoldName;
                    bool wasAllSelected = string.IsNullOrEmpty(curSelected) ||
                                          curSelected == "全部" ||
                                          curSelected == "All" ||
                                          curSelected == "全て" ||
                                          curSelected == (LanguageManager.Instance["Stat_All"] ?? "全部");

                    AllGotGoldNames.Clear();
                    string localizedAll = LanguageManager.Instance["Stat_All"] ?? "全部";
                    AllGotGoldNames.Add(localizedAll);
                    foreach (var g in allGolds) AllGotGoldNames.Add(g);

                    if (wasAllSelected)
                    {
                        SelectedGoldName = localizedAll;
                    }
                    else
                    {
                        int selectedResourceId = 0;
                        foreach (var g in goldItems)
                        {
                            if (GetLocalizedItemName(g.ResourceId, g.OriginalName, LanguageType.ZhHans) == curSelected ||
                                GetLocalizedItemName(g.ResourceId, g.OriginalName, LanguageType.En) == curSelected ||
                                GetLocalizedItemName(g.ResourceId, g.OriginalName, LanguageType.Ja) == curSelected)
                            {
                                selectedResourceId = g.ResourceId;
                                break;
                            }
                        }

                        if (selectedResourceId != 0)
                        {
                            string newSelectedName = GetLocalizedItemName(selectedResourceId, "");
                            if (AllGotGoldNames.Contains(newSelectedName))
                            {
                                SelectedGoldName = newSelectedName;
                            }
                            else
                            {
                                SelectedGoldName = localizedAll;
                            }
                        }
                        else
                        {
                            SelectedGoldName = localizedAll;
                        }
                    }
                });

                // 四星及歪率
                int fourStarCharacterCount = 0;
                int fourStarWeaponCount = 0;
                int success = 0;

                foreach (var item in filteredDatas)
                {
                    if (item.QualityLevel == 4)
                    {
                        var itemInfo = _gameData.GetItemById(item.ResourceId);
                        string typeStr = itemInfo?.Type ?? item.ResourceType;
                        if (typeStr.Contains("角色") || typeStr.Contains("Role") || typeStr.Contains("Character")) fourStarCharacterCount++;
                        else fourStarWeaponCount++;
                    }

                }

                var filteredCharacterEventStats = _gachaStatisticsService.OrganizeData(
                    filteredDatas.Where(x => ParsePoolType(x.CardPoolType) == (int)CardPoolType.CharacterEvent),
                    CardPoolType.CharacterEvent,
                    LanguageTypeExtensions.GetCode(_configService.User.AppLanguage));
                success = filteredCharacterEventStats.SuccessCount;
                int otherFiveStars = Math.Max(
                    0,
                    filteredCharacterEventStats.PoolStatistics.Calculate.HitGoldCount - success);

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

                    compareXLabels.Add(type.GetLocalizedDescription());
                    tidesData.Add(tides);
                    astritesData.Add(tides * 160);
                    avgTideData.Add(goldCount > 0 ? (double)tides / goldCount : 0);
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    ChartThemePalette palette = GetChartThemePalette();
                    var axisTextPaint = new SolidColorPaint(palette.TextSecondary);
                    var separatorPaint = new SolidColorPaint(palette.Stroke.WithAlpha((byte)Math.Min((int)palette.Stroke.Alpha, 120))) { StrokeThickness = 1 };

                    ChartLegendTextPaint = new SolidColorPaint(palette.TextSecondary);
                    ChartLegendBackgroundPaint = new SolidColorPaint(SKColors.Transparent);
                    ChartTooltipTextPaint = new SolidColorPaint(palette.TextPrimary);
                    ChartTooltipBackgroundPaint = new SolidColorPaint(
                        palette.SurfaceElevated.WithAlpha((byte)Math.Min((int)palette.SurfaceElevated.Alpha, 236)));

                    foreach (CardPoolChartData poolChart in PoolCharts)
                    {
                        foreach (ISeries series in poolChart.GoldHistorySeries)
                        {
                            if (series is ColumnSeries<int> columns)
                            {
                                columns.Fill = new SolidColorPaint(palette.Primary);
                                columns.DataLabelsPaint = new SolidColorPaint(palette.TextPrimary);
                            }
                        }

                        foreach (Axis axis in poolChart.XAxes)
                        {
                            axis.LabelsPaint = new SolidColorPaint(palette.TextSecondary);
                            axis.SeparatorsPaint = new SolidColorPaint(palette.Stroke) { StrokeThickness = 1 };
                        }
                    }

                    BuildInsightCharts(insights, axisTextPaint, separatorPaint, palette);

                    DailyPullLineSeries = new ISeries[]
                    {
                        new ColumnSeries<int>
                        {
                            Values = dailyPulls,
                            Name = LanguageManager.Instance["Stat_DailyPulls"] ?? "当日抽数",
                            MaxBarWidth = 24,
                            Fill = new SolidColorPaint(palette.Primary),
                            DataLabelsPaint = new SolidColorPaint(palette.TextPrimary)
                        },
                        new LineSeries<int>
                        {
                            Values = dailyGolds,
                            Name = LanguageManager.Instance["Stat_DailyGolds"] ?? "当日五星",
                            ScalesYAt = 1,
                            GeometrySize = 8,
                            Stroke = new SolidColorPaint(palette.Warning) { StrokeThickness = 3 },
                            GeometryFill = new SolidColorPaint(palette.Warning),
                            GeometryStroke = new SolidColorPaint(palette.Warning)
                        }
                    };
                    DailyXAxes = new[]
                    {
                        new Axis
                        {
                            Labels = dailyLabels,
                            LabelsRotation = dailyLabels.Count > 14 ? 45 : 0,
                            LabelsPaint = axisTextPaint,
                            SeparatorsPaint = separatorPaint,
                            MinLimit = IsTrendViewportEnabled ? TrendViewportStart - 0.5 : null,
                            MaxLimit = IsTrendViewportEnabled ? TrendViewportStart + TrendViewportSize - 0.5 : null
                        }
                    };
                    DailyYAxes = new[]
                    {
                        new Axis
                        {
                            Position = LiveChartsCore.Measure.AxisPosition.Start,
                            MinLimit = 0,
                            LabelsPaint = axisTextPaint,
                            SeparatorsPaint = separatorPaint
                        },
                        new Axis
                        {
                            Position = LiveChartsCore.Measure.AxisPosition.End,
                            MinLimit = 0,
                            ShowSeparatorLines = false,
                            LabelsPaint = axisTextPaint
                        }
                    };

                    SuccessRatePieSeries =
                    [
                        new PieSeries<int> { Values = [success], Name = LanguageManager.Instance["Stat_SuccessCount"] ?? "不歪", InnerRadius = 40, Fill = new SolidColorPaint(palette.Success) },
                        new PieSeries<int> { Values = [otherFiveStars], Name = LanguageManager.Instance["Stat_OtherGoldCount"] ?? "其他五星", InnerRadius = 40, Fill = new SolidColorPaint(palette.Warning) }
                    ];

                    FourStarPieSeries =
                    [
                        new PieSeries<int> { Values = [fourStarCharacterCount], Name = LanguageManager.Instance["Role"] ?? "角色", InnerRadius = 25, Fill = new SolidColorPaint(palette.Primary) },
                        new PieSeries<int> { Values = [fourStarWeaponCount], Name = LanguageManager.Instance["Weapon"] ?? "武器", InnerRadius = 25, Fill = new SolidColorPaint(palette.FourStar) }
                    ];

                    GlobalPoolCompareSeries =
                    [
                        new ColumnSeries<int> { Values = tidesData, Name = LanguageManager.Instance["Stat_TotalTides"] ?? "抽数", ScalesYAt = 0, Fill = new SolidColorPaint(palette.Primary) },
                        new LineSeries<double> { Values = avgTideData, Name = LanguageManager.Instance["Stat_AvgGold"] ?? "平均水位", ScalesYAt = 1, GeometrySize = 10, Stroke = new SolidColorPaint(palette.Warning) { StrokeThickness = 3 }, GeometryFill = new SolidColorPaint(palette.Warning), GeometryStroke = new SolidColorPaint(palette.Warning) }
                    ];

                    GlobalPoolXAxes = [new Axis { Labels = compareXLabels, LabelsRotation = 15, LabelsPaint = axisTextPaint, SeparatorsPaint = separatorPaint }];
                    GlobalPoolYAxes =
                    [
                        new Axis { Position = LiveChartsCore.Measure.AxisPosition.Start, Name = LanguageManager.Instance["Stat_TotalTides"] ?? "Count", LabelsPaint = axisTextPaint, NamePaint = axisTextPaint, SeparatorsPaint = separatorPaint },
                        new Axis { Position = LiveChartsCore.Measure.AxisPosition.End, Name = LanguageManager.Instance["Stat_AvgGold"] ?? "Avg Tide", ShowSeparatorLines = false, LabelsPaint = axisTextPaint, NamePaint = axisTextPaint }
                    ];
                });
            }
            finally
            {
                _isUpdatingCharts = false;
            }
        }

        private void ClearInsightCharts()
        {
            PityDistributionSeries = [];
            FiveStarTimelineSeries = [];
            RarityStackedSeries = [];
            ActivityHeatSeries = [];
            CumulativeTrendSeries = [];
            CurrentPityGaugeSeries = [];
            FeaturedExpectationSeries = [];
            CurrentCharacterPity = 0;
        }

        private void BuildInsightCharts(
            GachaInsights insights,
            SolidColorPaint axisTextPaint,
            SolidColorPaint separatorPaint,
            ChartThemePalette palette)
        {
            PityDistributionSeries =
            [
                new ColumnSeries<int>
                {
                    Values = insights.PityDistribution,
                    Name = LanguageManager.Instance["Stat_PityDistribution"] ?? "Pity distribution",
                    Fill = new SolidColorPaint(palette.Primary),
                    MaxBarWidth = 36
                }
            ];
            PityDistributionXAxes =
            [
                new Axis { Labels = insights.PityLabels.ToArray(), LabelsPaint = axisTextPaint, SeparatorsPaint = separatorPaint }
            ];
            PityDistributionYAxes = [new Axis { MinLimit = 0, LabelsPaint = axisTextPaint, SeparatorsPaint = separatorPaint }];
            RaisePropertyChanged(nameof(PityDistributionXAxes));
            RaisePropertyChanged(nameof(PityDistributionYAxes));

            FiveStarTimelineSeries =
            [
                new LineSeries<int>
                {
                    Values = insights.FiveStars.Select(x => x.Pity).ToArray(),
                    Name = LanguageManager.Instance["Stat_Pity"] ?? "Pity",
                    GeometrySize = 10,
                    Stroke = new SolidColorPaint(palette.Warning) { StrokeThickness = 3 },
                    GeometryFill = new SolidColorPaint(palette.Warning),
                    GeometryStroke = new SolidColorPaint(palette.Warning)
                }
            ];
            FiveStarTimelineXAxes =
            [
                new Axis
                {
                    Labels = insights.FiveStars.Select(x => $"{x.OccurredAt:MM-dd} {x.Name}").ToArray(),
                    LabelsRotation = insights.FiveStars.Count > 10 ? 35 : 0,
                    LabelsPaint = axisTextPaint,
                    SeparatorsPaint = separatorPaint
                }
            ];
            FiveStarTimelineYAxes = [new Axis { MinLimit = 0, LabelsPaint = axisTextPaint, SeparatorsPaint = separatorPaint }];
            RaisePropertyChanged(nameof(FiveStarTimelineXAxes));
            RaisePropertyChanged(nameof(FiveStarTimelineYAxes));

            RarityStackedSeries =
            [
                new StackedColumnSeries<int> { Values = insights.PoolRarities.Select(x => x.ThreeStar).ToArray(), Name = "3★", Fill = new SolidColorPaint(palette.TextMuted) },
                new StackedColumnSeries<int> { Values = insights.PoolRarities.Select(x => x.FourStar).ToArray(), Name = "4★", Fill = new SolidColorPaint(palette.FourStar) },
                new StackedColumnSeries<int> { Values = insights.PoolRarities.Select(x => x.FiveStar).ToArray(), Name = "5★", Fill = new SolidColorPaint(palette.Warning) }
            ];
            RarityStackedXAxes =
            [
                new Axis
                {
                    Labels = insights.PoolRarities.Select(x => x.PoolType.GetLocalizedDescription()).ToArray(),
                    LabelsRotation = 15,
                    LabelsPaint = axisTextPaint,
                    SeparatorsPaint = separatorPaint
                }
            ];
            RarityStackedYAxes = [new Axis { MinLimit = 0, LabelsPaint = axisTextPaint, SeparatorsPaint = separatorPaint }];
            RaisePropertyChanged(nameof(RarityStackedXAxes));
            RaisePropertyChanged(nameof(RarityStackedYAxes));

            DateTime heatStart = insights.DailyPulls.Count == 0
                ? DateTime.Today
                : insights.DailyPulls.Min(x => x.Date).Date;
            var heatPoints = insights.DailyPulls.Select(x =>
            {
                int week = (int)((x.Date.Date - heatStart).TotalDays / 7);
                int day = ((int)x.Date.DayOfWeek + 6) % 7;
                return new WeightedPoint(week, day, x.Pulls);
            }).ToArray();
            ActivityHeatSeries =
            [
                new HeatSeries<WeightedPoint>
                {
                    Values = heatPoints,
                    Name = LanguageManager.Instance["Stat_DailyPulls"] ?? "Daily pulls",
                    HeatMap =
                    [
                        ToLvcColor(palette.Primary.WithAlpha(20)),
                        ToLvcColor(palette.Primary.WithAlpha(80)),
                        ToLvcColor(palette.Primary.WithAlpha(160)),
                        ToLvcColor(palette.Primary)
                    ],
                    PointPadding = new LiveChartsCore.Drawing.Padding(2)
                }
            ];
            int heatWeeks = heatPoints.Length == 0 ? 0 : (int)heatPoints.Max(x => x.X ?? 0) + 1;
            ActivityHeatXAxes = [new Axis { Labels = Enumerable.Range(0, heatWeeks).Select(x => heatStart.AddDays(x * 7).ToString("MM-dd")).ToArray(), LabelsPaint = axisTextPaint, SeparatorsPaint = null }];
            ActivityHeatYAxes = [new Axis { Labels = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"], LabelsPaint = axisTextPaint, SeparatorsPaint = null }];
            RaisePropertyChanged(nameof(ActivityHeatXAxes));
            RaisePropertyChanged(nameof(ActivityHeatYAxes));

            CumulativeTrendSeries =
            [
                new LineSeries<int> { Values = insights.CumulativePulls.Select(x => x.Pulls).ToArray(), Name = LanguageManager.Instance["Stat_CumulativePulls"] ?? "Cumulative pulls", Fill = null, GeometrySize = 5, Stroke = new SolidColorPaint(palette.Primary) { StrokeThickness = 3 }, GeometryFill = new SolidColorPaint(palette.Primary), GeometryStroke = new SolidColorPaint(palette.Primary) },
                new LineSeries<int> { Values = insights.CumulativePulls.Select(x => x.FiveStars).ToArray(), Name = LanguageManager.Instance["Stat_CumulativeGolds"] ?? "Cumulative 5-star", ScalesYAt = 1, Fill = null, GeometrySize = 5, Stroke = new SolidColorPaint(palette.Warning) { StrokeThickness = 3 }, GeometryFill = new SolidColorPaint(palette.Warning), GeometryStroke = new SolidColorPaint(palette.Warning) }
            ];
            CumulativeTrendXAxes = [new Axis { Labels = insights.CumulativePulls.Select(x => x.Date.ToString("MM-dd")).ToArray(), LabelsPaint = axisTextPaint, SeparatorsPaint = separatorPaint }];
            CumulativeTrendYAxes =
            [
                new Axis { Position = LiveChartsCore.Measure.AxisPosition.Start, MinLimit = 0, LabelsPaint = axisTextPaint, SeparatorsPaint = separatorPaint },
                new Axis { Position = LiveChartsCore.Measure.AxisPosition.End, MinLimit = 0, LabelsPaint = axisTextPaint, ShowSeparatorLines = false }
            ];
            RaisePropertyChanged(nameof(CumulativeTrendXAxes));
            RaisePropertyChanged(nameof(CumulativeTrendYAxes));

            CurrentCharacterPity = insights.CurrentCharacterPity;
            CurrentPityGaugeSeries =
            [
                new PieSeries<double> { Values = [Math.Min(80, insights.CurrentCharacterPity)], Name = LanguageManager.Instance["Stat_CurrentPity"] ?? "Current pity", InnerRadius = 62, Fill = new SolidColorPaint(palette.Primary) },
                new PieSeries<double> { Values = [Math.Max(0, 80 - insights.CurrentCharacterPity)], Name = LanguageManager.Instance["Stat_RemainingPity"] ?? "Remaining", InnerRadius = 62, Fill = new SolidColorPaint(palette.Stroke.WithAlpha(90)) }
            ];

            FeaturedExpectationSeries =
            [
                new LineSeries<int> { Values = insights.FeaturedPulls.Select(x => x.CumulativePulls).ToArray(), Name = LanguageManager.Instance["Stat_ActualCumulative"] ?? "Actual cumulative", Fill = null, GeometrySize = 8, Stroke = new SolidColorPaint(palette.Primary) { StrokeThickness = 3 }, GeometryFill = new SolidColorPaint(palette.Primary), GeometryStroke = new SolidColorPaint(palette.Primary) },
                new LineSeries<double> { Values = insights.FeaturedPulls.Select(x => x.ExpectedCumulativePulls).ToArray(), Name = LanguageManager.Instance["Stat_ExpectedCumulative"] ?? "Expected cumulative", Fill = null, GeometrySize = 0, Stroke = new SolidColorPaint(palette.Success) { StrokeThickness = 2 } },
                new LineSeries<double> { Values = insights.FeaturedPulls.Select(x => x.RunningAverage).ToArray(), Name = LanguageManager.Instance["Stat_RunningFeaturedAverage"] ?? "Average per UP", ScalesYAt = 1, Fill = null, GeometrySize = 8, Stroke = new SolidColorPaint(palette.Warning) { StrokeThickness = 3 }, GeometryFill = new SolidColorPaint(palette.Warning), GeometryStroke = new SolidColorPaint(palette.Warning) }
            ];
            FeaturedExpectationXAxes = [new Axis { Labels = insights.FeaturedPulls.Select(x => $"{x.Index}. {x.Name}").ToArray(), LabelsRotation = 20, LabelsPaint = axisTextPaint, SeparatorsPaint = separatorPaint }];
            FeaturedExpectationYAxes =
            [
                new Axis { Position = LiveChartsCore.Measure.AxisPosition.Start, MinLimit = 0, LabelsPaint = axisTextPaint, SeparatorsPaint = separatorPaint },
                new Axis { Position = LiveChartsCore.Measure.AxisPosition.End, MinLimit = 0, LabelsPaint = axisTextPaint, ShowSeparatorLines = false }
            ];
            RaisePropertyChanged(nameof(FeaturedExpectationXAxes));
            RaisePropertyChanged(nameof(FeaturedExpectationYAxes));
        }

        private static ChartThemePalette GetChartThemePalette()
        {
            SKColor primary = GetChartColor("ChartPrimaryColor");
            return new ChartThemePalette(
                GetChartColor("TextPrimaryColor"),
                GetChartColor("TextSecondaryColor"),
                GetChartColor("TextMutedColor"),
                GetChartColor("ChartGridColor"),
                primary,
                GetChartColor("ChartSecondaryColor"),
                GetChartColor("ChartTertiaryColor"),
                GetChartColor("SurfaceElevatedColor"),
                TryGetChartColor("ChartFourStarColor", out SKColor fourStar)
                    ? fourStar
                    : DeriveFourStarColor(primary));
        }

        private static SKColor GetChartColor(string resourceKey)
        {
            return TryGetChartColor(resourceKey, out SKColor color)
                ? color
                : throw new InvalidOperationException($"Missing chart theme color resource: {resourceKey}");
        }

        private static bool TryGetChartColor(string resourceKey, out SKColor color)
        {
            if (Application.Current.Resources[resourceKey] is System.Windows.Media.Color resourceColor)
            {
                color = new SKColor(resourceColor.R, resourceColor.G, resourceColor.B, resourceColor.A);
                return true;
            }

            color = default;
            return false;
        }

        private static SKColor DeriveFourStarColor(SKColor primary)
        {
            byte red = (byte)Math.Clamp((primary.Red + primary.Blue) / 2 + 32, 0, 255);
            byte green = (byte)Math.Clamp(primary.Green * 0.65, 0, 255);
            byte blue = (byte)Math.Clamp(Math.Max(primary.Blue, primary.Red * 0.9), 0, 255);
            return new SKColor(red, green, blue, primary.Alpha);
        }

        private static LiveChartsCore.Drawing.LvcColor ToLvcColor(SKColor color)
            => new(color.Red, color.Green, color.Blue, color.Alpha);

        private readonly record struct ChartThemePalette(
            SKColor TextPrimary,
            SKColor TextSecondary,
            SKColor TextMuted,
            SKColor Stroke,
            SKColor Primary,
            SKColor Warning,
            SKColor Success,
            SKColor SurfaceElevated,
            SKColor FourStar);

        private string GetLocalizedItemName(int resourceId, string defaultName, LanguageType? lang = null)
        {
            if (resourceId == 0) return defaultName;
            var itemInfo = _gameData.GetItemById(resourceId);
            if (itemInfo != null)
            {
                string code = lang?.GetCode() ?? LanguageManager.Instance.CurrentLanguage.GetCode();
                return itemInfo.GetName(code) ?? defaultName;
            }
            return defaultName;
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

            if (_isLoadingLocalGachaLog)
            {
                return;
            }

            _isLoadingLocalGachaLog = true;
            IsStatisticsLoading = true;
            StatisticsErrorMessage = null;

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
                            var localData = await _userDataService.ReadGachaInSourceOrderAsync(SelectedUser.Uid, (int)type, _navigationCts.Token);

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
                                            _featuredCharacterCount = res.FeaturedCount;
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
                    HasStatisticsData = allGachaDatas.Count > 0;

                    _uiStateService.ShowToast(LanguageManager.Instance["Toast_Success"], LanguageManager.Instance["Msg_LoadedLocalGacha"], NotificationType.Success);
                    UserId = SelectedUser.Uid;
                }, "加载本地数据", ex =>
                {
                    HasStatisticsData = false;
                    StatisticsErrorMessage = ex.Message;
                });
            }
            finally
            {
                _uiStateService.HideLoading();
                _isLoadingLocalGachaLog = false;
                IsStatisticsLoading = false;
            }
        }

        private void ApplyTrendViewport()
        {
            if (DailyXAxes.Length == 0) return;

            DailyXAxes[0].MinLimit = IsTrendViewportEnabled
                ? TrendViewportStart - 0.5
                : null;
            DailyXAxes[0].MaxLimit = IsTrendViewportEnabled
                ? TrendViewportStart + TrendViewportSize - 0.5
                : null;
            RaisePropertyChanged(nameof(DailyXAxes));
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
                    var users = await _userDataService.ListAccountsAsync(_navigationCts.Token);

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
                                SelectedUser = Users.FirstOrDefault(u => u.Uid == _configService.User.LastUserId) ?? Users.First();
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

        private void ResetNavigationCancellation()
        {
            if (!_navigationCts.IsCancellationRequested) return;
            _navigationCts.Dispose();
            _navigationCts = new CancellationTokenSource();
        }
    }
}
