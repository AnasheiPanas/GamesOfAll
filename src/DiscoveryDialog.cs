using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace GamesOfAll
{
    // Ventana para revisar y añadir los juegos detectados fuera de las tiendas.
    internal sealed class DiscoveryDialog : Form
    {
        private readonly HashSet<string> existingPaths;
        private readonly List<GameCandidate> candidates = new List<GameCandidate>();
        private readonly List<GameCandidate> visible = new List<GameCandidate>();
        private readonly Dictionary<string, bool> checks = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        private CandidateList list;
        private TextField filter;
        private PillButton addButton;
        private bool scanning = true;
        private bool dragging;
        private Point dragStart;

        public readonly List<GameCandidate> Selected = new List<GameCandidate>();

        public DiscoveryDialog(HashSet<string> existingExePaths)
        {
            existingPaths = existingExePaths ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            Text = "Buscar juegos";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Bg;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            ClientSize = new Size(Theme.Px(840), Theme.Px(640));
            KeyPreview = true;
            Font = Theme.Body;

            int margin = Theme.Px(28);

            filter = new TextField("Filtrar por nombre…");
            filter.SetBounds(margin, Theme.Px(86), ClientSize.Width - margin * 2, Theme.Px(38));
            filter.Inner.TextChanged += delegate { RebuildVisible(); };

            list = new CandidateList(checks);
            list.SetBounds(margin, Theme.Px(138), ClientSize.Width - margin * 2,
                ClientSize.Height - Theme.Px(138) - Theme.Px(76));
            list.Changed += delegate { UpdateAddButton(); };

            PillButton cancel = new PillButton("Cancelar");
            cancel.Style = PillStyle.Ghost;
            cancel.SetBounds(ClientSize.Width - margin - Theme.Px(250), ClientSize.Height - Theme.Px(54),
                Theme.Px(120), Theme.Px(38));
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };

            addButton = new PillButton("Añadir");
            addButton.Style = PillStyle.Accent;
            addButton.SetBounds(ClientSize.Width - margin - Theme.Px(120), ClientSize.Height - Theme.Px(54),
                Theme.Px(120), Theme.Px(38));
            addButton.Click += delegate { Accept(); };

            PillButton close = new PillButton("");
            close.Style = PillStyle.Close;
            close.Icon = "\uE8BB";
            close.IconOnly = true;
            close.SetBounds(ClientSize.Width - Theme.Px(52), Theme.Px(12), Theme.Px(34), Theme.Px(30));
            close.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };

            Controls.Add(filter);
            Controls.Add(list);
            Controls.Add(cancel);
            Controls.Add(addButton);
            Controls.Add(close);

            MouseDown += DragArea_MouseDown;
            MouseMove += DragArea_MouseMove;
            MouseUp += delegate { dragging = false; };

            UpdateAddButton();
            StartScan();
        }

        private void StartScan()
        {
            Thread worker = new Thread(delegate()
            {
                List<GameCandidate> result = null;
                try { result = Discovery.DiscoverAll(); }
                catch (Exception ex) { Storage.Log("Descubrir juegos: " + ex.Message); }

                try
                {
                    if (IsHandleCreated)
                        BeginInvoke(new Action(delegate { ApplyScan(result ?? new List<GameCandidate>()); }));
                }
                catch { }
            });
            worker.IsBackground = true;
            worker.Start();
        }

        private void ApplyScan(List<GameCandidate> result)
        {
            scanning = false;
            foreach (GameCandidate c in result)
            {
                if (existingPaths.Contains(c.Exe)) continue;
                if (string.IsNullOrEmpty(c.Title)) continue;
                candidates.Add(c);
                checks[c.Key] = c.AutoAdd;
            }
            RebuildVisible();
        }

        private void RebuildVisible()
        {
            string q = filter.Value.Trim();
            visible.Clear();
            foreach (GameCandidate c in candidates)
            {
                if (q.Length > 0 && c.Title.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0 &&
                    (c.Exe ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) continue;
                visible.Add(c);
            }
            list.SetItems(visible, scanning);
            UpdateAddButton();
        }

        private int CheckedCount()
        {
            int n = 0;
            foreach (GameCandidate c in candidates)
            {
                bool value;
                if (checks.TryGetValue(c.Key, out value) && value) n++;
            }
            return n;
        }

        private void UpdateAddButton()
        {
            if (addButton == null) return;
            int n = CheckedCount();
            addButton.Text = n == 0 ? "Añadir" : (n == 1 ? "Añadir 1 juego" : "Añadir " + n + " juegos");
            addButton.Enabled = n > 0;
            addButton.Invalidate();
        }

        private void Accept()
        {
            Selected.Clear();
            foreach (GameCandidate c in candidates)
            {
                bool value;
                if (checks.TryGetValue(c.Key, out value) && value) Selected.Add(c);
            }
            DialogResult = DialogResult.OK;
            Close();
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
            if (keyData == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Theme.DrawText(g, "Juegos instalados en este PC", Theme.Title, Theme.Text,
                new Rectangle(Theme.Px(28), Theme.Px(24), ClientSize.Width - Theme.Px(90), Theme.Px(30)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            Theme.DrawText(g, "Detectamos accesos directos y carpetas de juegos. Marca los que quieras añadir.",
                Theme.Small, Theme.TextDim,
                new Rectangle(Theme.Px(28), Theme.Px(54), ClientSize.Width - Theme.Px(90), Theme.Px(20)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

            using (Pen p = new Pen(Theme.Stroke))
                g.DrawRectangle(p, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            list.DisposeIcons();
            base.OnFormClosed(e);
        }
    }

    internal sealed class CandidateList : Control
    {
        private readonly Dictionary<string, bool> checks;
        private readonly Dictionary<string, Image> icons = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        private List<GameCandidate> items = new List<GameCandidate>();
        private int scroll;
        private int hover = -1;
        private int rowHeight;
        private bool searching;

        public event EventHandler Changed;

        public CandidateList(Dictionary<string, bool> checkState)
        {
            checks = checkState;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            DoubleBuffered = true;
            Cursor = Cursors.Hand;
        }

        public void SetItems(List<GameCandidate> list, bool isSearching)
        {
            items = list;
            searching = isSearching;
            scroll = 0;
            hover = -1;
            Invalidate();
        }

        public void DisposeIcons()
        {
            foreach (Image img in icons.Values) { try { img.Dispose(); } catch { } }
            icons.Clear();
        }

        private int MaxScroll
        {
            get { return Math.Max(0, items.Count * rowHeight - Height); }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            rowHeight = Theme.Px(64);
            scroll = Math.Min(scroll, MaxScroll);
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            scroll += e.Delta > 0 ? -rowHeight * 2 : rowHeight * 2;
            if (scroll > MaxScroll) scroll = MaxScroll;
            if (scroll < 0) scroll = 0;
            Invalidate();
            base.OnMouseWheel(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int index = (e.Y + scroll) / Math.Max(1, rowHeight);
            if (index != hover) { hover = index; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hover = -1;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnClick(EventArgs e)
        {
            Point p = PointToClient(Cursor.Position);
            int index = (p.Y + scroll) / Math.Max(1, rowHeight);
            if (index >= 0 && index < items.Count)
            {
                string key = items[index].Key;
                bool current;
                checks.TryGetValue(key, out current);
                checks[key] = !current;
                Invalidate();
                if (Changed != null) Changed(this, EventArgs.Empty);
            }
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            using (SolidBrush bg = new SolidBrush(Theme.Bg)) g.FillRectangle(bg, ClientRectangle);

            if (items.Count == 0)
            {
                string title = searching ? "Buscando juegos instalados…" : "No se encontraron juegos nuevos";
                string hint = searching
                    ? "Revisando accesos directos y carpetas de juegos"
                    : "Prueba «Añadir juego» y elige el .exe manualmente";
                Theme.DrawText(g, title, Theme.Title, Color.FromArgb(150, 200, 212, 228),
                    new Rectangle(0, Height / 2 - Theme.Px(16), Width, Theme.Px(30)),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                Theme.DrawText(g, hint, Theme.Body, Theme.TextFaint,
                    new Rectangle(0, Height / 2 + Theme.Px(16), Width, Theme.Px(24)),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            if (rowHeight <= 0) rowHeight = Theme.Px(64);

            for (int i = 0; i < items.Count; i++)
            {
                int y = i * rowHeight - scroll;
                if (y > Height || y + rowHeight < 0) continue;
                DrawRow(g, items[i], i, y);
            }

            if (MaxScroll > 0)
            {
                int trackH = Height - Theme.Px(8);
                int thumbH = Math.Max(Theme.Px(34), (int)((float)trackH * Height / (items.Count * rowHeight)));
                int travel = trackH - thumbH;
                int thumbY = (int)(travel * ((float)scroll / MaxScroll));
                Theme.FillRound(g, new RectangleF(Width - Theme.Px(5), thumbY, Theme.Px(4), thumbH),
                    Theme.Pxf(2f), Color.FromArgb(70, 200, 212, 230));
            }
        }

        private void DrawRow(Graphics g, GameCandidate c, int index, int y)
        {
            Rectangle row = new Rectangle(Theme.Px(2), y, Width - Theme.Px(8), rowHeight);
            bool isHover = index == hover;
            bool isChecked;
            checks.TryGetValue(c.Key, out isChecked);

            using (SolidBrush bg = new SolidBrush(isHover ? Theme.Surface : Color.FromArgb(15, 18, 24)))
                g.FillRectangle(bg, row);
            using (Pen sep = new Pen(Color.FromArgb(23, 27, 34)))
                g.DrawLine(sep, row.Left, row.Bottom - 1, row.Right, row.Bottom - 1);

            int boxSize = Theme.Px(18);
            RectangleF box = new RectangleF(row.Left + Theme.Px(14), y + (rowHeight - boxSize) / 2f, boxSize, boxSize);
            if (isChecked) Theme.FillRound(g, box, Theme.Pxf(4f), Theme.Accent);
            Theme.StrokeRound(g, box, Theme.Pxf(4f),
                isChecked ? Theme.Accent : Color.FromArgb(90, 150, 162, 178), 1.4f);
            if (isChecked)
            {
                TextRenderer.DrawText(g, "\uE73E", Theme.SmallBold, Rectangle.Round(box),
                    Color.FromArgb(10, 14, 20),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }

            int iconSize = Theme.Px(30);
            Rectangle iconRect = new Rectangle((int)box.Right + Theme.Px(12),
                y + (rowHeight - iconSize) / 2, iconSize, iconSize);
            Image icon = GetIcon(c.Exe);
            if (icon != null) g.DrawImage(icon, iconRect);
            else Theme.FillRound(g, iconRect, Theme.Pxf(6f), Theme.SurfaceHover);

            int textX = iconRect.Right + Theme.Px(12);
            int sourceW = Theme.Px(112);
            Theme.DrawText(g, c.Title, Theme.BodyBold, Theme.Text,
                new Rectangle(textX, y + Theme.Px(11), Width - textX - sourceW - Theme.Px(26), Theme.Px(22)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);

            Theme.DrawText(g, ShortPath(c.Exe), Theme.Small, Theme.TextFaint,
                new Rectangle(textX, y + Theme.Px(33), Width - textX - sourceW - Theme.Px(26), Theme.Px(18)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);

            RectangleF chip = new RectangleF(Width - sourceW - Theme.Px(14),
                y + (rowHeight - Theme.Px(20)) / 2f, sourceW - Theme.Px(10), Theme.Px(20));
            Theme.FillRound(g, chip, Theme.Pxf(10f), Color.FromArgb(140, 24, 29, 38));
            TextRenderer.DrawText(g, c.Source, Theme.CardBadge, Rectangle.Round(chip),
                Color.FromArgb(150, 168, 182, 198),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }

        private string ShortPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            string dir = Path.GetDirectoryName(path) ?? "";
            if (dir.Length <= 70) return dir;
            return dir.Substring(0, 24) + "…" + dir.Substring(dir.Length - 40);
        }

        private Image GetIcon(string exe)
        {
            Image cached;
            if (icons.TryGetValue(exe, out cached)) return cached;
            Image img = Covers.LoadIconFile(exe);
            icons[exe] = img;
            return img;
        }
    }
}
