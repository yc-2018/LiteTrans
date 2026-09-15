using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;

namespace LiteTrans
{
    public partial class SettingsForm
    {
        private static List<KeyValuePair<string, string>> Opt(params string[] pairs)
        {
            var l = new List<KeyValuePair<string, string>>();
            for (int i = 0; i + 1 < pairs.Length; i += 2)
                l.Add(new KeyValuePair<string, string>(pairs[i], pairs[i + 1]));
            return l;
        }

        private void PageGeneral(PageBuilder p)
        {
            p.Section("快捷键");
            p.HotkeyField("翻译剪贴板", "Hotkey");
            p.HotkeyField("打开空白输入框", "HotkeyInput", true);
            p.Note("快捷键必须包含 Ctrl / Alt / Shift 之一；若提示被占用，换一个组合即可。");

            p.Section("启动");
            p.Switch("开机自动启动", "AutoStart", "写入当前用户启动项");
            p.Switch("启动后直接隐藏", "StartHidden", "不显示主窗口");
            p.Switch("只允许一个实例", "SingleInstance", "重复启动时唤起已有窗口");

            p.Section("窗口行为");
            p.Switch("按 Esc 收回托盘", "EscToTray");
            p.Switch("失去焦点时隐藏", "HideOnFocusLost", "点其他窗口就自动收起");
            p.Switch("窗口置顶", "TopMost");
            p.Switch("弹出时读取剪贴板", "ReadClipboardOnShow");
            p.Switch("复制即翻译", "MonitorClipboard", "监听剪贴板变化并自动弹出");
            p.Switch("译文自动写回剪贴板", "AutoCopyResult");
        }

        private void PageSelection(PageBuilder p)
        {
            p.Section("划词翻译");
            p.Switch("选中即翻译", "GrabSelection", "按热键时自动取走选中文字，不用先复制");
            p.Switch("用完还原剪贴板", "RestoreClipboard", "不覆盖你原本复制的内容");
            p.TextField("取词等待（毫秒）", "GrabWaitMs", 110);

            p.Section("工作方式");
            p.Note("按下热键时，程序优先直接读取选中的文字，这种方式不经过剪贴板、也不会产生按键副作用，"
                 + "浏览器、Office、记事本、编辑器等绝大多数程序都支持。");
            p.Note("少数程序（部分 PDF 阅读器、终端、老式控件）无法直接读取，此时会自动改为替你按一次 "
                 + "Ctrl+C 取词，取完立即把剪贴板还原成你原本的内容。");
            p.Note("若某个程序取词失败，把上面的取词等待调到 400~600 毫秒再试。"
                 + "没有选中任何文字时，会自动退回翻译剪贴板里的内容。");
        }

        private void PageAppearance(PageBuilder p)
        {
            p.Section("主题");
            p.Combo("配色方案", "Theme", Opt("auto", "跟随系统", "light", "浅色", "dark", "深色"));
            p.TextField("主题色", "Accent", 120);
            p.Note("主题色填 16 进制，例如 #4F8CFF。留空则使用默认蓝。");

            p.Section("字体与尺寸");
            p.Combo("界面字体", "FontFamily", FontOptions(), 220);
            p.Slider("字号", "FontSize", 10, 22, " pt");
            p.Slider("不透明度", "Opacity", 60, 100, " %");
            p.Switch("紧凑模式", "CompactMode", "原文区更矮，留更多空间给译文");

            p.Section("弹出位置");
            p.Combo("弹出时定位到", "ShowPosition",
                Opt("center", "屏幕中央", "cursor", "鼠标附近", "remember", "记住上次位置"));
        }

        private void PageTranslate(PageBuilder p)
        {
            p.Section("语言");
            p.Combo("目标语言", "TargetLang", Lang.All);
            p.Combo("反向语言", "PivotLang", Lang.All);
            p.Switch("自动判断方向", "AutoSwapCJK", "中文内容自动译成反向语言");
            p.Note("例：目标=简体中文、反向=英语时，英文译成中文，中文则译成英文。");

            p.Section("引擎");
            p.Combo("首选引擎", "Engine",
                Opt("transmart", "腾讯翻译（免密钥，推荐）", "ai", "AI 精翻（需配置）", "baidu", "百度翻译（需密钥）"), 260);
            p.Switch("单词词典增强", "DictEnhance", "查单词时附带音标和分词性释义");
            p.TextField("请求超时（毫秒）", "TimeoutMs", 110);
            p.Note("任一引擎失败会自动回退到腾讯翻译，因此始终可用。");

            p.Section("剪贴板文本预处理");
            p.Switch("驼峰命名拆词", "SplitCamel", "getUserName → get User Name");
            p.Switch("下划线转空格", "UnderscoreToSpace");
            p.Switch("合并多余空白", "CollapseSpaces");
            p.Switch("接合排版换行", "JoinLineBreaks", "修复 PDF 复制出来的断行");
            p.Switch("去除注释符号", "StripCodeComment", "剥掉行首的 // # * 等");
        }

        private static List<KeyValuePair<string, string>> FontOptions()
        {
            var want = new[]
            {
                "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI Variable Text",
                "Segoe UI", "等线", "思源黑体 CN", "HarmonyOS Sans SC", "Sarasa UI SC", "SimSun"
            };
            var installed = new List<string>();
            try
            {
                using (var ifc = new InstalledFontCollection())
                    foreach (var f in ifc.Families) installed.Add(f.Name);
            }
            catch { }

            var list = new List<KeyValuePair<string, string>>();
            foreach (var w in want)
                if (installed.Count == 0 || installed.Contains(w))
                    list.Add(new KeyValuePair<string, string>(w, w));

            if (list.Count == 0)
                list.Add(new KeyValuePair<string, string>("Microsoft YaHei UI", "Microsoft YaHei UI"));
            return list;
        }
    }
}
