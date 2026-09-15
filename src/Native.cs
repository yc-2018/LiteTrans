using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LiteTrans
{
    /// <summary>Win32 互操作：全局热键、Win11 圆角/深色标题栏、前台窗口抢占</summary>
    public static class Native
    {
        // ——— 全局热键 ———
        public const int WM_HOTKEY = 0x0312;
        public const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_WIN = 0x8, MOD_NOREPEAT = 0x4000;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        // ——— 前台窗口 ———
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr pid);
        [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern uint RegisterWindowMessage(string name);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string win);
        public const string MsgWindowTitle = "LiteTransMsgWnd_v1";
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wp, IntPtr lp);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
        [DllImport("user32.dll")] public static extern bool AddClipboardFormatListener(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool RemoveClipboardFormatListener(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool ReleaseCapture();
        [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X, Y; }

        /// <summary>Windows 有前台锁，直接 SetForegroundWindow 常常失败，需临时附加输入线程</summary>
        public static void ForceForeground(Form f)
        {
            try
            {
                IntPtr fore = GetForegroundWindow();
                uint foreThread = GetWindowThreadProcessId(fore, IntPtr.Zero);
                uint thisThread = GetCurrentThreadId();
                if (foreThread != thisThread) AttachThreadInput(foreThread, thisThread, true);
                ShowWindow(f.Handle, 5 /*SW_SHOW*/);
                SetForegroundWindow(f.Handle);
                f.Activate();
                if (foreThread != thisThread) AttachThreadInput(foreThread, thisThread, false);
            }
            catch { try { f.Activate(); } catch { } }
        }

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string subApp, string subIdList);

        /// <summary>让控件的原生滚动条切换到深色/浅色外观</summary>
        public static void ApplyScrollbarTheme(Control c, bool dark)
        {
            try
            {
                if (c == null || !c.IsHandleCreated) return;
                SetWindowTheme(c.Handle, dark ? "DarkMode_Explorer" : "Explorer", null);
            }
            catch { }
        }

        // ——— DWM：Win11 圆角与深色标题栏 ———
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWA_BORDER_COLOR = 34;
        private const int CORNER_ROUND = 2;

        public static void ApplyRoundCorners(IntPtr hwnd)
        {
            int v = CORNER_ROUND;
            try { DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref v, sizeof(int)); } catch { }
        }

        public static void ApplyDarkTitleBar(IntPtr hwnd, bool dark)
        {
            int v = dark ? 1 : 0;
            try { DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref v, sizeof(int)); } catch { }
        }

        public static void ApplyBorderColor(IntPtr hwnd, Color c)
        {
            int v = c.R | (c.G << 8) | (c.B << 16);
            try { DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref v, sizeof(int)); } catch { }
        }

        // ——— 系统是否为深色主题 ———
        public static bool SystemUsesDarkTheme()
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (k == null) return false;
                    var v = k.GetValue("AppsUseLightTheme");
                    return v != null && Convert.ToInt32(v) == 0;
                }
            }
            catch { return false; }
        }

        /// <summary>系统强调色，用作默认主题色</summary>
        public static Color? SystemAccent()
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM"))
                {
                    if (k == null) return null;
                    var v = k.GetValue("AccentColor");
                    if (v == null) return null;
                    // 注册表里是 ABGR
                    uint abgr = unchecked((uint)Convert.ToInt32(v));
                    return Color.FromArgb((int)(abgr & 0xFF), (int)((abgr >> 8) & 0xFF), (int)((abgr >> 16) & 0xFF));
                }
            }
            catch { return null; }
        }
    }

    /// <summary>把 "Ctrl+Alt+D" 这类字符串与 Win32 修饰键/虚拟键互转</summary>
    public static class HotkeyParser
    {
        public static bool TryParse(string s, out uint mods, out uint vk, out Keys keys)
        {
            mods = 0; vk = 0; keys = Keys.None;
            if (string.IsNullOrWhiteSpace(s)) return false;

            foreach (var raw in s.Split('+'))
            {
                var p = raw.Trim();
                if (p.Length == 0) continue;
                switch (p.ToLowerInvariant())
                {
                    case "ctrl": case "control": mods |= Native.MOD_CONTROL; keys |= Keys.Control; break;
                    case "alt": mods |= Native.MOD_ALT; keys |= Keys.Alt; break;
                    case "shift": mods |= Native.MOD_SHIFT; keys |= Keys.Shift; break;
                    case "win": case "windows": mods |= Native.MOD_WIN; break;
                    default:
                        try
                        {
                            var k = (Keys)Enum.Parse(typeof(Keys), Normalize(p), true);
                            vk = (uint)k; keys |= k;
                        }
                        catch { return false; }
                        break;
                }
            }
            return vk != 0 && mods != 0;   // 必须带修饰键，否则会抢占普通打字
        }

        private static string Normalize(string p)
        {
            if (p.Length == 1 && char.IsDigit(p[0])) return "D" + p;      // "1" -> Keys.D1
            if (p.Length == 1) return p.ToUpperInvariant();
            switch (p.ToLowerInvariant())
            {
                case "space": return "Space";
                case "enter": case "return": return "Enter";
                case "`": case "grave": return "Oemtilde";
                case "-": return "OemMinus";
                case "=": return "Oemplus";
                case "\\": return "OemPipe";
                case ";": return "OemSemicolon";
                case "'": return "OemQuotes";
                case ",": return "Oemcomma";
                case ".": return "OemPeriod";
                case "/": return "OemQuestion";
                case "[": return "OemOpenBrackets";
                case "]": return "OemCloseBrackets";
                default: return p;
            }
        }

        /// <summary>Keys 组合转回显示字符串，用于设置界面的按键录制</summary>
        public static string ToText(Keys k)
        {
            var key = k & Keys.KeyCode;
            if (key == Keys.None || key == Keys.ControlKey || key == Keys.Menu ||
                key == Keys.ShiftKey || key == Keys.LWin || key == Keys.RWin) return null;

            var s = "";
            if ((k & Keys.Control) == Keys.Control) s += "Ctrl+";
            if ((k & Keys.Alt) == Keys.Alt) s += "Alt+";
            if ((k & Keys.Shift) == Keys.Shift) s += "Shift+";
            if (s.Length == 0) return null;                  // 拒绝无修饰键的组合

            var name = key.ToString();
            if (name.Length == 2 && name[0] == 'D' && char.IsDigit(name[1])) name = name.Substring(1);
            return s + name;
        }
    }
}
