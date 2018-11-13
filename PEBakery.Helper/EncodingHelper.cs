/*
    Copyright (C) 2018 Hajin Jang
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

namespace PEBakery.Helper
{
    public static class EncodingHelper
    {
        private static readonly byte[] Utf8Bom = { 0xEF, 0xBB, 0xBF };
        private static readonly byte[] Utf16LeBom = { 0xFF, 0xFE };
        private static readonly byte[] Utf16BeBom = { 0xFE, 0xFF };

        public static Encoding DetectTextEncoding(string filePath)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return DetectTextEncoding(fs);
            }
        }

        public static Encoding DetectTextEncoding(Stream s)
        {
            byte[] bom = new byte[3];

            long posBackup = s.Position;
            s.Position = 0;
            s.Read(bom, 0, bom.Length);
            s.Position = posBackup;

            if (bom[0] == Utf8Bom[0] && bom[1] == Utf8Bom[1] && bom[2] == Utf8Bom[2])
                return Encoding.UTF8;
            if (bom[0] == Utf16LeBom[0] && bom[1] == Utf16LeBom[1])
                return Encoding.Unicode;
            if (bom[0] == Utf16BeBom[0] && bom[1] == Utf16BeBom[1])
                return Encoding.BigEndianUnicode;
            return Encoding.Default;
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

            long posBackup = stream.Position;
            stream.Position = 0;

            if (encoding.Equals(Encoding.UTF8))
            {
                byte[] bom = { 0xEF, 0xBB, 0xBF };
                stream.Write(bom, 0, bom.Length);
            }
            else if (encoding.Equals(Encoding.Unicode))
            {
                byte[] bom = { 0xFF, 0xFE };
                stream.Write(bom, 0, bom.Length);
            }
            else if (encoding.Equals(Encoding.BigEndianUnicode))
            {
                byte[] bom = { 0xFE, 0xFF };
                stream.Write(bom, 0, bom.Length);
            }
            else if (!encoding.Equals(Encoding.Default))
            { // Unsupported Encoding
                throw new ArgumentException($"[{encoding}] is not supported");
            }

            stream.Position = posBackup;
        }

        public static long TextBomLength(string filePath)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return TextBomLength(fs);
            }
        }

        public static long TextBomLength(Stream s)
        {
            byte[] bom = new byte[3];

            long posBackup = s.Position;
            s.Position = 0;
            s.Read(bom, 0, bom.Length);
            s.Position = posBackup;

            if (bom[0] == Utf8Bom[0] && bom[1] == Utf8Bom[1] && bom[2] == Utf8Bom[2])
                return 3;
            if (bom[0] == Utf16LeBom[0] && bom[1] == Utf16LeBom[1])
                return 2;
            if (bom[0] == Utf16BeBom[0] && bom[1] == Utf16BeBom[1])
                return 2;
            return 0;
        }

        public static void ConvertEncoding(string srcFile, string destFile, Encoding destEnc)
        {
            Encoding srcEnc = EncodingHelper.DetectTextEncoding(srcFile);
            using (StreamReader r = new StreamReader(srcFile, srcEnc))
            using (StreamWriter w = new StreamWriter(destFile, false, destEnc))
            {
                w.Write(r.ReadToEnd());
            }
        }

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

            // Read chunk into buffer
            byte[] buffer = new byte[peekSize];
            long posBak = s.Position;
            s.Position = 0;
            s.Read(buffer, 0, buffer.Length);
            s.Position = posBak;

            // Contains unicode BOM -> definitely text
            if (buffer[0] == Utf8Bom[0] && buffer[1] == Utf8Bom[1] && buffer[2] == Utf8Bom[2])
                return true;
            if (buffer[0] == Utf16LeBom[0] && buffer[1] == Utf16LeBom[1])
                return true;
            if (buffer[0] == Utf16BeBom[0] && buffer[1] == Utf16BeBom[1])
                return true;

            // Check if a chunk can be decoded as system default ANSI locale
            bool isText = true;
            Encoding ansiEnc = Encoding.GetEncoding(Encoding.Default.CodePage, new EncoderExceptionFallback(), new DecoderExceptionFallback());
            try
            {
                // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
                ansiEnc.GetChars(buffer);
            }
            catch (DecoderFallbackException)
            { // Failure
                isText = false;
            }

            return isText;
        }
    }
}
