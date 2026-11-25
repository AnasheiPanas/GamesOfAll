using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;

namespace GamesOfAll
{
    internal sealed class GameDetailsPanel : Panel
    {
        private readonly Game game;
        private readonly GameState state;
        private readonly Action persist;
        private readonly Image cover;
        private readonly Label eyebrow;
        private readonly Label title;
        private readonly Label platform;
        private readonly Label descriptionHeading;
        private readonly Label accountsDetails;
        private readonly LinkLabel source;
        private readonly RichTextBox description;
        private readonly Button backButton;
        private readonly Button playButton;
        private readonly Button refreshButton;
        private readonly Panel[] statTiles = new Panel[3];
        private readonly Label[] statValues = new Label[3];
        private readonly Label[] statCaptions = new Label[3];
        private Rectangle coverBounds;
        private string displayedDescription;
        private bool fetching;

        public event EventHandler PlayRequested;
        public event EventHandler CloseRequested;

        public GameDetailsPanel(Game game, GameState state, long playSeconds,
            IList<GameAccountStats> accountStats, Action persist)
        {
            this.game = game;
            this.state = state;
            this.persist = persist;
            cover = Covers.Load(game, state);

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = Theme.Bg;
            TabStop = true;

            backButton = MakeButton("←  Biblioteca", false);
            backButton.Click += delegate { RaiseCloseRequested(); };
            Controls.Add(backButton);

            eyebrow = MakeLabel("DETALLES DEL JUEGO", Theme.SmallBold, Theme.Accent);
            Controls.Add(eyebrow);

            title = MakeLabel(game.Title, Theme.MakeFont("Segoe UI Semibold", 24f, FontStyle.Bold), Theme.Text);
            title.AutoEllipsis = true;
            Controls.Add(title);

            platform = MakeLabel(game.PlatformName.ToUpperInvariant(), Theme.SmallBold, Theme.TextDim);
            Controls.Add(platform);

            string played = playSeconds >= 60 ? Fmt.Playtime(playSeconds) : "Sin registro";
            AddStat(0, played, "TIEMPO JUGADO");
            AddStat(1, state.PlayCount.ToString(), "PARTIDAS");
            AddStat(2, Fmt.Ago(state.LastPlayed), "ÚLTIMA VEZ");

            accountsDetails = MakeLabel(FormatAccountStats(accountStats), Theme.Small, Theme.TextDim);
            accountsDetails.Visible = !string.IsNullOrWhiteSpace(accountsDetails.Text);
            Controls.Add(accountsDetails);

            descriptionHeading = MakeLabel("Acerca de este juego", Theme.BodyBold, Theme.Text);
            Controls.Add(descriptionHeading);

            source = new LinkLabel();
            source.Font = Theme.Small;
            source.BackColor = Color.Transparent;
            source.AutoEllipsis = true;
            source.LinkClicked += Source_LinkClicked;
            Controls.Add(source);

            description = new RichTextBox();
            description.ReadOnly = true;
            description.DetectUrls = false;
            description.BorderStyle = BorderStyle.None;
            description.ScrollBars = RichTextBoxScrollBars.Vertical;
            description.BackColor = Theme.Surface;
            description.ForeColor = Theme.TextDim;
            description.Font = Theme.Body;
            description.Text = CurrentDescription();
            displayedDescription = description.Text;
            Controls.Add(description);

            refreshButton = MakeButton("Actualizar", false);
            refreshButton.Click += delegate { FetchDescription(); };
            Controls.Add(refreshButton);

            playButton = MakeButton("▶   JUGAR", true);
            playButton.Font = Theme.BodyBold;
            playButton.Click += delegate
            {
                if (PlayRequested != null) PlayRequested(this, EventArgs.Empty);
            };
            Controls.Add(playButton);

            UpdateSourceLabel();
            ApplyTheme();
            LayoutDetails();
        }

        private static string FormatAccountStats(IList<GameAccountStats> accountStats)
        {
            if (accountStats == null || accountStats.Count == 0) return "";
            List<string> lines = new List<string>();
            int accountNumber = 0;
            foreach (GameAccountStats account in accountStats)
            {
                if (account == null) continue;
                accountNumber++;
                string name = string.IsNullOrWhiteSpace(account.AccountName) ? "Cuenta" : account.AccountName;
                string time = account.PlaySeconds >= 60 ? Fmt.Playtime(account.PlaySeconds) : "0 min";
                string line = "Cuenta " + accountNumber + "  ·  " + name + "  ·  " + time;
                if (account.Level > 0) line += "  ·  Nv. " + account.Level;
                if (!string.IsNullOrWhiteSpace(account.Rank)) line += "  ·  " + account.Rank;
                lines.Add(line);
            }
            if (lines.Count == 0) return "";
            return "POR CUENTA" + Environment.NewLine + string.Join(Environment.NewLine, lines.ToArray());
        }

