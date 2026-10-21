using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LiteTrans
{
    /// <summary>
    /// 界面尺寸一律按 96 DPI（100% 缩放）设计，运行时统一乘当前显示器倍数。
    /// 字号用的是 pt，本身会随 DPI 变大；间距、控件尺寸若继续写死像素，
    /// 高 DPI 下就会出现「字变大、框没变大」的错位。
    /// </summary>
    internal static class UiScale
    {
        private const uint MonitorDefaultToNearest = 2;
        private const int MdtEffectiveDpi = 0;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint flags);
        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);
        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);
        [DllImport("user32.dll")]
        private static extern uint GetDpiForSystem();

        /// <summary>
        /// 当前缩放倍数（96 DPI = 1.0）。
        /// 实测 GetDpiForWindow 在窗口尚未按显示器初始化时会返回 96，
        /// 直接采信会让布局按 100% 算、字体却按 200% 画（字大框小）。
        /// 因此以 GetDpiForSystem 为准，并记住历史最大值，避免中途降级。
        /// </summary>
        public static float Factor(Control c)
        {
            uint dpi = 96;
            try
            {
                uint sys = GetDpiForSystem();
                if (sys >= 48 && sys <= 960) dpi = sys;
            }
            catch { }

            try
            {
                if (c != null && c.IsHandleCreated)
                {
                    uint w = GetDpiForWindow(c.Handle);
                    if (w >= 48 && w <= 960 && w > dpi) dpi = w;
                }
            }
            catch { }

            if (dpi < _maxDpi) dpi = _maxDpi;
            else _maxDpi = dpi;
            return dpi / 96f;
        }

        private static uint _maxDpi = 96;

        /// <summary>
        /// 进程还没建窗口时用系统 DPI。PerMonitorV2 进程里 GetDpiForMonitor 会返回
        /// 和实际缩放不一致的值（常常还是 96），必须优先用 GetDpiForSystem。
        /// </summary>
        public static float SystemFactor()
        {
            try
            {
                uint dpi = GetDpiForSystem();
                if (dpi >= 48 && dpi <= 960) return dpi / 96f;
            }
            catch { }

            try
            {
                var pos = Cursor.Position;
                IntPtr mon = MonitorFromPoint(new POINT { X = pos.X, Y = pos.Y }, MonitorDefaultToNearest);
                uint dx, dy;
                if (mon != IntPtr.Zero && GetDpiForMonitor(mon, MdtEffectiveDpi, out dx, out dy) == 0 &&
                    dx >= 48 && dx <= 960)
                    return dx / 96f;
            }
            catch { }
            return 1f;
        }
    }
}
