using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace GamesOfAll
{
    internal sealed class EmulatorsDialog : Form
    {
        private readonly List<EmulatorConfig> emulators;
        private readonly ListBox list;
        private readonly TextBox name;
        private readonly TextBox exe;
        private readonly TextBox romFolder;
        private readonly TextBox arguments;
        private readonly TextBox extensions;

        public List<EmulatorConfig> Emulators { get { return emulators; } }

        public EmulatorsDialog(List<EmulatorConfig> emulators)
        {
            this.emulators = emulators ?? new List<EmulatorConfig>();
            Text = "Emuladores";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(Theme.Px(760), Theme.Px(520));
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.Body;
            DoubleBuffered = true;

            list = new ListBox();
            list.BorderStyle = BorderStyle.None;
            list.BackColor = Theme.Surface;
            list.ForeColor = Theme.Text;
            list.Font = Theme.Body;
            list.SetBounds(Theme.Px(20), Theme.Px(70), Theme.Px(220), Theme.Px(360));
            list.SelectedIndexChanged += delegate { LoadSelected(); };

            name = MakeTextBox(Theme.Px(270), Theme.Px(90), Theme.Px(470));
            exe = MakeTextBox(Theme.Px(270), Theme.Px(155), Theme.Px(470));
            romFolder = MakeTextBox(Theme.Px(270), Theme.Px(220), Theme.Px(470));
            arguments = MakeTextBox(Theme.Px(270), Theme.Px(285), Theme.Px(470));
            extensions = MakeTextBox(Theme.Px(270), Theme.Px(350), Theme.Px(470));

            Controls.Add(list);
            Controls.Add(name);
            Controls.Add(exe);
            Controls.Add(romFolder);
            Controls.Add(arguments);
            Controls.Add(extensions);

            Button add = MakeButton("\uE710  Añadir emulador", Theme.Px(20), Theme.Px(20), Theme.Px(220));
            add.Click += AddEmulator;
            Controls.Add(add);

            Button findExe = MakeButton("Buscar .exe", Theme.Px(620), Theme.Px(120), Theme.Px(120));
            findExe.Click += FindExe;
            Controls.Add(findExe);

            Button findRoms = MakeButton("Buscar carpeta", Theme.Px(620), Theme.Px(185), Theme.Px(120));
            findRoms.Click += FindRomFolder;
            Controls.Add(findRoms);

            Button remove = MakeButton("Quitar", Theme.Px(20), Theme.Px(450), Theme.Px(100));
            remove.Click += RemoveEmulator;
            Controls.Add(remove);

            Button save = MakeButton("Guardar", ClientSize.Width - Theme.Px(230), Theme.Px(450), Theme.Px(100));
            save.Click += delegate { SaveSelected(); DialogResult = DialogResult.OK; Close(); };
            Controls.Add(save);

            Button cancel = MakeButton("Cancelar", ClientSize.Width - Theme.Px(120), Theme.Px(450), Theme.Px(100));
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);

            RefreshList();
            Paint += EmulatorsDialog_Paint;
        }

        private TextBox MakeTextBox(int x, int y, int width)
        {
            TextBox box = new TextBox();
            box.SetBounds(x, y, width, Theme.Px(30));
            box.BackColor = Theme.Surface;
            box.ForeColor = Theme.Text;
            box.BorderStyle = BorderStyle.FixedSingle;
            box.TextChanged += delegate { SaveSelected(false); RefreshListText(); };
            return box;
        }

        private Button MakeButton(string text, int x, int y, int width)
        {
            Button button = new Button();
            button.Text = text;
            button.SetBounds(x, y, width, Theme.Px(36));
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Theme.Stroke;
            button.BackColor = Theme.Surface;
            button.ForeColor = Theme.Text;
            button.UseVisualStyleBackColor = false;
            return button;
        }

        private void RefreshList()
        {
            list.BeginUpdate();
            list.Items.Clear();
            foreach (EmulatorConfig emulator in emulators)
                list.Items.Add(string.IsNullOrEmpty(emulator.Name) ? "Nuevo emulador" : emulator.Name);
            list.EndUpdate();
            if (list.Items.Count > 0) list.SelectedIndex = 0;
        }

        private void RefreshListText()
        {
            if (list.SelectedIndex < 0 || list.SelectedIndex >= emulators.Count) return;
            list.Items[list.SelectedIndex] = string.IsNullOrEmpty(name.Text) ? "Nuevo emulador" : name.Text;
        }

        private void LoadSelected()
        {
            if (list.SelectedIndex < 0 || list.SelectedIndex >= emulators.Count)
            {
                name.Text = exe.Text = romFolder.Text = arguments.Text = extensions.Text = "";
                return;
            }
            EmulatorConfig emulator = emulators[list.SelectedIndex];
            name.Text = emulator.Name ?? "";
            exe.Text = emulator.ExePath ?? "";
            romFolder.Text = emulator.RomFolder ?? "";
            arguments.Text = string.IsNullOrEmpty(emulator.Arguments) ? "{rom}" : emulator.Arguments;
            extensions.Text = emulator.Extensions ?? "";
        }

        private void SaveSelected(bool refreshText)
        {
            if (list.SelectedIndex < 0 || list.SelectedIndex >= emulators.Count) return;
            EmulatorConfig emulator = emulators[list.SelectedIndex];
            emulator.Name = name.Text.Trim();
            emulator.ExePath = exe.Text.Trim();
            emulator.RomFolder = romFolder.Text.Trim();
            emulator.Arguments = arguments.Text.Trim();
            emulator.Extensions = extensions.Text.Trim();
        }

        private void SaveSelected()
        {
            SaveSelected(true);
        }

        private void AddEmulator(object sender, EventArgs e)
        {
            EmulatorConfig emulator = new EmulatorConfig();
            emulator.Id = Guid.NewGuid().ToString("N");
            emulator.Name = "Nuevo emulador";
            emulators.Add(emulator);
            RefreshList();
            list.SelectedIndex = emulators.Count - 1;
            name.Select();
            name.SelectAll();
        }

        private void RemoveEmulator(object sender, EventArgs e)
        {
            if (list.SelectedIndex < 0) return;
            emulators.RemoveAt(list.SelectedIndex);
            RefreshList();
        }

        private void FindExe(object sender, EventArgs e)
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "Seleccionar ejecutable del emulador";
                dlg.Filter = "Ejecutables (*.exe)|*.exe|Todos los archivos (*.*)|*.*";
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    exe.Text = dlg.FileName;
                    if (name.Text.Length == 0)
                        name.Text = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);
                }
            }
        }

        private void FindRomFolder(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Selecciona la carpeta donde están las ROM/ISO";
                dlg.ShowNewFolderButton = false;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    romFolder.Text = dlg.SelectedPath;
            }
        }

        private void EmulatorsDialog_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            Theme.DrawText(g, "Emuladores", Theme.Title, Theme.Text,
                new Rectangle(Theme.Px(20), Theme.Px(12), Theme.Px(300), Theme.Px(36)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            Theme.DrawText(g, "Configuración", Theme.SmallBold, Theme.TextDim,
                new Rectangle(Theme.Px(270), Theme.Px(64), Theme.Px(300), Theme.Px(18)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            DrawLabel(g, "Nombre", Theme.Px(270), Theme.Px(72));
            DrawLabel(g, "Ejecutable", Theme.Px(270), Theme.Px(137));
            DrawLabel(g, "Carpeta de ROM/ISO", Theme.Px(270), Theme.Px(202));
            DrawLabel(g, "Argumentos ({rom})", Theme.Px(270), Theme.Px(267));
            DrawLabel(g, "Extensiones separadas por comas", Theme.Px(270), Theme.Px(332));
        }

        private void DrawLabel(Graphics g, string text, int x, int y)
        {
            Theme.DrawText(g, text, Theme.SmallBold, Theme.TextFaint,
                new Rectangle(x, y, Theme.Px(350), Theme.Px(16)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }
}
