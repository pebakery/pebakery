/*
    Copyright (C) 2017-2023 Hajin Jang
 
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
// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local

namespace PEBakery.Helper.Tests
{
    [TestClass]
    [TestCategory("PEBakery.Helper")]
    [TestCategory(nameof(FileHelper))]
    public class FileHelperTests
    {
        #region Temp Path
        [TestMethod]
        public void BaseTempDir()
        {
            string baseTempDir = FileHelper.BaseTempDir();
            Console.WriteLine($"BaseTempDir = {baseTempDir}");

            string startsWith = Path.Combine(Path.GetTempPath(), "PEBakery_");
            Assert.IsTrue(baseTempDir.StartsWith(startsWith, StringComparison.OrdinalIgnoreCase));

            int length = startsWith.Length + 8; // %TempDir%\PEBakery_<8 HEX NUM>
            Assert.AreEqual(length, baseTempDir.Length);
        }

        [TestMethod]
        public void GetTempDir()
        {
            string baseTempDir = FileHelper.BaseTempDir();
            Console.WriteLine($"BaseTempDir = {baseTempDir}");

            string tempDir = FileHelper.GetTempDir();
            Console.WriteLine($"TempDir     = {tempDir}");

            string startsWith = Path.Combine(baseTempDir, "d");
            Assert.IsTrue(tempDir.StartsWith(startsWith, StringComparison.OrdinalIgnoreCase));

            int length = startsWith.Length + 8;
            Assert.AreEqual(length, tempDir.Length);
        }

        [TestMethod]
        public void GetTempFile()
        {
            string baseTempDir = FileHelper.BaseTempDir();
            Console.WriteLine($"BaseTempDir = {baseTempDir}");

            void Template(string? ext)
            {
                string tempFile = FileHelper.GetTempFile(ext);
                Console.WriteLine($"TempFile    = {tempFile}");

                string startsWith = Path.Combine(baseTempDir, "f");
                Assert.IsTrue(tempFile.StartsWith(startsWith, StringComparison.OrdinalIgnoreCase));

                int length = startsWith.Length + 8;
                if (ext == null)
                    length += ".tmp".Length;
                else
                    length += 1 + ext.Length;
                Assert.AreEqual(length, tempFile.Length);
            }

            Template(null);
            Template("txt");
            Template("ini");
            Template("script");
        }
        #endregion

        #region GetFilesEx
        [TestMethod]
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

        #region GetDirFilesEx
        [TestMethod]
        public void GetDirFilesEx()
        {
            string srcDir = Path.Combine(TestSetup.SampleDir, "FileHelper");

            // Test 1
            {
                (string Path, bool IsDir)[] paths = FileHelper.GetDirFilesEx(srcDir, "*.txt", SearchOption.AllDirectories);

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
                (string Path, bool IsDir)[] paths = FileHelper.GetDirFilesEx(srcDir, "*.ini", SearchOption.AllDirectories);

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
                (string Path, bool IsDir)[] paths = FileHelper.GetDirFilesEx(srcDir, "*.txt", SearchOption.TopDirectoryOnly);

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

        #region DirCopy
        [TestMethod]
        public void DirCopy()
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
                FileHelper.DirCopy(srcDir, destDir, new DirCopyOptions
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
                FileHelper.DirCopy(srcDir, destDir, new DirCopyOptions
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
                FileHelper.DirCopy(srcDir, destDir, new DirCopyOptions
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
                FileHelper.DirCopy(srcDir, destDir, new DirCopyOptions
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

        #region IsPathNonExistDir
        [TestMethod]
        public void IsPathNonExistDir()
        {
            string destDir = FileHelper.GetTempDir();
            Directory.CreateDirectory(destDir);
            try
            {
                string? destRoot = Path.GetPathRoot(destDir);
                Assert.IsNotNull(destRoot);
                Assert.IsFalse(FileHelper.IsPathNonExistDir(destRoot));
                Assert.IsFalse(FileHelper.IsPathNonExistDir(destDir));

                // Test non-existing directory path
                string targetPath = Path.Combine(destDir, "Dummy");
                Assert.IsFalse(FileHelper.IsPathNonExistDir(targetPath));
                Assert.IsTrue(FileHelper.IsPathNonExistDir(targetPath + @"\"));

                // Test existing directory path
                Directory.CreateDirectory(targetPath);
                try
                {
                    Assert.IsFalse(FileHelper.IsPathNonExistDir(targetPath));
                    Assert.IsFalse(FileHelper.IsPathNonExistDir(targetPath + @"\"));
                }
                finally
                {
                    if (Directory.Exists(targetPath))
                        Directory.Delete(targetPath, true);
                }

                // Test existing file path
                try
                {
                    File.Create(targetPath).Dispose();
                    Assert.IsFalse(FileHelper.IsPathNonExistDir(targetPath));
                    Assert.IsTrue(FileHelper.IsPathNonExistDir(targetPath + @"\"));
                }
                finally
                {
                    if (File.Exists(targetPath))
                        File.Delete(targetPath);
                }
            }
            finally
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
            }
        }
        #endregion

        #region SubRootDirPath
        [TestMethod]
        public void SubRootDirPath()
        {
            static void Template(string path, string rootDir, string? expected, bool success)
            {
                try
                {
                    string result = FileHelper.SubRootDirPath(path, rootDir);
                    Assert.IsTrue(result.Equals(expected, StringComparison.Ordinal));
                }
                catch (ArgumentException)
                {
                    Assert.IsFalse(success);
                    return;
                }
                Assert.IsTrue(success);
            }

            Template(@"C:\PEBakery\Hello\World.txt", @"C:\PEBakery", @"Hello\World.txt", true);
            Template(@"C:\PEBakery\\Hello\World.txt", @"C:\PEBakery", @"Hello\World.txt", true);
            Template(@"\PEBakery\Hello\World.txt", @"C:\PEBakery", null, false);
        }
        #endregion
    }
}
