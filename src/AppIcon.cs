using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace LiteTrans
{
    /// <summary>运行时绘制图标，省掉外部 .ico 文件，保持单文件分发</summary>
    public static class AppIcon
    {
        [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr h);

        private static Icon _cached;
        private static bool _cachedDark;

        public static Icon Get(bool dark)
        {
            if (_cached != null && _cachedDark == dark) return _cached;
            _cached = Build(dark ? Color.FromArgb(98, 155, 255) : Color.FromArgb(79, 140, 255));
            _cachedDark = dark;
            return _cached;
        }

        public static Bitmap Draw(int size, Color accent)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.Transparent);

                float r = size * 0.24f;
                using (var path = Rounded(new RectangleF(0, 0, size - 1, size - 1), r))
                using (var brush = new LinearGradientBrush(
                    new RectangleF(0, 0, size, size),
                    Lighten(accent, 0.18f), Darken(accent, 0.12f), 55f))
                {
                    g.FillPath(brush, path);
                }

                // 字形用“译”，小尺寸下退化为 A 字更清晰
                var glyph = size >= 24 ? "译" : "A";
                var fontName = size >= 24 ? "Microsoft YaHei UI" : "Segoe UI";
                using (var f = new Font(fontName, size * 0.56f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    var rect = new RectangleF(0, size * 0.02f, size, size);
                    using (var shadow = new SolidBrush(Color.FromArgb(70, 0, 0, 0)))
                        g.DrawString(glyph, f, shadow, new RectangleF(0.6f, size * 0.02f + 0.6f, size, size), sf);
                    g.DrawString(glyph, f, Brushes.White, rect, sf);
                }
            }
            return bmp;
        }

        private static Icon Build(Color accent)
        {
            using (var bmp = Draw(32, accent))
            {
                var h = bmp.GetHicon();
                try
                {
                    using (var tmp = Icon.FromHandle(h))
                        return (Icon)tmp.Clone();     // 克隆后即可释放句柄
                }
                finally { DestroyIcon(h); }
            }
        }

        public static GraphicsPath Rounded(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = radius * 2;
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static Color Lighten(Color c, float k)
        {
            return Color.FromArgb(c.A,
                (int)Math.Min(255, c.R + 255 * k),
                (int)Math.Min(255, c.G + 255 * k),
                (int)Math.Min(255, c.B + 255 * k));
        }

        public static Color Darken(Color c, float k)
        {
            return Color.FromArgb(c.A,
                (int)Math.Max(0, c.R - 255 * k),
                (int)Math.Max(0, c.G - 255 * k),
                (int)Math.Max(0, c.B - 255 * k));
        }

        public static Color Mix(Color a, Color b, float t)
        {
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }
    }
}
