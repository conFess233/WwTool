using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
namespace WwTool.UI.Components
{
    public partial class TitleBar : UserControl
    {
        private Window _parentWindow;

        public TitleBar()
        {
            InitializeComponent();
            this.Loaded += TitleBar_Loaded;
        }

        private void TitleBar_Loaded(object sender, RoutedEventArgs e)
        {
            // 自动获取当前标题栏所属的 Window
            _parentWindow = Window.GetWindow(this);
            if (_parentWindow != null)
            {
                // 监听状态改变更新最大化图标
                _parentWindow.StateChanged += ParentWindow_StateChanged;

                IntPtr handle = new WindowInteropHelper(_parentWindow).Handle;
                HwndSource.FromHwnd(handle)?.AddHook(WindowProc);
            }
        }

        // 拖拽逻辑
        private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_parentWindow == null) return;

            // 双击最大化/还原窗口
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
                return;
            }

            if (e.ClickCount == 1)
            {
                try
                {
                    _parentWindow.DragMove();
                }
                catch (Exception)
                {
                }
            }
        }

        // 按钮事件
        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            if (_parentWindow != null) _parentWindow.WindowState = WindowState.Minimized;
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximize();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            _parentWindow?.Close();
        }

        private void ToggleMaximize()
        {
            if (_parentWindow == null) return;
            _parentWindow.WindowState = _parentWindow.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void ParentWindow_StateChanged(object sender, EventArgs e)
        {
            if (_parentWindow == null) return;
            // 切换最大化与常规状态的几何 icon
            if (_parentWindow.WindowState == WindowState.Maximized)
            {
                PathMaximize.Data = Geometry.Parse("M 0,2 L 8,2 L 8,10 L 0,10 Z M 2,2 L 2,0 L 10,0 L 10,8 L 8,8");
            }
            else
            {
                PathMaximize.Data = Geometry.Parse("M 0,0 L 10,0 L 10,10 L 0,10 Z");
            }
        }

        #region 任务栏边缘
        private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x0024) // WM_GETMINMAXINFO
            {
                WmGetMinMaxInfo(hwnd, lParam);
                handled = true;
            }
            return IntPtr.Zero;
        }

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, MONITORINFO lpMi);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);

        private void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
        {
            MINMAXINFO mmi = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO));
            IntPtr monitor = MonitorFromWindow(hwnd, 2); // MONITOR_DEFAULTTONEAREST

            if (monitor != IntPtr.Zero)
            {
                MONITORINFO monitorInfo = new MONITORINFO();
                GetMonitorInfo(monitor, monitorInfo);
                RECT rcWorkArea = monitorInfo.rcWork;
                RECT rcMonitorArea = monitorInfo.rcMonitor;

                mmi.ptMaxPosition.x = Math.Abs(rcWorkArea.left - rcMonitorArea.left);
                mmi.ptMaxPosition.y = Math.Abs(rcWorkArea.top - rcMonitorArea.top);
                mmi.ptMaxSize.x = Math.Abs(rcWorkArea.right - rcWorkArea.left);
                mmi.ptMaxSize.y = Math.Abs(rcWorkArea.bottom - rcWorkArea.top);
            }

            if (_parentWindow != null && _parentWindow.MinWidth > 0 && _parentWindow.MinHeight > 0)
            {
                PresentationSource source = PresentationSource.FromVisual(_parentWindow);
                if (source != null)
                {
                    // 计算屏幕 DPI 缩放
                    Matrix transformToDevice = source.CompositionTarget.TransformToDevice;
                    mmi.ptMinTrackSize.x = (int)(_parentWindow.MinWidth * transformToDevice.M11);
                    mmi.ptMinTrackSize.y = (int)(_parentWindow.MinHeight * transformToDevice.M22);
                }
                else
                {
                    mmi.ptMinTrackSize.x = (int)_parentWindow.MinWidth;
                    mmi.ptMinTrackSize.y = (int)_parentWindow.MinHeight;
                }
            }

            Marshal.StructureToPtr(mmi, lParam, true);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential)]
        private class MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MONITORINFO
        {
            public int cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            public RECT rcMonitor = new RECT();
            public RECT rcWork = new RECT();
            public int dwFlags = 0;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int left; public int top; public int right; public int bottom; }
        #endregion
    }
}
