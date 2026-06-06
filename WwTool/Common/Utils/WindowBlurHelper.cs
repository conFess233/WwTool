using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WwTool.Common.Utils
{
    /// <summary>
    /// 窗口模糊毛玻璃特效辅助类
    /// </summary>
    public static class WindowBlurHelper
    {
        [DllImport("user32.dll")]
        internal static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        [StructLayout(LayoutKind.Sequential)]
        internal struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        internal enum WindowCompositionAttribute
        {
            WCA_ACCENT_POLICY = 19
        }

        internal enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            ACCENT_ENABLE_BLURBEHIND = 3,       // Win10 标准模糊
            ACCENT_ENABLE_ACRYLICBLURBEHIND = 4 // Win10/Win11 亚克力模糊
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct AccentPolicy
        {
            public AccentState AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        [DllImport("dwmapi.dll")]
        internal static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        internal const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        internal enum DWM_WINDOW_CORNER_PREFERENCE
        {
            DWMWCP_DEFAULT = 0,
            DWMWCP_DONOTROUND = 1,
            DWMWCP_ROUND = 2,      // 标准圆角
            DWMWCP_ROUNDSMALL = 3  // 小圆角
        }

        /// <summary>
        /// 为指定的 WPF 窗口开启模糊效果
        /// </summary>
        public static void EnableBlur(Window window)
        {
            var windowHelper = new WindowInteropHelper(window);
            IntPtr hwnd = windowHelper.Handle;
            if (hwnd == IntPtr.Zero) return;

            var accent = new AccentPolicy
            {
                AccentState = AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
                // 这里可以控制亚克力的叠加颜色。0x01000000 代表完全交由 WPF 的 Background 画刷控制
                GradientColor = 0x01000000
            };

            SetAccentPolicy(hwnd, accent);

            int preference = (int)DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
        }

        /// <summary>
        /// 关闭窗口的毛玻璃模糊
        /// </summary>
        public static void DisableBlur(Window window)
        {
            var windowHelper = new WindowInteropHelper(window);
            IntPtr hwnd = windowHelper.Handle;
            if (hwnd == IntPtr.Zero) return;

            var accent = new AccentPolicy
            {
                AccentState = AccentState.ACCENT_DISABLED
            };

            SetAccentPolicy(hwnd, accent);

            int preference = (int)DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
        }

        private static void SetAccentPolicy(IntPtr hwnd, AccentPolicy accent)
        {
            int accentStructSize = Marshal.SizeOf(accent);
            IntPtr accentPtr = Marshal.AllocHGlobal(accentStructSize);
            Marshal.StructureToPtr(accent, accentPtr, false);

            var data = new WindowCompositionAttributeData
            {
                Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                SizeOfData = accentStructSize,
                Data = accentPtr
            };

            SetWindowCompositionAttribute(hwnd, ref data);
            Marshal.FreeHGlobal(accentPtr);
        }
    }
}