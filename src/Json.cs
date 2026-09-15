using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Web.Script.Serialization;

namespace LiteTrans
{
    /// <summary>围绕 JavaScriptSerializer 的取值助手，避免层层强制转换</summary>
    public static class Json
    {
        private static readonly JavaScriptSerializer Ser =
            new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 };

        public static object Parse(string s) { return Ser.DeserializeObject(s); }
        public static string Stringify(object o) { return Ser.Serialize(o); }

        /// <summary>按路径取值，支持 "a.b" 与数组下标 "a.0.b"，任一层缺失返回 null</summary>
        public static object Get(object node, string path)
        {
            if (node == null) return null;
            foreach (var seg in path.Split('.'))
            {
                if (node == null) return null;
                var dict = node as Dictionary<string, object>;
                if (dict != null)
                {
                    if (!dict.TryGetValue(seg, out node)) return null;
                    continue;
                }
                var list = node as IList;
                int idx;
                if (list != null && int.TryParse(seg, out idx))
                {
                    if (idx < 0 || idx >= list.Count) return null;
                    node = list[idx];
                    continue;
                }
                return null;
            }
            return node;
        }

        public static string Str(object node, string path)
        {
            var v = Get(node, path);
            return v == null ? null : Convert.ToString(v);
        }

        public static IList List(object node, string path)
        {
            return Get(node, path) as IList;
        }

        /// <summary>缩进美化，仅用于让磁盘上的配置文件可读可手改</summary>
        public static string Pretty(string json)
        {
            var sb = new StringBuilder();
            int indent = 0; bool inStr = false, esc = false;
            foreach (var c in json)
            {
                if (esc) { sb.Append(c); esc = false; continue; }
                if (c == (char)92 && inStr) { sb.Append(c); esc = true; continue; }
                if (c == '"') { inStr = !inStr; sb.Append(c); continue; }
                if (inStr) { sb.Append(c); continue; }

                if (c == '{' || c == '[') { sb.Append(c).Append('\n').Append(' ', ++indent * 2); }
                else if (c == '}' || c == ']') { sb.Append('\n').Append(' ', --indent * 2).Append(c); }
                else if (c == ',') { sb.Append(c).Append('\n').Append(' ', indent * 2); }
                else if (c == ':') { sb.Append(": "); }
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
