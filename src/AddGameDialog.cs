using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace GamesOfAll
{
    // Caja de texto con marco redondeado, al estilo del resto de la interfaz.
    internal sealed class TextField : Control
    {
        public readonly TextBox Inner;

        public TextField(string hint)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;

            Inner = new TextBox();
            Inner.BorderStyle = BorderStyle.None;
            Inner.BackColor = Theme.Surface;
            Inner.ForeColor = Theme.Text;
            Inner.Font = Theme.Body;
            Inner.GotFocus += delegate { Invalidate(); };
            Inner.LostFocus += delegate { Invalidate(); };
            Controls.Add(Inner);
            Tag = hint;
        }

        public string Value
        {
            get { return Inner.Text; }
            set { Inner.Text = value; }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Inner.SetBounds(Theme.Px(12), (Height - Theme.Px(20)) / 2, Math.Max(20, Width - Theme.Px(24)), Theme.Px(20));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            Theme.FillRound(g, r, Theme.Pxf(8f), Theme.Surface);
            Theme.StrokeRound(g, r, Theme.Pxf(8f),
                Inner.Focused ? Color.FromArgb(120, Theme.Accent) : Theme.Stroke, 1f);
        }
    }

    internal sealed class AddGameDialog : Form
    {
        private readonly TextField nameField;
        private readonly TextField platformField;
        private readonly TextField exeField;
        private readonly TextField romField;
        private readonly TextField argsField;
        private readonly PillButton browseExe;
        private readonly PillButton browseRom;
        private readonly PillButton nativeChip;
        private readonly PillButton emulatedChip;
        private readonly PillButton addButton;
        private readonly PillButton cancelButton;

        private GameKind kind = GameKind.Native;
        private bool dragging;
        private Point dragStart;

        private int margin;
        private int contentWidth;

        public string GameName { get { return nameField.Value.Trim(); } }
        public string PlatformLabel { get { return platformField.Value.Trim(); } }
        public string GameExe { get { return exeField.Value.Trim().Trim('"'); } }
        public string GameRom { get { return romField.Value.Trim().Trim('"'); } }
        public string GameArgs { get { return argsField.Value.Trim(); } }
        public GameKind Kind { get { return kind; } }

        public AddGameDialog(string initialExe)
        {
            Text = "Añadir juego";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Bg;
            ShowInTaskbar = false;
            KeyPreview = true;
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            Font = Theme.Body;

            margin = Theme.Px(28);
            ClientSize = new Size(Theme.Px(560), Theme.Px(424));
            contentWidth = ClientSize.Width - margin * 2;

            nativeChip = new PillButton("Juego instalado");
            nativeChip.Selected = true;
            nativeChip.Click += delegate { SetKind(GameKind.Native); };

            emulatedChip = new PillButton("Juego emulado (ROM)");
            emulatedChip.Click += delegate { SetKind(GameKind.Emulated); };

            nameField = new TextField("Nombre del juego");
            platformField = new TextField("PS2, N64, Minecraft…");

            exeField = new TextField("Ruta del .exe");
            if (!string.IsNullOrEmpty(initialExe)) exeField.Value = initialExe;

            romField = new TextField("Ruta de la ROM / ISO");

            argsField = new TextField("Opcional: -windowed -novid");

            browseExe = new PillButton("Examinar");
            browseExe.Click += delegate { BrowseExe(); };

            browseRom = new PillButton("Examinar");
            browseRom.Click += delegate { BrowseRom(); };

            addButton = new PillButton("Añadir juego");
            addButton.Style = PillStyle.Accent;
            addButton.Click += delegate { Accept(); };

            cancelButton = new PillButton("Cancelar");
            cancelButton.Style = PillStyle.Ghost;
            cancelButton.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };

            PillButton close = new PillButton("");
            close.Style = PillStyle.Close;
            close.Icon = "\uE8BB";
            close.IconOnly = true;
            close.SetBounds(ClientSize.Width - Theme.Px(52), Theme.Px(12), Theme.Px(34), Theme.Px(30));
            close.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };

            Controls.Add(nativeChip);
            Controls.Add(emulatedChip);
            Controls.Add(nameField);
            Controls.Add(platformField);
            Controls.Add(exeField);
            Controls.Add(browseExe);
            Controls.Add(romField);
            Controls.Add(browseRom);
            Controls.Add(argsField);
            Controls.Add(addButton);
            Controls.Add(cancelButton);
            Controls.Add(close);

            MouseDown += DragArea_MouseDown;
            MouseMove += DragArea_MouseMove;
            MouseUp += delegate { dragging = false; };

            Shown += delegate { nameField.Inner.Focus(); };
            ApplyLayout();
        }

        private void SetKind(GameKind value)
        {
            kind = value;
            nativeChip.Selected = value == GameKind.Native;
            emulatedChip.Selected = value == GameKind.Emulated;
            nativeChip.Invalidate();
            emulatedChip.Invalidate();
            ApplyLayout();
        }

        private void ApplyLayout()
        {
            margin = Theme.Px(28);
            int width = ClientSize.Width - margin * 2;

            nativeChip.SetBounds(margin, Theme.Px(78), Theme.Px(140), Theme.Px(30));
            emulatedChip.SetBounds(nativeChip.Right + Theme.Px(8), Theme.Px(78), Theme.Px(168), Theme.Px(30));

            int platformW = Theme.Px(186);
            nameField.SetBounds(margin, Theme.Px(138), width - platformW - Theme.Px(12), Theme.Px(40));
            platformField.SetBounds(margin + width - platformW, Theme.Px(138), platformW, Theme.Px(40));

            int browseW = Theme.Px(102);
            exeField.SetBounds(margin, Theme.Px(214), width - browseW - Theme.Px(10), Theme.Px(40));
            browseExe.SetBounds(exeField.Right + Theme.Px(10), Theme.Px(214), browseW, Theme.Px(40));

            bool emulated = kind == GameKind.Emulated;
            if (emulated)
            {
                romField.Visible = true;
                browseRom.Visible = true;
                romField.SetBounds(margin, Theme.Px(290), width - browseW - Theme.Px(10), Theme.Px(40));
                browseRom.SetBounds(romField.Right + Theme.Px(10), Theme.Px(290), browseW, Theme.Px(40));
                argsField.SetBounds(margin, Theme.Px(366), width, Theme.Px(40));
                ClientSize = new Size(ClientSize.Width, Theme.Px(508));
            }
            else
            {
                romField.Visible = false;
                browseRom.Visible = false;
                argsField.SetBounds(margin, Theme.Px(290), width, Theme.Px(40));
                ClientSize = new Size(ClientSize.Width, Theme.Px(424));
            }

            int btnY = ClientSize.Height - Theme.Px(62);
            cancelButton.SetBounds(ClientSize.Width - margin - Theme.Px(232), btnY, Theme.Px(112), Theme.Px(38));
            addButton.SetBounds(ClientSize.Width - margin - Theme.Px(112), btnY, Theme.Px(112), Theme.Px(38));
            Invalidate();
        }

        private void BrowseExe()
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = kind == GameKind.Emulated
                    ? "Selecciona el emulador"
                    : "Selecciona el ejecutable del juego";
                dlg.Filter = "Ejecutables (*.exe)|*.exe|Todos los archivos (*.*)|*.*";
                dlg.CheckFileExists = true;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    exeField.Value = dlg.FileName;
                    if (nameField.Value.Trim().Length == 0)
                        nameField.Value = Path.GetFileNameWithoutExtension(dlg.FileName);
                    if (kind == GameKind.Emulated) romField.Inner.Focus();
                    else argsField.Inner.Focus();
                }
            }
        }

        private void BrowseRom()
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "Selecciona la ROM o ISO del juego";
                dlg.Filter = "Juegos (*.iso;*.rom;*.bin;*.cue;*.gba;*.gb;*.gbc;*.nds;*.n64;*.z64;*.sfc;*.smc;*.nes;*.zip;*.7z;*.chd;*.rvz;*.wad)|" +
                             "*.iso;*.rom;*.bin;*.cue;*.gba;*.gb;*.gbc;*.nds;*.n64;*.z64;*.sfc;*.smc;*.nes;*.zip;*.7z;*.chd;*.rvz;*.wad|" +
                             "Todos los archivos (*.*)|*.*";
                dlg.CheckFileExists = true;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    romField.Value = dlg.FileName;
                    if (nameField.Value.Trim().Length == 0)
                        nameField.Value = Path.GetFileNameWithoutExtension(dlg.FileName);
                }
            }
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

        private void Accept()
        {
            string exe = GameExe;
            if (exe.Length == 0 || !File.Exists(exe))
            {
                MessageBox.Show(this,
                    kind == GameKind.Emulated
                        ? "Selecciona el ejecutable del emulador."
                        : "Selecciona un ejecutable que exista en el disco.",
                    "Falta el ejecutable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (kind == GameKind.Emulated)
            {
                string rom = GameRom;
                if (rom.Length == 0 || !File.Exists(rom))
                {
                    MessageBox.Show(this, "Selecciona la ROM o ISO que quieres cargar.",
                        "Falta la ROM", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (GameName.Length == 0)
                    nameField.Value = Path.GetFileNameWithoutExtension(rom);
            }
            else if (GameName.Length == 0)
            {
                nameField.Value = Path.GetFileNameWithoutExtension(exe);
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Enter) { Accept(); return true; }
            if (keyData == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Theme.DrawText(g, "Añadir juego", Theme.Title, Theme.Text,
                new Rectangle(Theme.Px(28), Theme.Px(22), ClientSize.Width - Theme.Px(90), Theme.Px(30)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            Theme.DrawText(g, "Un juego local, un emulador con su ROM o cualquier .exe",
                Theme.Small, Theme.TextDim,
                new Rectangle(Theme.Px(28), Theme.Px(50), ClientSize.Width - Theme.Px(90), Theme.Px(20)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

            DrawLabel(g, "Nombre", Theme.Px(120));
            DrawLabel(g, "Plataforma o etiqueta", Theme.Px(120), Theme.Px(28) + contentWidth - Theme.Px(186));

            DrawLabel(g, kind == GameKind.Emulated ? "Emulador (.exe)" : "Ejecutable (.exe)", Theme.Px(196));
            if (kind == GameKind.Emulated) DrawLabel(g, "ROM / ISO", Theme.Px(272));
            DrawLabel(g, "Argumentos", Theme.Px(kind == GameKind.Emulated ? 348 : 272));

            using (Pen p = new Pen(Theme.Stroke))
                g.DrawRectangle(p, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }

        private void DrawLabel(Graphics g, string text, int y)
        {
            DrawLabel(g, text, y, Theme.Px(29));
        }

        private void DrawLabel(Graphics g, string text, int y, int x)
        {
            Theme.DrawText(g, text, Theme.SmallBold, Theme.TextDim,
                new Rectangle(x, y, ClientSize.Width - x - Theme.Px(28), Theme.Px(18)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }
}
