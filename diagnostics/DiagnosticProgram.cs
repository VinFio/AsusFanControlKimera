using System;
using AsusFanControl;

namespace AsusFanControlKimera.Diagnostics
{
    internal static class DiagnosticProgram
    {
        [STAThread]
        private static int Main()
        {
            try
            {
                var control = new AsusControl();
                Console.WriteLine("Fan count: {0}", control.HealthyTable_FanCounts());
                Console.WriteLine("Fan speeds: {0}", string.Join(" ", control.GetFanSpeeds()));
                Console.WriteLine("CPU temp: {0}", control.Thermal_Read_Cpu_Temperature());
                Console.WriteLine("Premi INVIO per chiudere.");
                Console.ReadLine();
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                Console.WriteLine("Premi INVIO per chiudere.");
                Console.ReadLine();
                return 1;
            }
        }
    }
}
