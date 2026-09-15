using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace LiteTrans
{
    public partial class SettingsForm : Form
    {
        private readonly TrayApp _app;
        private Config _c;                 // 编辑副本，点保存才写回

        private Panel _nav, _body;
        private readonly List<FlatBtn> _tabs = new List<FlatBtn>();
        private readonly List<Panel> _pages = new List<Panel>();
        private FlatBtn _btnSave, _btnCancel;
        private readonly List<Action> _commits = new List<Action>();
        private Label _hint;

        public SettingsForm(TrayApp app)
        {
            _app = app;
            _c = app.Cfg.Clone();
            BuildUi();
        }

        private void BuildUi()
        {
            var t = Theme.Current;
            SuspendLayout();

            Text = "轻译 · 设置";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(820, 680);   // 容得下内容最长的页面，避免出现滚动条
            ShowInTaskbar = true;
            BackColor = t.Bg;
            ForeColor = t.Text;
            Font = new Font("Microsoft YaHei UI", 9f);
            Icon = AppIcon.Get(t.Dark);
            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
            HandleCreated += delegate
            {
                Native.ApplyDarkTitleBar(Handle, Theme.Current.Dark);
                Native.ApplyRoundCorners(Handle);
            };

            // 左侧导航
            _nav = new Panel { Dock = DockStyle.Left, Width = 132, BackColor = t.Panel };
            Controls.Add(_nav);

            // 右侧内容
            _body = new Panel { Dock = DockStyle.Fill, BackColor = t.Bg, Padding = new Padding(18, 14, 18, 0) };
            Controls.Add(_body);
            _body.BringToFront();

            // 底部按钮条
            var foot = new Panel { Dock = DockStyle.Bottom, Height = 52, BackColor = t.Bg };
            _btnSave = new FlatBtn { Text = "保存", Primary = true, Size = new Size(86, 32) };
            _btnCancel = new FlatBtn { Text = "取消", Size = new Size(86, 32) };
            _hint = new Label
            {
                AutoSize = true,
                ForeColor = t.SubText,
                Location = new Point(18, 18),
                BackColor = Color.Transparent,
                Text = "配置文件：%APPDATA%" + (char)92 + "LiteTrans" + (char)92 + "config.json",
            };
            _btnSave.Click += (s, e) => SaveAndClose();
            _btnCancel.Click += (s, e) => Close();
            foot.Controls.AddRange(new Control[] { _hint, _btnSave, _btnCancel });
            foot.Resize += delegate
            {
                _btnSave.Location = new Point(foot.Width - _btnSave.Width - 18, 10);
                _btnCancel.Location = new Point(_btnSave.Left - _btnCancel.Width - 8, 10);
            };
            Controls.Add(foot);
            foot.BringToFront();

            BuildPages();

            ResumeLayout();
            foot.PerformLayout();
            _btnSave.Location = new Point(foot.Width - _btnSave.Width - 18, 10);
            _btnCancel.Location = new Point(_btnSave.Left - _btnCancel.Width - 8, 10);
            SelectTab(0);
        }

        private void BuildPages()
        {
            var names = new[] { "常规", "划词翻译", "外观", "翻译", "朗读与历史", "高级接口" };
            var builders = new Action<PageBuilder>[]
            {
                PageGeneral, PageSelection, PageAppearance, PageTranslate, PageSpeech, PageAdvanced
            };

            for (int i = 0; i < names.Length; i++)
            {
                var page = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Current.Bg };
                page.Visible = false;
                var pb = new PageBuilder(page, _c, _commits);
                builders[i](pb);
                _body.Controls.Add(page);
                _pages.Add(page);

                var btn = new FlatBtn
                {
                    Text = names[i],
                    Size = new Size(116, 34),
                    Location = new Point(8, 12 + i * 40),
                    Radius = 6,
                };
                int idx = i;
                btn.Click += (s, e) => SelectTab(idx);
                _nav.Controls.Add(btn);
                _tabs.Add(btn);
            }
        }

        /// <summary>供外部指定初始页（命令行 --page=N）</summary>
        public void GoToTab(int index)
        {
            if (index >= 0 && index < _pages.Count) SelectTab(index);
        }

        private void SelectTab(int index)
        {
            for (int i = 0; i < _pages.Count; i++)
            {
                _pages[i].Visible = i == index;
                _tabs[i].Primary = i == index;
                _tabs[i].Invalidate();
            }
            if (_pages[index] != null) _pages[index].BringToFront();
        }

        private void SaveAndClose()
        {
            if (!ValidateHotkey()) return;
            CommitEditors();

            // 开机自启需要落到注册表
            if (_c.AutoStart != Startup.IsEnabled())
            {
                var requestedAutoStart = _c.AutoStart;
                Startup.Set(requestedAutoStart);
                _c.AutoStart = Startup.IsEnabled();
                if (_c.AutoStart != requestedAutoStart)
                    MessageBox.Show("开机自启状态未能更新，可能被安全软件拦截。", "轻译",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            foreach (var f in typeof(Config).GetFields()) f.SetValue(_app.Cfg, f.GetValue(_c));
            _app.Cfg.Save();
            _app.OnConfigChanged();
            Close();
        }

        private void CommitEditors()
        {
            foreach (var a in _commits)
            {
                try { a(); } catch { }
            }
        }

        private bool ValidateHotkey()
        {
            uint m, v; Keys k;
            if (!HotkeyParser.TryParse(_c.Hotkey, out m, out v, out k))
            {
                MessageBox.Show("主快捷键无效，必须包含 Ctrl / Alt / Shift 中的至少一个，例如 Ctrl+Alt+D。",
                    "轻译", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                SelectTab(0);
                return false;
            }
            return true;
        }
    }
}
