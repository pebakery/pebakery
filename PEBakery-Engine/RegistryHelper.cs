using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Win32.Interop;
using System.Security;
using System.Runtime.ConstrainedExecution;
using System.ComponentModel;

namespace BakeryEngine
{
    /// <summary>
    /// Exception used in BakeryEngine::ParseCommand
    /// </summary>
    public class InvalidRegistryKeyException : Exception
    {
        public InvalidRegistryKeyException() { }
        public InvalidRegistryKeyException(string message) : base(message) { }
        public InvalidRegistryKeyException(string message, Exception inner) : base(message, inner) { }
    }

    public static class RegistryHelper
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentProcess();
        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool OpenProcessToken(IntPtr ProcessHandle, UInt32 DesiredAccess, out IntPtr TokenHandle);
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, out LUID lpLuid);
        [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
        static extern bool AdjustTokenPrivileges(IntPtr htok, bool disableAllPrivileges, ref TOKEN_PRIVILEGES newState, UInt32 len, IntPtr prev, IntPtr relen);
        [DllImport("advapi32.dll", SetLastError = true)]
        static extern Int32 RegLoadKey(UInt32 hKey, string lpSubKey, string lpFile);
        [DllImport("advapi32.dll", SetLastError = true)]
        static extern Int32 RegUnLoadKey(UInt32 hKey, string lpSubKey);
        [DllImport("kernel32.dll", SetLastError = true)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        [SuppressUnmanagedCodeSecurity]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct LUID_AND_ATTRIBUTES
        {
            public LUID pLuid;
            public UInt32 Attributes;
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct TOKEN_PRIVILEGES
        {
            public int Count;
            public LUID Luid;
            public UInt32 Attr;
        }

        private const Int32 ANYSIZE_ARRAY = 1;
        private const UInt32 SE_PRIVILEGE_ENABLED = 0x00000002;
        private const UInt32 TOKEN_ADJUST_PRIVILEGES = 0x0020;
        private const UInt32 TOKEN_QUERY = 0x0008;

        public const UInt32 HKCR = 0x80000000; // HKEY_CLASSES_ROOT
        public const UInt32 HKCU = 0x80000001; // HKEY_CURRENT_USER
        public const UInt32 HKLM = 0x80000002; // HKEY_LOCAL_MACHINE
        public const UInt32 HKU  = 0x80000003; // HKEY_USERS
        public const UInt32 HKPD = 0x80000004; // HKEY_PERFORMANCE_DATA
        public const UInt32 HKCC = 0x80000005; // HKEY_CURRENT_CONFIG

        public static void HandleWin32Exception(string message)
        {
            int errorCode = Marshal.GetLastWin32Error();
            Win32Exception e = new Win32Exception(errorCode);
            throw new Win32Exception($"{message}, Error [{errorCode}, {e.Message}]");
        }

        public static void GetAdminPrivileges()
        {
            IntPtr hToken;
            TOKEN_PRIVILEGES pRestoreToken = new TOKEN_PRIVILEGES();
            TOKEN_PRIVILEGES pBackupToken = new TOKEN_PRIVILEGES();
            LUID restoreLUID;
            LUID backupLUID;

            if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out hToken))
                HandleWin32Exception("OpenProcessToken failed");

            if (!LookupPrivilegeValue(null, "SeRestorePrivilege", out restoreLUID))
                HandleWin32Exception("LookupPrivilegeValue failed");

            if (!LookupPrivilegeValue(null, "SeBackupPrivilege", out backupLUID))
                HandleWin32Exception("LookupPrivilegeValue failed");

            pRestoreToken.Count = 1;
            pRestoreToken.Luid = restoreLUID;
            pRestoreToken.Attr = SE_PRIVILEGE_ENABLED;

            pBackupToken.Count = 1;
            pBackupToken.Luid = backupLUID;
            pBackupToken.Attr = SE_PRIVILEGE_ENABLED;

            if (!AdjustTokenPrivileges(hToken, false, ref pRestoreToken, 0, IntPtr.Zero, IntPtr.Zero))
                HandleWin32Exception("AdjustTokenPrivileges failed");
            if (Marshal.GetLastWin32Error() == ResultWin32.ERROR_NOT_ALL_ASSIGNED)
                throw new Win32Exception($"AdjustTokenPrivileges failed, Try running this program with Administrator privilege.");
            CloseHandle(hToken);

            if (!AdjustTokenPrivileges(hToken, false, ref pBackupToken, 0, IntPtr.Zero, IntPtr.Zero))
                HandleWin32Exception("AdjustTokenPrivileges failed");
            if (Marshal.GetLastWin32Error() == ResultWin32.ERROR_NOT_ALL_ASSIGNED)
                throw new Win32Exception($"AdjustTokenPrivileges failed, Try running this program with Administrator privilege.");
            CloseHandle(hToken);
        }

        public static RegistryKey ParseRootKeyToRegKey(string rootKey)
        {
            return InternalParseRootKeyToRegKey(rootKey, false);
        }

        public static RegistryKey ParseRootKeyToRegKey(string rootKey, bool exception)
        {
            return InternalParseRootKeyToRegKey(rootKey, exception);
        }

        public static RegistryKey InternalParseRootKeyToRegKey(string rootKey, bool exception)
        { 
            RegistryKey regRoot;
            if (string.Equals(rootKey, "HKCR", StringComparison.OrdinalIgnoreCase))
                regRoot = Registry.ClassesRoot; // HKEY_CLASSES_ROOT
            else if (string.Equals(rootKey, "HKCU", StringComparison.OrdinalIgnoreCase))
                regRoot = Registry.CurrentUser; // HKEY_CURRENT_USER
            else if (string.Equals(rootKey, "HKLM", StringComparison.OrdinalIgnoreCase))
                regRoot = Registry.LocalMachine; // HKEY_LOCAL_MACHINE
            else if (string.Equals(rootKey, "HKU", StringComparison.OrdinalIgnoreCase))
                regRoot = Registry.Users; // HKEY_USERS
            else if (string.Equals(rootKey, "HKPD", StringComparison.OrdinalIgnoreCase))
                regRoot = Registry.PerformanceData; // HKEY_PERFORMANCE_DATA
            else if (string.Equals(rootKey, "HKCC", StringComparison.OrdinalIgnoreCase))
                regRoot = Registry.CurrentConfig; // HKEY_CURRENT_CONFIG
            else
            {
                if (exception)
                    throw new InvalidRegistryKeyException();
                else
                    regRoot = null;
            }
            return regRoot;
        }

        public static UInt32 ParseRootKeyToUInt32(string rootKey)
        {
            return InternalParseRootKeyToUInt32(rootKey, false);
        }

        public static UInt32 ParseRootKeyToUInt32(string rootKey, bool exception)
        {
            return InternalParseRootKeyToUInt32(rootKey, exception);
        }

        public static UInt32 InternalParseRootKeyToUInt32(string rootKey, bool exception)
        {
            UInt32 hKey;
            if (string.Equals(rootKey, "HKCR", StringComparison.OrdinalIgnoreCase))
                hKey = HKCR;
            else if (string.Equals(rootKey, "HKCU", StringComparison.OrdinalIgnoreCase))
                hKey = HKCU;
            else if (string.Equals(rootKey, "HKLM", StringComparison.OrdinalIgnoreCase))
                hKey = HKLM;
            else if (string.Equals(rootKey, "HKU", StringComparison.OrdinalIgnoreCase))
                hKey = HKU;
            else if (string.Equals(rootKey, "HKPD", StringComparison.OrdinalIgnoreCase))
                hKey = HKPD;
            else if (string.Equals(rootKey, "HKCC", StringComparison.OrdinalIgnoreCase))
                hKey = HKCC;
            else
            {
                if (exception)
                    throw new InvalidRegistryKeyException();
                else
                    hKey = 0;
            }
            return hKey;
        }
    }
}
