using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace LiteTrans
{
    /// <summary>开机自启：写 HKCU Run 项，无需管理员权限</summary>
    public static class Startup
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "LiteTrans";

        public static string ExePath
        {
            get { return Process.GetCurrentProcess().MainModule.FileName; }
        }

        public static bool IsEnabled()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                {
                    var v = k == null ? null : k.GetValue(ValueName) as string;
                    if (string.IsNullOrEmpty(v)) return false;
                    return v.IndexOf(ExePath, StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch { return false; }
        }

        public static bool Set(bool enable)
        {
            try
            {
                // Run 项可能尚不存在；CreateSubKey 让首次开启自启也能正常写入。
                using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (k == null) return false;
                    if (enable) k.SetValue(ValueName, "\"" + ExePath + "\" --tray");
                    else if (k.GetValue(ValueName) != null) k.DeleteValue(ValueName, false);
                    return true;
                }
            }
            catch { return false; }
        }
    }
}
