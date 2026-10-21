using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LiteTrans
{
    /// <summary>标题栏/工具栏用的矢量图标按钮，图形全部代码绘制</summary>
    public class IconBtn : Control
    {
        public string Kind = "close";      // close | min | gear | pin | swap | all | copy | speak
        public string Tip;
        private bool _hover, _down, _active;
        private ToolTip _toolTip;

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

        protected override void OnCreateControl()
        {
            base.OnCreateControl();
            if (!string.IsNullOrWhiteSpace(Tip))
            {
                _toolTip = new ToolTip();
                _toolTip.SetToolTip(this, Tip);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _toolTip != null) _toolTip.Dispose();
            base.Dispose(disposing);
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

            // 图标几何按 32x28 的设计稿书写，绘制时按控件实际大小整体缩放。
            // 否则高 DPI 下按钮放大了，图形还是那几个像素，看着只有按钮的一小块。
            float k = Math.Min(Width / 32f, Height / 28f);
            if (k <= 0f) k = 1f;

            if (_hover || _down || Active)
            {
                var bg = Kind == "close" ? Color.FromArgb(232, 66, 58)
                       : Active ? t.Accent
                       : (_down ? t.Border : t.Hover);
                float inset = 1f * k;
                using (var path = AppIcon.Rounded(
                    new RectangleF(inset, inset, Width - inset * 3, Height - inset * 3), 5f * k))
                using (var b = new SolidBrush(bg)) g.FillPath(b, path);
            }

            var fg = (Kind == "close" && (_hover || _down)) ? Color.White
                   : Active ? t.AccentText : t.Text;

            var state = g.Save();
            g.TranslateTransform(Width / 2f, Height / 2f);
            // 图形按 32x28 画稿书写，这里整体缩放。1.25 让图标约占按钮高度
            // 的六成，既不再发空，也不会顶到按钮边框。
            g.ScaleTransform(k * 1.25f, k * 1.25f);

            using (var p = new Pen(fg, 1.1f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                switch (Kind)
                {
                    case "close":
                        g.DrawLine(p, -4, -4, 4, 4);
                        g.DrawLine(p, 4, -4, -4, 4);
                        break;
                    case "min":
                        g.DrawLine(p, -5, 0, 5, 0);
                        break;
                    case "gear":
                        // 齿轮在 12px 内会糊成一团，改用辨识度更高的滑块图形
                        g.DrawLine(p, -5.5f, -3f, 5.5f, -3f);
                        g.DrawLine(p, -5.5f, 3f, 5.5f, 3f);
                        using (var dot = new SolidBrush(fg))
                        {
                            g.FillEllipse(dot, -2.6f, -5.6f, 5.2f, 5.2f);
                            g.FillEllipse(dot, 0.4f, 0.4f, 5.2f, 5.2f);
                        }
                        break;
                    case "pin":
                        // 图钉轮廓：上方圆头 + 下方针尖，比线条组合更易辨认
                        using (var pin = new GraphicsPath())
                        {
                            pin.AddLine(-4.6f, -4.8f, 4.6f, -4.8f);
                            pin.AddLine(4.6f, -4.8f, 2.4f, -0.8f);
                            pin.AddLine(2.4f, -0.8f, 2.4f, 1.6f);
                            pin.AddLine(2.4f, 1.6f, -2.4f, 1.6f);
                            pin.AddLine(-2.4f, 1.6f, -2.4f, -0.8f);
                            pin.AddLine(-2.4f, -0.8f, -4.6f, -4.8f);
                            pin.CloseFigure();
                            g.FillPath(new SolidBrush(fg), pin);
                        }
                        g.DrawLine(p, 0, 1.6f, 0, 5.0f);
                        break;
                    case "swap":
                        g.DrawLine(p, -5, -2.5f, 5, -2.5f);
                        g.DrawLine(p, 2.2f, -5.5f, 5, -2.5f);
                        g.DrawLine(p, 5, 2.5f, -5, 2.5f);
                        g.DrawLine(p, -2.2f, 5.5f, -5, 2.5f);
                        break;
                    case "all":
                        // 多引擎：三条等长结果线，保持标题栏图标清晰。
                        g.DrawLine(p, -5, -4.5f, 5, -4.5f);
                        g.DrawLine(p, -5, 0, 5, 0);
                        g.DrawLine(p, -5, 4.5f, 5, 4.5f);
                        break;
                    case "copy":
                        g.DrawRectangle(p, -5.5f, -5.5f, 7, 7);
                        g.DrawRectangle(p, -1.5f, -1.5f, 7, 7);
                        break;
                    case "speak":
                        g.DrawLine(p, -4.5f, -2.5f, -4.5f, 2.5f);
                        g.DrawLine(p, -4.5f, -2.5f, -1f, -5.5f);
                        g.DrawLine(p, -4.5f, 2.5f, -1f, 5.5f);
                        g.DrawLine(p, -1f, -5.5f, -1f, 5.5f);
                        g.DrawArc(p, 0.5f, -4f, 5f, 8f, -60, 120);
                        break;
                }
            }

            g.Restore(state);
        }
    }
}
