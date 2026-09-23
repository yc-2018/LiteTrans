using System;
using System.IO;
using System.Net;
using System.Text;

namespace LiteTrans
{
    public static class Http
    {
        public const string UA =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";

        static Http()
        {
            try
            {
                ServicePointManager.SecurityProtocol =
                    (SecurityProtocolType)3072 | (SecurityProtocolType)768;   // TLS 1.2 + 1.1
                ServicePointManager.DefaultConnectionLimit = 16;
                ServicePointManager.Expect100Continue = false;
            }
            catch { }
        }

        /// <param name="useProxy">国内接口一律绕开系统代理，避免开着代理软件时握手失败</param>
        public static string Post(string url, byte[] body, string contentType,
                                  int timeoutMs, WebHeaderCollection extra = null,
                                  bool useProxy = false, string referer = null)
        {
            var req = Build(url, timeoutMs, extra, useProxy, referer);
            req.Method = "POST";
            req.ContentType = contentType;
            req.ContentLength = body.Length;
            using (var s = req.GetRequestStream()) s.Write(body, 0, body.Length);
            return Read(req);
        }

        public static string PostJson(string url, string json, int timeoutMs,
                                      WebHeaderCollection extra = null,
                                      bool useProxy = false, string referer = null)
        {
            return Post(url, Encoding.UTF8.GetBytes(json), "application/json", timeoutMs, extra, useProxy, referer);
        }

        public static string Get(string url, int timeoutMs, WebHeaderCollection extra = null,
                                 bool useProxy = false, string referer = null)
        {
            var req = Build(url, timeoutMs, extra, useProxy, referer);
            req.Method = "GET";
            return Read(req);
        }

        /// <summary>GET 并带出最终重定向地址，便于按实际站点（如 www/cn 分流）续发同源请求。</summary>
        public static string GetWithFinalUrl(string url, int timeoutMs, out string finalUrl,
                                             WebHeaderCollection extra = null, bool useProxy = false,
                                             string referer = null)
        {
            var req = Build(url, timeoutMs, extra, useProxy, referer);
            req.Method = "GET";
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                finalUrl = resp.ResponseUri.ToString();
                return sr.ReadToEnd();
            }
        }

        private static HttpWebRequest Build(string url, int timeoutMs, WebHeaderCollection extra,
                                            bool useProxy, string referer)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = UA;
            req.Timeout = timeoutMs;
            req.ReadWriteTimeout = timeoutMs;
            req.Proxy = useProxy ? WebRequest.GetSystemWebProxy() : null;
            req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            req.Headers["Accept-Language"] = "zh-CN,zh;q=0.9,en;q=0.8";
            if (referer != null) req.Referer = referer;
            if (extra != null) foreach (string k in extra) req.Headers[k] = extra[k];
            return req;
        }

        private static string Read(HttpWebRequest req)
        {
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                return sr.ReadToEnd();
        }

        /// <summary>把 WebException 里的响应体一并带出来，便于在界面上显示真实原因</summary>
        public static string DescribeError(Exception ex)
        {
            var we = ex as WebException;
            if (we == null) return ex.Message;
            if (we.Status == WebExceptionStatus.Timeout) return "请求超时";
            var resp = we.Response as HttpWebResponse;
            if (resp == null) return "网络不可用（" + we.Status + "）";
            try
            {
                using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    var body = sr.ReadToEnd();
                    if (body.Length > 300) body = body.Substring(0, 300);
                    return "HTTP " + (int)resp.StatusCode + " " + body;
                }
            }
            catch { return "HTTP " + (int)resp.StatusCode; }
        }

        public static string UrlEncode(string s) { return Uri.EscapeDataString(s ?? ""); }
    }
}
