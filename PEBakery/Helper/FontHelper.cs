/*
    Copyright (C) 2016-2017 Hajin Jang
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

using System;
using System.Linq;
using System.Windows;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace PEBakery.Helper
{
    #region FontHelper
    public static class FontHelper
    {
        // if we specify CharSet.Auto instead of CharSet.Ansi, then the string will be unreadable
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
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

        public enum LogFontWeight : int
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
            CLIP_LH_ANGLES = (1 << 4),
            CLIP_TT_ALWAYS = (2 << 4),
            CLIP_DFA_DISABLE = (4 << 4),
            CLIP_EMBEDDED = (8 << 4),
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
            FF_DONTCARE = (0 << 4),
            FF_ROMAN = (1 << 4),
            FF_SWISS = (2 << 4),
            FF_MODERN = (3 << 4),
            FF_SCRIPT = (4 << 4),
            FF_DECORATIVE = (5 << 4),
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
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
            private short _alignment;
            public int nSizeMin;
            public int nSizeMax;
        }

        [Flags]
        public enum ChooseFontFlags : int
        {
            CF_SCREENFONTS = 0x00000001,
            CF_PRINTERFONTS = 0x00000002,
            CF_BOTH = (CF_SCREENFONTS | CF_PRINTERFONTS),
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
        public static extern bool ChooseFont([In, Out] ref CHOOSEFONT lpcf);

        public static LogFontWeight FontWeightConvert_WPFToLogFont(System.Windows.FontWeight weight)
        {
            if (weight == FontWeights.Thin)
                return LogFontWeight.FW_THIN;
            else if (weight == FontWeights.ExtraLight || weight == FontWeights.UltraLight)
                return LogFontWeight.FW_EXTRALIGHT;
            else if (weight == FontWeights.Light)
                return LogFontWeight.FW_LIGHT;
            else if (weight == FontWeights.Regular || weight == FontWeights.Normal)
                return LogFontWeight.FW_REGULAR;
            else if (weight == FontWeights.Medium)
                return LogFontWeight.FW_MEDIUM;
            else if (weight == FontWeights.SemiBold || weight == FontWeights.DemiBold)
                return LogFontWeight.FW_SEMIBOLD;
            else if (weight == FontWeights.Bold)
                return LogFontWeight.FW_BOLD;
            else if (weight == FontWeights.ExtraBold || weight == FontWeights.UltraBold)
                return LogFontWeight.FW_EXTRABOLD;
            else if (weight == FontWeights.Heavy || weight == FontWeights.Black)
                return LogFontWeight.FW_HEAVY;
            else
                return LogFontWeight.FW_REGULAR;
        }

        public static System.Windows.FontWeight FontWeightConvert_LogFontToWPF(LogFontWeight enumWeight)
        {
            switch (enumWeight)
            {
                case LogFontWeight.FW_THIN:
                    return FontWeights.Thin;
                case LogFontWeight.FW_EXTRALIGHT:
                    return FontWeights.ExtraLight;
                case LogFontWeight.FW_LIGHT:
                    return FontWeights.Light;
                case LogFontWeight.FW_REGULAR:
                    return FontWeights.Regular;
                case LogFontWeight.FW_MEDIUM:
                    return FontWeights.Medium;
                case LogFontWeight.FW_SEMIBOLD:
                    return FontWeights.SemiBold;
                case LogFontWeight.FW_BOLD:
                    return FontWeights.Bold;
                case LogFontWeight.FW_EXTRABOLD:
                    return FontWeights.ExtraBold;
                case LogFontWeight.FW_HEAVY:
                    return FontWeights.Heavy;
                default:
                    return FontWeights.Regular;
            }
        }

        public static System.Windows.FontWeight FontWeightConvert_StringToWPF(string str)
        {
            if (str.Equals("Thin", StringComparison.OrdinalIgnoreCase))
                return FontWeights.Thin;
            else if (str.Equals("ExtraLight", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("UltraLight", StringComparison.OrdinalIgnoreCase))
                return FontWeights.ExtraLight;
            else if (str.Equals("Light", StringComparison.OrdinalIgnoreCase))
                return FontWeights.Light;
            else if (str.Equals("Regular", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("Normal", StringComparison.OrdinalIgnoreCase))
                return FontWeights.Regular;
            else if (str.Equals("Medium", StringComparison.OrdinalIgnoreCase))
                return FontWeights.Medium;
            else if (str.Equals("SemiBold", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("DemiBold", StringComparison.OrdinalIgnoreCase))
                return FontWeights.SemiBold;
            else if (str.Equals("Bold", StringComparison.OrdinalIgnoreCase))
                return FontWeights.Bold;
            else if (str.Equals("ExtraBold", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("UltraBold", StringComparison.OrdinalIgnoreCase))
                return FontWeights.ExtraBold;
            else if (str.Equals("Heavy", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("Black", StringComparison.OrdinalIgnoreCase))
                return FontWeights.Heavy;
            else
                return FontWeights.Regular;
        }

        public struct WPFFont
        {
            public System.Windows.Media.FontFamily FontFamily;
            public System.Windows.FontWeight FontWeight;
            public int FontSizeInPoint; // In Point (72DPI)
            public int Win32FontSize => -(int)Math.Round(FontSizeInPoint * 96 / 72f);
            public double FontSizeInDIP => FontSizeInPoint * 96 / 72f; // Device Independent Pixel (96DPI)

            public WPFFont(System.Windows.Media.FontFamily fontFamily, System.Windows.FontWeight fontWeight, int fontSize)
            {
                FontFamily = fontFamily;
                FontWeight = fontWeight;
                FontSizeInPoint = fontSize;
            }
        }

        public static WPFFont ChooseFontDialog(WPFFont font, Window window, bool useStyle = false, bool monospace = false)
        {
            LOGFONT logFont = new LOGFONT()
            {
                lfCharSet = LogFontCharSet.DEFAULT_CHARSET,
                lfPitchAndFamily = LogFontPitchAndFamily.DEFAULT_PITCH | LogFontPitchAndFamily.FF_DONTCARE,
                lfFaceName = font.FontFamily.Source,
                lfWeight = FontWeightConvert_WPFToLogFont(font.FontWeight),
                lfHeight = font.Win32FontSize,
            };
            IntPtr pLogFont = Marshal.AllocHGlobal(Marshal.SizeOf(logFont));
            Marshal.StructureToPtr(logFont, pLogFont, false);

            CHOOSEFONT chooseFont = new CHOOSEFONT()
            {
                hwndOwner = new WindowInteropHelper(window).Handle,
                lpLogFont = pLogFont,
                Flags = (ChooseFontFlags.CF_SCREENFONTS
                 | ChooseFontFlags.CF_FORCEFONTEXIST
                 | ChooseFontFlags.CF_INITTOLOGFONTSTRUCT // Use LOGFONT
                 | ChooseFontFlags.CF_SCALABLEONLY),
            };
            if (monospace)
                chooseFont.Flags |= ChooseFontFlags.CF_FIXEDPITCHONLY;
            if (useStyle)
                chooseFont.Flags |= ChooseFontFlags.CF_EFFECTS;
            chooseFont.lStructSize = Marshal.SizeOf(chooseFont);

            bool result = ChooseFont(ref chooseFont);
            Marshal.PtrToStructure(pLogFont, logFont);

            System.Windows.Media.FontFamily fontFamily = new System.Windows.Media.FontFamily(logFont.lfFaceName);
            System.Windows.FontWeight fontWeight = FontWeightConvert_LogFontToWPF(logFont.lfWeight);
            int fontSize = -(int) Math.Round(logFont.lfHeight * 72 / 96f); // Point - 72DPI, Device Independent Pixel - 96DPI

            Marshal.FreeHGlobal(pLogFont);

            return new WPFFont(fontFamily, fontWeight, fontSize);
        }
    }
    #endregion
}
