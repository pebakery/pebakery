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

namespace UnitTest.Core.Command
{
    [TestClass]
    public class CommandArchiveTests
    {
        #region Compress
        [TestCategory("Command")]
        [TestCategory("CommandArchive")]
        [TestMethod]
        public void Compress()
        {
            Compress_DirTemplate("Zip", "France", "France_Store.zip", ArchiveHelper.CompressLevel.Store);
            Compress_DirTemplate("Zip", "Korea", "Korea_Best.zip", ArchiveHelper.CompressLevel.Best);
            Compress_FileTemplate("Zip", Path.Combine("UDL", "UDL_PEBakery.xml"), "UDL_Normal.zip", ArchiveHelper.CompressLevel.Normal);
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
                    using (FileStream srcStream = new FileStream(srcFiles[i], FileMode.Open))
                    using (FileStream destStream = new FileStream(destFiles[i], FileMode.Open))
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

                using (FileStream srcStream = new FileStream(srcFullPath, FileMode.Open))
                using (FileStream destStream = new FileStream(Path.Combine(compDir, srcFileName), FileMode.Open))
                {
                    byte[] srcDigest = HashHelper.CalcHash(HashType.SHA256, srcStream);
                    byte[] destDigest = HashHelper.CalcHash(HashType.SHA256, destStream);
                    Assert.IsTrue(srcDigest.SequenceEqual(destDigest));
                }

                /*
                string[] srcFiles = Directory.GetFiles(srcFilePath, "*", SearchOption.AllDirectories);
                string[] destFiles = Directory.GetFiles(destDir, "*", SearchOption.AllDirectories);
                Assert.IsTrue(srcFiles.Length == destFiles.Length);

                for (int i = 0; i < srcFiles.Length; i++)
                {
                    using (FileStream srcStream = new FileStream(srcFiles[i], FileMode.Open))
                    using (FileStream destStream = new FileStream(destFiles[i], FileMode.Open))
                    {
                        byte[] srcDigest = HashHelper.CalcHash(HashType.SHA256, srcStream);
                        byte[] destDigest = HashHelper.CalcHash(HashType.SHA256, destStream);
                        Assert.IsTrue(srcDigest.SequenceEqual(destDigest));
                    }
                }
                */
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
        public void Decompress()
        {
            Decompress_DirTemplate("Korea.zip");
            Decompress_DirTemplate("Korea.7z");
            Decompress_DirTemplate("France.zip");
            Decompress_DirTemplate("France.7z");
            Decompress_DirTemplate("France.rar"); // RAR5
            Decompress_FileTemplate("UDL.zip", "UDL_PEBakery.xml");
            Decompress_FileTemplate("UDL.7z", "UDL_PEBakery.xml");
            Decompress_FileTemplate("UDL.rar", "UDL_PEBakery.xml"); // Pre-RAR5
        }

        public void Decompress_FileTemplate(string archiveFile, string compFile, string encodingStr = null)
        { // Decompress,<SrcArchive>,<DestDir>,[UTF8|UTF16|UTF16BE|ANSI]
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

                using (FileStream srcStream = new FileStream(srcPath, FileMode.Open))
                using (FileStream destStream = new FileStream(destPath, FileMode.Open))
                {
                    byte[] srcDigest = HashHelper.CalcHash(HashType.SHA256, srcStream);
                    byte[] destDigest = HashHelper.CalcHash(HashType.SHA256, destStream);
                    Assert.IsTrue(srcDigest.SequenceEqual(destDigest));
                }
            }
            finally
            {
                File.Delete(destPath);
            }
        }

        public void Decompress_DirTemplate(string archiveFile, string encodingStr = null)
        { // Decompress,<SrcArchive>,<DestDir>,[UTF8|UTF16|UTF16BE|ANSI]
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
                    using (FileStream srcStream = new FileStream(srcFiles[i], FileMode.Open))
                    using (FileStream destStream = new FileStream(destFiles[i], FileMode.Open))
                    {
                        byte[] srcDigest = HashHelper.CalcHash(HashType.SHA256, srcStream);
                        byte[] destDigest = HashHelper.CalcHash(HashType.SHA256, destStream);
                        Assert.IsTrue(srcDigest.SequenceEqual(destDigest));
                    }
                }
            }
            finally
            {
                Directory.Delete(Path.Combine(destRootDir, archiveName), true);
            }
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
