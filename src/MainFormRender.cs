using System;
using System.Drawing;
using System.Collections.Generic;
using System.Windows.Forms;

namespace LiteTrans
{
    public partial class MainForm
    {
        private static readonly string[] LoadingFrames = { "翻译中", "翻译中 ·", "翻译中 ··", "翻译中 ···" };

        private void BeginLoadingDisplay()
        {
            _loadingActive = true;
            _loadingFrame = 0;
            if (_caseBtns != null) foreach (var b in _caseBtns) b.Visible = false;
            _dst.Clear();
            _dst.ReadOnly = true;
            _dst.SelectionColor = Theme.Current.SubText;
            _dst.SelectionFont = _dst.Font;
            _dst.Text = LoadingFrames[0];
            _loadingTimer.Start();
        }

        private void UpdateLoadingDisplay()
        {
            if (!_loadingActive || _dst == null || _dst.IsDisposed) return;
            _loadingFrame = (_loadingFrame + 1) % LoadingFrames.Length;
            _dst.SelectAll();
            _dst.SelectedText = LoadingFrames[_loadingFrame];
            _dst.SelectionStart = 0;
            _dst.SelectionLength = 0;
        }

        private void StopLoadingDisplay()
        {
            _loadingActive = false;
            if (_loadingTimer != null) _loadingTimer.Stop();
        }

        /// <summary>把译文、音标、分词性释义排进 RichTextBox，并做轻着色</summary>
        private void RenderResult(TransResult r)
        {
            var one = new List<TransResult>();
            if (r != null) one.Add(r);
            RenderResults(one, false);
        }

        /// <summary>渲染单引擎或“全部引擎”结果；每个引擎独立显示成功/失败状态。</summary>
        private void RenderResults(List<TransResult> results, bool allEngines)
        {
            var t = Theme.Current;
            _dst.Clear();
            _dst.SuspendLayout();

            var baseFont = _dst.Font;
            var small = new Font(baseFont.FontFamily, Math.Max(8f, baseFont.Size - 2.5f));
            var bold = new Font(baseFont.FontFamily, baseFont.Size, FontStyle.Bold);

            if (results == null || results.Count == 0)
            {
                Append("没有可用的翻译引擎", baseFont, Color.FromArgb(224, 82, 74));
                _dst.ResumeLayout();
                UpdateCaseChips();
                SetStatus("没有可用的翻译引擎");
                return;
            }

            int success = 0;
            for (int i = 0; i < results.Count; i++)
            {
                var r = results[i];
                if (r == null) continue;
                if (allEngines)
                {
                    if (i > 0) Append("\n\n", small, t.SubText);
                    Append("【" + EngineName(r.Engine) + "】", bold, t.Accent);
                    Append("\n", small, t.SubText);
                }

                if (!string.IsNullOrWhiteSpace(r.FallbackNotice))
                    Append("提示：" + r.FallbackNotice + "\n\n", small, t.Accent);

                if (!r.Ok)
                {
                    Append("失败：" + (r.Error ?? "翻译失败"), baseFont, Color.FromArgb(224, 82, 74));
                    continue;
                }
                success++;
                AppendResultBody(r, baseFont, small, t);
            }

            _dst.ResumeLayout();
            _dst.SelectionStart = 0;
            _dst.ScrollToCaret();

            UpdateCaseChips();

            if (allEngines)
            {
                SetStatus("全部引擎 · 成功 " + success + "/" + results.Count
                        + (_fromSelection ? " · 划词" : ""));
            }
            else
            {
                var r = results[0];
                if (r == null || !r.Ok)
                    SetStatus("失败 · " + ((r == null ? null : r.Error) ?? "翻译失败"));
                else
                    SetStatus(EngineName(r.Engine) + " · " + r.ElapsedMs + " ms"
                        + (r.Dict.Count > 0 ? " · 含词典" : "")
                        + (_fromSelection ? " · 划词" : "")
                        + (string.IsNullOrWhiteSpace(r.FallbackNotice) ? "" : " · " + r.FallbackNotice));
            }
        }

        private void AppendResultBody(TransResult r, Font baseFont, Font small, Theme t)
        {
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
            if (r.Dict != null && r.Dict.Count > 0)
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
            if (e.Control && e.KeyCode == Keys.Tab) { e.SuppressKeyPress = true; CycleTranslationMode(); return; }
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

        // ================== 开发者命名：标识符格式一键复制 ==================
        /// <summary>依据当前主结果刷新驼峰/下划线复制按钮的显示与内容。</summary>
        private void UpdateCaseChips()
        {
            if (_caseBtns == null) return;

            List<string> words = null;
            if (_last != null && _last.Ok) words = TextPrep.IdentifierWords(_last.Text);

            if (words == null)
            {
                if (CaseChipsVisible)
                {
                    foreach (var b in _caseBtns) b.Visible = false;
                    Relayout();
                }
                return;
            }

            var values = new[]
            {
                TextPrep.ToCamel(words),
                TextPrep.ToPascal(words),
                TextPrep.ToSnake(words),
                TextPrep.ToUpperSnake(words),
            };
            for (int i = 0; i < _caseBtns.Length; i++)
            {
                _caseBtns[i].Text = values[i];
                _caseBtns[i].Tag = values[i];
                _caseBtns[i].Visible = true;
            }
            Relayout();
        }

        private bool CaseChipsVisible
        {
            get { return _caseBtns != null && _caseBtns[0].Visible; }
        }

        private void CaseChip_Click(object sender, EventArgs e)
        {
            var value = (sender as Control) == null ? null : ((Control)sender).Tag as string;
            if (string.IsNullOrEmpty(value)) return;
            try
            {
                Clipboard.SetText(value);
                SetStatus("已复制 " + value);
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

        /// <summary>在自动、固定目标、固定反向三种模式间循环，并立即重译</summary>
        private void CycleTranslationMode()
        {
            var mode = C.GetTranslationMode();
            if (mode == "auto") mode = "forward";
            else if (mode == "forward") mode = "reverse";
            else mode = "auto";

            C.TranslationMode = mode;
            C.AutoSwapCJK = mode == "auto";
            C.NormalizeTranslationMode();
            C.Save();
            SyncModePicker();
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
