using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace GamesOfAll
{
    // Contenedor con barra de desplazamiento propia (oscura) y rejilla adaptable.
    internal sealed class GridPanel : ScrollableControl
    {
        public readonly List<GameCard> Cards = new List<GameCard>();
        public string EmptyText = "";
        public string EmptyHint = "";

        private int contentHeight;
        private int cardW = 160;
        private int cardH = 240;
        private int columns = 1;
        private int pad;
        private int gap;
        private Image ambientImage;

        public GridPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = Theme.Bg;
            AutoScroll = true;
            HorizontalScroll.Enabled = false;
            HorizontalScroll.Visible = false;
        }

        public int Columns { get { return columns; } }

        public void SetAmbient(Image source)
        {
            if (ambientImage != null)
            {
                ambientImage.Dispose();
                ambientImage = null;
            }
            if (source != null)
            {
                try
                {
                    Bitmap small = new Bitmap(48, 72);
                    using (Graphics g = Graphics.FromImage(small))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.DrawImage(source, new Rectangle(0, 0, small.Width, small.Height));
                    }
                    ambientImage = small;
                }
                catch { ambientImage = null; }
            }
            Invalidate();
        }

        public void SetCards(List<GameCard> cards)
        {
            SuspendLayout();
            foreach (GameCard c in Cards)
            {
                Controls.Remove(c);
                c.Dispose();
            }
            Cards.Clear();
            Cards.AddRange(cards);
            foreach (GameCard c in Cards) Controls.Add(c);
            ResumeLayout();

            AutoScrollPosition = Point.Empty;
            Relayout();
        }

        public GameCard FindCard(string id)
        {
            foreach (GameCard c in Cards)
                if (string.Equals(c.Game.Id, id, StringComparison.OrdinalIgnoreCase)) return c;
            return null;
        }

        public void Relayout()
        {
            pad = Theme.Px(26);
            gap = Theme.Px(18);
            int scrollbar = Theme.Px(12);
            int avail = Math.Max(Theme.Px(140), Width - pad * 2 - scrollbar);
            int target = Theme.Px(174);

            columns = Math.Max(1, (int)Math.Floor((double)(avail + gap) / (target + gap)));
            cardW = (avail - gap * (columns - 1)) / Math.Max(1, columns);
            cardH = (int)Math.Round(cardW * 1.5);

            int rows = (Cards.Count + columns - 1) / columns;
            contentHeight = pad * 2 + rows * cardH + Math.Max(0, rows - 1) * gap;
            AutoScrollMinSize = new Size(0, contentHeight);
            AutoScrollPosition = new Point(0, Math.Min(CurrentScroll, MaxScroll));
            PositionCards();
            Invalidate();
        }

        private int MaxScroll { get { return Math.Max(0, contentHeight - ClientSize.Height); } }
        private int CurrentScroll { get { return Math.Max(0, -AutoScrollPosition.Y); } }

        private void PositionCards()
        {
            for (int i = 0; i < Cards.Count; i++)
            {
                int col = i % columns;
                int row = i / columns;
                Cards[i].SetBounds(pad + col * (cardW + gap), pad + row * (cardH + gap), cardW, cardH);
            }
        }

        public void ScrollBy(int delta)
        {
            if (MaxScroll <= 0) return;
            int target = Math.Max(0, Math.Min(MaxScroll, CurrentScroll + delta));
            AutoScrollPosition = new Point(0, target);
        }

        public void EnsureVisible(int index)
        {
            if (index < 0 || index >= Cards.Count) return;
            int row = index / Math.Max(1, columns);
            int logicalTop = pad + row * (cardH + gap);
            int logicalBottom = logicalTop + cardH;

            int scroll = CurrentScroll;
            if (logicalTop - scroll < pad - Theme.Px(2)) scroll = logicalTop - pad;
            else if (logicalBottom - scroll > ClientSize.Height - pad) scroll = logicalBottom - ClientSize.Height + pad;
            else return;

            AutoScrollPosition = new Point(0, Math.Max(0, Math.Min(MaxScroll, scroll)));
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Relayout();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && ambientImage != null)
            {
                ambientImage.Dispose();
                ambientImage = null;
            }
            base.Dispose(disposing);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            ScrollBy(e.Delta > 0 ? -Theme.Px(150) : Theme.Px(150));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush b = new SolidBrush(Theme.Bg))
                g.FillRectangle(b, ClientRectangle);

            if (ambientImage != null)
            {
                using (ImageAttributes attr = new ImageAttributes())
                {
                    ColorMatrix matrix = new ColorMatrix();
                    matrix.Matrix33 = Theme.IsLight ? 0.10f : 0.18f;
                    attr.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                    g.DrawImage(ambientImage, new Rectangle(-Width / 3, -Height / 4, Width * 5 / 3, Height * 3 / 2),
                        0, 0, ambientImage.Width, ambientImage.Height, GraphicsUnit.Pixel, attr);
                }
                using (SolidBrush tint = new SolidBrush(Color.FromArgb(Theme.IsLight ? 150 : 118, Theme.Bg)))
                    g.FillRectangle(tint, ClientRectangle);
            }

            if (Cards.Count == 0 && !string.IsNullOrEmpty(EmptyText))
            {
                int centerY = Height / 2 - Theme.Px(30);
                Theme.DrawText(g, EmptyText, Theme.Title, Color.FromArgb(150, 200, 212, 228),
                    new Rectangle(Theme.Px(40), centerY, Width - Theme.Px(80), Theme.Px(30)),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                Theme.DrawText(g, EmptyHint, Theme.Body, Theme.TextFaint,
                    new Rectangle(Theme.Px(40), centerY + Theme.Px(30), Width - Theme.Px(80), Theme.Px(26)),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
        }

    }
}
