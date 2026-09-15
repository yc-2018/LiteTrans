using System.Collections.Generic;

namespace LiteTrans
{
    public static class Lang
    {
        /// <summary>下拉框用：code -> 显示名</summary>
        public static readonly List<KeyValuePair<string, string>> All = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string,string>("zh", "简体中文"),
            new KeyValuePair<string,string>("zh-TW", "繁体中文"),
            new KeyValuePair<string,string>("en", "英语"),
            new KeyValuePair<string,string>("ja", "日语"),
            new KeyValuePair<string,string>("ko", "韩语"),
            new KeyValuePair<string,string>("fr", "法语"),
            new KeyValuePair<string,string>("de", "德语"),
            new KeyValuePair<string,string>("es", "西班牙语"),
            new KeyValuePair<string,string>("ru", "俄语"),
            new KeyValuePair<string,string>("it", "意大利语"),
            new KeyValuePair<string,string>("pt", "葡萄牙语"),
            new KeyValuePair<string,string>("vi", "越南语"),
            new KeyValuePair<string,string>("th", "泰语"),
            new KeyValuePair<string,string>("ar", "阿拉伯语"),
        };

        public static string DisplayName(string code)
        {
            foreach (var kv in All) if (kv.Key == code) return kv.Value;
            return code;
        }

        /// <summary>把引擎返回的源语言代码显示成中文</summary>
        public static string Detected(string code)
        {
            if (string.IsNullOrEmpty(code) || code == "auto") return "自动";
            return DisplayName(code);
        }
    }
}
