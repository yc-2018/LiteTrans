using System;
using System.Drawing;
using System.Windows.Forms;

namespace LiteTrans
{
    public partial class MainForm
    {
        private const int Pad = 12;

        /// <summary>手工布局：原文占上 40%，译文占下 60%</summary>
        private void Relayout()
        {
            if (_bar == null || _relayouting) return;
            _relayouting = true;
            try { RelayoutCore(); }
            finally { _relayouting = false; }
        }

        private void RelayoutCore()
        {
            UpdateRegion();

            int w = ClientSize.Width, h = ClientSize.Height;
            int right = w - Pad;

            // 标题栏右侧按钮，从右往左排
            _btnClose.Location = new Point(right - _btnClose.Width, 6);
            _btnGear.Location = new Point(_btnClose.Left - _btnGear.Width - 2, 6);
            _btnPin.Location = new Point(_btnGear.Left - _btnPin.Width - 2, 6);
            _btnSwap.Location = new Point(_btnPin.Left - _btnSwap.Width - 2, 6);

            // 语言标签跟随标题实际宽度，并在交换按钮前留出缓冲区，
            // 避免“自动”的首字或较长语言名被相邻控件覆盖。
            int langLeft = _title.Right + 14;
            _langLabel.SetBounds(langLeft, 8,
                Math.Max(0, _btnSwap.Left - langLeft - 8), 24);

            int top = _bar.Height;
            int bottom = h - _foot.Height;
            int avail = bottom - top - Pad * 3;
            if (avail < 80) avail = 80;

            int srcH = MeasureSrcHeight(w - Pad * 2, avail);
            int dstH = avail - srcH;

            _srcCard.SetBounds(Pad, top + Pad, w - Pad * 2, srcH);
            _dstCard.SetBounds(Pad, _srcCard.Bottom + Pad, w - Pad * 2, dstH);

            const int inner = 10;
            _src.SetBounds(inner, inner, _srcCard.Width - inner * 2, _srcCard.Height - inner * 2);
            _dst.SetBounds(inner, inner, _dstCard.Width - inner * 2, _dstCard.Height - inner * 2);

            // 底部：按钮右对齐
            _btnTrans.Location = new Point(right - _btnTrans.Width, 7);
            _btnCopy.Location = new Point(_btnTrans.Left - _btnCopy.Width - 6, 8);
            _btnSpeak.Location = new Point(_btnCopy.Left - _btnSpeak.Width - 2, 8);

            // 左下角放置可直接切换的引擎选择器，状态信息占用剩余空间。
            _engineLabel.Location = new Point(Pad, 9);
            _enginePicker.Location = new Point(_engineLabel.Right + 6, 8);
            int statusLeft = _enginePicker.Right + 10;
            int statusRight = _btnSpeak.Left - 10;
            _status.SetBounds(statusLeft, 8, Math.Max(0, statusRight - statusLeft), 28);
        }

        /// <summary>原文只有一个单词时不该占掉四成高度，按真实内容高度收缩</summary>
        private int MeasureSrcHeight(int cardWidth, int avail)
        {
            int min = 46;
            int max = (int)(avail * (C.CompactMode ? 0.34 : 0.46));
            if (max < min) return min;

            var text = _src.Text;
            if (string.IsNullOrEmpty(text)) return min;

            // _srcContentH 来自 RichEdit 自己的排版结果，是唯一可靠的内容高度
            int h = _srcContentH > 0 ? _srcContentH + 22 : min;

            return Math.Max(min, Math.Min(h, max));
        }

        public void ApplyTheme()
        {
            var t = Theme.Current;
            BackColor = t.Bg;
            ForeColor = t.Text;

            _bar.BackColor = t.Bg;
            _foot.BackColor = t.Bg;

            foreach (Control c in _bar.Controls)
                if (c is Label) { c.ForeColor = c == _langLabel ? t.SubText : t.Text; }

            _status.ForeColor = t.SubText;

            _engineLabel.ForeColor = t.SubText;
            _enginePicker.BackColor = t.Card;
            _enginePicker.ForeColor = t.Text;
            SyncEnginePicker();

            _srcCard.BackColor = t.Panel;
            _dstCard.BackColor = t.Panel;

            var font = new Font(SafeFamily(), C.FontSize, GraphicsUnit.Point);
            _src.Font = font;
            _src.BackColor = t.Panel;
            _src.ForeColor = t.Text;

            _dst.Font = font;
            _dst.BackColor = t.Panel;
            _dst.ForeColor = t.Text;

            _btnPin.Active = C.TopMost;
            TopMost = C.TopMost;
            Opacity = Math.Max(0.4, Math.Min(1.0, C.Opacity / 100.0));

            if (IsHandleCreated)
            {
                Native.ApplyRoundCorners(Handle);
                Native.ApplyDarkTitleBar(Handle, t.Dark);
                Native.ApplyBorderColor(Handle, t.Border);
            }

            Native.ApplyScrollbarTheme(_src, t.Dark);
            Native.ApplyScrollbarTheme(_dst, t.Dark);

            Invalidate(true);
            Relayout();
            if (_last != null) RenderResult(_last);
        }

        private string SafeFamily()
        {
            try
            {
                using (var f = new Font(C.FontFamily, 10f))
                    if (f.Name.Equals(C.FontFamily, StringComparison.OrdinalIgnoreCase)) return C.FontFamily;
            }
            catch { }
            return "Microsoft YaHei UI";
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.ApplyRoundCorners(Handle);
            Native.ApplyDarkTitleBar(Handle, Theme.Current.Dark);
            Native.ApplyBorderColor(Handle, Theme.Current.Border);
            UpdateRegion();
        }

        private const float CornerRadius = 9f;

        /// <summary>DWM 的圆角画在非客户区，而这里的非客户区已被裁掉，
        /// 因此自行用 Region 裁出圆角，再补一圈抗锯齿描边遮住硬边。</summary>
        private void UpdateRegion()
        {
            if (!IsHandleCreated || Width <= 0 || Height <= 0) return;
            var old = Region;
            using (var path = AppIcon.Rounded(new RectangleF(0, 0, Width, Height), CornerRadius))
                Region = new Region(path);
            if (old != null) old.Dispose();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var path = AppIcon.Rounded(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), CornerRadius))
            using (var p = new Pen(Theme.Current.Border, 1.2f))
                g.DrawPath(p, path);
        }

        // ——— 无边框窗口：拖动与边缘缩放 ———
        private void Drag_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            Native.ReleaseCapture();
            Native.SendMessage(Handle, 0xA1 /*WM_NCLBUTTONDOWN*/, (IntPtr)2 /*HTCAPTION*/, IntPtr.Zero);
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x84;

            if (m.Msg == WM_NCHITTEST)
            {
                base.WndProc(ref m);
                if ((int)m.Result == 1 /*HTCLIENT*/)
                {
                    var p = PointToClient(new Point(m.LParam.ToInt32()));
                    const int grip = 6;
                    bool l = p.X <= grip, r = p.X >= ClientSize.Width - grip;
                    bool t = p.Y <= grip, b = p.Y >= ClientSize.Height - grip;

                    if (l && t) m.Result = (IntPtr)13;
                    else if (r && t) m.Result = (IntPtr)14;
                    else if (l && b) m.Result = (IntPtr)16;
                    else if (r && b) m.Result = (IntPtr)17;
                    else if (l) m.Result = (IntPtr)10;
                    else if (r) m.Result = (IntPtr)11;
                    else if (t) m.Result = (IntPtr)12;
                    else if (b) m.Result = (IntPtr)15;
                }
                return;
            }
            base.WndProc(ref m);
        }
    }
}
