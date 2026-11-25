using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GamesOfAll
{
    // Tarjeta de juego dibujada a mano: caratula a sangre, degradado inferior,
    // titulo, etiqueta de plataforma y accion al pasar el raton.
    internal sealed class GameCard : Control
    {
        public Game Game;
        public Image Cover;
        public bool IsSelected;
        public bool IsFavorite;
        public bool IsNew;
        public long PlaySeconds;

        private float hoverProgress;
        private float hoverTarget;
        private readonly Timer hoverTimer;

        public event EventHandler Activated;

        public GameCard(Game game)
        {
            Game = game;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.Selectable | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            Cursor = Cursors.Hand;
            TabStop = true;
            BackColor = Theme.Bg;
            hoverTimer = new Timer();
            hoverTimer.Interval = 15;
            hoverTimer.Tick += HoverTimer_Tick;
        }

        protected override void OnMouseEnter(EventArgs e) { SetHover(true); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { SetHover(false); base.OnMouseLeave(e); }

        private void SetHover(bool value)
        {
            hoverTarget = value ? 1f : 0f;
            if (!hoverTimer.Enabled) hoverTimer.Start();
        }

        private void HoverTimer_Tick(object sender, EventArgs e)
        {
            float delta = hoverTarget - hoverProgress;
            if (Math.Abs(delta) < 0.04f)
            {
                hoverProgress = hoverTarget;
                hoverTimer.Stop();
            }
            else hoverProgress += delta * 0.16f;
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (Grid != null) Grid.ScrollBy(e.Delta > 0 ? -Theme.Px(150) : Theme.Px(150));
        }

        protected override void OnClick(EventArgs e)
        {
            if (Activated != null) Activated(this, EventArgs.Empty);
            base.OnClick(e);
        }

        private GridPanel Grid
        {
            get { return Parent as GridPanel; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && Cover != null)
            {
                try { Cover.Dispose(); } catch { }
                Cover = null;
            }
            if (disposing) hoverTimer.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            RectangleF rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            float radius = Theme.Pxf(12f);
            Color surface = IsSelected ? Theme.SurfaceHover :
                Theme.Blend(Theme.Surface, Theme.SurfaceHover, hoverProgress);
            Theme.FillRound(g, rect, radius, surface);

            RectangleF coverRect = new RectangleF(1f, 1f, Width - 2f, Height - 2f);
            using (GraphicsPath clip = Theme.Round(coverRect, radius - 1f))
            {
                GraphicsState state = g.Save();
                g.SetClip(clip, CombineMode.Intersect);
                if (Cover != null)
                {
                    DrawCover(g, Cover, coverRect);
                }
                else
                {
                    DrawFallback(g, coverRect);
                }

                // Sombra inferior para que el texto siempre se lea
                int shadeTop = (int)(Height * 0.48f);
                using (LinearGradientBrush shade = new LinearGradientBrush(
                    new Rectangle(0, shadeTop, Width, Height - shadeTop),
                    Color.FromArgb(0, 0, 0, 0), Color.FromArgb(240, 4, 5, 8), LinearGradientMode.Vertical))
                {
                    g.FillRectangle(shade, 0, shadeTop, Width, Height - shadeTop);
                }

                if (hoverProgress > 0.01f)
                {
                    using (SolidBrush dim = new SolidBrush(Color.FromArgb((int)(52 * hoverProgress), 4, 6, 10)))
                        g.FillRectangle(dim, coverRect);
                }
                g.Restore(state);
            }

            // Titulo
            int textPad = Theme.Px(12);
            int badgeHeight = Theme.Px(18);
            int titleHeight = Theme.Px(22);
            int badgeY = Height - textPad - badgeHeight;
            int titleY = badgeY - Theme.Px(6) - titleHeight;

            Rectangle titleRect = new Rectangle(textPad, titleY, Width - textPad * 2, titleHeight);
            Theme.DrawText(g, Game.Title, Theme.CardTitle, Theme.Text, titleRect,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                TextFormatFlags.SingleLine);

            // Etiqueta de plataforma
            string label = Game.PlatformName;
            Size badgeText = TextRenderer.MeasureText(label, Theme.CardBadge,
                new Size(Width, badgeHeight), TextFormatFlags.NoPadding);
            int badgeWidth = badgeText.Width + Theme.Px(16);
            RectangleF badgeRect = new RectangleF(textPad, badgeY, badgeWidth, badgeHeight);
            Color brand = Theme.PlatformColor(Game.Platform);
            Theme.FillRound(g, badgeRect, Theme.Pxf(6f), Color.FromArgb(190, 8, 10, 14));
            Theme.StrokeRound(g, badgeRect, Theme.Pxf(6f), Color.FromArgb(90, brand), 1f);
            TextRenderer.DrawText(g, label, Theme.CardBadge,
                new Rectangle((int)badgeRect.X, (int)badgeRect.Y, (int)badgeRect.Width, (int)badgeRect.Height),
                brand, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);

            // Tiempo jugado
            string played = Fmt.Playtime(PlaySeconds);
            if (played.Length > 0)
            {
                Size playedSize = TextRenderer.MeasureText(played, Theme.CardBadge,
                    new Size(Width, badgeHeight), TextFormatFlags.NoPadding);
                Rectangle playedRect = new Rectangle(Width - textPad - playedSize.Width,
                    badgeY, playedSize.Width + 1, badgeHeight);
                TextRenderer.DrawText(g, played, Theme.CardBadge, playedRect,
                    Color.FromArgb(190, 214, 222, 234),
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            }

            if (IsNew)
            {
                RectangleF neu = new RectangleF(Theme.Px(10), Theme.Px(10), Theme.Px(48), Theme.Px(18));
                Theme.FillRound(g, neu, Theme.Pxf(9f), Theme.Accent);
                TextRenderer.DrawText(g, "NUEVO", Theme.CardBadge, Rectangle.Round(neu),
                    Color.FromArgb(8, 12, 18),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            }

            // Favorito
            if (IsFavorite)
            {
                Rectangle star = new Rectangle(Width - Theme.Px(30), Theme.Px(6), Theme.Px(24), Theme.Px(24));
                TextRenderer.DrawText(g, "\u2605", Theme.Star,
                    star, Color.FromArgb(255, 205, 92),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }

            // Accion al pasar el raton
            if (hoverProgress > 0.01f)
            {
                int pillW = Theme.Px(108);
                int pillH = Theme.Px(34);
                float pillY = (Height - pillH) / 2f - Theme.Px(10) + Theme.Px(7) * (1f - hoverProgress);
                RectangleF pill = new RectangleF((Width - pillW) / 2f, pillY, pillW, pillH);
                using (SolidBrush shadow = new SolidBrush(Color.FromArgb(70, 0, 0, 0)))
                {
                    using (GraphicsPath sp = Theme.Round(new RectangleF(pill.X + 1, pill.Y + 2, pill.Width, pill.Height), pillH / 2f))
                        g.FillPath(shadow, sp);
                }
                int pillAlpha = Math.Max(1, Math.Min(255, (int)(255 * hoverProgress)));
                Theme.FillRoundGradient(g, pill, pillH / 2f,
                    Color.FromArgb(pillAlpha, Theme.Accent), Color.FromArgb(pillAlpha, Theme.AccentDeep));
                TextRenderer.DrawText(g, "Ver detalles", Theme.SmallBold,
                    Rectangle.Round(pill), Color.FromArgb(pillAlpha, 8, 12, 18),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            }

            int borderAlpha = IsSelected ? 200 : (int)(115 * hoverProgress);
            if (borderAlpha > 0)
                Theme.StrokeRound(g, rect, radius, Color.FromArgb(borderAlpha, Theme.Accent),
                    IsSelected ? 1.8f : 1.2f);
        }

        private void DrawFallback(Graphics g, RectangleF rect)
        {
            Color brand = Theme.PlatformColor(Game.Platform);
            using (LinearGradientBrush brush = new LinearGradientBrush(
                new RectangleF(rect.X, rect.Y - 1, rect.Width, rect.Height + 2),
                Theme.Blend(Color.FromArgb(24, 28, 36), brand, 0.20f),
                Color.FromArgb(10, 12, 16), LinearGradientMode.Vertical))
                g.FillRectangle(brush, rect);

            string initials = Initials(Game.Title);
            TextRenderer.DrawText(g, initials, Theme.CardInitials, Rectangle.Round(rect),
                Color.FromArgb(120, brand),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private static string Initials(string title)
        {
            if (string.IsNullOrEmpty(title)) return "?";
            string[] parts = title.Split(new char[] { ' ', ':', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "?";
            if (parts.Length == 1) return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant();
            return (parts[0].Substring(0, 1) + parts[1].Substring(0, 1)).ToUpperInvariant();
        }

        // Recorta la imagen para llenar el rectangulo sin deformarla.
        private static void DrawCover(Graphics g, Image img, RectangleF dest)
        {
            if (img.Width <= 0 || img.Height <= 0) return;
            float destRatio = dest.Width / dest.Height;
            float srcRatio = (float)img.Width / img.Height;
            RectangleF src;
            if (srcRatio > destRatio)
            {
                float w = img.Height * destRatio;
                src = new RectangleF((img.Width - w) / 2f, 0, w, img.Height);
            }
            else
            {
                float h = img.Width / destRatio;
                src = new RectangleF(0, (img.Height - h) / 2f, img.Width, h);
            }
            g.DrawImage(img, dest, src, GraphicsUnit.Pixel);
        }

        protected override void OnEnter(EventArgs e) { IsSelected = true; Invalidate(); base.OnEnter(e); }
        protected override void OnLeave(EventArgs e) { IsSelected = false; Invalidate(); base.OnLeave(e); }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Left || keyData == Keys.Right || keyData == Keys.Up || keyData == Keys.Down ||
                keyData == Keys.Enter || keyData == Keys.Space) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                if (Activated != null) Activated(this, EventArgs.Empty);
                e.Handled = true;
                return;
            }
            base.OnKeyDown(e);
        }
    }
}
