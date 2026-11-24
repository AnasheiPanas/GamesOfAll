using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GamesOfAll
{
    internal class StatEntry
    {
        public string Title;
        public string Platform;
        public long PlaySeconds;
        public long LastPlayed;
        public int PlayCount;
        public Image Cover;
    }

    // Ventana con el tiempo jugado de cada juego.
    internal sealed class StatsDialog : Form
    {
        private readonly StatsList list;
        private bool dragging;
        private Point dragStart;

        public StatsDialog(List<StatEntry> entries, string summary)
        {
            Text = "Estadísticas";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Bg;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            ClientSize = new Size(Theme.Px(780), Theme.Px(620));
            KeyPreview = true;
            Font = Theme.Body;

            int margin = Theme.Px(28);

            list = new StatsList(entries);
            list.SetBounds(margin, Theme.Px(96), ClientSize.Width - margin * 2,
                ClientSize.Height - Theme.Px(140));

            PillButton close = new PillButton("");
            close.Style = PillStyle.Close;
            close.Icon = "\uE8BB";
            close.IconOnly = true;
            close.SetBounds(ClientSize.Width - Theme.Px(52), Theme.Px(12), Theme.Px(34), Theme.Px(30));
            close.Click += delegate { Close(); };

            PillButton ok = new PillButton("Volver");
            ok.Style = PillStyle.Ghost;
            ok.SetBounds(ClientSize.Width - margin - Theme.Px(100), ClientSize.Height - Theme.Px(44),
                Theme.Px(100), Theme.Px(32));
            ok.Click += delegate { Close(); };

            Controls.Add(list);
            Controls.Add(close);
            Controls.Add(ok);

            Tag = summary;
            MouseDown += DragArea_MouseDown;
            MouseMove += DragArea_MouseMove;
            MouseUp += delegate { dragging = false; };
        }

        private void DragArea_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { dragging = true; dragStart = e.Location; }
        }

        private void DragArea_MouseMove(object sender, MouseEventArgs e)
        {
            if (!dragging) return;
            Point screen = PointToScreen(e.Location);
            Location = new Point(screen.X - dragStart.X, screen.Y - dragStart.Y);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { Close(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Theme.DrawText(g, "Estadísticas de juego", Theme.Title, Theme.Text,
                new Rectangle(Theme.Px(28), Theme.Px(24), ClientSize.Width - Theme.Px(90), Theme.Px(30)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            Theme.DrawText(g, Convert.ToString(Tag), Theme.Small, Theme.TextDim,
                new Rectangle(Theme.Px(28), Theme.Px(54), ClientSize.Width - Theme.Px(90), Theme.Px(20)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

            using (Pen p = new Pen(Theme.Stroke))
                g.DrawRectangle(p, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            foreach (StatEntry entry in list.Entries)
            {
                if (entry.Cover != null) { try { entry.Cover.Dispose(); } catch { } }
            }
            base.OnFormClosed(e);
        }
    }

    internal sealed class StatsList : Control
    {
        public readonly List<StatEntry> Entries;
        private int scroll;
        private int rowHeight;

        public StatsList(List<StatEntry> entries)
        {
            Entries = entries;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            DoubleBuffered = true;
        }

        private int ContentHeight
        {
            get { return Entries.Count * rowHeight + Theme.Px(8); }
        }

        private int MaxScroll
        {
            get { return Math.Max(0, ContentHeight - Height); }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            rowHeight = Theme.Px(70);
            Clamp();
            Invalidate();
        }

        private void Clamp()
        {
            if (scroll > MaxScroll) scroll = MaxScroll;
            if (scroll < 0) scroll = 0;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            scroll += e.Delta > 0 ? -rowHeight * 2 : rowHeight * 2;
            Clamp();
            Invalidate();
            base.OnMouseWheel(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            using (SolidBrush bg = new SolidBrush(Theme.Bg)) g.FillRectangle(bg, ClientRectangle);

            if (Entries.Count == 0)
            {
                Theme.DrawText(g, "Todavía no hay horas registradas",
                    Theme.Title, Color.FromArgb(150, 200, 212, 228),
                    new Rectangle(0, Height / 2 - Theme.Px(30), Width, Theme.Px(30)),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                Theme.DrawText(g, "Juega algo y el tiempo se contará automáticamente",
                    Theme.Body, Theme.TextFaint,
                    new Rectangle(0, Height / 2, Width, Theme.Px(24)),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            if (rowHeight <= 0) rowHeight = Theme.Px(70);

            for (int i = 0; i < Entries.Count; i++)
            {
                int y = i * rowHeight - scroll;
                if (y > Height || y + rowHeight < 0) continue;
                DrawRow(g, Entries[i], new Rectangle(0, y, Width, rowHeight), i);
            }

            if (MaxScroll > 0)
            {
                int trackTop = Theme.Px(8);
                int trackH = Height - Theme.Px(16);
                int thumbH = Math.Max(Theme.Px(36), (int)((float)trackH * Height / ContentHeight));
                int travel = trackH - thumbH;
                int thumbY = trackTop + (int)(travel * ((float)scroll / MaxScroll));
                Theme.FillRound(g, new RectangleF(Width - Theme.Px(6), thumbY, Theme.Px(4), thumbH),
                    Theme.Pxf(2f), Color.FromArgb(70, 200, 212, 230));
            }
        }

        private void DrawRow(Graphics g, StatEntry entry, Rectangle row, int index)
        {
            int pad = Theme.Px(10);
            RectangleF card = new RectangleF(pad, row.Y + Theme.Px(3), row.Width - pad * 2, row.Height - Theme.Px(6));
            Theme.FillRound(g, card, Theme.Pxf(10f), index % 2 == 0 ? Theme.Surface : Color.FromArgb(17, 20, 26));

            int thumbW = Theme.Px(40);
            int thumbH = Theme.Px(54);
            Rectangle thumb = new Rectangle((int)card.X + Theme.Px(10),
                (int)card.Y + (row.Height - thumbH) / 2, thumbW, thumbH);

            if (entry.Cover != null)
            {
                using (GraphicsPath clip = Theme.Round(thumb, Theme.Pxf(6f)))
                {
                    g.SetClip(clip, CombineMode.Intersect);
                    DrawCropped(g, entry.Cover, thumb);
                    g.ResetClip();
                }
            }
            else
            {
                Theme.FillRound(g, thumb, Theme.Pxf(6f), Theme.SurfaceHover);
            }

            int textX = thumb.Right + Theme.Px(14);
            int rightW = Theme.Px(130);
            Theme.DrawText(g, entry.Title, Theme.BodyBold, Theme.Text,
                new Rectangle(textX, row.Y + Theme.Px(14), Width - textX - rightW - Theme.Px(20), Theme.Px(22)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);

            string sub = entry.Platform;
            if (entry.PlayCount > 0) sub += "  ·  " + entry.PlayCount + (entry.PlayCount == 1 ? " apertura" : " aperturas");
            Theme.DrawText(g, sub, Theme.Small, Theme.TextFaint,
                new Rectangle(textX, row.Y + Theme.Px(36), Width - textX - rightW - Theme.Px(20), Theme.Px(18)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);

            string played = entry.PlaySeconds >= 60 ? Fmt.Playtime(entry.PlaySeconds) : "—";
            Theme.DrawText(g, played, Theme.Title, Theme.Accent,
                new Rectangle(Width - rightW - Theme.Px(12), row.Y + Theme.Px(14), rightW, Theme.Px(24)),
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

            Theme.DrawText(g, "última vez " + Fmt.Ago(entry.LastPlayed), Theme.Small, Theme.TextFaint,
                new Rectangle(Width - rightW - Theme.Px(12), row.Y + Theme.Px(38), rightW, Theme.Px(18)),
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }

        private static void DrawCropped(Graphics g, Image img, Rectangle dest)
        {
            float destRatio = (float)dest.Width / dest.Height;
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
    }
}
