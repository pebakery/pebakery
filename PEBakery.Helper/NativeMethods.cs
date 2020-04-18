/*
    Copyright (C) 2016-2020 Hajin Jang
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

using Microsoft.Win32.SafeHandles;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

// ReSharper disable IdentifierTypo

namespace PEBakery.Helper
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    internal static class NativeMethods
    {
        #region FileHelper
        #region DOS 8.3 Path
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int GetShortPathName(
            [MarshalAs(UnmanagedType.LPWStr)] string longPath,
            [MarshalAs(UnmanagedType.LPWStr)] StringBuilder shortPath,
            int cchBuffer
        );

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int GetLongPathName(
            [MarshalAs(UnmanagedType.LPWStr)] string shortPath,
            [MarshalAs(UnmanagedType.LPWStr)] StringBuilder longPath,
            int cchBuffer
        );
        #endregion
        #endregion

        #region FontHelper
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public class LOGFONT
        {
            public int lfHeight;
            public int lfWidth;
            public int lfEscapement;
            public int lfOrientation;
            public LogFontWeight lfWeight;
            [MarshalAs(UnmanagedType.U1)]
            public bool lfItalic;
            [MarshalAs(UnmanagedType.U1)]
            public bool lfUnderline;
            [MarshalAs(UnmanagedType.U1)]
            public bool lfStrikeOut;
            public LogFontCharSet lfCharSet;
            public LogFontPrecision lfOutPrecision;
            public LogFontClipPrecision lfClipPrecision;
            public LogFontQuality lfQuality;
            public LogFontPitchAndFamily lfPitchAndFamily;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string lfFaceName;
        }

        public enum LogFontWeight
        {
            FW_DONTCARE = 0,
            FW_THIN = 100,
            FW_EXTRALIGHT = 200,
            FW_LIGHT = 300,
            FW_REGULAR = 400,
            FW_MEDIUM = 500,
            FW_SEMIBOLD = 600,
            FW_BOLD = 700,
            FW_EXTRABOLD = 800,
            FW_HEAVY = 900,
        }

        public enum LogFontCharSet : byte
        {
            ANSI_CHARSET = 0,
            DEFAULT_CHARSET = 1,
            SYMBOL_CHARSET = 2,
            SHIFTJIS_CHARSET = 128,
            HANGEUL_CHARSET = 129,
            HANGUL_CHARSET = 129,
            GB2312_CHARSET = 134,
            CHINESEBIG5_CHARSET = 136,
            OEM_CHARSET = 255,
            JOHAB_CHARSET = 130,
            HEBREW_CHARSET = 177,
            ARABIC_CHARSET = 178,
            GREEK_CHARSET = 161,
            TURKISH_CHARSET = 162,
            VIETNAMESE_CHARSET = 163,
            THAI_CHARSET = 222,
            EASTEUROPE_CHARSET = 238,
            RUSSIAN_CHARSET = 204,
            MAC_CHARSET = 77,
            BALTIC_CHARSET = 186,
        }

        public enum LogFontPrecision : byte
        {
            OUT_DEFAULT_PRECIS = 0,
            OUT_STRING_PRECIS = 1,
            OUT_CHARACTER_PRECIS = 2,
            OUT_STROKE_PRECIS = 3,
            OUT_TT_PRECIS = 4,
            OUT_DEVICE_PRECIS = 5,
            OUT_RASTER_PRECIS = 6,
            OUT_TT_ONLY_PRECIS = 7,
            OUT_OUTLINE_PRECIS = 8,
            OUT_SCREEN_OUTLINE_PRECIS = 9,
            OUT_PS_ONLY_PRECIS = 10,
        }

        public enum LogFontClipPrecision : byte
        {
            CLIP_DEFAULT_PRECIS = 0,
            CLIP_CHARACTER_PRECIS = 1,
            CLIP_STROKE_PRECIS = 2,
            CLIP_MASK = 0xf,
            CLIP_LH_ANGLES = 1 << 4,
            CLIP_TT_ALWAYS = 2 << 4,
            CLIP_DFA_DISABLE = 4 << 4,
            CLIP_EMBEDDED = 8 << 4,
        }

        public enum LogFontQuality : byte
        {
            DEFAULT_QUALITY = 0,
            DRAFT_QUALITY = 1,
            PROOF_QUALITY = 2,
            NONANTIALIASED_QUALITY = 3,
            ANTIALIASED_QUALITY = 4,
            CLEARTYPE_QUALITY = 5,
            CLEARTYPE_NATURAL_QUALITY = 6,
        }

        [Flags]
        public enum LogFontPitchAndFamily : byte
        {
            DEFAULT_PITCH = 0,
            FIXED_PITCH = 1,
            VARIABLE_PITCH = 2,
            FF_DONTCARE = 0 << 4,
            FF_ROMAN = 1 << 4,
            FF_SWISS = 2 << 4,
            FF_MODERN = 3 << 4,
            FF_SCRIPT = 4 << 4,
            FF_DECORATIVE = 5 << 4,
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct CHOOSEFONT
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hDC;
            public IntPtr lpLogFont;
            public int iPointSize;
            public ChooseFontFlags Flags;
            public int rgbColors;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpTemplateName;
            public IntPtr hInstance;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpszStyle;
            public short nFontType;
            private readonly short _alignment;
            public int nSizeMin;
            public int nSizeMax;
        }

        [Flags]
        public enum ChooseFontFlags
        {
            CF_SCREENFONTS = 0x00000001,
            CF_PRINTERFONTS = 0x00000002,
            CF_BOTH = CF_SCREENFONTS | CF_PRINTERFONTS,
            CF_SHOWHELP = 0x00000004,
            CF_ENABLEHOOK = 0x00000008,
            CF_ENABLETEMPLATE = 0x00000010,
            CF_ENABLETEMPLATEHANDLE = 0x00000020,
            CF_INITTOLOGFONTSTRUCT = 0x00000040,
            CF_USESTYLE = 0x00000080,
            CF_EFFECTS = 0x00000100,
            CF_APPLY = 0x00000200,
            CF_ANSIONLY = 0x00000400,
            CF_SCRIPTSONLY = CF_ANSIONLY,
            CF_NOVECTORFONTS = 0x00000800,
            CF_NOOEMFONTS = CF_NOVECTORFONTS,
            CF_NOSIMULATIONS = 0x00001000,
            CF_LIMITSIZE = 0x00002000,
            CF_FIXEDPITCHONLY = 0x00004000,
            CF_WYSIWYG = 0x00008000,
            CF_FORCEFONTEXIST = 0x00010000,
            CF_SCALABLEONLY = 0x00020000,
            CF_TTONLY = 0x00040000,
            CF_NOFACESEL = 0x00080000,
            CF_NOSTYLESEL = 0x00100000,
            CF_NOSIZESEL = 0x00200000,
            CF_SELECTSCRIPT = 0x00400000,
            CF_NOSCRIPTSEL = 0x00800000,
            CF_NOVERTFONTS = 0x01000000,
            CF_INACTIVEFONTS = 0x02000000
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, EntryPoint = "ChooseFont", SetLastError = true)]
        public static extern bool ChooseFont([In, Out] ref CHOOSEFONT lpcf);
        #endregion

        #region StringHelper
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        public static extern int StrCmpLogical(string psz1, string psz2);
        #endregion

        #region EncodingHelper
        public const uint CP_ACP = 0;
        public const uint WC_NO_BEST_FIT_CHARS = 0x00000400;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetACP();

        [DllImport("kernel32.dll")]
        public static extern unsafe int WideCharToMultiByte(
            uint codePage,
            uint dwFlags,
            [MarshalAs(UnmanagedType.LPWStr)] string lpWideCharStr,
            int cchWideChar,
            [MarshalAs(UnmanagedType.LPArray)] byte[] lpMultiByteStr,
            int cbMultiByte,
            byte* lpDefaultChar,
            int* lpUsedDefaultChar);
        #endregion

        #region RegistryHelper
        [DllImport("kernel32.dll")]
        public static extern IntPtr GetCurrentProcess();

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, out LUID lpLuid);

        [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern bool AdjustTokenPrivileges(IntPtr htok, bool disableAllPrivileges, ref TOKEN_PRIVILEGES newState, uint len, IntPtr prev, IntPtr relen);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int RegLoadKey(SafeRegistryHandle hKey, string lpSubKey, string lpFile);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int RegUnLoadKey(SafeRegistryHandle hKey, string lpSubKey);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern unsafe int RegQueryValueEx(SafeRegistryHandle hKey, string lpValueName, uint* lpReserved, uint* lpType, void* lpData, uint* lpcbData);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern unsafe int RegSetValueEx(SafeRegistryHandle hKey, string lpValueName, uint reserved, uint dwType, void* lpData, uint cbData);

        [DllImport("shlwapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int SHCopyKey(SafeRegistryHandle hKeySrc, string pszSrcSubKey, SafeRegistryHandle hKeyDest, uint reserved);

        [DllImport("kernel32.dll", SetLastError = true)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        [SuppressUnmanagedCodeSecurity]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        public struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }
        // ReSharper disable once UnusedMember.Local
        [StructLayout(LayoutKind.Sequential)]
        public struct LUID_AND_ATTRIBUTES
        {
            public LUID pLuid;
            public uint Attributes;
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct TOKEN_PRIVILEGES
        {
            public int Count;
            public LUID Luid;
            public uint Attr;
        }

        // ReSharper disable once UnusedMember.Local
        public const uint SE_PRIVILEGE_ENABLED = 0x00000002;
        public const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        public const uint TOKEN_QUERY = 0x0008;

        /*
        public const UInt32 HKCR = 0x80000000; // HKEY_CLASSES_ROOT
        public const UInt32 HKCU = 0x80000001; // HKEY_CURRENT_USER
        public const UInt32 HKLM = 0x80000002; // HKEY_LOCAL_MACHINE
        public const UInt32 HKU = 0x80000003; // HKEY_USERS
        public const UInt32 HKPD = 0x80000004; // HKEY_PERFORMANCE_DATA
        public const UInt32 HKCC = 0x80000005; // HKEY_CURRENT_CONFIG
        */
        #endregion

        #region SystemHelper
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool GlobalMemoryStatusEx(MEMORYSTATUSEX lpBuffer);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;

            public MEMORYSTATUSEX()
            {
                dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }
        #endregion
    }

    #region Win32 System Error Codes
    public static class WindowsErrorCode
    {
        public const int ERROR_SUCCESS = 0;
        public const int ERROR_FILE_NOT_FOUND = 2;
        public const int ERROR_NOT_ALL_ASSIGNED = 1300;
    }
    #endregion
}
