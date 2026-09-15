using System;
using System.Text;
using System.Text.RegularExpressions;

namespace LiteTrans
{
    /// <summary>剪贴板文本清洗：把代码标识符、PDF 硬换行等还原成适合翻译的自然文本</summary>
    public static class TextPrep
    {
        public static string Clean(string s, Config c)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\r\n", "\n").Replace('\r', '\n').Trim();

            if (c.StripCodeComment) s = StripComment(s);

            // 只在“看起来像标识符”时拆词，避免破坏正常英文句子
            if (c.SplitCamel && LooksLikeIdentifier(s))
                s = Regex.Replace(s, @"(?<=[^A-Z\s])([A-Z])", " $1");

            if (c.UnderscoreToSpace) s = s.Replace('_', ' ').Replace('-', ' ');

            if (c.JoinLineBreaks) s = JoinHardWraps(s);

            if (c.CollapseSpaces)
            {
                s = Regex.Replace(s, @"[ \t]+", " ");
                s = Regex.Replace(s, @"\n{3,}", "\n\n");
            }
            return s.Trim();
        }

        /// <summary>无空格或全为 a_b / aB 形式，视为代码标识符</summary>
        private static bool LooksLikeIdentifier(string s)
        {
            if (s.Length > 80 || s.Contains("\n")) return false;
            return !s.Contains(" ") && Regex.IsMatch(s, @"^[A-Za-z_][A-Za-z0-9_$]*$");
        }

        private static string StripComment(string s)
        {
            s = Regex.Replace(s, @"^\s*(//+|#+|\*+|--)\s?", "", RegexOptions.Multiline);
            s = Regex.Replace(s, @"/\*+|\*+/", "");
            return s;
        }

        /// <summary>行尾没有句末标点、且下一行以小写/逗号开头时，判定为排版硬换行并接回去</summary>
        private static string JoinHardWraps(string s)
        {
            var lines = s.Split('\n');
            var sb = new StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                var cur = lines[i].TrimEnd();
                sb.Append(cur);
                if (i == lines.Length - 1) break;

                var next = lines[i + 1].TrimStart();
                if (cur.Length == 0 || next.Length == 0) { sb.Append('\n'); continue; }

                bool endsSentence = Regex.IsMatch(cur, @"[.!?;:。！？；：""')\]】》]$");
                bool nextStartsNew = Regex.IsMatch(next, @"^[A-Z0-9\-•*•]") || IsCjk(next[0]);

                if (cur.EndsWith("-"))                       // 断词连字符：直接粘合
                {
                    sb.Length -= 1;
                }
                else if (!endsSentence && !nextStartsNew)
                {
                    sb.Append(IsCjk(cur[cur.Length - 1]) ? "" : " ");
                }
                else sb.Append('\n');
            }
            return sb.ToString();
        }

        public static bool IsCjk(char ch)
        {
            return (ch >= 0x4E00 && ch <= 0x9FFF) || (ch >= 0x3400 && ch <= 0x4DBF)
                || (ch >= 0x3000 && ch <= 0x303F) || (ch >= 0xFF00 && ch <= 0xFFEF);
        }

        /// <summary>中日韩字符占比，用于判断翻译方向</summary>
        public static double CjkRatio(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            int cjk = 0, total = 0;
            foreach (var ch in s)
            {
                if (char.IsWhiteSpace(ch) || char.IsPunctuation(ch)) continue;
                total++;
                if (IsCjk(ch)) cjk++;
            }
            return total == 0 ? 0 : (double)cjk / total;
        }

        /// <summary>是否为单个英文单词（触发词典增强）</summary>
        public static bool IsSingleWord(string s)
        {
            return !string.IsNullOrEmpty(s) && s.Length <= 32
                && Regex.IsMatch(s.Trim(), @"^[a-zA-Z][a-zA-Z'\-]*$");
        }
    }
}
