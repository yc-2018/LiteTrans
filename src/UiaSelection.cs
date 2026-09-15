using System;
using System.Text;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using System.Runtime.InteropServices;

namespace LiteTrans
{
    /// <summary>
    /// 通过 UI Automation 直接读取焦点控件里的选中文本。
    /// 相比模拟 Ctrl+C：不动剪贴板、无按键副作用，浏览器/Office/WPF/WinForms 普遍支持。
    /// 少数程序（部分 PDF 阅读器、游戏、老式控件）不实现 TextPattern，此时返回 null 由调用方回退。
    /// </summary>
    public static class UiaSelection
    {
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();

        /// <summary>UIA 偶尔会在无响应的目标上卡住，故放到后台线程并限时等待</summary>
        public static string TryGet(int timeoutMs)
        {
            if (timeoutMs < 100) timeoutMs = 100;

            string result = null;
            var done = new ManualResetEvent(false);

            var t = new Thread(delegate ()
            {
                try { result = Read(); }
                catch { }
                finally { try { done.Set(); } catch { } }
            });
            t.IsBackground = true;          // 卡住也不会阻止程序退出
            t.SetApartmentState(ApartmentState.MTA);
            t.Start();

            // 等待期间必须继续泵消息：UIA 查询可能要回调本线程，
            // 若在这里死等，查询自身窗口时会直接死锁。
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (done.WaitOne(10)) return result;
                try { System.Windows.Forms.Application.DoEvents(); }
                catch { }
            }
            return null;
        }

        private static string Read()
        {
            // 先按焦点元素取（最精确），失败再以前台窗口为锚点在其子树里找选区。
            // 只依赖 FocusedElement 不够稳：焦点可能落在无文本能力的容器上。
            var s = FromElement(AutomationElement.FocusedElement);
            if (!string.IsNullOrWhiteSpace(s)) return s;

            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero) return null;

            AutomationElement win = null;
            try { win = AutomationElement.FromHandle(fg); }
            catch { }
            if (win == null) return null;

            s = FromElement(win);
            if (!string.IsNullOrWhiteSpace(s)) return s;

            // 在后代里找第一个真正有选区的文本元素
            try
            {
                var all = win.FindAll(TreeScope.Descendants, Condition.TrueCondition);
                foreach (AutomationElement e in all)
                {
                    s = FromElement(e);
                    if (!string.IsNullOrWhiteSpace(s)) return s;
                }
            }
            catch { }

            return null;
        }

        /// <summary>从单个元素上尝试读出选中文本</summary>
        private static string FromElement(AutomationElement el)
        {
            if (el == null) return null;

            object pat;
            if (el.TryGetCurrentPattern(TextPattern.Pattern, out pat))
            {
                var tp = pat as TextPattern;
                if (tp != null)
                {
                    TextPatternRange[] ranges = null;
                    try { ranges = tp.GetSelection(); }
                    catch { }

                    if (ranges != null && ranges.Length > 0)
                    {
                        var sb = new StringBuilder();
                        foreach (var r in ranges)
                        {
                            if (r == null) continue;
                            try { sb.Append(r.GetText(-1)); } catch { }
                        }
                        var text = sb.ToString();
                        if (!string.IsNullOrWhiteSpace(text)) return text;
                    }
                }
            }
            return null;
        }
    }
}
