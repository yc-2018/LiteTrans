using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LiteTrans
{
    public class HistoryItem
    {
        public string Src = "", Dst = "", Engine = "", Time = "";
    }

    /// <summary>翻译历史，落盘为 JSON 数组，超出上限自动裁剪</summary>
    public static class History
    {
        private static List<HistoryItem> _items;
        private static readonly object Lock = new object();

        public static List<HistoryItem> Items
        {
            get { lock (Lock) { if (_items == null) Load(); return _items; } }
        }

        private static void Load()
        {
            _items = new List<HistoryItem>();
            try
            {
                if (!File.Exists(Config.HistoryPath)) return;
                var arr = Json.Parse(File.ReadAllText(Config.HistoryPath, Encoding.UTF8)) as IList;
                if (arr == null) return;
                foreach (var o in arr)
                {
                    _items.Add(new HistoryItem
                    {
                        Src = Json.Str(o, "Src") ?? "",
                        Dst = Json.Str(o, "Dst") ?? "",
                        Engine = Json.Str(o, "Engine") ?? "",
                        Time = Json.Str(o, "Time") ?? "",
                    });
                }
            }
            catch { _items = new List<HistoryItem>(); }
        }

        public static void Add(TransResult r, Config c)
        {
            if (!c.KeepHistory || r == null || !r.Ok) return;
            lock (Lock)
            {
                if (_items == null) Load();

                // 与上一条相同则不重复记录
                if (_items.Count > 0 && _items[0].Src == r.Source) return;

                _items.Insert(0, new HistoryItem
                {
                    Src = r.Source,
                    Dst = r.Text,
                    Engine = r.Engine,
                    Time = DateTime.Now.ToString("MM-dd HH:mm"),
                });
                if (_items.Count > Math.Max(10, c.HistoryMax))
                    _items.RemoveRange(c.HistoryMax, _items.Count - c.HistoryMax);
                Save();
            }
        }

        public static void Clear()
        {
            lock (Lock) { _items = new List<HistoryItem>(); Save(); }
        }

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Config.HistoryPath));
                File.WriteAllText(Config.HistoryPath, Json.Stringify(_items), new UTF8Encoding(false));
            }
            catch { }
        }
    }
}
