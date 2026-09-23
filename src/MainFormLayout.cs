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
            int w = ClientSize.Width, h = ClientSize.Height;
            int right = w - Pad;

            // 标题栏右侧按钮，从右往左排
            _btnClose.Location = new Point(right - _btnClose.Width, 6);
            _btnGear.Location = new Point(_btnClose.Left - _btnGear.Width - 2, 6);
            _btnPin.Location = new Point(_btnGear.Left - _btnPin.Width - 2, 6);
            _btnAllEngines.Location = new Point(_btnPin.Left - _btnAllEngines.Width - 2, 6);

            // 翻译模式下拉框放在标题旁，右侧按钮不再承担方向切换。
            int modeLeft = _title.Right + 12;
            int modeWidth = Math.Min(240, Math.Max(150, _btnAllEngines.Left - modeLeft - 10));
            _modePicker.SetBounds(modeLeft, 7, modeWidth, 26);

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

            int dstInnerW = _dstCard.Width - inner * 2;
            int dstInnerH = _dstCard.Height - inner * 2;
            if (CaseChipsVisible)
            {
                const int caseH = 26, gap = 6;
                _dst.SetBounds(inner, inner, dstInnerW, Math.Max(24, dstInnerH - caseH - gap));
                LayoutCaseChips(inner, _dst.Bottom + gap, dstInnerW, caseH);
            }
            else
            {
                _dst.SetBounds(inner, inner, dstInnerW, dstInnerH);
            }

            // 底部：按钮右对齐
            _btnTrans.Location = new Point(right - _btnTrans.Width, 7);
            _btnCopy.Location = new Point(_btnTrans.Left - _btnCopy.Width - 6, 8);
            _btnSpeak.Location = new Point(_btnCopy.Left - _btnSpeak.Width - 2, 8);

            // 左下角放置可直接切换的引擎选择器，状态信息占用剩余空间。
            _engineLabel.Location = new Point(Pad, 13);
            _enginePicker.Location = new Point(_engineLabel.Right + 6, 8);
            int statusLeft = _enginePicker.Right + 10;
            int statusRight = _btnSpeak.Left - 10;
            _status.SetBounds(statusLeft, 8, Math.Max(0, statusRight - statusLeft), 28);
        }

        /// <summary>把四个标识符格式按钮平分排在译文卡片底部一行。</summary>
        private void LayoutCaseChips(int x, int y, int width, int height)
        {
            int n = _caseBtns.Length;
            const int gap = 6;
            int each = (width - gap * (n - 1)) / n;
            for (int i = 0; i < n; i++)
                _caseBtns[i].SetBounds(x + i * (each + gap), y, each, height);
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
                if (c is Label) c.ForeColor = t.Text;

            _modePicker.BackColor = t.Card;
            _modePicker.ForeColor = t.Text;
            SyncModePicker();

            _allEngines = C.TranslateAllEngines;
            _btnAllEngines.Active = _allEngines;

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

            if (_caseBtns != null)
                foreach (var b in _caseBtns) b.BackColor = t.Panel;

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
            if (_lastResults != null && _allEngines)
                RenderResults(_lastResults, true);
            else if (_last != null)
                RenderResult(_last);
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
        }

        private const float CornerRadius = 9f;

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
            SetNativeChildRedraw(false);
            try
            {
                Native.ReleaseCapture();
                // 标题栏点击始终是移动窗口。边缘缩放由 WM_NCHITTEST/底栏边缘处理，
                // 不要把标题栏顶部的普通拖动误判成 HTTOP，避免拖动时反复重绘闪烁。
                Native.SendMessage(Handle, 0xA1 /*WM_NCLBUTTONDOWN*/, (IntPtr)2 /*HTCAPTION*/, IntPtr.Zero);
            }
            finally
            {
                // 系统移动循环结束后统一恢复，避免原生文本控件、光标和异步结果
                // 在快速拖动期间各自重绘，造成零星文字闪烁。
                SetNativeChildRedraw(true);
                Invalidate(true);
                Update();
            }
        }

        private void SetNativeChildRedraw(bool enabled)
        {
            var controls = new Control[] { _src, _dst, _modePicker, _enginePicker };
            foreach (var control in controls)
            {
                if (control == null || control.IsDisposed || !control.IsHandleCreated) continue;
                Native.SendMessage(control.Handle, 0x000B /*WM_SETREDRAW*/,
                    enabled ? (IntPtr)1 : IntPtr.Zero, IntPtr.Zero);
                if (enabled) control.Invalidate();
            }
        }

        private void EdgeResize_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            var control = sender as Control;
            var screenPoint = control == null ? Cursor.Position : control.PointToScreen(e.Location);
            var hit = ResizeHitTest(PointToClient(screenPoint));
            if (hit == 1) return;
            Native.ReleaseCapture();
            Native.SendMessage(Handle, 0xA1 /*WM_NCLBUTTONDOWN*/, (IntPtr)hit, IntPtr.Zero);
        }

        private void EdgeResize_MouseMove(object sender, MouseEventArgs e)
        {
            var control = sender as Control;
            if (control == null) return;
            int hit = ResizeHitTest(PointToClient(control.PointToScreen(e.Location)));
            if (hit == 12 || hit == 15) control.Cursor = Cursors.SizeNS;
            else if (hit == 13 || hit == 17) control.Cursor = Cursors.SizeNWSE;
            else if (hit == 14 || hit == 16) control.Cursor = Cursors.SizeNESW;
            else control.Cursor = Cursors.Default;
        }

        private void EdgeResize_MouseLeave(object sender, EventArgs e)
        {
            var control = sender as Control;
            if (control != null) control.Cursor = Cursors.Default;
        }

        private int ResizeHitTest(Point p)
        {
            const int grip = 9;
            const int topGrip = 3;
            // 标题栏占据窗口顶部，只有最外侧 3px 保留顶部缩放；其余区域用于移动。
            if (p.Y >= topGrip && p.Y < (_bar == null ? 40 : _bar.Height)) return 1;
            bool l = p.X <= grip, r = p.X >= ClientSize.Width - grip;
            bool t = p.Y <= grip, b = p.Y >= ClientSize.Height - grip;

            if (l && t) return 13;
            if (r && t) return 14;
            if (l && b) return 16;
            if (r && b) return 17;
            if (l) return 10;
            if (r) return 11;
            if (t) return 12;
            if (b) return 15;
            return 1;
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
                    m.Result = (IntPtr)ResizeHitTest(p);
                }
                return;
            }
            base.WndProc(ref m);
        }
    }
}
