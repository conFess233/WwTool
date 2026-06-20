using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Windows.Controls.Primitives;
using WwTool.Common;
using WwTool.Common.Events;
using WwTool.Common.Models;
using WwTool.Common.Models.Config;
using WwTool.Common.Utils;
using WwTool.Extensions;
using WwTool.Services;
using WwTool.Services.Interfaces;

namespace WwTool.UI.ViewModels
{
    /// <summary>
    /// 主窗体视图模型
    /// </summary>
    public class MainViewModel : BindableBase, IConfigureService
    {

        /// <summary>
        /// Prism 区域管理器，负责视图的导航切换
        /// </summary>
        private readonly IRegionManager _regionManager;
        /// <summary>
        /// Prism 弹窗服务，用于显示确认/警告等弹出式对话框
        /// </summary>
        private readonly IDialogService _dialogService;
        /// <summary>
        /// Prism 事件聚合器，用于发布/订阅全局事件（如背景模糊）
        /// </summary>
        private readonly IEventAggregator _eventAggregator;
        /// <summary>
        /// UI 状态服务，提供 Toast 提示和加载动画的控制
        /// </summary>
        private readonly IUIStateService _uiStateService;
        /// <summary>
        /// 配置服务，提供应用、API 和用户配置的读写
        /// </summary>
        private readonly IConfigService _configService;

        /// <summary>
        /// 暴露给视图层的 UI 状态服务实例
        /// </summary>
        public IUIStateService UIStateService { get { return _uiStateService; } }
        /// <summary>
        /// 侧边栏导航命令
        /// </summary>
        public DelegateCommand<NavItem> NavigateCommand { get; private set; }

        /// <summary>
        /// 侧边栏导航项集合
        /// </summary>
        private ObservableCollection<NavItem> navItems;

        /// <summary>
        /// 全局消息提示文本
        /// </summary>
        private string? message;
        /// <summary>
        /// 当前选中的侧边栏索引
        /// </summary>
        private int selectedIndex;
        /// <summary>
        /// 是否启用全局背景模糊（弹窗打开时启用）
        /// </summary>
        private bool _isGlobalBlur;

        public ObservableCollection<NavItem> NavItems
        {
            get { return navItems; }
            set { navItems = value; RaisePropertyChanged(); }
        }

        public string? Message
        {
            get { return message; }
            set { message = value; RaisePropertyChanged(); }
        }
        public int SelectedIndex
        {
            get { return selectedIndex; }
            set
            {
                selectedIndex = value; RaisePropertyChanged();
            }
        }

        /// <summary>
        /// 是否启用毛玻璃特效（从用户配置中读取）
        /// </summary>
        public bool IsGlassEffectEnabled => _configService.User.IsGlassEffectEnabled;
        public double BackgroundOpacity => _configService.User.GlassOpacity / 100.0;

        public bool IsGlobalBlur
        {
            get => _isGlobalBlur;
            set => SetProperty(ref _isGlobalBlur, value);
        }

        /// <summary>
        /// 构造函数，初始化服务依赖、导航命令和事件订阅
        /// </summary>
        public MainViewModel(IRegionManager regionManager, IDialogService dialogService, IUIStateService uIStateService, IEventAggregator eventAggregator, IConfigService configService)
        {
            this._dialogService = dialogService;
            this._eventAggregator = eventAggregator;
            this._uiStateService = uIStateService;
            this._regionManager = regionManager;
            _configService = configService;
            NavItems = new ObservableCollection<NavItem>();
            NavigateCommand = new DelegateCommand<NavItem>(Navigate);

            eventAggregator.GetEvent<GlobalBlurEvent>().Subscribe(isBlur =>
            {
                IsGlobalBlur = isBlur;
            });

            LanguageManager.Instance.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "Item[]")
                {
                    UpdateNavTitles();
                }
            };

