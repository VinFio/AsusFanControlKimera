using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using AsusFanControlKimera.Diagnostics;
using AsusFanControlKimera.Hardware;
using AsusFanControlKimera.Model;
using AsusFanControlKimera.Properties;
using Microsoft.Win32;

namespace AsusFanControlKimera.UI
{
    internal sealed class MainForm : Form
    {
        private readonly RadioButton systemMode = new RadioButton();
        private readonly RadioButton manualMode = new RadioButton();
        private readonly RadioButton curveMode = new RadioButton();
        private readonly TrackBar manualSpeed = new TrackBar();
        private readonly Label manualValue = new Label();
        private readonly Label temperatureValue = new Label();
        private readonly Label rpmValue = new Label();
        private readonly Label statusValue = new Label();
        private readonly CurveEditor curveEditor = new CurveEditor();
        private readonly TextBox curveText = new TextBox();
        private readonly NumericUpDown hysteresis = new NumericUpDown();
        private readonly NumericUpDown interval = new NumericUpDown();
        private readonly ToolStripMenuItem safeLimitsItem = new ToolStripMenuItem();
        private readonly ToolStripMenuItem releaseOnExitItem = new ToolStripMenuItem();
        private readonly ToolStripMenuItem minimizeToTrayItem = new ToolStripMenuItem();
        private readonly ToolStripMenuItem startMinimizedItem = new ToolStripMenuItem();
        private readonly ToolStripMenuItem startWithWindowsItem = new ToolStripMenuItem();
        private readonly ToolStripMenuItem debugItem = new ToolStripMenuItem();
        private readonly NotifyIcon trayIcon = new NotifyIcon();
        private readonly Timer refreshTimer = new Timer();
        private readonly Timer startupTimer = new Timer();
        private AsusFanController controller;
        private List<Point> curvePoints;
        private int lastCurveTemperature = int.MinValue;
        private int lastAppliedSpeed = -1;
        private bool loading = true;
        private bool exiting;
        private bool refreshInProgress;
        private bool failSafeActive;
        private int consecutiveReadFailures;
        private int consecutiveInvalidTemperatures;
        private int consecutiveZeroRpmSamples;
        private ulong lastObservedTemperature;
        private IList<int> lastObservedFanSpeeds = new List<int>();
        private FailSafeDialog failSafeDialog;

        internal MainForm()
        {
            Text = "Asus Fan Control Kimera";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(760, 690);
            Size = new Size(850, 735);
            Font = new Font("Segoe UI", 9F);

            BuildUi();
            LoadSettings();
            InitializeHardware();

            refreshTimer.Tick += RefreshTimerTick;
            startupTimer.Interval = 750;
            startupTimer.Tick += StartupTimerTick;
            Shown += MainFormShown;
            FormClosing += MainFormClosing;
            Resize += MainFormResize;
        }

