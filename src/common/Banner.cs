using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace Blood2HD
{
    /// <summary>The title banner of launcher and setup: game title plus the console artwork of the installed game.</summary>
    static class Banner
    {
        /// <summary>console.PCX from the game folder (the game's own console backdrop), or null.</summary>
        public static Bitmap Load(string dir)
        {
            try
            {
                return string.IsNullOrEmpty(dir) ? null : Pcx.Load(Path.Combine(dir, "console.PCX"));
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void Paint(Graphics g, Rectangle r, Bitmap art, string tag)
        {
            g.Clear(Theme.Back);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            if (art != null)
            {
                // the artwork on the right, faded into the dark page on the left
                int w = Math.Min(r.Width / 2, r.Height * art.Width / art.Height);
                var dst = new Rectangle(r.Right - w, r.Top, w, r.Height);
                int sh = art.Width * r.Height / w;
                g.DrawImage(art, dst, new Rectangle(0, Math.Max(0, (art.Height - sh) / 2), art.Width, Math.Min(art.Height, sh)), GraphicsUnit.Pixel);
                using (var fade = new LinearGradientBrush(new Rectangle(dst.Left - 1, r.Top, w / 2 + 2, r.Height), Theme.Back, Color.FromArgb(0, Theme.Back), LinearGradientMode.Horizontal))
                    g.FillRectangle(fade, dst.Left, r.Top, w / 2, r.Height);
            }
            int fh = Theme.S(30);
            using (var fade = new LinearGradientBrush(new Rectangle(r.Left, r.Bottom - fh - 1, r.Width, fh + 2), Color.FromArgb(0, Theme.Back), Theme.Back, LinearGradientMode.Vertical))
                g.FillRectangle(fade, r.Left, r.Bottom - fh, r.Width, fh);

            // the title block is laid out for a 150-unit banner and shrinks with smaller ones
            float k = r.Height / (float)Theme.S(150);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using (var big = Theme.Display(34f * k))
            using (var sub = Theme.Display(13f * k, FontStyle.Italic))
            using (var type = new Font("Courier New", Theme.Pt(9f * Math.Max(k, 0.85f)), FontStyle.Bold))
            using (var shadow = new SolidBrush(Color.FromArgb(200, 0, 0, 0)))
            using (var blood = new SolidBrush(Theme.AccentLight))
            using (var bone = new SolidBrush(Theme.Brass))
            using (var text = new SolidBrush(Theme.Text))
            {
                Func<float, float> s = v => Theme.S(v * k);
                g.DrawString("BLOOD II", big, shadow, s(24), r.Top + s(22));
                g.DrawString("BLOOD II", big, blood, s(22), r.Top + s(20));
                g.DrawString("The Chosen  ·  The Nightmare Levels", sub, bone, s(26), r.Top + s(80));
                Theme.Rule(g, (int)s(26), (int)s(330), r.Top + (int)s(112));
                g.DrawString(tag, type, text, s(26), r.Top + s(122));
            }
        }
    }

    /// <summary>Reader for 8-bit ZSoft PCX images (RLE, 256-colour palette at the end).</summary>
    static class Pcx
    {
        public static Bitmap Load(string path)
        {
            var b = File.ReadAllBytes(path);
            if (b.Length < 128 + 769 || b[0] != 10 || b[2] != 1 || b[3] != 8 || b[65] != 1 || b[b.Length - 769] != 12)
                return null;
            int w = BitConverter.ToUInt16(b, 8) - BitConverter.ToUInt16(b, 4) + 1;
            int h = BitConverter.ToUInt16(b, 10) - BitConverter.ToUInt16(b, 6) + 1;
            int stride = BitConverter.ToUInt16(b, 66);
            var pixels = new byte[stride * h];
            int src = 128, dst = 0, end = b.Length - 769;
            while (dst < pixels.Length && src < end)
            {
                byte v = b[src++];
                int run = 1;
                if ((v & 0xC0) == 0xC0)
                {
                    run = v & 0x3F;
                    v = src < end ? b[src++] : (byte)0;
                }
                while (run-- > 0 && dst < pixels.Length)
                    pixels[dst++] = v;
            }
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppRgb);
            var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
            var row = new int[w];
            int pal = b.Length - 768;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = pixels[y * stride + x] * 3;
                    row[x] = b[pal + i] << 16 | b[pal + i + 1] << 8 | b[pal + i + 2];
                }
                Marshal.Copy(row, 0, data.Scan0 + y * data.Stride, w);
            }
            bmp.UnlockBits(data);
            return bmp;
        }
    }
}
