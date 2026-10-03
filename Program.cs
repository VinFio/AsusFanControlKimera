using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
using AsusFanControlKimera.Diagnostics;
using AsusFanControlKimera.Localization;
using AsusFanControlKimera.Properties;
using AsusFanControlKimera.Security;
using AsusFanControlKimera.Setup;
using AsusFanControlKimera.UI;

namespace AsusFanControlKimera
{
    internal static class Program
    {
        private const string RestartArgument = "--restart";

        internal static string InteractiveUserSid { get; private set; }

        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Strings.SetLanguage(Settings.Default.Language);
            if (!EnsureSystemIdentity(args))
                return;

            using (var mutex = new Mutex(false, @"Global\AsusFanControlKimera.SingleInstance"))
            {
                // Dopo "Ripristina impostazioni" la nuova istanza attende che la
                // precedente abbia rilasciato le ventole e sia terminata.
                bool restarting = args.Any(argument =>
                    string.Equals(argument, RestartArgument, StringComparison.OrdinalIgnoreCase));
                if (!AcquireSingleInstance(mutex, restarting ? 15000 : 0))
                {
                    MessageBox.Show(Strings.Get("AlreadyRunning"), "Kimera",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                try
                {
                    RunApplication();
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
        }

        private static void RunApplication()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            MainForm mainForm = null;
            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
            {
                if (mainForm != null)
                    mainForm.HandleUnhandledException(e.Exception);
                MessageBox.Show(e.Exception.Message, Strings.Get("ErrorTitle"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate
                (object sender, UnhandledExceptionEventArgs e)
            {
                DiagnosticLogger.Log("ERROR",
                    "Eccezione fatale: " + (e.ExceptionObject ?? "sconosciuta"));
                if (mainForm != null)
                    mainForm.TryEmergencyReleaseHardware();
            };

            // Permette all'installazione di un aggiornamento di chiudere questa
            // istanza in modo ordinato (ventole restituite al firmware).
            using (EventWaitHandle shutdown = CreateShutdownEvent())
            {
                RegisteredWaitHandle registration = shutdown == null ? null :
                    ThreadPool.RegisterWaitForSingleObject(shutdown, delegate
                    {
                        MainForm form = mainForm;
                        if (form != null)
                            form.RequestShutdown();
                    }, null, Timeout.Infinite, true);
                try
                {
                    mainForm = new MainForm();
                    Application.Run(mainForm);
                }
                finally
                {
                    if (registration != null)
                        registration.Unregister(null);
                }
            }
        }

        private static EventWaitHandle CreateShutdownEvent()
        {
            try
            {
                // Solo SYSTEM e gli amministratori possono chiedere la chiusura.
                var security = new EventWaitHandleSecurity();
                security.AddAccessRule(new EventWaitHandleAccessRule(FileSystemTrust.SystemSid,
                    EventWaitHandleRights.FullControl, AccessControlType.Allow));
                security.AddAccessRule(new EventWaitHandleAccessRule(FileSystemTrust.AdministratorsSid,
                    EventWaitHandleRights.FullControl, AccessControlType.Allow));
                bool created;
                return new EventWaitHandle(false, EventResetMode.AutoReset,
                    Installer.ShutdownEventName, out created, security);
            }
            catch (Exception ex)
            {
                DiagnosticLogger.Log("ERROR", "Evento di chiusura non disponibile: " + ex);
                return null;
            }
        }

        private static bool AcquireSingleInstance(Mutex mutex, int timeout)
        {
            try
            {
                return mutex.WaitOne(timeout);
            }
            catch (AbandonedMutexException)
            {
                // L'istanza precedente è terminata senza rilasciare il mutex:
                // la proprietà passa comunque a questo processo.
                return true;
            }
        }

        /// <summary>
        /// Avvia una nuova istanza con lo stesso token (SYSTEM, sessione interattiva).
        /// La nuova istanza attende la chiusura di quella corrente.
        /// </summary>
        internal static void StartReplacementInstance()
        {
            StartReplacementInstance(Application.ExecutablePath);
        }

        internal static void StartReplacementInstance(string executable)
        {
            string arguments = RestartArgument;
            if (!string.IsNullOrEmpty(InteractiveUserSid))
                arguments += " --interactive-user-sid=\"" + InteractiveUserSid + "\"";
            Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                WorkingDirectory = Path.GetDirectoryName(executable),
                UseShellExecute = false
            });
        }

        private static bool EnsureSystemIdentity(string[] args)
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                if (identity.IsSystem)
                {
                    InteractiveUserSid = ReadInteractiveUserSid(args);
                    if (string.IsNullOrEmpty(InteractiveUserSid))
                        InteractiveUserSid = NormalizeSid(Settings.Default.InteractiveUserSid);
                    if (!string.IsNullOrEmpty(InteractiveUserSid))
                    {
                        Settings.Default.InteractiveUserSid = InteractiveUserSid;
                        Settings.Default.Save();
                    }
                    return true;
                }

                InteractiveUserSid = identity.User == null
                    ? null
                    : NormalizeSid(identity.User.Value);

                var principal = new WindowsPrincipal(identity);
                if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = Application.ExecutablePath,
                            UseShellExecute = true,
                            Verb = "runas",
                            WorkingDirectory = Application.StartupPath
                        });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(Strings.Format("ElevationRequired", ex.Message),
                            "Kimera", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    return false;
                }
            }

            RunElevatedStage();
            return false;
        }

