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

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global
// ReSharper disable EnumUnderlyingTypeIsInt

namespace PEBakery.Helper
{
    #region FontHelper
    public static class FontHelper
    {
        #region (Private) Convert WPF's FontWeight to/from LogFontWeight
        private static NativeMethods.LogFontWeight ConvertToLogFontWeight(FontWeight wpfWeight)
        {
            NativeMethods.LogFontWeight enumWeight;
            if (wpfWeight == FontWeights.Thin)
                enumWeight = NativeMethods.LogFontWeight.FW_THIN;
            else if (wpfWeight == FontWeights.ExtraLight || wpfWeight == FontWeights.UltraLight)
                enumWeight = NativeMethods.LogFontWeight.FW_EXTRALIGHT;
            else if (wpfWeight == FontWeights.Light)
                enumWeight = NativeMethods.LogFontWeight.FW_LIGHT;
            else if (wpfWeight == FontWeights.Regular || wpfWeight == FontWeights.Normal)
                enumWeight = NativeMethods.LogFontWeight.FW_REGULAR;
            else if (wpfWeight == FontWeights.Medium)
                enumWeight = NativeMethods.LogFontWeight.FW_MEDIUM;
            else if (wpfWeight == FontWeights.SemiBold || wpfWeight == FontWeights.DemiBold)
                enumWeight = NativeMethods.LogFontWeight.FW_SEMIBOLD;
            else if (wpfWeight == FontWeights.Bold)
                enumWeight = NativeMethods.LogFontWeight.FW_BOLD;
            else if (wpfWeight == FontWeights.ExtraBold || wpfWeight == FontWeights.UltraBold)
                enumWeight = NativeMethods.LogFontWeight.FW_EXTRABOLD;
            else if (wpfWeight == FontWeights.Heavy || wpfWeight == FontWeights.Black)
                enumWeight = NativeMethods.LogFontWeight.FW_HEAVY;
            else
                enumWeight = NativeMethods.LogFontWeight.FW_REGULAR;
            return enumWeight;
        }

        private static FontWeight ConvertToWpfFontWeight(NativeMethods.LogFontWeight enumWeight)
        {
            FontWeight wpfWeight;
            switch (enumWeight)
            {
                case NativeMethods.LogFontWeight.FW_THIN:
                    wpfWeight = FontWeights.Thin;
                    break;
                case NativeMethods.LogFontWeight.FW_EXTRALIGHT:
                    wpfWeight = FontWeights.ExtraLight;
                    break;
                case NativeMethods.LogFontWeight.FW_LIGHT:
                    wpfWeight = FontWeights.Light;
                    break;
                case NativeMethods.LogFontWeight.FW_REGULAR:
                    wpfWeight = FontWeights.Regular;
                    break;
                case NativeMethods.LogFontWeight.FW_MEDIUM:
                    wpfWeight = FontWeights.Medium;
                    break;
                case NativeMethods.LogFontWeight.FW_SEMIBOLD:
                    wpfWeight = FontWeights.SemiBold;
                    break;
                case NativeMethods.LogFontWeight.FW_BOLD:
                    wpfWeight = FontWeights.Bold;
                    break;
                case NativeMethods.LogFontWeight.FW_EXTRABOLD:
                    wpfWeight = FontWeights.ExtraBold;
                    break;
                case NativeMethods.LogFontWeight.FW_HEAVY:
                    wpfWeight = FontWeights.Heavy;
                    break;
                default:
                    wpfWeight = FontWeights.Regular;
                    break;
            }

            return wpfWeight;
        }
        #endregion

        #region struct FontInfo, ChooseFontDialog
        public struct FontInfo
        {
            public FontFamily FontFamily;
            public FontWeight FontWeight;
            public int PointSize; // In Point (72DPI)
            public double DeviceIndependentPixelSize => PointSize * 96 / 72f; // Device Independent Pixel (96DPI)
            /// <summary>
            /// For LOGFONT struct
            /// </summary>
            public int Win32Size => -(int)Math.Round(PointSize * 96 / 72f);

            // Every Windows PC has Consolas pre-installed.
            public static FontInfo DefaultMonospaced => new FontInfo(new FontFamily("Consolas"), FontWeights.Regular, 12);

