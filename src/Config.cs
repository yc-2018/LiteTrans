using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace LiteTrans
{
    /// <summary>一个 OpenAI 兼容翻译接口配置。</summary>
    public class AiProviderConfig
    {
        public string Id = "";
        public string Name = "AI 精翻";
        public bool Enabled = false;
        public string BaseUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1";
        public string Key = "";
        public string Model = "qwen-plus";
        public string Prompt = "你是专业翻译引擎。将用户文本准确、自然地翻译为{target}，保留原有换行与格式，只输出译文本身，不要任何解释或前后缀。";

        public AiProviderConfig Clone()
        {
            return new AiProviderConfig
            {
                Id = Id,
                Name = Name,
                Enabled = Enabled,
                BaseUrl = BaseUrl,
                Key = Key,
                Model = Model,
                Prompt = Prompt,
            };
        }
    }

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
        // 顶部模式选择：auto | forward | reverse。
        // 留空时兼容旧版本配置，由 AutoSwapCJK 推导。
        public string TranslationMode = "";

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
        public bool TranslateAllEngines = false;        // 主窗口同时请求所有已配置引擎
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

        // 可添加多个 OpenAI 兼容接口。旧版的 Ai* 字段会在首次加载时迁移到这里。
        public List<AiProviderConfig> AiProviders = new List<AiProviderConfig>();
        public bool AiProvidersInitialized = false;

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
            cfg.NormalizeAiProviders();
            cfg.NormalizeTranslationMode();
            return cfg;
        }

        /// <summary>迁移旧版单 AI 配置，并保证每项都有稳定且唯一的 ID。</summary>
        public void NormalizeAiProviders()
        {
            if (AiProviders == null) AiProviders = new List<AiProviderConfig>();

            if (!AiProvidersInitialized && AiProviders.Count == 0)
            {
                AiProviders.Add(new AiProviderConfig
                {
                    Name = "AI 精翻",
                    Enabled = AiEnabled,
                    BaseUrl = AiBaseUrl,
                    Key = AiKey,
                    Model = AiModel,
                    Prompt = AiPrompt,
                });
            }
            // 即使 JSON 来自手工配置，只要已经有列表就不再追加旧版第一项。
            AiProvidersInitialized = true;

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var provider in AiProviders)
            {
                if (provider == null) continue;
                provider.Name = string.IsNullOrWhiteSpace(provider.Name) ? "AI 精翻" : provider.Name.Trim();
                provider.BaseUrl = (provider.BaseUrl ?? "").Trim();
                provider.Key = (provider.Key ?? "").Trim();
                provider.Model = (provider.Model ?? "").Trim();
                provider.Prompt = provider.Prompt ?? "";

                var id = (provider.Id ?? "").Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(id) || ids.Contains(id))
                    id = Guid.NewGuid().ToString("N");
                provider.Id = id;
                ids.Add(id);
            }
            AiProviders.RemoveAll(delegate(AiProviderConfig p) { return p == null; });

            // 旧版保存的首选值没有 provider ID，迁移到第一项。
            if (string.Equals(Engine, "ai", StringComparison.OrdinalIgnoreCase) && AiProviders.Count > 0)
                Engine = AiEngineKey(AiProviders[0]);
            else if ((Engine ?? "").StartsWith("ai:", StringComparison.OrdinalIgnoreCase))
            {
                bool found = false;
                var id = Engine.Substring(3);
                foreach (var provider in AiProviders)
                    if (string.Equals(provider.Id, id, StringComparison.OrdinalIgnoreCase)) { found = true; break; }
                if (!found) Engine = "transmart";
            }

            // 保留旧字段镜像，便于降级到旧版本时仍能读取第一项配置。
            if (AiProviders.Count > 0)
            {
                var first = AiProviders[0];
                AiEnabled = first.Enabled;
                AiBaseUrl = first.BaseUrl;
                AiKey = first.Key;
                AiModel = first.Model;
                AiPrompt = first.Prompt;
            }
            else
            {
                // 用户删除全部 provider 后，旧版字段也要清空，避免降级版本
                // 又把已经删除的 AI 配置迁移回来。
                AiEnabled = false;
                AiBaseUrl = "";
                AiKey = "";
                AiModel = "";
                AiPrompt = "";
                if ((Engine ?? "").StartsWith("ai", StringComparison.OrdinalIgnoreCase))
                    Engine = "transmart";
            }
        }

        public static string AiEngineKey(AiProviderConfig provider)
        {
            return provider == null ? "ai" : "ai:" + provider.Id;
        }

        public AiProviderConfig FindAiProvider(string engine)
        {
            NormalizeAiProviders();
            if (string.IsNullOrWhiteSpace(engine) || string.Equals(engine, "ai", StringComparison.OrdinalIgnoreCase))
                return AiProviders.Count > 0 ? AiProviders[0] : null;
            if (!engine.StartsWith("ai:", StringComparison.OrdinalIgnoreCase)) return null;
            var id = engine.Substring(3);
            return AiProviders.Find(delegate(AiProviderConfig p)
            {
                return string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase);
            });
        }

        /// <summary>
        /// 将翻译模式收敛为当前支持的值，并把旧版 AutoSwapCJK 配置迁移到新字段。
        /// </summary>
        public void NormalizeTranslationMode()
        {
            var mode = (TranslationMode ?? "").Trim().ToLowerInvariant();
            if (mode != "auto" && mode != "forward" && mode != "reverse")
                mode = AutoSwapCJK ? "auto" : "forward";

            TranslationMode = mode;
            // AutoSwapCJK 仍由设置页使用，保持它与新模式一致。
            AutoSwapCJK = mode == "auto";
        }

        /// <summary>读取模式；用于直接构造的旧 Config 实例。</summary>
        public string GetTranslationMode()
        {
            var mode = (TranslationMode ?? "").Trim().ToLowerInvariant();
            if (mode == "auto" || mode == "forward" || mode == "reverse") return mode;
            return AutoSwapCJK ? "auto" : "forward";
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
                    else if (f.Name == "AiProviders") AiProviders = ReadAiProviders(v);
                }
                catch { }
            }
        }

        private static List<AiProviderConfig> ReadAiProviders(object value)
        {
            var result = new List<AiProviderConfig>();
            var items = value as IEnumerable;
            if (items == null) return result;

            foreach (var item in items)
            {
                var map = item as Dictionary<string, object>;
                if (map == null) continue;
                var provider = new AiProviderConfig();
                foreach (var field in typeof(AiProviderConfig).GetFields())
                {
                    object raw;
                    if (!map.TryGetValue(field.Name, out raw) || raw == null) continue;
                    try
                    {
                        if (field.FieldType == typeof(string)) field.SetValue(provider, Convert.ToString(raw));
                        else if (field.FieldType == typeof(bool)) field.SetValue(provider, Convert.ToBoolean(raw));
                    }
                    catch { }
                }
                result.Add(provider);
            }
            return result;
        }

        public void Save()
        {
            try
            {
                NormalizeAiProviders();
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
            c.AiProviders = new List<AiProviderConfig>();
            if (AiProviders != null)
                foreach (var provider in AiProviders)
                    if (provider != null) c.AiProviders.Add(provider.Clone());
            return c;
        }
    }
}
