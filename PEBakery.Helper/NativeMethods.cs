using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
// ReSharper disable IdentifierTypo

namespace PEBakery.Helper
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public class NativeMethods
    {
        #region FileHelper
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, SHFILEINFO psfi, uint cbFileInfo, uint uFalgs);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal class SHFILEINFO
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

        #region FontHelper
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        internal class LOGFONT
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

        internal enum LogFontWeight
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

        internal enum LogFontCharSet : byte
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

        internal enum LogFontPrecision : byte
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

        internal enum LogFontClipPrecision : byte
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

        internal enum LogFontQuality : byte
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
        internal enum LogFontPitchAndFamily : byte
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

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        internal struct CHOOSEFONT
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
        internal enum ChooseFontFlags
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

        [DllImport("comdlg32.dll", CharSet = CharSet.Auto, EntryPoint = "ChooseFont", SetLastError = true)]
        internal static extern bool ChooseFont([In, Out] ref CHOOSEFONT lpcf);
        #endregion

        #region StringHelper
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        internal static extern int StrCmpLogical(string psz1, string psz2);
        #endregion

        #region EncodingHelper
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetACP();
        #endregion
    }
}
