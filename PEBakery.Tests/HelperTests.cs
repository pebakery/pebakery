/*
    Copyright (C) 2017 Hajin Jang
 
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Helper;
using PEBakery.Tests.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PEBakery.Tests
{
    #region FileHelper
    [TestClass]
    public class FileHelperTests
    {
        #region DetectTextEncoding
        [TestMethod]
        [TestCategory("Helper")]
        [TestCategory("FileHelper")]
        public void FileHelper_DetectTextEncoding()
        {
            string tempDir = Path.GetTempFileName();
            File.Delete(tempDir);
            Directory.CreateDirectory(tempDir);
            string tempFile = Path.Combine(tempDir, "Sample.txt");

            try
            {
                // Empty -> ANSI
                File.Create(tempFile).Close();
                Assert.AreEqual(FileHelper.DetectTextEncoding(tempFile), Encoding.Default);

                // UTF-16 LE
                FileHelper.WriteTextBOM(tempFile, Encoding.Unicode);
                Assert.AreEqual(FileHelper.DetectTextEncoding(tempFile), Encoding.Unicode);

                // UTF-16 BE
                FileHelper.WriteTextBOM(tempFile, Encoding.BigEndianUnicode);
                Assert.AreEqual(FileHelper.DetectTextEncoding(tempFile), Encoding.BigEndianUnicode);

                // UTF-8
                FileHelper.WriteTextBOM(tempFile, Encoding.UTF8);
                Assert.AreEqual(FileHelper.DetectTextEncoding(tempFile), Encoding.UTF8);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
        #endregion

        [TestMethod]
        [TestCategory("Helper")]
        [TestCategory("FileHelper")]
        public void FileHelper_GetFilesEx()
        {
            string srcDir = Path.Combine(EngineTests.BaseDir, "WorkBench", "Helper", "FileHelper");

            // Test 1
            {
                string[] srcFiles = FileHelper.GetFilesEx(srcDir, "*.txt", SearchOption.AllDirectories);
                Assert.IsTrue(srcFiles.Length == 5);
            }
            // Test 2
            {
                string[] srcFiles = FileHelper.GetFilesEx(srcDir, "*.txt", SearchOption.TopDirectoryOnly);
                Assert.IsTrue(srcFiles.Length == 3);
            }
        }

        [TestMethod]
        [TestCategory("Helper")]
        [TestCategory("FileHelper")]
        public void FileHelper_DirectoryCopy()
        {
            string srcDir = Path.Combine(EngineTests.BaseDir, "WorkBench", "Helper", "FileHelper");
            string destDir = Path.Combine(EngineTests.BaseDir, "WorkBench", "Helper", "FileHelper_DirCopy");

            // Test 1
            FileHelper_DirectoryCopy_Template(destDir, () =>
            {
                FileHelper.DirectoryCopy(srcDir, destDir, true, true);
                string[] srcFiles = Directory.GetFiles(destDir, "*", SearchOption.AllDirectories);
                Assert.IsTrue(srcFiles.Length == 7);
            });

            // Test 2
            FileHelper_DirectoryCopy_Template(destDir, () =>
            {
                FileHelper.DirectoryCopy(srcDir, destDir, false, true);
                string[] srcFiles = Directory.GetFiles(destDir, "*", SearchOption.AllDirectories);
                Assert.IsTrue(srcFiles.Length == 4);
            });

            // Test 3
            FileHelper_DirectoryCopy_Template(destDir, () =>
            {
                FileHelper.DirectoryCopy(srcDir, destDir, true, true, "*.txt");
                string[] srcFiles = Directory.GetFiles(destDir, "*.txt", SearchOption.AllDirectories);
                Assert.IsTrue(srcFiles.Length == 5);
            });

            // Test 4
            FileHelper_DirectoryCopy_Template(destDir, () =>
            {
                FileHelper.DirectoryCopy(srcDir, destDir, false, true, "*.txt");
                string[] srcFiles = Directory.GetFiles(destDir, "*.txt", SearchOption.AllDirectories);
                Assert.IsTrue(srcFiles.Length == 3);
            });
        }

        public void FileHelper_DirectoryCopy_Template(string destDir, Action action)
        {
            Directory.CreateDirectory(destDir);
            try
            {
                action.Invoke();
            }
            finally
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
            }
        }
    }
    #endregion

    #region StringHelper
    [TestClass]
    public class StringHelperTests
    {
        [TestMethod]
        [TestCategory("Helper")]
        [TestCategory("StringHelper")]
        public void StringHelper_ReplaceEx()
        {
            string str;

            str = StringHelper.ReplaceEx(@"ABCD", "AB", "XYZ", StringComparison.Ordinal);
            Assert.IsTrue(str.Equals("XYZCD", StringComparison.Ordinal));

            str = StringHelper.ReplaceEx(@"ABCD", "ab", "XYZ", StringComparison.Ordinal);
            Assert.IsTrue(str.Equals("ABCD", StringComparison.Ordinal));

            str = StringHelper.ReplaceEx(@"abcd", "AB", "XYZ", StringComparison.OrdinalIgnoreCase);
            Assert.IsTrue(str.Equals("XYZcd", StringComparison.Ordinal));

            str = StringHelper.ReplaceEx(@"abcd", "ab", "XYZ", StringComparison.OrdinalIgnoreCase);
            Assert.IsTrue(str.Equals("XYZcd", StringComparison.Ordinal));
        }
    }
    #endregion
}
