using System;
using System.Threading;
using System.Windows.Forms;

namespace LiteTrans
{
    public partial class SettingsForm
    {
        private void PageSpeech(PageBuilder p)
        {
            p.Section("朗读");
            p.Switch("翻译后自动朗读", "AutoSpeak", "单词读原文，句子读译文");
            p.Slider("语速", "SpeakRate", -10, 10);
            p.Slider("音量", "SpeakVolume", 0, 100, " %");
            p.TextField("自动朗读长度上限", "AutoSpeakMaxLen", 110);
            p.Note("超过该字符数则不自动朗读，仍可点朗读按钮或按 Ctrl+D 手动播放。");
            p.ButtonRow("", "试听一下", delegate
            {
                CommitEditors();
                Speech.Speak("轻译已就绪，Translation is ready.", _c);
            });

            p.Section("历史记录");
            p.Switch("保存翻译历史", "KeepHistory", "可从托盘菜单快速回看");
            p.TextField("最多保存条数", "HistoryMax", 110);
            p.ButtonRow("", "清空历史记录", delegate
            {
                if (MessageBox.Show("确定清空全部翻译历史？", "轻译",
                        MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
                {
                    History.Clear();
                    MessageBox.Show("已清空。", "轻译", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            });
        }

        private void PageAdvanced(PageBuilder p)
        {
            p.Section("AI 精翻（OpenAI 兼容接口）");
            p.Switch("启用 AI 精翻", "AiEnabled", "长句、术语和语境表现更好");
            p.TextField("接口地址", "AiBaseUrl", 350);
            p.TextField("API Key", "AiKey", 350, true);
            p.TextField("模型名称", "AiModel", 220);
            p.TextField("系统提示词", "AiPrompt", 350);
            p.Note("地址填到 /v1 即可，程序自动补 /chat/completions；{target} 会替换为目标语言。");
            p.ButtonRow("", "测试 AI 接口", delegate { TestEngine("ai"); }, 150);

            p.Section("百度翻译开放平台");
            p.TextField("APP ID", "BaiduAppId", 220);
            p.TextField("密钥", "BaiduKey", 220, true);
            p.LinkNote("在 ", "https://fanyi-api.baidu.com", " 申请，标准版每月有免费额度。不填则不使用。",
                "https://fanyi-api.baidu.com/manage/developer");
            p.ButtonRow("", "测试百度翻译", delegate { TestEngine("baidu"); }, 150);

            p.Section("连通性测试");
            p.ButtonRow("", "测试首选引擎", delegate { TestEngine(null); }, 150);
            p.ButtonRow("", "打开配置文件夹", delegate
            {
                try
                {
                    var dir = System.IO.Path.GetDirectoryName(Config.FilePath);
                    System.IO.Directory.CreateDirectory(dir);
                    System.Diagnostics.Process.Start("explorer.exe", dir);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("打开失败：" + ex.Message, "轻译");
                }
            }, 150);
        }

        /// <summary>用当前未保存的设置真正跑一次翻译，直接反馈结果</summary>
        private void TestEngine(string engine)
        {
            CommitEditors();
            var cfg = _c.Clone();
            var requested = string.IsNullOrWhiteSpace(engine) ? _c.Engine : engine;
            if (!string.IsNullOrWhiteSpace(requested)) cfg.Engine = requested;
            if (requested == "ai") cfg.AiEnabled = true;

            var dlg = new Form
            {
                Text = "连通性测试",
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                ClientSize = new System.Drawing.Size(430, 150),
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Theme.Current.Bg,
                ForeColor = Theme.Current.Text,
                Font = new System.Drawing.Font("Microsoft YaHei UI", 9f),
            };
            var lbl = new Label
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                Text = "正在测试 …",
                ForeColor = Theme.Current.Text,
            };
            dlg.Controls.Add(lbl);
            dlg.HandleCreated += delegate
            {
                Native.ApplyDarkTitleBar(dlg.Handle, Theme.Current.Dark);
                Native.ApplyRoundCorners(dlg.Handle);
            };

            dlg.Shown += delegate
            {
                ThreadPool.QueueUserWorkItem(delegate
                {
                    var r = Translator.Translate("Knowledge is power.", cfg, false, requested);
                    try
                    {
                        dlg.BeginInvoke((MethodInvoker)delegate
                        {
                            var fallback = string.IsNullOrWhiteSpace(r.FallbackNotice)
                                ? ""
                                : "\n\n提示：" + r.FallbackNotice;
                            var actual = string.IsNullOrWhiteSpace(r.Engine) ? "未返回" : r.Engine;
                            lbl.Text = r.Ok && (string.IsNullOrWhiteSpace(requested) || r.Engine == requested)
                                ? "成功\n\n引擎：" + actual + "\n耗时：" + r.ElapsedMs + " ms\n译文：" + r.Text + fallback
                                : "失败\n\n请求引擎：" + (requested ?? cfg.Engine) + "\n"
                                  + (r.Error ?? (r.FallbackNotice ?? "未返回目标引擎结果")) + fallback;
                        });
                    }
                    catch { }
                });
            };

            dlg.ShowDialog(this);
        }
    }
}
