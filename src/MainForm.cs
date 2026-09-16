using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace LiteTrans
{
    public partial class MainForm : Form
    {
        private readonly TrayApp _app;
        private Config C { get { return _app.Cfg; } }

        // 标题栏
        private Panel _bar;
        private Label _title;
        private ComboBox _modePicker;
        private IconBtn _btnAllEngines, _btnPin, _btnGear, _btnClose;

        // 内容
        private Card _srcCard, _dstCard;
        private RichTextBox _src;
        private RichTextBox _dst;

        // 底部
        private Panel _foot;
        private Label _engineLabel;
        private ComboBox _enginePicker;
        private Label _status;
        private FlatBtn _btnTrans;
        private IconBtn _btnSpeak, _btnCopy;

        private int _srcContentH;            // 原文实际排版高度，由 ContentsResized 报告
        private bool _relayouting;           // 防止布局与内容重排互相触发
        private int _reqSeq;                 // 请求序号，丢弃过期响应
        private TransResult _last;
        private List<TransResult> _lastResults;
        private bool _fromSelection;         // 本次内容是否来自划词抓取
        private bool _allEngines;
        private bool _syncingModePicker;
        private bool _syncingEnginePicker;
        private System.Windows.Forms.Timer _loadingTimer;
        private int _loadingFrame;
        private bool _loadingActive;

        private readonly List<string> _engineKeys = new List<string>();
        private static readonly string[] ModeKeys = { "auto", "forward", "reverse" };

        public MainForm(TrayApp app)
        {
            _app = app;
            _allEngines = C.TranslateAllEngines;
            BuildUi();
            ApplyTheme();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= 0x20000;        // CS_DROPSHADOW：给无边框窗口一点投影
                return cp;
            }
        }

        private void BuildUi()
        {
            SuspendLayout();
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            MinimumSize = new Size(420, 300);
            Size = new Size(C.WinW, C.WinH);
            KeyPreview = true;
            DoubleBuffered = true;
            Text = "轻译 LiteTrans";

            // ——— 标题栏 ———
            _bar = new Panel { Dock = DockStyle.Top, Height = 40 };
            _bar.MouseDown += Drag_MouseDown;
            _bar.MouseMove += EdgeResize_MouseMove;
            _bar.MouseLeave += EdgeResize_MouseLeave;

            _title = new Label
            {
                Text = "轻译",
                AutoSize = true,
                Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold),
                Location = new Point(14, 10),
                BackColor = Color.Transparent,
            };
            _title.MouseDown += Drag_MouseDown;

            _modePicker = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                IntegralHeight = false,
                Size = new Size(190, 26),
                Font = new Font("Microsoft YaHei UI", 8.5f),
                FormattingEnabled = true,
                DropDownWidth = 240,
                AccessibleName = "翻译模式",
                AccessibleDescription = "选择自动判断方向或固定译入语言",
            };
            _modePicker.SelectedIndexChanged += ModePicker_SelectedIndexChanged;

            _btnPin = new IconBtn { Kind = "pin", Tip = "窗口置顶" };
            _btnGear = new IconBtn { Kind = "gear", Tip = "设置" };
            _btnClose = new IconBtn { Kind = "close", Tip = "收回托盘 (Esc)" };
            _btnAllEngines = new IconBtn { Kind = "all", Tip = "全部已配置引擎翻译" };

            _btnAllEngines.Click += (s, e) =>
            {
                _allEngines = !_allEngines;
                C.TranslateAllEngines = _allEngines;
                C.Save();
                _btnAllEngines.Active = _allEngines;
                if (_allEngines) EnsureAllEnginesWindowHeight();
                SetStatus(_allEngines ? "已开启全部引擎翻译" : "已切换为首选引擎翻译");
                if (!string.IsNullOrWhiteSpace(_src.Text)) TranslateNow(_src.Text);
            };

            _btnPin.Click += (s, e) =>
            {
                C.TopMost = !C.TopMost;
                TopMost = C.TopMost;
                _btnPin.Active = C.TopMost;
                C.Save();
            };
            _btnGear.Click += (s, e) => _app.ShowSettings();
            _btnClose.Click += (s, e) => HideToTray();

            _bar.Controls.AddRange(new Control[] { _title, _modePicker, _btnAllEngines, _btnPin, _btnGear, _btnClose });

            // ——— 原文 ———
            _srcCard = new Card();
            _src = new RichTextBox
            {
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical,   // 仅在内容超出时出现
                AcceptsTab = false,
                DetectUrls = false,
                ShortcutsEnabled = true,
            };
            _src.KeyDown += Src_KeyDown;
            _src.ContentsResized += delegate(object s, ContentsResizedEventArgs e)
            {
                _srcContentH = e.NewRectangle.Height;
                Relayout();
            };
            _srcCard.Controls.Add(_src);

            // ——— 译文 ———
            _dstCard = new Card();
            _dst = new RichTextBox
            {
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                DetectUrls = false,
                ScrollBars = RichTextBoxScrollBars.Vertical,
            };
            _dst.KeyDown += Global_KeyDown;
            _dstCard.Controls.Add(_dst);
            _loadingTimer = new System.Windows.Forms.Timer { Interval = 180 };
            _loadingTimer.Tick += delegate { UpdateLoadingDisplay(); };

            // ——— 底部 ———
            _foot = new Panel { Dock = DockStyle.Bottom, Height = 44 };
            _foot.MouseDown += EdgeResize_MouseDown;
            _foot.MouseMove += EdgeResize_MouseMove;
            _foot.MouseLeave += EdgeResize_MouseLeave;
            _engineLabel = new Label
            {
                Text = "引擎",
                AutoSize = true,
                BackColor = Color.Transparent,
                Font = new Font("Microsoft YaHei UI", 8.5f),
            };
            _enginePicker = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                IntegralHeight = false,
                Size = new Size(126, 26),
                Font = new Font("Microsoft YaHei UI", 8.5f),
                FormattingEnabled = true,
                DropDownWidth = 220,
            };
            _enginePicker.SelectedIndexChanged += EnginePicker_SelectedIndexChanged;

            _status = new Label
            {
                AutoSize = false,
                Font = new Font("Microsoft YaHei UI", 8.5f),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                BackColor = Color.Transparent,
            };
            _btnTrans = new FlatBtn { Text = "翻译", Primary = true, Size = new Size(76, 30) };
            _btnTrans.Click += (s, e) => TranslateNow(_src.Text);
            _btnSpeak = new IconBtn { Kind = "speak", Tip = "朗读译文" };
            _btnCopy = new IconBtn { Kind = "copy", Tip = "复制译文" };
            _btnSpeak.Click += (s, e) => SpeakResult();
            _btnCopy.Click += (s, e) => CopyResult();

            _foot.Controls.AddRange(new Control[] { _engineLabel, _enginePicker, _status, _btnTrans, _btnSpeak, _btnCopy });

            Controls.AddRange(new Control[] { _srcCard, _dstCard, _foot, _bar });

            Resize += (s, e) => Relayout();
            KeyDown += Global_KeyDown;
            Deactivate += MainForm_Deactivate;

            ResumeLayout();
            SyncModePicker();
            SyncEnginePicker();
            Relayout();
        }

        private void ModePicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_syncingModePicker || _modePicker.SelectedIndex < 0 ||
                _modePicker.SelectedIndex >= ModeKeys.Length) return;

            C.TranslationMode = ModeKeys[_modePicker.SelectedIndex];
            C.AutoSwapCJK = C.TranslationMode == "auto";
            C.NormalizeTranslationMode();
            C.Save();
            SyncModePicker();
            SetStatus("已切换到 " + _modePicker.Text);
            if (!string.IsNullOrWhiteSpace(_src.Text)) TranslateNow(_src.Text);
        }

        private void SyncModePicker()
        {
            if (_modePicker == null) return;
            _syncingModePicker = true;
            try
            {
                var target = Lang.DisplayName(C.TargetLang);
                var pivot = Lang.DisplayName(C.PivotLang);
                _modePicker.BeginUpdate();
                _modePicker.Items.Clear();
                _modePicker.Items.Add("自动判断方向");
                _modePicker.Items.Add("固定译成 " + target);
                _modePicker.Items.Add("固定译成 " + pivot);
                var mode = C.GetTranslationMode();
                int index = 0;
                for (int i = 0; i < ModeKeys.Length; i++)
                    if (ModeKeys[i] == mode) { index = i; break; }
                _modePicker.SelectedIndex = index;
                _modePicker.EndUpdate();
            }
            finally { _syncingModePicker = false; }
        }

        private void EnginePicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_syncingEnginePicker || _enginePicker.SelectedIndex < 0 ||
                _enginePicker.SelectedIndex >= _engineKeys.Count) return;

            var engine = _engineKeys[_enginePicker.SelectedIndex];
            if (C.Engine == engine) return;

            C.Engine = engine;
            C.Save();
            SetStatus("已切换到 " + EngineName(engine));

            // 已有原文时立即重译，让下拉框的切换结果可见。
            if (!string.IsNullOrWhiteSpace(_src.Text)) TranslateNow(_src.Text);
        }

        private void SyncEnginePicker()
        {
            if (_enginePicker == null) return;
            _syncingEnginePicker = true;
            try
            {
                C.NormalizeAiProviders();
                var preferred = C.Engine;
                if (string.Equals(preferred, "ai", StringComparison.OrdinalIgnoreCase))
                    preferred = Config.AiEngineKey(C.FindAiProvider("ai"));
                if (string.IsNullOrWhiteSpace(preferred)) preferred = "transmart";

                var keys = Translator.GetConfiguredEngineKeys(C);
                bool hasPreferred = false;
                foreach (var key in keys)
                    if (string.Equals(key, preferred, StringComparison.OrdinalIgnoreCase)) { hasPreferred = true; break; }
                if (!hasPreferred) keys.Add(preferred);
                _engineKeys.Clear();
                _engineKeys.AddRange(keys);

                _enginePicker.BeginUpdate();
                _enginePicker.Items.Clear();
                foreach (var key in _engineKeys)
                    _enginePicker.Items.Add(Translator.EngineDisplay(key, C));
                int index = 0;
                for (int i = 0; i < _engineKeys.Count; i++)
                    if (string.Equals(_engineKeys[i], preferred, StringComparison.OrdinalIgnoreCase)) { index = i; break; }
                if (_enginePicker.SelectedIndex != index) _enginePicker.SelectedIndex = index;
                _enginePicker.EndUpdate();
            }
            finally { _syncingEnginePicker = false; }
        }
    }
}