        // Fase con privilegi di amministratore: installazione o aggiornamento se
        // necessari, quindi avvio come SYSTEM tramite PsExec.
        private static void RunElevatedStage()
        {
            string target = Application.ExecutablePath;
            Version installedVersion = Installer.InstalledVersion;
            bool updateAvailable = installedVersion != null && installedVersion < Installer.CurrentVersion;
            // Una cartella accettata con "Esegui da qui" non chiede più nulla,
            // salvo quando questa copia può aggiornare quella installata.
            if (!Installer.IsRunningFromInstallDirectory &&
                (updateAvailable || !Installer.IsUnprotectedLocationAccepted(Application.StartupPath)))
            {
                switch (AskInstallation())
                {
                    case InstallAction.Install:
                        if (!Installer.Install(InteractiveUserSid))
                            return;
                        target = Installer.InstalledExecutable;
                        break;
                    case InstallAction.RunInstalled:
                        target = Installer.InstalledExecutable;
                        break;
                    case InstallAction.RunHere:
                        // Scelta consapevole (il messaggio spiega il rischio): non
                        // viene più chiesta per questa cartella.
                        Installer.AcceptUnprotectedLocation(Application.StartupPath);
                        break;
                    default:
                        return;
                }
            }

            string directory = Path.GetDirectoryName(target);
            bool installed = Installer.IsInstallDirectory(directory);
            string psExec = installed ? Installer.EnsureInstalledPsExec() : Installer.FindSignedPsExec();
            if (psExec == null)
            {
                // EnsureInstalledPsExec mostra già il proprio messaggio.
                if (!installed)
                    MessageBox.Show(Strings.Get("PsExecMissingText"),
                        Strings.Get("PsExecMissingTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (!ConfirmProtectedLocation(target, psExec))
                return;

            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = psExec,
                    Arguments = "-accepteula -i -s -d \"" + target +
                        "\" --interactive-user-sid=\"" + InteractiveUserSid + "\"",
                    WorkingDirectory = directory,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (process != null)
                    process.WaitForExit(5000);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Strings.Format("LaunchSystemFailed", ex.Message),
                    "Kimera", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private enum InstallAction { Cancel, Install, RunInstalled, RunHere }

        private static InstallAction AskInstallation()
        {
            Version current = Installer.CurrentVersion;
            Version installed = Installer.InstalledVersion;
            string title = Strings.Get("InstallTitle");
            string cancel = Strings.Get("Cancel");

            if (installed == null)
            {
                int answer = ChoiceDialog.Show(title,
                    Strings.Format("InstallPrompt", Installer.InstallDirectory), SystemIcons.Question,
                    new[] { Strings.Get("InstallButton"), Strings.Get("RunHereButton"), cancel }, 0, 2);
                return answer == 0 ? InstallAction.Install
                    : answer == 1 ? InstallAction.RunHere : InstallAction.Cancel;
            }

            if (installed < current)
            {
                int answer = ChoiceDialog.Show(title,
                    Strings.Format("UpdatePrompt", installed, Installer.InstallDirectory, current),
                    SystemIcons.Question,
                    new[] { Strings.Get("UpdateButton"), Strings.Get("RunInstalledButton"), cancel }, 0, 2);
                return answer == 0 ? InstallAction.Install
                    : answer == 1 ? InstallAction.RunInstalled : InstallAction.Cancel;
            }

            int choice = ChoiceDialog.Show(title,
                Strings.Format("InstalledPrompt", installed, Installer.InstallDirectory),
                SystemIcons.Information,
                new[] { Strings.Get("RunInstalledButton"), Strings.Get("RunHereButton"), cancel }, 0, 2);
            return choice == 0 ? InstallAction.RunInstalled
                : choice == 1 ? InstallAction.RunHere : InstallAction.Cancel;
        }

        private static string ReadInteractiveUserSid(string[] args)
        {
            const string prefix = "--interactive-user-sid=";
            foreach (string argument in args)
            {
                if (argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return NormalizeSid(argument.Substring(prefix.Length).Trim('"'));
            }
            return null;
        }

        private static string NormalizeSid(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            try
            {
                return new SecurityIdentifier(value).Value;
            }
            catch
            {
                return null;
            }
        }

        // Kimera, AsusWinIO64.dll e PsExec vengono eseguiti come SYSTEM: se un account
        // non amministratore può modificarli, può ottenere privilegi SYSTEM.
        private static bool ConfirmProtectedLocation(string executable, string psExec)
        {
            string directory = Path.GetDirectoryName(executable);
            var candidates = new List<string>
            {
                directory,
                executable,
                Path.Combine(directory, "AsusWinIO64.dll"),
                executable + ".config",
                Path.GetDirectoryName(psExec),
                psExec
            };
            List<string> unprotected = candidates
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(path => File.Exists(path) || Directory.Exists(path))
                .Where(IsUnprotected)
                .ToList();
            if (unprotected.Count == 0 || Installer.IsUnprotectedLocationAccepted(directory))
                return true;

            DialogResult answer = MessageBox.Show(
                Strings.Format("UnprotectedLocationText", string.Join("\n", unprotected)),
                Strings.Get("UnprotectedLocationTitle"), MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes)
                return false;

            Installer.AcceptUnprotectedLocation(directory);
            return true;
        }

        private static bool IsUnprotected(string path)
        {
            try
            {
                return FileSystemTrust.IsWritableByNonAdministrators(path);
            }
            catch
            {
                return true;
            }
        }
    }
}