            _configService.User.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_configService.User.IsGlassEffectEnabled))
                {
                    RaisePropertyChanged(nameof(IsGlassEffectEnabled));
                    RaisePropertyChanged(nameof(BackgroundOpacity));
                }
                else if (e.PropertyName == nameof(_configService.User.GlassOpacity))
                {
                    RaisePropertyChanged(nameof(BackgroundOpacity));
                }
            };
        }

        /// <summary>
        /// 语言切换后更新侧边栏导航项的显示标题
        /// </summary>
        private void UpdateNavTitles()
        {
            foreach (var item in NavItems)
            {
                switch (item.NameSpace)
                {
                    case "IndexView": item.Title = LanguageManager.Instance["Nav_Home"]; break;
                    case "StatisticsView": item.Title = LanguageManager.Instance["Nav_Statistics"]; break;
                    case "RoleDataView": item.Title = LanguageManager.Instance["Nav_RoleData"]; break;
                    case "ExplorationDataView": item.Title = LanguageManager.Instance["Nav_Exploration"]; break;
                    case "MotorDataView": item.Title = LanguageManager.Instance["Nav_Motor"]; break;
                    case "SettingsView": item.Title = LanguageManager.Instance["Nav_Settings"]; break;
                    case "AboutView": item.Title = LanguageManager.Instance["Nav_About"]; break;
                }
            }
        }

        #region 方法
        /// <summary>
        /// 执行侧边栏导航，将主内容区域切换到目标视图
        /// </summary>
        /// <param name="item">目标导航项</param>
        private void Navigate(NavItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.NameSpace)) return;

            _regionManager.Regions[PrismManager.MainViewRegionName].RequestNavigate(item.NameSpace);
        }

        /// <summary>
        /// 创建侧边栏导航菜单项，按顺序添加页面导航入口
        /// </summary>
        private void CreateNav()
        {
            NavItems.Add(new NavItem { Icon = "HomeImage", Title = LanguageManager.Instance["Nav_Home"], NameSpace = "IndexView", IsBottomItem = false });
            NavItems.Add(new NavItem { Icon = "StatisticsImage", Title = LanguageManager.Instance["Nav_Statistics"], NameSpace = "StatisticsView", IsBottomItem = false });
            NavItems.Add(new NavItem { Icon = "RoleImage", Title = LanguageManager.Instance["Nav_RoleData"], NameSpace = "RoleDataView", IsBottomItem = false });
            NavItems.Add(new NavItem { Icon = "ChestImage", Title = LanguageManager.Instance["Nav_Exploration"], NameSpace = "ExplorationDataView", IsBottomItem = false });
            NavItems.Add(new NavItem { Icon = "MotorImage", Title = LanguageManager.Instance["Nav_Motor"], NameSpace = "MotorDataView", IsBottomItem = false });
            NavItems.Add(new NavItem { Icon = "SettingsImage", Title = LanguageManager.Instance["Nav_Settings"], NameSpace = "SettingsView", IsBottomItem = true });
            NavItems.Add(new NavItem { Icon = "AboutImage", Title = LanguageManager.Instance["Nav_About"], NameSpace = "AboutView", IsBottomItem = true });
        }
        /// <summary>
        /// 应用程序初始化配置，创建导航菜单并跳转到首页
        /// </summary>
        public async Task ConfigureAsync()
        {
            NavItems.Clear();
            CreateNav();
            _regionManager.Regions[PrismManager.MainViewRegionName].RequestNavigate("IndexView");
        }

        /// <summary>
        /// 请求关闭窗口
        /// </summary>
        /// <param name="confirmAction">确认后执行的关闭委托</param>
        public void RequestClose(Action confirmAction)
        {
            var parameters = new DialogParameters
            {
                { "Title", LanguageManager.Instance["Dialog_ExitTitle"] },
                { "Message", LanguageManager.Instance["Dialog_ExitMessage"] },
                { "ShowCancel", true }
            };

            _dialogService.ShowDialog("AlertView", parameters, result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    confirmAction?.Invoke();
                }
            });
        }

        #endregion
    }
}
