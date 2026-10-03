using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
using AsusFanControlKimera.Localization;
using AsusFanControlKimera.Security;
using Microsoft.Win32;

namespace AsusFanControlKimera.Setup
{
    /// <summary>
    /// Installazione integrata: copia Kimera in Program Files, dove solo gli
    /// amministratori possono modificarlo, e gestisce PsExec, aggiornamenti e rimozione.
    /// </summary>
    internal static class Installer
    {
        internal const string ExecutableName = "AsusFanControlKimera.exe";
        internal const string ShutdownEventName = @"Global\AsusFanControlKimera.Shutdown";
        internal const string PsExecDownloadUrl = "https://live.sysinternals.com/PsExec.exe";
        private const string PsExecName = "PsExec.exe";
        private const string ShortcutName = "Asus Fan Control Kimera.lnk";
        private const string PreferencesKeyPath = @"Software\AsusFanControlKimera";
        private const string AcceptedLocationsValue = "AcceptedUnprotectedLocations";
        private static readonly string[] RuntimeFiles =
        {
            ExecutableName, ExecutableName + ".config", "AsusWinIO64.dll"
        };

        internal static string InstallDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "AsusFanControlKimera");
            }
        }

        internal static string InstalledExecutable
        {
            get { return Path.Combine(InstallDirectory, ExecutableName); }
        }

        internal static bool IsRunningFromInstallDirectory
        {
            get { return IsInstallDirectory(Application.StartupPath); }
        }

        internal static Version CurrentVersion
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version; }
        }

        internal static Version InstalledVersion
        {
            get
            {
                try
                {
                    if (!File.Exists(InstalledExecutable))
                        return null;
                    return new Version(FileVersionInfo.GetVersionInfo(InstalledExecutable).FileVersion);
                }
                catch
                {
                    return null;
                }
            }
        }

        internal static bool IsInstallDirectory(string directory)
        {
            return PathsEqual(directory, InstallDirectory);
        }

        internal static bool PathsEqual(string first, string second)
        {
            if (string.IsNullOrEmpty(first) || string.IsNullOrEmpty(second))
                return false;
            try
            {
                return string.Equals(
                    Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar),
                    Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Installa o aggiorna Kimera copiando i file accanto all'eseguibile corrente.
        /// Richiede privilegi di amministratore o SYSTEM. Mostra da sé gli eventuali errori.
        /// </summary>
        internal static bool Install(string userSid)
        {
            try
            {
                string source = Application.StartupPath;
                foreach (string file in RuntimeFiles)
                {
                    if (!File.Exists(Path.Combine(source, file)))
                        throw new FileNotFoundException(Strings.Format("InstallMissingFile", file));
                }

                if (!StopRunningInstances())
                    return false;

                PrepareInstallDirectory();
                foreach (string file in RuntimeFiles)
                    CopyWithRetry(Path.Combine(source, file), Path.Combine(InstallDirectory, file));

                // Verifica a posteriori: un file preesistente potrebbe aver
                // conservato permessi diversi da quelli della cartella.
                foreach (string file in RuntimeFiles)
                {
                    string target = Path.Combine(InstallDirectory, file);
                    if (FileSystemTrust.IsWritableByNonAdministrators(target))
                        throw new InvalidOperationException(
                            Strings.Format("InstallFolderUnprotected", target));
                }

                TryCreateShortcut();
                MigrateStartup(userSid);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(Strings.Format("InstallFailed", ex.Message),
                    Strings.Get("InstallTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        /// <summary>
        /// Garantisce un PsExec firmato da Microsoft nella cartella di installazione:
        /// lo copia da una posizione nota oppure, previa conferma, lo scarica.
        /// </summary>
        internal static string EnsureInstalledPsExec()
        {
            string target = Path.Combine(InstallDirectory, PsExecName);
            try
            {
                if (File.Exists(target) && Authenticode.IsMicrosoftSigned(target))
                    return target;

                foreach (string candidate in ExistingPsExecCandidates())
                {
                    if (PathsEqual(candidate, target) || !File.Exists(candidate))
                        continue;
                    CopyWithRetry(candidate, target);
                    // La firma viene controllata sulla copia, già nella cartella protetta.
                    if (Authenticode.IsMicrosoftSigned(target))
                        return target;
                    File.Delete(target);
                }

                DialogResult answer = MessageBox.Show(
                    Strings.Format("PsExecDownloadPrompt", PsExecDownloadUrl),
                    Strings.Get("PsExecMissingTitle"), MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (answer != DialogResult.Yes)
                    return null;

                string download = target + ".download";
                DownloadFile(PsExecDownloadUrl, download);
                if (!Authenticode.IsMicrosoftSigned(download))
                {
                    File.Delete(download);
                    throw new InvalidOperationException(
                        Strings.Format("PsExecInvalidSignature", PsExecDownloadUrl));
                }
                if (File.Exists(target))
                    File.Delete(target);
                File.Move(download, target);
                return target;
            }
            catch (Exception ex)
            {
                MessageBox.Show(Strings.Format("PsExecDownloadFailed", ex.Message),
                    Strings.Get("PsExecMissingTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }

        /// <summary>PsExec firmato da Microsoft per un Kimera non installato, oppure null.</summary>
        internal static string FindSignedPsExec()
        {
            var candidates = new List<string> { Path.Combine(InstallDirectory, PsExecName) };
            candidates.AddRange(ExistingPsExecCandidates());
            return candidates.FirstOrDefault(path =>
                File.Exists(path) && Authenticode.IsMicrosoftSigned(path));
        }

        private static IEnumerable<string> ExistingPsExecCandidates()
        {
            yield return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "AsusFanControl", PsExecName);
            yield return Path.Combine(Application.StartupPath, PsExecName);
        }

        internal static bool IsUnprotectedLocationAccepted(string directory)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(PreferencesKeyPath))
                {
                    var accepted = key == null ? null : key.GetValue(AcceptedLocationsValue) as string[];
                    return accepted != null && accepted.Any(item => PathsEqual(item, directory));
                }
            }
            catch
            {
                return false;
            }
        }

        // Salvata nel profilo utente e non nelle impostazioni dell'applicazione,
        // che cambiano a ogni versione: così la scelta resta valida dopo un aggiornamento.
        internal static void AcceptUnprotectedLocation(string directory)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(PreferencesKeyPath))
                {
                    var accepted = (key.GetValue(AcceptedLocationsValue) as string[] ?? new string[0])
                        .Where(item => !PathsEqual(item, directory))
                        .Concat(new[] { directory })
                        .ToArray();
                    key.SetValue(AcceptedLocationsValue, accepted, RegistryValueKind.MultiString);
                }
            }
            catch
            {
                // Al prossimo avvio la domanda verrà semplicemente ripetuta.
            }
        }

        /// <summary>
        /// Rimuove collegamento e cartella di installazione. Va chiamato dall'istanza
        /// installata, che deve chiudersi subito dopo: la cartella viene eliminata
        /// appena il processo è terminato.
        /// </summary>
        internal static void ScheduleUninstall()
        {
            string directory = InstallDirectory;
            if (!IsRunningFromInstallDirectory || !File.Exists(Path.Combine(directory, ExecutableName)))
                throw new InvalidOperationException(directory);

            TryRemoveShortcut();
            string quoted = "\"" + directory + "\"";
            Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
                Arguments = "/d /c \"for /l %i in (1,1,30) do (ping -n 2 127.0.0.1 >nul & " +
                    "rmdir /s /q " + quoted + " 2>nul & if not exist " + quoted + " exit /b 0)\"",
                WorkingDirectory = Environment.SystemDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }

        private static void PrepareInstallDirectory()
        {
            string directory = InstallDirectory;
            if (Directory.Exists(directory) &&
                (new DirectoryInfo(directory).Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException(Strings.Format("InstallFolderUnprotected", directory));

            // Una nuova cartella in Program Files eredita permessi già protetti.
            Directory.CreateDirectory(directory);
            if (!FileSystemTrust.IsWritableByNonAdministrators(directory))
                return;

            Directory.SetAccessControl(directory,
                FileSystemTrust.CreateProtectedDirectorySecurity(FileSystemTrust.AdministratorsSid));
            if (FileSystemTrust.IsWritableByNonAdministrators(directory))
                throw new InvalidOperationException(Strings.Format("InstallFolderUnprotected", directory));
        }

        private static bool StopRunningInstances()
        {
            while (true)
            {
                if (!OtherInstancesRunning())
                    return true;

                // Le versioni 1.4.0 e successive si chiudono su richiesta,
                // restituendo prima le ventole al firmware.
                try
                {
                    using (EventWaitHandle shutdown = EventWaitHandle.OpenExisting(
                        ShutdownEventName, EventWaitHandleRights.Modify | EventWaitHandleRights.Synchronize))
                        shutdown.Set();
                }
                catch
                {
                    // Versione precedente o istanza già in chiusura.
                }

                DateTime deadline = DateTime.UtcNow.AddSeconds(20);
                while (OtherInstancesRunning() && DateTime.UtcNow < deadline)
                    Thread.Sleep(250);
                if (!OtherInstancesRunning())
                {
                    // Lascia al driver il tempo di chiudere i file della cartella.
                    Thread.Sleep(500);
                    return true;
                }

                if (MessageBox.Show(Strings.Get("CloseRunningPrompt"), Strings.Get("InstallTitle"),
                    MessageBoxButtons.RetryCancel, MessageBoxIcon.Warning) != DialogResult.Retry)
                    return false;
            }
        }

        private static bool OtherInstancesRunning()
        {
            int currentId = Process.GetCurrentProcess().Id;
            Process[] processes = Process.GetProcessesByName(
                Path.GetFileNameWithoutExtension(ExecutableName));
            try
            {
                return processes.Any(process => process.Id != currentId);
            }
            finally
            {
                foreach (Process process in processes)
                    process.Dispose();
            }
        }

        private static void CopyWithRetry(string source, string target)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    File.Copy(source, target, true);
                    return;
                }
                catch (IOException)
                {
                    if (attempt >= 10)
                        throw;
                    Thread.Sleep(500);
                }
            }
        }

        private static void DownloadFile(string url, string target)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Timeout = 30000;
            request.ReadWriteTimeout = 30000;
            request.UserAgent = "AsusFanControlKimera/" + CurrentVersion;
            using (var response = (HttpWebResponse)request.GetResponse())
            using (Stream input = response.GetResponseStream())
            using (var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
                input.CopyTo(output);
        }

        private static void MigrateStartup(string sid)
        {
            // Mantiene l'avvio automatico se era già attivo, passando dalla chiave
            // Run (con conferma UAC) all'attività pianificata verso Program Files.
            if (string.IsNullOrEmpty(sid))
                return;
            bool legacy = StartupTask.LegacyRunExists(sid);
            if (!legacy && StartupTask.GetCommand() == null)
                return;
            StartupTask.Create(InstalledExecutable, sid);
            if (legacy)
                StartupTask.DeleteLegacyRun(sid);
        }

        private static string ShortcutPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
                    ShortcutName);
            }
        }

        private static void TryCreateShortcut()
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                object shell = Activator.CreateInstance(shellType);
                try
                {
                    object shortcut = shellType.InvokeMember("CreateShortcut",
                        BindingFlags.InvokeMethod, null, shell, new object[] { ShortcutPath });
                    Type shortcutType = shortcut.GetType();
                    shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null,
                        shortcut, new object[] { InstalledExecutable });
                    shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null,
                        shortcut, new object[] { InstallDirectory });
                    shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null,
                        shortcut, new object[] { InstalledExecutable + ",0" });
                    shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null,
                        shortcut, null);
                }
                finally
                {
                    System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
                }
            }
            catch
            {
                // Il collegamento è una comodità: l'installazione resta valida anche senza.
            }
        }

        private static void TryRemoveShortcut()
        {
            try
            {
                if (File.Exists(ShortcutPath))
                    File.Delete(ShortcutPath);
            }
            catch
            {
            }
        }
    }
}
