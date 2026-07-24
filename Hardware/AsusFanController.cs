using System;
using System.Collections.Generic;
using System.Threading;

namespace AsusFanControlKimera.Hardware
{
    internal sealed class AsusFanController : IDisposable
    {
        private readonly object sync = new object();
        private bool disposed;
        private int lastKnownFanCount;

        internal int LastDuty { get; private set; }
        internal int LastFanCount { get; private set; }

        internal AsusFanController()
        {
            lock (sync)
            {
                AsusWinIO64.InitializeWinIo();
            }
        }

        internal int FanCount
        {
            get
            {
                lock (sync)
                {
                    EnsureAvailable();
                    return GetFanCountWithRetry();
                }
            }
        }

        internal IList<int> ReadFanSpeeds()
        {
            lock (sync)
            {
                EnsureAvailable();
                var result = new List<int>();
                int count = GetFanCountWithRetry();
                for (byte index = 0; index < count; index++)
                {
                    AsusWinIO64.HealthyTable_SetFanIndex(index);
                    result.Add(AsusWinIO64.HealthyTable_FanRPM());
                }
                return result;
            }
        }

        internal ulong ReadCpuTemperature()
        {
            lock (sync)
            {
                EnsureAvailable();
                return AsusWinIO64.Thermal_Read_Cpu_Temperature();
            }
        }

        internal void SetAllFans(int percent)
        {
            percent = Math.Max(0, Math.Min(100, percent));
            // Mantiene volutamente la stessa conversione del motore AsusFanControl:
            // float, troncamento a byte, quindi promozione implicita per la P/Invoke.
            byte duty = (byte)(percent / 100.0f * 255);

            lock (sync)
            {
                EnsureAvailable();
                int count = GetFanCountWithRetry();
                LastFanCount = count;
                LastDuty = duty;
                try
                {
                    for (byte index = 0; index < count; index++)
                    {
                        SetFanSpeed(duty, index);
                        if (index + 1 < count)
                            Thread.Sleep(20);
                    }
                }
                catch
                {
                    // Se il comando fallisce dopo avere modificato solo alcune
                    // ventole, tenta di restituire tutte quelle note al firmware.
                    TryReleaseKnownFans();
                    throw;
                }
            }
        }

        private static void SetFanSpeed(byte value, byte fanIndex)
        {
            // Ordine identico ad AsusFanControl funzionante.
            AsusWinIO64.HealthyTable_SetFanIndex(fanIndex);
            AsusWinIO64.HealthyTable_SetFanTestMode((char)(value > 0 ? 0x01 : 0x00));
            AsusWinIO64.HealthyTable_SetFanPwmDuty(value);
        }

        internal void ReleaseControl()
        {
            SetAllFans(0);
        }

        internal bool TryEmergencyRelease()
        {
            lock (sync)
            {
                if (disposed)
                    return false;
                return TryReleaseKnownFans();
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed)
                    return;
                AsusWinIO64.ShutdownWinIo();
                disposed = true;
            }
        }

        private void EnsureAvailable()
        {
            if (disposed)
                throw new ObjectDisposedException("AsusFanController");
        }

        private int GetFanCountWithRetry()
        {
            // Su alcuni portatili il servizio WinIO risponde prima che la tabella
            // hardware ASUS sia pronta. L'app originale evita implicitamente il
            // problema perché l'utente invia il primo comando dopo l'avvio.
            const int attempts = 20;
            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                int count = AsusWinIO64.HealthyTable_FanCounts();
                if (count >= 1 && count <= 8)
                {
                    lastKnownFanCount = count;
                    return count;
                }
                if (attempt < attempts)
                    Thread.Sleep(100);
            }

            // Durante un guasto transitorio è più sicuro usare il conteggio già
            // verificato, soprattutto per poter disattivare il test mode.
            if (lastKnownFanCount >= 1 && lastKnownFanCount <= 8)
                return lastKnownFanCount;

            throw new InvalidOperationException(
                "Il driver ASUS non ha restituito un conteggio ventole valido (1-8) dopo 20 tentativi.");
        }

        private bool TryReleaseKnownFans()
        {
            int count = lastKnownFanCount;
            if (count < 1 || count > 8)
                return false;

            bool success = true;
            for (byte index = 0; index < count; index++)
            {
                try
                {
                    SetFanSpeed(0, index);
                }
                catch
                {
                    success = false;
                }
            }
            LastDuty = 0;
            return success;
        }
    }
}
