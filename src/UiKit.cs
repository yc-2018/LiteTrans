using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LiteTrans
{
    /// <summary>扁平圆角按钮，支持主色实心与幽灵两种样式</summary>
    public class FlatBtn : Control
    {
        public bool Primary;
        public int Radius = 6;
        private bool _hover, _down;

        public FlatBtn()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw | ControlStyles.UserPaint
                   | ControlStyles.SupportsTransparentBackColor, true);
            Cursor = Cursors.Hand;
            Size = new Size(78, 30);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var t = Theme.Current;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Color fill, fore, border;

            if (Primary)
            {
                fill = _down ? AppIcon.Darken(t.Accent, 0.08f)
                     : _hover ? AppIcon.Lighten(t.Accent, 0.06f) : t.Accent;
                fore = t.AccentText;
                border = Color.Transparent;
            }
            else
            {
                fill = _down ? t.Border : _hover ? t.Hover : t.Card;
                fore = t.Text;
                border = t.Border;
            }
            if (!Enabled) { fill = t.Card; fore = t.SubText; border = t.Border; }

            using (var path = AppIcon.Rounded(rect, Radius))
            {
                using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                if (border != Color.Transparent)
                    using (var p = new Pen(border)) g.DrawPath(p, path);
            }

            using (var sf = new StringFormat(StringFormatFlags.NoWrap)
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
            })
            using (var b = new SolidBrush(fore))
                g.DrawString(Text, Font, b, new RectangleF(0, 0, Width, Height), sf);
        }
    }

    /// <summary>细边框圆角卡片，用来包住原生文本框，使其呈现现代控件外观</summary>
    public class Card : Panel
    {
        public int Radius = 8;
        public bool Highlight;

        public Card()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            var t = Theme.Current;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : t.Bg);

            var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var path = AppIcon.Rounded(rect, Radius))
            {
                using (var b = new SolidBrush(BackColor)) g.FillPath(b, path);
                using (var p = new Pen(Highlight ? t.Accent : t.Border, Highlight ? 1.4f : 1f))
                    g.DrawPath(p, path);
            }
        }
    }
}
