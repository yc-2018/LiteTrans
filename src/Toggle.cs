using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LiteTrans
{
    /// <summary>Win11 风格开关，替代深色模式下观感很差的原生 CheckBox</summary>
    public class Toggle : Control
    {
        private bool _on, _hover;
        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get { return _on; }
            set
            {
                if (_on == value) return;
                _on = value;
                Invalidate();
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }

        public Toggle()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw | ControlStyles.UserPaint
                   | ControlStyles.SupportsTransparentBackColor, true);
            Size = new Size(40, 22);
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnClick(EventArgs e) { Checked = !Checked; base.OnClick(e); }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (Focused && (keyData == Keys.Space || keyData == Keys.Enter)) { Checked = !Checked; return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var t = Theme.Current;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : t.Bg);

            float h = Height - 2f, w = Width - 2f;
            var track = new RectangleF(1, 1, w, h);

            Color fill = _on
                ? (_hover ? AppIcon.Lighten(t.Accent, 0.08f) : t.Accent)
                : (_hover ? t.Border : t.Card);

            using (var path = AppIcon.Rounded(track, h / 2f))
            {
                using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                if (!_on)
                    using (var p = new Pen(t.SubText, 1f)) g.DrawPath(p, path);
            }

            float knob = h - 7f;
            float x = _on ? (track.Right - knob - 3.5f) : (track.Left + 3.5f);
            var knobColor = _on ? Color.White : t.SubText;
            using (var b = new SolidBrush(knobColor))
                g.FillEllipse(b, x, track.Top + 3.5f, knob, knob);

            if (Focused)
            {
                using (var p = new Pen(t.Accent, 1f) { DashStyle = DashStyle.Dot })
                using (var path = AppIcon.Rounded(new RectangleF(0, 0, Width - 1, Height - 1), (Height - 1) / 2f))
                    g.DrawPath(p, path);
            }
        }
    }
}
