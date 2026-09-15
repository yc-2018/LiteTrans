using System;
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
        private Label _langLabel;
        private IconBtn _btnPin, _btnGear, _btnClose, _btnSwap;

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
        private bool _fromSelection;         // 本次内容是否来自划词抓取
        private bool _syncingEnginePicker;
        private System.Windows.Forms.Timer _loadingTimer;
        private int _loadingFrame;

        private static readonly string[] EngineKeys = { "transmart", "ai", "baidu" };
        private static readonly string[] EngineLabels = { "腾讯翻译", "AI 精翻", "百度翻译" };

        public MainForm(TrayApp app)
        {
            _app = app;
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

            _title = new Label
            {
                Text = "轻译",
                AutoSize = true,
                Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold),
                Location = new Point(14, 10),
                BackColor = Color.Transparent,
            };
            _title.MouseDown += Drag_MouseDown;

            _langLabel = new Label
            {
                AutoSize = false,
                Font = new Font("Microsoft YaHei UI", 8.5f),
                Location = new Point(58, 13),
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
            };
            _langLabel.MouseDown += Drag_MouseDown;
            _langLabel.Padding = new Padding(2, 0, 0, 0);

            _btnSwap = new IconBtn { Kind = "swap", Tip = "切换翻译方向" };
            _btnPin = new IconBtn { Kind = "pin", Tip = "窗口置顶" };
            _btnGear = new IconBtn { Kind = "gear", Tip = "设置" };
            _btnClose = new IconBtn { Kind = "close", Tip = "收回托盘 (Esc)" };

            _btnSwap.Click += (s, e) => { SwapDirection(); };
            _btnPin.Click += (s, e) =>
            {
                C.TopMost = !C.TopMost;
                TopMost = C.TopMost;
                _btnPin.Active = C.TopMost;
                C.Save();
            };
            _btnGear.Click += (s, e) => _app.ShowSettings();
            _btnClose.Click += (s, e) => HideToTray();

            _bar.Controls.AddRange(new Control[] { _title, _langLabel, _btnSwap, _btnPin, _btnGear, _btnClose });

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
            _engineLabel = new Label
            {
                Text = "引擎",
                AutoSize = false,
                Size = new Size(38, 26),
                TextAlign = ContentAlignment.MiddleLeft,
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
            };
            for (int i = 0; i < EngineLabels.Length; i++) _enginePicker.Items.Add(EngineLabels[i]);
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
            SyncEnginePicker();
            Relayout();
        }

        private void EnginePicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_syncingEnginePicker || _enginePicker.SelectedIndex < 0 ||
                _enginePicker.SelectedIndex >= EngineKeys.Length) return;

            var engine = EngineKeys[_enginePicker.SelectedIndex];
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
                int index = 0;
                for (int i = 0; i < EngineKeys.Length; i++)
                    if (EngineKeys[i] == C.Engine) { index = i; break; }
                if (_enginePicker.SelectedIndex != index) _enginePicker.SelectedIndex = index;
            }
            finally { _syncingEnginePicker = false; }
        }
    }
}
