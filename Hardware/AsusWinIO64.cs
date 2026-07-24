using System.Runtime.InteropServices;

namespace AsusFanControlKimera.Hardware
{
    internal static class AsusWinIO64
    {
        [DllImport("AsusWinIO64.dll")]
        internal static extern void InitializeWinIo();
        [DllImport("AsusWinIO64.dll")]
        internal static extern void ShutdownWinIo();
        [DllImport("AsusWinIO64.dll")]
        internal static extern int HealthyTable_FanCounts();
        [DllImport("AsusWinIO64.dll")]
        internal static extern void HealthyTable_SetFanIndex(byte index);
        [DllImport("AsusWinIO64.dll")]
        internal static extern int HealthyTable_FanRPM();
        [DllImport("AsusWinIO64.dll")]
        internal static extern void HealthyTable_SetFanTestMode(char mode);
        [DllImport("AsusWinIO64.dll")]
        internal static extern void HealthyTable_SetFanPwmDuty(short duty);
        [DllImport("AsusWinIO64.dll")]
        internal static extern ulong Thermal_Read_Cpu_Temperature();
    }
}