            public FontInfo(FontFamily fontFamily, FontWeight fontWeight, int fontSize)
            {
                FontFamily = fontFamily;
                FontWeight = fontWeight;
                PointSize = fontSize;
            }

            public override string ToString()
            {
                return $"{FontFamily.Source}, {PointSize}pt";
            }
        }

        public static FontInfo ChooseFontDialog(FontInfo font, Window window, bool useStyle = false, bool monospaced = false)
        {
            NativeMethods.LOGFONT logFont = new NativeMethods.LOGFONT
            {
                lfCharSet = NativeMethods.LogFontCharSet.DEFAULT_CHARSET,
                lfPitchAndFamily = NativeMethods.LogFontPitchAndFamily.DEFAULT_PITCH | NativeMethods.LogFontPitchAndFamily.FF_DONTCARE,
                lfFaceName = font.FontFamily.Source,
                lfWeight = ConvertToLogFontWeight(font.FontWeight),
                lfHeight = font.Win32Size,
            };
            IntPtr pLogFont = Marshal.AllocHGlobal(Marshal.SizeOf(logFont));
            Marshal.StructureToPtr(logFont, pLogFont, false);

            NativeMethods.CHOOSEFONT chosenFont = new NativeMethods.CHOOSEFONT
            {
                hwndOwner = new WindowInteropHelper(window).Handle,
                lpLogFont = pLogFont,
                Flags = NativeMethods.ChooseFontFlags.CF_SCREENFONTS |
                        NativeMethods.ChooseFontFlags.CF_FORCEFONTEXIST |
                        NativeMethods.ChooseFontFlags.CF_INITTOLOGFONTSTRUCT | // Use LOGFONT
                        NativeMethods.ChooseFontFlags.CF_SCALABLEONLY,
            };
            if (monospaced)
                chosenFont.Flags |= NativeMethods.ChooseFontFlags.CF_FIXEDPITCHONLY;
            if (useStyle)
                chosenFont.Flags |= NativeMethods.ChooseFontFlags.CF_EFFECTS;
            chosenFont.lStructSize = Marshal.SizeOf(chosenFont);

            NativeMethods.ChooseFont(ref chosenFont);
            Marshal.PtrToStructure(pLogFont, logFont);
            try
            {
                FontFamily fontFamily = new FontFamily(logFont.lfFaceName);
                FontWeight fontWeight = ConvertToWpfFontWeight(logFont.lfWeight);
                int fontSize = -(int)Math.Round(logFont.lfHeight * 72 / 96f); // Point - 72DPI, Device Independent Pixel - 96DPI
                return new FontInfo(fontFamily, fontWeight, fontSize);
            }
            finally
            {
                Marshal.FreeHGlobal(pLogFont);
            }
        }
        #endregion

        #region ParseFontWeight
        public static FontWeight ParseFontWeight(string str)
        {
            FontWeight wpfWeight;

            if (str.Equals("Thin", StringComparison.OrdinalIgnoreCase))
                wpfWeight = FontWeights.Thin;
            else if (str.Equals("ExtraLight", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("UltraLight", StringComparison.OrdinalIgnoreCase))
                wpfWeight = FontWeights.ExtraLight;
            else if (str.Equals("Light", StringComparison.OrdinalIgnoreCase))
                wpfWeight = FontWeights.Light;
            else if (str.Equals("Regular", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("Normal", StringComparison.OrdinalIgnoreCase))
                wpfWeight = FontWeights.Regular;
            else if (str.Equals("Medium", StringComparison.OrdinalIgnoreCase))
                wpfWeight = FontWeights.Medium;
            else if (str.Equals("SemiBold", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("DemiBold", StringComparison.OrdinalIgnoreCase))
                wpfWeight = FontWeights.SemiBold;
            else if (str.Equals("Bold", StringComparison.OrdinalIgnoreCase))
                wpfWeight = FontWeights.Bold;
            else if (str.Equals("ExtraBold", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("UltraBold", StringComparison.OrdinalIgnoreCase))
                wpfWeight = FontWeights.ExtraBold;
            else if (str.Equals("Heavy", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("Black", StringComparison.OrdinalIgnoreCase))
                wpfWeight = FontWeights.Heavy;
            else
                return FontWeights.Regular;

            return wpfWeight;
        }
        #endregion
    }
    #endregion
}
