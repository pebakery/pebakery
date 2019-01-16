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

using System;
using System.IO;
using System.Text;

namespace PEBakery.Ini
{
    #region EncodingHelper
    internal static class MiniHelper
    {
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
            Encoding encoding = null;
            if (buffer.Length < offset + count)
                throw new ArgumentOutOfRangeException(nameof(buffer));

            if (3 <= offset + count &&
                buffer[offset] == Utf8Bom[0] && buffer[offset + 1] == Utf8Bom[1] && buffer[offset + 2] == Utf8Bom[2])
            {
                encoding = Encoding.UTF8;
            }
            else if (2 <= offset + count)
            {
                if (buffer[offset] == Utf16LeBom[0] && buffer[offset + 1] == Utf16LeBom[1])
                    encoding = Encoding.Unicode;
                else if (buffer[offset] == Utf16BeBom[0] && buffer[offset + 1] == Utf16BeBom[1])
                    encoding = Encoding.BigEndianUnicode;
            }

            return encoding ?? DefaultAnsi;
        }

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
            {
                stream.Write(Utf8Bom, 0, Utf8Bom.Length);
            }
            else if (encoding.CodePage == Encoding.Unicode.CodePage)
            {
                stream.Write(Utf16LeBom, 0, Utf16LeBom.Length);
            }
            else if (encoding.CodePage == Encoding.BigEndianUnicode.CodePage)
            {
                stream.Write(Utf16BeBom, 0, Utf16BeBom.Length);
            }
            else if (encoding.CodePage != DefaultAnsi.CodePage)
            {
                // Unsupported Encoding
                throw new ArgumentException($"[{encoding}] is not supported");
            }
        }

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

        /// <summary>
        /// Replace src with dest. 
        /// </summary>
        /// <param name="src"></param>
        /// <param name="dest"></param>
        public static void FileReplaceEx(string src, string dest)
        {
            try
            {
                // File.Copy removes ACL and ADS.
                // Instead, use File.Replace.
                File.Replace(src, dest, null);
            }
            catch (IOException)
            {
                // However, File.Replace throws IOException if src and dest files are in different volume.
                // In this case, try File.Copy as fallback.
                File.Copy(src, dest, true);
                File.Delete(src);
            }
        }
    }
    #endregion
}
