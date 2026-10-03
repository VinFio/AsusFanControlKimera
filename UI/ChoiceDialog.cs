using System;
using System.Drawing;
using System.Windows.Forms;

namespace AsusFanControlKimera.UI
{
    /// <summary>Finestra di messaggio con pulsanti personalizzati.</summary>
    internal sealed class ChoiceDialog : Form
    {
        private int choice;

        private ChoiceDialog(string title, string message, Icon icon,
            string[] buttons, int defaultIndex, int cancelIndex)
        {
            choice = cancelIndex;
            Text = title;
            Font = new Font("Segoe UI", 9F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            TopMost = true;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(12);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            var layout = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 2
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var picture = new PictureBox
            {
                Image = icon.ToBitmap(),
                SizeMode = PictureBoxSizeMode.AutoSize,
                Margin = new Padding(0, 0, 12, 0)
            };
            var text = new Label
            {
                Text = message,
                AutoSize = true,
                MaximumSize = new Size(470, 0),
                Margin = new Padding(0, 4, 0, 16)
            };
            layout.Controls.Add(picture, 0, 0);
            layout.Controls.Add(text, 1, 0);

            var buttonPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.RightToLeft,
                Anchor = AnchorStyles.Right,
                WrapContents = false,
                Margin = new Padding(0)
            };
            // RightToLeft: aggiunti in ordine inverso per mostrarli da sinistra a destra.
            for (int index = buttons.Length - 1; index >= 0; index--)
            {
                int selected = index;
                var button = new Button
                {
                    Text = buttons[index],
                    AutoSize = true,
                    MinimumSize = new Size(96, 28),
                    Margin = new Padding(6, 0, 0, 0)
                };
                button.Click += delegate
                {
                    choice = selected;
                    DialogResult = DialogResult.OK;
                    Close();
                };
                buttonPanel.Controls.Add(button);
                if (index == defaultIndex)
                    AcceptButton = button;
                if (index == cancelIndex)
                    CancelButton = button;
            }
            layout.Controls.Add(buttonPanel, 0, 1);
            layout.SetColumnSpan(buttonPanel, 2);
            Controls.Add(layout);

            Shown += delegate
            {
                var accept = AcceptButton as Button;
                if (accept != null)
                    accept.Focus();
            };
        }

        /// <summary>Restituisce l'indice del pulsante scelto, oppure cancelIndex se la finestra viene chiusa.</summary>
        internal static int Show(string title, string message, Icon icon,
            string[] buttons, int defaultIndex, int cancelIndex)
        {
            using (var dialog = new ChoiceDialog(title, message, icon, buttons, defaultIndex, cancelIndex))
            {
                dialog.ShowDialog();
                return dialog.choice;
            }
        }
    }
}
