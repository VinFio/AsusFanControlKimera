using System;
using System.Drawing;
using System.Windows.Forms;
using AsusFanControlKimera.Diagnostics;
using AsusFanControlKimera.Localization;

namespace AsusFanControlKimera.UI
{
    // Visualizzatore interno: Kimera gira come SYSTEM, quindi non apre editor esterni
    // (le finestre Apri/Salva di un processo SYSTEM permetterebbero di avviare
    // programmi con gli stessi privilegi).
    internal sealed class LogViewerDialog : Form
    {
        private readonly TextBox content = new TextBox();

        internal LogViewerDialog()
        {
            Text = Strings.Get("LogViewerTitle");
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = true;
            MinimizeBox = false;
            ClientSize = new Size(860, 520);
            MinimumSize = new Size(480, 300);
            Font = new Font("Segoe UI", 9F);
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            content.Multiline = true;
            content.ReadOnly = true;
            content.WordWrap = false;
            content.ScrollBars = ScrollBars.Both;
            content.Dock = DockStyle.Fill;
            content.BackColor = SystemColors.Window;
            content.Font = new Font("Consolas", 9F);
            content.MaxLength = 0;

            var path = new Label
            {
                Text = DiagnosticLogger.LogPath,
                Dock = DockStyle.Top,
                Height = 26,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(6, 0, 6, 0)
            };

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(8)
            };
            var close = new Button { Text = Strings.Get("Close"), Size = new Size(100, 28) };
            close.Click += delegate { Close(); };
            var reload = new Button { Text = Strings.Get("Refresh"), Size = new Size(100, 28) };
            reload.Click += delegate { LoadContent(); };
            buttons.Controls.Add(close);
            buttons.Controls.Add(reload);

            Controls.Add(content);
            Controls.Add(path);
            Controls.Add(buttons);
            AcceptButton = close;
            CancelButton = close;
            Shown += delegate { LoadContent(); };
        }

        private void LoadContent()
        {
            try
            {
                content.Text = DiagnosticLogger.ReadLog();
            }
            catch (Exception ex)
            {
                content.Text = Strings.Format("LogReadFailed", ex.Message);
            }
            content.SelectionStart = content.TextLength;
            content.ScrollToCaret();
        }
    }
}
