using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace LiteTrans
{
    /// <summary>用户配置，序列化到 %APPDATA%\LiteTrans\config.json</summary>
    public class Config
    {
        // —— 热键 ——
        public string Hotkey = "Ctrl+Alt+D";          // 主热键：弹出并翻译剪贴板
        public string HotkeyInput = "";                // 可选：弹出空白输入窗

        // —— 行为 ——
        public bool AutoStart = false;                 // 开机自启
        public bool StartHidden = true;                // 启动即隐藏到托盘
        public bool GrabSelection = true;              // 划词翻译：先抓取选中文字
        public bool RestoreClipboard = true;           // 抓取后还原原剪贴板
        public int GrabWaitMs = 260;                   // 等待目标程序响应复制的上限
        public bool ReadClipboardOnShow = true;        // 没抓到选区时退回读剪贴板
        public bool EscToTray = true;                  // Esc 收回托盘
        public bool HideOnFocusLost = false;           // 失焦自动隐藏
        public bool TopMost = true;                    // 窗口置顶
        public bool AutoCopyResult = false;            // 译文自动回写剪贴板
        public bool MonitorClipboard = false;          // 复制即翻译
        public bool TrayBalloonOnStart = true;         // 首次启动气泡提示
        public bool SingleInstance = true;

        // —— 语言 ——
        public string TargetLang = "zh";               // 主目标语言
        public string PivotLang = "en";                // 当源语言==目标语言时改译到此语言
        public bool AutoSwapCJK = true;                // 中→英 / 外→中 自动互换

        // —— 外观 ——
        public string Theme = "auto";                  // auto | light | dark
        public string Accent = "#4F8CFF";
        public int FontSize = 13;
        public string FontFamily = "Microsoft YaHei UI";
        public string ShowPosition = "center";         // center | cursor | remember
        public int WinX = -1, WinY = -1;
        public int WinW = 600, WinH = 460;
        public int Opacity = 100;                      // 60~100
        public bool CompactMode = false;

        // —— 引擎 ——
        public string Engine = "transmart";            // transmart | ai | baidu
        public bool DictEnhance = true;                // 单词时附加词典释义/音标
        public int TimeoutMs = 9000;

        // 百度翻译开放平台（可选）
        public string BaiduAppId = "";
        public string BaiduKey = "";

        // AI 精翻（任意 OpenAI 兼容接口）
        public bool AiEnabled = false;
        public string AiBaseUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1";
        public string AiKey = "";
        public string AiModel = "qwen-plus";
        public string AiPrompt = "你是专业翻译引擎。将用户文本准确、自然地翻译为{target}，保留原有换行与格式，只输出译文本身，不要任何解释或前后缀。";

        // —— 文本预处理 ——
        public bool SplitCamel = true;                 // 驼峰拆词
        public bool UnderscoreToSpace = true;          // 下划线转空格
        public bool CollapseSpaces = true;             // 合并多余空白
        public bool JoinLineBreaks = true;             // 合并 PDF 式硬换行
        public bool StripCodeComment = true;           // 去掉 // /* * # 等注释符

        // —— 朗读 ——
        public bool AutoSpeak = false;
        public int SpeakRate = 0;                      // -10 ~ 10
        public int SpeakVolume = 100;
        public int AutoSpeakMaxLen = 120;              // 超长不自动朗读

        // —— 历史 ——
        public bool KeepHistory = true;
        public int HistoryMax = 200;

        // —— 内部 ——
        public bool FirstRun = true;

        // ================= 持久化 =================
        private static string Dir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "LiteTrans");
            }
        }
        public static string FilePath { get { return Path.Combine(Dir, "config.json"); } }
        public static string HistoryPath { get { return Path.Combine(Dir, "history.json"); } }

        public static Config Load()
        {
            var cfg = new Config();
            try
            {
                if (File.Exists(FilePath))
                {
                    var txt = File.ReadAllText(FilePath, Encoding.UTF8);
                    var map = new JavaScriptSerializer().DeserializeObject(txt) as Dictionary<string, object>;
                    if (map != null) cfg.Apply(map);
                }
            }
            catch { /* 配置损坏则回退默认值 */ }
            return cfg;
        }

        /// <summary>逐字段套用，缺字段/类型不符时保留默认值，便于版本升级</summary>
        private void Apply(Dictionary<string, object> m)
        {
            foreach (var f in typeof(Config).GetFields())
            {
                object v;
                if (!m.TryGetValue(f.Name, out v) || v == null) continue;
                try
                {
                    if (f.FieldType == typeof(string)) f.SetValue(this, Convert.ToString(v));
                    else if (f.FieldType == typeof(bool)) f.SetValue(this, Convert.ToBoolean(v));
                    else if (f.FieldType == typeof(int)) f.SetValue(this, Convert.ToInt32(v));
                }
                catch { }
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var json = new JavaScriptSerializer().Serialize(this);
                File.WriteAllText(FilePath, Json.Pretty(json), new UTF8Encoding(false));
            }
            catch { }
        }

        public Config Clone()
        {
            var c = new Config();
            foreach (var f in typeof(Config).GetFields()) f.SetValue(c, f.GetValue(this));
            return c;
        }
    }
}
