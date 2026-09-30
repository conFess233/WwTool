using WwTool.UI.Models;
using WwTool.Services.Presentation;
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
using ExceptionHelper = WwTool.Services.Presentation.ExceptionHelper;

namespace WwTool.UI.ViewModels
{
    /// <summary>
    /// 抽卡数据统计视图模型，处理抽卡记录的获取和统计计算
    /// </summary>
    public class StatisticsViewModel : BindableBase, INavigationAware
    {
        /// <summary>统一 Skia 图表的中日韩字体；进程内只配置一次。</summary>
        static StatisticsViewModel()
        {
            LiveCharts.Configure(settings => settings.HasTextSettings(new TextSettings
            {
                DefaultTypeface = SKTypeface.FromFamilyName("Microsoft YaHei UI")
            }));
        }
        private CancellationTokenSource _navigationCts = new();
        private bool _isActive;
        private readonly IGetDataService _getDataService;
        private readonly IDialogService _dialogService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IUIStateService _uiStateService;
        private readonly IConfigService _configService;
        private readonly GameDataService _gameData;
        private readonly IUserDataService _userDataService;
        private readonly IGachaStatisticsService _gachaStatisticsService;
        private readonly ILoggerService _logger;
        private readonly IGachaLogLocator _gachaLogLocator;

