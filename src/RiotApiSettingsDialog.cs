using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace GamesOfAll
{
    internal sealed class RiotApiSettingsDialog : Form
    {
        private readonly TextBox apiKey;
        private readonly ComboBox region;
        private readonly string existingKey;

        public string ProtectedApiKey { get; private set; }
        public string SelectedRegion { get; private set; }

        public RiotApiSettingsDialog(string protectedKey, string selectedRegion)
        {
            existingKey = protectedKey;
            ProtectedApiKey = protectedKey;
            SelectedRegion = selectedRegion;
            Text = "Datos online de Riot";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(Theme.Px(570), Theme.Px(330));
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.Body;

            Label heading = MakeLabel("Riot API", Theme.MakeFont("Segoe UI Semibold", 18f, FontStyle.Bold), Theme.Text);
            heading.SetBounds(Theme.Px(24), Theme.Px(18), Theme.Px(500), Theme.Px(32));
            Controls.Add(heading);

            Label explanation = MakeLabel(
                "La consulta oficial por internet necesita una clave de Riot Developer. Se cifra con Windows DPAPI y solo se envía a api.riotgames.com.",
                Theme.Small, Theme.TextDim);
            explanation.SetBounds(Theme.Px(24), Theme.Px(57), Theme.Px(520), Theme.Px(42));
            Controls.Add(explanation);

            LinkLabel portal = new LinkLabel();
            portal.Text = "Abrir Riot Developer Portal";
            portal.Font = Theme.Small;
            portal.LinkColor = Theme.Accent;
            portal.ActiveLinkColor = Theme.AccentDeep;
            portal.SetBounds(Theme.Px(24), Theme.Px(99), Theme.Px(260), Theme.Px(22));
            portal.LinkClicked += delegate
            {
                try
                {
                    Process.Start(new ProcessStartInfo("https://developer.riotgames.com/") { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    Storage.Log("No se pudo abrir Riot Developer Portal: " + ex.Message);
                }
            };
            Controls.Add(portal);

            Label keyLabel = MakeLabel("Clave de API", Theme.SmallBold, Theme.Text);
            keyLabel.SetBounds(Theme.Px(24), Theme.Px(132), Theme.Px(180), Theme.Px(20));
            Controls.Add(keyLabel);

            apiKey = new TextBox();
            apiKey.UseSystemPasswordChar = true;
            apiKey.BorderStyle = BorderStyle.FixedSingle;
            apiKey.BackColor = Theme.Surface;
            apiKey.ForeColor = Theme.Text;
            apiKey.Font = Theme.Body;
            apiKey.SetBounds(Theme.Px(24), Theme.Px(154), Theme.Px(520), Theme.Px(29));
            Controls.Add(apiKey);

            Label keyStatus = MakeLabel(string.IsNullOrEmpty(existingKey)
                ? "No hay una clave guardada. Déjalo vacío para no configurar internet."
                : "Ya hay una clave cifrada guardada. Déjalo vacío para conservarla.",
                Theme.Small, Theme.TextFaint);
            keyStatus.SetBounds(Theme.Px(24), Theme.Px(185), Theme.Px(520), Theme.Px(20));
            Controls.Add(keyStatus);

            Label regionLabel = MakeLabel("Región de respaldo para cuentas sin región detectada", Theme.SmallBold, Theme.Text);
            regionLabel.SetBounds(Theme.Px(24), Theme.Px(212), Theme.Px(520), Theme.Px(20));
            Controls.Add(regionLabel);

            region = new ComboBox();
            region.DropDownStyle = ComboBoxStyle.DropDownList;
            region.BackColor = Theme.Surface;
            region.ForeColor = Theme.Text;
            region.Font = Theme.Body;
            region.Items.Add("Automática (Riot Client)");
            foreach (string value in RiotApiService.SupportedRegions) region.Items.Add(value);
            region.SelectedIndex = 0;
            for (int i = 1; i < region.Items.Count; i++)
                if (string.Equals(region.Items[i].ToString(), selectedRegion, StringComparison.OrdinalIgnoreCase))
                    region.SelectedIndex = i;
            region.SetBounds(Theme.Px(24), Theme.Px(235), Theme.Px(210), Theme.Px(28));
            Controls.Add(region);

            Button save = MakeButton("Guardar");
            save.SetBounds(Theme.Px(306), Theme.Px(282), Theme.Px(112), Theme.Px(32));
            save.Click += delegate
            {
                if (!string.IsNullOrWhiteSpace(apiKey.Text))
                    ProtectedApiKey = RiotApiService.ProtectKey(apiKey.Text);
                SelectedRegion = region.SelectedIndex <= 0 ? null : region.SelectedItem.ToString();
                DialogResult = DialogResult.OK;
            };
            Controls.Add(save);

            Button clear = MakeButton("Quitar clave");
            clear.SetBounds(Theme.Px(182), Theme.Px(282), Theme.Px(112), Theme.Px(32));
            clear.Click += delegate
            {
                ProtectedApiKey = null;
                SelectedRegion = region.SelectedIndex <= 0 ? null : region.SelectedItem.ToString();
                DialogResult = DialogResult.OK;
            };
            Controls.Add(clear);

            Button cancel = MakeButton("Cancelar");
            cancel.SetBounds(Theme.Px(432), Theme.Px(282), Theme.Px(112), Theme.Px(32));
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(cancel);
            AcceptButton = save;
            CancelButton = cancel;
        }

        private Label MakeLabel(string text, Font font, Color color)
        {
            return new Label
            {
                Text = text,
                Font = font,
                ForeColor = color,
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };
        }

        private Button MakeButton(string text)
        {
            return new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Surface,
                ForeColor = Theme.Text,
                FlatAppearance = { BorderColor = Theme.Stroke, BorderSize = 1 },
                UseVisualStyleBackColor = false,
                Cursor = Cursors.Hand
            };
        }
    }
}
