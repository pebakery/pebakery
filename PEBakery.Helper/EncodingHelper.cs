/*
    Copyright (C) 2018-2020 Hajin Jang
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
using System.IO;
using System.Text;

namespace PEBakery.Helper
{
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
                // In .Net Framework, Encoding.Default is system's active code page.
                // In .Net Core, System.Default is always UTF8.
                // To prepare .Net Core migration, implement .Net Framework's Encoding.Default.
                // 
                // .Net Core does not know about ANSI encodings, so do not forget to install this package.
                // https://www.nuget.org/packages/System.Text.Encoding.CodePages/
                int codepage = NativeMethods.GetACP();
                switch (codepage)
                {
                    case 65001:
                        // If codepage is 65001, .Net Framework's Encoding.Default returns UTF8 without a BOM.
                        return Utf8EncodingNoBom;
                    default:
                        // Encoding.GetEncoding() internally caches instances. No need to cache myself.
                        return Encoding.GetEncoding(codepage);
                }
            }
        }

        private readonly static AdvTextEncDetect AdvDetect = new AdvTextEncDetect();
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
        /// <param name="filePath"></param>
        /// <param name="peekSize"></param>
        /// <remarks>
        /// 4KB is not enough. Use at least 16KB of buffer.
        /// </remarks>
        /// <returns>Encoding instance</returns>
        public static Encoding DetectEncoding(string filePath, int peekSize = 16 * 1024)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return DetectEncoding(fs, peekSize);
            }
        }

        public static Encoding DetectEncoding(Stream s, int peekSize = 16 * 1024)
        {
            byte[] buffer = new byte[peekSize];
            int bytesRead = s.Read(buffer, 0, buffer.Length);

            return DetectEncoding(buffer, 0, bytesRead);
        }

        public static Encoding DetectEncoding(byte[] buffer, int offset, int count)
        {
            TextEncoding textEnc = AdvDetect.DetectEncoding(buffer, offset, count);
            return AdvTextEncDetect.TextEncodingToBclEncoding(textEnc);
        }

        public static Encoding DetectEncoding(ReadOnlySpan<byte> span)
        {
            TextEncoding textEnc = AdvDetect.DetectEncoding(span);
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

            // Encoding.Equals() checks equality of fallback as well as equality of codepage.
            // Ignore fallback here, only check codepage id.
            if (encoding.CodePage == Encoding.UTF8.CodePage)
                stream.Write(Utf8Bom, 0, Utf8Bom.Length);
            else if (encoding.CodePage == Encoding.Unicode.CodePage)
                stream.Write(Utf16LeBom, 0, Utf16LeBom.Length);
            else if (encoding.CodePage == Encoding.BigEndianUnicode.CodePage)
                stream.Write(Utf16BeBom, 0, Utf16BeBom.Length);
            else if (encoding.CodePage != DefaultAnsi.CodePage) // Unsupported Encoding
                throw new ArgumentException($"[{encoding}] is not supported");
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
    }
}
