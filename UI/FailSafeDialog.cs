using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using AsusFanControlKimera.Diagnostics;
using AsusFanControlKimera.Localization;

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
            Text = Strings.Get("FailSafeTitle");
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
                    ? Strings.Get("FailSafeReleased")
                    : Strings.Get("FailSafeUnconfirmed"),
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
                Text = Strings.Format("FailSafeDetails",
                    occurredAt, cause, affectedFans, snapshot,
                    released ? Strings.Get("Succeeded") : Strings.Get("NotConfirmed"))
            };

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(8)
            };
            var acknowledge = new Button { Text = Strings.Get("Acknowledge"), Size = new Size(110, 30) };
            acknowledge.Click += delegate { Close(); };
            var openLog = new Button
            {
                Text = Strings.Get("OpenLog"),
                Size = new Size(110, 30),
                Enabled = DiagnosticLogger.Enabled && File.Exists(DiagnosticLogger.LogPath)
            };
            openLog.Click += delegate
            {
                try
                {
                    var viewer = new LogViewerDialog();
                    viewer.Show(this);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Strings.Get("OpenLogFailed"),
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
