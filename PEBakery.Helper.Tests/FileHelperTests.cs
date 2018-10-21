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
using System;
using System.IO;
using System.Linq;
using System.Text;
// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local

namespace PEBakery.Helper.Tests
{
    #region FileHelper
    [TestClass]
    public class FileHelperTests
    {
        #region DetectTextEncoding
        [TestMethod]
        [TestCategory("Helper")]
        [TestCategory("FileHelper")]
        public void DetectTextEncoding()
        {
            string tempDir = FileHelper.GetTempDir();
            string tempFile = Path.Combine(tempDir, "Sample.txt");

            try
            {
                // Empty -> ANSI
                File.Create(tempFile).Close();
                Assert.AreEqual(FileHelper.DetectTextEncoding(tempFile), Encoding.Default);

                // UTF-16 LE
                FileHelper.WriteTextBom(tempFile, Encoding.Unicode);
                Assert.AreEqual(FileHelper.DetectTextEncoding(tempFile), Encoding.Unicode);

                // UTF-16 BE
                FileHelper.WriteTextBom(tempFile, Encoding.BigEndianUnicode);
                Assert.AreEqual(FileHelper.DetectTextEncoding(tempFile), Encoding.BigEndianUnicode);

                // UTF-8
                FileHelper.WriteTextBom(tempFile, Encoding.UTF8);
                Assert.AreEqual(FileHelper.DetectTextEncoding(tempFile), Encoding.UTF8);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
        #endregion

        #region GetFilesEx
        [TestMethod]
        [TestCategory("Helper")]
        [TestCategory("FileHelper")]
        public void GetFilesEx()
        {
            string srcDir = Path.Combine(TestSetup.SampleDir, "FileHelper");

            // Test 1
            string[] srcFiles = FileHelper.GetFilesEx(srcDir, "*.txt", SearchOption.AllDirectories);
            Assert.IsTrue(srcFiles.Length == 6);
            Assert.IsTrue(srcFiles.Contains(Path.Combine(srcDir, "A.txt"), StringComparer.Ordinal));
            Assert.IsTrue(srcFiles.Contains(Path.Combine(srcDir, "B.txt"), StringComparer.Ordinal));
            Assert.IsTrue(srcFiles.Contains(Path.Combine(srcDir, "C.txt"), StringComparer.Ordinal));
            Assert.IsTrue(srcFiles.Contains(Path.Combine(srcDir, "Y", "U", "V.txt"), StringComparer.Ordinal));
            Assert.IsTrue(srcFiles.Contains(Path.Combine(srcDir, "Z", "X.txt"), StringComparer.Ordinal));
            Assert.IsTrue(srcFiles.Contains(Path.Combine(srcDir, "Za", "W.txt"), StringComparer.Ordinal));

            // Test 2
            srcFiles = FileHelper.GetFilesEx(srcDir, "*.txt", SearchOption.TopDirectoryOnly);
            Assert.IsTrue(srcFiles.Contains(Path.Combine(srcDir, "A.txt"), StringComparer.Ordinal));
            Assert.IsTrue(srcFiles.Contains(Path.Combine(srcDir, "B.txt"), StringComparer.Ordinal));
            Assert.IsTrue(srcFiles.Contains(Path.Combine(srcDir, "C.txt"), StringComparer.Ordinal));
            Assert.IsTrue(srcFiles.Length == 3);
        }
        #endregion

        #region GetFilesExWithDir
        [TestMethod]
        [TestCategory("Helper")]
        [TestCategory("FileHelper")]
        public void GetFilesExWithDir()
        {
            string srcDir = Path.Combine(TestSetup.SampleDir, "FileHelper");

            // Test 1
            {
                (string Path, bool IsDir)[] paths = FileHelper.GetFilesExWithDirs(srcDir, "*.txt", SearchOption.AllDirectories);

                string[] dirs = paths.Where(x => x.IsDir).Select(x => x.Path).ToArray();
                Assert.IsTrue(dirs.Length == 5);
                Assert.IsTrue(dirs.Contains(Path.Combine(srcDir), StringComparer.Ordinal));
                Assert.IsTrue(dirs.Contains(Path.Combine(srcDir, "Z"), StringComparer.Ordinal));
                Assert.IsTrue(dirs.Contains(Path.Combine(srcDir, "Za"), StringComparer.Ordinal));
                Assert.IsTrue(dirs.Contains(Path.Combine(srcDir, "Y"), StringComparer.Ordinal));
                Assert.IsTrue(dirs.Contains(Path.Combine(srcDir, "Y", "U"), StringComparer.Ordinal));

                string[] files = paths.Where(x => !x.IsDir).Select(x => x.Path).ToArray();
                Assert.IsTrue(files.Length == 6);
                Assert.IsTrue(files.Contains(Path.Combine(srcDir, "A.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(srcDir, "B.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(srcDir, "C.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(srcDir, "Y", "U", "V.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(srcDir, "Z", "X.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(srcDir, "Za", "W.txt"), StringComparer.Ordinal));
            }
            // Test 2
            {
                (string Path, bool IsDir)[] paths = FileHelper.GetFilesExWithDirs(srcDir, "*.ini", SearchOption.AllDirectories);

                string[] dirs = paths.Where(x => x.IsDir).Select(x => x.Path).ToArray();
                Assert.IsTrue(dirs.Length == 2);
                Assert.IsTrue(dirs.Contains(Path.Combine(srcDir), StringComparer.Ordinal));
                Assert.IsTrue(dirs.Contains(Path.Combine(srcDir, "Z"), StringComparer.Ordinal));

                string[] files = paths.Where(x => !x.IsDir).Select(x => x.Path).ToArray();
                Assert.IsTrue(files.Length == 2);
                Assert.IsTrue(files.Contains(Path.Combine(srcDir, "D.ini"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(srcDir, "Z", "Y.ini"), StringComparer.Ordinal));
            }
            // Test 3
            {
                (string Path, bool IsDir)[] paths = FileHelper.GetFilesExWithDirs(srcDir, "*.txt", SearchOption.TopDirectoryOnly);

                string[] dirs = paths.Where(x => x.IsDir).Select(x => x.Path).ToArray();
                Assert.IsTrue(dirs.Length == 1);
                Assert.IsTrue(dirs.Contains(Path.Combine(srcDir), StringComparer.Ordinal));

                string[] files = paths.Where(x => !x.IsDir).Select(x => x.Path).ToArray();
                Assert.IsTrue(files.Length == 3);
                Assert.IsTrue(files.Contains(Path.Combine(srcDir, "A.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(srcDir, "B.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(srcDir, "C.txt"), StringComparer.Ordinal));
            }
        }
        #endregion

        #region DirectoryCopy
        [TestMethod]
        [TestCategory("Helper")]
        [TestCategory("FileHelper")]
        public void DirectoryCopy()
        {
            string srcDir = Path.Combine(TestSetup.SampleDir, "FileHelper");
            string destDir = FileHelper.GetTempDir();

            void Template(Action action)
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

            // Test 1
            Template(() =>
            {
                FileHelper.DirCopy(srcDir, destDir, new FileHelper.DirCopyOptions
                {
                    CopySubDirs = true,
                    Overwrite = true,
                });
                string[] files = Directory.GetFiles(destDir, "*", SearchOption.AllDirectories);
                Assert.IsTrue(files.Length == 8);
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "A.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "B.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "C.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "D.ini"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "Y", "U", "V.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "Z", "X.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "Z", "Y.ini"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "Za", "W.txt"), StringComparer.Ordinal));
            });

            // Test 2
            Template(() =>
            {
                FileHelper.DirCopy(srcDir, destDir, new FileHelper.DirCopyOptions
                {
                    CopySubDirs = false,
                    Overwrite = true,
                });
                string[] files = Directory.GetFiles(destDir, "*", SearchOption.AllDirectories);
                Assert.IsTrue(files.Length == 4);
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "A.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "B.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "C.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "D.ini"), StringComparer.Ordinal));
            });

            // Test 3
            Template(() =>
            {
                FileHelper.DirCopy(srcDir, destDir, new FileHelper.DirCopyOptions
                {
                    CopySubDirs = true,
                    Overwrite = true,
                    FileWildcard = "*.txt",
                });
                string[] files = Directory.GetFiles(destDir, "*.txt", SearchOption.AllDirectories);
                Assert.IsTrue(files.Length == 6);
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "A.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "B.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "C.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "Y", "U", "V.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "Z", "X.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "Za", "W.txt"), StringComparer.Ordinal));
            });

            // Test 4
            Template(() =>
            {
                FileHelper.DirCopy(srcDir, destDir, new FileHelper.DirCopyOptions
                {
                    CopySubDirs = false,
                    Overwrite = true,
                    FileWildcard = "*.txt",
                });
                string[] files = Directory.GetFiles(destDir, "*.txt", SearchOption.AllDirectories);
                Assert.IsTrue(files.Length == 3);
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "A.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "B.txt"), StringComparer.Ordinal));
                Assert.IsTrue(files.Contains(Path.Combine(destDir, "C.txt"), StringComparer.Ordinal));
            });
        }
        #endregion
    }
    #endregion
}
