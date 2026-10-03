using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Text;
using System.Xml;
using Microsoft.Win32;

namespace AsusFanControlKimera.Setup
{
    /// <summary>
    /// Avvio con Windows tramite un'attività pianificata eseguita all'accesso
    /// dell'utente con privilegi elevati: nessuna richiesta UAC a ogni accesso.
    /// </summary>
    internal static class StartupTask
    {
        internal const string TaskName = "AsusFanControlKimera";
        private const string LegacyRunValue = "AsusFanControlKimera";
        private const string RunKeyPath = @"\Software\Microsoft\Windows\CurrentVersion\Run";
        private const string TaskNamespace = "http://schemas.microsoft.com/windows/2004/02/mit/task";

        /// <summary>Percorso dell'eseguibile avviato dall'attività, oppure null se non esiste.</summary>
        internal static string GetCommand()
        {
            string output;
            if (RunSchtasks("/Query /TN \"" + TaskName + "\" /XML ONE", out output) != 0)
                return null;
            try
            {
                var document = new XmlDocument { XmlResolver = null };
                document.LoadXml(output.Substring(Math.Max(0, output.IndexOf('<'))));
                var namespaces = new XmlNamespaceManager(document.NameTable);
                namespaces.AddNamespace("t", TaskNamespace);
                XmlNode command = document.SelectSingleNode("/t:Task/t:Actions/t:Exec/t:Command", namespaces);
                return command == null ? string.Empty : command.InnerText.Trim().Trim('"');
            }
            catch (XmlException)
            {
                // L'attività esiste ma non è leggibile: viene trattata come da ricreare.
                return string.Empty;
            }
        }

        internal static void Create(string executablePath, string userSid)
        {
            if (string.IsNullOrEmpty(userSid))
                throw new ArgumentException("userSid");

            // Il file XML viene scritto nella cartella protetta di Kimera, non in una
            // cartella temporanea dove un altro utente potrebbe sostituirlo prima
            // che schtasks lo legga.
            string directory = Path.GetDirectoryName(executablePath);
            string xmlPath = Path.Combine(directory, "startup-task.xml");
            try
            {
                File.WriteAllText(xmlPath, BuildDefinition(executablePath, directory, userSid),
                    Encoding.Unicode);
                string output;
                if (RunSchtasks("/Create /TN \"" + TaskName + "\" /XML \"" + xmlPath + "\" /F",
                    out output) != 0)
                    throw new InvalidOperationException(output.Trim());
            }
            finally
            {
                try { File.Delete(xmlPath); } catch { }
            }
        }

        internal static void Delete()
        {
            if (GetCommand() == null)
                return;
            string output;
            if (RunSchtasks("/Delete /TN \"" + TaskName + "\" /F", out output) != 0)
                throw new InvalidOperationException(output.Trim());
        }

        // Avvio automatico delle versioni precedenti (chiave Run con richiesta UAC).
        internal static bool LegacyRunExists(string userSid)
        {
            try
            {
                using (RegistryKey key = OpenRunKey(userSid, false))
                    return key != null && key.GetValue(LegacyRunValue) != null;
            }
            catch
            {
                return false;
            }
        }

        internal static void DeleteLegacyRun(string userSid)
        {
            using (RegistryKey key = OpenRunKey(userSid, true))
            {
                if (key != null)
                    key.DeleteValue(LegacyRunValue, false);
            }
        }

        private static RegistryKey OpenRunKey(string userSid, bool writable)
        {
            if (string.IsNullOrEmpty(userSid))
                return null;
            return Registry.Users.OpenSubKey(userSid + RunKeyPath, writable);
        }

        private static string BuildDefinition(string executablePath, string directory, string userSid)
        {
            string sid = SecurityElement.Escape(userSid);
            return
                "<?xml version=\"1.0\" encoding=\"UTF-16\"?>\r\n" +
                "<Task version=\"1.2\" xmlns=\"" + TaskNamespace + "\">\r\n" +
                "  <RegistrationInfo>\r\n" +
                "    <Author>AsusFanControlKimera</Author>\r\n" +
                "    <Description>Avvia Asus Fan Control Kimera all'accesso dell'utente.</Description>\r\n" +
                "  </RegistrationInfo>\r\n" +
                "  <Triggers>\r\n" +
                "    <LogonTrigger>\r\n" +
                "      <Enabled>true</Enabled>\r\n" +
                "      <UserId>" + sid + "</UserId>\r\n" +
                // Lascia ai servizi ASUS il tempo di avviarsi dopo l'accesso.
                "      <Delay>PT15S</Delay>\r\n" +
                "    </LogonTrigger>\r\n" +
                "  </Triggers>\r\n" +
                "  <Principals>\r\n" +
                "    <Principal id=\"Author\">\r\n" +
                "      <UserId>" + sid + "</UserId>\r\n" +
                "      <LogonType>InteractiveToken</LogonType>\r\n" +
                "      <RunLevel>HighestAvailable</RunLevel>\r\n" +
                "    </Principal>\r\n" +
                "  </Principals>\r\n" +
                "  <Settings>\r\n" +
                "    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>\r\n" +
                // Su un portatile i valori predefiniti impedirebbero l'avvio a batteria.
                "    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>\r\n" +
                "    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>\r\n" +
                "    <AllowHardTerminate>true</AllowHardTerminate>\r\n" +
                "    <StartWhenAvailable>false</StartWhenAvailable>\r\n" +
                "    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>\r\n" +
                "    <IdleSettings>\r\n" +
                "      <StopOnIdleEnd>false</StopOnIdleEnd>\r\n" +
                "      <RestartOnIdle>false</RestartOnIdle>\r\n" +
                "    </IdleSettings>\r\n" +
                "    <AllowStartOnDemand>true</AllowStartOnDemand>\r\n" +
                "    <Enabled>true</Enabled>\r\n" +
                "    <Hidden>false</Hidden>\r\n" +
                "    <RunOnlyIfIdle>false</RunOnlyIfIdle>\r\n" +
                "    <WakeToRun>false</WakeToRun>\r\n" +
                // Il predefinito (72 ore) terminerebbe l'avvio dopo tre giorni.
                "    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>\r\n" +
                "    <Priority>4</Priority>\r\n" +
                "  </Settings>\r\n" +
                "  <Actions Context=\"Author\">\r\n" +
                "    <Exec>\r\n" +
                "      <Command>\"" + SecurityElement.Escape(executablePath) + "\"</Command>\r\n" +
                "      <WorkingDirectory>" + SecurityElement.Escape(directory) + "</WorkingDirectory>\r\n" +
                "    </Exec>\r\n" +
                "  </Actions>\r\n" +
                "</Task>\r\n";
        }

        private static int RunSchtasks(string arguments, out string output)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, "schtasks.exe"),
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Environment.SystemDirectory
            };
            using (Process process = Process.Start(startInfo))
            {
                var error = process.StandardError.ReadToEndAsync();
                string standardOutput = process.StandardOutput.ReadToEnd();
                if (!process.WaitForExit(30000))
                {
                    try { process.Kill(); } catch { }
                    throw new TimeoutException("schtasks.exe");
                }
                output = standardOutput + error.Result;
                return process.ExitCode;
            }
        }
    }
}
