using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace GamesOfAll
{
    internal sealed class MainForm : Form
    {
        private const int WM_NCHITTEST = 0x84;

        private readonly Settings settings;
        private List<Game> games = new List<Game>();
        private readonly List<Game> visibleGames = new List<Game>();

        private readonly Queue<Game> coverQueue = new Queue<Game>();
        private readonly object coverLock = new object();
        private bool coverWorkerRunning;

        private readonly Dictionary<string, DateTime> runningSince = new Dictionary<string, DateTime>();
        private readonly Dictionary<string, GameAccountStats> runningAccounts =
            new Dictionary<string, GameAccountStats>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, GameAccountStats> runningSteamAccounts =
            new Dictionary<string, GameAccountStats>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<string>> processHintCache = new Dictionary<string, List<string>>();
        private readonly HashSet<string> newIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> riotLookupsInProgress = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> lastRiotLookupAttempt =
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly object riotLookupLock = new object();
        private bool riotApiKeyNoticeShown;
        private GameAccountStats latestRiotAccount;
        private DateTime lastRiotProfileRefresh = DateTime.MinValue;

        private GridPanel grid;
        private Panel topBar;
        private Panel filtersBar;
        private Panel bottomBar;
        private GameDetailsPanel detailsPanel;
        private LoadingOverlay loadingOverlay;
        private SearchBox search;
        private PillButton addButton;
        private PillButton statsButton;
        private PillButton settingsButton;
        private PillButton minButton;
        private PillButton maxButton;
        private PillButton closeButton;
        private PillButton chipAll;
        private PillButton chipSteam;
        private PillButton chipEpic;
        private PillButton chipRiot;
        private PillButton chipLocal;
        private PillButton chipEmulated;
        private PillButton chipFav;
        private PillButton sortButton;
        private Label statusText;
        private Label hintText;

        private string filter = "all";
        private bool favoritesOnly;
        private string selectedId;
        private string defaultHint = "Clic para ver detalles  ·  Clic derecho para más opciones";

        private System.Windows.Forms.Timer reorderTimer;
        private System.Windows.Forms.Timer statusTimer;
        private System.Windows.Forms.Timer playtimeTimer;

        private bool dragging;
        private Point dragStart;
        private bool scanning;
        private bool discovering;

        public MainForm()
        {
            settings = Storage.LoadSettings();
            Storage.EnsureFolders();
            Theme.Apply(settings.Theme);

            Text = "Games";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg;
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            KeyPreview = true;
            AllowDrop = true;
            MinimumSize = new Size(Theme.Px(900), Theme.Px(620));
            Font = Theme.Body;

            int w = settings.WindowWidth > 0 ? settings.WindowWidth : Theme.Px(1280);
            int h = settings.WindowHeight > 0 ? settings.WindowHeight : Theme.Px(810);
            ClientSize = new Size(w, h);

            BuildUi();
            RestoreWindowPosition();

            DragEnter += delegate(object s, DragEventArgs e)
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
            };
            DragDrop += MainForm_DragDrop;

            reorderTimer = new System.Windows.Forms.Timer();
            reorderTimer.Interval = 1400;
            reorderTimer.Tick += delegate { reorderTimer.Stop(); RefreshGrid(); };

            statusTimer = new System.Windows.Forms.Timer();
            statusTimer.Interval = 7000;
            statusTimer.Tick += delegate { statusTimer.Stop(); hintText.Text = defaultHint; };

            Shown += delegate
            {
                if (loadingOverlay != null) loadingOverlay.StartAnimation();
                StartScan();
                StartPlaytimeWatcher();
            };
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x20000; // sombra de ventana
                return cp;
            }
        }

        // ------------------------------------------------------------- CONSTRUCCION

        private void BuildUi()
        {
            grid = new GridPanel();
            grid.EmptyText = "Detectando tus juegos…";
            grid.EmptyHint = "Steam, Epic, Riot y los juegos instalados en el PC";

            topBar = new Panel();
            topBar.BackColor = Theme.BgSoft;
            topBar.Paint += TopBar_Paint;
            topBar.MouseDown += Drag_MouseDown;
            topBar.MouseMove += Drag_MouseMove;
            topBar.MouseUp += Drag_MouseUp;
            topBar.MouseDoubleClick += delegate { ToggleMaximize(); };

            filtersBar = new Panel();
            filtersBar.BackColor = Theme.Bg;

            bottomBar = new Panel();
            bottomBar.BackColor = Theme.BgSoft;
            bottomBar.Paint += BottomBar_Paint;

            statusText = new Label();
            statusText.AutoSize = false;
            statusText.BackColor = Color.Transparent;
            statusText.ForeColor = Theme.TextFaint;
            statusText.Font = Theme.Small;
            statusText.TextAlign = ContentAlignment.MiddleLeft;

            hintText = new Label();
            hintText.AutoSize = false;
            hintText.BackColor = Color.Transparent;
            hintText.ForeColor = Theme.TextFaint;
            hintText.Font = Theme.Small;
            hintText.TextAlign = ContentAlignment.MiddleRight;
            hintText.Text = defaultHint;

            search = new SearchBox("Buscar en tu biblioteca", "");
            search.QueryChanged += delegate { RefreshGrid(); };

            addButton = new PillButton("Añadir juego");
            addButton.Style = PillStyle.Accent;
            addButton.Icon = "\uE710";
            addButton.Click += delegate { AddGameDialogFlow(); };

            statsButton = new PillButton("");
            statsButton.Icon = "\uE9D2";
            statsButton.IconOnly = true;
            statsButton.Style = PillStyle.Ghost;
            statsButton.Click += delegate { ShowStats(); };

            settingsButton = new PillButton("");
            settingsButton.Icon = "\uE713";
            settingsButton.IconOnly = true;
            settingsButton.Style = PillStyle.Ghost;
            settingsButton.Click += delegate { ShowSettingsMenu(); };

            minButton = MakeWindowButton("\uE921", PillStyle.Ghost, delegate { WindowState = FormWindowState.Minimized; });
            maxButton = MakeWindowButton("\uE922", PillStyle.Ghost, delegate { ToggleMaximize(); });
            closeButton = MakeWindowButton("\uE8BB", PillStyle.Close, delegate { Close(); });

            chipAll = MakeChip("Todos", "all");
            chipSteam = MakeChip("Steam", "Steam");
            chipEpic = MakeChip("Epic", "Epic");
            chipRiot = MakeChip("Riot", "Riot");
            chipLocal = MakeChip("Locales", "Local");
            chipEmulated = MakeChip("Emuladores", "Emulated");

            chipFav = new PillButton("Favoritos");
            chipFav.Icon = "\uE734";
            chipFav.Click += delegate
            {
                favoritesOnly = !favoritesOnly;
                chipFav.Selected = favoritesOnly;
                chipFav.Invalidate();
                RefreshGrid();
            };

            sortButton = new PillButton("Recientes");
            sortButton.Icon = "\uE8CB";
            sortButton.Click += delegate
            {
                settings.SortMode = (settings.SortMode + 1) % 3;
                UpdateSortButton();
                SaveSettings();
                RefreshGrid();
            };

            topBar.Controls.Add(search);
            topBar.Controls.Add(addButton);
            topBar.Controls.Add(statsButton);
            topBar.Controls.Add(settingsButton);
            topBar.Controls.Add(minButton);
            topBar.Controls.Add(maxButton);
            topBar.Controls.Add(closeButton);

            filtersBar.Controls.Add(chipAll);
            filtersBar.Controls.Add(chipSteam);
            filtersBar.Controls.Add(chipEpic);
            filtersBar.Controls.Add(chipRiot);
            filtersBar.Controls.Add(chipLocal);
            filtersBar.Controls.Add(chipEmulated);
            filtersBar.Controls.Add(chipFav);
            filtersBar.Controls.Add(sortButton);

            bottomBar.Controls.Add(statusText);
            bottomBar.Controls.Add(hintText);

            Controls.Add(grid);
            Controls.Add(topBar);
            Controls.Add(filtersBar);
            Controls.Add(bottomBar);
            loadingOverlay = new LoadingOverlay();
            loadingOverlay.Dismissed += delegate
            {
                Controls.Remove(loadingOverlay);
                loadingOverlay.Dispose();
                loadingOverlay = null;
            };
            Controls.Add(loadingOverlay);
            loadingOverlay.BringToFront();

            chipAll.Selected = true;
            UpdateSortButton();
            LayoutUi();
        }

        private PillButton MakeChip(string text, string value)
        {
            PillButton chip = new PillButton(text);
            chip.Click += delegate { SetFilter(value); };
            return chip;
        }

        private PillButton MakeWindowButton(string icon, PillStyle style, EventHandler onClick)
        {
            PillButton b = new PillButton("");
            b.Icon = icon;
            b.IconOnly = true;
            b.Style = style;
            b.Click += onClick;
            return b;
        }

        private void LayoutUi()
        {
            if (topBar == null) return;
            int topH = Theme.Px(68);
            int filtersH = Theme.Px(54);
            int bottomH = Theme.Px(34);
            int w = ClientSize.Width;
            int h = ClientSize.Height;

            topBar.SetBounds(0, 0, w, topH);
            filtersBar.SetBounds(0, topH, w, filtersH);
            bottomBar.SetBounds(0, h - bottomH, w, bottomH);
            grid.SetBounds(0, topH + filtersH, w, Math.Max(0, h - topH - filtersH - bottomH));
            if (detailsPanel != null) detailsPanel.Bounds = grid.Bounds;
            if (loadingOverlay != null) loadingOverlay.Bounds = ClientRectangle;

            int y = (topH - Theme.Px(38)) / 2;
            int right = w - Theme.Px(14);

            closeButton.SetBounds(right - Theme.Px(32), y, Theme.Px(32), Theme.Px(30));
            maxButton.SetBounds(closeButton.Left - Theme.Px(30), y, Theme.Px(32), Theme.Px(30));
            minButton.SetBounds(maxButton.Left - Theme.Px(30), y, Theme.Px(32), Theme.Px(30));

            int searchW = Math.Min(Theme.Px(320), Math.Max(Theme.Px(170), w / 3));
            search.SetBounds(minButton.Left - Theme.Px(16) - searchW, y, searchW, Theme.Px(38));
            addButton.SetBounds(search.Left - Theme.Px(10) - Theme.Px(136), y, Theme.Px(136), Theme.Px(38));
            settingsButton.SetBounds(addButton.Left - Theme.Px(8) - Theme.Px(38), y, Theme.Px(38), Theme.Px(38));
            statsButton.SetBounds(settingsButton.Left - Theme.Px(4) - Theme.Px(38), y, Theme.Px(38), Theme.Px(38));

            LayoutChips();

            int by = (bottomH - Theme.Px(18)) / 2;
            statusText.SetBounds(Theme.Px(26), by, w / 2, Theme.Px(18));
            hintText.SetBounds(w / 2, by, w / 2 - Theme.Px(26), Theme.Px(18));

            grid.Relayout();
        }

        private void LayoutChips()
        {
            int x = Theme.Px(26);
            int y = (Theme.Px(54) - Theme.Px(32)) / 2;
            PillButton[] chips = { chipAll, chipSteam, chipEpic, chipRiot, chipLocal, chipEmulated, chipFav };
            foreach (PillButton chip in chips)
            {
                int width = ChipWidth(chip);
                chip.SetBounds(x, y, width, Theme.Px(32));
                x += width + Theme.Px(8);
            }

            int sortW = Theme.Px(126);
            sortButton.SetBounds(filtersBar.Width - Theme.Px(26) - sortW, y, sortW, Theme.Px(32));
        }

        private int ChipWidth(PillButton chip)
        {
            Size text = TextRenderer.MeasureText(chip.Text, chip.Font, new Size(400, 40),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            int extra = string.IsNullOrEmpty(chip.Icon) ? 0 : Theme.Px(22);
            return text.Width + Theme.Px(30) + extra;
        }

        private void UpdateSortButton()
        {
            if (settings.SortMode == 1) sortButton.Text = "A - Z";
            else if (settings.SortMode == 2) sortButton.Text = "Más jugados";
            else sortButton.Text = "Recientes";
            sortButton.Invalidate();
            if (filtersBar != null) LayoutChips();
        }

        // ------------------------------------------------------------------ PINTADO

        private void TopBar_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using (LinearGradientBrush brush = new LinearGradientBrush(
                new Rectangle(0, 0, topBar.Width, topBar.Height),
                Theme.Blend(Theme.BgSoft, Theme.Surface, 0.30f), Theme.BgSoft, LinearGradientMode.Vertical))
                g.FillRectangle(brush, 0, 0, topBar.Width, topBar.Height);

            using (Pen line = new Pen(Theme.Stroke))
                g.DrawLine(line, 0, topBar.Height - 1, topBar.Width, topBar.Height - 1);

            int size = Theme.Px(36);
            float ly = (topBar.Height - size) / 2f;
            RectangleF logo = new RectangleF(Theme.Px(26), ly, size, size);
            Theme.FillRoundGradient(g, logo, Theme.Pxf(11f), Theme.Accent, Theme.Accent2);

            using (SolidBrush white = new SolidBrush(Color.FromArgb(252, 255, 255, 255)))
            {
                PointF[] triangle =
                {
                    new PointF(logo.X + size * 0.38f, logo.Y + size * 0.27f),
                    new PointF(logo.X + size * 0.38f, logo.Y + size * 0.73f),
                    new PointF(logo.X + size * 0.72f, logo.Y + size * 0.5f)
                };
                g.FillPolygon(white, triangle);
            }

            int textX = (int)logo.Right + Theme.Px(11);
            DrawTracked(g, "Games", Theme.Logo, Theme.Text, textX, Theme.Px(16), Theme.Pxf(0.7f));
            DrawTracked(g, "Launcher", Theme.Small, Theme.TextFaint, textX + Theme.Px(1), Theme.Px(40), Theme.Pxf(1.4f));
        }

        private static void DrawTracked(Graphics g, string text, Font font, Color color, int x, int y, float tracking)
        {
            float cursor = x;
            foreach (char ch in text)
            {
                string s = ch.ToString();
                TextRenderer.DrawText(g, s, font, new Point((int)cursor, y), color,
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                Size sz = TextRenderer.MeasureText(g, s, font, new Size(200, 60),
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                cursor += sz.Width + tracking;
            }
        }

        private void BottomBar_Paint(object sender, PaintEventArgs e)
        {
            using (Pen line = new Pen(Color.FromArgb(28, 33, 42)))
                e.Graphics.DrawLine(line, 0, 0, bottomBar.Width, 0);
        }

        // ------------------------------------------------------------------ ESCANEO

        private void StartScan()
        {
            scanning = true;
            grid.EmptyText = "Detectando tus juegos…";
            grid.EmptyHint = "Steam, Epic, Riot y los juegos instalados en el PC";
            grid.Invalidate();
            hintText.Text = "Buscando juegos instalados…";

            Thread worker = new Thread(delegate()
            {
                List<Game> result = null;
                try { result = Scanner.ScanAll(); }
                catch (Exception ex) { Storage.Log("Escanear: " + ex); }

                try
                {
                    if (IsHandleCreated)
                        BeginInvoke(new Action(delegate { ApplyScan(result ?? new List<Game>()); }));
                }
                catch { }
            });
            worker.IsBackground = true;
            worker.Start();
        }

        private void ApplyScan(List<Game> scanned)
        {
            scanning = false;
            games = scanned;
            foreach (Game custom in settings.CustomGames)
            {
                if (custom == null || string.IsNullOrEmpty(custom.Id)) continue;
                bool exists = false;
                foreach (Game g in games) if (string.Equals(g.Id, custom.Id, StringComparison.OrdinalIgnoreCase)) { exists = true; break; }
                if (!exists) games.Add(custom);
            }

            foreach (Game g in games) StateOf(g);

            RefreshGrid();
            RefreshKnownRiotAccountsOnline();
            hintText.Text = games.Count == 0
                ? "No se encontraron juegos. Usa «Añadir juego»."
                : defaultHint;

            foreach (Game g in games) QueueCover(g);
            StartAutoDiscovery();
            if (loadingOverlay != null) loadingOverlay.Dismiss();
        }

        private GameState StateOf(Game g)
        {
            GameState st;
            if (!settings.States.TryGetValue(g.Id, out st) || st == null)
            {
                st = new GameState();
                settings.States[g.Id] = st;
            }
            if (st.AccountStats == null) st.AccountStats = new List<GameAccountStats>();
            if (g.LastPlayed > st.LastPlayed) st.LastPlayed = g.LastPlayed;
            return st;
        }

        // ------------------------------------------------------------------- LISTA

        private void RefreshGrid()
        {
            string keep = selectedId;
            string query = search.Query;
            visibleGames.Clear();

            foreach (Game g in games)
            {
                GameState st = StateOf(g);
                if (!settings.ShowHidden && st.Hidden) continue;
                if (favoritesOnly && !st.Favorite) continue;
                if (filter == "Emulated")
                {
                    if (!g.IsEmulated) continue;
                }
                else if (filter != "all" && !string.Equals(g.PlatformName, filter, StringComparison.OrdinalIgnoreCase)) continue;
                if (query.Length > 0 && g.Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                visibleGames.Add(g);
            }
            visibleGames.Sort(CompareGames);

            List<GameCard> cards = new List<GameCard>(visibleGames.Count);
            foreach (Game g in visibleGames)
            {
                GameState st = StateOf(g);
                GameCard card = new GameCard(g);
                card.IsFavorite = st.Favorite;
                card.IsNew = newIds.Contains(g.Id);
                card.PlaySeconds = GetPlaySeconds(g, st);
                card.Cover = Covers.Load(g, st);
                card.Activated += Card_Activated;
                card.MouseUp += Card_MouseUp;
                card.MouseEnter += delegate(object sender, EventArgs args)
                {
                    grid.SetAmbient(card.Cover);
                };
                card.MouseLeave += delegate(object sender, EventArgs args)
                {
                    GameCard selected = grid.FindCard(selectedId);
                    grid.SetAmbient(selected != null ? selected.Cover : null);
                };
                cards.Add(card);
            }
            grid.SetCards(cards);

            bool searching = search.ContainsFocus;
            GameCard target = null;
            if (!string.IsNullOrEmpty(keep)) target = grid.FindCard(keep);
            if (target == null && grid.Cards.Count > 0) target = grid.Cards[0];
            if (target != null)
            {
                selectedId = target.Game.Id;
                grid.SetAmbient(target.Cover);
                if (!searching) target.Focus();
            }
            else selectedId = null;

            if (grid.Cards.Count == 0)
            {
                if (scanning)
                {
                    grid.EmptyText = "Detectando tus juegos…";
                    grid.EmptyHint = "Steam, Epic, Riot y los juegos instalados en el PC";
                }
                else if (query.Length > 0)
                {
                    grid.EmptyText = "Ningún juego coincide con «" + query + "»";
                    grid.EmptyHint = "Prueba con otro nombre o borra la búsqueda";
                }
                else if (favoritesOnly)
                {
                    grid.EmptyText = "Todavía no tienes favoritos";
                    grid.EmptyHint = "Clic derecho en un juego → Marcar como favorito";
                }
                else if (filter == "Emulated")
                {
                    grid.EmptyText = "Todavía no hay juegos emulados";
                    grid.EmptyHint = settings.Emulators.Count == 0
                        ? "Configura tu primer emulador en Ajustes → Emuladores"
                        : settings.Emulators.Count + (settings.Emulators.Count == 1
                            ? " emulador configurado · añade una ROM o carpeta de juegos"
                            : " emuladores configurados · añade una ROM o carpeta de juegos");
                }
                else if (games.Count > 0)
                {
                    grid.EmptyText = "No hay juegos en este filtro";
                    grid.EmptyHint = "Cambia de plataforma o pulsa «Todos»";
                }
                else
                {
                    grid.EmptyText = "Tu biblioteca está vacía";
                    grid.EmptyHint = "Pulsa «Añadir juego» o arrastra un .exe aquí";
                }
                grid.Invalidate();
            }

            UpdateCounts();
        }

        private int CompareGames(Game a, Game b)
        {
            GameState sa = StateOf(a);
            GameState sb = StateOf(b);
            if (sa.Favorite != sb.Favorite) return sa.Favorite ? -1 : 1;

            if (settings.SortMode == 2)
            {
                int played = GetPlaySeconds(b, sb).CompareTo(GetPlaySeconds(a, sa));
                if (played != 0) return played;
            }
            else if (settings.SortMode == 0)
            {
                int last = sb.LastPlayed.CompareTo(sa.LastPlayed);
                if (last != 0) return last;
            }
            return string.Compare(a.Title, b.Title, StringComparison.CurrentCultureIgnoreCase);
        }

        private void UpdateCounts()
        {
            int total = games.Count;
            HashSet<Platform> platforms = new HashSet<Platform>();
            foreach (Game g in games) platforms.Add(g.Platform);

            string text;
            if (total == 0) text = "Sin juegos detectados";
            else
            {
                text = total + (total == 1 ? " juego" : " juegos");
                text += "  ·  " + platforms.Count + (platforms.Count == 1 ? " plataforma" : " plataformas");
                if (visibleGames.Count != total) text += "  ·  " + visibleGames.Count + " visibles";
            }
            statusText.Text = text;
        }

        private void SetFilter(string value)
        {
            filter = value;
            chipAll.Selected = value == "all";
            chipSteam.Selected = value == "Steam";
            chipEpic.Selected = value == "Epic";
            chipRiot.Selected = value == "Riot";
            chipLocal.Selected = value == "Local";
            chipEmulated.Selected = value == "Emulated";
            foreach (PillButton c in new PillButton[] { chipAll, chipSteam, chipEpic, chipRiot, chipLocal, chipEmulated })
                c.Invalidate();
            RefreshGrid();
        }

        // ------------------------------------------------------------------ ACCIONES

        private void Card_Activated(object sender, EventArgs e)
        {
            GameCard card = sender as GameCard;
            if (card != null)
            {
                selectedId = card.Game.Id;
                grid.SetAmbient(card.Cover);
                ShowGameDetails(card.Game);
            }
        }

        private void Card_MouseUp(object sender, MouseEventArgs e)
        {
            GameCard card = sender as GameCard;
            if (card == null || e.Button != MouseButtons.Right) return;
            selectedId = card.Game.Id;
            card.Focus();
            ShowGameMenu(card);
        }

        private void Launch(Game g)
        {
            SyncEmulatorConfig(g);
            string error;
            if (!GameLauncher.Launch(g, out error))
            {
                hintText.Text = "No se pudo iniciar " + g.Title;
                MessageBox.Show(this, error, "No se pudo iniciar " + g.Title,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            GameState st = StateOf(g);
            st.LastPlayed = Fmt.ToUnix(DateTime.UtcNow);
            st.PlayCount = st.PlayCount + 1;
            newIds.Remove(g.Id);
            SaveSettings();

            hintText.Text = "Iniciando " + g.Title + "…";
            statusTimer.Stop();
            statusTimer.Start();

            if (settings.MinimizeOnLaunch) WindowState = FormWindowState.Minimized;
            reorderTimer.Stop();
            reorderTimer.Start();
        }

        private void ShowGameDetails(Game game)
        {
            if (game == null) return;
            CloseGameDetails();
            GameState state = StateOf(game);
            detailsPanel = new GameDetailsPanel(game, state, GetPlaySeconds(game, state),
                GetAccountStats(game, state), SaveSettings);
            detailsPanel.PlayRequested += delegate
            {
                CloseGameDetails();
                Launch(game);
            };
            detailsPanel.CloseRequested += delegate { CloseGameDetails(); };
            detailsPanel.Bounds = grid.Bounds;
            Controls.Add(detailsPanel);
            detailsPanel.BringToFront();
            detailsPanel.Focus();
            detailsPanel.BeginLookup();
        }

        private void CloseGameDetails()
        {
            if (detailsPanel == null) return;
            GameDetailsPanel closing = detailsPanel;
            detailsPanel = null;
            Controls.Remove(closing);
            closing.Dispose();
            GameCard card = grid.FindCard(selectedId);
            if (card != null) card.Focus();
        }

        private void ShowGameMenu(GameCard card)
        {
            Game g = card.Game;
            GameState st = StateOf(g);
            ContextMenuStrip menu = Menus.Create();

            ToolStripMenuItem play = Menus.Item(menu, "   Jugar", delegate { Launch(g); });
            play.Font = Theme.BodyBold;

            Menus.Separator(menu);
            if (!string.IsNullOrEmpty(g.InstallDir) && Directory.Exists(g.InstallDir))
                Menus.Item(menu, "   Abrir carpeta de instalación", delegate { GameLauncher.OpenFolder(g.InstallDir); });
            Menus.Item(menu, "   Cambiar carátula…", delegate { ChangeCover(g); });
            Menus.Item(menu, st.Favorite ? "   Quitar de favoritos" : "   Marcar como favorito", delegate
            {
                st.Favorite = !st.Favorite;
                SaveSettings();
                RefreshGrid();
            });

            Menus.Separator(menu);
            long playSeconds = GetPlaySeconds(g, st);
            string timeText = playSeconds >= 60 ? Fmt.Playtime(playSeconds) + " jugadas" : "Sin horas registradas";
            ToolStripMenuItem info = Menus.Item(menu, "   " + timeText, null);
            info.Enabled = false;

            Menus.Item(menu, "   Ocultar de la biblioteca", delegate
            {
                st.Hidden = true;
                SaveSettings();
                RefreshGrid();
                hintText.Text = "«" + g.Title + "» oculto. Actívalo en el menú de ajustes.";
            });

            if (g.Platform == Platform.Local && IsCustomGame(g.Id))
                Menus.Item(menu, "   Quitar de la lista", delegate { RemoveCustomGame(g); });

            menu.Show(card, new Point(Theme.Px(20), Theme.Px(20)));
        }

        private bool IsCustomGame(string id)
        {
            foreach (Game g in settings.CustomGames)
                if (string.Equals(g.Id, id, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private void ChangeCover(Game g)
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "Elige una imagen para " + g.Title;
                dlg.Filter = "Imágenes (*.jpg;*.jpeg;*.png;*.webp;*.bmp)|*.jpg;*.jpeg;*.png;*.webp;*.bmp|Todos los archivos (*.*)|*.*";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    Storage.EnsureFolders();
                    string ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();
                    if (ext != ".png" && ext != ".jpg" && ext != ".jpeg" && ext != ".bmp") ext = ".jpg";
                    string name = g.CoverKey + "_custom" + ext;
                    string dest = Path.Combine(Storage.CoversDir, name);
                    using (Image probe = Covers.LoadFile(dlg.FileName))
                        if (probe == null) throw new InvalidOperationException("El archivo no es una imagen válida.");

                    GameState st = StateOf(g);
                    string previous = st.CustomCover;
                    File.Copy(dlg.FileName, dest, true);
                    st.CustomCover = name;
                    SaveSettings();
                    if (!string.IsNullOrEmpty(previous) && !string.Equals(previous, name, StringComparison.OrdinalIgnoreCase))
                    {
                        try { File.Delete(Path.Combine(Storage.CoversDir, previous)); } catch { }
                    }
                    RefreshGrid();
                    hintText.Text = "Carátula actualizada.";
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "No se pudo usar la imagen: " + ex.Message, "Carátula",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void RemoveCustomGame(Game g)
        {
            DialogResult r = MessageBox.Show(this,
                "¿Quitar «" + g.Title + "» de la biblioteca?\nEl juego no se desinstala.",
                "Quitar juego", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes) return;

            settings.CustomGames.RemoveAll(delegate(Game x)
            {
                return string.Equals(x.Id, g.Id, StringComparison.OrdinalIgnoreCase);
            });
            settings.States.Remove(g.Id);
            games.RemoveAll(delegate(Game x)
            {
                return string.Equals(x.Id, g.Id, StringComparison.OrdinalIgnoreCase);
            });
            if (!string.IsNullOrEmpty(g.Executable)) AddIgnored(g.Executable);
            SaveSettings();
            RefreshGrid();
        }

        // ------------------------------------------------------------------- AÑADIR

        private void AddGameDialogFlow()
        {
            EnsureEmulatorIds();
            using (AddGameDialog dlg = new AddGameDialog("", settings.Emulators))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                bool added = AddLocalGame(dlg.GameName, dlg.GameExe, dlg.GameArgs,
                    Path.GetDirectoryName(dlg.GameExe), dlg.Kind, dlg.GameRom, dlg.PlatformLabel,
                    dlg.EmulatorId);
                if (!added) SaveSettings();
                if (added) hintText.Text = "«" + (dlg.GameName.Length > 0 ? dlg.GameName : Path.GetFileNameWithoutExtension(dlg.GameExe)) + "» añadido.";
                else hintText.Text = "Ese juego ya está en la biblioteca.";
            }
        }

        private bool AddLocalGame(string title, string exe, string args, string installDir,
            GameKind kind, string rom, string platformLabel, string emulatorId = null)
        {
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) return false;

            string seed = kind == GameKind.Emulated && !string.IsNullOrEmpty(rom) ? exe + "|" + rom : exe;
            string id = "local:" + StableHash(seed.ToLowerInvariant());

            foreach (Game g in games)
            {
                bool sameGame = string.Equals(g.Id, id, StringComparison.OrdinalIgnoreCase) ||
                    (kind == GameKind.Emulated && !string.IsNullOrEmpty(rom) &&
                     string.Equals(g.RomPath, rom, StringComparison.OrdinalIgnoreCase));
                if (sameGame)
                {
                    if (kind == GameKind.Emulated)
                        UpdateEmulatorLink(g, exe, args, installDir, platformLabel, emulatorId);
                    return false;
                }
                if (kind == GameKind.Native && !string.IsNullOrEmpty(g.Executable) &&
                    string.Equals(g.Executable, exe, StringComparison.OrdinalIgnoreCase)) return false;
            }

            Game game = new Game();
            game.Id = id;
            game.Title = string.IsNullOrEmpty(title) ? Path.GetFileNameWithoutExtension(exe) : title;
            game.Platform = Platform.Local;
            game.Kind = kind;
            game.LaunchTarget = exe;
            game.Executable = exe;
            game.LaunchArgs = args;
            game.IconSource = exe;
            game.InstallDir = string.IsNullOrEmpty(installDir) ? Path.GetDirectoryName(exe) : installDir;
            game.CustomPlatform = string.IsNullOrEmpty(platformLabel) ? null : platformLabel.Trim();
            game.RomPath = rom;
            game.EmulatorId = emulatorId;

            settings.CustomGames.Add(game);
            games.Add(game);
            newIds.Add(id);
            RemoveIgnored(exe);
            SaveSettings();

            selectedId = id;
            RefreshGrid();
            QueueCover(game);
            return true;
        }

        private bool UpdateEmulatorLink(Game game, string exe, string args, string installDir,
            string platformLabel, string emulatorId)
        {
            bool changed = false;
            if (!string.Equals(game.Executable, exe, StringComparison.OrdinalIgnoreCase))
            {
                game.Executable = exe;
                game.LaunchTarget = exe;
                game.IconSource = exe;
                changed = true;
            }
            if (!string.Equals(game.LaunchArgs, args, StringComparison.Ordinal))
            {
                game.LaunchArgs = args;
                changed = true;
            }
            if (!string.Equals(game.InstallDir, installDir, StringComparison.OrdinalIgnoreCase))
            {
                game.InstallDir = installDir;
                changed = true;
            }
            if (!string.Equals(game.CustomPlatform, platformLabel, StringComparison.Ordinal))
            {
                game.CustomPlatform = platformLabel;
                changed = true;
            }
            if (!string.Equals(game.EmulatorId, emulatorId, StringComparison.OrdinalIgnoreCase))
            {
                game.EmulatorId = emulatorId;
                changed = true;
            }
            return changed;
        }

        private void SyncEmulatorConfig(Game game)
        {
            if (game == null || !game.IsEmulated) return;
            EmulatorConfig selected = null;
            foreach (EmulatorConfig emulator in settings.Emulators)
            {
                if (!string.IsNullOrEmpty(game.EmulatorId) &&
                    string.Equals(emulator.Id, game.EmulatorId, StringComparison.OrdinalIgnoreCase))
                {
                    selected = emulator;
                    break;
                }
                if (selected == null && !string.IsNullOrEmpty(emulator.ExePath) &&
                    string.Equals(emulator.ExePath, game.Executable, StringComparison.OrdinalIgnoreCase))
                    selected = emulator;
            }
            if (selected == null) return;

            UpdateEmulatorLink(game, selected.ExePath,
                string.IsNullOrEmpty(selected.Arguments) ? "{rom}" : selected.Arguments,
                Path.GetDirectoryName(selected.ExePath),
                string.IsNullOrEmpty(selected.Name) ? "Emulado" : selected.Name, selected.Id);
        }

        private static string StableHash(string text)
        {
            uint hash = 2166136261;
            foreach (char c in text)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return hash.ToString("x8");
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null) return;
            int added = 0;
            foreach (string file in files)
            {
                if (!file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                if (AddLocalGame(Path.GetFileNameWithoutExtension(file), file, "", Path.GetDirectoryName(file),
                        GameKind.Native, null, null)) added++;
            }
            if (added > 0) hintText.Text = added == 1 ? "Juego añadido desde el archivo." : added + " juegos añadidos.";
        }

        // --------------------------------------------------------------- DESCUBRIR

        private void StartAutoDiscovery()
        {
            if (!settings.AutoDiscover || discovering) return;
            discovering = true;

            Thread worker = new Thread(delegate()
            {
                List<GameCandidate> result = null;
                try { result = Discovery.DiscoverAll(); }
                catch (Exception ex) { Storage.Log("Descubrir: " + ex.Message); }

                try
                {
                    if (IsHandleCreated)
                        BeginInvoke(new Action(delegate { ApplyAutoDiscovery(result); }));
                }
                catch { }
            });
            worker.IsBackground = true;
            worker.Start();
        }

        private void ApplyAutoDiscovery(List<GameCandidate> candidates)
        {
            discovering = false;
            if (candidates == null) return;

            List<string> addedTitles = new List<string>();
            foreach (GameCandidate c in candidates)
            {
                if (!c.AutoAdd) continue;
                if (IsIgnored(c.Exe) || ExistsExecutable(c.Exe)) continue;
                if (AddLocalGame(c.Title, c.Exe, c.Args, c.Folder, GameKind.Native, null, null))
                    addedTitles.Add(c.Title);
            }

            if (addedTitles.Count > 0)
            {
                SaveSettings();
                RefreshGrid();
                hintText.Text = addedTitles.Count == 1
                    ? "Juego nuevo añadido: " + addedTitles[0]
                    : addedTitles.Count + " juegos nuevos añadidos automáticamente";
            }
        }

        private void ShowDiscoveryDialog()
        {
            HashSet<string> existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Game g in games)
            {
                if (!string.IsNullOrEmpty(g.Executable)) existing.Add(g.Executable);
                if (!string.IsNullOrEmpty(g.RomPath)) existing.Add(g.RomPath);
            }

            using (DiscoveryDialog dlg = new DiscoveryDialog(existing))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                int added = 0;
                foreach (GameCandidate c in dlg.Selected)
                {
                    if (AddLocalGame(c.Title, c.Exe, c.Args, c.Folder, GameKind.Native, null, null)) added++;
                }
                if (added > 0)
                {
                    SaveSettings();
                    RefreshGrid();
                    hintText.Text = added == 1 ? "1 juego añadido." : added + " juegos añadidos.";
                }
                else hintText.Text = "No se añadió ningún juego.";
            }
        }

        private bool ExistsExecutable(string exe)
        {
            if (string.IsNullOrEmpty(exe)) return false;
            foreach (Game g in games)
            {
                if (!string.IsNullOrEmpty(g.Executable) &&
                    string.Equals(g.Executable, exe, StringComparison.OrdinalIgnoreCase)) return true;
                if (!string.IsNullOrEmpty(g.InstallDir) &&
                    exe.StartsWith(g.InstallDir.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private bool IsIgnored(string exe)
        {
            if (string.IsNullOrEmpty(exe)) return false;
            foreach (string p in settings.IgnoredPaths)
                if (string.Equals(p, exe, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private void AddIgnored(string exe)
        {
            if (!IsIgnored(exe)) settings.IgnoredPaths.Add(exe);
        }

        private void RemoveIgnored(string exe)
        {
            settings.IgnoredPaths.RemoveAll(delegate(string p)
            {
                return string.Equals(p, exe, StringComparison.OrdinalIgnoreCase);
            });
        }

        // -------------------------------------------------------------- ESTADISTICAS

        private void ShowStats()
        {
            List<StatEntry> entries = new List<StatEntry>();
            long totalSeconds = 0;
            foreach (Game g in games)
            {
                GameState st = StateOf(g);
                long playSeconds = GetPlaySeconds(g, st);
                if (playSeconds < 60) continue;
                totalSeconds += playSeconds;
                StatEntry entry = new StatEntry();
                entry.Title = g.Title;
                entry.Platform = g.PlatformName;
                entry.PlaySeconds = playSeconds;
                entry.LastPlayed = st.LastPlayed;
                entry.PlayCount = st.PlayCount;
                entry.Cover = Covers.Load(g, st);
                entries.Add(entry);
            }

            entries.Sort(delegate(StatEntry a, StatEntry b)
            {
                int c = b.PlaySeconds.CompareTo(a.PlaySeconds);
                if (c != 0) return c;
                return string.Compare(a.Title, b.Title, StringComparison.CurrentCultureIgnoreCase);
            });

            string summary;
            if (entries.Count == 0)
                summary = "Aún no hay horas registradas; empieza a jugar y se contarán solas.";
            else
                summary = Fmt.Playtime(totalSeconds) + " en total  ·  " + entries.Count +
                          (entries.Count == 1 ? " juego con tiempo" : " juegos con tiempo");

            using (StatsDialog dlg = new StatsDialog(entries, summary))
                dlg.ShowDialog(this);
        }

        // ------------------------------------------------------------ HORAS JUGADAS

        private void StartPlaytimeWatcher()
        {
            playtimeTimer = new System.Windows.Forms.Timer();
            playtimeTimer.Interval = 15000;
            playtimeTimer.Tick += delegate { CheckRunningGames(); };
            playtimeTimer.Start();
        }

        private void CheckRunningGames()
        {
            if (games.Count == 0) return;

            HashSet<string> running = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                Process[] all = Process.GetProcesses();
                foreach (Process p in all)
                {
                    try
                    {
                        string name = p.ProcessName;
                        if (!string.IsNullOrEmpty(name)) running.Add(name.ToLowerInvariant());
                    }
                    catch { }
                    finally { p.Dispose(); }
                }
            }
            catch { }

            DateTime now = DateTime.Now;
            bool changed = false;

            Game league = FindLeagueGame();
            if (league != null &&
                (running.Contains("leagueclientux") || running.Contains("leagueclient")) &&
                (now - lastRiotProfileRefresh).TotalSeconds >= 60)
            {
                lastRiotProfileRefresh = now;
                GameAccountStats profile = ReadRiotAccount(league);
                if (profile != null)
                {
                    latestRiotAccount = profile;
                    if (UpdateRiotAccountStats(league, profile, 0)) changed = true;
                    QueueRiotOnlineLookup(profile, false);
                }
            }

            foreach (Game g in games)
            {
                bool active = false;
                foreach (string hint in ProcessHints(g))
                {
                    if (running.Contains(hint)) { active = true; break; }
                }

                bool tracked = runningSince.ContainsKey(g.Id);
                if (active && !tracked)
                {
                    runningSince[g.Id] = now;
                    if (g.Platform == Platform.Steam)
                    {
                        Scanner.RefreshSteamPlaytimes(games);
                        GameAccountStats account = PrepareSteamAccount(g);
                        if (account != null) runningSteamAccounts[g.Id] = account;
                        UpdateCardPlaytime(g.Id);
                    }
                    else if (g.Platform == Platform.Riot)
                    {
                        GameAccountStats profile = ReadRiotAccount(g);
                        if (profile == null && latestRiotAccount != null)
                            profile = CopyAccountStats(latestRiotAccount);
                        if (profile != null)
                        {
                            runningAccounts[g.Id] = profile;
                            if (UpdateRiotAccountStats(g, profile, 0)) changed = true;
                            QueueRiotOnlineLookup(profile, true);
                        }
                    }
                }
                else if (!active && tracked)
                {
                    DateTime start = runningSince[g.Id];
                    runningSince.Remove(g.Id);
                    GameAccountStats account = null;
                    if (g.Platform == Platform.Riot)
                    {
                        GameAccountStats current = ReadRiotAccount(g);
                        runningAccounts.TryGetValue(g.Id, out account);
                        account = MergeRiotSessionAccount(account, current);
                        runningAccounts.Remove(g.Id);
                    }
                    else if (g.Platform == Platform.Steam)
                    {
                        runningSteamAccounts.TryGetValue(g.Id, out account);
                        runningSteamAccounts.Remove(g.Id);
                    }
                    long seconds = (long)(now - start).TotalSeconds;
                    if (seconds >= 15)
                    {
                        RecordGameSession(g, seconds, account);
                        changed = true;
                        if (g.Platform == Platform.Riot && account != null)
                            QueueRiotOnlineLookup(account, true);
                        if (g.Platform == Platform.Steam) Scanner.RefreshSteamPlaytimes(games);
                        UpdateCardPlaytime(g.Id);
                    }
                }
            }

            if (changed)
            {
                SaveSettings();
                if (settings.SortMode == 2) RefreshGrid();
            }
        }

        private List<string> ProcessHints(Game g)
        {
            List<string> cached;
            if (processHintCache.TryGetValue(g.Id, out cached)) return cached;

            List<string> hints = g.ProcessHints();
            if (g.Platform == Platform.Riot &&
                string.Equals(g.LaunchTarget, "league_of_legends", StringComparison.OrdinalIgnoreCase))
            {
                hints.Clear();
                hints.Add("league of legends");
            }
            if (hints.Count == 0 && !string.IsNullOrEmpty(g.InstallDir) && Directory.Exists(g.InstallDir))
            {
                try
                {
                    foreach (string name in Discovery.ProcessNamesInFolder(g.InstallDir))
                        if (!hints.Contains(name)) hints.Add(name);
                }
                catch { }
            }
            processHintCache[g.Id] = hints;
            return hints;
        }

        private void UpdateCardPlaytime(string id)
        {
            GameCard card = grid.FindCard(id);
            if (card == null) return;
            GameState st = StateOf(card.Game);
            card.PlaySeconds = GetPlaySeconds(card.Game, st);
            card.Invalidate();
        }

        private long GetPlaySeconds(Game game, GameState state)
        {
            if (game.Platform == Platform.Riot && state.AccountStats.Count > 0)
            {
                long accountTotal = 0;
                foreach (GameAccountStats account in state.AccountStats)
                {
                    if (account == null || string.IsNullOrWhiteSpace(account.AccountId)) continue;
                    if (IsRiotAccountIgnored(account.AccountId)) continue;
                    long seconds = Math.Max(0, account.PlaySeconds);
                    if (accountTotal > long.MaxValue - seconds) return long.MaxValue;
                    accountTotal += seconds;
                }
                return accountTotal;
            }
            if (game.Platform != Platform.Steam) return state.PlaySeconds;
            long total = 0;
            foreach (GameAccountStats account in GetAccountStats(game, state))
            {
                if (account.PlaySeconds > long.MaxValue - total) return long.MaxValue;
                total += account.PlaySeconds;
            }
            return Math.Max(state.PlaySeconds, total);
        }

        private IList<GameAccountStats> GetAccountStats(Game game, GameState state)
        {
            if (game.Platform == Platform.Steam)
            {
                List<GameAccountStats> combined = new List<GameAccountStats>();
                if (game.AccountStats != null)
                {
                    foreach (GameAccountStats native in game.AccountStats)
                    {
                        GameAccountStats account = CopyAccountStats(native);
                        GameAccountStats local = FindAccount(state.AccountStats, native.AccountId);
                        if (local != null)
                        {
                            account.BaselinePlaySeconds = local.BaselinePlaySeconds;
                            account.LocalPlaySeconds = local.LocalPlaySeconds;
                            if (!string.IsNullOrWhiteSpace(local.AccountName)) account.AccountName = local.AccountName;
                            long locallyTracked = AddSeconds(local.BaselinePlaySeconds, local.LocalPlaySeconds);
                            account.PlaySeconds = Math.Max(account.PlaySeconds, locallyTracked);
                        }
                        combined.Add(account);
                    }
                }
                foreach (GameAccountStats local in state.AccountStats)
                {
                    bool found = false;
                    foreach (GameAccountStats native in combined)
                        if (string.Equals(native.AccountId, local.AccountId, StringComparison.OrdinalIgnoreCase))
                        { found = true; break; }
                    if (found) continue;
                    GameAccountStats account = CopyAccountStats(local);
                    account.PlaySeconds = AddSeconds(local.BaselinePlaySeconds, local.LocalPlaySeconds);
                    combined.Add(account);
                }
                return combined;
            }
            if (game.Platform == Platform.Riot)
            {
                List<GameAccountStats> visible = new List<GameAccountStats>();
                foreach (GameAccountStats account in state.AccountStats)
                    if (account != null && !IsRiotAccountIgnored(account.AccountId))
                        visible.Add(account);
                return visible;
            }
            return state.AccountStats;
        }

        private GameAccountStats PrepareSteamAccount(Game game)
        {
            string accountId = Scanner.GetActiveSteamAccountId();
            if (string.IsNullOrWhiteSpace(accountId)) return null;

            GameAccountStats native = FindAccount(game.AccountStats, accountId);
            GameState state = StateOf(game);
            GameAccountStats local = FindAccount(state.AccountStats, accountId);
            if (local == null)
            {
                local = new GameAccountStats();
                local.AccountId = accountId;
                state.AccountStats.Add(local);
            }
            if (native != null && !string.IsNullOrWhiteSpace(native.AccountName))
                local.AccountName = native.AccountName;
            if (local.LocalPlaySeconds == 0 && local.BaselinePlaySeconds == 0 && native != null)
                local.BaselinePlaySeconds = native.PlaySeconds;
            return CopyAccountStats(local);
        }

        private static GameAccountStats FindAccount(IList<GameAccountStats> accounts, string accountId)
        {
            if (accounts == null || string.IsNullOrWhiteSpace(accountId)) return null;
            foreach (GameAccountStats account in accounts)
                if (account != null && string.Equals(account.AccountId, accountId, StringComparison.OrdinalIgnoreCase))
                    return account;
            return null;
        }

        private static long AddSeconds(long first, long second)
        {
            if (first < 0) first = 0;
            if (second < 0) second = 0;
            return second > long.MaxValue - first ? long.MaxValue : first + second;
        }

        private Game FindLeagueGame()
        {
            foreach (Game game in games)
                if (game.Platform == Platform.Riot &&
                    string.Equals(game.LaunchTarget, "league_of_legends", StringComparison.OrdinalIgnoreCase))
                    return game;
            return null;
        }

        private GameAccountStats ReadRiotAccount(Game game)
        {
            try
            {
                string directory = !string.IsNullOrEmpty(game.Executable)
                    ? Path.GetDirectoryName(game.Executable)
                    : game.InstallDir;
                GameAccountStats profile = RiotClientService.ReadCurrentAccount(directory);
                if (profile != null)
                {
                    if (string.IsNullOrEmpty(profile.Region))
                        profile.Region = RiotApiService.NormalizeRegion(settings.RiotApiRegion);
                    latestRiotAccount = CopyAccountStats(profile);
                }
                return profile;
            }
            catch (Exception ex)
            {
                Storage.Log("No se pudo leer la cuenta activa de Riot: " + ex.Message);
                return null;
            }
        }

        private bool IsRiotAccountIgnored(string accountId)
        {
            if (string.IsNullOrWhiteSpace(accountId)) return false;
            foreach (string ignored in settings.IgnoredRiotAccounts)
                if (string.Equals(ignored, accountId, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private List<GameAccountStats> GetKnownRiotAccounts()
        {
            Dictionary<string, GameAccountStats> known =
                new Dictionary<string, GameAccountStats>(StringComparer.OrdinalIgnoreCase);
            foreach (Game game in games)
            {
                if (game.Platform != Platform.Riot) continue;
                foreach (GameAccountStats account in StateOf(game).AccountStats)
                {
                    if (account == null || string.IsNullOrWhiteSpace(account.AccountId) ||
                        IsRiotAccountIgnored(account.AccountId)) continue;
                    GameAccountStats current;
                    if (!known.TryGetValue(account.AccountId, out current) ||
                        account.UpdatedAt > current.UpdatedAt)
                        known[account.AccountId] = CopyAccountStats(account);
                }
            }
            return new List<GameAccountStats>(known.Values);
        }

        private void RefreshKnownRiotAccountsOnline()
        {
            if (string.IsNullOrEmpty(settings.RiotApiKeyProtected)) return;
            foreach (GameAccountStats account in GetKnownRiotAccounts())
                QueueRiotOnlineLookup(account, true);
        }

        private void QueueRiotOnlineLookup(GameAccountStats profile, bool force)
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.AccountId) ||
                IsRiotAccountIgnored(profile.AccountId)) return;
            if (string.IsNullOrWhiteSpace(settings.RiotApiKeyProtected))
            {
                if (!riotApiKeyNoticeShown)
                {
                    riotApiKeyNoticeShown = true;
                    ShowTransientHint("Para nivel y rango online de Riot, configura una clave desde Ajustes.");
                }
                return;
            }

            GameAccountStats known = null;
            foreach (GameAccountStats account in GetKnownRiotAccounts())
                if (string.Equals(account.AccountId, profile.AccountId, StringComparison.OrdinalIgnoreCase))
                { known = account; break; }

            DateTime now = DateTime.UtcNow;
            if (!force && known != null && known.OnlineUpdatedAt > 0 &&
                Fmt.ToUnix(now) - known.OnlineUpdatedAt < 30 * 60)
                return;
            lock (riotLookupLock)
            {
                DateTime attempted;
                if (lastRiotLookupAttempt.TryGetValue(profile.AccountId, out attempted) &&
                    !force && now - attempted < TimeSpan.FromMinutes(5)) return;
                if (!riotLookupsInProgress.Add(profile.AccountId)) return;
                lastRiotLookupAttempt[profile.AccountId] = now;
            }

            string key = RiotApiService.UnprotectKey(settings.RiotApiKeyProtected);
            if (string.IsNullOrWhiteSpace(key))
            {
                lock (riotLookupLock) riotLookupsInProgress.Remove(profile.AccountId);
                return;
            }
            GameAccountStats requestProfile = CopyAccountStats(profile);
            string fallbackRegion = settings.RiotApiRegion;
            ThreadPool.QueueUserWorkItem(delegate
            {
                GameAccountStats updated = RiotApiService.LookupOnline(requestProfile, key, fallbackRegion);
                try
                {
                    if (IsDisposed || !IsHandleCreated)
                    {
                        lock (riotLookupLock) riotLookupsInProgress.Remove(requestProfile.AccountId);
                        return;
                    }
                    BeginInvoke(new Action(delegate
                    {
                        lock (riotLookupLock) riotLookupsInProgress.Remove(requestProfile.AccountId);
                        if (updated == null)
                        {
                            ShowTransientHint("No se pudieron actualizar los datos de Riot. Revisa clave, región o conexión.");
                            return;
                        }
                        if (IsRiotAccountIgnored(updated.AccountId)) return;

                        bool changed = false;
                        foreach (Game game in games)
                        {
                            if (game.Platform != Platform.Riot) continue;
                            GameState state = StateOf(game);
                            GameAccountStats current = FindAccount(state.AccountStats, updated.AccountId);
                            if (current == null) continue;
                            if (updated.Level > 0 && current.Level != updated.Level)
                            { current.Level = updated.Level; changed = true; }
                            if (!string.IsNullOrWhiteSpace(updated.Rank) && current.Rank != updated.Rank)
                            { current.Rank = updated.Rank; changed = true; }
                            if (!string.IsNullOrWhiteSpace(updated.Region) && current.Region != updated.Region)
                            { current.Region = updated.Region; changed = true; }
                            if (updated.OnlineUpdatedAt > current.OnlineUpdatedAt)
                            { current.OnlineUpdatedAt = updated.OnlineUpdatedAt; changed = true; }
                        }
                        if (!changed) return;
                        SaveSettings();
                        RefreshGrid();
                        if (detailsPanel != null)
                        {
                            foreach (Game game in games)
                                if (game.Platform == Platform.Riot &&
                                    detailsPanel.MatchesGame(game.Id))
                                {
                                    GameState state = StateOf(game);
                                    detailsPanel.UpdateAccountData(GetPlaySeconds(game, state),
                                        GetAccountStats(game, state));
                                    break;
                                }
                        }
                    }));
                }
                catch (InvalidOperationException)
                {
                    lock (riotLookupLock) riotLookupsInProgress.Remove(requestProfile.AccountId);
                }
            });
        }

        private void ShowTransientHint(string text)
        {
            hintText.Text = text;
            statusTimer.Stop();
            statusTimer.Start();
        }

        private bool UpdateRiotAccountStats(Game game, GameAccountStats profile, long seconds)
        {
            if (game == null || profile == null) return false;
            string key = string.IsNullOrWhiteSpace(profile.AccountId)
                ? profile.AccountName : profile.AccountId;
            if (IsRiotAccountIgnored(key)) return false;
            GameState state = StateOf(game);
            GameAccountStats current = null;
            foreach (GameAccountStats account in state.AccountStats)
            {
                if (string.Equals(account.AccountId, key, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(profile.AccountName) &&
                     string.Equals(account.AccountName, profile.AccountName, StringComparison.OrdinalIgnoreCase)))
                {
                    current = account;
                    break;
                }
            }

            bool changed = false;
            if (current == null)
            {
                current = new GameAccountStats();
                current.AccountId = key;
                state.AccountStats.Add(current);
                changed = true;
            }
            if (!string.IsNullOrWhiteSpace(profile.AccountName) && current.AccountName != profile.AccountName)
            {
                current.AccountName = profile.AccountName;
                changed = true;
            }
            if (!string.IsNullOrWhiteSpace(profile.Rank) && current.Rank != profile.Rank)
            {
                current.Rank = profile.Rank;
                changed = true;
            }
            if (profile.Level > 0 && current.Level != profile.Level)
            {
                current.Level = profile.Level;
                changed = true;
            }
            if (!string.IsNullOrWhiteSpace(profile.Region) && current.Region != profile.Region)
            {
                current.Region = profile.Region;
                changed = true;
            }
            if (seconds > 0)
            {
                current.PlaySeconds = AddSeconds(current.PlaySeconds, seconds);
                state.PlaySeconds = AddSeconds(state.PlaySeconds, seconds);
                changed = true;
            }
            if (profile.UpdatedAt > current.UpdatedAt)
                current.UpdatedAt = profile.UpdatedAt;
            if (profile.OnlineUpdatedAt > current.OnlineUpdatedAt)
                current.OnlineUpdatedAt = profile.OnlineUpdatedAt;
            return changed;
        }

        private void RecordGameSession(Game game, long seconds, GameAccountStats account)
        {
            if (game.Platform == Platform.Riot && account != null)
                UpdateRiotAccountStats(game, account, seconds);
            else if (game.Platform == Platform.Steam && account != null)
            {
                GameState state = StateOf(game);
                GameAccountStats local = FindAccount(state.AccountStats, account.AccountId);
                if (local == null)
                {
                    local = CopyAccountStats(account);
                    state.AccountStats.Add(local);
                }
                local.LocalPlaySeconds = AddSeconds(local.LocalPlaySeconds, seconds);
                state.PlaySeconds = AddSeconds(state.PlaySeconds, seconds);
            }
            else
            {
                GameState state = StateOf(game);
                state.PlaySeconds = AddSeconds(state.PlaySeconds, seconds);
            }
        }

        private static GameAccountStats CopyAccountStats(GameAccountStats source)
        {
            if (source == null) return null;
            GameAccountStats copy = new GameAccountStats();
            copy.AccountId = source.AccountId;
            copy.AccountName = source.AccountName;
            copy.PlaySeconds = source.PlaySeconds;
            copy.BaselinePlaySeconds = source.BaselinePlaySeconds;
            copy.LocalPlaySeconds = source.LocalPlaySeconds;
            copy.Rank = source.Rank;
            copy.Level = source.Level;
            copy.Region = source.Region;
            copy.UpdatedAt = source.UpdatedAt;
            copy.OnlineUpdatedAt = source.OnlineUpdatedAt;
            return copy;
        }

        private void FinalizeSessions()
        {
            if (runningSince.Count == 0) return;
            DateTime now = DateTime.Now;
            foreach (KeyValuePair<string, DateTime> pair in runningSince)
            {
                Game g = null;
                foreach (Game candidate in games)
                    if (string.Equals(candidate.Id, pair.Key, StringComparison.OrdinalIgnoreCase)) { g = candidate; break; }
                if (g == null) continue;
                long seconds = (long)(now - pair.Value).TotalSeconds;
                if (seconds >= 15)
                {
                    GameAccountStats account = null;
                    if (g.Platform == Platform.Riot)
                    {
                        GameAccountStats current = ReadRiotAccount(g);
                        runningAccounts.TryGetValue(g.Id, out account);
                        account = MergeRiotSessionAccount(account, current);
                    }
                    else if (g.Platform == Platform.Steam)
                    {
                        runningSteamAccounts.TryGetValue(g.Id, out account);
                    }
                    RecordGameSession(g, seconds, account);
                    if (g.Platform == Platform.Steam) Scanner.RefreshSteamPlaytimes(games);
                }
            }
            runningSince.Clear();
            runningAccounts.Clear();
            runningSteamAccounts.Clear();
            SaveSettings();
        }

        private static GameAccountStats MergeRiotSessionAccount(GameAccountStats session, GameAccountStats current)
        {
            if (session == null) return CopyAccountStats(current);
            if (current == null ||
                !string.Equals(session.AccountId, current.AccountId, StringComparison.OrdinalIgnoreCase))
                return session;
            if (!string.IsNullOrWhiteSpace(current.AccountName)) session.AccountName = current.AccountName;
            if (!string.IsNullOrWhiteSpace(current.Rank)) session.Rank = current.Rank;
            if (current.Level > 0) session.Level = current.Level;
            if (!string.IsNullOrWhiteSpace(current.Region)) session.Region = current.Region;
            if (current.UpdatedAt > session.UpdatedAt) session.UpdatedAt = current.UpdatedAt;
            if (current.OnlineUpdatedAt > session.OnlineUpdatedAt) session.OnlineUpdatedAt = current.OnlineUpdatedAt;
            return session;
        }

        // ----------------------------------------------------------------- AJUSTES

        private void ShowSettingsMenu()
        {
            ContextMenuStrip menu = Menus.Create();

            ToolStripMenuItem minItem = Menus.Item(menu, "Minimizar al iniciar un juego", delegate
            {
                settings.MinimizeOnLaunch = !settings.MinimizeOnLaunch;
                SaveSettings();
            });
            minItem.Checked = settings.MinimizeOnLaunch;

            ToolStripMenuItem autoItem = Menus.Item(menu, "Añadir juegos nuevos al abrir", delegate
            {
                settings.AutoDiscover = !settings.AutoDiscover;
                SaveSettings();
                if (settings.AutoDiscover) StartAutoDiscovery();
            });
            autoItem.Checked = settings.AutoDiscover;

            ToolStripMenuItem hiddenItem = Menus.Item(menu, "Mostrar juegos ocultos", delegate
            {
                settings.ShowHidden = !settings.ShowHidden;
                SaveSettings();
                RefreshGrid();
            });
            hiddenItem.Checked = settings.ShowHidden;

            Menus.Separator(menu);
            ToolStripMenuItem themeMenu = new ToolStripMenuItem("Tema");
            foreach (string[] theme in new string[][] {
                new string[] { "Oscuro", "0" },
                new string[] { "Blanco", "1" },
                new string[] { "Rojo y negro", "2" }
            })
            {
                int value = int.Parse(theme[1]);
                ToolStripMenuItem item = new ToolStripMenuItem(theme[0]);
                item.Checked = settings.Theme == value;
                item.Click += delegate
                {
                    settings.Theme = value;
                    Theme.Apply(value);
                    ApplyThemeToChildren(this);
                    BackColor = Theme.Bg;
                    topBar.BackColor = Theme.BgSoft;
                    filtersBar.BackColor = Theme.Bg;
                    bottomBar.BackColor = Theme.BgSoft;
                    if (detailsPanel != null) detailsPanel.ApplyTheme();
                    SaveSettings();
                    RefreshGrid();
                    Invalidate(true);
                };
                themeMenu.DropDownItems.Add(item);
            }
            menu.Items.Add(themeMenu);
            Menus.Item(menu, "Emuladores…", delegate { ShowEmulatorsDialog(); });
            Menus.Item(menu, "Cuentas de Riot…", delegate { ShowRiotAccounts(); });
            Menus.Item(menu, "Configurar datos online de Riot…", delegate { ShowRiotApiSettings(); });
            Menus.Item(menu, "Buscar juegos instalados en el PC…", delegate { ShowDiscoveryDialog(); });
            Menus.Item(menu, "Estadísticas de juego…", delegate { ShowStats(); });
            Menus.Item(menu, "Volver a detectar todo", delegate
            {
                foreach (Game g in games) QueueCover(g);
                StartScan();
            });
            Menus.Item(menu, "Abrir carpeta de datos", delegate { GameLauncher.OpenFolder(Storage.DataDir); });
            Menus.Separator(menu);
            Menus.Item(menu, "Acerca de Games", delegate
            {
                MessageBox.Show(this,
                    "Games 1.0\n\nTu biblioteca unificada de Steam, Epic Games, Riot, juegos locales y emuladores.\n\n" +
                    "Clic en una tarjeta para ver sus detalles y jugar.\nClic derecho para más opciones.\n\n" +
                    "Datos guardados en:\n" + Storage.DataDir,
                    "Acerca de Games", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });

            menu.Show(settingsButton, new Point(0, settingsButton.Height + Theme.Px(4)));
        }

        private void ShowRiotApiSettings()
        {
            using (RiotApiSettingsDialog dialog =
                new RiotApiSettingsDialog(settings.RiotApiKeyProtected, settings.RiotApiRegion))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                settings.RiotApiKeyProtected = dialog.ProtectedApiKey;
                settings.RiotApiRegion = dialog.SelectedRegion;
                riotApiKeyNoticeShown = false;
                SaveSettings();
            }
            RefreshKnownRiotAccountsOnline();
        }

        private void ShowRiotAccounts()
        {
            List<GameAccountStats> ignored = GetIgnoredRiotAccounts();
            using (RiotAccountsDialog dialog = new RiotAccountsDialog(
                GetKnownRiotAccounts(), ignored, RemoveRiotAccount, RestoreRiotAccount))
                dialog.ShowDialog(this);
        }

        private List<GameAccountStats> GetIgnoredRiotAccounts()
        {
            Dictionary<string, GameAccountStats> ignored =
                new Dictionary<string, GameAccountStats>(StringComparer.OrdinalIgnoreCase);
            foreach (Game game in games)
            {
                if (game.Platform != Platform.Riot) continue;
                foreach (GameAccountStats account in StateOf(game).AccountStats)
                    if (account != null && IsRiotAccountIgnored(account.AccountId) &&
                        !ignored.ContainsKey(account.AccountId))
                        ignored[account.AccountId] = CopyAccountStats(account);
            }
            foreach (string accountId in settings.IgnoredRiotAccounts)
            {
                if (ignored.ContainsKey(accountId)) continue;
                GameAccountStats account = new GameAccountStats();
                account.AccountId = accountId;
                string name;
                if (settings.IgnoredRiotAccountNames.TryGetValue(accountId, out name))
                    account.AccountName = name;
                ignored[accountId] = account;
            }
            return new List<GameAccountStats>(ignored.Values);
        }

        private void RemoveRiotAccount(string accountId)
        {
            if (string.IsNullOrWhiteSpace(accountId)) return;
            GameAccountStats profile = null;
            foreach (GameAccountStats account in GetKnownRiotAccounts())
                if (string.Equals(account.AccountId, accountId, StringComparison.OrdinalIgnoreCase))
                { profile = account; break; }

            if (!IsRiotAccountIgnored(accountId))
                settings.IgnoredRiotAccounts.Add(accountId);
            if (profile != null && !string.IsNullOrWhiteSpace(profile.AccountName))
                settings.IgnoredRiotAccountNames[accountId] = profile.AccountName;
            SaveSettings();
            RefreshGrid();
        }

        private void RestoreRiotAccount(string accountId)
        {
            if (string.IsNullOrWhiteSpace(accountId)) return;
            settings.IgnoredRiotAccounts.RemoveAll(delegate(string ignored)
            {
                return string.Equals(ignored, accountId, StringComparison.OrdinalIgnoreCase);
            });
            settings.IgnoredRiotAccountNames.Remove(accountId);
            SaveSettings();
            RefreshGrid();
            foreach (GameAccountStats account in GetKnownRiotAccounts())
                if (string.Equals(account.AccountId, accountId, StringComparison.OrdinalIgnoreCase))
                {
                    QueueRiotOnlineLookup(account, true);
                    break;
                }
        }

        private void SaveSettings()
        {
            try
            {
                if (WindowState == FormWindowState.Normal)
                {
                    settings.WindowWidth = ClientSize.Width;
                    settings.WindowHeight = ClientSize.Height;
                    settings.WindowX = Left;
                    settings.WindowY = Top;
                }
                Storage.SaveSettings(settings);
            }
            catch { }
        }

        private void ApplyThemeToChildren(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                if (control is Panel || control is Label)
                {
                    control.BackColor = control is Label ? Color.Transparent : Theme.Bg;
                    if (control is Label) control.ForeColor = Theme.TextDim;
                }
                control.Invalidate(true);
                ApplyThemeToChildren(control);
            }
        }

        private void ShowEmulatorsDialog()
        {
            EnsureEmulatorIds();
            List<EmulatorConfig> edited = new List<EmulatorConfig>();
            foreach (EmulatorConfig emulator in settings.Emulators)
                edited.Add(CloneEmulator(emulator));
            using (EmulatorsDialog dlg = new EmulatorsDialog(edited))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                settings.Emulators = dlg.Emulators;
                EnsureEmulatorIds();
                int added = ScanEmulatorGames();
                SaveSettings();
                SetFilter("Emulated");
                hintText.Text = added == 0
                    ? "Configuración de emuladores guardada."
                    : (added == 1 ? "1 juego emulado añadido." : added + " juegos emulados añadidos.");
            }
        }

        private int ScanEmulatorGames()
        {
            int added = 0;
            foreach (EmulatorConfig emulator in settings.Emulators)
            {
                if (string.IsNullOrEmpty(emulator.ExePath) || !File.Exists(emulator.ExePath)) continue;
                if (string.IsNullOrEmpty(emulator.RomFolder) || !Directory.Exists(emulator.RomFolder)) continue;

                string[] extensions = (emulator.Extensions ?? "").Split(new char[] { ',' },
                    StringSplitOptions.RemoveEmptyEntries);
                if (extensions.Length == 0) extensions = new string[] { ".iso", ".bin", ".cue", ".rom", ".chd" };

                string[] files;
                try
                {
                    files = Directory.GetFiles(emulator.RomFolder, "*", SearchOption.AllDirectories);
                }
                catch
                {
                    continue;
                }

                foreach (string rom in files)
                {
                    bool match = false;
                    string ext = Path.GetExtension(rom);
                    foreach (string allowed in extensions)
                        if (string.Equals(ext, allowed.Trim(), StringComparison.OrdinalIgnoreCase)) { match = true; break; }
                    if (!match) continue;

                    string title = Path.GetFileNameWithoutExtension(rom);
                    if (title.Length == 0) continue;
                    if (AddLocalGame(title, emulator.ExePath,
                        string.IsNullOrEmpty(emulator.Arguments) ? "{rom}" : emulator.Arguments,
                        Path.GetDirectoryName(emulator.ExePath), GameKind.Emulated, rom,
                        string.IsNullOrEmpty(emulator.Name) ? "Emulado" : emulator.Name, emulator.Id))
                        added++;
                }
            }
            return added;
        }

        private void EnsureEmulatorIds()
        {
            foreach (EmulatorConfig emulator in settings.Emulators)
                if (emulator != null && string.IsNullOrEmpty(emulator.Id))
                    emulator.Id = Guid.NewGuid().ToString("N");
        }

        private static EmulatorConfig CloneEmulator(EmulatorConfig source)
        {
            EmulatorConfig clone = new EmulatorConfig();
            clone.Id = source.Id;
            clone.Name = source.Name;
            clone.ExePath = source.ExePath;
            clone.RomFolder = source.RomFolder;
            clone.Arguments = source.Arguments;
            clone.Extensions = source.Extensions;
            return clone;
        }

        private void RestoreWindowPosition()
        {
            if (settings.WindowWidth <= 0) return;
            Rectangle bounds = Screen.PrimaryScreen.WorkingArea;
            bool visible = false;
            foreach (Screen screen in Screen.AllScreens)
            {
                Rectangle target = new Rectangle(settings.WindowX, settings.WindowY,
                    settings.WindowWidth, settings.WindowHeight);
                if (screen.WorkingArea.IntersectsWith(target)) { visible = true; bounds = screen.WorkingArea; break; }
            }
            if (!visible) return;

            int w = Math.Min(settings.WindowWidth, bounds.Width);
            int h = Math.Min(settings.WindowHeight, bounds.Height);
            ClientSize = new Size(w, h);
            int x = Math.Max(bounds.X, Math.Min(settings.WindowX, bounds.Right - Width));
            int y = Math.Max(bounds.Y, Math.Min(settings.WindowY, bounds.Bottom - Height));
            Location = new Point(x, y);
        }

        // ------------------------------------------------------- PORTADAS EN HILO

        private void QueueCover(Game g)
        {
            lock (coverLock) coverQueue.Enqueue(g);
            StartCoverWorker();
        }

        private void StartCoverWorker()
        {
            lock (coverLock)
            {
                if (coverWorkerRunning) return;
                coverWorkerRunning = true;
            }
            Thread worker = new Thread(CoverLoop);
            worker.IsBackground = true;
            worker.Start();
        }

        private void CoverLoop()
        {
            while (true)
            {
                Game g = null;
                lock (coverLock)
                {
                    if (coverQueue.Count > 0) g = coverQueue.Dequeue();
                }
                if (g == null)
                {
                    lock (coverLock)
                    {
                        if (coverQueue.Count == 0) { coverWorkerRunning = false; return; }
                    }
                    continue;
                }

                bool changed = false;
                try
                {
                    GameState snapshot = new GameState();
                    GameState real = StateOf(g);
                    snapshot.CustomCover = real.CustomCover;
                    changed = Covers.Ensure(g, snapshot);
                }
                catch (Exception ex) { Storage.Log("Portada " + g.Title + ": " + ex.Message); }

                if (!changed) continue;
                string id = g.Id;
                try
                {
                    if (IsHandleCreated)
                        BeginInvoke(new Action(delegate { ApplyCover(id); }));
                }
                catch { }
            }
        }

        private void ApplyCover(string id)
        {
            GameCard card = grid.FindCard(id);
            if (card == null) return;
            Image image = Covers.Load(card.Game, StateOf(card.Game));
            if (image == null) return;
            if (card.Cover != null) { try { card.Cover.Dispose(); } catch { } }
            card.Cover = image;
            card.Invalidate();
        }

        // ------------------------------------------------------------- VENTANA

        private void ToggleMaximize()
        {
            if (WindowState == FormWindowState.Maximized) WindowState = FormWindowState.Normal;
            else
            {
                MaximizedBounds = Screen.FromControl(this).WorkingArea;
                WindowState = FormWindowState.Maximized;
            }
            maxButton.Icon = WindowState == FormWindowState.Maximized ? "\uE923" : "\uE922";
        }

        private void Drag_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            dragging = true;
            dragStart = new Point(e.X + (sender as Control).Left, e.Y + (sender as Control).Top);
        }

        private void Drag_MouseMove(object sender, MouseEventArgs e)
        {
            if (!dragging || WindowState == FormWindowState.Maximized) return;
            Point screen = PointToScreen(new Point(e.X + (sender as Control).Left, e.Y + (sender as Control).Top));
            Location = new Point(screen.X - dragStart.X, screen.Y - dragStart.Y);
        }

        private void Drag_MouseUp(object sender, MouseEventArgs e)
        {
            dragging = false;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Maximized && MaximizedBounds.IsEmpty)
                MaximizedBounds = Screen.FromControl(this).WorkingArea;
            LayoutUi();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            FinalizeSessions();
            SaveSettings();
            base.OnFormClosing(e);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (detailsPanel != null)
            {
                if (keyData == Keys.Escape) { CloseGameDetails(); return true; }
                if (keyData == (Keys.Control | Keys.F))
                {
                    CloseGameDetails();
                    search.FocusInput();
                    return true;
                }
                if (keyData == (Keys.Control | Keys.N))
                {
                    CloseGameDetails();
                    AddGameDialogFlow();
                    return true;
                }
                return base.ProcessCmdKey(ref msg, keyData);
            }
            if (keyData == (Keys.Control | Keys.F)) { search.FocusInput(); return true; }
            if (keyData == (Keys.Control | Keys.N)) { AddGameDialogFlow(); return true; }

            bool typing = search.ContainsFocus;
            if (keyData == Keys.Escape && typing) { search.Clear(); return true; }

            if (!typing)
            {
                if (keyData == Keys.Left) { MoveSelection(-1); return true; }
                if (keyData == Keys.Right) { MoveSelection(1); return true; }
                if (keyData == Keys.Up) { MoveSelection(-Math.Max(1, grid.Columns)); return true; }
                if (keyData == Keys.Down) { MoveSelection(Math.Max(1, grid.Columns)); return true; }
                if (keyData == Keys.Enter) { ActivateSelected(); return true; }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private int SelectedIndex()
        {
            if (string.IsNullOrEmpty(selectedId)) return -1;
            for (int i = 0; i < grid.Cards.Count; i++)
                if (string.Equals(grid.Cards[i].Game.Id, selectedId, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        private void MoveSelection(int delta)
        {
            if (grid.Cards.Count == 0) return;
            int index = SelectedIndex();
            if (index < 0) index = 0;
            else index = Math.Max(0, Math.Min(grid.Cards.Count - 1, index + delta));

            GameCard card = grid.Cards[index];
            selectedId = card.Game.Id;
            grid.SetAmbient(card.Cover);
            card.Focus();
            grid.EnsureVisible(index);
        }

        private void ActivateSelected()
        {
            int index = SelectedIndex();
            if (index >= 0 && index < grid.Cards.Count) ShowGameDetails(grid.Cards[index].Game);
            else if (grid.Cards.Count > 0) ShowGameDetails(grid.Cards[0].Game);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST && WindowState == FormWindowState.Normal)
            {
                base.WndProc(ref m);
                if ((int)m.Result == 1)
                {
                    int screenX = (short)((long)m.LParam & 0xFFFF);
                    int screenY = (short)(((long)m.LParam >> 16) & 0xFFFF);
                    Point p = PointToClient(new Point(screenX, screenY));
                    int b = Theme.Px(7);
                    bool left = p.X <= b;
                    bool right = p.X >= ClientSize.Width - b;
                    bool top = p.Y <= b;
                    bool bottom = p.Y >= ClientSize.Height - b;

                    if (top && left) m.Result = (IntPtr)13;
                    else if (top && right) m.Result = (IntPtr)14;
                    else if (bottom && left) m.Result = (IntPtr)16;
                    else if (bottom && right) m.Result = (IntPtr)17;
                    else if (left) m.Result = (IntPtr)10;
                    else if (right) m.Result = (IntPtr)11;
                    else if (top) m.Result = (IntPtr)12;
                    else if (bottom) m.Result = (IntPtr)15;
                }
                return;
            }
            base.WndProc(ref m);
        }
    }
}
