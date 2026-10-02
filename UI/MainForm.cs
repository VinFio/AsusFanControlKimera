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
using AsusFanControlKimera.Localization;
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
        private readonly ComboBox curveProfileCombo = new ComboBox();
        private readonly Button saveCurveProfileButton = new Button();
        private readonly Button saveCurveProfileAsButton = new Button();
        private readonly Button curveProfileActionsButton = new Button();
        private readonly ContextMenuStrip curveProfileActionsMenu = new ContextMenuStrip();
        private readonly ToolStripMenuItem renameCurveProfileItem = new ToolStripMenuItem();
        private readonly ToolStripMenuItem deleteCurveProfileItem = new ToolStripMenuItem();
        private readonly NumericUpDown hysteresis = new NumericUpDown();
        private readonly NumericUpDown interval = new NumericUpDown();
        private readonly ToolStripMenuItem safeLimitsItem = new ToolStripMenuItem();
        private readonly ToolStripMenuItem releaseOnExitItem = new ToolStripMenuItem();
        private readonly ToolStripMenuItem minimizeToTrayItem = new ToolStripMenuItem();
        private readonly ToolStripMenuItem startMinimizedItem = new ToolStripMenuItem();
        private readonly ToolStripMenuItem startWithWindowsItem = new ToolStripMenuItem();
        private readonly ToolStripMenuItem debugItem = new ToolStripMenuItem();
        private readonly ToolStripMenuItem italianLanguageItem = new ToolStripMenuItem();
        private readonly ToolStripMenuItem englishLanguageItem = new ToolStripMenuItem();
        private readonly ToolStripMenuItem russianLanguageItem = new ToolStripMenuItem();
        private readonly NotifyIcon trayIcon = new NotifyIcon();
        private readonly Bitmap trayModeIndicator = CreateTrayModeIndicator();
        private readonly Timer refreshTimer = new Timer();
        private readonly Timer startupTimer = new Timer();
        private readonly IDictionary<Control, string> localizedControls =
            new Dictionary<Control, string>();
        private readonly IDictionary<ToolStripItem, string> localizedItems =
            new Dictionary<ToolStripItem, string>();
        private AsusFanController controller;
        private List<Point> curvePoints;
        private List<CurveProfile> curveProfiles = new List<CurveProfile>();
        private string activeCurveProfileName;
        private bool updatingCurveProfileUi;
        private int lastCurveTemperature = int.MinValue;
        private int lastAppliedSpeed = -1;
        private bool loading = true;
        private bool exiting;
        private bool refreshInProgress;
        private bool failSafeActive;
        private int consecutiveReadFailures;
        private int consecutiveInvalidTemperatures;
        private int consecutiveZeroRpmSamples;
        private static readonly TimeSpan ZeroRpmGracePeriod = TimeSpan.FromSeconds(5);
        private DateTime zeroRpmCheckNotBefore = DateTime.MinValue;
        private ulong lastObservedTemperature;
        private bool hasObservedTemperature;
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
            var options = LocalizeItem(new ToolStripMenuItem(), "Options");
            LocalizeItem(safeLimitsItem, "SafeLimits");
            LocalizeItem(releaseOnExitItem, "ReleaseOnExit");
            LocalizeItem(minimizeToTrayItem, "MinimizeToTray");
            LocalizeItem(startMinimizedItem, "StartMinimized");
            LocalizeItem(startWithWindowsItem, "StartWithWindows");
            LocalizeItem(debugItem, "Debug");
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
            var language = LocalizeItem(new ToolStripMenuItem(), "Language");
            LocalizeItem(italianLanguageItem, "Italian");
            LocalizeItem(englishLanguageItem, "English");
            LocalizeItem(russianLanguageItem, "Russian");
            italianLanguageItem.Click += delegate { ChangeLanguage(Strings.Italian); };
            englishLanguageItem.Click += delegate { ChangeLanguage(Strings.English); };
            russianLanguageItem.Click += delegate { ChangeLanguage(Strings.Russian); };
            language.DropDownItems.AddRange(new ToolStripItem[] {
                italianLanguageItem, englishLanguageItem, russianLanguageItem
            });
            options.DropDownItems.AddRange(new ToolStripItem[] {
                safeLimitsItem, releaseOnExitItem, minimizeToTrayItem,
                new ToolStripSeparator(), startMinimizedItem, startWithWindowsItem,
                new ToolStripSeparator(), language,
                new ToolStripSeparator(), debugItem,
                new ToolStripSeparator(), LocalizeItem(
                    new ToolStripMenuItem(null, null, ResetSettings), "ResetSettings")
            });
            var help = LocalizeItem(new ToolStripMenuItem(), "Help");
            help.DropDownItems.Add(LocalizeItem(
                new ToolStripMenuItem(null, null, ShowAbout), "About"));
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

            var modeBox = LocalizeControl(
                new GroupBox { Dock = DockStyle.Fill }, "ControlMode");
            var modeFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(8)
            };
            LocalizeControl(systemMode, "AsusSystemDisabled");
            LocalizeControl(manualMode, "Manual");
            LocalizeControl(curveMode, "TemperatureCurve");
            systemMode.AutoSize = manualMode.AutoSize = curveMode.AutoSize = true;
            systemMode.CheckedChanged += ModeChanged;
            manualMode.CheckedChanged += ModeChanged;
            curveMode.CheckedChanged += ModeChanged;
            modeFlow.Controls.AddRange(new Control[] { systemMode, manualMode, curveMode });
            modeBox.Controls.Add(modeFlow);
            top.Controls.Add(modeBox, 0, 0);

            var manualBox = LocalizeControl(
                new GroupBox { Dock = DockStyle.Fill }, "ManualSpeed");
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

            var statsBox = LocalizeControl(
                new GroupBox { Dock = DockStyle.Fill }, "HardwareStatus");
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
            stats.Controls.Add(LocalizeControl(new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft
            }, "CpuLabel"), 0, 0);
            stats.Controls.Add(temperatureValue, 1, 0);
            stats.Controls.Add(LocalizeControl(new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft
            }, "FansLabel"), 0, 1);
            stats.Controls.Add(rpmValue, 1, 1);
            var refreshButton = LocalizeControl(new Button
            {
                AutoSize = false,
                Size = new Size(92, 28),
                Margin = new Padding(0, 3, 0, 0)
            }, "Refresh");
            refreshButton.Click += delegate { RefreshHardware(true); };
            stats.Controls.Add(refreshButton, 0, 2);
            stats.SetColumnSpan(refreshButton, 2);
            statsBox.Controls.Add(stats);
            top.Controls.Add(statsBox, 2, 0);

            var curveBox = LocalizeControl(
                new GroupBox { Dock = DockStyle.Fill }, "CurveHelp");
            var curveArea = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(6, 2, 6, 6)
            };
            curveArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            curveArea.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            curveArea.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var curveProfileBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(2, 5, 0, 0)
            };
            var curveProfileLabel = LocalizeControl(new Label
            {
                AutoSize = true,
                Margin = new Padding(0, 7, 7, 0)
            }, "CurveProfile");
            curveProfileCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            curveProfileCombo.Width = 210;
            curveProfileCombo.Margin = new Padding(0, 3, 8, 0);
            curveProfileCombo.SelectedIndexChanged += CurveProfileSelectionChanged;
            LocalizeControl(saveCurveProfileButton, "SaveProfile");
            saveCurveProfileButton.Size = new Size(82, 28);
            saveCurveProfileButton.Margin = new Padding(0, 2, 6, 0);
            saveCurveProfileButton.Click += SaveCurveProfile;
            LocalizeControl(saveCurveProfileAsButton, "SaveProfileAs");
            saveCurveProfileAsButton.Size = new Size(124, 28);
            saveCurveProfileAsButton.Margin = new Padding(0, 2, 6, 0);
            saveCurveProfileAsButton.Click += SaveCurveProfileAs;
            curveProfileActionsButton.Text = "\u2026";
            curveProfileActionsButton.Size = new Size(38, 28);
            curveProfileActionsButton.Margin = new Padding(0, 2, 0, 0);
            curveProfileActionsButton.Click += ShowCurveProfileActions;
            LocalizeItem(renameCurveProfileItem, "RenameProfile");
            LocalizeItem(deleteCurveProfileItem, "DeleteProfile");
            renameCurveProfileItem.Click += RenameCurveProfile;
            deleteCurveProfileItem.Click += DeleteCurveProfile;
            curveProfileActionsMenu.Items.Add(renameCurveProfileItem);
            curveProfileActionsMenu.Items.Add(deleteCurveProfileItem);
            curveProfileBar.Controls.Add(curveProfileLabel);
            curveProfileBar.Controls.Add(curveProfileCombo);
            curveProfileBar.Controls.Add(saveCurveProfileButton);
            curveProfileBar.Controls.Add(saveCurveProfileAsButton);
            curveProfileBar.Controls.Add(curveProfileActionsButton);

            curveEditor.Dock = DockStyle.Fill;
            curveEditor.CurveChanged += CurveEditorChanged;
            curveArea.Controls.Add(curveProfileBar, 0, 0);
            curveArea.Controls.Add(curveEditor, 0, 1);
            curveBox.Controls.Add(curveArea);
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
            curveText.TextChanged += CurveTextChanged;
            var applyCurve = LocalizeControl(
                new Button { Dock = DockStyle.Fill }, "Apply");
            var resetCurve = LocalizeControl(
                new Button { Dock = DockStyle.Fill }, "Default");
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
            resetCurve.Size = new Size(120, 34);
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
            var hysteresisLabel = LocalizeControl(new Label
            {
                AutoSize = true,
                Margin = new Padding(0, 5, 8, 0)
            }, "Hysteresis");
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
            var intervalLabel = LocalizeControl(new Label
            {
                AutoSize = true,
                Margin = new Padding(0, 5, 8, 0)
            }, "Interval");
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
            var trayOpen = LocalizeItem(new ToolStripMenuItem(), "TrayOpen");
            trayOpen.Click += delegate { RestoreFromTray(); };
            var traySystem = LocalizeItem(new ToolStripMenuItem(), "TrayAsusSystem");
            traySystem.Click += delegate { systemMode.Checked = true; };
            var trayCurve = LocalizeItem(new ToolStripMenuItem(), "TrayTemperatureCurve");
            trayCurve.Click += delegate { curveMode.Checked = true; };
            trayMenu.Opening += delegate
            {
                traySystem.Image = systemMode.Checked ? trayModeIndicator : null;
                trayCurve.Image = curveMode.Checked ? trayModeIndicator : null;
            };
            trayMenu.Items.Add(trayOpen);
            trayMenu.Items.Add(traySystem);
            trayMenu.Items.Add(trayCurve);
            trayMenu.Items.Add(new ToolStripSeparator());
            var trayExit = LocalizeItem(new ToolStripMenuItem(), "TrayExit");
            trayExit.Click += delegate { exiting = true; Close(); };
            trayMenu.Items.Add(trayExit);
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
            Strings.SetLanguage(Settings.Default.Language);
            ApplyLocalizedText();
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
            curveProfiles = CurveProfileStore.Deserialize(Settings.Default.CurveProfiles).ToList();
            CurveProfile activeProfile = FindCurveProfile(Settings.Default.ActiveCurveProfile);
            activeCurveProfileName = activeProfile == null ? null : activeProfile.Name;
            if (Settings.Default.ActiveCurveProfile != (activeCurveProfileName ?? string.Empty))
            {
                Settings.Default.ActiveCurveProfile = activeCurveProfileName ?? string.Empty;
                Settings.Default.Save();
            }
            RefreshCurveProfileUi();

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
                statusValue.Text = Strings.Get("EngineWaiting");
                SetControlsAvailable(false);
                refreshTimer.Interval = (int)interval.Value;
                startupTimer.Start();
            }
            catch (Exception ex)
            {
                DiagnosticLogger.Log("ERROR", "Inizializzazione hardware fallita: " + ex);
                statusValue.Text = Strings.Format("HardwareUnavailable", ex.Message);
                statusValue.ForeColor = Color.Firebrick;
                // Mostra Sistema ASUS senza sovrascrivere la modalità salvata:
                // il guasto potrebbe essere temporaneo.
                loading = true;
                systemMode.Checked = true;
                loading = false;
                curveText.Enabled = false;
                SetControlsAvailable(false);
                MessageBox.Show(Strings.Format("HardwareInitFailedText", ex.Message),
                    Strings.Get("HardwareUnavailableTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                statusValue.Text = Strings.Format("HardwareReady", fanCount);
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
                statusValue.Text = Strings.Format("HardwareNotReady", ex.Message);
                MessageBox.Show(Strings.Format("HardwareNotReadyText", ex.Message),
                    Strings.Get("FansNotDetectedTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                ApplySpeed(0, Strings.Get("SystemControlReleased"), true);
            }
            else if (manualMode.Checked)
            {
                Settings.Default.Mode = "Manual";
                ApplyManual(true);
            }
            else if (curveMode.Checked)
            {
                Settings.Default.Mode = "Curve";
                statusValue.Text = Strings.Get("CurveWaiting");
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
            ApplySpeed(speed, Strings.Format("ManualStatus", speed), force);
        }

        private bool ApplySpeed(int speed, string status, bool force)
        {
            if (controller == null)
                return false;
            if (!force && lastAppliedSpeed == speed)
                return true;
            try
            {
                controller.SetAllFans(speed);
                // Solo partendo da ventole forse ferme (firmware, stato ignoto o
                // velocità sotto il 40%): le piccole variazioni della curva non
                // devono rimandare continuamente il controllo 0 RPM.
                if (speed >= 40 && lastAppliedSpeed < 40)
                    zeroRpmCheckNotBefore = DateTime.UtcNow + ZeroRpmGracePeriod;
                lastAppliedSpeed = speed;
                DiagnosticLogger.Log("PWM", string.Format(
                    "mode={0}; requested={1}%; duty={2}/255; fans={3}",
                    CurrentModeName(), speed, controller.LastDuty, controller.LastFanCount));
                statusValue.ForeColor = Color.DimGray;
                statusValue.Text = Strings.Format("CommandSent",
                    status, controller.LastFanCount, controller.LastDuty);
                UpdateTrayText();
                return true;
            }
            catch (Exception ex)
            {
                // SetAllFans può aver già restituito le ventole al firmware:
                // lo stato precedente non è più affidabile, quindi il prossimo
                // comando deve essere inviato comunque.
                lastAppliedSpeed = -1;
                DiagnosticLogger.Log("ERROR", "Comando ventole fallito: " + ex);
                statusValue.ForeColor = Color.Firebrick;
                statusValue.Text = Strings.Format("FanControlError", ex.Message);
                UpdateTrayText();
                return false;
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
                hasObservedTemperature = true;
                lastObservedFanSpeeds = speeds.ToList();
                consecutiveReadFailures = 0;
                temperatureValue.Text = rawTemperature + " °C";
                rpmValue.Text = string.Join(" / ", speeds.Select((rpm, i) =>
                    string.Format("F{0}: {1}", i + 1, rpm))) + " RPM";
                UpdateTrayText();
                DiagnosticLogger.LogSnapshot(CurrentModeName(), rawTemperature, speeds,
                    lastAppliedSpeed, controller.LastDuty);

                int temperature = rawTemperature > int.MaxValue ? int.MaxValue : (int)rawTemperature;
                bool controllingFans = !systemMode.Checked;
                // Dopo un cambio di velocità le ventole ferme hanno bisogno di
                // qualche secondo per ripartire: niente conteggio 0 RPM in quel periodo.
                if (controllingFans && lastAppliedSpeed >= 40 &&
                    DateTime.UtcNow >= zeroRpmCheckNotBefore &&
                    speeds.Count > 0 && speeds.Any(rpm => rpm <= 0))
                    consecutiveZeroRpmSamples++;
                else
                    consecutiveZeroRpmSamples = 0;

                if (consecutiveZeroRpmSamples >= 3)
                {
                    string affectedFans = string.Join(", ", speeds
                        .Select((rpm, index) => new { rpm, index })
                        .Where(item => item.rpm <= 0)
                        .Select(item => "F" + (item.index + 1)));
                    ActivateFailSafe(Strings.Get("ZeroRpmReason"), affectedFans);
                    return;
                }

                if (temperature < 10 || temperature > 115)
                {
                    consecutiveInvalidTemperatures++;
                    statusValue.ForeColor = Color.Firebrick;
                    statusValue.Text = Strings.Format("InvalidTemperatureStatus",
                        temperature, consecutiveInvalidTemperatures);
                    if (controllingFans && consecutiveInvalidTemperatures >= 2)
                        ActivateFailSafe(Strings.Get("InvalidTemperatureReason"), "-");
                    return;
                }
                consecutiveInvalidTemperatures = 0;

                if (curveMode.Checked &&
                    (forceCurve || lastCurveTemperature == int.MinValue ||
                     Math.Abs(temperature - lastCurveTemperature) >= (int)hysteresis.Value))
                {
                    int speed = SafeSpeed(FanCurve.Evaluate(curvePoints, temperature));
                    // Se il comando fallisce si riprova alla lettura successiva.
                    if (ApplySpeed(speed, Strings.Format("CurveStatus", temperature, speed), false))
                        lastCurveTemperature = temperature;
                }
            }
            catch (Exception ex)
            {
                consecutiveReadFailures++;
                DiagnosticLogger.Log("ERROR", string.Format(
                    "Lettura hardware fallita ({0}/3): {1}", consecutiveReadFailures, ex));
                statusValue.ForeColor = Color.Firebrick;
                statusValue.Text = Strings.Format("HardwareReadError",
                    consecutiveReadFailures, ex.Message);
                if (consecutiveReadFailures >= 3 && !systemMode.Checked)
                    ActivateFailSafe(Strings.Get("HardwareReadReason"), "-");
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
            List<Point> edited;
            try
            {
                // Una curva non valida verrebbe scartata al riavvio insieme
                // all'eventuale profilo che la contiene.
                edited = FanCurve.Parse(FanCurve.Serialize(curveEditor.Points));
            }
            catch (FormatException)
            {
                curveEditor.Points = curvePoints;
                return;
            }
            curvePoints = edited;
            curveText.Text = FanCurve.Serialize(curvePoints);
            SaveCurve();
        }

        private void ApplyCurveText(object sender, EventArgs e)
        {
            CommitCurveText();
        }

        private bool CommitCurveText()
        {
            if (CurveTextMatchesCurrent())
                return true;
            try
            {
                curvePoints = FanCurve.Parse(curveText.Text);
                curveEditor.Points = curvePoints;
                curveText.Text = FanCurve.Serialize(curvePoints);
                SaveCurve();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Strings.Get("InvalidCurveTitle"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        private void CurveTextChanged(object sender, EventArgs e)
        {
            if (!loading)
                RefreshCurveProfileUi();
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
            RefreshCurveProfileUi();
            lastCurveTemperature = int.MinValue;
            if (curveMode.Checked)
                RefreshHardware(true);
        }

        private void CurveProfileSelectionChanged(object sender, EventArgs e)
        {
            if (updatingCurveProfileUi)
                return;

            var option = curveProfileCombo.SelectedItem as CurveProfileOption;
            if (option == null || string.IsNullOrEmpty(option.ProfileName) ||
                string.Equals(option.ProfileName, activeCurveProfileName,
                    StringComparison.OrdinalIgnoreCase))
                return;

            CurveProfile target = FindCurveProfile(option.ProfileName);
            if (target == null)
            {
                RefreshCurveProfileUi();
                return;
            }

            if (FindCurveProfile(activeCurveProfileName) == null &&
                CurveProfileMatchesCurrent(target))
            {
                activeCurveProfileName = target.Name;
                PersistCurveProfiles();
                RefreshCurveProfileUi();
                return;
            }

            if (!ConfirmLeavingCurrentCurve())
            {
                RefreshCurveProfileUi();
                return;
            }

            ApplyCurveProfile(target);
        }

        private void SaveCurveProfile(object sender, EventArgs e)
        {
            SaveActiveCurveProfile(true);
        }

        private void SaveCurveProfileAs(object sender, EventArgs e)
        {
            SaveCurrentCurveAsProfile();
        }

        private bool SaveActiveCurveProfile(bool reportStatus)
        {
            CurveProfile profile = FindCurveProfile(activeCurveProfileName);
            if (profile == null || curvePoints == null || !CommitCurveText())
                return false;

            CurveProfile current = CaptureCurrentCurveProfile(profile.Name);
            profile.Curve = current.Curve;
            profile.Hysteresis = current.Hysteresis;
            profile.RefreshInterval = current.RefreshInterval;
            PersistCurveProfiles();
            RefreshCurveProfileUi();
            DiagnosticLogger.Log("PROFILE", "Profilo curva aggiornato: " + profile.Name);
            if (reportStatus)
            {
                statusValue.ForeColor = Color.DimGray;
                statusValue.Text = Strings.Format("ProfileSavedStatus", profile.Name);
            }
            return true;
        }

        private bool SaveCurrentCurveAsProfile()
        {
            if (curvePoints == null || !CommitCurveText())
                return false;

            string name;
            if (!TryGetCurveProfileName("SaveProfileAsTitle", "ProfileNamePrompt",
                string.Empty, null, out name))
                return false;

            curveProfiles.Add(CaptureCurrentCurveProfile(name));
            activeCurveProfileName = name;
            PersistCurveProfiles();
            RefreshCurveProfileUi();
            DiagnosticLogger.Log("PROFILE", "Profilo curva creato: " + name);
            statusValue.ForeColor = Color.DimGray;
            statusValue.Text = Strings.Format("ProfileSavedStatus", name);
            return true;
        }

        private void ShowCurveProfileActions(object sender, EventArgs e)
        {
            bool hasProfile = FindCurveProfile(activeCurveProfileName) != null;
            renameCurveProfileItem.Enabled = hasProfile;
            deleteCurveProfileItem.Enabled = hasProfile;
            if (hasProfile)
                curveProfileActionsMenu.Show(curveProfileActionsButton,
                    new Point(0, curveProfileActionsButton.Height));
        }

        private void RenameCurveProfile(object sender, EventArgs e)
        {
            CurveProfile profile = FindCurveProfile(activeCurveProfileName);
            if (profile == null)
                return;

            string name;
            if (!TryGetCurveProfileName("RenameProfileTitle", "ProfileNamePrompt",
                profile.Name, profile.Name, out name) ||
                string.Equals(name, profile.Name, StringComparison.Ordinal))
                return;

            string oldName = profile.Name;
            profile.Name = name;
            activeCurveProfileName = name;
            PersistCurveProfiles();
            RefreshCurveProfileUi();
            DiagnosticLogger.Log("PROFILE", string.Format(
                "Profilo curva rinominato: {0} -> {1}", oldName, name));
        }

        private void DeleteCurveProfile(object sender, EventArgs e)
        {
            CurveProfile profile = FindCurveProfile(activeCurveProfileName);
            if (profile == null || MessageBox.Show(
                Strings.Format("DeleteProfilePrompt", profile.Name),
                Strings.Get("DeleteProfileTitle"), MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            curveProfiles.Remove(profile);
            activeCurveProfileName = null;
            PersistCurveProfiles();
            RefreshCurveProfileUi();
            DiagnosticLogger.Log("PROFILE", "Profilo curva eliminato: " + profile.Name);
            statusValue.ForeColor = Color.DimGray;
            statusValue.Text = Strings.Format("ProfileDeletedStatus", profile.Name);
        }

        private bool ConfirmLeavingCurrentCurve()
        {
            CurveProfile activeProfile = FindCurveProfile(activeCurveProfileName);
            if (activeProfile != null && CurveProfileMatchesCurrent(activeProfile))
                return true;

            string message = activeProfile == null
                ? Strings.Get("UnsavedCurrentCurvePrompt")
                : Strings.Format("UnsavedProfilePrompt", activeProfile.Name);
            DialogResult answer = MessageBox.Show(message,
                Strings.Get("UnsavedProfileTitle"), MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);
            if (answer == DialogResult.Cancel)
                return false;
            if (answer == DialogResult.No)
                return true;
            return activeProfile == null
                ? SaveCurrentCurveAsProfile()
                : SaveActiveCurveProfile(false);
        }

        private void ApplyCurveProfile(CurveProfile profile)
        {
            bool previousLoading = loading;
            loading = true;
            try
            {
                curvePoints = FanCurve.Parse(profile.Curve);
                curveEditor.Points = curvePoints;
                curveText.Text = FanCurve.Serialize(curvePoints);
                hysteresis.Value = Clamp(profile.Hysteresis, 0, 15);
                interval.Value = Clamp(profile.RefreshInterval, 500, 10000);
                activeCurveProfileName = profile.Name;
                Settings.Default.Curve = curveText.Text;
                Settings.Default.Hysteresis = (int)hysteresis.Value;
                Settings.Default.RefreshInterval = (int)interval.Value;
                Settings.Default.ActiveCurveProfile = activeCurveProfileName;
                Settings.Default.Save();
                refreshTimer.Interval = (int)interval.Value;
            }
            finally
            {
                loading = previousLoading;
            }

            lastCurveTemperature = int.MinValue;
            RefreshCurveProfileUi();
            DiagnosticLogger.Log("PROFILE", "Profilo curva caricato: " + profile.Name);
            if (curveMode.Checked)
                RefreshHardware(true);
        }

        private CurveProfile CaptureCurrentCurveProfile(string name)
        {
            return new CurveProfile
            {
                Name = name,
                Curve = FanCurve.Serialize(curvePoints),
                Hysteresis = (int)hysteresis.Value,
                RefreshInterval = (int)interval.Value
            };
        }

        private CurveProfile FindCurveProfile(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            return curveProfiles.FirstOrDefault(profile => string.Equals(
                profile.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private bool CurveProfileMatchesCurrent(CurveProfile profile)
        {
            return profile != null && curvePoints != null &&
                CurveTextMatchesCurrent() &&
                string.Equals(profile.Curve, FanCurve.Serialize(curvePoints),
                    StringComparison.Ordinal) &&
                profile.Hysteresis == (int)hysteresis.Value &&
                profile.RefreshInterval == (int)interval.Value;
        }

        private bool CurveTextMatchesCurrent()
        {
            return curvePoints != null && string.Equals(curveText.Text,
                FanCurve.Serialize(curvePoints), StringComparison.Ordinal);
        }

        private void PersistCurveProfiles()
        {
            Settings.Default.CurveProfiles = CurveProfileStore.Serialize(curveProfiles);
            Settings.Default.ActiveCurveProfile = activeCurveProfileName ?? string.Empty;
            Settings.Default.Save();
        }

        private void RefreshCurveProfileUi()
        {
            if (curveProfileCombo.IsDisposed)
                return;

            bool previousUpdating = updatingCurveProfileUi;
            updatingCurveProfileUi = true;
            curveProfileCombo.BeginUpdate();
            try
            {
                curveProfileCombo.Items.Clear();
                CurveProfile activeProfile = FindCurveProfile(activeCurveProfileName);
                int selectedIndex = -1;
                if (activeProfile == null)
                {
                    curveProfileCombo.Items.Add(new CurveProfileOption(
                        null, Strings.Get("CurrentCurve")));
                    selectedIndex = 0;
                }

                foreach (CurveProfile profile in curveProfiles)
                {
                    bool active = activeProfile != null && string.Equals(
                        activeProfile.Name, profile.Name, StringComparison.OrdinalIgnoreCase);
                    string label = profile.Name +
                        (active && !CurveProfileMatchesCurrent(profile) ? " *" : string.Empty);
                    int index = curveProfileCombo.Items.Add(
                        new CurveProfileOption(profile.Name, label));
                    if (active)
                        selectedIndex = index;
                }
                curveProfileCombo.SelectedIndex = selectedIndex;
                bool hasActiveProfile = activeProfile != null;
                saveCurveProfileButton.Enabled = hasActiveProfile;
                curveProfileActionsButton.Enabled = hasActiveProfile;
            }
            finally
            {
                curveProfileCombo.EndUpdate();
                updatingCurveProfileUi = previousUpdating;
            }
        }

        private bool TryGetCurveProfileName(string titleKey, string promptKey,
            string initialName, string ignoredExistingName, out string name)
        {
            name = null;
            while (true)
            {
                string candidate;
                if (!ShowCurveProfileNameDialog(Strings.Get(titleKey),
                    Strings.Get(promptKey), initialName, out candidate))
                    return false;

                candidate = candidate.Trim();
                if (!CurveProfileStore.IsValidName(candidate))
                {
                    MessageBox.Show(Strings.Format("InvalidProfileName",
                        CurveProfileStore.MaximumNameLength), Strings.Get("InvalidProfileTitle"),
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    initialName = candidate;
                    continue;
                }

                CurveProfile existing = FindCurveProfile(candidate);
                if (existing != null && !string.Equals(existing.Name,
                    ignoredExistingName, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(Strings.Format("DuplicateProfileName", candidate),
                        Strings.Get("InvalidProfileTitle"), MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    initialName = candidate;
                    continue;
                }

                name = candidate;
                return true;
            }
        }

        private bool ShowCurveProfileNameDialog(string title, string prompt,
            string initialName, out string name)
        {
            using (var dialog = new Form())
            using (var label = new Label())
            using (var textBox = new TextBox())
            using (var okButton = new Button())
            using (var cancelButton = new Button())
            {
                dialog.Text = title;
                dialog.Font = Font;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.ClientSize = new Size(430, 130);
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;
                dialog.ShowInTaskbar = false;
                label.Text = prompt;
                label.SetBounds(12, 12, 406, 24);
                textBox.Text = initialName ?? string.Empty;
                textBox.MaxLength = CurveProfileStore.MaximumNameLength;
                textBox.SetBounds(12, 40, 406, 24);
                okButton.Text = Strings.Get("OK");
                okButton.DialogResult = DialogResult.OK;
                okButton.SetBounds(246, 88, 80, 28);
                cancelButton.Text = Strings.Get("Cancel");
                cancelButton.DialogResult = DialogResult.Cancel;
                cancelButton.SetBounds(338, 88, 80, 28);
                dialog.Controls.AddRange(new Control[]
                {
                    label, textBox, okButton, cancelButton
                });
                dialog.AcceptButton = okButton;
                dialog.CancelButton = cancelButton;
                dialog.Shown += delegate
                {
                    textBox.Focus();
                    textBox.SelectAll();
                };
                bool accepted = dialog.ShowDialog(this) == DialogResult.OK;
                name = accepted ? textBox.Text : null;
                return accepted;
            }
        }

        private void SettingsMenuChanged(object sender, EventArgs e)
        {
            if (loading)
                return;

            if (ReferenceEquals(sender, safeLimitsItem) && !safeLimitsItem.Checked)
            {
                DialogResult answer = MessageBox.Show(
                    Strings.Get("UnsafeLimitsText"), Strings.Get("UnsafeLimitsTitle"),
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
                    Strings.Get("ReleaseDisabledText"), Strings.Get("ReleaseDisabledTitle"),
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
            RefreshCurveProfileUi();
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
                            Strings.Get("InteractiveProfileUnavailable"));
                    if (startWithWindowsItem.Checked)
                        key.SetValue("AsusFanControlKimera", "\"" + Application.ExecutablePath + "\"");
                    else
                        key.DeleteValue("AsusFanControlKimera", false);
                }
            }
            catch (Exception ex)
            {
                DiagnosticLogger.Log("ERROR", "Avvio automatico non modificato: " + ex);
                MessageBox.Show(Strings.Format("StartupChangeFailed", ex.Message),
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
                ? Strings.Format("DebugEnabled", DiagnosticLogger.LogPath)
                : Strings.Get("DebugDisabled");
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

            // Blocca anche le letture hardware ancora in corso in background.
            exiting = true;
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
            trayModeIndicator.Dispose();
            curveProfileActionsMenu.Dispose();
        }

        private void ResetSettings(object sender, EventArgs e)
        {
            if (MessageBox.Show(Strings.Get("ResetPrompt"), "Kimera",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            Settings.Default.Reset();
            // Evita che Upgrade() reimporti le impostazioni di una versione precedente.
            Settings.Default.UpgradeRequired = false;
            Settings.Default.InteractiveUserSid = Program.InteractiveUserSid ?? string.Empty;
            Settings.Default.Save();
            exiting = true;
            Close();
            try
            {
                Program.StartReplacementInstance();
            }
            catch (Exception ex)
            {
                DiagnosticLogger.Log("ERROR", "Riavvio dopo il ripristino fallito: " + ex);
                MessageBox.Show(Strings.Format("RestartFailed", ex.Message), "Kimera",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ShowAbout(object sender, EventArgs e)
        {
            string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            MessageBox.Show(Strings.Format("AboutText", version), Strings.Get("About"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private T LocalizeControl<T>(T control, string key) where T : Control
        {
            localizedControls[control] = key;
            control.Text = Strings.Get(key);
            return control;
        }

        private T LocalizeItem<T>(T item, string key) where T : ToolStripItem
        {
            localizedItems[item] = key;
            item.Text = Strings.Get(key);
            return item;
        }

        private void ApplyLocalizedText()
        {
            foreach (KeyValuePair<Control, string> entry in localizedControls)
                entry.Key.Text = Strings.Get(entry.Value);
            foreach (KeyValuePair<ToolStripItem, string> entry in localizedItems)
                entry.Key.Text = Strings.Get(entry.Value);

            italianLanguageItem.Checked = Strings.CurrentLanguage == Strings.Italian;
            englishLanguageItem.Checked = Strings.CurrentLanguage == Strings.English;
            russianLanguageItem.Checked = Strings.CurrentLanguage == Strings.Russian;
            curveEditor.Invalidate();
            if (curvePoints != null)
                RefreshCurveProfileUi();
            PerformLayout();
        }

        private void ChangeLanguage(string language)
        {
            if (Strings.CurrentLanguage == language)
                return;

            Strings.SetLanguage(language);
            Settings.Default.Language = Strings.CurrentLanguage;
            Settings.Default.Save();
            ApplyLocalizedText();
            statusValue.ForeColor = Color.DimGray;
            statusValue.Text = Strings.Get("LanguageChanged");
            if (!failSafeActive)
                UpdateTrayText();
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static Bitmap CreateTrayModeIndicator()
        {
            var indicator = new Bitmap(16, 16);
            using (Graphics graphics = Graphics.FromImage(indicator))
            {
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.FillEllipse(Brushes.Black, 4, 4, 8, 8);
            }
            return indicator;
        }

        private void UpdateTrayText()
        {
            if (failSafeActive)
                return;

            string text = lastAppliedSpeed == 0
                ? Strings.Get("TraySystem")
                : lastAppliedSpeed > 0
                    ? string.Format("Kimera - {0}% PWM", lastAppliedSpeed)
                    : "Asus Fan Control Kimera";
            if (hasObservedTemperature)
                text += string.Format(" - {0} °C", lastObservedTemperature);

            trayIcon.Text = TruncateTrayText(text);
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
                ? Strings.Format("FailSafeReleasedStatus", reason)
                : Strings.Format("FailSafeUnconfirmedStatus", reason);
            string rpmSnapshot = lastObservedFanSpeeds == null ||
                lastObservedFanSpeeds.Count == 0
                ? "-"
                : string.Join(", ", lastObservedFanSpeeds.Select((rpm, index) =>
                    string.Format("F{0}:{1} RPM", index + 1, rpm)));
            string snapshot = Strings.Format("Snapshot",
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
                failSafeDialog.FormClosed += delegate
                {
                    failSafeDialog = null;
                    // Avviso confermato: Kimera è in Sistema ASUS e il testo
                    // dell'icona può tornare a mostrare lo stato reale.
                    failSafeActive = false;
                    if (!exiting && !IsDisposed)
                        UpdateTrayText();
                };
                failSafeDialog.Show();
                failSafeDialog.Activate();
            }
        }

        internal void HandleUnhandledException(Exception exception)
        {
            DiagnosticLogger.Log("ERROR", "Eccezione UI non gestita: " + exception);
            ActivateFailSafe(Strings.Format("UnexpectedError", exception.Message), "-");
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

        private sealed class CurveProfileOption
        {
            internal readonly string ProfileName;
            private readonly string displayText;

            internal CurveProfileOption(string profileName, string displayText)
            {
                ProfileName = profileName;
                this.displayText = displayText;
            }

            public override string ToString()
            {
                return displayText;
            }
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
