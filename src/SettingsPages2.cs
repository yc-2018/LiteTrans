using System;
using System.Collections.Generic;
using System.Drawing;
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

            // AI 配置通常较长，放在最后，避免把常用的连通性测试挤到窗口底部。
            p.Section("AI 精翻（OpenAI 兼容接口）");
            p.Note("可添加多个接口并自定义名称。地址填到 /v1 即可，程序自动补 /chat/completions；{target} 会替换为目标语言。启用的接口会出现在主窗口引擎列表中。");
            BuildAiProviders(p);
        }

        private void BuildAiProviders(PageBuilder p)
        {
            _c.NormalizeAiProviders();
            // 这块区域刻意不自带滚动条：页面本身已可滚动，再套一层滚动区域后
            // 两层各滚到各自尽头，最后一张卡片的“测试 / 删除”按钮就露不全。
            // 改为高度随卡片数量增长，统一由页面的滚动条负责。
            var host = new Panel
            {
                Width = 596,
                AutoScroll = false,
                BackColor = Theme.Current.Bg,
            };
            var list = new FlowLayoutPanel
            {
                Location = new Point(0, 38),
                Width = 592,
                AutoScroll = false,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Theme.Current.Bg,
            };
            var editors = new List<AiProviderEditor>();
            var add = new FlatBtn { Text = "新增 AI 配置", Size = new Size(130, 29), Location = new Point(0, 2) };
            host.Controls.Add(add);
            host.Controls.Add(list);

            Action rebuild = null;
            rebuild = delegate
            {
                host.SuspendLayout();
                while (list.Controls.Count > 0)
                {
                    var child = list.Controls[list.Controls.Count - 1];
                    list.Controls.RemoveAt(list.Controls.Count - 1);
                    child.Dispose();
                }
                editors.Clear();
                foreach (var provider in _c.AiProviders)
                {
                    var editor = new AiProviderEditor(provider);
                    editor.TestRequested += delegate { CommitEditors(); TestEngine(Config.AiEngineKey(provider)); };
                    editor.RemoveRequested += delegate
                    {
                        CommitEditors();
                        _c.AiProviders.Remove(provider);
                        rebuild();
                    };
                    editors.Add(editor);
                    editor.Width = Math.Max(300, list.Width - 6);
                    list.Controls.Add(editor);
                }
                list.Height = editors.Count * AiProviderEditor.Stride;
                // 末尾多留一点空白，滚到底时最后一张卡片的按钮不会贴着窗口边缘
                host.Height = list.Top + list.Height + 12;
                host.ResumeLayout();
                // 卡片数量变了，页面的滚动范围要跟着重算，否则最后一张卡片
                // 的“测试 / 删除”按钮会卡在视口外滚不出来
                var owner = host.Parent as ScrollableControl;
                if (owner != null)
                {
                    var want = new Size(0, host.Bottom + 8);
                    if (owner.AutoScrollMinSize != want) owner.AutoScrollMinSize = want;
                }
            };

            add.Click += delegate
            {
                CommitEditors();
                _c.AiProvidersInitialized = true;
                _c.AiProviders.Add(new AiProviderConfig
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = "AI 配置 " + (_c.AiProviders.Count + 1),
                    Enabled = true,
                });
                rebuild();
            };
            rebuild();
            p.AddControl(host, host.Height, 8);
            // 窗口宽度变化时，卡片跟着页面一起伸缩，避免右边框被裁掉
            p.WidthChanged += delegate(int w)
            {
                list.Width = Math.Max(320, w - 4);
                foreach (var editor in editors) editor.Width = list.Width - 6;
            };
            list.Width = Math.Max(320, p.ContentWidth - 4);
            foreach (var e in editors) e.Width = list.Width - 6;
            p.AddCommit(delegate
            {
                foreach (var editor in editors) editor.Commit();
                _c.NormalizeAiProviders();
            });
        }

        /// <summary>用当前未保存的设置真正跑一次翻译，直接反馈结果</summary>
        private void TestEngine(string engine)
        {
            CommitEditors();
            var cfg = _c.Clone();
            var requested = string.IsNullOrWhiteSpace(engine) ? _c.Engine : engine;
            if (!string.IsNullOrWhiteSpace(requested)) cfg.Engine = requested;
            if (requested == "ai" || (requested ?? "").StartsWith("ai:", StringComparison.OrdinalIgnoreCase))
            {
                var provider = cfg.FindAiProvider(requested);
                if (provider != null) provider.Enabled = true;
            }

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

    /// <summary>单个 AI 接口的可编辑卡片。</summary>
    internal sealed class AiProviderEditor : Panel
    {
        private const int CardHeight = 236;
        private const int GapBelow = 8;

        /// <summary>一张卡片在列表里占用的垂直空间（含卡片之间的间距）。</summary>
        public const int Stride = CardHeight + GapBelow;

        private readonly AiProviderConfig _provider;
        private readonly TextBox _name, _url, _key, _model, _prompt;
        private readonly Toggle _enabled;
        private readonly Label _enableLabel;
        public event EventHandler TestRequested;
        public event EventHandler RemoveRequested;

        public AiProviderEditor(AiProviderConfig provider)
        {
            _provider = provider;
            Width = 584;
            Height = CardHeight;
            Margin = new Padding(0, 0, 0, GapBelow);
            BackColor = Theme.Current.Card;
            BorderStyle = BorderStyle.FixedSingle;

            _name = Field("名称", provider.Name, 8, 8, 310);
            _url = Field("接口地址", provider.BaseUrl, 8, 42, 566);
            _key = Field("API Key", provider.Key, 8, 76, 566, true);
            _model = Field("模型", provider.Model, 8, 110, 566);
            _prompt = Field("提示词", provider.Prompt, 8, 144, 566);

            _enableLabel = new Label { Text = "启用", AutoSize = true, Location = new Point(410, 13), ForeColor = Theme.Current.Text, BackColor = Color.Transparent };
            _enabled = new Toggle { Location = new Point(450, 10), Checked = provider.Enabled };
            Controls.Add(_enableLabel);
            Controls.Add(_enabled);

            var test = new FlatBtn { Text = "测试", Size = new Size(70, 27), Location = new Point(8, 197) };
            test.Click += delegate { if (TestRequested != null) TestRequested(this, EventArgs.Empty); };
            var remove = new FlatBtn { Text = "删除", Size = new Size(70, 27), Location = new Point(86, 197) };
            remove.Click += delegate { if (RemoveRequested != null) RemoveRequested(this, EventArgs.Empty); };
            Controls.Add(test);
            Controls.Add(remove);
            OnResize(EventArgs.Empty);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_url == null) return;   // 构造期间设置 Width 会先触发一次
            // 卡片宽度跟随窗口，输入框右端始终贴着卡片内边距
            int right = ClientSize.Width - 10;
            int left = 8 + 80;
            int full = Math.Max(140, right - left);
            _url.Width = full;
            _key.Width = full;
            _model.Width = full;
            _prompt.Width = full;

            // 开关比输入框视觉更重，右端比输入框再内缩一点，不要贴着卡片边框
            _enabled.Left = Math.Max(left + 110, right - _enabled.Width - 6);
            _enableLabel.Left = _enabled.Left - 36;
            _name.Width = Math.Max(120, _enableLabel.Left - 12 - left);
        }

        private TextBox Field(string label, string value, int x, int y, int width, bool password = false)
        {
            var l = new Label { Text = label, AutoSize = false, Size = new Size(74, 25), Location = new Point(x, y + 3), ForeColor = Theme.Current.Text, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleLeft };
            var tb = new TextBox { BorderStyle = BorderStyle.FixedSingle, Location = new Point(x + 80, y), Width = width - 80, Height = 25, Text = value ?? "", BackColor = Theme.Current.Bg, ForeColor = Theme.Current.Text, UseSystemPasswordChar = password };
            Controls.Add(l);
            Controls.Add(tb);
            return tb;
        }

        public void Commit()
        {
            _provider.Name = (_name.Text ?? "").Trim();
            _provider.BaseUrl = (_url.Text ?? "").Trim();
            _provider.Key = (_key.Text ?? "").Trim();
            _provider.Model = (_model.Text ?? "").Trim();
            _provider.Prompt = _prompt.Text ?? "";
            _provider.Enabled = _enabled.Checked;
        }
    }
}
