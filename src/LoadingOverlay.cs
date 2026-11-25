using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GamesOfAll
{
    internal sealed class LoadingOverlay : Control
    {
        private readonly Timer animationTimer;
        private int elapsed;
        private float rotation;
        private int fade;
        private bool dismissRequested;

        public event EventHandler Dismissed;

        public LoadingOverlay()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = Theme.Bg;
            animationTimer = new Timer();
            animationTimer.Interval = 16;
            animationTimer.Tick += AnimationTimer_Tick;
        }

        public void StartAnimation()
        {
            elapsed = 0;
            fade = 0;
            animationTimer.Start();
        }

        public void Dismiss()
        {
            dismissRequested = true;
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            elapsed += animationTimer.Interval;
            rotation = (rotation + 5.2f) % 360f;
            if (dismissRequested && elapsed >= 1500)
                fade = Math.Min(255, fade + 22);

            if (fade >= 255)
            {
                animationTimer.Stop();
                Visible = false;
                if (Dismissed != null) Dismissed(this, EventArgs.Empty);
                return;
            }
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.HighQuality;
            using (LinearGradientBrush background = new LinearGradientBrush(ClientRectangle,
                Theme.BgSoft, Theme.Bg, LinearGradientMode.Vertical))
                g.FillRectangle(background, ClientRectangle);

            using (SolidBrush glow = new SolidBrush(Color.FromArgb(22, Theme.Accent)))
            {
                float diameter = Math.Min(Width, Height) * 0.74f;
                g.FillEllipse(glow, (Width - diameter) / 2f, (Height - diameter) / 2f - Theme.Px(28),
                    diameter, diameter);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width <= 0 || Height <= 0) return;
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int alpha = 255 - fade;
            float scale = 1f - (fade / 255f) * 0.08f;
            float centerX = Width / 2f;
            float centerY = Height / 2f - Theme.Px(20);
            float radius = Theme.Px(54) * scale;
            int count = 12;

            for (int i = 0; i < count; i++)
            {
                float angle = (rotation + i * (360f / count)) * (float)Math.PI / 180f;
                float depth = (i + count - (int)(rotation / 30f)) % count;
                int dotAlpha = (int)(alpha * (0.18f + 0.82f * depth / count));
                float size = Theme.Px(5) + Theme.Pxf(2f) * depth / count;
                float x = centerX + (float)Math.Cos(angle) * radius - size / 2f;
                float y = centerY + (float)Math.Sin(angle) * radius - size / 2f;
                Color dotColor = Theme.Blend(Theme.Accent, Theme.Accent2, (float)i / count);
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(dotAlpha, dotColor)))
                    g.FillEllipse(brush, x, y, size, size);
            }

            int logoSize = Theme.Px(56);
            RectangleF logo = new RectangleF(centerX - logoSize / 2f, centerY - logoSize / 2f,
                logoSize, logoSize);
            Theme.FillRoundGradient(g, logo, Theme.Pxf(16f),
                Color.FromArgb(alpha, Theme.Accent), Color.FromArgb(alpha, Theme.AccentDeep));
            PointF[] triangle =
            {
                new PointF(logo.X + logo.Width * 0.42f, logo.Y + logo.Height * 0.28f),
                new PointF(logo.X + logo.Width * 0.42f, logo.Y + logo.Height * 0.72f),
                new PointF(logo.X + logo.Width * 0.72f, logo.Y + logo.Height * 0.5f)
            };
            using (SolidBrush white = new SolidBrush(Color.FromArgb(alpha, Color.White)))
                g.FillPolygon(white, triangle);

            int textY = (int)(centerY + radius + Theme.Px(26));
            TextRenderer.DrawText(g, "GAMES", Theme.Logo,
                new Rectangle(0, textY, Width, Theme.Px(30)),
                Color.FromArgb(alpha, Theme.Text),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);

            TextRenderer.DrawText(g, "Preparando tu biblioteca", Theme.Body,
                new Rectangle(0, textY + Theme.Px(36), Width, Theme.Px(24)),
                Color.FromArgb((int)(alpha * 0.72f), Theme.TextDim),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);

            float pulse = (float)(0.5 + 0.5 * Math.Sin(elapsed * 0.009));
            int barWidth = Theme.Px(76);
            int barHeight = Theme.Px(3);
            float barX = centerX - barWidth / 2f;
            float barY = textY + Theme.Px(76);
            RectangleF track = new RectangleF(barX, barY, barWidth, barHeight);
            Theme.FillRound(g, track, barHeight / 2f, Color.FromArgb((int)(alpha * 0.12f), Theme.Text));
            float progressWidth = Theme.Px(20) + Theme.Px(24) * pulse;
            RectangleF progress = new RectangleF(barX + (barWidth - progressWidth) * pulse,
                barY, progressWidth, barHeight);
            Theme.FillRoundGradient(g, progress, barHeight / 2f,
                Color.FromArgb(alpha, Theme.Accent), Color.FromArgb(alpha, Theme.Accent2));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                animationTimer.Stop();
                animationTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
