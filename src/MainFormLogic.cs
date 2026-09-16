using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace LiteTrans
{
    public partial class MainForm
    {
        /// <summary>热键/托盘唤起：定位窗口 → 取剪贴板 → 立刻翻译</summary>
        public void ShowUp(bool readClipboard)
        {
            Theme.Apply(C);

            // 取词必须赶在窗口显示之前：一旦自己成了前台窗口，对方的选区就没了
            string text = null;
            bool fromSelection = false;
            if (readClipboard && C.GrabSelection)
            {
                text = Selection.Grab(C.GrabWaitMs, C.RestoreClipboard);
                fromSelection = !string.IsNullOrWhiteSpace(text);
            }
            if (string.IsNullOrWhiteSpace(text) && readClipboard && C.ReadClipboardOnShow)
                text = SafeClipboardText();

            ApplyTheme();
            PositionWindow();

            Show();
            WindowState = FormWindowState.Normal;
            Native.ForceForeground(this);

            // 子控件句柄在窗口显示后才存在，此时才能改原生滚动条外观
            Native.ApplyScrollbarTheme(_src, Theme.Current.Dark);
            Native.ApplyScrollbarTheme(_dst, Theme.Current.Dark);

            _fromSelection = fromSelection;

            if (!string.IsNullOrWhiteSpace(text))
            {
                _src.Text = TextPrep.Clean(text, C);
                _src.Select(0, 0);
                _src.ScrollToCaret();
                TranslateNow(_src.Text);
            }
            else
            {
                _src.Focus();
                _src.SelectAll();
                if (string.IsNullOrWhiteSpace(_src.Text))
                    SetStatus(C.GrabSelection
                        ? "没有选中文字，剪贴板也是空的 — 直接输入后按 Ctrl+Enter"
                        : "剪贴板为空，直接输入后按 Ctrl+Enter 翻译");
            }
        }

        /// <summary>托盘唤起：先显示窗口，再读取剪贴板，避免划词/UIA 查询挡住弹窗。</summary>
        public void ShowFromTray()
        {
            ShowUp(false);

            var text = SafeClipboardText();
            if (string.IsNullOrWhiteSpace(text)) return;

            _fromSelection = false;
            _src.Text = TextPrep.Clean(text, C);
            _src.Select(0, 0);
            _src.ScrollToCaret();
            TranslateNow(_src.Text);
        }

        private void PositionWindow()
        {
            var screen = Screen.FromPoint(Cursor.Position).WorkingArea;

            if (C.ShowPosition == "remember" && C.WinX >= 0 && C.WinY >= 0)
            {
                var pt = new Point(C.WinX, C.WinY);
                if (Screen.FromPoint(pt).WorkingArea.IntersectsWith(new Rectangle(pt, Size)))
                {
                    Location = pt;
                    return;
                }
            }

            if (C.ShowPosition == "cursor")
            {
                var p = Cursor.Position;
                int x = p.X - Width / 2, y = p.Y + 18;
                x = Math.Max(screen.Left, Math.Min(x, screen.Right - Width));
                y = Math.Max(screen.Top, Math.Min(y, screen.Bottom - Height));
                Location = new Point(x, y);
                return;
            }

            Location = new Point(
                screen.Left + (screen.Width - Width) / 2,
                screen.Top + (screen.Height - Height) / 2);
        }

        /// <summary>剪贴板偶发被其他进程占用，重试几次</summary>
        private static string SafeClipboardText()
        {
            for (int i = 0; i < 5; i++)
            {
                try { if (Clipboard.ContainsText()) return Clipboard.GetText(); return null; }
                catch { Thread.Sleep(40); }
            }
            return null;
        }

        public void SetSource(string text)
        {
            _src.Text = text ?? "";
        }

        public void HideToTray()
        {
            Speech.Stop();
            StopLoadingDisplay();
            if (C.ShowPosition == "remember" && WindowState == FormWindowState.Normal)
            {
                C.WinX = Location.X; C.WinY = Location.Y;
            }
            if (WindowState == FormWindowState.Normal)
            {
                C.WinW = Width; C.WinH = Height;
            }
            C.Save();
            Hide();
        }

        // ================== 翻译 ==================
        public void TranslateNow(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) { SetStatus("没有可翻译的内容"); return; }

            int seq = ++_reqSeq;
            _btnTrans.Enabled = false;
            _btnTrans.Text = "翻译中";
            BeginLoadingDisplay();
            SetStatus(_allEngines ? "正在请求全部已配置引擎 …" : "正在请求 " + EngineName(C.Engine) + " …");

            var cfgSnapshot = C.Clone();
            bool allEngines = _allEngines;
            if (allEngines)
            {
                var engines = OrderedEngineKeys(cfgSnapshot);
                var batch = new TransResult[engines.Count];
                if (engines.Count == 0)
                {
                    CompleteTranslation(seq, new List<TransResult>(), true);
                    return;
                }

                int pending = engines.Count;
                for (int i = 0; i < engines.Count; i++)
                {
                    int index = i;
                    string engine = engines[i];
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        try
                        {
                            // Each request gets its own clone because normalization assigns
                            // provider IDs and must not mutate another request's snapshot.
                            var localCfg = cfgSnapshot.Clone();
                            var item = Translator.TranslateWithEngine(text, localCfg, engine);
                            if (string.IsNullOrWhiteSpace(item.Engine)) item.Engine = engine;
                            batch[index] = item;
                        }
                        catch (Exception ex)
                        {
                            batch[index] = new TransResult
                            {
                                Source = text,
                                TgtLang = Translator.DecideTarget(text, cfgSnapshot),
                                Engine = engine,
                                Error = Http.DescribeError(ex)
                            };
                        }
                        finally
                        {
                            if (Interlocked.Decrement(ref pending) == 0)
                            {
                                var results = new List<TransResult>(batch);
                                CompleteTranslation(seq, results, true);
                            }
                        }
                    });
                }
                return;
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                TransResult item;
                try { item = Translator.Translate(text, cfgSnapshot); }
                catch (Exception ex) { item = new TransResult { Source = text, Error = Http.DescribeError(ex) }; }
                var results = new List<TransResult> { item };
                CompleteTranslation(seq, results, false);
            });
        }

        private void CompleteTranslation(int seq, List<TransResult> results, bool allEngines)
        {
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    if (seq != _reqSeq) return;           // 已有更新的请求，丢弃
                    _btnTrans.Enabled = true;
                    _btnTrans.Text = "翻译";
                    StopLoadingDisplay();
                    _lastResults = results;
                    _last = FirstSuccessful(results);
                    RenderResults(results, allEngines);
                    AfterTranslate(_last);
                });
            }
            catch { }
        }

        /// <summary>按首选引擎优先排列，随后补上全部已配置引擎。</summary>
        private List<string> OrderedEngineKeys(Config cfg)
        {
            var list = Translator.GetConfiguredEngineKeys(cfg);
            var preferred = cfg.Engine;
            if (string.IsNullOrWhiteSpace(preferred)) return list;

            int index = -1;
            for (int i = 0; i < list.Count; i++)
                if (string.Equals(list[i], preferred, StringComparison.OrdinalIgnoreCase)) { index = i; break; }
            if (index > 0)
            {
                var item = list[index];
                list.RemoveAt(index);
                list.Insert(0, item);
            }
            return list;
        }

        private static TransResult FirstSuccessful(List<TransResult> results)
        {
            if (results == null) return null;
            foreach (var result in results)
                if (result != null && result.Ok) return result;
            return results.Count > 0 ? results[0] : null;
        }

        private void AfterTranslate(TransResult r)
        {
            if (r == null || !r.Ok) return;

            if (C.AutoCopyResult)
            {
                try { Clipboard.SetText(r.Text); } catch { }
            }
            if (C.AutoSpeak && r.Source.Length <= C.AutoSpeakMaxLen)
            {
                // 单词读原文（学发音），句子读译文
                Speech.Speak(TextPrep.IsSingleWord(r.Source) ? r.Source : r.Text, C);
            }
            History.Add(r, C);
        }

        private void SetStatus(string s)
        {
            _status.Text = s;
        }

        private string EngineName(string e)
        {
            return Translator.EngineDisplay(e, C);
        }
    }
}
