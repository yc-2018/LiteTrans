using System;
using System.Speech.Synthesis;

namespace LiteTrans
{
    /// <summary>SAPI 朗读，按内容自动挑选中英文发音人</summary>
    public static class Speech
    {
        private static SpeechSynthesizer _s;
        private static readonly object Lock = new object();

        private static SpeechSynthesizer Engine
        {
            get
            {
                if (_s == null) _s = new SpeechSynthesizer();
                return _s;
            }
        }

        public static bool Available
        {
            get { try { return Engine != null; } catch { return false; } }
        }

        public static void Speak(string text, Config c)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            try
            {
                lock (Lock)
                {
                    var e = Engine;
                    e.SpeakAsyncCancelAll();
                    e.Rate = Math.Max(-10, Math.Min(10, c.SpeakRate));
                    e.Volume = Math.Max(0, Math.Min(100, c.SpeakVolume));
                    SelectVoice(e, TextPrep.CjkRatio(text) > 0.2);
                    e.SpeakAsync(text.Length > 1000 ? text.Substring(0, 1000) : text);
                }
            }
            catch { }
        }

        public static void Stop()
        {
            try { lock (Lock) if (_s != null) _s.SpeakAsyncCancelAll(); } catch { }
        }

        private static void SelectVoice(SpeechSynthesizer e, bool chinese)
        {
            try
            {
                foreach (var v in e.GetInstalledVoices())
                {
                    if (!v.Enabled) continue;
                    var culture = v.VoiceInfo.Culture.Name;
                    bool isZh = culture.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
                    if (isZh == chinese) { e.SelectVoice(v.VoiceInfo.Name); return; }
                }
            }
            catch { }
        }
    }
}
