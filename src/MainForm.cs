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
        private Label _langLabel;
        private IconBtn _btnPin, _btnGear, _btnClose, _btnSwap;

        // 内容
        private Card _srcCard, _dstCard;
        private RichTextBox _src;
        private RichTextBox _dst;

        // 底部
        private Panel _foot;
        private Label _status;
        private FlatBtn _btnTrans;
        private IconBtn _btnSpeak, _btnCopy;

        private int _srcContentH;            // 原文实际排版高度，由 ContentsResized 报告
        private bool _relayouting;           // 防止布局与内容重排互相触发
        private int _reqSeq;                 // 请求序号，丢弃过期响应
        private TransResult _last;
        private bool _fromSelection;         // 本次内容是否来自划词抓取

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

            var title = new Label
            {
                Text = "轻译",
                AutoSize = true,
                Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold),
                Location = new Point(14, 10),
                BackColor = Color.Transparent,
            };
            title.MouseDown += Drag_MouseDown;

            _langLabel = new Label
            {
                AutoSize = true,
                Font = new Font("Microsoft YaHei UI", 8.5f),
                Location = new Point(58, 13),
                BackColor = Color.Transparent,
            };
            _langLabel.MouseDown += Drag_MouseDown;

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

            _bar.Controls.AddRange(new Control[] { title, _langLabel, _btnSwap, _btnPin, _btnGear, _btnClose });

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

            // ——— 底部 ———
            _foot = new Panel { Dock = DockStyle.Bottom, Height = 44 };
            _status = new Label
            {
                AutoSize = true,
                Font = new Font("Microsoft YaHei UI", 8.5f),
                Location = new Point(14, 15),
                BackColor = Color.Transparent,
            };
            _btnTrans = new FlatBtn { Text = "翻译", Primary = true, Size = new Size(76, 30) };
            _btnTrans.Click += (s, e) => TranslateNow(_src.Text);
            _btnSpeak = new IconBtn { Kind = "speak", Tip = "朗读译文" };
            _btnCopy = new IconBtn { Kind = "copy", Tip = "复制译文" };
            _btnSpeak.Click += (s, e) => SpeakResult();
            _btnCopy.Click += (s, e) => CopyResult();

            _foot.Controls.AddRange(new Control[] { _status, _btnTrans, _btnSpeak, _btnCopy });

            Controls.AddRange(new Control[] { _srcCard, _dstCard, _foot, _bar });

            Resize += (s, e) => Relayout();
            KeyDown += Global_KeyDown;
            Deactivate += MainForm_Deactivate;

            ResumeLayout();
            Relayout();
        }
    }
}
