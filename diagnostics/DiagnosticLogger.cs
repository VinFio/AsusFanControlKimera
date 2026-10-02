using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Text;
using AsusFanControlKimera.Security;
using Microsoft.Win32.SafeHandles;

namespace AsusFanControlKimera.Diagnostics
{
    internal static class DiagnosticLogger
    {
        private const long MaximumBytes = 1024L * 1024L;
        private const long RetainedBytes = 768L * 1024L;
        private const AccessControlSections OwnerAndAccess =
            AccessControlSections.Owner | AccessControlSections.Access;
        private static readonly object Sync = new object();
        private static bool enabled;
        private static bool locationVerified;

        internal static string LogPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "AsusFanControlKimera",
                    "kimera-debug.log");
            }
        }

        internal static bool Enabled { get { return enabled; } }

        internal static void Configure(bool value)
        {
            lock (Sync)
            {
                enabled = value;
                if (enabled)
                    WriteCore("DEBUG", "Registro diagnostico abilitato.");
            }
        }

        internal static void Log(string eventType, string message)
        {
            lock (Sync)
            {
                if (!enabled)
                    return;
                WriteCore(eventType, message);
            }
        }

        internal static void LogSnapshot(
            string mode,
            ulong temperature,
            IList<int> fanSpeeds,
            int requestedPwm,
            int duty)
        {
            string rpm = fanSpeeds == null
                ? "-"
                : string.Join(",", fanSpeeds.Select((value, index) =>
                    string.Format(CultureInfo.InvariantCulture, "F{0}:{1}", index + 1, value)));
            Log("SNAPSHOT", string.Format(CultureInfo.InvariantCulture,
                "mode={0}; tempC={1}; rpm={2}; pwm={3}; duty={4}/255",
                mode, temperature, rpm, requestedPwm, duty));
        }

        internal static string ReadLog()
        {
            lock (Sync)
            {
                string path = LogPath;
                if (!PrepareLocation(path) || !File.Exists(path))
                    return string.Empty;
                return File.ReadAllText(path, Encoding.UTF8);
            }
        }

        private static void WriteCore(string eventType, string message)
        {
            try
            {
                string path = LogPath;
                if (!PrepareLocation(path))
                    return;

                string line = string.Format(CultureInfo.InvariantCulture,
                    "{0:O} | {1} | {2}{3}",
                    DateTime.Now, eventType, Sanitize(message), Environment.NewLine);
                File.AppendAllText(path, line, new UTF8Encoding(false));
                if (new FileInfo(path).Length > MaximumBytes)
                    TrimOldEntries(path);
            }
            catch
            {
                // Il debug non deve mai interferire con il controllo ventole.
            }
        }

        // Kimera scrive come SYSTEM: la cartella del registro deve essere modificabile
        // solo da SYSTEM e Administrators, altrimenti un utente standard potrebbe
        // sostituire il file con un collegamento verso un file di sistema.
        private static bool PrepareLocation(string path)
        {
            if (locationVerified)
                return true;
            try
            {
                string directory = Path.GetDirectoryName(path);
                var info = new DirectoryInfo(directory);
                if (info.Exists && (info.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    // Rimuove solo la giunzione, non la cartella di destinazione.
                    Directory.Delete(directory, false);
                    info.Refresh();
                }

                if (!info.Exists)
                    Directory.CreateDirectory(directory,
                        FileSystemTrust.CreateProtectedDirectorySecurity());
                else if (FileSystemTrust.AllowsUntrustedWrite(info.GetAccessControl(OwnerAndAccess)))
                    info.SetAccessControl(FileSystemTrust.CreateProtectedDirectorySecurity());

                info.Refresh();
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0 ||
                    FileSystemTrust.AllowsUntrustedWrite(info.GetAccessControl(OwnerAndAccess)))
                    return false;

                // Con la cartella protetta nessuno può creare nuovi collegamenti; quelli
                // preesistenti vengono rimossi (si elimina il nome, non il file di destinazione).
                if (File.Exists(path) && IsLinkedFile(path))
                    File.Delete(path);

                locationVerified = true;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsLinkedFile(string path)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                return true;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            {
                ByHandleFileInformation information;
                if (!GetFileInformationByHandle(stream.SafeFileHandle, out information))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                return information.NumberOfLinks > 1;
            }
        }

        private static void TrimOldEntries(string path)
        {
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            var retained = new List<string>();
            long bytes = 0;
            for (int index = lines.Length - 1; index >= 0; index--)
            {
                long lineBytes = Encoding.UTF8.GetByteCount(lines[index] + Environment.NewLine);
                if (bytes + lineBytes > RetainedBytes)
                    break;
                retained.Add(lines[index]);
                bytes += lineBytes;
            }
            retained.Reverse();
            File.WriteAllLines(path, retained, new UTF8Encoding(false));
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            return value.Replace("\r", " ").Replace("\n", " ");
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ByHandleFileInformation
        {
            internal uint FileAttributes;
            internal System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
            internal System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
            internal System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
            internal uint VolumeSerialNumber;
            internal uint FileSizeHigh;
            internal uint FileSizeLow;
            internal uint NumberOfLinks;
            internal uint FileIndexHigh;
            internal uint FileIndexLow;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandle(
            SafeFileHandle file, out ByHandleFileInformation information);
    }
}
