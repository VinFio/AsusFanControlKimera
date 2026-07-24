using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
using AsusFanControlKimera.Diagnostics;
using AsusFanControlKimera.Properties;
using AsusFanControlKimera.UI;

namespace AsusFanControlKimera
{
    internal static class Program
    {
        internal static string InteractiveUserSid { get; private set; }

        [STAThread]
        private static void Main(string[] args)
        {
            if (!EnsureSystemIdentity(args))
                return;

            bool firstInstance;
            using (var mutex = new Mutex(true, @"Global\AsusFanControlKimera.SingleInstance", out firstInstance))
            {
                if (!firstInstance)
                {
                    MessageBox.Show("AsusFanControlKimera è già in esecuzione.", "Kimera",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                MainForm mainForm = null;
                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
                {
                    if (mainForm != null)
                        mainForm.HandleUnhandledException(e.Exception);
                    MessageBox.Show(e.Exception.Message, "Errore Kimera",
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
                        MessageBox.Show("Sono necessari i privilegi elevati.\n\n" + ex.Message,
                            "Kimera", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    return false;
                }
            }

            string psExec = FindPsExec();
            if (psExec == null)
            {
                MessageBox.Show(
                    "PsExec.exe non è stato trovato.\n\n" +
                    "Kimera deve essere eseguito come account SYSTEM, come il run.bat " +
                    "dell'AsusFanControl funzionante.\n\n" +
                    "Percorso atteso:\nC:\\Program Files (x86)\\AsusFanControl\\PsExec.exe",
                    "Kimera - PsExec mancante", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

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
                MessageBox.Show("Impossibile avviare Kimera come SYSTEM.\n\n" + ex.Message,
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

        private static string FindPsExec()
        {
            string[] candidates =
            {
                Path.Combine(Application.StartupPath, "PsExec.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "AsusFanControl", "PsExec.exe")
            };
            foreach (string candidate in candidates)
                if (File.Exists(candidate))
                    return candidate;
            return null;
        }
    }
}
