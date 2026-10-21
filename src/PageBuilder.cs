using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace LiteTrans
{
    /// <summary>按行堆叠的设置表单构建器，控件与 Config 字段通过反射双向绑定</summary>
    public class PageBuilder
    {
        private readonly Panel _p;
        private readonly Config _c;
        private readonly List<Action> _commits;
        private int _y;

        // 宽度跟随页面变化的控件（说明文字、AI 卡片区等），避免窗口变窄时右侧被裁
        private readonly List<Control> _fluid = new List<Control>();
        private int _contentW;

        private readonly float _dpi;

        // 以下都是 96 DPI 的设计值，实际使用时经 D() 折算
        private const int LabelW = 178;
        private const int CtrlX = 190;
        private const int RowH = 34;
        private const int TextRowH = 40;
        /// <summary>按最窄情形测量说明文字高度，窗口变宽后只会多出留白，不会截断尾部</summary>
        private const int NarrowW = 460;

        private int D(int px) { return (int)Math.Round(px * _dpi); }

        /// <summary>供外部（AI 配置卡片等）复用同一套 DPI 折算</summary>
        internal int Dpi(int px) { return D(px); }

        internal float DpiFactor { get { return _dpi; } }

        public PageBuilder(Panel panel, Config cfg, List<Action> commits)
        {
            _p = panel; _c = cfg; _commits = commits;
            _dpi = UiScale.Factor(panel);
            _y = D(4);
            _contentW = D(596);
        }

        public int ContentWidth { get { return _contentW; } }

        /// <summary>页面宽度变化时调用，让可伸缩控件跟着改宽</summary>
        public event Action<int> WidthChanged;

        public bool SetContentWidth(int width)
        {
            if (width < D(420)) width = D(420);
            if (width == _contentW) return false;
            _contentW = width;
            foreach (var c in _fluid) c.Width = width;
            if (WidthChanged != null) WidthChanged(width);
            return true;
        }

        /// <summary>内容总高度，供页面设置滚动范围用</summary>
        public int ContentBottom
        {
            get
            {
                int bottom = _y;
                foreach (Control c in _p.Controls)
                    if (c.Bottom + D(8) > bottom) bottom = c.Bottom + D(8);
                return bottom;
            }
        }

        /// <summary>给复杂设置控件预留一块可滚动区域。</summary>
        public void AddControl(Control control, int height, int margin = 10)
        {
            if (control == null) return;
            control.Location = new Point(0, _y);
            control.Width = _contentW;
            control.Height = height;
            _p.Controls.Add(control);
            _fluid.Add(control);
            _y += height + D(margin);
        }

        public void AddCommit(Action commit)
        {
            if (commit != null) _commits.Add(commit);
        }

        private FieldInfo F(string name)
        {
            var f = typeof(Config).GetField(name);
            if (f == null) throw new ArgumentException("配置字段不存在: " + name);
            return f;
        }

        private Label MakeLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                Size = new Size(D(LabelW), D(22)),
                Location = new Point(0, _y + D(3)),
                ForeColor = Theme.Current.Text,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
            };
        }

        public void Section(string title)
        {
            if (_y > D(4)) _y += D(10);
            var l = new Label
            {
                Text = title,
                AutoSize = true,
                Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold),
                ForeColor = Theme.Current.Accent,
                Location = new Point(0, _y),
                BackColor = Color.Transparent,
            };
            _p.Controls.Add(l);
            _y += D(28);
        }

        public void Note(string text)
        {
            var font = new Font("Microsoft YaHei UI", 8.5f);

            // 按文字实际需要的行数给高度，避免长说明被裁掉尾部
            int h;
            try
            {
                h = TextRenderer.MeasureText(text, font,
                        new Size(D(NarrowW), int.MaxValue), TextFormatFlags.WordBreak).Height + D(4);
            }
            catch { h = D(34); }
            if (h < D(20)) h = D(20);

            var l = new Label
            {
                Text = text,
                AutoSize = false,
                Size = new Size(_contentW, h),
                Location = new Point(0, _y),
                ForeColor = Theme.Current.SubText,
                BackColor = Color.Transparent,
                Font = font,
            };
            _p.Controls.Add(l);
            _fluid.Add(l);
            _y += h + D(10);
        }

        /// <summary>带一个可点击链接的说明行</summary>
        public void LinkNote(string before, string linkText, string after, string url)
        {
            var text = (before ?? "") + (linkText ?? "") + (after ?? "");
            var font = new Font("Microsoft YaHei UI", 8.5f);

            int h;
            try
            {
                h = TextRenderer.MeasureText(text, font,
                        new Size(D(NarrowW), int.MaxValue), TextFormatFlags.WordBreak).Height + D(4);
            }
            catch { h = D(34); }
            if (h < D(20)) h = D(20);

            var l = new LinkLabel
            {
                Text = text,
                AutoSize = false,
                Size = new Size(_contentW, h),
                Location = new Point(0, _y),
                ForeColor = Theme.Current.SubText,
                LinkColor = Theme.Current.Accent,
                ActiveLinkColor = Theme.Current.Accent,
                VisitedLinkColor = Theme.Current.Accent,
                BackColor = Color.Transparent,
                Font = font,
                LinkBehavior = LinkBehavior.HoverUnderline,
                LinkArea = new LinkArea((before ?? "").Length, (linkText ?? "").Length),
                TabStop = true,
            };
            l.LinkClicked += delegate
            {
                try
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                catch
                {
                    try { Process.Start("explorer.exe", url); }
                    catch { MessageBox.Show("无法打开链接：" + url, "轻译", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                }
            };
            _p.Controls.Add(l);
            _fluid.Add(l);
            _y += h + D(10);
        }

        public Toggle Switch(string label, string field, string desc = null)
        {
            var fi = F(field);
            _p.Controls.Add(MakeLabel(label));

            var tg = new Toggle
            {
                Size = new Size(D(40), D(22)),
                Location = new Point(D(CtrlX), _y + D(3)),
                Checked = (bool)fi.GetValue(_c),
            };
            _p.Controls.Add(tg);
            _commits.Add(delegate { fi.SetValue(_c, tg.Checked); });

            if (!string.IsNullOrEmpty(desc))
            {
                _p.Controls.Add(new Label
                {
                    Text = desc,
                    AutoSize = false,
                    Size = new Size(D(355), D(22)),
                    Location = new Point(D(CtrlX) + D(52), _y + D(6)),
                    ForeColor = Theme.Current.SubText,
                    BackColor = Color.Transparent,
                    Font = new Font("Microsoft YaHei UI", 8.5f),
                });
            }
            _y += D(RowH);
            return tg;
        }

        public ComboBox Combo(string label, string field, IList<KeyValuePair<string, string>> options, int width = 190)
        {
            var fi = F(field);
            _p.Controls.Add(MakeLabel(label));

            var cb = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(D(CtrlX), _y + D(1)),
                Width = D(width),
                BackColor = Theme.Current.Card,
                ForeColor = Theme.Current.Text,
                Font = new Font("Microsoft YaHei UI", 9f),
            };

            var cur = Convert.ToString(fi.GetValue(_c));
            foreach (var kv in options) cb.Items.Add(kv.Value);
            for (int i = 0; i < options.Count; i++)
                if (options[i].Key == cur) { cb.SelectedIndex = i; break; }
            if (cb.SelectedIndex < 0 && cb.Items.Count > 0) cb.SelectedIndex = 0;

            _p.Controls.Add(cb);
            _commits.Add(delegate
            {
                if (cb.SelectedIndex >= 0) fi.SetValue(_c, options[cb.SelectedIndex].Key);
            });
            _y += D(RowH);
            return cb;
        }

        public TextBox TextField(string label, string field, int width = 300, bool password = false)
        {
            var fi = F(field);
            _p.Controls.Add(MakeLabel(label));

            var card = new Card { Location = new Point(D(CtrlX), _y), Size = new Size(D(width), D(30)), BackColor = Theme.Current.Card, Radius = D(6) };
            var tb = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Location = new Point(D(8), D(6)),
                Width = D(width) - D(16),
                Text = Convert.ToString(fi.GetValue(_c)),
                BackColor = Theme.Current.Card,
                ForeColor = Theme.Current.Text,
                Font = new Font("Microsoft YaHei UI", 9f),
                UseSystemPasswordChar = password,
            };
            tb.Enter += delegate { card.Highlight = true; card.Invalidate(); };
            tb.Leave += delegate { card.Highlight = false; card.Invalidate(); };
            card.Controls.Add(tb);
            _p.Controls.Add(card);

            _commits.Add(delegate
            {
                if (fi.FieldType == typeof(int))
                {
                    int v; if (int.TryParse(tb.Text.Trim(), out v)) fi.SetValue(_c, v);
                }
                else fi.SetValue(_c, tb.Text.Trim());
            });
            _y += D(TextRowH);
            return tb;
        }

        public void Slider(string label, string field, int min, int max, string suffix = "")
        {
            var fi = F(field);
            _p.Controls.Add(MakeLabel(label));

            var val = new Label
            {
                AutoSize = false,
                Size = new Size(D(60), D(22)),
                Location = new Point(D(CtrlX) + D(210), _y + D(3)),
                ForeColor = Theme.Current.SubText,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
            };

            int cur = Convert.ToInt32(fi.GetValue(_c));
            if (cur < min) cur = min;
            if (cur > max) cur = max;

            var bar = new TrackBar
            {
                Location = new Point(D(CtrlX) - D(6), _y - D(2)),
                Width = D(210),
                Minimum = min,
                Maximum = max,
                Value = cur,
                TickStyle = TickStyle.None,
                BackColor = Theme.Current.Bg,
            };
            // TrackBar 默认高 45px，会压住下一行控件，必须显式收窄
            bar.AutoSize = false;
            bar.Height = D(26);
            bar.ValueChanged += delegate { val.Text = bar.Value + suffix; };
            val.Text = bar.Value + suffix;

            _p.Controls.Add(bar);
            _p.Controls.Add(val);
            _commits.Add(delegate { fi.SetValue(_c, bar.Value); });
            _y += D(RowH) + D(4);
        }

        /// <summary>快捷键录制框：点一下再按组合键即可</summary>
        public void HotkeyField(string label, string field, bool allowEmpty = false)
        {
            var fi = F(field);
            _p.Controls.Add(MakeLabel(label));

            var card = new Card { Location = new Point(D(CtrlX), _y), Size = new Size(D(190), D(28)), BackColor = Theme.Current.Card, Radius = D(6) };
            var tb = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Location = new Point(D(8), D(5)),
                Width = D(174),
                ReadOnly = true,
                Text = Convert.ToString(fi.GetValue(_c)),
                BackColor = Theme.Current.Card,
                ForeColor = Theme.Current.Text,
                Font = new Font("Microsoft YaHei UI", 9f),
                Cursor = Cursors.Hand,
            };

            string captured = tb.Text;
            bool recording = false;
            if (string.IsNullOrEmpty(captured)) tb.Text = "（未设置）";

            tb.Enter += delegate { recording = true; card.Highlight = true; card.Invalidate(); tb.Text = "请按下组合键…"; };
            tb.Leave += delegate
            {
                recording = false; card.Highlight = false; card.Invalidate();
                tb.Text = string.IsNullOrEmpty(captured) ? "（未设置）" : captured;
            };

            tb.KeyDown += delegate(object s, KeyEventArgs e)
            {
                e.SuppressKeyPress = true;
                if (!recording) return;

                if (e.KeyCode == Keys.Escape)
                {
                    tb.Text = string.IsNullOrEmpty(captured) ? "（未设置）" : captured;
                    _p.Focus();
                    return;
                }
                if (allowEmpty && (e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back))
                {
                    captured = ""; tb.Text = "（未设置）"; return;
                }

                var text = HotkeyParser.ToText(e.KeyData);
                if (text != null) { captured = text; tb.Text = text; }
            };

            card.Controls.Add(tb);
            _p.Controls.Add(card);

            var tip = new Label
            {
                Text = "点击后按下组合键" + (allowEmpty ? "，Delete 清除" : ""),
                AutoSize = true,
                Location = new Point(D(CtrlX) + D(200), _y + D(6)),
                ForeColor = Theme.Current.SubText,
                BackColor = Color.Transparent,
                Font = new Font("Microsoft YaHei UI", 8.5f),
            };
            _p.Controls.Add(tip);

            _commits.Add(delegate { fi.SetValue(_c, captured); });
            _y += D(RowH);
        }

        public void ButtonRow(string label, string btnText, EventHandler onClick, int width = 130)
        {
            if (!string.IsNullOrEmpty(label)) _p.Controls.Add(MakeLabel(label));
            var b = new FlatBtn { Text = btnText, Size = new Size(D(width), D(29)), Location = new Point(D(CtrlX), _y) };
            b.Click += onClick;
            _p.Controls.Add(b);
            _y += D(RowH) + D(2);
        }
    }
}
