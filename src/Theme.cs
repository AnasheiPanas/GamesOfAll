using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GamesOfAll
{
    // Paleta, tipografias y utilidades de dibujo compartidas por toda la interfaz.
    internal static class Theme
    {
        public static float S = 1f; // factor de escala DPI

        public static Color Bg = Color.FromArgb(8, 10, 14);
        public static Color BgSoft = Color.FromArgb(13, 16, 21);
        public static Color Surface = Color.FromArgb(20, 24, 31);
        public static Color SurfaceHover = Color.FromArgb(30, 35, 44);
        public static Color Stroke = Color.FromArgb(37, 43, 54);
        public static Color StrokeSoft = Color.FromArgb(27, 32, 41);
        public static Color Text = Color.FromArgb(236, 240, 246);
        public static Color TextDim = Color.FromArgb(138, 148, 164);
        public static Color TextFaint = Color.FromArgb(92, 101, 116);
        public static Color Accent = Color.FromArgb(96, 200, 255);
        public static Color AccentDeep = Color.FromArgb(70, 130, 255);
        public static Color Accent2 = Color.FromArgb(170, 120, 255);
        public static Color Danger = Color.FromArgb(255, 104, 104);
        public static bool IsLight;

        public static void Apply(int theme)
        {
            if (theme == 1)
            {
                IsLight = true;
                Bg = Color.FromArgb(246, 247, 249);
                BgSoft = Color.FromArgb(238, 240, 244);
                Surface = Color.FromArgb(255, 255, 255);
                SurfaceHover = Color.FromArgb(232, 236, 242);
                Stroke = Color.FromArgb(205, 211, 220);
                StrokeSoft = Color.FromArgb(220, 224, 230);
                Text = Color.FromArgb(28, 32, 38);
                TextDim = Color.FromArgb(92, 100, 112);
                TextFaint = Color.FromArgb(132, 139, 150);
                Accent = Color.FromArgb(48, 132, 210);
                AccentDeep = Color.FromArgb(36, 92, 180);
                Accent2 = Color.FromArgb(120, 78, 188);
                Danger = Color.FromArgb(210, 74, 74);
            }
            else if (theme == 2)
            {
                IsLight = false;
                Bg = Color.FromArgb(12, 10, 10);
                BgSoft = Color.FromArgb(24, 13, 14);
                Surface = Color.FromArgb(38, 20, 21);
                SurfaceHover = Color.FromArgb(58, 27, 29);
                Stroke = Color.FromArgb(82, 39, 42);
                StrokeSoft = Color.FromArgb(48, 25, 26);
                Text = Color.FromArgb(248, 238, 238);
                TextDim = Color.FromArgb(190, 150, 152);
                TextFaint = Color.FromArgb(145, 112, 114);
                Accent = Color.FromArgb(238, 62, 72);
                AccentDeep = Color.FromArgb(174, 28, 38);
                Accent2 = Color.FromArgb(255, 126, 91);
                Danger = Color.FromArgb(255, 96, 96);
            }
            else
            {
                IsLight = false;
                Bg = Color.FromArgb(8, 10, 14);
                BgSoft = Color.FromArgb(13, 16, 21);
                Surface = Color.FromArgb(20, 24, 31);
                SurfaceHover = Color.FromArgb(30, 35, 44);
                Stroke = Color.FromArgb(37, 43, 54);
                StrokeSoft = Color.FromArgb(27, 32, 41);
                Text = Color.FromArgb(236, 240, 246);
                TextDim = Color.FromArgb(138, 148, 164);
                TextFaint = Color.FromArgb(92, 101, 116);
                Accent = Color.FromArgb(96, 200, 255);
                AccentDeep = Color.FromArgb(70, 130, 255);
                Accent2 = Color.FromArgb(170, 120, 255);
                Danger = Color.FromArgb(255, 104, 104);
            }
        }

        public static Font Logo;
        public static Font Title;
        public static Font Subtitle;
        public static Font Body;
        public static Font BodyBold;
        public static Font Small;
        public static Font SmallBold;
        public static Font CardTitle;
        public static Font CardBadge;
        public static Font CardInitials;
        public static Font Star;
        public static Font IconGlyph;

        public static void Init(float scale)
        {
            S = scale <= 0f ? 1f : scale;
            Logo = MakeFont("Segoe UI", 16.5f, FontStyle.Bold);
            Title = MakeFont("Segoe UI Semibold", 13f, FontStyle.Regular);
            Subtitle = MakeFont("Segoe UI", 9.5f, FontStyle.Regular);
            Body = MakeFont("Segoe UI", 10f, FontStyle.Regular);
            BodyBold = MakeFont("Segoe UI", 10f, FontStyle.Bold);
            Small = MakeFont("Segoe UI", 8.5f, FontStyle.Regular);
            SmallBold = MakeFont("Segoe UI", 8.5f, FontStyle.Bold);
            CardTitle = MakeFont("Segoe UI Semibold", 10f, FontStyle.Regular);
            CardBadge = MakeFont("Segoe UI", 7.5f, FontStyle.Bold);
            CardInitials = MakeFont("Segoe UI", 34f, FontStyle.Bold);
            Star = MakeFont("Segoe UI", 13f, FontStyle.Regular);
            IconGlyph = MakeFont("Segoe MDL2 Assets", 10f, FontStyle.Regular);
        }

        public static Font MakeFont(string family, float size, FontStyle style)
        {
            try { return new Font(family, size * S, style, GraphicsUnit.Point); }
            catch { return new Font("Segoe UI", size * S, style, GraphicsUnit.Point); }
        }

        public static int Px(float v) { return (int)Math.Round(v * S); }
        public static float Pxf(float v) { return v * S; }

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            GraphicsPath p = new GraphicsPath();
            float d = radius * 2f;
            if (radius <= 0.2f || r.Width <= d || r.Height <= d)
            {
                p.AddRectangle(r);
                return p;
            }
            p.AddArc(r.X, r.Y, d, d, 180f, 90f);
            p.AddArc(r.Right - d, r.Y, d, d, 270f, 90f);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0f, 90f);
            p.AddArc(r.X, r.Bottom - d, d, d, 90f, 90f);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, RectangleF r, float radius, Color color)
        {
            using (GraphicsPath p = Round(r, radius))
            using (SolidBrush b = new SolidBrush(color))
                g.FillPath(b, p);
        }

        public static void FillRoundGradient(Graphics g, RectangleF r, float radius, Color top, Color bottom)
        {
            using (GraphicsPath p = Round(r, radius))
            using (LinearGradientBrush b = new LinearGradientBrush(
                new RectangleF(r.X, r.Y - 1f, r.Width, r.Height + 2f), top, bottom, LinearGradientMode.Vertical))
                g.FillPath(b, p);
        }

        public static void StrokeRound(Graphics g, RectangleF r, float radius, Color color, float width)
        {
            using (GraphicsPath p = Round(r, radius))
            using (Pen pen = new Pen(color, width))
            {
                pen.Alignment = PenAlignment.Inset;
                g.DrawPath(pen, p);
            }
        }

        // Dibuja un texto con recorte por puntos suspensivos dentro del rectangulo.
        public static void DrawText(Graphics g, string text, Font font, Color color, Rectangle bounds,
            TextFormatFlags flags)
        {
            TextRenderer.DrawText(g, text, font, bounds, color, flags | TextFormatFlags.NoPadding);
        }

        public static void DrawTextCenter(Graphics g, string text, Font font, Color color, Rectangle bounds)
        {
            TextRenderer.DrawText(g, text, font, bounds, color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }

        public static Color PlatformColor(Platform platform)
        {
            switch (platform)
            {
                case Platform.Steam: return Color.FromArgb(102, 192, 244);
                case Platform.Epic: return Color.FromArgb(206, 212, 224);
                case Platform.Riot: return Color.FromArgb(255, 86, 92);
                default: return Color.FromArgb(126, 226, 168);
            }
        }

        public static Color Blend(Color a, Color b, float t)
        {
            if (t < 0f) t = 0f; if (t > 1f) t = 1f;
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t),
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }
    }
}