        private void BuildUi()
        {
            var menu = new MenuStrip();
            var options = new ToolStripMenuItem("Opzioni");
            safeLimitsItem.Text = "Limiti sicuri (minimo 40%, massimo 99%)";
            releaseOnExitItem.Text = "Rilascia il controllo ventole all'uscita";
            minimizeToTrayItem.Text = "Riduci nell'area di notifica";
            startMinimizedItem.Text = "Avvia ridotto nell'area di notifica";
            startWithWindowsItem.Text = "Avvia con Windows (richiede conferma UAC)";
            debugItem.Text = "Debug";
            safeLimitsItem.CheckOnClick = releaseOnExitItem.CheckOnClick =
                minimizeToTrayItem.CheckOnClick = startMinimizedItem.CheckOnClick =
                startWithWindowsItem.CheckOnClick = true;
            debugItem.CheckOnClick = true;
            safeLimitsItem.CheckedChanged += SettingsMenuChanged;
            releaseOnExitItem.CheckedChanged += SettingsMenuChanged;
            minimizeToTrayItem.CheckedChanged += SettingsMenuChanged;
            startMinimizedItem.CheckedChanged += SettingsMenuChanged;
            startWithWindowsItem.CheckedChanged += StartWithWindowsChanged;
            debugItem.CheckedChanged += DebugChanged;
            options.DropDownItems.AddRange(new ToolStripItem[] {
                safeLimitsItem, releaseOnExitItem, minimizeToTrayItem,
                new ToolStripSeparator(), startMinimizedItem, startWithWindowsItem,
                new ToolStripSeparator(), debugItem,
                new ToolStripSeparator(), new ToolStripMenuItem("Ripristina impostazioni", null, ResetSettings)
            });
            var help = new ToolStripMenuItem("Aiuto");
            help.DropDownItems.Add(new ToolStripMenuItem("Informazioni", null, ShowAbout));
            menu.Items.AddRange(new ToolStripItem[] { options, help });
            MainMenuStrip = menu;
            Controls.Add(menu);

            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            // DockStyle.Fill occupa anche l'area sotto una MenuStrip aggiunta
            // separatamente. Riserviamo esplicitamente l'altezza del menu.
            root.Padding = new Padding(12, menu.Height + 8, 12, 12);
            root.RowCount = 4;
            root.ColumnCount = 1;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            Controls.Add(root);
            menu.BringToFront();

            var top = new TableLayoutPanel();
            top.Dock = DockStyle.Fill;
            top.ColumnCount = 3;
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
            root.Controls.Add(top, 0, 0);

            var modeBox = new GroupBox { Text = "Modalità di controllo", Dock = DockStyle.Fill };
            var modeFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(8)
            };
            systemMode.Text = "Sistema ASUS (controllo disattivato)";
            manualMode.Text = "Manuale";
            curveMode.Text = "Curva temperatura";
            systemMode.AutoSize = manualMode.AutoSize = curveMode.AutoSize = true;
            systemMode.CheckedChanged += ModeChanged;
            manualMode.CheckedChanged += ModeChanged;
            curveMode.CheckedChanged += ModeChanged;
            modeFlow.Controls.AddRange(new Control[] { systemMode, manualMode, curveMode });
            modeBox.Controls.Add(modeFlow);
            top.Controls.Add(modeBox, 0, 0);

            var manualBox = new GroupBox { Text = "Velocità manuale", Dock = DockStyle.Fill };
            manualSpeed.Minimum = 1;
            manualSpeed.Maximum = 100;
            manualSpeed.TickFrequency = 10;
            manualSpeed.Dock = DockStyle.Top;
            manualSpeed.ValueChanged += delegate
            {
                manualValue.Text = manualSpeed.Value + "%";
            };
            manualSpeed.MouseUp += delegate
            {
                if (!loading && controller != null && manualMode.Checked)
                    ApplyManual(true);
            };
            manualSpeed.KeyUp += delegate(object sender, KeyEventArgs e)
            {
                if (!loading && controller != null && manualMode.Checked &&
                    (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right ||
                     e.KeyCode == Keys.PageUp || e.KeyCode == Keys.PageDown ||
                     e.KeyCode == Keys.Home || e.KeyCode == Keys.End))
                    ApplyManual(true);
            };
            manualValue.Dock = DockStyle.Bottom;
            manualValue.Height = 28;
            manualValue.TextAlign = ContentAlignment.MiddleCenter;
            manualValue.Font = new Font(Font, FontStyle.Bold);
            manualBox.Controls.Add(manualSpeed);
            manualBox.Controls.Add(manualValue);
            top.Controls.Add(manualBox, 1, 0);

