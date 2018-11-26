using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace PEBakery.Helper
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class NativeMethods
    {
        #region FileHelper
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, SHFILEINFO psfi, uint cbFileInfo, uint uFalgs);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public class SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        #region DOS 8.3 Path
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern int GetShortPathName(
            [MarshalAs(UnmanagedType.LPTStr)] string longPath,
            [MarshalAs(UnmanagedType.LPTStr)] StringBuilder shortPath,
            int cchBuffer
        );

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern int GetLongPathName(
            [MarshalAs(UnmanagedType.LPTStr)] string shortPath,
            [MarshalAs(UnmanagedType.LPTStr)] StringBuilder longPath,
            int cchBuffer
        );
        #endregion
        #endregion

        #region StringHelper
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        public static extern int StrCmpLogical(string psz1, string psz2);
        #endregion

        #region EncodingHelper
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetACP();
        #endregion
    }
}
