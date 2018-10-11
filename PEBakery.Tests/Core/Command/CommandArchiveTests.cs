/*
    Copyright (C) 2017-2018 Hajin Jang
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
using PEBakery.Core;
using PEBakery.Helper;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PEBakery.Tests.Core.Command
{
    [TestClass]
    public class CommandArchiveTests
    {
        #region Compress
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandArchive")]
        public void Compress()
        {
            void DirTemplate(string arcType, string srcDirName, ArchiveHelper.CompressLevel? level)
            { // Compress,<ArchiveType>,<SrcPath>,<DestArchive>,[CompressLevel]
                EngineState s = EngineTests.CreateEngineState();
                string srcDir = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandArchive"));
                string srcFullPath = Path.Combine(srcDir, srcDirName);
                string destDir = FileHelper.GetTempFileNameEx();
                string destArchive = Path.Combine(destDir, $"{srcDirName}.{arcType}");
                string decompDir = FileHelper.GetTempFileNameEx();

                try
                {
                    Directory.CreateDirectory(destDir);
                    Directory.CreateDirectory(decompDir);

                    string rawCode = $@"Compress,{arcType},""%TestBench%\CommandArchive\{srcDirName}"",""{destArchive}""";
                    switch (level)
                    {
                        case ArchiveHelper.CompressLevel.Best:
                            rawCode += ",BEST";
                            break;
                        case ArchiveHelper.CompressLevel.Fastest:
                            rawCode += ",FASTEST";
                            break;
                        case ArchiveHelper.CompressLevel.Normal:
                            rawCode += ",NORMAL";
                            break;
                        case ArchiveHelper.CompressLevel.Store:
                            rawCode += ",STORE";
                            break;
                    }
                    EngineTests.Eval(s, rawCode, CodeType.Compress, ErrorCheck.Success);
                    EngineTests.ExtractWith7z(srcDir, destArchive, decompDir);

                    string[] srcFiles = Directory.GetFiles(srcFullPath, "*", SearchOption.AllDirectories);
                    string[] destFiles = Directory.GetFiles(decompDir, "*", SearchOption.AllDirectories);
                    Assert.IsTrue(srcFiles.Length == destFiles.Length);

                    for (int i = 0; i < srcFiles.Length; i++)
                    {
                        using (FileStream srcStream = new FileStream(srcFiles[i], FileMode.Open, FileAccess.Read, FileShare.Read))
                        using (FileStream destStream = new FileStream(destFiles[i], FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            byte[] srcDigest = HashHelper.CalcHash(HashHelper.HashType.SHA256, srcStream);
                            byte[] destDigest = HashHelper.CalcHash(HashHelper.HashType.SHA256, destStream);
                            Assert.IsTrue(srcDigest.SequenceEqual(destDigest));
                        }
                    }
                }
                finally
                {
                    if (Directory.Exists(destDir))
                        Directory.Delete(destDir, true);
                    if (Directory.Exists(decompDir))
                        Directory.Delete(decompDir, true);
                }
            }

            void FileTemplate(string arcType, string srcFilePath, ArchiveHelper.CompressLevel level)
            { // Compress,<ArchiveType>,<SrcPath>,<DestArchive>,[CompressLevel]
                EngineState s = EngineTests.CreateEngineState();
                string srcDir = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandArchive"));
                string srcFullPath = Path.Combine(srcDir, srcFilePath);
                string srcFileName = Path.GetFileName(srcFilePath);
                string destDir = FileHelper.GetTempFileNameEx();
                string destArchive = Path.Combine(destDir, $"{srcFileName}.{arcType}");
                string decompDir = FileHelper.GetTempFileNameEx();

                try
                {
                    Directory.CreateDirectory(destDir);
                    Directory.CreateDirectory(decompDir);

                    string rawCode = $@"Compress,{arcType},""%TestBench%\CommandArchive\{srcFilePath}"",""{destArchive}""";
                    switch (level)
                    {
                        case ArchiveHelper.CompressLevel.Best:
                            rawCode += ",BEST";
                            break;
                        case ArchiveHelper.CompressLevel.Fastest:
                            rawCode += ",FASTEST";
                            break;
                        case ArchiveHelper.CompressLevel.Normal:
                            rawCode += ",NORMAL";
                            break;
                        case ArchiveHelper.CompressLevel.Store:
                            rawCode += ",STORE";
                            break;
                    }
                    EngineTests.Eval(s, rawCode, CodeType.Compress, ErrorCheck.Success);
                    EngineTests.ExtractWith7z(srcDir, destArchive, decompDir);

                    using (FileStream srcStream = new FileStream(srcFullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (FileStream destStream = new FileStream(Path.Combine(decompDir, srcFileName), FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        byte[] srcDigest = HashHelper.CalcHash(HashHelper.HashType.SHA256, srcStream);
                        byte[] destDigest = HashHelper.CalcHash(HashHelper.HashType.SHA256, destStream);
                        Assert.IsTrue(srcDigest.SequenceEqual(destDigest));
                    }
                }
                finally
                {
                    Directory.Delete(destDir, true);
                    Directory.Delete(decompDir, true);
                }
            }

            DirTemplate("Zip", "France", ArchiveHelper.CompressLevel.Store);
            DirTemplate("7z", "Korea", ArchiveHelper.CompressLevel.Normal);
            DirTemplate("Zip", "Korea", ArchiveHelper.CompressLevel.Best);
            FileTemplate("Zip", Path.Combine("Korean_IME_Logo", "Korean_IME_Logo.jpg"), ArchiveHelper.CompressLevel.Normal);
            FileTemplate("7z", Path.Combine("Korean_IME_Logo", "Korean_IME_Logo.jpg"), ArchiveHelper.CompressLevel.Best);
        }
        #endregion

        #region Decompress
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandArchive")]
        public void Decompress()
        {
            void DirTemplate(string archiveFile)
            {
                Debug.Assert(archiveFile != null);
                string archiveType = Path.GetExtension(archiveFile).Substring(1);
                string archiveName = archiveFile.Substring(0, archiveFile.Length - (archiveType.Length + 1));

                EngineState s = EngineTests.CreateEngineState();
                string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandArchive"));
                string srcFullPath = Path.Combine(dirPath, archiveName);
                string destDir = FileHelper.GetTempFileNameEx();

                try
                {
                    string rawCode = $"Decompress,\"%TestBench%\\CommandArchive\\{archiveFile}\",\"{destDir}\"";
                    EngineTests.Eval(s, rawCode, CodeType.Decompress, ErrorCheck.Success);

                    string[] srcFiles = Directory.GetFiles(srcFullPath, "*", SearchOption.AllDirectories);
                    string[] destFiles = Directory.GetFiles(Path.Combine(destDir, archiveName), "*", SearchOption.AllDirectories);
                    Assert.IsTrue(srcFiles.Length == destFiles.Length);

                    for (int i = 0; i < srcFiles.Length; i++)
                    {
                        using (FileStream srcStream = new FileStream(srcFiles[i], FileMode.Open, FileAccess.Read, FileShare.Read))
                        using (FileStream destStream = new FileStream(destFiles[i], FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            byte[] srcDigest = HashHelper.CalcHash(HashHelper.HashType.SHA256, srcStream);
                            byte[] destDigest = HashHelper.CalcHash(HashHelper.HashType.SHA256, destStream);
                            Assert.IsTrue(srcDigest.SequenceEqual(destDigest));
                        }
                    }
                }
                finally
                {
                    if (Directory.Exists(destDir))
                        Directory.Delete(destDir, true);
                }
            }

            void FileTemplate(string archiveFile, string compFile)
            {
                Debug.Assert(archiveFile != null);
                string archiveType = Path.GetExtension(archiveFile).Substring(1);
                string archiveName = archiveFile.Substring(0, archiveFile.Length - (archiveType.Length + 1));

                EngineState s = EngineTests.CreateEngineState();
                string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandArchive"));
                string srcPath = Path.Combine(dirPath, archiveName, compFile);
                string destDir = FileHelper.GetTempFileNameEx();
                string destPath = Path.Combine(destDir, compFile);

                try
                {
                    Directory.CreateDirectory(destDir);

                    string rawCode = $"Decompress,\"%TestBench%\\CommandArchive\\{archiveFile}\",\"{destDir}\"";
                    EngineTests.Eval(s, rawCode, CodeType.Decompress, ErrorCheck.Success);

                    using (FileStream srcStream = new FileStream(srcPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (FileStream destStream = new FileStream(destPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        byte[] srcDigest = HashHelper.CalcHash(HashHelper.HashType.SHA256, srcStream);
                        byte[] destDigest = HashHelper.CalcHash(HashHelper.HashType.SHA256, destStream);
                        Assert.IsTrue(srcDigest.SequenceEqual(destDigest));
                    }
                }
                finally
                {
                    if (Directory.Exists(destDir))
                        Directory.Delete(destDir, true);
                }
            }

            DirTemplate("Korea.zip");
            DirTemplate("Korea.7z");
            DirTemplate("France.zip");
            DirTemplate("France.7z");
            DirTemplate("France.rar"); // RAR5
            FileTemplate("Korean_IME_Logo.zip", "Korean_IME_Logo.jpg");
            FileTemplate("Korean_IME_Logo.7z", "Korean_IME_Logo.jpg");
            FileTemplate("Korean_IME_Logo.rar", "Korean_IME_Logo.jpg"); // RAR2.9
        }
        #endregion

        #region Expand
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandArchive")]
        public void Expand()
        {
            EngineState s = EngineTests.CreateEngineState();
            string destDir = FileHelper.GetTempFileNameEx();

            void FileTemplate(string compFile, string rawCode, ErrorCheck check, bool testPreserve = false, bool checkIfPreserve = true)
            {
                string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandArchive"));
                string srcPath = Path.Combine(dirPath, "Cab", compFile);
                string destPath = Path.Combine(destDir, compFile);
                Debug.Assert(destPath != null);

                try
                {
                    if (testPreserve) // Check preserve
                    {
                        Directory.CreateDirectory(destDir);
                        File.Create(destPath).Close();
                    }

                    EngineTests.Eval(s, rawCode, CodeType.Expand, check);
                    if (check == ErrorCheck.Success || check == ErrorCheck.Warning)
                    {
                        if (!testPreserve && File.Exists(destPath) || testPreserve && checkIfPreserve)
                        {
                            using (FileStream srcStream = new FileStream(srcPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                            using (FileStream destStream = new FileStream(destPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                            {
                                byte[] srcDigest = HashHelper.CalcHash(HashHelper.HashType.SHA256, srcStream);
                                byte[] destDigest = HashHelper.CalcHash(HashHelper.HashType.SHA256, destStream);
                                Assert.IsTrue(srcDigest.SequenceEqual(destDigest));
                            }
                        }
                    }
                }
                finally
                {
                    if (Directory.Exists(destDir))
                        Directory.Delete(destDir, true);
                }
            }

            void DirTemplate(string rawCode, ErrorCheck check, bool testPreserve = false, bool checkIfPreserve = true)
            {
                string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandArchive"));
                string srcPath = Path.Combine(dirPath, "Cab");

                try
                {
                    if (testPreserve) // Check preserve
                        File.Create(destDir).Close();

                    EngineTests.Eval(s, rawCode, CodeType.Expand, check);
                    if (check == ErrorCheck.Success || check == ErrorCheck.Warning)
                    {
                        string[] srcFiles = Directory.GetFiles(srcPath, "*", SearchOption.AllDirectories);
                        string[] destFiles = Directory.GetFiles(destDir, "*", SearchOption.AllDirectories);
                        Assert.IsTrue(srcFiles.Length == destFiles.Length);

                        if (!testPreserve && Directory.Exists(destDir) || testPreserve && checkIfPreserve)
                        {
                            for (int i = 0; i < srcFiles.Length; i++)
                            {
                                using (FileStream srcStream = new FileStream(srcFiles[i], FileMode.Open, FileAccess.Read, FileShare.Read))
                                using (FileStream destStream = new FileStream(destFiles[i], FileMode.Open, FileAccess.Read, FileShare.Read))
                                {
                                    byte[] srcDigest = HashHelper.CalcHash(HashHelper.HashType.SHA256, srcStream);
                                    byte[] destDigest = HashHelper.CalcHash(HashHelper.HashType.SHA256, destStream);
                                    Assert.IsTrue(srcDigest.SequenceEqual(destDigest));
                                }
                            }
                        }
                    }
                }
                finally
                {
                    if (Directory.Exists(destDir))
                        Directory.Delete(destDir, true);
                }
            }

            string testCode = $"Expand,\"%TestBench%\\CommandArchive\\ex1.cab\",\"{destDir}\"";
            FileTemplate("ex1.jpg", testCode, ErrorCheck.Success);

            testCode = $"Expand,\"%TestBench%\\CommandArchive\\ex2.cab\",\"{destDir}\",ex2.jpg";
            FileTemplate("ex2.jpg", testCode, ErrorCheck.Success);

            testCode = $"Expand,\"%TestBench%\\CommandArchive\\ex2.cab\",\"{destDir}\",ex1.jpg";
            FileTemplate("ex1.jpg", testCode, ErrorCheck.Error);

            testCode = $"Expand,\"%TestBench%\\CommandArchive\\ex3.jp_\",\"{destDir}\",ex3.jpg,PRESERVE";
            FileTemplate("ex3.jpg", testCode, ErrorCheck.Success, false, true);

            testCode = $"Expand,\"%TestBench%\\CommandArchive\\ex3.jp_\",\"{destDir}\",ex3.jpg,PRESERVE";
            FileTemplate("ex3.jpg", testCode, ErrorCheck.Overwrite, true, false);

            testCode = $"Expand,\"%TestBench%\\CommandArchive\\ex3.jp_\",\"{destDir}\",ex3.jpg,PRESERVE,NOWARN";
            FileTemplate("ex3.jpg", testCode, ErrorCheck.Success, true, false);

            testCode = $"Expand,\"%TestBench%\\CommandArchive\\ex4.cab\",\"{destDir}\"";
            DirTemplate(testCode, ErrorCheck.Success);

            testCode = $"Expand,\"%TestBench%\\CommandArchive\\ex4.cab\",\"{destDir}\",ex3.jpg";
            FileTemplate("ex3.jpg", testCode, ErrorCheck.Success);

            testCode = $"Expand,\"%TestBench%\\CommandArchive\\ex4.cab\",\"{destDir}\",ex2.jpg,NOWARN";
            FileTemplate("ex2.jpg", testCode, ErrorCheck.Success, true);

            testCode = $"Expand,\"%TestBench%\\CommandArchive\\ex4.cab\",\"{destDir}\",ex2.jpg,NOWARN";
            FileTemplate("ex2.jpg", testCode, ErrorCheck.Success, false);
        }
        #endregion

        #region CopyOrExpand
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandArchive")]
        public void CopyOrExpand()
        {
            EngineState s = EngineTests.CreateEngineState();

            CopyOrExpand_1(s);
            CopyOrExpand_2(s);
            CopyOrExpand_3(s);
        }

        public void CopyOrExpand_1(EngineState s)
        {
            string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandArchive"));
            string srcPath = Path.Combine(dirPath, "Cab", "ex3.jpg");
            string destDir = FileHelper.GetTempFileNameEx();
            string destFile = Path.Combine(destDir, "ex3.jpg");

            if (Directory.Exists(destDir))
                Directory.Delete(destDir, true);
            try
            {
                Directory.CreateDirectory(destDir);

                string rawCode = $"CopyOrExpand,\"%TestBench%\\CommandArchive\\ex3.jpg\",\"{destDir}\"";
                EngineTests.Eval(s, rawCode, CodeType.CopyOrExpand, ErrorCheck.Success);

                Assert.IsTrue(Directory.Exists(destDir));
                Assert.IsTrue(File.Exists(destFile));

                using (FileStream srcStream = new FileStream(srcPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (FileStream destStream = new FileStream(destFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    byte[] srcDigest = HashHelper.CalcHash(HashHelper.HashType.SHA256, srcStream);
                    byte[] destDigest = HashHelper.CalcHash(HashHelper.HashType.SHA256, destStream);
                    Assert.IsTrue(srcDigest.SequenceEqual(destDigest));
                }
            }
            finally
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
            }
        }

        public void CopyOrExpand_2(EngineState s)
        {
            string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandArchive"));
            string srcPath = Path.Combine(dirPath, "Cab", "ex3.jpg");
            string destDir = FileHelper.GetTempFileNameEx();
            string destFile = Path.Combine(destDir, "change.jpg");

            if (Directory.Exists(destDir))
                Directory.Delete(destDir, true);

            try
            {
                Directory.CreateDirectory(destDir);

                string rawCode = $"CopyOrExpand,\"%TestBench%\\CommandArchive\\ex3.jpg\",\"{destDir}\\change.jpg\"";
                EngineTests.Eval(s, rawCode, CodeType.CopyOrExpand, ErrorCheck.Success);

                Assert.IsTrue(Directory.Exists(destDir));
                Assert.IsTrue(File.Exists(destFile));

                using (FileStream srcStream = new FileStream(srcPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (FileStream destStream = new FileStream(destFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    byte[] srcDigest = HashHelper.CalcHash(HashHelper.HashType.SHA256, srcStream);
                    byte[] destDigest = HashHelper.CalcHash(HashHelper.HashType.SHA256, destStream);
                    Assert.IsTrue(srcDigest.SequenceEqual(destDigest));
                }
            }
            finally
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
            }
        }

        public void CopyOrExpand_3(EngineState s)
        {
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string rawCode = $"CopyOrExpand,\"%TestBench%\\CommandArchive\\ex5.jpg\",\"{destDir}\"";
            EngineTests.Eval(s, rawCode, CodeType.CopyOrExpand, ErrorCheck.Error);
        }
        #endregion
    }
}
