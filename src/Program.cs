using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

namespace LiteTrans
{
    internal static class Program
    {
        private static Mutex _mutex;
        private static readonly string MutexName = "Global" + (char)92 + "LiteTrans_SingleInstance_v1";

        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try { SetDpiAware(); } catch { }

            var cfg = Config.Load();

            bool wantSettings = Array.IndexOf(args, "--settings") >= 0;
            if (cfg.SingleInstance && !AcquireMutex())
            {
                // 已有实例在跑：让它响应本次意图，然后本进程安静退出
                WakeExistingInstance(wantSettings);
                return;
            }

            Application.ThreadException += (s, e) => ReportCrash(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => ReportCrash(e.ExceptionObject as Exception);

            bool startHidden = cfg.StartHidden;
            bool openSettings = false;
            int settingsPage = 0;
            foreach (var a in args)
            {
                if (a == "--tray") startHidden = true;
                else if (a == "--show") startHidden = false;
                else if (a == "--settings") { startHidden = true; openSettings = true; }
                else if (a.StartsWith("--page="))
                {
                    startHidden = true; openSettings = true;
                    int.TryParse(a.Substring(7), out settingsPage);
                }
            }

            var app = new TrayApp(cfg, startHidden);
            if (cfg.MonitorClipboard) app.ClipboardWatch(true);
            if (openSettings) app.ShowSettings(settingsPage);

            Application.Run(app);
        }

        private static bool AcquireMutex()
        {
            bool created;
            _mutex = new Mutex(true, MutexName, out created);
            return created;
        }

        /// <summary>给已运行实例发广播，wParam 区分是弹翻译窗还是打开设置</summary>
        private static void WakeExistingInstance(bool settings)
        {
            try
            {
                uint msg = Native.RegisterWindowMessage("LiteTrans_Wake");
                IntPtr wp = (IntPtr)(settings ? 1 : 0);

                IntPtr target = Native.FindWindow(null, Native.MsgWindowTitle);
                if (target != IntPtr.Zero) Native.PostMessage(target, msg, wp, IntPtr.Zero);
                else Native.PostMessage((IntPtr)0xFFFF /*HWND_BROADCAST*/, msg, wp, IntPtr.Zero);
            }
            catch { }
        }

        private static void SetDpiAware()
        {
            // Win10 1703+ 用 PerMonitorV2，失败则退回系统级 DPI 感知
            if (Environment.OSVersion.Version.Major >= 6)
            {
                if (!Native.SetProcessDpiAwarenessContext((IntPtr)(-4))) Native.SetProcessDPIAware();
            }
        }

        private static void ReportCrash(Exception ex)
        {
            if (ex == null) return;
            try
            {
                var path = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "LiteTrans", "crash.log");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                System.IO.File.AppendAllText(path,
                    DateTime.Now + "  " + ex + Environment.NewLine + Environment.NewLine);
            }
            catch { }

            MessageBox.Show("轻译遇到意外错误：\n\n" + ex.Message +
                "\n\n详情已写入 %APPDATA%" + (char)92 + "LiteTrans" + (char)92 + "crash.log",
                "轻译", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
