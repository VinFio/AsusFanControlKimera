using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace AsusFanControlKimera.Setup
{
    internal static class Authenticode
    {
        private const uint WtdUiNone = 2;
        private const uint WtdRevokeNone = 0;
        private const uint WtdChoiceFile = 1;
        private const uint WtdStateActionIgnore = 0;
        // Nessun download durante la verifica: all'accesso la rete potrebbe non
        // essere ancora disponibile.
        private const uint WtdCacheOnlyUrlRetrieval = 0x00001000;
        private static readonly Guid GenericVerifyV2 =
            new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
        private static readonly Regex MicrosoftOrganization =
            new Regex(@"(^|,\s*)O=Microsoft Corporation(\s*,|$)", RegexOptions.CultureInvariant);

        /// <summary>
        /// Vero se il file ha una firma Authenticode valida rilasciata a Microsoft Corporation.
        /// </summary>
        internal static bool IsMicrosoftSigned(string path)
        {
            try
            {
                if (!HasValidSignature(path))
                    return false;
                using (var certificate = new X509Certificate2(
                    X509Certificate.CreateFromSignedFile(path)))
                    return MicrosoftOrganization.IsMatch(certificate.Subject);
            }
            catch
            {
                return false;
            }
        }

        private static bool HasValidSignature(string path)
        {
            var fileInfo = new WintrustFileInfo
            {
                cbStruct = (uint)Marshal.SizeOf(typeof(WintrustFileInfo)),
                pcwszFilePath = path
            };
            IntPtr filePointer = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WintrustFileInfo)));
            try
            {
                Marshal.StructureToPtr(fileInfo, filePointer, false);
                var data = new WintrustData
                {
                    cbStruct = (uint)Marshal.SizeOf(typeof(WintrustData)),
                    dwUIChoice = WtdUiNone,
                    fdwRevocationChecks = WtdRevokeNone,
                    dwUnionChoice = WtdChoiceFile,
                    pFile = filePointer,
                    dwStateAction = WtdStateActionIgnore,
                    dwProvFlags = WtdCacheOnlyUrlRetrieval
                };
                Guid action = GenericVerifyV2;
                return WinVerifyTrust(new IntPtr(-1), ref action, ref data) == 0;
            }
            finally
            {
                Marshal.DestroyStructure(filePointer, typeof(WintrustFileInfo));
                Marshal.FreeHGlobal(filePointer);
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WintrustFileInfo
        {
            internal uint cbStruct;
            [MarshalAs(UnmanagedType.LPWStr)]
            internal string pcwszFilePath;
            internal IntPtr hFile;
            internal IntPtr pgKnownSubject;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WintrustData
        {
            internal uint cbStruct;
            internal IntPtr pPolicyCallbackData;
            internal IntPtr pSIPClientData;
            internal uint dwUIChoice;
            internal uint fdwRevocationChecks;
            internal uint dwUnionChoice;
            internal IntPtr pFile;
            internal uint dwStateAction;
            internal IntPtr hWVTStateData;
            internal IntPtr pwszURLReference;
            internal uint dwProvFlags;
            internal uint dwUIContext;
            internal IntPtr pSignatureSettings;
        }

        [DllImport("wintrust.dll", CharSet = CharSet.Unicode)]
        private static extern int WinVerifyTrust(IntPtr window, ref Guid action, ref WintrustData data);
    }
}
