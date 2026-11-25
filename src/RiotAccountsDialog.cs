using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace GamesOfAll
{
    internal sealed class RiotAccountsDialog : Form
    {
        private readonly FlowLayoutPanel rows;
        private readonly Label empty;
        private readonly Action<string> removeAccount;
        private readonly Action<string> restoreAccount;

        public RiotAccountsDialog(IList<GameAccountStats> active, IList<GameAccountStats> ignored,
            Action<string> removeAccount, Action<string> restoreAccount)
        {
            this.removeAccount = removeAccount;
            this.restoreAccount = restoreAccount;
            Text = "Cuentas de Riot";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(Theme.Px(650), Theme.Px(490));
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.Body;

            Label heading = new Label
            {
                Text = "Cuentas conocidas de Riot",
                Font = Theme.MakeFont("Segoe UI Semibold", 18f, FontStyle.Bold),
                ForeColor = Theme.Text,
                BackColor = Color.Transparent,
                AutoSize = true
            };
            heading.SetBounds(Theme.Px(22), Theme.Px(18), Theme.Px(520), Theme.Px(32));
            Controls.Add(heading);

            Label hint = new Label
            {
                Text = "Games detecta las cuentas que iniciaste con el cliente abierto. Puedes ocultarlas y restaurarlas; esto no modifica tu cuenta de Riot.",
                Font = Theme.Small,
                ForeColor = Theme.TextDim,
                BackColor = Color.Transparent
            };
            hint.SetBounds(Theme.Px(22), Theme.Px(58), Theme.Px(602), Theme.Px(38));
            Controls.Add(hint);

            rows = new FlowLayoutPanel();
            rows.FlowDirection = FlowDirection.TopDown;
            rows.WrapContents = false;
            rows.AutoScroll = true;
            rows.BackColor = Theme.Bg;
            rows.SetBounds(Theme.Px(18), Theme.Px(104), Theme.Px(614), Theme.Px(326));
            Controls.Add(rows);

            empty = new Label
            {
                Text = "Todavía no se detectó ninguna cuenta.",
                Font = Theme.Body,
                ForeColor = Theme.TextFaint,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill
            };
            rows.Controls.Add(empty);

            if (active != null)
                foreach (GameAccountStats account in active) AddRow(account, false);
            if (ignored != null)
                foreach (GameAccountStats account in ignored) AddRow(account, true);

            Button close = new Button
            {
                Text = "Cerrar",
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Surface,
                ForeColor = Theme.Text,
                UseVisualStyleBackColor = false,
                Cursor = Cursors.Hand
            };
            close.FlatAppearance.BorderColor = Theme.Stroke;
            close.SetBounds(Theme.Px(510), Theme.Px(444), Theme.Px(112), Theme.Px(32));
            close.Click += delegate { Close(); };
            Controls.Add(close);
        }

        private void AddRow(GameAccountStats account, bool removed)
        {
            if (account == null || string.IsNullOrWhiteSpace(account.AccountId)) return;
            if (empty.Parent == rows) rows.Controls.Remove(empty);
            Panel row = new Panel();
            row.BackColor = Theme.Surface;
            row.Margin = new Padding(Theme.Px(4));
            row.Size = new Size(Theme.Px(580), Theme.Px(76));

            string summary = (string.IsNullOrWhiteSpace(account.AccountName) ? "Cuenta Riot" : account.AccountName);
            if (account.Level > 0) summary += "  ·  Nivel " + account.Level;
            if (!string.IsNullOrWhiteSpace(account.Rank)) summary += "  ·  " + account.Rank;
            if (account.PlaySeconds > 0) summary += "  ·  " + Fmt.Playtime(account.PlaySeconds);
            if (!string.IsNullOrWhiteSpace(account.Region)) summary += "  ·  " + account.Region.ToUpperInvariant();

            Label details = new Label
            {
                Text = (removed ? "Eliminada · " : "") + summary,
                Font = Theme.SmallBold,
                ForeColor = removed ? Theme.TextFaint : Theme.Text,
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };
            details.SetBounds(Theme.Px(14), Theme.Px(8), Theme.Px(405), Theme.Px(58));
            row.Controls.Add(details);

            Button action = new Button
            {
                Text = removed ? "Restaurar" : "Quitar",
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Bg,
                ForeColor = Theme.Text,
                UseVisualStyleBackColor = false,
                Cursor = Cursors.Hand
            };
            action.FlatAppearance.BorderColor = Theme.Stroke;
            action.SetBounds(Theme.Px(436), Theme.Px(21), Theme.Px(126), Theme.Px(32));
            action.Click += delegate
            {
                if (!removed)
                {
                    DialogResult answer = MessageBox.Show(this,
                        "Ocultar «" + (account.AccountName ?? "esta cuenta") +
                        "» de Games? Se conservarán sus datos locales por si quieres restaurarla.",
                        "Quitar cuenta Riot", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (answer != DialogResult.Yes) return;
                    if (removeAccount != null) removeAccount(account.AccountId);
                }
                else if (restoreAccount != null) restoreAccount(account.AccountId);
                rows.Controls.Remove(row);
                row.Dispose();
                if (rows.Controls.Count == 0) rows.Controls.Add(empty);
            };
            row.Controls.Add(action);
            rows.Controls.Add(row);
        }
    }
}
