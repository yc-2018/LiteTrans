using System;
using System.Drawing;
using System.Windows.Forms;

namespace LiteTrans
{
    public partial class MainForm
    {
        /// <summary>把译文、音标、分词性释义排进 RichTextBox，并做轻着色</summary>
        private void RenderResult(TransResult r)
        {
            var t = Theme.Current;
            _dst.Clear();
            _dst.SuspendLayout();

            var baseFont = _dst.Font;
            var small = new Font(baseFont.FontFamily, Math.Max(8f, baseFont.Size - 2.5f));
            var bold = new Font(baseFont.FontFamily, baseFont.Size, FontStyle.Bold);

            if (!r.Ok)
            {
                Append(r.Error ?? "翻译失败", baseFont, Color.FromArgb(224, 82, 74));
                SetStatus("失败 · " + (r.Error ?? ""));
                _dst.ResumeLayout();
                return;
            }

            // 音标
            if (!string.IsNullOrEmpty(r.PhoneticUk) || !string.IsNullOrEmpty(r.PhoneticUs))
            {
                if (!string.IsNullOrEmpty(r.PhoneticUk))
                    Append("英 " + r.PhoneticUk + "   ", small, t.SubText);
                if (!string.IsNullOrEmpty(r.PhoneticUs))
                    Append("美 " + r.PhoneticUs, small, t.SubText);
                Append("\n", small, t.SubText);
            }

            // 主译文
            Append(r.Text, baseFont, t.Text);

            // 词典释义
            if (r.Dict.Count > 0)
            {
                Append("\n\n", small, t.SubText);
                Append("词典释义\n", small, t.Accent);
                foreach (var d in r.Dict)
                {
                    if (!string.IsNullOrWhiteSpace(d.Pos))
                        Append("  " + d.Pos.PadRight(6), small, t.Accent);
                    else
                        Append("  · ", small, t.Accent);
                    Append(d.Mean + "\n", small, t.Text);
                }
            }

            _dst.ResumeLayout();
            _dst.SelectionStart = 0;
            _dst.ScrollToCaret();

            _langLabel.Text = Lang.Detected(r.SrcLang) + "  →  " + Lang.DisplayName(r.TgtLang);
            SetStatus(EngineName(r.Engine) + " · " + r.ElapsedMs + " ms"
                    + (r.Dict.Count > 0 ? " · 含词典" : "")
                    + (_fromSelection ? " · 划词" : ""));
        }

        private void Append(string text, Font font, Color color)
        {
            _dst.SelectionStart = _dst.TextLength;
            _dst.SelectionLength = 0;
            _dst.SelectionFont = font;
            _dst.SelectionColor = color;
            _dst.AppendText(text);
        }

        // ================== 交互 ==================
        private void Src_KeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+Enter 翻译；Enter 在多行框里保持换行
            if (e.Control && e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                TranslateNow(_src.Text);
                return;
            }
            if (e.Control && e.KeyCode == Keys.A)
            {
                e.SuppressKeyPress = true;
                _src.SelectAll();
                return;
            }
            Global_KeyDown(sender, e);
        }

        private void Global_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape && C.EscToTray)
            {
                e.SuppressKeyPress = true;
                HideToTray();
                return;
            }
            if (e.Control && e.Shift && e.KeyCode == Keys.C) { e.SuppressKeyPress = true; CopyResult(); return; }
            if (e.Control && e.KeyCode == Keys.D) { e.SuppressKeyPress = true; SpeakResult(); return; }
            if (e.Control && e.KeyCode == Keys.Oemcomma) { e.SuppressKeyPress = true; _app.ShowSettings(); return; }
            if (e.Control && e.KeyCode == Keys.Tab) { e.SuppressKeyPress = true; SwapDirection(); return; }
        }

        private void MainForm_Deactivate(object sender, EventArgs e)
        {
            // 设置窗口打开时不算失焦
            if (C.HideOnFocusLost && Visible && !_app.SettingsOpen) HideToTray();
        }

        public void CopyResult()
        {
            if (_last == null || !_last.Ok) return;
            try
            {
                Clipboard.SetText(_last.Text);
                SetStatus("译文已复制");
            }
            catch { SetStatus("复制失败，剪贴板被占用"); }
        }

        public void SpeakResult()
        {
            if (_last == null || !_last.Ok) { SetStatus("没有可朗读的内容"); return; }
            var text = TextPrep.IsSingleWord(_last.Source) ? _last.Source : _last.Text;
            Speech.Speak(text, C);
            SetStatus("朗读中…");
        }

        /// <summary>互换主/备目标语言，并立即重译</summary>
        private void SwapDirection()
        {
            var tmp = C.TargetLang;
            C.TargetLang = C.PivotLang;
            C.PivotLang = tmp;
            C.Save();
            _langLabel.Text = "→ " + Lang.DisplayName(C.TargetLang);
            if (!string.IsNullOrWhiteSpace(_src.Text)) TranslateNow(_src.Text);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape && C.EscToTray) { HideToTray(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // 点关闭一律收回托盘，真正退出走托盘菜单
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HideToTray();
                return;
            }
            base.OnFormClosing(e);
        }
    }
}
