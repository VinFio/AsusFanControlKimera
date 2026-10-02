using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
using AsusFanControlKimera.Diagnostics;
using AsusFanControlKimera.Localization;
using AsusFanControlKimera.Properties;
using AsusFanControlKimera.Security;
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
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
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
            mainForm = new MainForm();
            Application.Run(mainForm);
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
            string arguments = RestartArgument;
            if (!string.IsNullOrEmpty(InteractiveUserSid))
                arguments += " --interactive-user-sid=\"" + InteractiveUserSid + "\"";
            Process.Start(new ProcessStartInfo
            {
                FileName = Application.ExecutablePath,
                Arguments = arguments,
                WorkingDirectory = Application.StartupPath,
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

            string psExec = FindPsExec();
            if (psExec == null)
            {
                MessageBox.Show(Strings.Get("PsExecMissingText"),
                    Strings.Get("PsExecMissingTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            if (!ConfirmProtectedLocation(psExec))
                return false;

            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = psExec,
                    Arguments = "-accepteula -i -s -d \"" + Application.ExecutablePath +
                        "\" --interactive-user-sid=\"" + InteractiveUserSid + "\"",
                    WorkingDirectory = Application.StartupPath,
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
            return false;
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
        private static bool ConfirmProtectedLocation(string psExec)
        {
            string directory = Application.StartupPath;
            var candidates = new List<string>
            {
                directory,
                Application.ExecutablePath,
                Path.Combine(directory, "AsusWinIO64.dll"),
                Application.ExecutablePath + ".config",
                Path.GetDirectoryName(psExec),
                psExec
            };
            List<string> unprotected = candidates
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(path => File.Exists(path) || Directory.Exists(path))
                .Where(IsUnprotected)
                .ToList();
            if (unprotected.Count == 0)
                return true;

            string acceptedKey = directory + "|" + psExec;
            if (string.Equals(Settings.Default.AcceptedUnprotectedLocation, acceptedKey,
                StringComparison.OrdinalIgnoreCase))
                return true;

            DialogResult answer = MessageBox.Show(
                Strings.Format("UnprotectedLocationText", string.Join("\n", unprotected)),
                Strings.Get("UnprotectedLocationTitle"), MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes)
                return false;

            Settings.Default.AcceptedUnprotectedLocation = acceptedKey;
            Settings.Default.Save();
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

        private static string FindPsExec()
        {
            // La cartella protetta di Program Files ha la precedenza su una copia locale.
            string[] candidates =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "AsusFanControl", "PsExec.exe"),
                Path.Combine(Application.StartupPath, "PsExec.exe")
            };
            foreach (string candidate in candidates)
                if (File.Exists(candidate))
                    return candidate;
            return null;
        }
    }
}