            var statsBox = new GroupBox { Text = "Stato hardware", Dock = DockStyle.Fill };
            var stats = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(8, 4, 8, 4) };
            stats.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 68));
            stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            stats.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            stats.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            stats.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            temperatureValue.Dock = DockStyle.Fill;
            temperatureValue.AutoEllipsis = true;
            temperatureValue.TextAlign = ContentAlignment.MiddleLeft;
            rpmValue.Dock = DockStyle.Fill;
            rpmValue.AutoEllipsis = true;
            rpmValue.TextAlign = ContentAlignment.MiddleLeft;
            stats.Controls.Add(new Label
            {
                Text = "CPU:",
                Dock = DockStyle.Fill,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            stats.Controls.Add(temperatureValue, 1, 0);
            stats.Controls.Add(new Label
            {
                Text = "Ventole:",
                Dock = DockStyle.Fill,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 1);
            stats.Controls.Add(rpmValue, 1, 1);
            var refreshButton = new Button
            {
                Text = "Aggiorna",
                AutoSize = false,
                Size = new Size(92, 28),
                Margin = new Padding(0, 3, 0, 0)
            };
            refreshButton.Click += delegate { RefreshHardware(true); };
            stats.Controls.Add(refreshButton, 0, 2);
            stats.SetColumnSpan(refreshButton, 2);
            statsBox.Controls.Add(stats);
            top.Controls.Add(statsBox, 2, 0);

            var curveBox = new GroupBox { Text = "Curva ventole — doppio clic aggiunge, trascina sposta, clic destro elimina", Dock = DockStyle.Fill };
            curveEditor.Dock = DockStyle.Fill;
            curveEditor.CurveChanged += CurveEditorChanged;
            curveBox.Controls.Add(curveEditor);
            root.Controls.Add(curveBox, 0, 1);

            var curveControls = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(0, 6, 0, 2)
            };
            curveControls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            curveControls.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 224));
            curveControls.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            curveControls.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            curveText.Dock = DockStyle.Fill;
            curveText.Font = new Font("Consolas", 9F);
            var applyCurve = new Button { Text = "Applica", Dock = DockStyle.Fill };
            var resetCurve = new Button { Text = "Predefinita", Dock = DockStyle.Fill };
            var curveButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 4, 0, 0)
            };
            applyCurve.Dock = DockStyle.None;
            applyCurve.Size = new Size(84, 34);
            resetCurve.Dock = DockStyle.None;
            resetCurve.Size = new Size(96, 34);
            curveButtons.Controls.Add(applyCurve);
            curveButtons.Controls.Add(resetCurve);
            applyCurve.Click += ApplyCurveText;
            resetCurve.Click += ResetCurve;
            hysteresis.Minimum = 0; hysteresis.Maximum = 15; hysteresis.Dock = DockStyle.Fill;
            interval.Minimum = 500; interval.Maximum = 10000; interval.Increment = 500; interval.Dock = DockStyle.Fill;
            hysteresis.ValueChanged += NumericSettingsChanged;
            interval.ValueChanged += NumericSettingsChanged;

            var hysteresisPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(4, 8, 0, 0)
            };
            var hysteresisLabel = new Label
            {
                Text = "Isteresi °C",
                AutoSize = true,
                Margin = new Padding(0, 5, 8, 0)
            };
            hysteresis.Dock = DockStyle.None;
            hysteresis.Width = 68;
            hysteresisPanel.Controls.Add(hysteresisLabel);
            hysteresisPanel.Controls.Add(hysteresis);

            var intervalPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(4, 8, 0, 0)
            };
            var intervalLabel = new Label
            {
                Text = "Intervallo ms",
                AutoSize = true,
                Margin = new Padding(0, 5, 8, 0)
            };
            interval.Dock = DockStyle.None;
            interval.Width = 76;
            intervalPanel.Controls.Add(intervalLabel);
            intervalPanel.Controls.Add(interval);

            var curveSettingsPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(0)
            };
            curveSettingsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            curveSettingsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            curveSettingsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            curveSettingsPanel.Controls.Add(intervalPanel, 0, 0);
            curveSettingsPanel.Controls.Add(hysteresisPanel, 0, 1);

            curveControls.Controls.Add(curveText, 0, 0);
            curveControls.Controls.Add(curveButtons, 0, 1);
            curveControls.Controls.Add(curveSettingsPanel, 1, 0);
            curveControls.SetRowSpan(curveSettingsPanel, 2);
            root.Controls.Add(curveControls, 0, 2);

            statusValue.Dock = DockStyle.Fill;
            statusValue.TextAlign = ContentAlignment.MiddleLeft;
            statusValue.ForeColor = Color.DimGray;
            root.Controls.Add(statusValue, 0, 3);

            var trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Apri", null, delegate { RestoreFromTray(); });
            trayMenu.Items.Add("Controllo sistema ASUS", null, delegate { systemMode.Checked = true; });
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("Esci", null, delegate { exiting = true; Close(); });
            trayIcon.Text = "Asus Fan Control Kimera";
            Icon applicationIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            trayIcon.Icon = applicationIcon ?? SystemIcons.Application;
            trayIcon.ContextMenuStrip = trayMenu;
            trayIcon.MouseDoubleClick += delegate { RestoreFromTray(); };
            Icon = applicationIcon ?? SystemIcons.Application;
        }

        private void LoadSettings()
        {
            if (Settings.Default.UpgradeRequired)
            {
                try
                {
                    Settings.Default.Upgrade();
                }
                catch
                {
                    // Prima installazione o profilo precedente non disponibile.
                }
                Settings.Default.UpgradeRequired = false;
                Settings.Default.Save();
            }
            if (!string.IsNullOrEmpty(Program.InteractiveUserSid) &&
                Settings.Default.InteractiveUserSid != Program.InteractiveUserSid)
            {
                Settings.Default.InteractiveUserSid = Program.InteractiveUserSid;
                Settings.Default.Save();
            }

            safeLimitsItem.Checked = Settings.Default.SafeLimits;
            releaseOnExitItem.Checked = Settings.Default.ReleaseOnExit;
            minimizeToTrayItem.Checked = Settings.Default.MinimizeToTray;
            startMinimizedItem.Checked = Settings.Default.StartMinimized;
            startWithWindowsItem.Enabled = !string.IsNullOrEmpty(Program.InteractiveUserSid);
            startWithWindowsItem.Checked = IsStartupEnabled();
            debugItem.Checked = Settings.Default.DebugEnabled;
            DiagnosticLogger.Configure(Settings.Default.DebugEnabled);
            manualSpeed.Value = Clamp(Settings.Default.ManualSpeed, 1, 100);
            hysteresis.Value = Clamp(Settings.Default.Hysteresis, 0, 15);
            interval.Value = Clamp(Settings.Default.RefreshInterval, 500, 10000);
            try { curvePoints = FanCurve.Parse(Settings.Default.Curve); }
            catch { curvePoints = FanCurve.Parse(FanCurve.DefaultText); }
            curveEditor.Points = curvePoints;
            curveText.Text = FanCurve.Serialize(curvePoints);

            if (Settings.Default.Mode == "Curve") curveMode.Checked = true;
            else if (Settings.Default.Mode == "System") systemMode.Checked = true;
            else manualMode.Checked = true;
            loading = false;
        }

        private void InitializeHardware()
        {
            try
            {
                controller = new AsusFanController();
                DiagnosticLogger.Log("STARTUP", "Motore AsusFanControl inizializzato.");
                statusValue.Text = "Motore inizializzato; attendo che la tabella hardware ASUS sia pronta…";
                SetControlsAvailable(false);
                refreshTimer.Interval = (int)interval.Value;
                startupTimer.Start();
            }
            catch (Exception ex)
            {
                DiagnosticLogger.Log("ERROR", "Inizializzazione hardware fallita: " + ex);
                statusValue.Text = "Hardware non disponibile: " + ex.Message;
                statusValue.ForeColor = Color.Firebrick;
                systemMode.Checked = true;
                SetControlsAvailable(false);
                MessageBox.Show("Impossibile inizializzare AsusWinIO64.\n\n" + ex.Message +
                    "\n\nAvvia l'applicazione come amministratore e verifica che il modello ASUS sia supportato.",
                    "Kimera - hardware non disponibile", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void StartupTimerTick(object sender, EventArgs e)
        {
            startupTimer.Stop();
            try
            {
                int fanCount = await Task.Run(delegate { return controller.FanCount; });
                DiagnosticLogger.Log("STARTUP",
                    string.Format("Hardware pronto; fanCount={0}.", fanCount));
                statusValue.Text = string.Format("Hardware pronto: {0} ventola/e rilevata/e.", fanCount);
                SetControlsAvailable(true);
                DiagnosticLogger.Log("MODE",
                    "Modalità ripristinata all'avvio: " + CurrentModeName());
                ApplySelectedMode(true);
                RefreshHardware(true);
                refreshTimer.Start();
            }
            catch (Exception ex)
            {
                DiagnosticLogger.Log("ERROR", "Tabella hardware non pronta: " + ex);
                SetControlsAvailable(false);
                statusValue.ForeColor = Color.Firebrick;
                statusValue.Text = "Hardware non pronto: " + ex.Message;
                MessageBox.Show(ex.Message +
                    "\n\nChiudi eventuali altre applicazioni di controllo ASUS e riprova.",
                    "Kimera - ventole non rilevate", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void SetControlsAvailable(bool enabled)
        {
            manualMode.Enabled = enabled;
            curveMode.Enabled = enabled;
            manualSpeed.Enabled = enabled && manualMode.Checked;
            curveEditor.Enabled = enabled && curveMode.Checked;
        }

        private void ModeChanged(object sender, EventArgs e)
        {
            if (loading || !((RadioButton)sender).Checked)
                return;
            failSafeActive = false;
            ResetSafetyCounters();
            DiagnosticLogger.Log("MODE", "Modalità selezionata: " + CurrentModeName());
            ApplySelectedMode(false);
        }

        private void ApplySelectedMode(bool force)
        {
            manualSpeed.Enabled = manualMode.Checked;
            curveEditor.Enabled = curveMode.Checked;
            curveText.Enabled = curveMode.Checked;
            lastCurveTemperature = int.MinValue;
            lastAppliedSpeed = force ? -1 : lastAppliedSpeed;

            if (systemMode.Checked)
            {
                Settings.Default.Mode = "System";
                ApplySpeed(0, "Controllo rilasciato al sistema ASUS.", true);
            }
            else if (manualMode.Checked)
            {
                Settings.Default.Mode = "Manual";
                ApplyManual(true);
            }
            else if (curveMode.Checked)
            {
                Settings.Default.Mode = "Curve";
                statusValue.Text = "Curva attiva; in attesa della temperatura.";
                RefreshHardware(true);
            }
            Settings.Default.Save();
        }

        private void ApplyManual(bool force)
        {
            int speed = SafeSpeed(manualSpeed.Value);
            if (manualSpeed.Value != speed)
                manualSpeed.Value = speed;
            Settings.Default.ManualSpeed = speed;
            Settings.Default.Save();
            ApplySpeed(speed, string.Format("Modalità manuale: {0}% PWM.", speed), force);
        }

        private void ApplySpeed(int speed, string status, bool force)
        {
            if (controller == null || (!force && lastAppliedSpeed == speed))
                return;
            try
            {
                controller.SetAllFans(speed);
                lastAppliedSpeed = speed;
                DiagnosticLogger.Log("PWM", string.Format(
                    "mode={0}; requested={1}%; duty={2}/255; fans={3}",
                    CurrentModeName(), speed, controller.LastDuty, controller.LastFanCount));
                statusValue.ForeColor = Color.DimGray;
                statusValue.Text = string.Format("{0} Comando inviato a {1} ventola/e (duty {2}/255).",
                    status, controller.LastFanCount, controller.LastDuty);
                trayIcon.Text = TruncateTrayText(speed == 0 ? "Kimera - Sistema ASUS" : string.Format("Kimera - {0}% PWM", speed));
            }
            catch (Exception ex)
            {
                DiagnosticLogger.Log("ERROR", "Comando ventole fallito: " + ex);
                statusValue.ForeColor = Color.Firebrick;
                statusValue.Text = "Errore controllo ventole: " + ex.Message;
            }
        }

        private void RefreshTimerTick(object sender, EventArgs e)
        {
            RefreshHardware(false);
        }

        private async void RefreshHardware(bool forceCurve)
        {
            if (controller == null || refreshInProgress || exiting || IsDisposed)
                return;
            refreshInProgress = true;
            try
            {
                HardwareSnapshot snapshot = await Task.Run(delegate
                {
                    ulong measuredTemperature = controller.ReadCpuTemperature();
                    IList<int> fanSpeeds = controller.ReadFanSpeeds();
                    return new HardwareSnapshot(measuredTemperature, fanSpeeds);
                });
                if (exiting || IsDisposed)
                    return;

                ulong rawTemperature = snapshot.Temperature;
                IList<int> speeds = snapshot.FanSpeeds;
                lastObservedTemperature = rawTemperature;
                lastObservedFanSpeeds = speeds.ToList();
                consecutiveReadFailures = 0;
                temperatureValue.Text = rawTemperature + " °C";
                rpmValue.Text = string.Join(" / ", speeds.Select((rpm, i) =>
                    string.Format("F{0}: {1}", i + 1, rpm))) + " RPM";
                DiagnosticLogger.LogSnapshot(CurrentModeName(), rawTemperature, speeds,
                    lastAppliedSpeed, controller.LastDuty);

                int temperature = rawTemperature > int.MaxValue ? int.MaxValue : (int)rawTemperature;
                if (lastAppliedSpeed >= 40 && speeds.Count > 0 && speeds.Any(rpm => rpm <= 0))
                    consecutiveZeroRpmSamples++;
                else
                    consecutiveZeroRpmSamples = 0;

                if (consecutiveZeroRpmSamples >= 3)
                {
                    string affectedFans = string.Join(", ", speeds
                        .Select((rpm, index) => new { rpm, index })
                        .Where(item => item.rpm <= 0)
                        .Select(item => "F" + (item.index + 1)));
                    ActivateFailSafe(
                        "Una ventola ha restituito 0 RPM per tre letture consecutive.",
                        affectedFans);
                    return;
                }

                if (temperature < 10 || temperature > 115)
                {
                    consecutiveInvalidTemperatures++;
                    statusValue.ForeColor = Color.Firebrick;
                    statusValue.Text = string.Format(
                        "Temperatura ASUS non valida ({0} °C), tentativo {1}/2: curva sospesa.",
                        temperature, consecutiveInvalidTemperatures);
                    if (consecutiveInvalidTemperatures >= 2)
                        ActivateFailSafe(
                            "Il sensore ASUS ha restituito due temperature non valide consecutive.",
                            "-");
                    return;
                }
                consecutiveInvalidTemperatures = 0;

                if (curveMode.Checked &&
                    (forceCurve || lastCurveTemperature == int.MinValue ||
                     Math.Abs(temperature - lastCurveTemperature) >= (int)hysteresis.Value))
                {
                    int speed = SafeSpeed(FanCurve.Evaluate(curvePoints, temperature));
                    ApplySpeed(speed, string.Format("Curva: {0} °C → {1}% PWM.", temperature, speed), false);
                    lastCurveTemperature = temperature;
                }
            }
            catch (Exception ex)
            {
                consecutiveReadFailures++;
                DiagnosticLogger.Log("ERROR", string.Format(
                    "Lettura hardware fallita ({0}/3): {1}", consecutiveReadFailures, ex));
                statusValue.ForeColor = Color.Firebrick;
                statusValue.Text = string.Format("Errore lettura hardware ({0}/3): {1}",
                    consecutiveReadFailures, ex.Message);
                if (consecutiveReadFailures >= 3)
                    ActivateFailSafe("Tre letture hardware consecutive sono fallite.", "-");
            }
            finally
            {
                refreshInProgress = false;
            }
        }

        private int SafeSpeed(int value)
        {
            if (!safeLimitsItem.Checked)
                return Clamp(value, 1, 100);
            return Clamp(value, 40, 99);
        }

        private void CurveEditorChanged(object sender, EventArgs e)
        {
            curvePoints = curveEditor.Points.ToList();
            curveText.Text = FanCurve.Serialize(curvePoints);
            SaveCurve();
        }

        private void ApplyCurveText(object sender, EventArgs e)
        {
            try
            {
                curvePoints = FanCurve.Parse(curveText.Text);
                curveEditor.Points = curvePoints;
                curveText.Text = FanCurve.Serialize(curvePoints);
                SaveCurve();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Curva non valida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ResetCurve(object sender, EventArgs e)
        {
            curvePoints = FanCurve.Parse(FanCurve.DefaultText);
            curveEditor.Points = curvePoints;
            curveText.Text = FanCurve.Serialize(curvePoints);
            SaveCurve();
        }

        private void SaveCurve()
        {
            Settings.Default.Curve = FanCurve.Serialize(curvePoints);
            Settings.Default.Save();
            lastCurveTemperature = int.MinValue;
            if (curveMode.Checked)
                RefreshHardware(true);
        }

        private void SettingsMenuChanged(object sender, EventArgs e)
        {
            if (loading)
                return;

            if (ReferenceEquals(sender, safeLimitsItem) && !safeLimitsItem.Checked)
            {
                DialogResult answer = MessageBox.Show(
                    "Disattivando i limiti sicuri sarà possibile comandare le ventole fino all'1%. " +
                    "Valori troppo bassi possono arrestare fisicamente una ventola.\n\nContinuare?",
                    "Kimera - impostazione potenzialmente pericolosa",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (answer != DialogResult.Yes)
                {
                    loading = true;
                    safeLimitsItem.Checked = true;
                    loading = false;
                }
            }

            if (ReferenceEquals(sender, releaseOnExitItem) && !releaseOnExitItem.Checked)
            {
                DialogResult answer = MessageBox.Show(
                    "Disattivando questa opzione, la chiusura normale di Kimera non restituirà " +
                    "il controllo delle ventole al firmware ASUS. L'ultimo test mode/PWM può " +
                    "rimanere attivo dopo l'uscita.\n\nContinuare?",
                    "Kimera - controllo ventole attivo dopo l'uscita",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (answer != DialogResult.Yes)
                {
                    loading = true;
                    releaseOnExitItem.Checked = true;
                    loading = false;
                }
            }

            Settings.Default.SafeLimits = safeLimitsItem.Checked;
            Settings.Default.ReleaseOnExit = releaseOnExitItem.Checked;
            Settings.Default.MinimizeToTray = minimizeToTrayItem.Checked;
            Settings.Default.StartMinimized = startMinimizedItem.Checked;
            Settings.Default.Save();
            if (manualMode.Checked) ApplyManual(true);
            if (curveMode.Checked) RefreshHardware(true);
        }

        private void NumericSettingsChanged(object sender, EventArgs e)
        {
            if (loading)
                return;
            Settings.Default.Hysteresis = (int)hysteresis.Value;
            Settings.Default.RefreshInterval = (int)interval.Value;
            Settings.Default.Save();
            refreshTimer.Interval = (int)interval.Value;
            lastCurveTemperature = int.MinValue;
        }

        private void StartWithWindowsChanged(object sender, EventArgs e)
        {
            if (loading)
                return;
            try
            {
                using (RegistryKey key = OpenInteractiveUserRunKey(true))
                {
                    if (key == null)
                        throw new InvalidOperationException(
                            "Il profilo dell'utente interattivo non è disponibile.");
                    if (startWithWindowsItem.Checked)
                        key.SetValue("AsusFanControlKimera", "\"" + Application.ExecutablePath + "\"");
                    else
                        key.DeleteValue("AsusFanControlKimera", false);
                }
            }
            catch (Exception ex)
            {
                DiagnosticLogger.Log("ERROR", "Avvio automatico non modificato: " + ex);
                MessageBox.Show("Impossibile modificare l'avvio automatico:\n" + ex.Message,
                    "Kimera", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                loading = true;
                startWithWindowsItem.Checked = IsStartupEnabled();
                loading = false;
            }
        }

        private void DebugChanged(object sender, EventArgs e)
        {
            if (loading)
                return;
            if (!debugItem.Checked)
                DiagnosticLogger.Log("DEBUG", "Registro diagnostico disabilitato.");
            Settings.Default.DebugEnabled = debugItem.Checked;
            Settings.Default.Save();
            DiagnosticLogger.Configure(debugItem.Checked);
            statusValue.ForeColor = Color.DimGray;
            statusValue.Text = debugItem.Checked
                ? "Debug attivo: " + DiagnosticLogger.LogPath
                : "Debug disattivato.";
        }

        private bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey key = OpenInteractiveUserRunKey(false))
                    return key != null && key.GetValue("AsusFanControlKimera") != null;
            }
            catch { return false; }
        }

        private RegistryKey OpenInteractiveUserRunKey(bool writable)
        {
            if (string.IsNullOrEmpty(Program.InteractiveUserSid))
                return null;
            string path = Program.InteractiveUserSid +
                @"\Software\Microsoft\Windows\CurrentVersion\Run";
            return Registry.Users.OpenSubKey(path, writable);
        }

        private void MainFormShown(object sender, EventArgs e)
        {
            if (Settings.Default.StartMinimized)
                MinimizeToTray();
        }

        private void MainFormResize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized && minimizeToTrayItem.Checked)
                MinimizeToTray();
        }

        private void MinimizeToTray()
        {
            Hide();
            trayIcon.Visible = true;
        }

        private void RestoreFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
            trayIcon.Visible = false;
        }

        private void MainFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!exiting && minimizeToTrayItem.Checked && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                MinimizeToTray();
                return;
            }

            refreshTimer.Stop();
            startupTimer.Stop();
            DiagnosticLogger.Log("SHUTDOWN", "Chiusura applicazione richiesta.");
            try
            {
                if (controller != null && releaseOnExitItem.Checked)
                {
                    controller.ReleaseControl();
                    DiagnosticLogger.Log("PWM",
                        "Controllo ventole restituito al firmware durante la chiusura.");
                }
            }
            catch (Exception ex)
            {
                DiagnosticLogger.Log("ERROR",
                    "Rilascio normale durante la chiusura fallito: " + ex);
                if (controller != null)
                    controller.TryEmergencyRelease();
            }
            if (controller != null)
                controller.Dispose();
            trayIcon.Visible = false;
            trayIcon.Dispose();
        }

        private void ResetSettings(object sender, EventArgs e)
        {
            if (MessageBox.Show("Ripristinare tutte le impostazioni?", "Kimera",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            Settings.Default.Reset();
            Settings.Default.Save();
            Application.Restart();
            exiting = true;
            Close();
        }

        private void ShowAbout(object sender, EventArgs e)
        {
            string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            MessageBox.Show("Asus Fan Control Kimera\nVersione " + version +
                "\n\nMotore compatibile con AsusFanControl.\nUI e gestione curva reimplementate.",
                "Informazioni", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static string TruncateTrayText(string text)
        {
            return text.Length <= 63 ? text : text.Substring(0, 63);
        }

        private void ResetSafetyCounters()
        {
            consecutiveReadFailures = 0;
            consecutiveInvalidTemperatures = 0;
            consecutiveZeroRpmSamples = 0;
        }

        private void ActivateFailSafe(string reason, string affectedFans)
        {
            if (failSafeActive || exiting || IsDisposed)
                return;

            failSafeActive = true;
            int pwmBeforeFailSafe = lastAppliedSpeed;
            int dutyBeforeFailSafe = controller == null ? -1 : controller.LastDuty;
            bool released = false;
            try
            {
                if (controller != null)
                {
                    controller.ReleaseControl();
                    released = true;
                }
            }
            catch
            {
                if (controller != null)
                    released = controller.TryEmergencyRelease();
            }

            lastAppliedSpeed = released ? 0 : lastAppliedSpeed;
            bool previousLoading = loading;
            loading = true;
            systemMode.Checked = true;
            loading = previousLoading;
            manualSpeed.Enabled = false;
            curveEditor.Enabled = false;
            curveText.Enabled = false;
            Settings.Default.Mode = "System";
            Settings.Default.Save();

            statusValue.ForeColor = Color.Firebrick;
            statusValue.Text = released
                ? "FAIL-SAFE: controllo restituito al firmware ASUS. " + reason
                : "FAIL-SAFE: rilascio al firmware non confermato. " + reason;
            string rpmSnapshot = lastObservedFanSpeeds == null ||
                lastObservedFanSpeeds.Count == 0
                ? "-"
                : string.Join(", ", lastObservedFanSpeeds.Select((rpm, index) =>
                    string.Format("F{0}:{1} RPM", index + 1, rpm)));
            string snapshot = string.Format(
                "temperatura={0} °C; {1}; PWM={2}%; duty={3}/255",
                lastObservedTemperature, rpmSnapshot, pwmBeforeFailSafe,
                dutyBeforeFailSafe);
            DiagnosticLogger.Log("FAILSAFE", string.Format(
                "cause={0}; affectedFans={1}; released={2}; {3}",
                reason, affectedFans, released, snapshot));
            trayIcon.Text = TruncateTrayText("Kimera - FAIL-SAFE");
            trayIcon.BalloonTipTitle = "Asus Fan Control Kimera - FAIL-SAFE";
            trayIcon.BalloonTipText = statusValue.Text;
            trayIcon.BalloonTipIcon = ToolTipIcon.Error;
            if (trayIcon.Visible)
                trayIcon.ShowBalloonTip(10000);

            if (failSafeDialog == null || failSafeDialog.IsDisposed)
            {
                failSafeDialog = new FailSafeDialog(
                    DateTime.Now, reason, affectedFans, snapshot, released);
                failSafeDialog.FormClosed += delegate { failSafeDialog = null; };
                failSafeDialog.Show();
                failSafeDialog.Activate();
            }
        }

        internal void HandleUnhandledException(Exception exception)
        {
            DiagnosticLogger.Log("ERROR", "Eccezione UI non gestita: " + exception);
            ActivateFailSafe("Errore imprevisto: " + exception.Message, "-");
        }

        private string CurrentModeName()
        {
            if (curveMode.Checked)
                return "Curve";
            if (manualMode.Checked)
                return "Manual";
            return "System";
        }

        internal void TryEmergencyReleaseHardware()
        {
            try
            {
                if (controller != null)
                    controller.TryEmergencyRelease();
            }
            catch { }
        }

        private sealed class HardwareSnapshot
        {
            internal readonly ulong Temperature;
            internal readonly IList<int> FanSpeeds;

            internal HardwareSnapshot(ulong temperature, IList<int> fanSpeeds)
            {
                Temperature = temperature;
                FanSpeeds = fanSpeeds;
            }
        }
    }
}