        public void BeginLookup()
        {
            if (string.IsNullOrWhiteSpace(state.Description) &&
                string.IsNullOrWhiteSpace(state.ManualDescription))
                FetchDescription();
        }

        public bool MatchesGame(string gameId)
        {
            return string.Equals(game.Id, gameId, StringComparison.OrdinalIgnoreCase);
        }

        public void UpdateAccountData(long playSeconds, IList<GameAccountStats> accountStats)
        {
            statValues[0].Text = playSeconds >= 60 ? Fmt.Playtime(playSeconds) : "Sin registro";
            accountsDetails.Text = FormatAccountStats(accountStats);
            accountsDetails.Visible = !string.IsNullOrWhiteSpace(accountsDetails.Text);
            LayoutDetails();
        }

        public void ApplyTheme()
        {
            BackColor = Theme.Bg;
            title.ForeColor = Theme.Text;
            eyebrow.ForeColor = Theme.Accent;
            platform.ForeColor = Theme.TextDim;
            accountsDetails.ForeColor = Theme.TextDim;
            descriptionHeading.ForeColor = Theme.Text;
            description.BackColor = Theme.Surface;
            description.ForeColor = Theme.TextDim;
            source.LinkColor = Theme.Accent;
            source.ActiveLinkColor = Theme.AccentDeep;
            source.VisitedLinkColor = Theme.Accent;
            foreach (Panel tile in statTiles)
                if (tile != null) { tile.BackColor = Theme.Surface; tile.Invalidate(); }
            foreach (Label value in statValues)
                if (value != null) value.ForeColor = Theme.Text;
            foreach (Label caption in statCaptions)
                if (caption != null) caption.ForeColor = Theme.TextFaint;
            StyleButton(backButton, false);
            StyleButton(refreshButton, false);
            StyleButton(playButton, true);
            Invalidate(true);
        }

        private void AddStat(int index, string value, string caption)
        {
            Panel tile = new Panel();
            tile.BackColor = Theme.Surface;
            tile.Paint += delegate(object sender, PaintEventArgs e)
            {
                RectangleF bounds = new RectangleF(0.5f, 0.5f, tile.Width - 1f, tile.Height - 1f);
                Theme.FillRound(e.Graphics, bounds, Theme.Pxf(10f), Theme.Surface);
                Theme.StrokeRound(e.Graphics, bounds, Theme.Pxf(10f), Theme.StrokeSoft, 1f);
            };

            Label valueLabel = MakeLabel(value, Theme.BodyBold, Theme.Text);
            Label captionLabel = MakeLabel(caption, Theme.CardBadge, Theme.TextFaint);
            tile.Controls.Add(valueLabel);
            tile.Controls.Add(captionLabel);
            Controls.Add(tile);
            statTiles[index] = tile;
            statValues[index] = valueLabel;
            statCaptions[index] = captionLabel;
        }

        private Label MakeLabel(string text, Font font, Color color)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = font;
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            label.AutoEllipsis = true;
            return label;
        }

        private Button MakeButton(string text, bool accent)
        {
            Button button = new Button();
            button.Text = text;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = accent ? Theme.Accent : Theme.Stroke;
            button.BackColor = accent ? Theme.Accent : Theme.Surface;
            button.ForeColor = accent ? Color.FromArgb(8, 12, 18) : Theme.Text;
            button.UseVisualStyleBackColor = false;
            button.Cursor = Cursors.Hand;
            return button;
        }

        private void StyleButton(Button button, bool accent)
        {
            if (button == null) return;
            button.FlatAppearance.BorderColor = accent ? Theme.Accent : Theme.Stroke;
            button.BackColor = accent ? Theme.Accent : Theme.Surface;
            button.ForeColor = accent ? Color.FromArgb(8, 12, 18) : Theme.Text;
        }

        private string CurrentDescription()
        {
            if (!string.IsNullOrWhiteSpace(state.ManualDescription)) return state.ManualDescription;
            if (!string.IsNullOrWhiteSpace(state.Description)) return state.Description;
            return "Estamos buscando información del juego. Si no hay una fuente disponible, esta ficha seguirá mostrando los datos guardados localmente.";
        }

        private string SourceText()
        {
            if (!string.IsNullOrWhiteSpace(state.ManualDescription))
                return "Descripción local guardada";
            if (string.IsNullOrWhiteSpace(state.Description))
                return fetching ? "Buscando una descripción online…" : "Información todavía no disponible";
            string fetched = state.DescriptionFetchedAt > 0
                ? " · guardada " + Fmt.Ago(state.DescriptionFetchedAt)
                : " · caché local";
            return "Fuente: " + (state.DescriptionSource ?? "Internet") + fetched;
        }