        /// <summary>只使被删除账号的缓存失效，其他账号不受影响。</summary>
        private void OnAccountDeleted(object? sender, AccountDeletedEventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var user in Users.Where(x => x.Uid == e.Uid).ToArray()) Users.Remove(user);
                if (SelectedUser?.Uid != e.Uid) return;
                _navigationCts.Cancel();
                _navigationCts.Dispose();
                _navigationCts = new CancellationTokenSource();
                _accountLoadCts?.Cancel();
                _accountLoadCts = null;
                ++_accountLoadVersion;
                _isLoadingLocalGachaLog = false;
                IsStatisticsLoading = false;
                _isInitialized = false;
                _allCachedGachaDatas.Clear();
                foreach (var pool in PoolStatistics)
                {
                    pool.HitGoldDatas.Clear();
                    pool.Calculate.Clear();
                }
                HasStatisticsData = false;
                FilteredHitGoldFlow.Clear();
                InvalidateCharts();
                SelectedUser = null!;

            });
        }
        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _isActive = false;
            _navigationCts.Cancel();
            ReleaseChartResources();
        }

        public StatisticsViewModel(IEventAggregator eventAggregator, IUIStateService uIStateService, IGetDataService getDataService, IDialogService dialogService, IConfigService configService, GameDataService gameData, IUserDataService userDataService, IGachaStatisticsService gachaStatisticsService, ILoggerService logger, IGachaLogLocator gachaLogLocator)
        {
            _uiStateService = uIStateService;
            _eventAggregator = eventAggregator;
            _getDataService = getDataService;
            _dialogService = dialogService;
            _configService = configService;
            _gameData = gameData;
            _gameData.Changed += OnCatalogChanged;
            _userDataService = userDataService;
            System.Windows.WeakEventManager<IUserDataService, AccountDeletedEventArgs>.AddHandler(userDataService, nameof(IUserDataService.AccountDeleted), OnAccountDeleted);
            _gachaStatisticsService = gachaStatisticsService;
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
            ImportUrlCommand = new DelegateCommand(() => RefreshQueryData(showMessage: true));

            foreach (var type in Enum.GetValues<CardPoolType>())
            {
                var filter = new PoolTypeFilterItem { PoolType = type, IsSelected = true };
                filter.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(PoolTypeFilterItem.IsSelected)) InvalidateCharts(); };
                PoolFilters.Add(filter);
            }

            _configService.User.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_configService.User.IsReducedMotionEnabled))
                {
                    RaisePropertyChanged(nameof(ChartAnimationsSpeed));
                }
                if (e.PropertyName == nameof(_configService.User.BaseTheme) ||
                    e.PropertyName == nameof(_configService.User.AccentTheme) ||
                    e.PropertyName == nameof(_configService.User.AppLanguage))
                {
                    Task.Delay(50).ContinueWith(_ =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            InvalidateCharts();
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
            _isActive = true;
            ResetNavigationCancellation();
            if (!_isInitialized)
            {
                SelectedStatisticsTabIndex = 0;
                SelectedPoolStatisticsIndex = 0;
            }
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
                    _accountLoadCts?.Cancel();
                    ++_accountLoadVersion;
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
        private List<GachaPull> _allCachedGachaDatas = new();

        private int _selectedDateRangeIndex = 0; // 0=全部, 1=最近1个月, 2=最近3个月
        public int SelectedDateRangeIndex
        {
            get => _selectedDateRangeIndex;
            set { if (SetProperty(ref _selectedDateRangeIndex, value)) InvalidateCharts(); }
        }

        public ObservableCollection<PoolTypeFilterItem> PoolFilters { get; set; } = new();

        private string _selectedGoldName = "";
        public string SelectedGoldName
        {
            get => _selectedGoldName;
            set { if (SetProperty(ref _selectedGoldName, value)) InvalidateCharts(); }
        }

        public ObservableCollection<string> AllGotGoldNames { get; set; } = new();
        #endregion

        #region 图表数据绑定
        /// <summary>图表沿用统一动效时长，并响应减少动画设置。</summary>
        public TimeSpan ChartAnimationsSpeed => _configService.User.IsReducedMotionEnabled
            ? TimeSpan.FromMilliseconds(80) : TimeSpan.FromMilliseconds(220);

        private ISeries[] _globalPoolCompareSeries = [];
        public ISeries[] GlobalPoolCompareSeries { get => _globalPoolCompareSeries; set { _globalPoolCompareSeries = value; RaisePropertyChanged(); } }

        private double _poolComparisonHeight = 240;
        /// <summary>每个卡池保留一行，避免自动省略类别标签。</summary>
        public double PoolComparisonHeight { get => _poolComparisonHeight; private set => SetProperty(ref _poolComparisonHeight, value); }

        private ISeries[] _poolAverageSeries = [];
        public ISeries[] PoolAverageSeries { get => _poolAverageSeries; set { _poolAverageSeries = value; RaisePropertyChanged(); } }
        private Axis[] _poolAverageXAxes = [];
        public Axis[] PoolAverageXAxes { get => _poolAverageXAxes; set { _poolAverageXAxes = value; RaisePropertyChanged(); } }
        private Axis[] _poolAverageYAxes = [];
        public Axis[] PoolAverageYAxes { get => _poolAverageYAxes; set { _poolAverageYAxes = value; RaisePropertyChanged(); } }

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
            set
            {
                if (SetProperty(ref _selectedStatisticsTabIndex, value) && value == 1)
                {
                    _ = EnsureChartsAsync();
                }
            }
        }

        private int _selectedPoolStatisticsIndex;
        public int SelectedPoolStatisticsIndex
        {
            get => _selectedPoolStatisticsIndex;
            set
            {
                // 选择器在快照替换或卸载时短暂产生 -1，不清除用户当前卡池。
                if (value < 0) return;
                SetProperty(ref _selectedPoolStatisticsIndex, value);
            }
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

        private double? _filteredAveragePity;
        public double? FilteredAveragePity
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
        private double? _avgLimitCharaTide;  // 角色限定池每限定金平均抽数
        private double? _avgCharaTide;       // 角色限定池每金平均抽数

        public double? AvgLimitCharaTide
        {
            get => _avgLimitCharaTide;
            set
            {
                _avgLimitCharaTide = value;
                RaisePropertyChanged();
            }
        }
        public double? AvgCharaTide
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
        /// <param name="showMessage">是否显示用户主动导入的结果提示</param>
        void RefreshQueryData(bool showMessage = false)
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
                if (showMessage)
                {
                    ToastHelper.ShowActionResult(
                        _uiStateService,
                        LanguageManager.Instance["Toast_Success"],
                        LanguageManager.Instance["Msg_AutoImportSuccess"],
                        NotificationType.Success,
                        nameof(StatisticsViewModel),
                        "gacha:manual-import-url");
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
                _uiStateService.ShowToast(new NotificationRequest
                {
                    Title = LanguageManager.Instance["Msg_AutoImportSuccessTitle"],
                    Message = LanguageManager.Instance["Msg_AutoImportSuccess"],
                    Type = NotificationType.Success,
                    Priority = NotificationPriority.Important,
                    Source = nameof(StatisticsViewModel),
                    DedupeKey = "gacha:auto-import"
                });
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

                    _uiStateService.ShowToast(new NotificationRequest
                    {
                        Title = LanguageManager.Instance["Toast_Success"],
                        Message = LanguageManager.Instance["Msg_ClearedData"],
                        Type = NotificationType.Success,
                        Priority = NotificationPriority.Important,
                        Source = nameof(StatisticsViewModel),
                        DedupeKey = "gacha:data-cleared"
                    });
                }
            });

        }

        /// <summary>
        /// 获取指定卡池类型的抽卡记录
        /// </summary>
        /// <param name="poolType">卡池类型枚举值</param>
        /// <returns>抽卡记录集合</returns>
        private async Task<IEnumerable<GachaPull>> GetGachaLog(int poolType, GachaServerRegion serverRegion)
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
            string? uid = SelectedUser?.Uid;
            if (string.IsNullOrEmpty(uid)) return;
            CancellationToken token = _navigationCts.Token;
            GachaServerRegion serverRegion = _selectedGachaServerRegion;
            SetGachaImportInProgress(true);
            _logger.Info("在 StatisticsViewModel 中调用了 StatisticsDatas 命令");
            try
            {
                _uiStateService.ShowLoading(LanguageManager.Instance["Msg_SyncingGacha"]);
                await Task.Delay(50);
                RefreshQueryData();
                int previousRecordCount = _allCachedGachaDatas.Count;

                await ExceptionHelper.ExecuteAsync(async () =>
                {
                    foreach (var type in Enum.GetValues<CardPoolType>())
                    {
                        _uiStateService.ShowLoading(string.Format(LanguageManager.Instance["Msg_SyncingPool"], type.GetLocalizedDescription()));
                        if (SelectedUser?.Uid != uid) throw new OperationCanceledException(token);
                        var gachaData = await GetGachaLog((int)type, serverRegion);
                        await _userDataService.ImportGachaAsync(uid, (int)type, gachaData, "remote", token);
                    }

                    _uiStateService.ShowLoading(LanguageManager.Instance["Msg_SyncFinishedProcessing"]);
                    GachaLoadSnapshot snapshot = await ReadAndCalculateAllPoolsAsync(uid, token);
                    if (token.IsCancellationRequested || SelectedUser?.Uid != uid) return;
                    ApplyPoolSnapshot(snapshot);

                    _uiStateService.ShowLoading(LanguageManager.Instance["Msg_CalculatingData"]);
                    await Statistics(snapshot.AllRecords);

                    int addedRecordCount = Math.Max(0, snapshot.AllRecords.Count - previousRecordCount);
                    NotificationType resultType = addedRecordCount == 0 ? NotificationType.Info : NotificationType.Success;
                    ToastHelper.ShowActionResult(
                        _uiStateService,
                        LanguageManager.Instance[resultType == NotificationType.Info ? "Toast_Info" : "Toast_Success"],
                        addedRecordCount == 0
                            ? LanguageManager.Instance["Msg_ActionNoNewData"]
                            : string.Format(LanguageManager.Instance["Msg_GachaSyncAdded"], addedRecordCount),
                        resultType,
                        nameof(StatisticsViewModel),
                        "gacha:cloud-sync");

                    if (SelectedUser?.Uid == uid) UserId = uid;
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
        private async Task Statistics(List<GachaPull>? allGachaDatas = null)
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

            _chartsDirty = true;
            _chartInvalidationVersion++;
            if (SelectedStatisticsTabIndex == 1)
            {
                await EnsureChartsAsync(showLoading: false);
            }
        }

        private bool _isUpdatingCharts = false;
        private bool _isEnsuringCharts;
        private bool _chartsDirty = true;
        private bool _chartsLoaded;
        private int _chartInvalidationVersion;
        private bool _isLoadingLocalGachaLog;
        private CancellationTokenSource? _accountLoadCts;
        private int _accountLoadVersion;
        private bool _isStatisticsLoading;
        public bool IsStatisticsLoading { get => _isStatisticsLoading; set => SetProperty(ref _isStatisticsLoading, value); }
        private bool _hasStatisticsData;
        public bool HasStatisticsData { get => _hasStatisticsData; set => SetProperty(ref _hasStatisticsData, value); }
        private string? _statisticsErrorMessage;
        public string? StatisticsErrorMessage { get => _statisticsErrorMessage; set => SetProperty(ref _statisticsErrorMessage, value); }

        private void InvalidateCharts()
        {
            // 图表构建会重建筛选项集合，绑定层可能短暂回写空选项。
            // 这类内部回写不应再次触发图表构建，否则会形成无限刷新循环。
            if (_isUpdatingCharts)
            {
                return;
            }

            _chartsDirty = true;
            _chartInvalidationVersion++;
            if (_isActive && SelectedStatisticsTabIndex == 1 && !_isEnsuringCharts)
            {
                _ = EnsureChartsAsync();
            }
        }

        private async Task EnsureChartsAsync(bool showLoading = true)
        {
            if (!_isActive || SelectedStatisticsTabIndex != 1 || (!_chartsDirty && _chartsLoaded) || _isEnsuringCharts)
            {
                return;
            }

            _isEnsuringCharts = true;
            if (showLoading)
            {
                _uiStateService.ShowLoading(LanguageManager.Instance["Msg_CalculatingData"]);
                await Application.Current.Dispatcher.InvokeAsync(
                    () => { },
                    System.Windows.Threading.DispatcherPriority.Background);
            }

            try
            {
                do
                {
                    int version = _chartInvalidationVersion;
                    try { await UpdateChartsAsync(); }
                    catch (OperationCanceledException) { return; }
                    _chartsLoaded = true;
                    _chartsDirty = version != _chartInvalidationVersion;
                }
                while (_chartsDirty && _isActive && SelectedStatisticsTabIndex == 1);
            }
            finally
            {
                _isEnsuringCharts = false;
                if (showLoading)
                {
                    _uiStateService.HideLoading();
                }
            }
        }

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
                    FilteredAveragePity = null;
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
                    PoolAverageSeries = [];
                    PoolAverageXAxes = [];
                    PoolAverageYAxes = [];
                    GlobalPoolXAxes = [];
                    GlobalPoolYAxes = [];
                    ClearInsightCharts();
                    return;
                }

                int version = _chartInvalidationVersion;
                string? uid = SelectedUser?.Uid;
                var records = _allCachedGachaDatas.ToList();
                var pools = PoolFilters.Where(x => x.IsSelected).Select(x => x.PoolType).ToHashSet();
                int range = SelectedDateRangeIndex;
                string language = _configService.User.AppLanguage.GetCode();
                CancellationToken token = _navigationCts.Token;
                _isUpdatingCharts = false; // 后台准备期间仍接收用户筛选失效通知。
                var prepared = await Task.Run(() => StatisticsChartDataService.Build(records, pools, range,
                    language, _gachaStatisticsService, _gameData, token), token);
                _isUpdatingCharts = true;
                if (token.IsCancellationRequested || version != _chartInvalidationVersion || SelectedUser?.Uid != uid) return;
                var filteredDatas = prepared.Records;
                var insights = prepared.Insights;
                var dailyBuckets = prepared.Days;
                var dailyLabels = prepared.Labels;
                var dailyPulls = prepared.Pulls;
                var dailyGolds = prepared.Golds;
                var peakDay = prepared.Peak;
                IsTrendViewportEnabled = dailyBuckets.Count > TrendViewportSize;
                TrendViewportMaximum = Math.Max(0, dailyBuckets.Count - TrendViewportSize);
                TrendViewportStart = TrendViewportMaximum;

                FilteredPullCount = filteredDatas.Count;
                FilteredGoldCount = filteredDatas.Count(x => x.QualityLevel == 5);
                FilteredAveragePity = FilteredGoldCount == 0
                    ? null
                    : insights.FiveStars.Average(x => x.Pity);
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

                int fourStarCharacterCount = prepared.FourCharacters;
                int fourStarWeaponCount = prepared.FourWeapons;
                int success = prepared.Success;
                int otherFiveStars = prepared.OtherGolds;
                var compareXLabels = prepared.PoolLabels.Select(x => Enum.Parse<CardPoolType>(x).GetLocalizedDescription()).ToList();
                var tidesData = prepared.Tides;
                var avgTideData = prepared.Averages;
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
                            Fill = null, LineSmoothness = 0, GeometrySize = 6,
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

                    PoolComparisonHeight = Math.Max(240, compareXLabels.Count * 36 + 64);
                    // 分类比较使用独立水平条形图，避免双轴将抽数与平均值混为同一尺度。
                    GlobalPoolCompareSeries =
                    [
                        new RowSeries<int> { Values = tidesData, Name = LanguageManager.Instance["Stat_TotalTides"], Fill = new SolidColorPaint(palette.Primary), MaxBarWidth = 28 }
                    ];
                    PoolAverageSeries =
                    [
                        new RowSeries<double?> { Values = avgTideData,
                            Name = LanguageManager.Instance["Stat_AvgGold"], Fill = new SolidColorPaint(palette.Warning), MaxBarWidth = 28 }
                    ];
                    GlobalPoolXAxes = [new Axis { MinLimit = 0, LabelsPaint = axisTextPaint, SeparatorsPaint = separatorPaint }];
                    GlobalPoolYAxes = [new Axis { Labels = compareXLabels, MinStep = 1, ForceStepToMin = true, TextSize = 14, LabelsPaint = axisTextPaint, ShowSeparatorLines = false }];
                    PoolAverageXAxes = [new Axis { MinLimit = 0, LabelsPaint = axisTextPaint, SeparatorsPaint = separatorPaint }];
                    PoolAverageYAxes = [new Axis { Labels = compareXLabels, MinStep = 1, ForceStepToMin = true, TextSize = 14, LabelsPaint = axisTextPaint, ShowSeparatorLines = false }];
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

        /// <summary>
        /// 离开统计页时断开可重建的 LiveCharts/Skia 对象引用，保留原始记录与轻量汇总。
        /// </summary>
        private void ReleaseChartResources()
        {
            PoolCharts.Clear();
            FilteredHitGoldFlow = new();
            DailyPullLineSeries = [];
            DailyXAxes = [];
            DailyYAxes = [];
            SuccessRatePieSeries = [];
            FourStarPieSeries = [];
            GlobalPoolCompareSeries = [];
            PoolAverageSeries = [];
            PoolAverageXAxes = [];
            PoolAverageYAxes = [];
            GlobalPoolXAxes = [];
            GlobalPoolYAxes = [];
            ClearInsightCharts();

            PityDistributionXAxes = [];
            PityDistributionYAxes = [];
            FiveStarTimelineXAxes = [];
            FiveStarTimelineYAxes = [];
            RarityStackedXAxes = [];
            RarityStackedYAxes = [];
            ActivityHeatXAxes = [];
            ActivityHeatYAxes = [];
            CumulativeTrendXAxes = [];
            CumulativeTrendYAxes = [];
            FeaturedExpectationXAxes = [];
            FeaturedExpectationYAxes = [];

            RaisePropertyChanged(nameof(PityDistributionXAxes));
            RaisePropertyChanged(nameof(PityDistributionYAxes));
            RaisePropertyChanged(nameof(FiveStarTimelineXAxes));
            RaisePropertyChanged(nameof(FiveStarTimelineYAxes));
            RaisePropertyChanged(nameof(RarityStackedXAxes));
            RaisePropertyChanged(nameof(RarityStackedYAxes));
            RaisePropertyChanged(nameof(ActivityHeatXAxes));
            RaisePropertyChanged(nameof(ActivityHeatYAxes));
            RaisePropertyChanged(nameof(CumulativeTrendXAxes));
            RaisePropertyChanged(nameof(CumulativeTrendYAxes));
            RaisePropertyChanged(nameof(FeaturedExpectationXAxes));
            RaisePropertyChanged(nameof(FeaturedExpectationYAxes));

            _chartsLoaded = false;
            _chartsDirty = true;
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
                    YToolTipLabelFormatter = point =>
                        $"{insights.FiveStars[point.Index].OccurredAt:g} · {insights.FiveStars[point.Index].Name}: {point.Model} {LanguageManager.Instance["Unit_Pull"]}",
                    Fill = null, LineSmoothness = 0, GeometrySize = 8,
                    Stroke = new SolidColorPaint(palette.Warning) { StrokeThickness = 3 },
                    GeometryFill = new SolidColorPaint(palette.Warning),
                    GeometryStroke = new SolidColorPaint(palette.Warning)
                }
            ];
            FiveStarTimelineXAxes =
            [
                new Axis
                {
                    Labels = insights.FiveStars.Select(x => x.OccurredAt.ToString("MM-dd")).ToArray(),
                    LabelsRotation = 0,
                    LabelsPaint = axisTextPaint,
                    SeparatorsPaint = separatorPaint
                }
            ];
            FiveStarTimelineYAxes = [new Axis { MinLimit = 0, LabelsPaint = axisTextPaint, SeparatorsPaint = separatorPaint }];
            RaisePropertyChanged(nameof(FiveStarTimelineXAxes));
            RaisePropertyChanged(nameof(FiveStarTimelineYAxes));

            RarityStackedSeries =
            [
                new StackedRowSeries<int> { Values = insights.PoolRarities.Select(x => x.ThreeStar).ToArray(), Name = "3★", Fill = new SolidColorPaint(palette.TextMuted) },
                new StackedRowSeries<int> { Values = insights.PoolRarities.Select(x => x.FourStar).ToArray(), Name = "4★", Fill = new SolidColorPaint(palette.FourStar) },
                new StackedRowSeries<int> { Values = insights.PoolRarities.Select(x => x.FiveStar).ToArray(), Name = "5★", Fill = new SolidColorPaint(palette.Warning) }
            ];
            RarityStackedXAxes = [new Axis { MinLimit = 0, LabelsPaint = axisTextPaint, SeparatorsPaint = separatorPaint }];
            RarityStackedYAxes = [new Axis { Labels = insights.PoolRarities.Select(x => x.PoolType.GetLocalizedDescription()).ToArray(), MinStep = 1, ForceStepToMin = true, TextSize = 14, LabelsPaint = axisTextPaint, ShowSeparatorLines = false }];
            RaisePropertyChanged(nameof(RarityStackedXAxes));
            RaisePropertyChanged(nameof(RarityStackedYAxes));

            DateTime heatStart = insights.DailyPulls.Count == 0
                ? DateTime.Today
                : insights.DailyPulls.Min(x => x.Date).Date;
            // 列以周一为起点，避免第一周的日期和星期位置错位。
            heatStart = heatStart.AddDays(-(((int)heatStart.DayOfWeek + 6) % 7));
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
            ActivityHeatYAxes = [new Axis { Labels = Enumerable.Range(0, 7).Select(day => LanguageManager.Instance.CurrentLanguage.GetDisplayCulture().DateTimeFormat.AbbreviatedDayNames[(day + 1) % 7]).ToArray(), MinStep = 1, ForceStepToMin = true, IsInverted = true, LabelsPaint = axisTextPaint, SeparatorsPaint = null }];
            RaisePropertyChanged(nameof(ActivityHeatXAxes));
            RaisePropertyChanged(nameof(ActivityHeatYAxes));

            CumulativeTrendSeries =
            [
                new LineSeries<int> { LineSmoothness = 0, Values = insights.CumulativePulls.Select(x => x.Pulls).ToArray(), Name = LanguageManager.Instance["Stat_CumulativePulls"] ?? "Cumulative pulls", Fill = null, GeometrySize = 5, Stroke = new SolidColorPaint(palette.Primary) { StrokeThickness = 3 }, GeometryFill = new SolidColorPaint(palette.Primary), GeometryStroke = new SolidColorPaint(palette.Primary) },
                new LineSeries<int> { LineSmoothness = 0, Values = insights.CumulativePulls.Select(x => x.FiveStars).ToArray(), Name = LanguageManager.Instance["Stat_CumulativeGolds"] ?? "Cumulative 5-star", ScalesYAt = 1, Fill = null, GeometrySize = 5, Stroke = new SolidColorPaint(palette.Warning) { StrokeThickness = 3 }, GeometryFill = new SolidColorPaint(palette.Warning), GeometryStroke = new SolidColorPaint(palette.Warning) }
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
                new LineSeries<int> { LineSmoothness = 0, Values = insights.FeaturedPulls.Select(x => x.CumulativePulls).ToArray(), Name = LanguageManager.Instance["Stat_ActualCumulative"] ?? "Actual cumulative", Fill = null, GeometrySize = 8, Stroke = new SolidColorPaint(palette.Primary) { StrokeThickness = 3 }, GeometryFill = new SolidColorPaint(palette.Primary), GeometryStroke = new SolidColorPaint(palette.Primary) },
                new LineSeries<double> { LineSmoothness = 0, Values = insights.FeaturedPulls.Select(x => x.ExpectedCumulativePulls).ToArray(), Name = LanguageManager.Instance["Stat_ExpectedCumulative"] ?? "Expected cumulative", Fill = null, GeometrySize = 0, Stroke = new SolidColorPaint(palette.Success) { StrokeThickness = 2 } },
                new LineSeries<double> { LineSmoothness = 0, Values = insights.FeaturedPulls.Select(x => x.RunningAverage).ToArray(), Name = LanguageManager.Instance["Stat_RunningFeaturedAverage"] ?? "Average per UP", ScalesYAt = 1, Fill = null, GeometrySize = 8, Stroke = new SolidColorPaint(palette.Warning) { StrokeThickness = 3 }, GeometryFill = new SolidColorPaint(palette.Warning), GeometryStroke = new SolidColorPaint(palette.Warning) }
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
                string name = itemInfo.GetName(code);
                return string.IsNullOrWhiteSpace(name) ? defaultName : name;
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

        /// <summary>新目录到达后重算缓存中的抽卡记录，保持账号数据不变。</summary>
        private void OnCatalogChanged(object? sender, EventArgs e)
        {
            Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                if (!_isActive || SelectedUser is null || _isLoadingLocalGachaLog) return;
                try { await LoadLocalGachaLog(); }
                catch (OperationCanceledException) { }
                catch (Exception ex) { _logger.Warn("目录更新后重算抽卡统计失败。", ex); }
            });
        }

        private sealed record GachaLoadSnapshot(
            List<GachaPull> AllRecords,
            List<GachaStatisticsResult> PoolResults);

        /// <summary>
        /// 在后台按服务端原始顺序读取并计算所有卡池，完成后再由 UI 线程一次性提交。
        /// </summary>
        private Task<GachaLoadSnapshot> ReadAndCalculateAllPoolsAsync(string uid, CancellationToken? cancellationToken = null)
        {
            string languageCode = LanguageTypeExtensions.GetCode(_configService.User.AppLanguage);
            CancellationToken token = cancellationToken ?? _navigationCts.Token;

            return Task.Run(async () =>
            {
                var allRecords = new List<GachaPull>();
                var poolResults = new List<GachaStatisticsResult>();

                foreach (CardPoolType type in Enum.GetValues<CardPoolType>())
                {
                    token.ThrowIfCancellationRequested();
                    var records = (await _userDataService.ReadGachaInSourceOrderAsync(
                        uid,
                        (int)type,
                        token))?.ToList() ?? [];

                    allRecords.AddRange(records);
                    poolResults.Add(_gachaStatisticsService.OrganizeData(records, type, languageCode));
                }

                return new GachaLoadSnapshot(allRecords, poolResults);
            }, token);
        }

        private void ApplyPoolSnapshot(GachaLoadSnapshot snapshot)
        {
            foreach (GachaStatisticsResult result in snapshot.PoolResults)
            {
                CardPoolStatistics? pool = PoolStatistics.FirstOrDefault(
                    x => x.PoolType == result.PoolStatistics.PoolType);
                if (pool == null)
                {
                    continue;
                }

                pool.HitGoldDatas.Clear();
                foreach (HitGoldData hit in result.PoolStatistics.HitGoldDatas)
                {
                    pool.HitGoldDatas.Add(hit);
                }

                pool.Calculate = result.PoolStatistics.Calculate;

                if (pool.PoolType == CardPoolType.CharacterEvent)
                {
                    SuccessCount = result.SuccessCount;
                    MissCount = result.MissCount;
                    _featuredCharacterCount = result.FeaturedCount;
                }
            }
        }

        /// <summary>
        /// 从本地数据库加载当前账号的抽卡记录并重新统计
        /// </summary>
        /// <param name="showMessage">是否显示用户主动加载的结果提示</param>
        public async Task LoadLocalGachaLog(bool showMessage = false)
        {
            if (string.IsNullOrEmpty(SelectedUser?.Uid))
            {
                _uiStateService.ShowToast(LanguageManager.Instance["Toast_Error"], LanguageManager.Instance["Msg_UidEmptyForGacha"], NotificationType.Warning);
                return;
            }

            string uid = SelectedUser.Uid;
            int version = ++_accountLoadVersion;
            _accountLoadCts?.Cancel();
            using var loadCts = CancellationTokenSource.CreateLinkedTokenSource(_navigationCts.Token);
            _accountLoadCts = loadCts;

            _isLoadingLocalGachaLog = true;
            IsStatisticsLoading = true;
            StatisticsErrorMessage = null;

            _logger.Info("在 StatisticsViewModel 中调用了 LoadLocalGachaLog 命令");

            try
            {
                _uiStateService.ShowLoading(LanguageManager.Instance["Msg_LoadingLocalGacha"]);

                await ExceptionHelper.ExecuteAsync(async () =>
                {
                    GachaLoadSnapshot snapshot = await ReadAndCalculateAllPoolsAsync(uid, loadCts.Token);
                    if (loadCts.IsCancellationRequested || version != _accountLoadVersion || SelectedUser?.Uid != uid) return;
                    ApplyPoolSnapshot(snapshot);
                    await Statistics(snapshot.AllRecords);
                    if (version != _accountLoadVersion || SelectedUser?.Uid != uid) return;
                    HasStatisticsData = snapshot.AllRecords.Count > 0;

                    if (showMessage)
                    {
                        NotificationType resultType = snapshot.AllRecords.Count == 0 ? NotificationType.Info : NotificationType.Success;
                        ToastHelper.ShowActionResult(
                            _uiStateService,
                            LanguageManager.Instance[resultType == NotificationType.Info ? "Toast_Info" : "Toast_Success"],
                            snapshot.AllRecords.Count == 0
                                ? LanguageManager.Instance["Msg_ActionNoNewData"]
                                : LanguageManager.Instance["Msg_LoadedLocalGacha"],
                            resultType,
                            nameof(StatisticsViewModel),
                            "gacha:load-local-data");
                    }

                    UserId = uid;
                }, "加载本地数据", ex =>
                {
                    if (version != _accountLoadVersion) return;
                    HasStatisticsData = false;
                    StatisticsErrorMessage = ex.Message;
                }, notifyUser: showMessage);
            }
            finally
            {
                if (version == _accountLoadVersion)
                {
                    _accountLoadCts = null;
                    _uiStateService.HideLoading();
                    _isLoadingLocalGachaLog = false;
                    IsStatisticsLoading = false;
                }
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
            await LoadLocalGachaLog(showMessage: true);
        }


        /// <summary>
        /// 从本地数据库加载所有用户账号并自动选中上次使用的账号
        /// </summary>
        /// <param name="showMessage">是否显示用户主动刷新账号的结果提示</param>
        private async Task LoadLocalAccount(bool showMessage = false)
        {
            try
            {
                _uiStateService.ShowLoading(LanguageManager.Instance["Msg_LoadingLocalAccount"]);

                await ExceptionHelper.ExecuteAsync(async () =>
                {
                    var users = await _userDataService.ListAccountsAsync(_navigationCts.Token);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (AccountList.Refresh(Users, users, _configService.User.LastUserId) is { } selected)
                        {
                            SelectedUser = selected;
                        }
                    });

                    if (showMessage)
                    {
                        NotificationType resultType = users.Count == 0 ? NotificationType.Info : NotificationType.Success;
                        ToastHelper.ShowActionResult(
                            _uiStateService,
                            LanguageManager.Instance[resultType == NotificationType.Info ? "Toast_Info" : "Toast_Success"],
                            users.Count == 0
                                ? LanguageManager.Instance["Msg_ActionNoNewData"]
                                : string.Format(LanguageManager.Instance["Msg_LoadedLocalAccount"], users.Count),
                            resultType,
                            nameof(StatisticsViewModel),
                            "gacha:refresh-accounts");
                    }

                }, "获取本地账号", notifyUser: showMessage);
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
            await LoadLocalAccount(showMessage: true);
        }

        private void ResetNavigationCancellation()
        {
            if (!_navigationCts.IsCancellationRequested) return;
            _navigationCts.Dispose();
            _navigationCts = new CancellationTokenSource();
        }
    }
}
