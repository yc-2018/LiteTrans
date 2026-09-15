using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LiteTrans
{
    /// <summary>标题栏/工具栏用的矢量图标按钮，图形全部代码绘制</summary>
    public class IconBtn : Control
    {
        public string Kind = "close";      // close | min | gear | pin | swap | copy | speak
        public string Tip;
        private bool _hover, _down, _active;

        /// <summary>选中态（如置顶已开启）；赋值即重绘，防止界面与配置脱节</summary>
        public bool Active
        {
            get { return _active; }
            set
            {
                if (_active == value) return;
                _active = value;
                Invalidate();
            }
        }

        public IconBtn()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw | ControlStyles.UserPaint
                   | ControlStyles.SupportsTransparentBackColor, true);
            Size = new Size(32, 28);
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var t = Theme.Current;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : t.Bg);

            if (_hover || _down || Active)
            {
                var bg = Kind == "close" ? Color.FromArgb(232, 66, 58)
                       : Active ? t.Accent
                       : (_down ? t.Border : t.Hover);
                using (var path = AppIcon.Rounded(new RectangleF(1, 1, Width - 3, Height - 3), 5))
                using (var b = new SolidBrush(bg)) g.FillPath(b, path);
            }

            var fg = (Kind == "close" && (_hover || _down)) ? Color.White
                   : Active ? t.AccentText : t.Text;
            float cx = Width / 2f, cy = Height / 2f;

            using (var p = new Pen(fg, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                switch (Kind)
                {
                    case "close":
                        g.DrawLine(p, cx - 4, cy - 4, cx + 4, cy + 4);
                        g.DrawLine(p, cx + 4, cy - 4, cx - 4, cy + 4);
                        break;
                    case "min":
                        g.DrawLine(p, cx - 5, cy, cx + 5, cy);
                        break;
                    case "gear":
                        // 齿轮在 12px 内会糊成一团，改用辨识度更高的滑块图形
                        g.DrawLine(p, cx - 5.5f, cy - 3f, cx + 5.5f, cy - 3f);
                        g.DrawLine(p, cx - 5.5f, cy + 3f, cx + 5.5f, cy + 3f);
                        using (var dot = new SolidBrush(fg))
                        {
                            g.FillEllipse(dot, cx - 2.6f, cy - 5.6f, 5.2f, 5.2f);
                            g.FillEllipse(dot, cx + 0.4f, cy + 0.4f, 5.2f, 5.2f);
                        }
                        break;
                    case "pin":
                        // “置顶”语义：一条顶线加一个向上箭头
                        g.DrawLine(p, cx - 5f, cy - 5f, cx + 5f, cy - 5f);
                        g.DrawLine(p, cx, cy + 5.5f, cx, cy - 1.5f);
                        g.DrawLine(p, cx - 3.4f, cy + 1.6f, cx, cy - 1.8f);
                        g.DrawLine(p, cx + 3.4f, cy + 1.6f, cx, cy - 1.8f);
                        break;
                    case "swap":
                        g.DrawLine(p, cx - 5, cy - 2.5f, cx + 5, cy - 2.5f);
                        g.DrawLine(p, cx + 2.2f, cy - 5.5f, cx + 5, cy - 2.5f);
                        g.DrawLine(p, cx + 5, cy + 2.5f, cx - 5, cy + 2.5f);
                        g.DrawLine(p, cx - 2.2f, cy + 5.5f, cx - 5, cy + 2.5f);
                        break;
                    case "copy":
                        g.DrawRectangle(p, cx - 5.5f, cy - 5.5f, 7, 7);
                        g.DrawRectangle(p, cx - 1.5f, cy - 1.5f, 7, 7);
                        break;
                    case "speak":
                        g.DrawLine(p, cx - 4.5f, cy - 2.5f, cx - 4.5f, cy + 2.5f);
                        g.DrawLine(p, cx - 4.5f, cy - 2.5f, cx - 1f, cy - 5.5f);
                        g.DrawLine(p, cx - 4.5f, cy + 2.5f, cx - 1f, cy + 5.5f);
                        g.DrawLine(p, cx - 1f, cy - 5.5f, cx - 1f, cy + 5.5f);
                        g.DrawArc(p, cx + 0.5f, cy - 4f, 5f, 8f, -60, 120);
                        break;
                }
            }
        }
    }
}
