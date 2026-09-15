using System;
using System.IO;
using System.Text;

namespace LiteTrans
{
    /// <summary>临时诊断日志，定位热键与取词链路</summary>
    public static class Dbg
    {
        private static readonly object L = new object();
        public static bool Enabled = false;   // 仅排障时打开

        public static void Log(string msg)
        {
            if (!Enabled) return;
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LiteTrans");
                Directory.CreateDirectory(dir);
                lock (L)
                    File.AppendAllText(Path.Combine(dir, "debug.log"),
                        DateTime.Now.ToString("HH:mm:ss.fff") + "  " + msg + Environment.NewLine,
                        new UTF8Encoding(false));
            }
            catch { }
        }
    }
}
