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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PEBakery.IniLib
{
    internal static class IniHelper
    {
        /// <summary>
        /// Detect text file's encoding with BOM
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        internal static Encoding DetectTextEncoding(string filePath)
        {
            byte[] bom = new byte[4];

            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                fs.Read(bom, 0, bom.Length);
            }

            if (bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
                return Encoding.UTF8;
            else if (bom[0] == 0xFF && bom[1] == 0xFE)
                return Encoding.Unicode;
            else if (bom[0] == 0xFE && bom[1] == 0xFF)
                return Encoding.BigEndianUnicode;
            return Encoding.Default;
        }

        /// <summary>
        /// Detect text file's encoding with BOM
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        internal static Encoding DetectTextEncoding(Stream stream)
        {
            byte[] bom = new byte[4];

            stream.Position = 0;
            stream.Read(bom, 0, bom.Length);
            stream.Position = 0;

            if (bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
                return Encoding.UTF8;
            else if (bom[0] == 0xFF && bom[1] == 0xFE)
                return Encoding.Unicode;
            else if (bom[0] == 0xFE && bom[1] == 0xFF)
                return Encoding.BigEndianUnicode;
            return Encoding.Default;
        }

        /// <summary>
        /// Write Unicode BOM into text file stream
        /// </summary>
        /// <param name="fs"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        internal static void WriteTextBOM(string path, Encoding encoding)
        {
            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                if (encoding == Encoding.UTF8)
                {
                    byte[] bom = new byte[] { 0xEF, 0xBB, 0xBF };
                    fs.Write(bom, 0, bom.Length);
                }
                else if (encoding == Encoding.Unicode)
                {
                    byte[] bom = new byte[] { 0xFF, 0xFE };
                    fs.Write(bom, 0, bom.Length);
                }
                else if (encoding == Encoding.BigEndianUnicode)
                {
                    byte[] bom = new byte[] { 0xFE, 0xFF };
                    fs.Write(bom, 0, bom.Length);
                }
                else if (encoding != Encoding.Default)
                { // Unsupported Encoding
                    throw new ArgumentException($"[{encoding}] is not supported");
                }
            }
        }

        /// <summary>
        /// Write Unicode BOM into text file stream
        /// </summary>
        /// <param name="fs"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        internal static FileStream WriteTextBOM(FileStream fs, Encoding encoding)
        {
            if (encoding == Encoding.UTF8)
            {
                byte[] bom = new byte[] { 0xEF, 0xBB, 0xBF };
                fs.Write(bom, 0, bom.Length);
            }
            else if (encoding == Encoding.Unicode)
            {
                byte[] bom = new byte[] { 0xFF, 0xFE };
                fs.Write(bom, 0, bom.Length);
            }
            else if (encoding == Encoding.BigEndianUnicode)
            {
                byte[] bom = new byte[] { 0xFE, 0xFF };
                fs.Write(bom, 0, bom.Length);
            }
            else if (encoding != Encoding.Default)
            { // Unsupported Encoding
                throw new ArgumentException($"[{encoding}] is not supported");
            }

            return fs;
        }

        /// <summary>
        /// Replace src with dest. 
        /// </summary>
        /// <param name="src"></param>
        /// <param name="dest"></param>
        internal static void FileReplaceEx(string src, string dest)
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
}