        private void UpdateSourceLabel()
        {
            source.Text = SourceText();
            source.Links.Clear();
            if (string.IsNullOrWhiteSpace(state.ManualDescription) &&
                !string.IsNullOrEmpty(state.DescriptionSourceUrl))
            {
                source.LinkArea = new LinkArea(0, source.Text.Length);
            }
        }

        private void FetchDescription()
        {
            if (fetching) return;
            fetching = true;
            refreshButton.Enabled = false;
            UpdateSourceLabel();

            ThreadPool.QueueUserWorkItem(delegate
            {
                GameDescriptionResult result = null;
                Exception failure = null;
                try { result = GameDescriptionService.Find(game); }
                catch (Exception ex) { failure = ex; }

                if (failure != null) Storage.Log("Descripción de " + game.Title + ": " + failure.Message);
                if (IsDisposed || !IsHandleCreated) return;

                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        fetching = false;
                        if (IsDisposed) return;
                        refreshButton.Enabled = true;
                        if (result != null && !string.IsNullOrWhiteSpace(result.Text))
                        {
                            state.Description = result.Text;
                            state.DescriptionSource = result.Source;
                            state.DescriptionSourceUrl = result.SourceUrl;
                            state.DescriptionFetchedAt = Fmt.ToUnix(DateTime.UtcNow);
                            if (string.IsNullOrWhiteSpace(state.ManualDescription) &&
                                description.Text == displayedDescription)
                            {
                                description.Text = result.Text;
                                displayedDescription = description.Text;
                            }
                            persist();
                        }
                        else if (string.IsNullOrWhiteSpace(state.Description) &&
                            string.IsNullOrWhiteSpace(state.ManualDescription))
                        {
                            description.Text = failure == null
                                ? "No encontramos una descripción online. Vuelve a intentarlo más tarde."
                                : "No se pudo conectar con las fuentes online. Inténtalo más tarde.";
                            displayedDescription = description.Text;
                        }
                        UpdateSourceLabel();
                        Invalidate();
                    }));
                }
                catch (InvalidOperationException) { }
            });
        }

        private void Source_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (string.IsNullOrEmpty(state.DescriptionSourceUrl)) return;
            try
            {
                Process.Start(new ProcessStartInfo(state.DescriptionSourceUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Storage.Log("Abrir fuente de descripción: " + ex.Message);
                MessageBox.Show(FindForm(), "No se pudo abrir la fuente: " + ex.Message,
                    "Descripción", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RaiseCloseRequested()
        {
            if (CloseRequested != null) CloseRequested(this, EventArgs.Empty);
        }

        private void LayoutDetails()
        {
            if (Width <= 0 || Height <= 0 || title == null) return;

            int pad = Theme.Px(28);
            int coverHeight = Math.Min(Theme.Px(500), Math.Max(Theme.Px(220), Height - Theme.Px(104)));
            int coverWidth = Math.Min(Theme.Px(334), coverHeight * 2 / 3);
            coverHeight = coverWidth * 3 / 2;
            int coverY = Math.Max(Theme.Px(66), (Height - coverHeight) / 2);
            coverBounds = new Rectangle(pad, coverY, coverWidth, coverHeight);

            int rightX = coverBounds.Right + Theme.Px(32);
            int rightWidth = Math.Max(Theme.Px(280), Width - rightX - pad);
            backButton.SetBounds(pad, Theme.Px(18), Theme.Px(132), Theme.Px(34));
            eyebrow.SetBounds(rightX, Theme.Px(34), rightWidth, Theme.Px(20));
            title.SetBounds(rightX, Theme.Px(60), rightWidth, Theme.Px(62));
            platform.SetBounds(rightX, Theme.Px(126), rightWidth, Theme.Px(20));

            int tileGap = Theme.Px(10);
            int tileWidth = Math.Max(Theme.Px(80), (rightWidth - tileGap * 2) / 3);
            int tileY = Theme.Px(158);
            for (int i = 0; i < statTiles.Length; i++)
            {
                int x = rightX + i * (tileWidth + tileGap);
                statTiles[i].SetBounds(x, tileY, tileWidth, Theme.Px(66));
                statValues[i].SetBounds(Theme.Px(12), Theme.Px(10), tileWidth - Theme.Px(24), Theme.Px(23));
                statCaptions[i].SetBounds(Theme.Px(12), Theme.Px(38), tileWidth - Theme.Px(24), Theme.Px(16));
            }

            int descriptionTop = accountsDetails.Visible ? Theme.Px(294) : Theme.Px(247);
            if (accountsDetails.Visible)
                accountsDetails.SetBounds(rightX, Theme.Px(232), rightWidth, Theme.Px(54));
            descriptionHeading.SetBounds(rightX, descriptionTop, rightWidth - Theme.Px(110), Theme.Px(24));
            refreshButton.SetBounds(rightX + rightWidth - Theme.Px(100), descriptionTop - Theme.Px(2),
                Theme.Px(100), Theme.Px(28));
            source.SetBounds(rightX, descriptionTop + Theme.Px(26), rightWidth, Theme.Px(20));
            int descriptionY = descriptionTop + Theme.Px(52);
            int playY = Height - Theme.Px(56);
            int descriptionHeight = Math.Max(Theme.Px(70), playY - descriptionY - Theme.Px(14));
            description.SetBounds(rightX, descriptionY, rightWidth, descriptionHeight);
            playButton.SetBounds(rightX + rightWidth - Theme.Px(156), playY, Theme.Px(156), Theme.Px(42));
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutDetails();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.HighQuality;
            using (LinearGradientBrush background = new LinearGradientBrush(ClientRectangle,
                Theme.BgSoft, Theme.Bg, LinearGradientMode.Vertical))
                g.FillRectangle(background, ClientRectangle);

            if (cover != null && Width > 0 && Height > 0)
            {
                using (ImageAttributes attributes = new ImageAttributes())
                {
                    ColorMatrix matrix = new ColorMatrix();
                    matrix.Matrix33 = Theme.IsLight ? 0.06f : 0.12f;
                    attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                    g.DrawImage(cover, new Rectangle(0, 0, Width, Height),
                        0, 0, cover.Width, cover.Height, GraphicsUnit.Pixel, attributes);
                }
                using (SolidBrush shade = new SolidBrush(Color.FromArgb(205, Theme.Bg)))
                    g.FillRectangle(shade, ClientRectangle);
            }

            using (LinearGradientBrush shade = new LinearGradientBrush(ClientRectangle,
                Color.FromArgb(0, Theme.Accent), Color.FromArgb(14, Theme.Accent2), 0f))
                g.FillRectangle(shade, ClientRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            RectangleF coverRect = new RectangleF(coverBounds.X, coverBounds.Y,
                coverBounds.Width, coverBounds.Height);
            using (GraphicsPath path = Theme.Round(coverRect, Theme.Pxf(14f)))
            {
                GraphicsState state = g.Save();
                g.SetClip(path);
                if (cover != null) DrawCover(g, cover, coverRect);
                else DrawCoverPlaceholder(g, coverRect);
                using (LinearGradientBrush shade = new LinearGradientBrush(
                    new Rectangle(coverBounds.X, coverBounds.Y + coverBounds.Height / 2,
                        coverBounds.Width, coverBounds.Height / 2),
                    Color.FromArgb(0, 0, 0, 0), Color.FromArgb(90, 0, 0, 0), LinearGradientMode.Vertical))
                    g.FillRectangle(shade, coverBounds.X, coverBounds.Y + coverBounds.Height / 2,
                        coverBounds.Width, coverBounds.Height / 2);
                    g.Restore(state);
                Theme.StrokeRound(g, coverRect, Theme.Pxf(14f), Color.FromArgb(115, Theme.Text), 1f);
            }
        }

        private void DrawCoverPlaceholder(Graphics g, RectangleF rect)
        {
            Color brand = Theme.PlatformColor(game.Platform);
            using (LinearGradientBrush brush = new LinearGradientBrush(rect,
                Theme.Blend(Theme.Surface, brand, 0.24f), Theme.Bg, LinearGradientMode.Vertical))
                g.FillRectangle(brush, rect);
            TextRenderer.DrawText(g, Initials(game.Title), Theme.CardInitials,
                Rectangle.Round(rect), Color.FromArgb(160, brand),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private static void DrawCover(Graphics g, Image image, RectangleF dest)
        {
            float destRatio = dest.Width / dest.Height;
            float sourceRatio = (float)image.Width / image.Height;
            RectangleF source;
            if (sourceRatio > destRatio)
            {
                float width = image.Height * destRatio;
                source = new RectangleF((image.Width - width) / 2f, 0, width, image.Height);
            }
            else
            {
                float height = image.Width / destRatio;
                source = new RectangleF(0, (image.Height - height) / 2f, image.Width, height);
            }
            g.DrawImage(image, dest, source, GraphicsUnit.Pixel);
        }

        private static string Initials(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "?";
            string[] parts = value.Split(new char[] { ' ', ':', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant();
            return parts[0].Substring(0, 1).ToUpperInvariant() + parts[1].Substring(0, 1).ToUpperInvariant();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && cover != null) cover.Dispose();
            base.Dispose(disposing);
        }
    }
}
