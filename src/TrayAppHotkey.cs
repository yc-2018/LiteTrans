using System;
using System.Windows.Forms;

namespace LiteTrans
{
    public partial class TrayApp
    {
        // ================== 窗口管理 ==================
        public MainForm Main
        {
            get
            {
                if (_main == null || _main.IsDisposed)
                {
                    _main = new MainForm(this);
                    _main.CreateControl();
                }
                return _main;
            }
        }

        public void ShowMain(bool readClipboard)
        {
            Dbg.Log("ShowMain(readClipboard=" + readClipboard + ") 进入");
            try { Main.ShowUp(readClipboard); Dbg.Log("ShowMain 正常返回"); }
            catch (Exception ex)
            {
                Dbg.Log("ShowMain 异常: " + ex);
                MessageBox.Show("打开窗口失败：" + ex.Message, "轻译",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void ShowSettings() { ShowSettings(0); }

        public void ShowSettings(int page)
        {
            if (_settings != null && !_settings.IsDisposed)
            {
                _settings.Show();
                _settings.GoToTab(page);
                Native.ForceForeground(_settings);
                return;
            }
            _settings = new SettingsForm(this);
            _settings.GoToTab(page);
            _settings.FormClosed += delegate { _settings = null; };
            _settings.Show();
            Native.ForceForeground(_settings);
        }

        /// <summary>设置保存后调用：重注册热键、重绘主题、刷新托盘</summary>
        public void OnConfigChanged()
        {
            Theme.Apply(Cfg);
            RegisterHotkeys();

            _tray.Icon = AppIcon.Get(Theme.Current.Dark);
            _tray.Text = "轻译 · " + Cfg.Hotkey;
            RebuildMenu();

            if (_main != null && !_main.IsDisposed) _main.ApplyTheme();
            ClipboardWatch(Cfg.MonitorClipboard);
        }

        public void ExitApp()
        {
            try { Cfg.Save(); } catch { }
            try { UnregisterHotkeys(); } catch { }
            try { ClipboardWatch(false); } catch { }
            try { Speech.Stop(); } catch { }
            if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
            if (_hk != null) _hk.DestroyHandle();
            ExitThread();
        }

        // ================== 全局热键 ==================
        public void RegisterHotkeys()
        {
            UnregisterHotkeys();

            uint mods, vk; Keys keys;
            if (HotkeyParser.TryParse(Cfg.Hotkey, out mods, out vk, out keys))
            {
                bool okReg = Native.RegisterHotKey(_hk.Handle, HK_MAIN, mods | Native.MOD_NOREPEAT, vk);
                Dbg.Log("RegisterHotKey " + Cfg.Hotkey + " ok=" + okReg + " hwnd=" + _hk.Handle);
                if (!okReg) NotifyHotkeyFailed(Cfg.Hotkey);
            }

            if (!string.IsNullOrWhiteSpace(Cfg.HotkeyInput) &&
                HotkeyParser.TryParse(Cfg.HotkeyInput, out mods, out vk, out keys))
            {
                Native.RegisterHotKey(_hk.Handle, HK_INPUT, mods | Native.MOD_NOREPEAT, vk);
            }
        }

        private void UnregisterHotkeys()
        {
            if (_hk == null) return;
            Native.UnregisterHotKey(_hk.Handle, HK_MAIN);
            Native.UnregisterHotKey(_hk.Handle, HK_INPUT);
        }

        private void NotifyHotkeyFailed(string hk)
        {
            if (_tray == null) return;
            _tray.ShowBalloonTip(5000, "快捷键被占用",
                hk + " 已被其他程序注册，请在设置里换一个组合。", ToolTipIcon.Warning);
        }

        public void OnHotkey(int id)
        {
            Dbg.Log("OnHotkey id=" + id);
            if (id == HK_MAIN) ShowMain(true);
            else if (id == HK_INPUT) ShowMain(false);
        }

        // ================== 复制即翻译 ==================
        private bool _watching;
        private string _lastClip;

        public void ClipboardWatch(bool on)
        {
            if (on == _watching) return;
            _watching = on;
            if (on) Native.AddClipboardFormatListener(_hk.Handle);
            else Native.RemoveClipboardFormatListener(_hk.Handle);
        }

        public void OnClipboardChanged()
        {
            if (!Cfg.MonitorClipboard) return;
            if (Selection.Busy) return;      // 是划词抓取/还原造成的变化，不是用户复制
            try
            {
                if (!Clipboard.ContainsText()) return;
                var txt = Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(txt) || txt == _lastClip) return;
                _lastClip = txt;
                ShowMain(true);
            }
            catch { }
        }
    }

    /// <summary>只为接收 WM_HOTKEY / WM_CLIPBOARDUPDATE 而存在的消息窗口</summary>
    public class HotkeyWindow : NativeWindow
    {
        private readonly TrayApp _app;
        private const int WM_CLIPBOARDUPDATE = 0x031D;
        private static readonly uint WM_WAKE = Native.RegisterWindowMessage("LiteTrans_Wake");

        public HotkeyWindow(TrayApp app)
        {
            _app = app;
            // 不能用 HWND_MESSAGE：那种窗口既收不到广播，也无法被 FindWindow 定位，
            // 单实例唤起会静默失效。改成挪到屏幕外的隐藏工具窗口。
            CreateHandle(new CreateParams
            {
                Caption = Native.MsgWindowTitle,
                X = -32000, Y = -32000, Width = 1, Height = 1,
                Style = 0,                                  // 不含 WS_VISIBLE
                ExStyle = 0x00000080 | 0x08000000,          // TOOLWINDOW | NOACTIVATE
            });
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY) { Dbg.Log("WndProc 收到 WM_HOTKEY id=" + m.WParam.ToInt32()); _app.OnHotkey(m.WParam.ToInt32()); }
            else if (m.Msg == WM_CLIPBOARDUPDATE) _app.OnClipboardChanged();
            else if (WM_WAKE != 0 && m.Msg == WM_WAKE)
            {
                if (m.WParam.ToInt32() == 1) _app.ShowSettings();
                else _app.ShowMain(true);
            }
            base.WndProc(ref m);
        }
    }
}
