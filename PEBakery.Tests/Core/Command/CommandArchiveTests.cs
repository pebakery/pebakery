/*
    Copyright (C) 2017 Hajin Jang
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
*/

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Core;
using System.IO;
using PEBakery.Helper;
using System.Text;
using System.Linq;
using System.Diagnostics;

namespace PEBakery.Tests.Core.Command
{
    [TestClass]
    public class CommandArchiveTests
    {
        #region Compress
        [TestCategory("Command")]
        [TestCategory("CommandArchive")]
        [TestMethod]
        public void Archive_Compress()
        {
            Compress_DirTemplate("Zip", "France", "France_Store.zip", ArchiveHelper.CompressLevel.Store);
            Compress_DirTemplate("Zip", "Korea", "Korea_Best.zip", ArchiveHelper.CompressLevel.Best);
            Compress_FileTemplate("Zip", Path.Combine("Korean_IME_Logo", "Korean_IME_Logo.jpg"), "Korean_IME_Logo_Normal.zip", ArchiveHelper.CompressLevel.Normal);
        }

        public void Compress_DirTemplate(string arcType, string srcDirPath, string destArc, ArchiveHelper.CompressLevel level, string encodingStr = null)
        { // Compress,<ArchiveType>,<SrcPath>,<DestArchive>,[CompressLevel],[UTF8|UTF16|UTF16BE|ANSI]
            EngineState s = EngineTests.CreateEngineState();
            string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandArchive"));
            string destRootDir = Path.Combine(dirPath, "Compress_Dest");
            string destFullPath = Path.Combine(destRootDir, destArc);
            string compRootDir = Path.Combine(dirPath, "Compress_Comp");
            string compDir = Path.Combine(compRootDir, destArc);
            string srcFullPath = Path.Combine(dirPath, srcDirPath);

            try
            {
                Directory.CreateDirectory(destRootDir);
                Directory.CreateDirectory(compDir);

                string rawCode = $"Compress,{arcType},\"%TestBench%\\CommandArchive\\{srcDirPath}\",\"%TestBench%\\CommandArchive\\Compress_Dest\\{destArc}\"";
                if (encodingStr != null)
                    rawCode += "," + encodingStr;
                EngineTests.Eval(s, rawCode, CodeType.Compress, ErrorCheck.Success, out CodeCommand cmd);

                Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Compress));
                CodeInfo_Compress info = cmd.Info as CodeInfo_Compress;

                ArchiveHelper.DecompressManaged(destFullPath, compDir, true, info.Encoding);

                string[] srcFiles = Directory.GetFiles(srcFullPath, "*", SearchOption.AllDirectories);
                string[] destFiles = Directory.GetFiles(compDir, "*", SearchOption.AllDirectories);
                Assert.IsTrue(srcFiles.Length == destFiles.Length);

