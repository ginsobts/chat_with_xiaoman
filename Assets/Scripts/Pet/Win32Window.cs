using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
#endif

namespace VN
{
    /// <summary>
    /// 把当前 Windows 窗口变成：无边框 / 透明（逐像素 alpha）/ 始终置顶 / 可点击穿透。
    /// 仅在打包后的 Windows 运行时生效；编辑器中为空操作。
    /// </summary>
    public static class Win32Window
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;
        private const uint WS_POPUP = 0x80000000;
        private const uint WS_VISIBLE = 0x10000000;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOPMOST = 0x00000008;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_APPWINDOW = 0x00040000;

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        [StructLayout(LayoutKind.Sequential)]
        private struct MARGINS { public int left, right, top, bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);
        [DllImport("user32.dll")] private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
        [DllImport("kernel32.dll")] private static extern uint GetTickCount();
        [DllImport("Dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS m);

        private static IntPtr _hwnd;
        private static uint _baseExStyle;
        private static bool _clickThrough;
#endif

        public static void MakeTransparentOverlay(int width = 0, int height = 0)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            _hwnd = ResolveUnityWindow();
            if (_hwnd == IntPtr.Zero)
            {
                UnityEngine.Debug.LogWarning("[Win32Window] 找不到 Unity 窗口句柄，无法启用桌宠透明窗口。");
                return;
            }

            // 无边框
            SetWindowLong(_hwnd, GWL_STYLE, WS_POPUP | WS_VISIBLE);

            // 分层 + 置顶 + 工具窗口：工具窗口不会显示在任务栏里。
            _baseExStyle = WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_TOOLWINDOW;
            SetWindowLong(_hwnd, GWL_EXSTYLE, _baseExStyle);
            RefreshTaskbarButton();

            // 逐像素透明：把 DWM 边框扩展到整个客户区
            var margins = new MARGINS { left = -1, right = -1, top = -1, bottom = -1 };
            DwmExtendFrameIntoClientArea(_hwnd, ref margins);

            // 覆盖整个主屏并置顶
            int sw = width > 0 ? width : Screen.currentResolution.width;
            int sh = height > 0 ? height : Screen.currentResolution.height;
            SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, sw, sh,
                SWP_FRAMECHANGED | SWP_SHOWWINDOW);
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private static IntPtr ResolveUnityWindow()
        {
            IntPtr hwnd = GetActiveWindow();
            if (hwnd != IntPtr.Zero) return hwnd;

            hwnd = GetForegroundWindow();
            if (hwnd != IntPtr.Zero) return hwnd;

            var process = Process.GetCurrentProcess();
            if (process.MainWindowHandle == IntPtr.Zero)
                process.Refresh();
            return process.MainWindowHandle;
        }
#endif

        /// <summary>
        /// 退出桌宠/AVG 悬浮形态、回到普通游戏时调用：
        /// 撤销透明置顶和点击穿透，恢复成一个能正常接收点击的无边框窗口。
        /// 不还原的话，穿透状态会残留，回到开头后点哪都没反应（像卡死）。
        /// </summary>
        public static void RestoreNormalWindow(int width = 0, int height = 0)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (_hwnd == IntPtr.Zero) _hwnd = ResolveUnityWindow();
            if (_hwnd == IntPtr.Zero) return;

            _clickThrough = false;

            // 无边框但不再分层/穿透/置顶，恢复普通应用窗口，使主游戏能重新出现在任务栏。
            SetWindowLong(_hwnd, GWL_STYLE, WS_POPUP | WS_VISIBLE);
            _baseExStyle = WS_EX_APPWINDOW;
            SetWindowLong(_hwnd, GWL_EXSTYLE, _baseExStyle);
            RefreshTaskbarButton();

            // 撤销 DWM 玻璃扩展（否则透明区域仍会透出桌面）
            var margins = new MARGINS { left = 0, right = 0, top = 0, bottom = 0 };
            DwmExtendFrameIntoClientArea(_hwnd, ref margins);

            int sw = width > 0 ? width : Screen.currentResolution.width;
            int sh = height > 0 ? height : Screen.currentResolution.height;
            SetWindowPos(_hwnd, HWND_NOTOPMOST, 0, 0, sw, sh,
                SWP_FRAMECHANGED | SWP_SHOWWINDOW);
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private static void RefreshTaskbarButton()
        {
            // Windows 通常只在窗口重新 show 时刷新任务栏按钮归属。
            ShowWindow(_hwnd, SW_HIDE);
            ShowWindow(_hwnd, SW_SHOW);
        }
#endif

        /// <summary>true=鼠标点击穿透到桌面；false=窗口接收点击。</summary>
        public static void SetClickThrough(bool clickThrough)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (_hwnd == IntPtr.Zero) return;
            if (clickThrough == _clickThrough) return;
            _clickThrough = clickThrough;

            uint ex = _baseExStyle;
            if (clickThrough) ex |= (uint)WS_EX_TRANSPARENT;
            SetWindowLong(_hwnd, GWL_EXSTYLE, ex);
#endif
        }

        /// <summary>
        /// Windows 系统级「最后一次鼠标/键盘输入」距离现在的秒数。
        /// 桌宠窗口点击穿透或失焦时，Unity 自己的 Input 不一定可靠，所以用系统 API。
        /// </summary>
        public static float GetSystemIdleSeconds()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var info = new LASTINPUTINFO();
            info.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(LASTINPUTINFO));
            if (!GetLastInputInfo(ref info)) return 0f;
            uint now = GetTickCount();
            return (now - info.dwTime) / 1000f;
#else
            return 0f;
#endif
        }
    }
}
