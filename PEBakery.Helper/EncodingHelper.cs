/*
    Copyright (C) 2018-2022 Hajin Jang
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

using PEBakery.Helper.ThirdParty;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PEBakery.Helper
{
    #region TextEncoding
    public enum TextEncoding
    {
        None, // Unknown or binary
        Ascii, // 0-127
        Ansi, // 0-255
        Utf8Bom, // UTF8 with BOM
        Utf8NoBom, // UTF8 without BOM
        Utf16LeBom, // UTF16 LE with BOM
        Utf16LeNoBom, // UTF16 LE without BOM
        Utf16BeBom, // UTF16-BE with BOM
        Utf16BeNoBom // UTF16-BE without BOM
    }
    #endregion

    #region EncodingHelper
    public static class EncodingHelper
    {
        #region (private) Fields
        private static readonly byte[] Utf8Bom = { 0xEF, 0xBB, 0xBF };
        private static readonly byte[] Utf16LeBom = { 0xFF, 0xFE };
        private static readonly byte[] Utf16BeBom = { 0xFE, 0xFF };

        private static readonly Encoding Utf8EncodingNoBom = new UTF8Encoding(false);
        public static Encoding DefaultAnsi
        {
            get
            {
                // In .NET Framework, Encoding.Default is system's active code page.
                // In .NET Core, System.Default is always UTF8.
                // To prepare .NET Core migration, implement .Net Framework's Encoding.Default.
                // 
                // .NET Core itself does not know about ANSI encodings, so do not forget to install this package.
                // https://www.nuget.org/packages/System.Text.Encoding.CodePages/
                int codepage = NativeMethods.GetACP();
                switch (codepage)
                {
                    case 65001:
                        // If codepage is 65001, .Net Framework's Encoding.Default returns UTF8 without a BOM.
                        return Utf8EncodingNoBom;
                    default:
                        // Encoding.GetEncoding() internally caches instances. No need to cache myself.
                        // return Encoding.GetEncoding(codepage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
                        return Encoding.GetEncoding(codepage);
                }
            }
        }

        private readonly static AdvTextEncDetect AdvDetect = new AdvTextEncDetect();
        private const int TextPeekSize = 16 * 1024;
        #endregion

        #region DetectBom
        public static Encoding DetectBom(string filePath)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return DetectBom(fs);
            }
        }

        public static Encoding DetectBom(Stream s)
        {
            byte[] buffer = new byte[3];
            int bytesRead = s.Read(buffer, 0, buffer.Length);
            return DetectBom(buffer, 0, bytesRead);
        }

        public static Encoding DetectBom(byte[] buffer, int offset, int count)
        {
            if (buffer.Length < offset + count)
                throw new ArgumentOutOfRangeException(nameof(buffer));

            return DetectBom(buffer.AsSpan(offset, count));
        }

        public static Encoding DetectBom(ReadOnlySpan<byte> span)
        {
            Encoding encoding = null;
            if (3 <= span.Length && span[0] == Utf8Bom[0] && span[1] == Utf8Bom[1] && span[2] == Utf8Bom[2])
            {
                encoding = Encoding.UTF8;
            }
            else if (2 <= span.Length)
            {
                if (span[0] == Utf16LeBom[0] && span[1] == Utf16LeBom[1])
                    encoding = Encoding.Unicode;
                else if (span[0] == Utf16BeBom[0] && span[1] == Utf16BeBom[1])
                    encoding = Encoding.BigEndianUnicode;
            }

            return encoding ?? DefaultAnsi;
        }
        #endregion

        #region DetectEncoding
        /// <summary>
        /// Detect encoding with heuristics. Ex) Detect UTF-8 wo BOM as UTF-8, not ANSI.
        /// </summary>
        /// <remarks>4KB is not enough. Use at least 16KB of buffer.
        /// <returns>Encoding instance</returns>
        public static Encoding DetectEncoding(string filePath, int peekSize = TextPeekSize)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return DetectEncoding(fs, peekSize);
            }
        }

        /// <summary>
        /// Detect encoding with heuristics. Ex) Detect UTF-8 wo BOM as UTF-8, not ANSI.
        /// </summary>
        /// <remarks>4KB is not enough. Use at least 16KB of buffer.
        /// <returns>Encoding instance</returns>
        public static Encoding DetectEncoding(string filePath, out TextEncoding textEnc, int peekSize = TextPeekSize)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return DetectEncoding(fs, out textEnc, peekSize);
            }
        }

        public static Encoding DetectEncoding(Stream s, int peekSize = TextPeekSize)
        {
            byte[] buffer = new byte[peekSize];
            int bytesRead = s.Read(buffer, 0, buffer.Length);

            return DetectEncoding(buffer, 0, bytesRead);
        }

        public static Encoding DetectEncoding(Stream s, out TextEncoding textEnc, int peekSize = TextPeekSize)
        {
            byte[] buffer = new byte[peekSize];
            int bytesRead = s.Read(buffer, 0, buffer.Length);

            return DetectEncoding(buffer, 0, bytesRead, out textEnc);
        }

        public static Encoding DetectEncoding(byte[] buffer, int offset, int count)
        {
            TextEncoding textEnc = AdvDetect.DetectEncoding(buffer, offset, count);
            return AdvTextEncDetect.TextEncodingToBclEncoding(textEnc);
        }

        public static Encoding DetectEncoding(byte[] buffer, int offset, int count, out TextEncoding textEnc)
        {
            textEnc = AdvDetect.DetectEncoding(buffer, offset, count);
            return AdvTextEncDetect.TextEncodingToBclEncoding(textEnc);
        }

        public static Encoding DetectEncoding(ReadOnlySpan<byte> span)
        {
            TextEncoding textEnc = AdvDetect.DetectEncoding(span);
            return AdvTextEncDetect.TextEncodingToBclEncoding(textEnc);
        }

        public static Encoding DetectEncoding(ReadOnlySpan<byte> span, out TextEncoding textEnc)
        {
            textEnc = AdvDetect.DetectEncoding(span);
            return AdvTextEncDetect.TextEncodingToBclEncoding(textEnc);
        }
        #endregion

        #region WriteTextBom
        public static void WriteTextBom(string path, Encoding encoding)
        {
            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                WriteTextBom(fs, encoding);
            }
        }

        public static void WriteTextBom(Stream stream, Encoding encoding)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            // Unicode Encoding: returns span of BOM
            // Non-Unicode Encoding: return span of length zero
            ReadOnlySpan<byte> bom = encoding.Preamble;
            if (bom.Length == 0)
                return;
            stream.Write(bom);
        }
        #endregion

        #region TextBomLength
        public static int TextBomLength(string filePath)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return TextBomLength(fs);
            }
        }

        public static int TextBomLength(Stream s)
        {
            byte[] buffer = new byte[3];
            int bytesRead = s.Read(buffer, 0, buffer.Length);
            return TextBomLength(buffer, 0, bytesRead);
        }

        public static int TextBomLength(byte[] buffer, int offset, int count)
        {
            int length = 0;
            if (buffer.Length < offset + count)
                throw new ArgumentOutOfRangeException(nameof(buffer));

            if (3 <= offset + count &&
                buffer[offset] == Utf8Bom[0] && buffer[offset + 1] == Utf8Bom[1] && buffer[offset + 2] == Utf8Bom[2])
            {
                length = Utf8Bom.Length;
            }
            else if (2 <= offset + count)
            {
                if (buffer[offset] == Utf16LeBom[0] && buffer[offset + 1] == Utf16LeBom[1])
                    length = Utf16LeBom.Length;
                else if (buffer[offset] == Utf16BeBom[0] && buffer[offset + 1] == Utf16BeBom[1])
                    length = Utf16BeBom.Length;
            }

            return length;
        }
        #endregion

        #region EncodingEquals
        public static bool EncodingEquals(Encoding e1, Encoding e2)
        {
            if (e1 == null)
            {
                return e2 == null;
            }
            else
            {
                if (e2 == null)
                {
                    return false;
                }
                else
                {
                    byte[] bom1 = e1.GetPreamble();
                    byte[] bom2 = e2.GetPreamble();
                    return e1.CodePage == e2.CodePage && bom1.SequenceEqual(bom2);
                }
            }
        }
        #endregion

        #region IsText
        public static bool IsText(string filePath, int peekSize)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return IsText(fs, peekSize);
            }
        }

        public static bool IsText(Stream s, int peekSize)
        {
            // At least check one page
            if (peekSize < 4096)
                peekSize = 4096;

            // Read buffer from Stream
            byte[] buffer = new byte[peekSize];
            int bytesRead = s.Read(buffer, 0, buffer.Length);
            return IsText(buffer, 0, bytesRead);
        }

        public static bool IsText(byte[] buffer, int offset, int count)
        {
            if (buffer.Length < offset + count)
                throw new ArgumentOutOfRangeException(nameof(buffer));

            // [Stage 1] Contains unicode BOM -> text
            if (3 <= offset + count &&
                buffer[offset] == Utf8Bom[0] && buffer[offset + 1] == Utf8Bom[1] && buffer[offset + 2] == Utf8Bom[2])
                return true;
            if (2 <= offset + count)
            {
                if (buffer[offset] == Utf16LeBom[0] && buffer[offset + 1] == Utf16LeBom[1])
                    return true;
                if (buffer[offset] == Utf16BeBom[0] && buffer[offset + 1] == Utf16BeBom[1])
                    return true;
            }

            // [Stage 2] Check if a chunk can be decoded as system default ANSI locale.
            // Many multi-byte encodings have 'unused area'. If a file contains one of these area, treat it as a binary.
            // Ex) EUC-KR's layout : https://en.wikipedia.org/wiki/CP949#/media/File:Unified_Hangul_Code.svg
            bool isText = true;
            Encoding ansiEnc = Encoding.GetEncoding(DefaultAnsi.CodePage, new EncoderExceptionFallback(), new DecoderExceptionFallback());
            try
            {
                // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
                ansiEnc.GetChars(buffer, offset, count);
            }
            catch (DecoderFallbackException)
            { // Failure
                isText = false;
            }

            // [Stage 3]
            // Problem: Some encodings make use of 128-255 area, so every byte is valid. (e.g. Windows-1252 / CP437)
            // To counter these issue, if file is seems to be text, check again with AutoIt.Common.TextEncodingDetect
            if (isText)
            {
                AdvTextEncDetect detect = new AdvTextEncDetect();
                byte[] idxZeroBuffer;
                if (offset == 0)
                {
                    idxZeroBuffer = buffer;
                }
                else
                {
                    idxZeroBuffer = new byte[count];
                    Array.Copy(buffer, offset, idxZeroBuffer, 0, count);
                }

                switch (detect.DetectEncoding(idxZeroBuffer, 0, idxZeroBuffer.Length))
                {
                    // Binary
                    case TextEncoding.None:
                    // PEBakery mandates unicode text to have BOM.
                    // They must have been filtered out in stage 1.
                    case TextEncoding.Utf16LeBom:
                    case TextEncoding.Utf16BeBom:
                    case TextEncoding.Utf8Bom:
                    // Treat unicode text file without a BOM as a binary.
                    case TextEncoding.Utf16LeNoBom:
                    case TextEncoding.Utf16BeNoBom:
                    case TextEncoding.Utf8NoBom:
                        isText = false;
                        break;
                }
            }

            return isText;
        }
        #endregion

        #region IsAnsiCompatible
        /// <summary>
        /// Check if the given string is compatible with system's active ANSI codepage.
        /// </summary>
        /// <remarks>
        /// Same functionality can be implemented with Encoding and EncoderFallback, but it involves exception throwing.
        /// </remarks>
        public static unsafe bool IsActiveCodePageCompatible(string str)
        {
            return IsCodePageCompatible(NativeMethods.CP_ACP, str);
        }

        /// <summary>
        /// Check if the given string is compatible with a given codepage.
        /// </summary>
        /// <remarks>
        /// Same functionality can be implemented with Encoding and EncoderFallback, but it involves exception throwing.
        /// </remarks>
        public static unsafe bool IsCodePageCompatible(uint codepage, string str)
        {
            // Empty string must be compatible to any encoding, right?
            if (str.Length == 0)
                return true;

            // Get required buffer size
            int bufferSize = NativeMethods.WideCharToMultiByte(codepage, 0, str, -1, null, 0, null, null);

            // Try to convert unicode string to multi-byte, and see whether conversion fails or not.
            int usedDefaultChar = 0;
            byte[] buffer = new byte[bufferSize + 2];
            int ret = NativeMethods.WideCharToMultiByte(codepage, NativeMethods.WC_NO_BEST_FIT_CHARS, str, -1, buffer, bufferSize, null, &usedDefaultChar);

            // Return test result
            if (ret == 0)
                return false; // Conversion failed, assume that str is not compatible
            return usedDefaultChar == 0;
        }
        #endregion

        #region SmartDetectEncoding
        /// <summary>
        /// Detect Encoding of the text file, considering the content to write.
        /// If the fils is ASCII-only but the content to write is not compatible with ANSI, it will be treated as UTF-8 wo BOM.
        /// </summary>
        /// <param name="filePath">The text file to detect encoding</param>
        /// <param name="content">Content to write into the file</param>
        /// <returns>Instance of the Encoding</returns>
        public static Encoding SmartDetectEncoding(string filePath, string content)
        {
            // Detect Encoding
            Encoding encoding = DetectEncoding(filePath, out TextEncoding textEnc);

            // Is the text is ASCII-only?
            switch (textEnc)
            {
                case TextEncoding.Ascii:
                case TextEncoding.None:
                    { // Check whether content to write is ANSI-compatible.
                        if (!IsActiveCodePageCompatible(content)) // If not, use UTF-8 wo BOM instead.
                            encoding = new UTF8Encoding(false);
                    }
                    break;
            }

            return encoding;
        }

        /// <summary>
        /// Detect Encoding of the text file, considering the content to write.
        /// If the fils is ASCII-only but the content to write is not compatible with ANSI, it will be treated as UTF-8 wo BOM.
        /// </summary>
        /// <param name="filePath">The text file to detect encoding</param>
        /// <param name="contents">Contents to write into the file</param>
        /// <returns>Instance of the Encoding</returns>
        public static Encoding SmartDetectEncoding(string filePath, IEnumerable<string> contents)
        {
            // Detect Encoding
            Encoding encoding = DetectEncoding(filePath, out TextEncoding textEnc);

            // Is the text is ASCII-only?
            switch (textEnc)
            {
                case TextEncoding.Ascii:
                case TextEncoding.None:
                    { // Check whether content to write is ANSI-compatible.
                        if (!contents.All(x => IsActiveCodePageCompatible(x))) // If not, use UTF-8 wo BOM instead.
                            encoding = new UTF8Encoding(false);
                    }
                    break;
            }

            return encoding;
        }

        /// <summary>
        /// Detect Encoding of the text file, considering the content to write.
        /// If the file is ASCII-only but the content to write is not compatible with ANSI, it will be treated as UTF-8 wo BOM.
        /// </summary>
        /// <param name="filePath">The text file to detect encoding</param>
        /// <param name="isContentAnsiCompat">Check if content to write is compatible with ANSI encoding</param>
        /// <returns>Instance of the Encoding</returns>
        public static Encoding SmartDetectEncoding(string filePath, Func<bool> isContentAnsiCompat)
        {
            // Detect Encoding
            Encoding encoding = DetectEncoding(filePath, out TextEncoding textEnc);

            // Is the text is ASCII-only?
            switch (textEnc)
            {
                case TextEncoding.Ascii:
                case TextEncoding.None:
                    { // Check whether content to write is ANSI-compatible.
                        if (!isContentAnsiCompat()) // If not, use UTF-8 wo BOM instead.
                            encoding = new UTF8Encoding(false);
                    }
                    break;
            }

            return encoding;
        }
        #endregion
    }
    #endregion
}
