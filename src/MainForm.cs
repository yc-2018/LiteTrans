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
        private IconBtn _btnPin, _btnGear, _btnClose;

        // 内容
        private Card _srcCard, _dstCard;
        private RichTextBox _src;
        private RichTextBox _dst;

        // 开发者命名：短英文结果时提供多种标识符格式一键复制
        private FlatBtn[] _caseBtns;
        private ToolTip _caseTip;
        private static readonly string[] CaseTips = { "小驼峰命名 · 点击复制", "大驼峰命名 · 点击复制",
                                                      "下划线小写 · 点击复制", "下划线大写 · 点击复制" };

        // 底部
        private Panel _foot;
        private Label _engineLabel;
        private ComboBox _enginePicker;
        private Label _status;
        private FlatBtn _btnTrans;
        private IconBtn _btnSpeak, _btnCopy;

        private bool _relayouting;           // 防止布局与内容重排互相触发
        private bool _relayoutQueued;        // 已经投递过一次延迟布局
        private bool _relayoutPending;       // 有布局请求在等组字结束
        private bool _imeComposing;          // 原文框正在输入法组字
        private ImeWatch _imeWatch;
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

        private const string AllEnginesKey = "all";
        private readonly List<string> _engineKeys = new List<string>();
        private static readonly string[] ModeKeys = { "auto", "forward", "reverse" };

        // 界面按 96 DPI 设计；字体用 pt 会自己随 DPI 放大，间距和控件尺寸也
        // 必须按同一倍数放大，否则高 DPI 下就是“字变大、框没变大”。
        // 构造阶段还没有窗口句柄，拿不到真实 DPI，先按 1.0 建界面，
        // OnHandleCreated 时再按实际缩放整体折算一次。
        private float _dpi = 1f;

        /// <summary>96 DPI 设计值 → 当前 DPI 的物理像素</summary>
        private int D(int px) { return (int)Math.Round(px * _dpi); }

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

        /// <summary>拖到不同缩放的显示器时，重新折算并重排一遍</summary>
        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            SyncDpi();
        }

        private void SyncDpi()
        {
            float dpi = UiScale.Factor(this);
            bool changed = Math.Abs(dpi - _dpi) > 0.01f;
            _dpi = dpi;
            if (changed) ApplyDpiScale();
        }

        /// <summary>C.WinW/WinH 存的是 96 DPI 设计值，这里按当前缩放出实际窗口尺寸</summary>
        private void ApplyWindowSize()
        {
            int w = Math.Max(D(420), D(C.WinW));
            int h = Math.Max(D(300), D(C.WinH));
            if (Width != w || Height != h) Size = new Size(w, h);
        }

        /// <summary>按当前 DPI 重新给控件定尺寸；字体是 pt，自己会跟着缩放</summary>
        private void ApplyDpiScale()
        {
            if (_bar == null) return;

            _bar.Height = D(40);
            _foot.Height = D(44);

            _title.Location = new Point(D(14), D(10));
            // 下拉框宽度由 Relayout 按最长选项实测，这里只定高度
            _modePicker.Size = new Size(_modePicker.Width, D(26));

            _btnPin.Size = _btnGear.Size = _btnClose.Size = new Size(D(32), D(28));

            _enginePicker.Size = new Size(D(126), D(26));
            _enginePicker.DropDownWidth = D(220);
            _btnTrans.Size = new Size(D(76), D(30));
            _btnSpeak.Size = _btnCopy.Size = new Size(D(32), D(28));

            _engineLabel.Location = new Point(D(12), D(13));

            MinimumSize = new Size(D(420), D(300));
            ApplyWindowSize();
            Relayout();
        }

        private void BuildUi()
        {
            SuspendLayout();
            // 尺寸完全由 D() 按 DPI 折算，关掉 WinForms 的自动缩放，
            // 否则会二次缩放：我们算出 1278x856，WinForms 再按字体 DPI 缩回 639x428。
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            MinimumSize = new Size(D(420), D(300));
            ApplyWindowSize();
            KeyPreview = true;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw, true);
            Text = "轻译 LiteTrans";

            // ——— 标题栏 ———
            _bar = new Panel { Dock = DockStyle.Top, Height = D(40) };
            _bar.MouseDown += Drag_MouseDown;

            _title = new Label
            {
                Text = "轻译",
                AutoSize = true,
                Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold),
                Location = new Point(D(14), D(10)),
                BackColor = Color.Transparent,
            };
            _title.MouseDown += Drag_MouseDown;

            _modePicker = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                IntegralHeight = false,
                Size = new Size(D(190), D(26)),
                Font = new Font("Microsoft YaHei UI", 8.5f),
                FormattingEnabled = true,
                DropDownWidth = 240,
                AccessibleName = "翻译模式",
                AccessibleDescription = "选择自动判断方向或固定译入语言",
            };
            _modePicker.SelectedIndexChanged += ModePicker_SelectedIndexChanged;

            _btnPin = new IconBtn { Kind = "pin", Tip = "窗口置顶", Size = new Size(D(32), D(28)) };
            _btnGear = new IconBtn { Kind = "gear", Tip = "设置", Size = new Size(D(32), D(28)) };
            _btnClose = new IconBtn { Kind = "close", Tip = "收回托盘 (Esc)", Size = new Size(D(32), D(28)) };
            _btnPin.Click += (s, e) =>
            {
                C.TopMost = !C.TopMost;
                TopMost = C.TopMost;
                _btnPin.Active = C.TopMost;
                C.Save();
            };
            _btnGear.Click += (s, e) => _app.ShowSettings();
            _btnClose.Click += (s, e) => HideToTray();

            _bar.Controls.AddRange(new Control[] { _title, _modePicker, _btnPin, _btnGear, _btnClose });

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
            _src.HandleCreated += delegate { AttachImeWatch(); };
            _src.HandleDestroyed += delegate
            {
                if (_imeWatch != null)
                {
                    try { _imeWatch.ReleaseHandle(); } catch { }
                    _imeWatch = null;
                }
                _imeComposing = false;
                _relayoutQueued = false;
            };
            _src.ContentsResized += delegate(object s, ContentsResizedEventArgs e)
            {
                // 不要在这条 RichEdit 通知里同步改布局：输入法组字时，控件的
                // ContentsResized 就发生在这轮组字消息内部，同步 Relayout 会
                // 重新读文本、改尺寸，直接把组字打断（候选框一闪即消、中文打不进去）。
                // 这里只登记，等消息处理完（且不在组字中）再统一重排。
                QueueRelayout();
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

            // 短英文译文的标识符格式一键复制：小驼峰 / 大驼峰 / 下划线小写 / 下划线大写
            _caseTip = new ToolTip();
            _caseBtns = new FlatBtn[4];
            for (int i = 0; i < _caseBtns.Length; i++)
            {
                var btn = new FlatBtn { Radius = D(5), Visible = false, Font = new Font("Consolas", 8.5f) };
                btn.Click += CaseChip_Click;
                _caseTip.SetToolTip(btn, CaseTips[i]);
                _caseBtns[i] = btn;
                _dstCard.Controls.Add(btn);
                btn.BringToFront();
            }

            _loadingTimer = new System.Windows.Forms.Timer { Interval = 180 };
            _loadingTimer.Tick += delegate { UpdateLoadingDisplay(); };

            // ——— 底部 ———
            _foot = new Panel { Dock = DockStyle.Bottom, Height = D(44) };
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
                Size = new Size(D(126), D(26)),
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
            _btnTrans = new FlatBtn { Text = "翻译", Primary = true, Size = new Size(D(76), D(30)) };
            _btnTrans.Click += (s, e) => TranslateNow(_src.Text);
            _btnSpeak = new IconBtn { Kind = "speak", Tip = "朗读译文", Size = new Size(D(32), D(28)) };
            _btnCopy = new IconBtn { Kind = "copy", Tip = "复制译文", Size = new Size(D(32), D(28)) };
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
            bool allEngines = engine == AllEnginesKey;
            if (_allEngines == allEngines && (allEngines || C.Engine == engine)) return;

            _allEngines = allEngines;
            C.TranslateAllEngines = allEngines;
            if (!allEngines) C.Engine = engine;
            C.Save();
            if (allEngines) EnsureAllEnginesWindowHeight();
            SetStatus(allEngines ? "已开启全部引擎翻译" : "已切换到 " + EngineName(engine));

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
                _engineKeys.Add(AllEnginesKey);
                _engineKeys.AddRange(keys);

                _enginePicker.BeginUpdate();
                _enginePicker.Items.Clear();
                foreach (var key in _engineKeys)
                    _enginePicker.Items.Add(key == AllEnginesKey ? "⭐全部翻译⭐" : Translator.EngineDisplay(key, C));
                var selected = _allEngines ? AllEnginesKey : preferred;
                int index = 0;
                for (int i = 0; i < _engineKeys.Count; i++)
                    if (string.Equals(_engineKeys[i], selected, StringComparison.OrdinalIgnoreCase)) { index = i; break; }
                if (_enginePicker.SelectedIndex != index) _enginePicker.SelectedIndex = index;
                _enginePicker.EndUpdate();
            }
            finally { _syncingEnginePicker = false; }
        }
    }
}
