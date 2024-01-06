/*
    Copyright (C) 2019-2023 Hajin Jang
    Licensed under GPL 3.0
 
    PEBakery is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.

    Additional permission under GNU GPL version 3 section 7

    If you modify this program, or any covered work, by linking
    or combining it with external libraries, containing parts
    covered by the terms of various license, the licensors of
    this program grant you additional permission to convey the
    resulting work. An external library is a library which is
    not derived from or based on this program. 
*/

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;

namespace PEBakery.Core.Tests
{
    [TestClass]
    public class FileTypeDetectorTests
    {
        #region Expected Answer
        private struct TypeInfo
        {
            public readonly string FileType;
            public readonly string MimeType;
            public readonly bool IsText;

            public TypeInfo(string fileType, string mimeType, bool isText)
            {
                FileType = fileType;
                MimeType = mimeType;
                IsText = isText;
            }
        }

        private readonly Dictionary<string, TypeInfo> _isTextResultDict = new Dictionary<string, TypeInfo>(StringComparer.Ordinal)
        {
            ["Banner.7z"] = new TypeInfo("7-zip archive data, version 0.3", "application/x-7z-compressed", false),
            ["Banner.svg"] = new TypeInfo("SVG Scalable Vector Graphics image", "image/svg+xml", true),
            ["Banner.zip"] = new TypeInfo("Zip archive data, at least v2.0 to extract", "application/zip", false),
            ["CP949.txt"] = new TypeInfo("Non-ISO extended-ASCII text, with very long lines, with CRLF, NEL line terminators", "text/plain", true),
            ["Random.bin"] = new TypeInfo("data", "application/octet-stream", false),
            ["ShiftJIS.html"] = new TypeInfo("HTML document, Non-ISO extended-ASCII text, with very long lines, with CRLF, LF, NEL line terminators", "text/html", true),
            ["Type3.pdf"] = new TypeInfo("PDF document, version 1.4", "application/pdf", false),
            ["UTF16BE.txt"] = new TypeInfo("Big-endian UTF-16 Unicode text, with very long lines, with CRLF line terminators", "text/plain", true),
            ["UTF16LE.txt"] = new TypeInfo("Little-endian UTF-16 Unicode text, with very long lines, with CR line terminators", "text/plain", true),
            ["UTF8.txt"] = new TypeInfo("UTF-8 Unicode (with BOM) text, with very long lines, with CRLF line terminators", "text/plain", true),
            ["UTF8woBOM.txt"] = new TypeInfo("UTF-8 Unicode text, with very long lines, with CRLF line terminators", "text/plain", true),
            ["Zero.bin"] = new TypeInfo("data", "application/octet-stream", false),
        };
        #endregion

        #region FileType
        [TestMethod]
        [TestCategory("FileTypeDetector")]
        public void FileType()
        {
            string testBench = EngineTests.Project.Variables.Expand("%TestBench%");
            string srcDir = Path.Combine(testBench, "FileTypeDetector");
            string[] files = Directory.GetDirectories(srcDir);
            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);
                Assert.IsNotNull(fileName);
                string expected = _isTextResultDict[fileName].FileType;

                // FilePath
                string ret = Global.FileTypeDetector.FileType(file);
                Assert.IsTrue(ret.Equals(expected, StringComparison.Ordinal));

                // ReadOnlySpan<byte>
                byte[] buffer;
                using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    buffer = new byte[fs.Length];
                    fs.Read(buffer, 0, buffer.Length);
                }
                ret = Global.FileTypeDetector.FileType(buffer);
                Assert.IsTrue(ret.Equals(expected, StringComparison.Ordinal));
            }
        }
        #endregion

        #region MimeType
        [TestMethod]
        [TestCategory("FileTypeDetector")]
        public void MimeType()
        {
            string testBench = EngineTests.Project.Variables.Expand("%TestBench%");
            string srcDir = Path.Combine(testBench, "FileTypeDetector");
            string[] files = Directory.GetDirectories(srcDir);
            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);
                Assert.IsNotNull(fileName);
                string expected = _isTextResultDict[fileName].MimeType;

                // FilePath
                string ret = Global.FileTypeDetector.MimeType(file);
                Assert.IsTrue(ret.Equals(expected, StringComparison.Ordinal));

                // ReadOnlySpan<byte>
                byte[] buffer;
                using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    buffer = new byte[fs.Length];
                    fs.Read(buffer, 0, buffer.Length);
                }
                ret = Global.FileTypeDetector.MimeType(buffer);
                Assert.IsTrue(ret.Equals(expected, StringComparison.Ordinal));
            }
        }
        #endregion

        #region IsText
        [TestMethod]
        [TestCategory("FileTypeDetector")]
        public void IsText()
        {
            string testBench = EngineTests.Project.Variables.Expand("%TestBench%");
            string srcDir = Path.Combine(testBench, "FileTypeDetector");
            string[] files = Directory.GetDirectories(srcDir);
            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);
                Assert.IsNotNull(fileName);
                bool expected = _isTextResultDict[fileName].IsText;

                // FilePath
                bool ret = Global.FileTypeDetector.IsText(file);
                Assert.AreEqual(expected, ret);

                // ReadOnlySpan<byte>
                byte[] buffer;
                using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    buffer = new byte[fs.Length];
                    fs.Read(buffer, 0, buffer.Length);
                }
                ret = Global.FileTypeDetector.IsText(buffer);
                Assert.AreEqual(expected, ret);
            }
        }
        #endregion
    }
}
