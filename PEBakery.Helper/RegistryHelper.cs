/*
    Copyright (C) 2016-2018 Hajin Jang
    Licensed under MIT License.
 
    MIT License

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
*/

using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security;
// ReSharper disable FieldCanBeMadeReadOnly.Local
// ReSharper disable MemberCanBePrivate.Local
// ReSharper disable InconsistentNaming

namespace PEBakery.Helper
{
    #region RegistryHelper
    public static class RegistryHelper
    {
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();
        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, UInt32 DesiredAccess, out IntPtr TokenHandle);
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, out LUID lpLuid);
        [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(IntPtr htok, bool disableAllPrivileges, ref TOKEN_PRIVILEGES newState, UInt32 len, IntPtr prev, IntPtr relen);
        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern int RegLoadKey(SafeRegistryHandle hKey, string lpSubKey, string lpFile);
        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern int RegUnLoadKey(SafeRegistryHandle hKey, string lpSubKey);
        [DllImport("kernel32.dll", SetLastError = true)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        [SuppressUnmanagedCodeSecurity]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }
        // ReSharper disable once UnusedMember.Local
        [StructLayout(LayoutKind.Sequential)]
        private struct LUID_AND_ATTRIBUTES
        {
            public LUID pLuid;
            public uint Attributes;
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct TOKEN_PRIVILEGES
        {
            public int Count;
            public LUID Luid;
            public uint Attr;
        }

        // ReSharper disable once UnusedMember.Local
        private const int ANYSIZE_ARRAY = 1;
        private const uint SE_PRIVILEGE_ENABLED = 0x00000002;
        private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        private const uint TOKEN_QUERY = 0x0008;

        /*
        public const UInt32 HKCR = 0x80000000; // HKEY_CLASSES_ROOT
        public const UInt32 HKCU = 0x80000001; // HKEY_CURRENT_USER
        public const UInt32 HKLM = 0x80000002; // HKEY_LOCAL_MACHINE
        public const UInt32 HKU = 0x80000003; // HKEY_USERS
        public const UInt32 HKPD = 0x80000004; // HKEY_PERFORMANCE_DATA
        public const UInt32 HKCC = 0x80000005; // HKEY_CURRENT_CONFIG
        */

        public static void GetAdminPrivileges()
        {
            TOKEN_PRIVILEGES pRestoreToken = new TOKEN_PRIVILEGES();
            TOKEN_PRIVILEGES pBackupToken = new TOKEN_PRIVILEGES();

            if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out IntPtr hToken))
                throw new BetterWin32Errors.Win32Exception("OpenProcessToken failed");

            if (!LookupPrivilegeValue(null, "SeRestorePrivilege", out LUID restoreLUID))
                throw new BetterWin32Errors.Win32Exception("LookupPrivilegeValue failed");

            if (!LookupPrivilegeValue(null, "SeBackupPrivilege", out LUID backupLUID))
                throw new BetterWin32Errors.Win32Exception("LookupPrivilegeValue failed");

            pRestoreToken.Count = 1;
            pRestoreToken.Luid = restoreLUID;
            pRestoreToken.Attr = SE_PRIVILEGE_ENABLED;

            pBackupToken.Count = 1;
            pBackupToken.Luid = backupLUID;
            pBackupToken.Attr = SE_PRIVILEGE_ENABLED;

            if (!AdjustTokenPrivileges(hToken, false, ref pRestoreToken, 0, IntPtr.Zero, IntPtr.Zero))
            {
                BetterWin32Errors.Win32Error error = BetterWin32Errors.Win32Exception.GetLastWin32Error();
                if (error == BetterWin32Errors.Win32Error.ERROR_NOT_ALL_ASSIGNED)
                    throw new BetterWin32Errors.Win32Exception("AdjustTokenPrivileges failed, try running this program with Administrator privilege.");
                else
                    throw new BetterWin32Errors.Win32Exception("AdjustTokenPrivileges failed");
            }

            if (!AdjustTokenPrivileges(hToken, false, ref pBackupToken, 0, IntPtr.Zero, IntPtr.Zero))
            {
                BetterWin32Errors.Win32Error error = BetterWin32Errors.Win32Exception.GetLastWin32Error();
                if (error == BetterWin32Errors.Win32Error.ERROR_NOT_ALL_ASSIGNED)
                    throw new BetterWin32Errors.Win32Exception("AdjustTokenPrivileges failed, try running this program with Administrator privilege.");
                else
                    throw new BetterWin32Errors.Win32Exception("AdjustTokenPrivileges failed");
            }
            CloseHandle(hToken);
        }

        #region Parse
        public static RegistryKey ParseStringToRegKey(string rootKey)
        {
            RegistryKey regRoot;
            if (rootKey.Equals("HKCR", StringComparison.OrdinalIgnoreCase) ||
                rootKey.Equals("HKEY_CLASSES_ROOT", StringComparison.OrdinalIgnoreCase))
                regRoot = Registry.ClassesRoot; // HKEY_CLASSES_ROOT
            else if (rootKey.Equals("HKCU", StringComparison.OrdinalIgnoreCase) ||
                rootKey.Equals("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase))
                regRoot = Registry.CurrentUser; // HKEY_CURRENT_USER
            else if (rootKey.Equals("HKLM", StringComparison.OrdinalIgnoreCase) ||
                rootKey.Equals("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase))
                regRoot = Registry.LocalMachine; // HKEY_LOCAL_MACHINE
            else if (rootKey.Equals("HKU", StringComparison.OrdinalIgnoreCase) ||
                rootKey.Equals("HKEY_USERS", StringComparison.OrdinalIgnoreCase))
                regRoot = Registry.Users; // HKEY_USERS
            else if (rootKey.Equals("HKCC", StringComparison.OrdinalIgnoreCase) ||
                rootKey.Equals("HKEY_CURRENT_CONFIG", StringComparison.OrdinalIgnoreCase))
                regRoot = Registry.CurrentConfig; // HKEY_CURRENT_CONFIG
            else
                regRoot = null;
            return regRoot;
        }

        public static string RegKeyToString(RegistryKey regKey)
        {
            string rootKey;
            if (regKey == Registry.ClassesRoot)
                rootKey = "HKCR";
            else if (regKey == Registry.CurrentUser)
                rootKey = "HKCU";
            else if (regKey == Registry.LocalMachine)
                rootKey = "HKLM";
            else if (regKey == Registry.Users)
                rootKey = "HKU";
            else if (regKey == Registry.CurrentConfig)
                rootKey = "HKCC";
            else
                rootKey = null;
            return rootKey;
        }

        public static string RegKeyToFullString(RegistryKey regKey)
        {
            string rootKey;
            if (regKey == Registry.ClassesRoot)
                rootKey = "HKEY_CLASSES_ROOT";
            else if (regKey == Registry.CurrentUser)
                rootKey = "HKEY_CURRENT_USER";
            else if (regKey == Registry.LocalMachine)
                rootKey = "HKEY_LOCAL_MACHINE";
            else if (regKey == Registry.Users)
                rootKey = "HKEY_USERS";
            else if (regKey == Registry.CurrentConfig)
                rootKey = "HKEY_CURRENT_CONFIG";
            else
                rootKey = null;
            return rootKey;
        }

        public static SafeRegistryHandle ParseStringToHandle(string rootKey)
        {
            SafeRegistryHandle hKey;
            if (rootKey.Equals("HKCR", StringComparison.OrdinalIgnoreCase) ||
                rootKey.Equals("HKEY_CLASSES_ROOT", StringComparison.OrdinalIgnoreCase))
                hKey = Registry.ClassesRoot.Handle; // HKEY_CLASSES_ROOT
            else if (rootKey.Equals("HKCU", StringComparison.OrdinalIgnoreCase) ||
                rootKey.Equals("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase))
                hKey = Registry.CurrentUser.Handle; // HKEY_CURRENT_USER
            else if (rootKey.Equals("HKLM", StringComparison.OrdinalIgnoreCase) ||
                rootKey.Equals("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase))
                hKey = Registry.LocalMachine.Handle; // HKEY_LOCAL_MACHINE
            else if (rootKey.Equals("HKU", StringComparison.OrdinalIgnoreCase) ||
                rootKey.Equals("HKEY_USERS", StringComparison.OrdinalIgnoreCase))
                hKey = Registry.Users.Handle; // HKEY_USERS
            else if (rootKey.Equals("HKCC", StringComparison.OrdinalIgnoreCase) ||
                rootKey.Equals("HKEY_CURRENT_CONFIG", StringComparison.OrdinalIgnoreCase))
                hKey = Registry.CurrentConfig.Handle; // HKEY_CURRENT_CONFIG
            else
                hKey = new SafeRegistryHandle(IntPtr.Zero, true);
            return hKey;
        }
        #endregion

        #region CopySubKey
        public static void CopySubKey(RegistryKey srcKey, string srcSubKeyPath, RegistryKey destKey, string destSubKeyPath)
        {
            using (RegistryKey srcSubKey = srcKey.OpenSubKey(srcSubKeyPath, false))
            using (RegistryKey destSubKey = destKey.CreateSubKey(destSubKeyPath, true))
            {
                if (srcSubKey == null)
                    throw new ArgumentException($"Unable to find subkey [{srcSubKeyPath}]");
                if (destSubKey == null)
                    throw new ArgumentException($"Unalbe to create dest subkey [{destSubKeyPath}]");

                CopySubKey(srcSubKey, destSubKey);
            }
        }

        public static void CopySubKey(RegistryKey srcSubKey, RegistryKey destSubKey)
        {
            if (srcSubKey == null)
                throw new ArgumentNullException(nameof(srcSubKey));
            if (destSubKey == null)
                throw new ArgumentNullException(nameof(destSubKey));

            foreach (string valueName in srcSubKey.GetValueNames())
            {
                RegistryValueKind kind = srcSubKey.GetValueKind(valueName);
                object value = srcSubKey.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                destSubKey.SetValue(valueName, value, kind);
            }

            foreach (string subKeyName in srcSubKey.GetSubKeyNames())
            {
                using (RegistryKey copySrcSubKey = srcSubKey.OpenSubKey(subKeyName, false))
                using (RegistryKey copyDestSubKey = destSubKey.CreateSubKey(subKeyName, true))
                {
                    CopySubKey(copySrcSubKey, copyDestSubKey);
                }
            }
        }
        #endregion

        #region GetDefaultExecutablePath, GetDefaultWebBrowserPath
        private static readonly Dictionary<string, string> DefaultWebBrowsers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> DefaultExecutables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> DefaultOpenCommands = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static string GetDefaultExecutablePath(string ext, bool onlyExePath)
        {
            const string exePathKeyTemplate = @"{0}\shell\open\command";

            if (onlyExePath)
            {
                if (DefaultExecutables.ContainsKey(ext))
                    return DefaultExecutables[ext];
            }
            else
            {
                if (DefaultOpenCommands.ContainsKey(ext))
                    return DefaultOpenCommands[ext];
            }

            RegistryKey extSubKey = null;
            RegistryKey exePathSubKey = null;
            try
            {
                extSubKey = Registry.ClassesRoot.OpenSubKey(ext, false);
                if (extSubKey == null)
                    return null;

                if (!(extSubKey.GetValue(null, null) is string progId))
                    return null;

                string exePathKey = string.Format(exePathKeyTemplate, progId);
                exePathSubKey = Registry.ClassesRoot.OpenSubKey(exePathKey, false);
                if (exePathSubKey == null)
                    return null;

                if (!(exePathSubKey.GetValue(null, null) is string exePath))
                    return null;

                if (onlyExePath)
                {
                    int idx = exePath.LastIndexOf(".exe", StringComparison.OrdinalIgnoreCase) + 4;
                    exePath = exePath.Substring(0, idx).Trim().Trim('\"').Trim();
                    DefaultExecutables[ext] = exePath;
                    return exePath;
                }
                else
                {
                    DefaultOpenCommands[ext] = exePath;
                    return exePath;
                }
            }
            finally
            {
                extSubKey?.Close();
                exePathSubKey?.Close();
            }
        }

        public static string GetDefaultWebBrowserPath(string protocol, bool onlyExePath)
        {
            const string progIdKey = "ProgId";
            const string httpsDefaultKey = @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoice";
            const string exePathKeyTemplate = @"{0}\shell\open\command";

            if (onlyExePath)
            {
                if (DefaultWebBrowsers.ContainsKey(protocol))
                    return DefaultWebBrowsers[protocol];
            }
            else
            {
                if (DefaultOpenCommands.ContainsKey(protocol))
                    return DefaultOpenCommands[protocol];
            }

            RegistryKey httpsSubKey = null;
            RegistryKey exePathSubKey = null;
            try
            {
                httpsSubKey = Registry.CurrentUser.OpenSubKey(httpsDefaultKey, false);
                if (httpsSubKey == null)
                {
                    httpsSubKey = Registry.LocalMachine.OpenSubKey(httpsDefaultKey, false);
                    if (httpsSubKey == null)
                        return null;
                }

                if (!(httpsSubKey.GetValue(progIdKey, null) is string progId))
                    return null;

                string exePathKey = string.Format(exePathKeyTemplate, progId);
                exePathSubKey = Registry.ClassesRoot.OpenSubKey(exePathKey, false);
                if (exePathSubKey == null)
                    return null;

                if (!(exePathSubKey.GetValue(null, null) is string browserPath))
                    return null;

                if (onlyExePath)
                {
                    int idx = browserPath.LastIndexOf(".exe", StringComparison.OrdinalIgnoreCase) + 4;
                    browserPath = browserPath.Substring(0, idx).Trim().Trim('\"').Trim();
                    DefaultWebBrowsers[protocol] = browserPath;
                    return browserPath;
                }
                else
                {
                    DefaultOpenCommands[protocol] = browserPath;
                    return browserPath;
                }

            }
            finally
            {
                httpsSubKey?.Close();
                exePathSubKey?.Close();
            }
        }
        #endregion
    }
    #endregion
}
