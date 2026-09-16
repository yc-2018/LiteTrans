using System;
using System.Drawing;
using System.Windows.Forms;

namespace LiteTrans
{
    /// <summary>应用中枢：持有托盘图标、全局热键与各窗口</summary>
    public partial class TrayApp : ApplicationContext
    {
        public Config Cfg;
        private NotifyIcon _tray;
        private HotkeyWindow _hk;
        private MainForm _main;
        private SettingsForm _settings;

        public bool SettingsOpen { get { return _settings != null && _settings.Visible; } }

        private const int HK_MAIN = 0xA001;
        private const int HK_INPUT = 0xA002;

        public TrayApp(Config cfg, bool startHidden)
        {
            Cfg = cfg;
            Theme.Apply(Cfg);

            _hk = new HotkeyWindow(this);
            BuildTray();
            RegisterHotkeys();

            // 与注册表实际状态对齐，避免手动改过注册表后不一致
            Cfg.AutoStart = Startup.IsEnabled();

            if (!startHidden) ShowMain(false);

            if (Cfg.FirstRun && Cfg.TrayBalloonOnStart)
            {
                _tray.ShowBalloonTip(4000, "轻译已在后台运行",
                    "按 " + Cfg.Hotkey + " 翻译剪贴板内容，Esc 收回托盘。",
                    ToolTipIcon.Info);
                Cfg.FirstRun = false;
                Cfg.Save();
            }
        }

        // ================== 托盘 ==================
        private void BuildTray()
        {
            _tray = new NotifyIcon
            {
                Icon = AppIcon.Get(Theme.Current.Dark),
                Visible = true,
                Text = "轻译 · " + Cfg.Hotkey,
            };
            // 使用 MouseUp 处理左键，避免 NotifyIcon 的双击手势吞掉单击消息。
            _tray.MouseUp += Tray_MouseUp;
            RebuildMenu();
        }

        private void Tray_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) ShowMainFromTray();
        }

        public void RebuildMenu()
        {
            var m = new ContextMenuStrip { ShowImageMargin = false, Font = new Font("Microsoft YaHei UI", 9f) };

            m.Items.Add(Item("翻译剪贴板　" + Cfg.Hotkey, delegate { ShowMainFromTray(); }));
            m.Items.Add(Item("打开输入框", delegate { ShowMain(false); }));
            m.Items.Add(new ToolStripSeparator());

            var hist = new ToolStripMenuItem("最近翻译");
            var items = History.Items;
            if (items.Count == 0) hist.DropDownItems.Add(Item("（暂无记录）", null, false));
            else
            {
                int n = Math.Min(12, items.Count);
                for (int i = 0; i < n; i++)
                {
                    var it = items[i];
                    var label = it.Src.Replace("\n", " ");
                    if (label.Length > 28) label = label.Substring(0, 28) + "…";
                    var captured = it;
                    hist.DropDownItems.Add(Item(label, delegate { ReplayHistory(captured); }));
                }
                hist.DropDownItems.Add(new ToolStripSeparator());
                hist.DropDownItems.Add(Item("清空历史", delegate { History.Clear(); RebuildMenu(); }));
            }
            m.Items.Add(hist);
            m.Items.Add(new ToolStripSeparator());

            m.Items.Add(Item("设置…", delegate { ShowSettings(); }));
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(Item("退出", delegate { ExitApp(); }));

            if (_tray.ContextMenuStrip != null) _tray.ContextMenuStrip.Dispose();
            _tray.ContextMenuStrip = m;
        }

        private static ToolStripMenuItem Item(string text, EventHandler h, bool enabled = true)
        {
            var i = new ToolStripMenuItem(text) { Enabled = enabled };
            if (h != null) i.Click += h;
            return i;
        }

        private void ReplayHistory(HistoryItem it)
        {
            var f = Main;
            f.ShowUp(false);
            f.SetSource(it.Src);
            f.TranslateNow(it.Src);
        }
    }
}
