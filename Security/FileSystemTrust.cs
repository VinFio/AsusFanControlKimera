using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace AsusFanControlKimera.Security
{
    internal static class FileSystemTrust
    {
        // WriteData/CreateFiles, AppendData/CreateDirectories, permessi e proprietà,
        // più i bit generici GENERIC_ALL e GENERIC_WRITE delle ACE ereditabili.
        private const FileSystemRights WriteRights =
            FileSystemRights.WriteData |
            FileSystemRights.AppendData |
            FileSystemRights.ChangePermissions |
            FileSystemRights.TakeOwnership |
            (FileSystemRights)0x50000000;

        private static readonly SecurityIdentifier LocalSystem =
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        private static readonly SecurityIdentifier Administrators =
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        private static readonly SecurityIdentifier CreatorOwner =
            new SecurityIdentifier(WellKnownSidType.CreatorOwnerSid, null);
        private static readonly SecurityIdentifier TrustedInstaller =
            new SecurityIdentifier("S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464");

        /// <summary>
        /// Indica se un account diverso da SYSTEM, Administrators o TrustedInstaller
        /// può creare, modificare o cambiare i permessi del file o della cartella.
        /// </summary>
        internal static bool IsWritableByNonAdministrators(string path)
        {
            FileSystemSecurity security = Directory.Exists(path)
                ? (FileSystemSecurity)Directory.GetAccessControl(path,
                    AccessControlSections.Owner | AccessControlSections.Access)
                : File.GetAccessControl(path,
                    AccessControlSections.Owner | AccessControlSections.Access);
            return AllowsUntrustedWrite(security);
        }

        internal static bool AllowsUntrustedWrite(FileSystemSecurity security)
        {
            // Il proprietario può sempre riscrivere la DACL.
            var owner = security.GetOwner(typeof(SecurityIdentifier)) as SecurityIdentifier;
            if (owner == null || !IsTrusted(owner))
                return true;

            foreach (FileSystemAccessRule rule in
                security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            {
                if (rule.AccessControlType != AccessControlType.Allow ||
                    (rule.FileSystemRights & WriteRights) == 0)
                    continue;
                var sid = rule.IdentityReference as SecurityIdentifier;
                // CREATOR OWNER vale solo per oggetti creati da chi ha già diritti di creazione.
                if (sid == null || sid == CreatorOwner)
                    continue;
                if (!IsTrusted(sid))
                    return true;
            }
            return false;
        }

        internal static SecurityIdentifier SystemSid { get { return LocalSystem; } }
        internal static SecurityIdentifier AdministratorsSid { get { return Administrators; } }

        internal static DirectorySecurity CreateProtectedDirectorySecurity()
        {
            return CreateProtectedDirectorySecurity(LocalSystem);
        }

        internal static DirectorySecurity CreateProtectedDirectorySecurity(SecurityIdentifier owner)
        {
            const InheritanceFlags inherit =
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
            var security = new DirectorySecurity();
            security.SetOwner(owner);
            security.SetAccessRuleProtection(true, false);
            security.AddAccessRule(new FileSystemAccessRule(LocalSystem,
                FileSystemRights.FullControl, inherit, PropagationFlags.None,
                AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(Administrators,
                FileSystemRights.FullControl, inherit, PropagationFlags.None,
                AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
                FileSystemRights.ReadAndExecute, inherit, PropagationFlags.None,
                AccessControlType.Allow));
            return security;
        }

        private static bool IsTrusted(SecurityIdentifier sid)
        {
            return sid == LocalSystem || sid == Administrators || sid == TrustedInstaller;
        }
    }
}
