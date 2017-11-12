using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Core;
using PEBakery.Helper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PEBakery.Tests.Core.Command
{
    [TestClass]
    public class CommandFileTests
    {
        #region Const String
        private const string SrcDir = "Src";
        private const string DestDir_FileCopy = "Dest_FileCopy";
        private const string DestDir_FileDelete = "Dest_FileDelete";
        private const string DestDir_FileRename = "Dest_FileRename";
        private const string DestDir_FileCreateBlank = "Dest_FileCreateBlank";
        #endregion

        #region FileCopy
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandFile")]
        public void File_FileCopy()
        { // FileCopy,<SrcFile>,<DestPath>[,PRESERVE][,NOWARN][,NOREC]
            EngineState s = EngineTests.CreateEngineState();

            string scriptDirPath = Path.Combine("%TestBench%", "CommandFile");
            string scriptSrcDir = Path.Combine(scriptDirPath, SrcDir);
            string scriptDestDir = Path.Combine(scriptDirPath, DestDir_FileCopy);

            FileCopy_SingleTemplate(s, $@"FileCopy,{scriptSrcDir}\A.txt,{scriptDestDir}", "A.txt", null);
            FileCopy_SingleTemplate(s, $@"FileCopy,{scriptSrcDir}\A.txt,{scriptDestDir}\B.txt", "A.txt", "B.txt");
            FileCopy_SingleTemplate(s, $@"FileCopy,{scriptSrcDir}\Z\Y.ini,{scriptDestDir}", Path.Combine("Z", "Y.ini"), "Y.ini");
            FileCopy_MultiTemplate(s, $@"FileCopy,{scriptSrcDir}\*.txt,{scriptDestDir}", "*.txt", true);
            FileCopy_MultiTemplate(s, $@"FileCopy,{scriptSrcDir}\*.ini,{scriptDestDir},NOREC", "*.ini", false);

            FileCopy_SingleTemplate(s, $@"FileCopy,{scriptSrcDir}\P.txt,{scriptDestDir}", "P.txt", null, ErrorCheck.Error);
            FileCopy_SingleTemplate(s, $@"FileCopy,{scriptSrcDir}\C.txt,{scriptDestDir}", "C.txt", null, ErrorCheck.Warning, true);
            FileCopy_SingleTemplate(s, $@"FileCopy,{scriptSrcDir}\C.txt,{scriptDestDir},NOWARN", "C.txt", null, ErrorCheck.Success, true);
            FileCopy_SingleTemplate(s, $@"FileCopy,{scriptSrcDir}\C.txt,{scriptDestDir},PRESERVE", "C.txt", null, ErrorCheck.Warning, true);
            FileCopy_SingleTemplate(s, $@"FileCopy,{scriptSrcDir}\C.txt,{scriptDestDir},PRESERVE,NOWARN", "C.txt", null, ErrorCheck.Success, true, true);
        }

        private void FileCopy_SingleTemplate(
            EngineState s, 
            string rawCode,
            string srcFileName, 
            string destFileName,
            ErrorCheck check = ErrorCheck.Success,
            bool preserve = false,
            bool ignoreCompare = false)
        {
            if (destFileName == null)
                destFileName = srcFileName;

            string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandFile"));
            string srcDir = Path.Combine(dirPath, SrcDir);
            string destDir = Path.Combine(dirPath, DestDir_FileCopy);

            string srcFullPath = Path.Combine(srcDir, srcFileName);
            string destFullPath = Path.Combine(destDir, destFileName);

            if (Directory.Exists(destDir))
                Directory.Delete(destDir, true);
            Directory.CreateDirectory(destDir);
            try
            {
                if (preserve)
                    File.Create(destFullPath).Close();

                EngineTests.Eval(s, rawCode, CodeType.FileCopy, check);

                if (check == ErrorCheck.Success && ignoreCompare == false)
                {
                    Assert.IsTrue(File.Exists(destFullPath));
                    
                    using (FileStream srcStream = new FileStream(srcFullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (FileStream destStream = new FileStream(destFullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        byte[] srcDigest = HashHelper.CalcHash(HashType.SHA256, srcStream);
                        byte[] destDigest = HashHelper.CalcHash(HashType.SHA256, destStream);
                        Assert.IsTrue(srcDigest.SequenceEqual(destDigest));
                    }
                }
            }
            finally
            {
                Directory.Delete(destDir, true);
            }
        }

        private void FileCopy_MultiTemplate(EngineState s, string rawCode, string srcFileWildCard, bool recursive, ErrorCheck check = ErrorCheck.Success)
        {
            string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandFile"));
            string srcDir = Path.Combine(dirPath, SrcDir);
            string destDir = Path.Combine(dirPath, DestDir_FileCopy);

            if (Directory.Exists(destDir))
                Directory.Delete(destDir, true);
            Directory.CreateDirectory(destDir);
            try
            {
                EngineTests.Eval(s, rawCode, CodeType.FileCopy, check);

                if (check == ErrorCheck.Success)
                {
                    string[] srcFiles;
                    string[] destFiles;
                    if (recursive)
                    {
                        srcFiles = Directory.GetFiles(srcDir, srcFileWildCard, SearchOption.AllDirectories);
                        destFiles = Directory.GetFiles(destDir, srcFileWildCard, SearchOption.AllDirectories);
                    }
                    else
                    {
                        srcFiles = Directory.GetFiles(srcDir, srcFileWildCard);
                        destFiles = Directory.GetFiles(destDir, srcFileWildCard);
                    }

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
            }
            finally
            {
                Directory.Delete(destDir, true);
            }
        }
        #endregion

        #region FileDelete
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandFile")]
        public void File_FileDelete()
        { // FileDelete,<FilePath>[,NOWARN][,NOREC]
            EngineState s = EngineTests.CreateEngineState();

            string scriptDirPath = Path.Combine("%TestBench%", "CommandFile");
            string scriptSrcDir = Path.Combine(scriptDirPath, SrcDir);
            string scriptDestDir = Path.Combine(scriptDirPath, DestDir_FileDelete);

            FileDelete_SingleTemplate(s, $@"FileDelete,{scriptDestDir}\A.txt", "A.txt");
            FileDelete_SingleTemplate(s, $@"FileDelete,{scriptDestDir}\H.txt", "H.txt", ErrorCheck.Warning);
            FileDelete_SingleTemplate(s, $@"FileDelete,{scriptDestDir}\H.txt,NOWARN", "H.txt", ErrorCheck.Success);
            FileDelete_MultiTemplate(s, $@"FileDelete,{scriptDestDir}\*.ini", "*.ini", ErrorCheck.Success);
            FileDelete_MultiTemplate(s, $@"FileDelete,{scriptDestDir}\*.ini,NOREC", "*.ini", ErrorCheck.Success, false);
        }

        private void FileDelete_SingleTemplate(EngineState s, string rawCode, string fileName, ErrorCheck check = ErrorCheck.Success)
        {
            string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandFile"));

            string srcDir = Path.Combine(dirPath, SrcDir);
            string destDir = Path.Combine(dirPath, DestDir_FileDelete);

            string destFullPath = Path.Combine(destDir, fileName);

            if (Directory.Exists(destDir))
                Directory.Delete(destDir, true);
            FileHelper.WBDirCopy(srcDir, destDir, true, true);
            try
            {
                EngineTests.Eval(s, rawCode, CodeType.FileDelete, check);

                if (check == ErrorCheck.Success)
                {
                    Assert.IsFalse(File.Exists(destFullPath));
                }
            }
            finally
            {
                Directory.Delete(destDir, true);
            }
        }

        private void FileDelete_MultiTemplate(EngineState s, string rawCode, string wildCard, ErrorCheck check = ErrorCheck.Success, bool recursive = true)
        {
            string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandFile"));

            string srcDir = Path.Combine(dirPath, SrcDir);
            string destDir = Path.Combine(dirPath, DestDir_FileDelete);

            string destFullPath = Path.Combine(destDir, wildCard);

            if (Directory.Exists(destDir))
                Directory.Delete(destDir, true);
            FileHelper.WBDirCopy(srcDir, destDir, true, true);
            try
            {
                string[] destFiles;
                if (recursive)
                    destFiles = Directory.GetFiles(destDir, wildCard, SearchOption.AllDirectories);
                else
                    destFiles = Directory.GetFiles(destDir, wildCard);

                EngineTests.Eval(s, rawCode, CodeType.FileDelete, check);

                if (check == ErrorCheck.Success)
                { 
                    for (int i = 0; i < destFiles.Length; i++)
                        Assert.IsFalse(File.Exists(destFullPath));
                }
            }
            finally
            {
                Directory.Delete(destDir, true);
            }
        }
        #endregion

        #region FileRename
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandFile")]
        public void File_FileRename()
        { // FileRename,<SrcPath>,<DestPath>
            EngineState s = EngineTests.CreateEngineState();

            string scriptDirPath = Path.Combine("%TestBench%", "CommandFile");
            string scriptSrcDir = Path.Combine(scriptDirPath, SrcDir);
            string scriptDestDir = Path.Combine(scriptDirPath, DestDir_FileRename);

            FileRename_Template(s, $@"FileRename,{scriptDestDir}\A.txt,{scriptDestDir}\R.txt", "A.txt", "R.txt");
            FileRename_Template(s, $@"FileRename,{scriptDestDir}\A.txt,{scriptDestDir}\B.txt", "A.txt", "B.txt", ErrorCheck.Error);
            FileRename_Template(s, $@"FileRename,{scriptDestDir}\R.txt,{scriptDestDir}\S.txt", "R.txt", "S.txt", ErrorCheck.Error);
            FileRename_Template(s, $@"FileMove,{scriptDestDir}\A.txt,{scriptDestDir}\R.txt", "A.txt", "R.txt");
        }

        private void FileRename_Template(EngineState s, string rawCode, string srcFileName, string destFileName, ErrorCheck check = ErrorCheck.Success)
        {
            string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandFile"));

            string srcDir = Path.Combine(dirPath, SrcDir);
            string destDir = Path.Combine(dirPath, DestDir_FileRename);

            string srcFullPath = Path.Combine(destDir, srcFileName);
            string destFullPath = Path.Combine(destDir, destFileName);

            if (Directory.Exists(destDir))
                Directory.Delete(destDir, true);
            FileHelper.WBDirCopy(srcDir, destDir, true, true);
            try
            {
                if (rawCode.StartsWith("FileRename", StringComparison.OrdinalIgnoreCase))
                    EngineTests.Eval(s, rawCode, CodeType.FileRename, check);
                else
                    EngineTests.Eval(s, rawCode, CodeType.FileMove, check);

                if (check == ErrorCheck.Success)
                {
                    Assert.IsFalse(File.Exists(srcFullPath));
                    Assert.IsTrue(File.Exists(destFullPath));
                }
            }
            finally
            {
                Directory.Delete(destDir, true);
            }
        }
        #endregion

        #region FileCreateBlank
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandFile")]
        public void File_FileCreateBlank()
        { // FileCreateBlank,<FilePath>[,PRESERVE][,NOWARN][,UTF8|UTF16|UTF16BE|ANSI]
            EngineState s = EngineTests.CreateEngineState();

            string scriptDirPath = Path.Combine("%TestBench%", "CommandFile");
            string scriptDestDir = Path.Combine(scriptDirPath, DestDir_FileCreateBlank);

            FileCreateBlank_Template(s, $@"FileCreateBlank,{scriptDestDir}\A.txt", "A.txt", Encoding.Default, false);
            FileCreateBlank_Template(s, $@"FileCreateBlank,{scriptDestDir}\A.txt,UTF8", "A.txt", Encoding.UTF8, false);
            FileCreateBlank_Template(s, $@"FileCreateBlank,{scriptDestDir}\A.txt,UTF16", "A.txt", Encoding.Unicode, false);
            FileCreateBlank_Template(s, $@"FileCreateBlank,{scriptDestDir}\A.txt,UTF16BE", "A.txt", Encoding.BigEndianUnicode, false);
            FileCreateBlank_Template(s, $@"FileCreateBlank,{scriptDestDir}\A.txt", "A.txt", Encoding.Default, true, ErrorCheck.Warning);
            FileCreateBlank_Template(s, $@"FileCreateBlank,{scriptDestDir}\A.txt,PRESERVE", "A.txt", Encoding.Default, true, ErrorCheck.Warning);
            FileCreateBlank_Template(s, $@"FileCreateBlank,{scriptDestDir}\A.txt,PRESERVE", "A.txt", Encoding.Default, false);
            FileCreateBlank_Template(s, $@"FileCreateBlank,{scriptDestDir}\A.txt,NOWARN", "A.txt", Encoding.Default, true);
            FileCreateBlank_Template(s, $@"FileCreateBlank,{scriptDestDir}\A.txt,NOWARN", "A.txt", Encoding.Default, false);
            FileCreateBlank_Template(s, $@"FileCreateBlank,{scriptDestDir}\A.txt,PRESERVE,NOWARN", "A.txt", Encoding.Default, true);
            FileCreateBlank_Template(s, $@"FileCreateBlank,{scriptDestDir}\A.txt,PRESERVE,NOWARN", "A.txt", Encoding.Default, false);
        }

        private void FileCreateBlank_Template(EngineState s, string rawCode, string fileName, Encoding encoding, bool createDummy,  ErrorCheck check = ErrorCheck.Success)
        {
            string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandFile"));

            string destDir = Path.Combine(dirPath, DestDir_FileCreateBlank);

            string destFullPath = Path.Combine(destDir, fileName);

            if (Directory.Exists(destDir))
                Directory.Delete(destDir, true);
            Directory.CreateDirectory(destDir);
            try
            {
                if (createDummy)
                    File.Create(destFullPath).Close();

                EngineTests.Eval(s, rawCode, CodeType.FileCreateBlank, check);

                if (check == ErrorCheck.Success)
                {
                    Assert.IsTrue(File.Exists(destFullPath));
                    Assert.IsTrue(FileHelper.DetectTextEncoding(destFullPath) == encoding);
                }
            }
            finally
            {
                Directory.Delete(destDir, true);
            }
        }
        #endregion

        #region FileSize
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandFile")]
        public void File_FileSize()
        { // FileSize,<FileName>,<DestVar>
            EngineState s = EngineTests.CreateEngineState();

            string scriptDirPath = Path.Combine("%TestBench%", "CommandFile");
            string scriptSrcDir = Path.Combine(scriptDirPath, SrcDir);

            FileSize_Template(s, $@"FileSize,{scriptSrcDir}\A.txt,%Dest%", "1");
            FileSize_Template(s, $@"FileSize,{scriptSrcDir}\B.txt,%Dest%", "2");
            FileSize_Template(s, $@"FileSize,{scriptSrcDir}\C.txt,%Dest%", "3");
            FileSize_Template(s, $@"Retrieve,FileSize,{scriptSrcDir}\C.txt,%Dest%", "3");
        }

        private void FileSize_Template(EngineState s, string rawCode, string comp, ErrorCheck check = ErrorCheck.Success)
        {
            EngineTests.Eval(s, rawCode, CodeType.FileSize, check);

            if (check == ErrorCheck.Success)
            {
                Assert.IsTrue(s.Variables["Dest"].Equals(comp, StringComparison.Ordinal));
            }
        }
        #endregion

        #region FileVersion
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandFile")]
        public void File_FileVersion()
        { // FileVersion,<FilePath>,<DestVar>
            EngineState s = EngineTests.CreateEngineState();

            string scriptDirPath = Path.Combine("%TestBench%", "CommandFile");
            string scriptSrcDir = Path.Combine(scriptDirPath, SrcDir);

            // bt11_exe and bt20_exe BatteryLine's binary (https://github.com/ied206/BatteryLine)
            FileVersion_Template(s, $@"FileVersion,{scriptSrcDir}\bt11_exe,%Dest%", "0.0.0.0");
            FileVersion_Template(s, $@"FileVersion,{scriptSrcDir}\bt20_exe,%Dest%", "2.0.0.0");
            FileVersion_Template(s, $@"Retrieve,FileVersion,{scriptSrcDir}\bt20_exe,%Dest%", "2.0.0.0");
        }

        private void FileVersion_Template(EngineState s, string rawCode, string comp, ErrorCheck check = ErrorCheck.Success)
        {
            EngineTests.Eval(s, rawCode, CodeType.FileVersion, check);

            if (check == ErrorCheck.Success)
            {
                Assert.IsTrue(s.Variables["Dest"].Equals(comp, StringComparison.Ordinal));
            }   
        }
        #endregion
    }
}
