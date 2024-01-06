/*
    Copyright (C) 2016-2023 Hajin Jang
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
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace PEBakery.Helper
{
    #region RegistryHelper
    public static class RegistryHelper
    {
        #region P/Invoke Methods
        public static void GetAdminPrivileges()
        {
            NativeMethods.TOKEN_PRIVILEGES pRestoreToken = new NativeMethods.TOKEN_PRIVILEGES();
            NativeMethods.TOKEN_PRIVILEGES pBackupToken = new NativeMethods.TOKEN_PRIVILEGES();

            // Because procHandle is a pseudo handle, it does not need to be closed.
            // https://docs.microsoft.com/en-us/windows/desktop/api/processthreadsapi/nf-processthreadsapi-getcurrentprocess#remarks
            IntPtr procHandle = NativeMethods.GetCurrentProcess();

            const uint desiredAccess = NativeMethods.TOKEN_ADJUST_PRIVILEGES | NativeMethods.TOKEN_QUERY;
            if (!NativeMethods.OpenProcessToken(procHandle, desiredAccess, out IntPtr hToken))
                throw new Win32Exception("OpenProcessToken failed");

            try
            {
                if (!NativeMethods.LookupPrivilegeValue(null, "SeRestorePrivilege", out NativeMethods.LUID restoreLuid))
                    throw new Win32Exception("LookupPrivilegeValue failed");

                if (!NativeMethods.LookupPrivilegeValue(null, "SeBackupPrivilege", out NativeMethods.LUID backupLuid))
                    throw new Win32Exception("LookupPrivilegeValue failed");

                pRestoreToken.Count = 1;
                pRestoreToken.Luid = restoreLuid;
                pRestoreToken.Attr = NativeMethods.SE_PRIVILEGE_ENABLED;

                pBackupToken.Count = 1;
                pBackupToken.Luid = backupLuid;
                pBackupToken.Attr = NativeMethods.SE_PRIVILEGE_ENABLED;

                if (!NativeMethods.AdjustTokenPrivileges(hToken, false, ref pRestoreToken, 0, IntPtr.Zero, IntPtr.Zero))
                {
                    int ret = Marshal.GetLastWin32Error();
                    if (ret == WindowsErrorCode.ERROR_NOT_ALL_ASSIGNED)
                        throw new Win32Exception(ret, "AdjustTokenPrivileges failed, try running this program with Administrator privilege.");
                    else
                        throw new Win32Exception(ret, "AdjustTokenPrivileges failed");
                }

                if (!NativeMethods.AdjustTokenPrivileges(hToken, false, ref pBackupToken, 0, IntPtr.Zero, IntPtr.Zero))
                {
                    int ret = Marshal.GetLastWin32Error();
                    if (ret == WindowsErrorCode.ERROR_NOT_ALL_ASSIGNED)
                        throw new Win32Exception(ret, "AdjustTokenPrivileges failed, try running this program with Administrator privilege.");
                    else
                        throw new Win32Exception(ret, "AdjustTokenPrivileges failed");
                }
            }
            finally
            {
                NativeMethods.CloseHandle(hToken);
            }
        }

        public static int RegLoadKey(SafeRegistryHandle hKey, string lpSubKey, string lpFile)
        {
            return NativeMethods.RegLoadKey(hKey, lpSubKey, lpFile);
        }

        public static int RegUnLoadKey(SafeRegistryHandle hKey, string lpSubKey)
        {
            return NativeMethods.RegUnLoadKey(hKey, lpSubKey);
        }
        #endregion

        #region Parse
        [SupportedOSPlatform("windows")]
        public static RegistryHive? ParseStringToRegHive(string rootKey)
        {
            RegistryHive? regHive;
            if (rootKey.Equals("HKCR", StringComparison.OrdinalIgnoreCase) ||
                rootKey.Equals("HKEY_CLASSES_ROOT", StringComparison.OrdinalIgnoreCase))
                regHive = RegistryHive.ClassesRoot; // HKEY_CLASSES_ROOT
            else if (rootKey.Equals("HKCU", StringComparison.OrdinalIgnoreCase) ||
                rootKey.Equals("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase))
                regHive = RegistryHive.CurrentUser; // HKEY_CURRENT_USER
            else if (rootKey.Equals("HKLM", StringComparison.OrdinalIgnoreCase) ||
                rootKey.Equals("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase))
                regHive = RegistryHive.LocalMachine; // HKEY_LOCAL_MACHINE
            else if (rootKey.Equals("HKU", StringComparison.OrdinalIgnoreCase) ||
                rootKey.Equals("HKEY_USERS", StringComparison.OrdinalIgnoreCase))
                regHive = RegistryHive.Users; // HKEY_USERS
            else if (rootKey.Equals("HKCC", StringComparison.OrdinalIgnoreCase) ||
                rootKey.Equals("HKEY_CURRENT_CONFIG", StringComparison.OrdinalIgnoreCase))
                regHive = RegistryHive.CurrentConfig; // HKEY_CURRENT_CONFIG
            else
                regHive = null;
            return regHive;
        }

        [SupportedOSPlatform("windows")]
        public static RegistryKey? ParseStringToRegKey(string rootKey)
        {
            RegistryKey? regRoot;
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

        [SupportedOSPlatform("windows")]
        public static string? RegHiveToString(RegistryHive regHive)
        {
            string? rootKey;
            if (regHive == RegistryHive.ClassesRoot)
                rootKey = "HKCR";
            else if (regHive == RegistryHive.CurrentUser)
                rootKey = "HKCU";
            else if (regHive == RegistryHive.LocalMachine)
                rootKey = "HKLM";
            else if (regHive == RegistryHive.Users)
                rootKey = "HKU";
            else if (regHive == RegistryHive.CurrentConfig)
                rootKey = "HKCC";
            else
                rootKey = null;
            return rootKey;
        }

        [SupportedOSPlatform("windows")]
        public static string? RegKeyToString(RegistryKey regKey)
        {
            string? rootKey;
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

        [SupportedOSPlatform("windows")]
        public static string? RegKeyToFullString(RegistryKey regKey)
        {
            string? rootKey;
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

        [SupportedOSPlatform("windows")]
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

        public static RegistryValueKind? ParseValueKind(string str)
        {
            if (str.Equals("REG_BINARY", StringComparison.OrdinalIgnoreCase))
                return RegistryValueKind.Binary;
            else if (str.Equals("REG_DWORD", StringComparison.OrdinalIgnoreCase))
                return RegistryValueKind.DWord;
            else if (str.Equals("REG_EXPAND_SZ", StringComparison.OrdinalIgnoreCase))
                return RegistryValueKind.ExpandString;
            else if (str.Equals("REG_MULTI_SZ", StringComparison.OrdinalIgnoreCase))
                return RegistryValueKind.MultiString;
            else if (str.Equals("REG_NONE", StringComparison.OrdinalIgnoreCase))
                return RegistryValueKind.None;
            else if (str.Equals("REG_QWORD", StringComparison.OrdinalIgnoreCase))
                return RegistryValueKind.QWord;
            else if (str.Equals("REG_SZ", StringComparison.OrdinalIgnoreCase))
                return RegistryValueKind.String;
            return null;
        }

        /// <summary>
        /// The dictionary to map RegistryValueKidn to WBInt values.
        /// WBInt value does not exactly map to RegistryValueKind, so maunal conversion is necessary.
        /// </summary>
        private static readonly Dictionary<RegistryValueKind, uint> ValueKindWBIntDict = new Dictionary<RegistryValueKind, uint>()
        {
            [RegistryValueKind.None] = 0,
            [RegistryValueKind.String] = 1,
            [RegistryValueKind.ExpandString] = 2,
            [RegistryValueKind.Binary] = 3,
            [RegistryValueKind.DWord] = 4,
            [RegistryValueKind.MultiString] = 7,
            [RegistryValueKind.QWord] = 11,
        };

        public static uint? ValueKindToWBInt(RegistryValueKind valueType)
        {
            if (ValueKindWBIntDict.ContainsKey(valueType))
                return ValueKindWBIntDict[valueType];    
            return null;
        }

        public static RegistryValueKind? WBIntToValudKind(uint wbInt)
        {
            foreach (var kv in ValueKindWBIntDict)
            {
                if (kv.Value == wbInt)
                    return kv.Key;
            }
            return null;
        }
        #endregion

        #region CopySubKey (SHCopyKey wrapper)
        public static void CopySubKey(RegistryKey srcKey, string srcSubKeyPath, RegistryKey destKey, string destSubKeyPath)
        {
            using (RegistryKey destSubKey = destKey.CreateSubKey(destSubKeyPath, true))
            {
                if (destSubKey == null)
                    throw new ArgumentException($"Unable to create dest subkey [{destSubKeyPath}]");

                int ret = NativeMethods.SHCopyKey(srcKey.Handle, srcSubKeyPath, destSubKey.Handle, 0);
                if (ret != WindowsErrorCode.ERROR_SUCCESS)
                    throw new Win32Exception(ret, "SHCopyKey failed");
            }
        }
        #endregion

        #region RegExistValue
        /// <summary>
        /// Wrapper of Win32 RegQueryValueEx, which bypass value type checking.
        /// </summary>
        public static unsafe bool RegExistValue(RegistryKey hKey, string subKeyPath, string valueName)
        {
            using (RegistryKey subKey = hKey.CreateSubKey(subKeyPath, false))
            {
                if (subKey == null)
                    return false;

                uint dataSize = 0;
                int ret = NativeMethods.RegQueryValueEx(subKey.Handle, valueName, null, null, null, &dataSize);
                return ret != WindowsErrorCode.ERROR_FILE_NOT_FOUND;
            }
        }
        #endregion

        #region RegGetValue, RegSetValue
        /// <summary>
        /// Wrapper of Win32 RegQueryValueEx, which bypass value type checking.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object? RegGetValue(RegistryKey hKey, string subKeyPath, string valueName, uint valueType)
        {
            return RegGetValue(hKey, subKeyPath, valueName, (RegistryValueKind)valueType);
        }

        /// <summary>
        /// Wrapper of Win32 RegQueryValueEx, which bypass value type checking.
        /// </summary>
        public static unsafe object? RegGetValue(RegistryKey hKey, string subKeyPath, string valueName, RegistryValueKind valueType)
        {
            static void CheckReturnValue(int ret)
            {
                if (ret != WindowsErrorCode.ERROR_SUCCESS)
                    throw new Win32Exception(ret, "RegQueryValueEx failed");
            }

            using (RegistryKey subKey = hKey.CreateSubKey(subKeyPath, false))
            {
                if (subKey == null)
                    throw new ArgumentException($"Unable to open subkey [{subKeyPath}]");

                if (valueType == RegistryValueKind.Unknown || !Enum.IsDefined(valueType))
                {
                    // We don't know how to interpret byte array into C# objects.
                    // Let's return raw byte array we received from RegQueryValueEx.

                    // Get required buffer size
                    uint dataSize = 0;
                    int ret = NativeMethods.RegQueryValueEx(subKey.Handle, valueName, null, null, null, &dataSize);
                    CheckReturnValue(ret);

                    // Get actual data
                    byte[] data = new byte[dataSize];
                    fixed (byte* dataPtr = data)
                    {
                        ret = NativeMethods.RegQueryValueEx(subKey.Handle, valueName, null, null, dataPtr, &dataSize);
                    }
                    CheckReturnValue(ret);

                    return data;
                }
                else
                {
                    object? value = subKey.GetValue(valueName);
                    return value;
                }
            }
        }

        /// <summary>
        /// Wrapper of Win32 RegSetValueEx, which bypass value type checking.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RegSetValue(RegistryKey key, string subKeyPath, string valueName, object value, RegistryValueKind valueType)
        {
            RegSetValue(key, subKeyPath, valueName, value, (uint)valueType);
        }

        /// <summary>
        /// Wrapper of Win32 RegSetValueEx, which bypass value type checking.
        /// </summary>
        public static unsafe void RegSetValue(RegistryKey key, string subKeyPath, string valueName, object value, uint valueType)
        {
            static void CheckReturnValue(int ret)
            {
                if (ret != WindowsErrorCode.ERROR_SUCCESS)
                    throw new Win32Exception(ret, "RegSetValueEx failed");
            }

            using (RegistryKey subKey = key.CreateSubKey(subKeyPath, true))
            {
                if (subKey == null)
                    throw new ArgumentException($"Unable to open subkey [{subKeyPath}]");

                int ret;
                if (value is int int32t)
                {
                    ret = NativeMethods.RegSetValueEx(subKey.Handle, valueName, 0, valueType, &int32t, sizeof(uint));
                }
                else if (value is uint uint32t)
                {
                    ret = NativeMethods.RegSetValueEx(subKey.Handle, valueName, 0, valueType, &uint32t, sizeof(uint));
                }
                else if (value is long int64t)
                {
                    ret = NativeMethods.RegSetValueEx(subKey.Handle, valueName, 0, valueType, &int64t, sizeof(ulong));
                }
                else if (value is ulong uint64t)
                {
                    ret = NativeMethods.RegSetValueEx(subKey.Handle, valueName, 0, valueType, &uint64t, sizeof(ulong));
                }
                else if (value is byte[] bytes)
                {
                    fixed (byte* bufPtr = bytes)
                    {
                        ret = NativeMethods.RegSetValueEx(subKey.Handle, valueName, 0, valueType, bufPtr, (uint)bytes.Length);
                    }
                }
                else if (value is string str)
                {
                    // For string-based types, such as REG_SZ, the string must be null-terminated. 
                    // https://docs.microsoft.com/en-us/windows/desktop/api/winreg/nf-winreg-regsetvalueexw

                    byte[] rawStr = Encoding.Unicode.GetBytes(str);

                    // Put arbitrary NULL at the end to the buffer
                    byte[] buffer = new byte[rawStr.Length + 2];
                    rawStr.CopyTo(buffer, 0);
                    buffer[rawStr.Length] = 0;
                    buffer[rawStr.Length + 1] = 0;

                    fixed (byte* bufPtr = buffer)
                    {
                        ret = NativeMethods.RegSetValueEx(subKey.Handle, valueName, 0, valueType, bufPtr, (uint)buffer.Length);
                    }
                }
                else if (value is string[] strs)
                {
                    // With the REG_MULTI_SZ data type, the string must be terminated with two null characters.
                    // https://docs.microsoft.com/en-us/windows/desktop/api/winreg/nf-winreg-regsetvalueexw

                    int bufferSize = 2; // +2 for last continued NULL char
                    byte[][] rawStrs = new byte[strs.Length][];
                    for (int i = 0; i < strs.Length; i++)
                    {
                        string s = strs[i];

                        byte[] rawStr = Encoding.Unicode.GetBytes(s);

                        rawStrs[i] = rawStr;
                        bufferSize += rawStr.Length + 2; // +2 for NULL char
                    }

                    // Put arbitrary NULL as a delimiter to the buffer
                    int bufPos = 0;
                    byte[] buffer = new byte[bufferSize];
                    foreach (byte[] rawStr in rawStrs)
                    {
                        rawStr.CopyTo(buffer, bufPos);
                        buffer[rawStr.Length] = 0;
                        buffer[rawStr.Length + 1] = 0;

                        bufPos += rawStr.Length + 2; // +2 for NULL char
                    }
                    // Add last continued NULL char
                    buffer[bufPos] = 0;
                    buffer[bufPos + 1] = 0;

                    fixed (byte* bufPtr = buffer)
                    {
                        ret = NativeMethods.RegSetValueEx(subKey.Handle, valueName, 0, valueType, bufPtr, (uint)buffer.Length);
                    }
                }
                else
                {
                    throw new ArgumentException($"Unsupported object type [{value.GetType()}]");
                }

                CheckReturnValue(ret);
            }
        }
        #endregion

        #region GetDefaultExecutablePath, GetDefaultWebBrowserPath
        [SupportedOSPlatform("windows")]
        public static string? GetDefaultExecutablePath(string ext, bool onlyExePath)
        {
            const string exePathKeyTemplate = @"{0}\shell\open\command";

            RegistryKey? extSubKey = null;
            RegistryKey? exePathSubKey = null;
            try
            {
                extSubKey = Registry.ClassesRoot.OpenSubKey(ext, false);
                if (extSubKey == null)
                    return null;

                if (extSubKey.GetValue(null, null) is not string progId)
                    return null;

                string exePathKey = string.Format(exePathKeyTemplate, progId);
                exePathSubKey = Registry.ClassesRoot.OpenSubKey(exePathKey, false);
                if (exePathSubKey == null)
                    return null;

                if (exePathSubKey.GetValue(null, null) is not string exePath)
                    return null;

                if (onlyExePath)
                {
                    int idx = exePath.LastIndexOf(".exe", StringComparison.OrdinalIgnoreCase) + 4;
                    exePath = exePath.AsSpan(0, idx).Trim().Trim('\"').Trim().ToString();
                    return exePath;
                }
                else
                {
                    return exePath;
                }
            }
            finally
            {
                extSubKey?.Close();
                exePathSubKey?.Close();
            }
        }

        [SupportedOSPlatform("windows")]
        public static string? GetDefaultWebBrowserPath(bool onlyExePath)
        {
            const string progIdKey = "ProgId";
            const string httpsDefaultKey = @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoice";
            const string exePathKeyTemplate = @"{0}\shell\open\command";

            RegistryKey? httpsSubKey = null;
            RegistryKey? exePathSubKey = null;
            try
            {
                httpsSubKey = Registry.CurrentUser.OpenSubKey(httpsDefaultKey, false);
                if (httpsSubKey == null)
                {
                    httpsSubKey = Registry.LocalMachine.OpenSubKey(httpsDefaultKey, false);
                    if (httpsSubKey == null)
                        return null;
                }

                if (httpsSubKey.GetValue(progIdKey, null) is not string progId)
                    return null;

                string exePathKey = string.Format(exePathKeyTemplate, progId);
                exePathSubKey = Registry.ClassesRoot.OpenSubKey(exePathKey, false);
                if (exePathSubKey == null)
                    return null;

                if (exePathSubKey.GetValue(null, null) is not string browserPath)
                    return null;

                if (onlyExePath)
                {
                    int idx = browserPath.LastIndexOf(".exe", StringComparison.OrdinalIgnoreCase) + 4;
                    browserPath = browserPath.AsSpan(0, idx).Trim().Trim('\"').Trim().ToString();
                    return browserPath;
                }
                else
                {
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
