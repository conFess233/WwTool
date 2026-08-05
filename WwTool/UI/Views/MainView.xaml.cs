using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using WwTool.Common.Models.Config;
using WwTool.Common.Utils;
using WwTool.Services;
using WwTool.Services.Interfaces;
using WwTool.UI.ViewModels;

namespace WwTool.UI.Views
{
    /// <summary>
    /// MainView.xaml 的交互逻辑
    /// </summary>
    /// <summary>
    /// 主界面视图
    /// </summary>
    public partial class MainView : Window
    {

        private bool _isConfirmed = false;
        private readonly IConfigService _configService;
        public MainView(IConfigService configService)
        {
            InitializeComponent();
            leftNav.ConfigurePushTransition(MainRegionContent, NavigationColumn);
            _configService = configService;
            _configService.User.PropertyChanged += UserConfig_PropertyChanged;
            SystemParameters.StaticPropertyChanged += SystemParameters_StaticPropertyChanged;
        }

        private void SystemParameters_StaticPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SystemParameters.HighContrast))
            {
                Dispatcher.Invoke(ApplyGlassEffect);
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            ApplyGlassEffect();
        }

        private void UserConfig_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // 开关与透明度变化都需要立即同步到窗口材质。
            if (e.PropertyName is nameof(UserConfig.IsGlassEffectEnabled) or nameof(UserConfig.GlassOpacity))
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ApplyGlassEffect();
                });
            }
        }

        /// <summary>
        /// 同步毛玻璃状态与主窗口底色透明度。
        /// </summary>
        private void ApplyGlassEffect()
        {
            bool useGlass = _configService.User.IsGlassEffectEnabled && !SystemParameters.HighContrast;
            WindowBackgroundLayer.Opacity = useGlass
                ? Math.Clamp(_configService.User.GlassOpacity, 10, 95) / 100d
                : 1d;

            if (useGlass)
            {
                WindowBlurHelper.EnableBlur(this);
            }
            else
            {
                // 移除底层 API 效果
                WindowBlurHelper.DisableBlur(this);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _configService.User.PropertyChanged -= UserConfig_PropertyChanged;
            SystemParameters.StaticPropertyChanged -= SystemParameters_StaticPropertyChanged;
            base.OnClosed(e);
        }

        private void ResizeGrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                // 获取当前窗口句柄，并移交系统进行右下角拉伸
                IntPtr handle = new WindowInteropHelper(this).Handle;
                Mouse.Capture(null);
                SendMessage(handle, WM_SYSCOMMAND, (IntPtr)(SC_SIZE + BOTTOMRIGHT), IntPtr.Zero);
                e.Handled = true;
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (_isConfirmed) return;

            e.Cancel = true;

            if (this.DataContext is MainViewModel vm)
            {
                vm.RequestClose(() =>
                {
                    _isConfirmed = true;
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        this.Close();
                    }));
                });
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);

        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_SIZE = 0xF000;
        private const int BOTTOMRIGHT = 8;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    }
}