                for (int i = 0; i < srcFiles.Length; i++)
                {
                    using (FileStream srcStream = new FileStream(srcFiles[i], FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (FileStream destStream = new FileStream(destFiles[i], FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        byte[] srcDigest = HashHelper.CalcHash(HashType.SHA256, srcStream);
                        byte[] destDigest = HashHelper.CalcHash(HashType.SHA256, destStream);
                        Assert.IsTrue(srcDigest.SequenceEqual(destDigest));
                    }
                }
            }
            finally
            {
                Directory.Delete(destRootDir, true);
                Directory.Delete(compRootDir, true);
            }
        }

        public void Compress_FileTemplate(string arcType, string srcFilePath, string destArc, ArchiveHelper.CompressLevel level, string encodingStr = null)
        { // Compress,<ArchiveType>,<SrcPath>,<DestArchive>,[CompressLevel],[UTF8|UTF16|UTF16BE|ANSI]
            EngineState s = EngineTests.CreateEngineState();
            string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandArchive"));
            string destDir = Path.Combine(dirPath, "Compress_Dest");
            string compRootDir = Path.Combine(dirPath, "Compress_Comp");
            string compDir = Path.Combine(compRootDir, destArc);
            string srcFullPath = Path.Combine(dirPath, srcFilePath);
            string destFullPath = Path.Combine(destDir, destArc);
            string srcFileName = Path.GetFileName(srcFilePath);

            try
            {
                Directory.CreateDirectory(destDir);
                Directory.CreateDirectory(compDir);

                string rawCode = $"Compress,{arcType},\"%TestBench%\\CommandArchive\\{srcFilePath}\",\"%TestBench%\\CommandArchive\\Compress_Dest\\{destArc}\"";
                if (encodingStr != null)
                    rawCode += "," + encodingStr;
                EngineTests.Eval(s, rawCode, CodeType.Compress, ErrorCheck.Success, out CodeCommand cmd);

                Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Compress));
                CodeInfo_Compress info = cmd.Info as CodeInfo_Compress;

                ArchiveHelper.DecompressManaged(destFullPath, compDir, true, info.Encoding);

                using (FileStream srcStream = new FileStream(srcFullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (FileStream destStream = new FileStream(Path.Combine(compDir, srcFileName), FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    byte[] srcDigest = HashHelper.CalcHash(HashType.SHA256, srcStream);
                    byte[] destDigest = HashHelper.CalcHash(HashType.SHA256, destStream);
                    Assert.IsTrue(srcDigest.SequenceEqual(destDigest));
                }
            }
            finally
            {
                Directory.Delete(destDir, true);
                Directory.Delete(compRootDir, true);
            }
        }
        #endregion

        #region Decompress
        [TestCategory("Command")]
        [TestCategory("CommandArchive")]
        [TestMethod]
        public void Archive_Decompress()
        {
            Decompress_DirTemplate("Korea.zip");
            Decompress_DirTemplate("Korea.7z");
            Decompress_DirTemplate("France.zip");
            Decompress_DirTemplate("France.7z");
            Decompress_DirTemplate("France.rar"); // RAR5
            Decompress_FileTemplate("Korean_IME_Logo.zip", "Korean_IME_Logo.jpg");
            Decompress_FileTemplate("Korean_IME_Logo.7z", "Korean_IME_Logo.jpg");
            Decompress_FileTemplate("Korean_IME_Logo.rar", "Korean_IME_Logo.jpg"); // Pre-RAR5
        }

        public void Decompress_FileTemplate(string archiveFile, string compFile, string encodingStr = null)
        {
            string archiveType = Path.GetExtension(archiveFile).Substring(1);
            string archiveName = archiveFile.Substring(0, archiveFile.Length - (archiveType.Length + 1));

            EngineState s = EngineTests.CreateEngineState();
            string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandArchive"));
            string srcPath = Path.Combine(dirPath, archiveName, compFile);
            string destPath = Path.Combine(dirPath, "Decompress", compFile);
            
            try
            {
                string rawCode = $"Decompress,\"%TestBench%\\CommandArchive\\{archiveFile}\",\"%TestBench%\\CommandArchive\\Decompress\"";
                if (encodingStr != null)
                    rawCode += "," + encodingStr;
                EngineTests.Eval(s, rawCode, CodeType.Decompress, ErrorCheck.Success);

                using (FileStream srcStream = new FileStream(srcPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (FileStream destStream = new FileStream(destPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    byte[] srcDigest = HashHelper.CalcHash(HashType.SHA256, srcStream);
                    byte[] destDigest = HashHelper.CalcHash(HashType.SHA256, destStream);
                    Assert.IsTrue(srcDigest.SequenceEqual(destDigest));
                }

                Console.WriteLine($"{archiveFile} Success");
            }
            finally
            {
                File.Delete(destPath);
            }
        }

        public void Decompress_DirTemplate(string archiveFile, string encodingStr = null)
        {
            string archiveType = Path.GetExtension(archiveFile).Substring(1);
            string archiveName = archiveFile.Substring(0, archiveFile.Length - (archiveType.Length + 1));

            EngineState s = EngineTests.CreateEngineState();
            string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandArchive"));
            string srcFullPath = Path.Combine(dirPath, archiveName);
            string destRootDir = Path.Combine(dirPath, "Decompress");

            try
            {
                string rawCode = $"Decompress,\"%TestBench%\\CommandArchive\\{archiveFile}\",\"%TestBench%\\CommandArchive\\Decompress\"";
                if (encodingStr != null)
                    rawCode += "," + encodingStr;
                EngineTests.Eval(s, rawCode, CodeType.Decompress, ErrorCheck.Success);

                string[] srcFiles = Directory.GetFiles(srcFullPath, "*", SearchOption.AllDirectories);
                string[] destFiles = Directory.GetFiles(Path.Combine(destRootDir, archiveName), "*", SearchOption.AllDirectories);
                Assert.IsTrue(srcFiles.Length == destFiles.Length);

                for (int i = 0; i < srcFiles.Length; i++)
                {
                    using (FileStream srcStream = new FileStream(srcFiles[i], FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (FileStream destStream = new FileStream(destFiles[i], FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        byte[] srcDigest = HashHelper.CalcHash(HashType.SHA256, srcStream);
                        byte[] destDigest = HashHelper.CalcHash(HashType.SHA256, destStream);
                        Assert.IsTrue(srcDigest.SequenceEqual(destDigest));
                    }
                }
                Console.WriteLine($"{archiveFile} Success");
            }
            finally
            {
                Directory.Delete(Path.Combine(destRootDir, archiveName), true);
            }
        }
        #endregion

        #region Expand
        [TestCategory("Command")]
        [TestCategory("CommandArchive")]
        [TestMethod]
        public void Archive_Expand()
        {
            string rawCode; // $"Expand,\"%TestBench%\\CommandArchive\\{archiveFile}\",\"%TestBench%\\CommandArchive\\Decompress\""

            rawCode = $"Expand,\"%TestBench%\\CommandArchive\\{"ex1.cab"}\",\"%TestBench%\\CommandArchive\\Expand_0\"";
            Expand_FileTemplate("ex1.cab", "ex1.jpg", 0, rawCode, ErrorCheck.Success);

            rawCode = $"Expand,\"%TestBench%\\CommandArchive\\{"ex2.cab"}\",\"%TestBench%\\CommandArchive\\Expand_1\",ex2.jpg";
            Expand_FileTemplate("ex2.cab", "ex2.jpg", 1, rawCode, ErrorCheck.Success);

            rawCode = $"Expand,\"%TestBench%\\CommandArchive\\{"ex2.cab"}\",\"%TestBench%\\CommandArchive\\Expand_2\",ex1.jpg";
            Expand_FileTemplate("ex2.cab", "ex1.jpg", 2, rawCode, ErrorCheck.Error);

            rawCode = $"Expand,\"%TestBench%\\CommandArchive\\{"ex3.jp_"}\",\"%TestBench%\\CommandArchive\\Expand_3\",ex3.jpg,PRESERVE";
            Expand_FileTemplate("ex3.jp_", "ex3.jpg", 3, rawCode, ErrorCheck.Success, false, true);

            rawCode = $"Expand,\"%TestBench%\\CommandArchive\\{"ex3.jp_"}\",\"%TestBench%\\CommandArchive\\Expand_4\",ex3.jpg,PRESERVE";
            Expand_FileTemplate("ex3.jp_", "ex3.jpg", 4, rawCode, ErrorCheck.Warning, true, false);

            rawCode = $"Expand,\"%TestBench%\\CommandArchive\\{"ex3.jp_"}\",\"%TestBench%\\CommandArchive\\Expand_5\",ex3.jpg,PRESERVE,NOWARN";
            Expand_FileTemplate("ex3.jp_", "ex3.jpg", 5, rawCode, ErrorCheck.Success, true, false);

            rawCode = $"Expand,\"%TestBench%\\CommandArchive\\{"ex4.cab"}\",\"%TestBench%\\CommandArchive\\Expand_6\"";
            Expand_DirTemplate("ex4.cab", "Cab", 6, rawCode, ErrorCheck.Success);

            rawCode = $"Expand,\"%TestBench%\\CommandArchive\\{"ex4.cab"}\",\"%TestBench%\\CommandArchive\\Expand_7\",ex3.jpg";
            Expand_FileTemplate("ex4.cab", "ex3.jpg", 7, rawCode, ErrorCheck.Success);

            rawCode = $"Expand,\"%TestBench%\\CommandArchive\\{"ex4.cab"}\",\"%TestBench%\\CommandArchive\\Expand_8\",ex2.jpg,NOWARN";
            Expand_FileTemplate("ex4.cab", "ex2.jpg", 8, rawCode, ErrorCheck.Success, true);

            rawCode = $"Expand,\"%TestBench%\\CommandArchive\\{"ex4.cab"}\",\"%TestBench%\\CommandArchive\\Expand_9\",ex2.jpg,NOWARN";
            Expand_FileTemplate("ex4.cab", "ex2.jpg", 9, rawCode, ErrorCheck.Success, false);
        }

        public void Expand_FileTemplate(string archiveFile, string compFile, int rev, string rawCode, ErrorCheck check, bool testPreserve = false, bool checkIfPreserve = true)
        {
            EngineState s = EngineTests.CreateEngineState();
            string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandArchive"));
            string srcPath = Path.Combine(dirPath, "Cab", compFile);
            string destPath = Path.Combine(dirPath, $"Expand_{rev}", compFile);

            try
            {
                if (testPreserve) // Check preserve
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                    File.Create(destPath).Close();
                }

                EngineTests.Eval(s, rawCode, CodeType.Expand, check);

                if ((!testPreserve && File.Exists(destPath)) 
                    || (testPreserve && checkIfPreserve))
                {
                    using (FileStream srcStream = new FileStream(srcPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (FileStream destStream = new FileStream(destPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        byte[] srcDigest = HashHelper.CalcHash(HashType.SHA256, srcStream);
                        byte[] destDigest = HashHelper.CalcHash(HashType.SHA256, destStream);
                        Assert.IsTrue(srcDigest.SequenceEqual(destDigest));
                    }
                }
                

                Console.WriteLine($"{archiveFile}, {rev} Success");
            }
            finally
            {
                Directory.Delete(Path.GetDirectoryName(destPath), true);
            }
        }

        public void Expand_DirTemplate(string archiveFile, string compDir, int rev, string rawCode, ErrorCheck check, bool testPreserve = false, bool checkIfPreserve = true)
        { 
            EngineState s = EngineTests.CreateEngineState();
            string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandArchive"));
            string srcPath = Path.Combine(dirPath, "Cab");
            string destPath = Path.Combine(dirPath, $"Expand_{rev}");

            try
            {
                if (testPreserve) // Check preserve
                    File.Create(destPath).Close();

                EngineTests.Eval(s, rawCode, CodeType.Expand, check);

                string[] srcFiles = Directory.GetFiles(srcPath, "*", SearchOption.AllDirectories);
                string[] destFiles = Directory.GetFiles(destPath, "*", SearchOption.AllDirectories);
                Assert.IsTrue(srcFiles.Length == destFiles.Length);

                if ((!testPreserve && Directory.Exists(destPath))
                    || (testPreserve && checkIfPreserve))
                {
                    for (int i = 0; i < srcFiles.Length; i++)
                    {
                        using (FileStream srcStream = new FileStream(srcFiles[i], FileMode.Open, FileAccess.Read, FileShare.Read))
                        using (FileStream destStream = new FileStream(destFiles[i], FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            byte[] srcDigest = HashHelper.CalcHash(HashType.SHA256, srcStream);
                            byte[] destDigest = HashHelper.CalcHash(HashType.SHA256, destStream);
                            Assert.IsTrue(srcDigest.SequenceEqual(destDigest));
                        }
                    }
                }
                

                Console.WriteLine($"{archiveFile} Success");
            }
            finally
            {
                Directory.Delete(destPath, true);
            }
        }
        #endregion

        #region CopyOrExpand
        [TestCategory("Command")]
        [TestCategory("CommandArchive")]
        [TestMethod]
        public void Archive_CopyOrExpand()
        {
            CopyOrExpand_1();
            CopyOrExpand_2();
            CopyOrExpand_3();
        }

        public void CopyOrExpand_1()
        {
            EngineState s = EngineTests.CreateEngineState();
            string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandArchive"));
            string srcPath = Path.Combine(dirPath, "Cab", "ex3.jpg");
            string destDir = Path.Combine(dirPath, "CopyOrExpand_1");
            string destFile = Path.Combine(destDir, "ex3.jpg");

            try
            {
                Directory.CreateDirectory(destDir);

                string rawCode = "CopyOrExpand,\"%TestBench%\\CommandArchive\\ex3.jpg\",\"%TestBench%\\CommandArchive\\CopyOrExpand_1\"";
                EngineTests.Eval(s, rawCode, CodeType.CopyOrExpand, ErrorCheck.Success);

                Assert.IsTrue(Directory.Exists(destDir));
                Assert.IsTrue(File.Exists(destFile));

                using (FileStream srcStream = new FileStream(srcPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (FileStream destStream = new FileStream(destFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    byte[] srcDigest = HashHelper.CalcHash(HashType.SHA256, srcStream);
                    byte[] destDigest = HashHelper.CalcHash(HashType.SHA256, destStream);
                    Assert.IsTrue(srcDigest.SequenceEqual(destDigest));
                }
            }
            finally
            {
                Directory.Delete(destDir, true);
            }
        }

        public void CopyOrExpand_2()
        {
            EngineState s = EngineTests.CreateEngineState();
            string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandArchive"));
            string srcPath = Path.Combine(dirPath, "Cab", "ex3.jpg");
            string destDir = Path.Combine(dirPath, "CopyOrExpand_2");
            string destFile = Path.Combine(destDir, "change.jpg");

            try
            {
                Directory.CreateDirectory(destDir);

                string rawCode = "CopyOrExpand,\"%TestBench%\\CommandArchive\\ex3.jpg\",\"%TestBench%\\CommandArchive\\CopyOrExpand_2\\change.jpg\"";
                EngineTests.Eval(s, rawCode, CodeType.CopyOrExpand, ErrorCheck.Success);

                Assert.IsTrue(Directory.Exists(destDir));
                Assert.IsTrue(File.Exists(destFile));

                using (FileStream srcStream = new FileStream(srcPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (FileStream destStream = new FileStream(destFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    byte[] srcDigest = HashHelper.CalcHash(HashType.SHA256, srcStream);
                    byte[] destDigest = HashHelper.CalcHash(HashType.SHA256, destStream);
                    Assert.IsTrue(srcDigest.SequenceEqual(destDigest));
                }
            }
            finally
            {
                Directory.Delete(destDir, true);
            }
        }

        public void CopyOrExpand_3()
        {
            string rawCode = "CopyOrExpand,\"%TestBench%\\CommandArchive\\ex5.jpg\",\"%TestBench%\\CommandArchive\\CopyOrExpand_3\"";
            EngineTests.Eval(rawCode, CodeType.CopyOrExpand, ErrorCheck.Error);
        }
        #endregion

        #region Utility
        private static string SampleText()
        {
            string tempFile = Path.GetTempFileName();
            FileHelper.WriteTextBOM(tempFile, Encoding.UTF8);
            using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
            {
                w.Write("Hello\r\nArchive\r\nPEBakery\r\nUnitTest");
                w.Close();
            }

            return tempFile;
        }
        #endregion
    }
}
