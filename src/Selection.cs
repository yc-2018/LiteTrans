using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace LiteTrans
{
    /// <summary>
    /// 划词翻译：向当前前台程序模拟一次 Ctrl+C 取走选中文字，再把剪贴板还原，
    /// 让用户"选中即可翻译"，不必自己按复制。
    /// </summary>
    public static class Selection
    {
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vk);
        [DllImport("user32.dll")]
        private static extern uint GetClipboardSequenceNumber();

        private const uint KEYUP = 0x0002;
        private const byte VK_CONTROL = 0x11, VK_C = 0x43;

        // 左右分身也要一并松开，否则残留的 Alt 会把 Ctrl+C 变成 Ctrl+Alt+C
        private static readonly byte[] Modifiers =
        {
            0xA0, 0xA1, 0x10,   // Shift / LShift / RShift
            0xA2, 0xA3, 0x11,   // Ctrl  / LCtrl  / RCtrl
            0xA4, 0xA5, 0x12,   // Alt   / LAlt   / RAlt
            0x5B, 0x5C          // LWin / RWin
        };

        /// <summary>抓取期间为 true，供剪贴板监听器跳过自己造成的变化，避免自触发循环</summary>
        public static volatile bool Busy;

        /// <summary>本次取词走的是哪条路径，用于界面提示</summary>
        public static string LastMethod = "";

        /// <returns>选中的文字；没有选区或两种方式都失败时返回 null</returns>
        public static string Grab(int waitMs, bool restoreClipboard)
        {
            return Grab(waitMs, restoreClipboard, true);
        }

        public static string Grab(int waitMs, bool restoreClipboard, bool preferUia)
        {
            if (waitMs < 60) waitMs = 60;
            if (waitMs > 1500) waitMs = 1500;
            LastMethod = "";

            // 首选 UI Automation：不碰剪贴板、无按键副作用
            if (preferUia)
            {
                try
                {
                    var viaUia = UiaSelection.TryGet(Math.Max(400, waitMs));
                    if (!string.IsNullOrWhiteSpace(viaUia))
                    {
                        LastMethod = "uia";
                        return viaUia;
                    }
                }
                catch { }
            }

            Busy = true;
            try
            {
                // 先记下原剪贴板，抓完要还回去
                string oldText = null;
                bool hadText = false;
                try
                {
                    if (Clipboard.ContainsText()) { oldText = Clipboard.GetText(); hadText = true; }
                }
                catch { }

                uint before = GetClipboardSequenceNumber();

                ReleaseHeldModifiers();
                SendCopy();

                // 轮询剪贴板序号：变了才说明目标程序真的响应了复制
                string grabbed = null;
                for (int waited = 0; waited < waitMs; waited += 15)
                {
                    Thread.Sleep(15);
                    if (GetClipboardSequenceNumber() == before) continue;

                    for (int retry = 0; retry < 4 && grabbed == null; retry++)
                    {
                        try { if (Clipboard.ContainsText()) grabbed = Clipboard.GetText(); }
                        catch { Thread.Sleep(30); }
                    }
                    break;
                }

                if (string.IsNullOrWhiteSpace(grabbed)) return null;
                LastMethod = "copy";

                // 还原，避免污染用户自己的剪贴板内容
                if (restoreClipboard && hadText && oldText != grabbed)
                {
                    try { Clipboard.SetText(oldText); } catch { }
                }
                return grabbed;
            }
            catch { return null; }
            finally
            {
                // 让还原动作产生的剪贴板事件先走完，再解除屏蔽
                ThreadPool.QueueUserWorkItem(delegate
                {
                    Thread.Sleep(250);
                    Busy = false;
                });
            }
        }

        /// <summary>热键按下时 Ctrl/Alt 仍被物理按住，不松开会干扰后面的 Ctrl+C</summary>
        private static void ReleaseHeldModifiers()
        {
            bool any = false;
            foreach (var vk in Modifiers)
            {
                if ((GetAsyncKeyState(vk) & 0x8000) == 0) continue;
                keybd_event(vk, 0, KEYUP, UIntPtr.Zero);
                any = true;
            }
            if (any) Thread.Sleep(40);
        }

        private static void SendCopy()
        {
            keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
            keybd_event(VK_C, 0, 0, UIntPtr.Zero);
            Thread.Sleep(20);
            keybd_event(VK_C, 0, KEYUP, UIntPtr.Zero);
            keybd_event(VK_CONTROL, 0, KEYUP, UIntPtr.Zero);
        }
    }
}
