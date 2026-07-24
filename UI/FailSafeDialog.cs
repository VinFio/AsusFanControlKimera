using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using AsusFanControlKimera.Diagnostics;

namespace AsusFanControlKimera.UI
{
    internal sealed class FailSafeDialog : Form
    {
        internal FailSafeDialog(
            DateTime occurredAt,
            string cause,
            string affectedFans,
            string snapshot,
            bool released)
        {
            Text = "Kimera - FAIL-SAFE attivato";
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            ControlBox = false;
            ShowInTaskbar = true;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ClientSize = new Size(570, 330);
            Font = new Font("Segoe UI", 9F);
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            var title = new Label
            {
                Text = released
                    ? "Controllo restituito al firmware ASUS"
                    : "ATTENZIONE: rilascio al firmware non confermato",
                ForeColor = released ? Color.DarkGreen : Color.Firebrick,
                Font = new Font(Font, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 38,
                TextAlign = ContentAlignment.MiddleCenter
            };

            var details = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                BackColor = SystemColors.Window,
                Text = string.Format(
                    "Ora: {0:yyyy-MM-dd HH:mm:ss}\r\n\r\n" +
                    "Causa: {1}\r\n\r\n" +
                    "Ventola/e coinvolta/e: {2}\r\n\r\n" +
                    "Ultimi valori: {3}\r\n\r\n" +
                    "Rilascio al firmware: {4}",
                    occurredAt, cause, affectedFans, snapshot,
                    released ? "RIUSCITO" : "NON CONFERMATO")
            };

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(8)
            };
            var acknowledge = new Button { Text = "Ho compreso", Size = new Size(110, 30) };
            acknowledge.Click += delegate { Close(); };
            var openLog = new Button
            {
                Text = "Apri registro",
                Size = new Size(110, 30),
                Enabled = DiagnosticLogger.Enabled && File.Exists(DiagnosticLogger.LogPath)
            };
            openLog.Click += delegate
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "notepad.exe",
                        Arguments = "\"" + DiagnosticLogger.LogPath + "\"",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Impossibile aprire il registro",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            buttons.Controls.Add(acknowledge);
            buttons.Controls.Add(openLog);

            Controls.Add(details);
            Controls.Add(title);
            Controls.Add(buttons);
            AcceptButton = acknowledge;
        }
    }
}
