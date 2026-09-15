using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace LiteTrans
{
    public class DictEntry
    {
        public string Pos;      // 词性
        public string Mean;     // 释义
    }

    public class TransResult
    {
        public string Source = "";
        public string Text = "";                 // 译文
        public string Engine = "";
        public string SrcLang = "", TgtLang = "";
        public string PhoneticUk, PhoneticUs;
        public List<DictEntry> Dict = new List<DictEntry>();
        public string Error;
        public string FallbackNotice;
        public int ElapsedMs;
        public bool Ok { get { return string.IsNullOrEmpty(Error) && !string.IsNullOrEmpty(Text); } }
    }

    public static class Translator
    {
        /// <summary>决定译入语言：中文内容译成 PivotLang，其余一律译成 TargetLang</summary>
        public static string DecideTarget(string text, Config c)
        {
            if (!c.AutoSwapCJK) return c.TargetLang;
            bool srcIsCjk = TextPrep.CjkRatio(text) > 0.2;
            bool targetIsCjk = c.TargetLang == "zh" || c.TargetLang == "ja" || c.TargetLang == "ko";
            return (srcIsCjk && targetIsCjk) ? c.PivotLang : c.TargetLang;
        }

        public static TransResult Translate(string raw, Config c)
        {
            return Translate(raw, c, true, null);
        }

        /// <summary>翻译入口；指定测试引擎时允许调用方限制回退范围。</summary>
        public static TransResult Translate(string raw, Config c, bool allowFallback, string requestedEngine)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var text = TextPrep.Clean(raw, c);
            var r = new TransResult { Source = text, TgtLang = DecideTarget(text, c) };

            if (string.IsNullOrWhiteSpace(text))
            {
                r.Error = "没有可翻译的内容";
                return r;
            }

            // 主引擎（失败则依次回退）
            var requested = string.IsNullOrWhiteSpace(requestedEngine) ? c.Engine : requestedEngine;
            bool requestedConfigured = IsEngineConfigured(requested, c);
            if (!allowFallback && !requestedConfigured)
            {
                r.Error = MissingConfigNotice(requested, null);
                r.ElapsedMs = (int)sw.ElapsedMilliseconds;
                return r;
            }

            var order = allowFallback
                ? BuildChain(c)
                : new List<string> { requested };
            foreach (var eng in order)
            {
                // 每个引擎都复用同一个结果对象；清掉上一次尝试留下的字段，
                // 避免失败状态或部分译文影响下一个引擎的 Ok 判断。
                ResetAttempt(r);
                try
                {
                    switch (eng)
                    {
                        case "ai": Ai(text, r, c); break;
                        case "baidu": Baidu(text, r, c); break;
                        default: Transmart(text, r, c); break;
                    }
                    if (r.Ok)
                    {
                        r.Engine = eng;
                        r.Error = null;
                        if (eng != requested)
                        {
                            r.FallbackNotice = requestedConfigured
                                ? "首选引擎不可用，已自动切换到" + EngineDisplay(eng)
                                : MissingConfigNotice(requested, eng);
                        }
                        break;
                    }
                }
                catch (Exception ex) { r.Error = Http.DescribeError(ex); }
            }

            // 单词时并上词典释义与音标（不影响主译文成败）
            if (c.DictEnhance && TextPrep.IsSingleWord(text))
            {
                try { Dictionary(text, r, c); } catch { }
            }

            if (!r.Ok && string.IsNullOrEmpty(r.Error)) r.Error = "所有翻译引擎均未返回结果";
            if (!r.Ok && !requestedConfigured)
                r.FallbackNotice = MissingConfigNotice(requested, null);
            r.ElapsedMs = (int)sw.ElapsedMilliseconds;
            return r;
        }

        private static void ResetAttempt(TransResult r)
        {
            r.Text = "";
            r.Engine = "";
            r.SrcLang = "";
            r.PhoneticUk = null;
            r.PhoneticUs = null;
            r.Dict.Clear();
            r.Error = null;
        }

        private static bool IsEngineConfigured(string engine, Config c)
        {
            if (engine == "baidu")
                return !string.IsNullOrWhiteSpace(c.BaiduAppId) && !string.IsNullOrWhiteSpace(c.BaiduKey);
            if (engine == "ai")
                return c.AiEnabled && !string.IsNullOrWhiteSpace(c.AiKey) &&
                       !string.IsNullOrWhiteSpace(c.AiBaseUrl);
            return engine == "transmart" || string.IsNullOrWhiteSpace(engine);
        }

        private static string MissingConfigNotice(string engine, string actual)
        {
            string name = engine == "baidu" ? "百度翻译" : engine == "ai" ? "AI 精翻" : EngineDisplay(engine);
            if (string.IsNullOrEmpty(actual)) return name + "未配置，当前未使用该引擎";
            return name + "未配置，已自动切换到" + EngineDisplay(actual);
        }

        private static List<string> BuildChain(Config c)
        {
            var list = new List<string>();
            if (c.Engine == "ai" && c.AiEnabled && !string.IsNullOrWhiteSpace(c.AiKey) && !string.IsNullOrWhiteSpace(c.AiBaseUrl)) list.Add("ai");
            else if (c.Engine == "baidu" && !string.IsNullOrWhiteSpace(c.BaiduAppId) && !string.IsNullOrWhiteSpace(c.BaiduKey)) list.Add("baidu");

            if (!list.Contains("transmart")) list.Add("transmart");      // 免密钥主力，永远兜底
            if (!list.Contains("baidu") && !string.IsNullOrWhiteSpace(c.BaiduAppId) && !string.IsNullOrWhiteSpace(c.BaiduKey)) list.Add("baidu");
            if (!list.Contains("ai") && c.AiEnabled && !string.IsNullOrWhiteSpace(c.AiKey) && !string.IsNullOrWhiteSpace(c.AiBaseUrl)) list.Add("ai");
            return list;
        }

        private static string EngineDisplay(string engine)
        {
            if (engine == "ai") return "AI 精翻";
            if (engine == "baidu") return "百度翻译";
            return "腾讯翻译";
        }

        // ================== 腾讯交互翻译（免密钥） ==================
        private static void Transmart(string text, TransResult r, Config c)
        {
            var lines = text.Split('\n');
            var arr = new List<object>();
            foreach (var l in lines) arr.Add(l);

            var payload = Json.Stringify(new Dictionary<string, object> {
                { "header", new Dictionary<string,object>{
                    { "fn", "auto_translation" },
                    { "client_key", "browser-chrome-126.0.0-Windows 10-" + DateTime.Now.Ticks % 100000 + "-0" } } },
                { "type", "plain" },
                { "model_category", "normal" },
                { "source", new Dictionary<string,object>{ { "lang", "auto" }, { "text_list", arr } } },
                { "target", new Dictionary<string,object>{ { "lang", r.TgtLang } } }
            });

            var resp = Http.PostJson("https://transmart.qq.com/api/imt", payload, c.TimeoutMs,
                                     null, false, "https://transmart.qq.com/zh-CN/index");
            var node = Json.Parse(resp);

            if (Json.Str(node, "header.ret_code") != "succ")
                throw new Exception("transmart: " + (Json.Str(node, "header.message") ?? "返回异常"));

            var outs = Json.List(node, "auto_translation");
            if (outs == null || outs.Count == 0) throw new Exception("transmart 未返回译文");

            var sb = new StringBuilder();
            foreach (var o in outs) sb.AppendLine(Convert.ToString(o));
            r.Text = sb.ToString().TrimEnd();
            r.SrcLang = Json.Str(node, "src_lang") ?? "auto";
        }

        // ================== 词典增强：音标 + 分词性释义 ==================
        private static void Dictionary(string word, TransResult r, Config c)
        {
            int t = Math.Min(c.TimeoutMs, 4000);

            // 1) 88 邮箱：带英美双音标，且释义按词性分组
            try
            {
                var resp = Http.Get("https://mail.88.com/api/x/wcw/wordMean?word=" + Http.UrlEncode(word), t);
                var node = Json.Parse(resp);

                var ph = Json.List(node, "var.phonetics");
                if (ph != null)
                    foreach (var item in ph)
                    {
                        var type = Json.Str(item, "t");
                        var val = Json.Str(item, "p");
                        if (string.IsNullOrEmpty(val)) continue;
                        if (type == "uk" || type == "en") r.PhoneticUk = val;
                        else if (type == "us") r.PhoneticUs = val;
                    }

                var means = Json.List(node, "var.means");
                if (means != null)
                    foreach (var item in means)
                    {
                        var v = Json.Str(item, "v");
                        if (string.IsNullOrWhiteSpace(v)) continue;
                        r.Dict.Add(new DictEntry { Pos = Json.Str(item, "c"), Mean = v });
                    }
            }
            catch { }

            if (r.Dict.Count > 0) return;

            // 2) 百度 sug 兜底
            try
            {
                var resp = Http.Post("https://fanyi.baidu.com/sug",
                    Encoding.UTF8.GetBytes("kw=" + Http.UrlEncode(word)),
                    "application/x-www-form-urlencoded", t);
                var data = Json.List(Json.Parse(resp), "data");
                if (data != null)
                    foreach (var item in data)
                    {
                        if (!string.Equals(Json.Str(item, "k"), word, StringComparison.OrdinalIgnoreCase)) continue;
                        var v = Json.Str(item, "v");
                        if (!string.IsNullOrWhiteSpace(v)) r.Dict.Add(new DictEntry { Mean = v });
                        break;
                    }
            }
            catch { }

            if (r.Dict.Count > 0) return;

            // 3) 有道 suggest 兜底
            try
            {
                var resp = Http.Get("https://dict.youdao.com/suggest?num=1&ver=3.0&doctype=json&cache=false&le=en&q="
                                    + Http.UrlEncode(word), t);
                var ex = Json.Str(Json.Parse(resp), "data.entries.0.explain");
                if (!string.IsNullOrWhiteSpace(ex)) r.Dict.Add(new DictEntry { Mean = ex });
            }
            catch { }
        }

        // ================== AI 精翻（任意 OpenAI 兼容接口） ==================
        private static void Ai(string text, TransResult r, Config c)
        {
            var langName = Lang.DisplayName(r.TgtLang);
            var sys = (c.AiPrompt ?? "").Replace("{target}", langName);
            if (string.IsNullOrWhiteSpace(sys)) sys = "把文本翻译为" + langName + "，只输出译文。";

            var payload = Json.Stringify(new Dictionary<string, object> {
                { "model", c.AiModel },
                { "temperature", 0.2 },
                { "messages", new List<object> {
                    new Dictionary<string,object>{ {"role","system"}, {"content", sys} },
                    new Dictionary<string,object>{ {"role","user"},   {"content", text} } } }
            });

            var url = (c.AiBaseUrl ?? "").TrimEnd('/');
            if (!url.EndsWith("/chat/completions")) url += "/chat/completions";

            var h = new WebHeaderCollection();
            h["Authorization"] = "Bearer " + c.AiKey;

            // AI 端点可能在境外，这里允许走系统代理
            var resp = Http.PostJson(url, payload, Math.Max(c.TimeoutMs, 30000), h, true);
            var content = Json.Str(Json.Parse(resp), "choices.0.message.content");
            if (string.IsNullOrWhiteSpace(content)) throw new Exception("AI 未返回内容");

            r.Text = content.Trim();
            r.SrcLang = "auto";
        }

        // ================== 百度翻译开放平台 ==================
        private static void Baidu(string text, TransResult r, Config c)
        {
            var salt = new Random().Next(32768, 65536).ToString();
            var sign = Md5(c.BaiduAppId + text + salt + c.BaiduKey);

            var from = "auto";
            var to = r.TgtLang == "zh" ? "zh" : r.TgtLang;

            var body = "appid=" + Http.UrlEncode(c.BaiduAppId) + "&q=" + Http.UrlEncode(text)
                     + "&from=" + from + "&to=" + Http.UrlEncode(to)
                     + "&salt=" + salt + "&sign=" + sign;

            var resp = Http.Post("https://fanyi-api.baidu.com/api/trans/vip/translate",
                Encoding.UTF8.GetBytes(body), "application/x-www-form-urlencoded", c.TimeoutMs);

            var node = Json.Parse(resp);
            var err = Json.Str(node, "error_code");
            if (!string.IsNullOrEmpty(err) && err != "52000")
                throw new Exception("百度 " + err + ": " + (Json.Str(node, "error_msg") ?? ""));

            var list = Json.List(node, "trans_result");
            if (list == null || list.Count == 0) throw new Exception("百度未返回译文");

            var sb = new StringBuilder();
            foreach (var item in list) sb.AppendLine(Json.Str(item, "dst"));
            r.Text = sb.ToString().TrimEnd();
            r.SrcLang = Json.Str(node, "from") ?? "auto";
        }

        private static string Md5(string s)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(s));
                var sb = new StringBuilder();
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
