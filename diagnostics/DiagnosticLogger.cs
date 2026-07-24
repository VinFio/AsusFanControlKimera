using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace AsusFanControlKimera.Diagnostics
{
    internal static class DiagnosticLogger
    {
        private const long MaximumBytes = 1024L * 1024L;
        private const long RetainedBytes = 768L * 1024L;
        private static readonly object Sync = new object();
        private static bool enabled;

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

        private static void WriteCore(string eventType, string message)
        {
            try
            {
                string path = LogPath;
                string directory = Path.GetDirectoryName(path);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

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
    }
}
