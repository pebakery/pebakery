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
        private const string SrcDir_File = "SrcFile";
        private const string SrcDir_Dir = "SrcDir";
        private const string DestDir_FileCopy = "Dest_FileCopy";
        private const string DestDir_FileDelete = "Dest_FileDelete";
        private const string DestDir_FileRename = "Dest_FileRename";
        private const string DestDir_FileCreateBlank = "Dest_FileCreateBlank";
        private const string DestDir_DirCopy = "Dest_DirCopy";
        private const string DestDir_DirDelete = "Dest_DirDelete";
        private const string DestDir_DirMove = "Dest_DirMove";
        private const string DestDir_DirMake = "Dest_DirMake";
        private const string DestDir_PathMove = "Dest_PathMove";
        #endregion

        #region FileCopy
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandFile")]
        public void File_FileCopy()
        { // FileCopy,<SrcFile>,<DestPath>[,PRESERVE][,NOWARN][,NOREC]
            EngineState s = EngineTests.CreateEngineState();

            string scriptDirPath = Path.Combine("%TestBench%", "CommandFile");
            string scriptSrcDir = Path.Combine(scriptDirPath, SrcDir_File);
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
            string srcDir = Path.Combine(dirPath, SrcDir_File);
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
            string srcDir = Path.Combine(dirPath, SrcDir_File);
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
            string scriptSrcDir = Path.Combine(scriptDirPath, SrcDir_File);
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

            string srcDir = Path.Combine(dirPath, SrcDir_File);
            string destDir = Path.Combine(dirPath, DestDir_FileDelete);

            string destFullPath = Path.Combine(destDir, fileName);

            if (Directory.Exists(destDir))
                Directory.Delete(destDir, true);
            FileHelper.DirectoryCopy(srcDir, destDir, true, true);
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

            string srcDir = Path.Combine(dirPath, SrcDir_File);
            string destDir = Path.Combine(dirPath, DestDir_FileDelete);

            string destFullPath = Path.Combine(destDir, wildCard);

            if (Directory.Exists(destDir))
                Directory.Delete(destDir, true);
            FileHelper.DirectoryCopy(srcDir, destDir, true, true);
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

        #region FileRename, FileMove
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandFile")]
        public void File_FileRename()
        { // FileRename,<SrcPath>,<DestPath>
            EngineState s = EngineTests.CreateEngineState();

            string scriptDirPath = Path.Combine("%TestBench%", "CommandFile");
            string scriptSrcDir = Path.Combine(scriptDirPath, SrcDir_File);
            string scriptDestDir = Path.Combine(scriptDirPath, DestDir_FileRename);

            FileRename_Template(s, $@"FileRename,{scriptDestDir}\A.txt,{scriptDestDir}\R.txt", "A.txt", "R.txt");
            FileRename_Template(s, $@"FileRename,{scriptDestDir}\A.txt,{scriptDestDir}\B.txt", "A.txt", "B.txt", ErrorCheck.Error);
            FileRename_Template(s, $@"FileRename,{scriptDestDir}\R.txt,{scriptDestDir}\S.txt", "R.txt", "S.txt", ErrorCheck.Error);
            FileRename_Template(s, $@"FileMove,{scriptDestDir}\A.txt,{scriptDestDir}\R.txt", "A.txt", "R.txt");
        }

        private void FileRename_Template(EngineState s, string rawCode, string srcFileName, string destFileName, ErrorCheck check = ErrorCheck.Success)
        {
            string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandFile"));

            string srcDir = Path.Combine(dirPath, SrcDir_File);
            string destDir = Path.Combine(dirPath, DestDir_FileRename);

            string srcFullPath = Path.Combine(destDir, srcFileName);
            string destFullPath = Path.Combine(destDir, destFileName);

            if (Directory.Exists(destDir))
                Directory.Delete(destDir, true);
            FileHelper.DirectoryCopy(srcDir, destDir, true, true);
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
            string scriptSrcDir = Path.Combine(scriptDirPath, SrcDir_File);

            FileSize_Template(s, $@"FileSize,{scriptSrcDir}\A.txt,%Dest%", "1");
            FileSize_Template(s, $@"FileSize,{scriptSrcDir}\B.txt,%Dest%", "2");
            FileSize_Template(s, $@"FileSize,{scriptSrcDir}\C.txt,%Dest%", "3");
            FileSize_Template(s, $@"Retrieve,FileSize,{scriptSrcDir}\C.txt,%Dest%", "3");
            FileSize_Template(s, $@"FileSize,{scriptSrcDir}\NotExist,%Dest%", string.Empty, ErrorCheck.Error);
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
            string scriptSrcDir = Path.Combine(scriptDirPath, SrcDir_File);

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

        #region DirCopy
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandFile")]
        public void File_DirCopy()
        {  // DirCopy,<SrcDir>,<DestPath>
            EngineState s = EngineTests.CreateEngineState();

            string scriptDirPath = Path.Combine("%TestBench%", "CommandFile");
            string scriptSrcDir = Path.Combine(scriptDirPath, SrcDir_Dir);
            string scriptDestDir = Path.Combine(scriptDirPath, DestDir_DirCopy);

            DirCopy_SingleTemplate(s, $@"DirCopy,{scriptSrcDir}\ABCD,{scriptDestDir}", "ABCD");
            DirCopy_SingleTemplate(s, $@"DirCopy,{scriptSrcDir}\ABDE,{scriptDestDir}", "ABDE");
            DirCopy_SingleTemplate(s, $@"DirCopy,{scriptSrcDir}\AB*,{scriptDestDir}", "AB*");
            DirCopy_SingleTemplate(s, $@"DirCopy,{scriptSrcDir}\*,{scriptDestDir}", "*");

            s.CompatDirCopyBug = true;
            DirCopy_SingleTemplate(s, $@"DirCopy,{scriptSrcDir}\*,{scriptDestDir}", "*", ErrorCheck.Success, true);
        }

        private void DirCopy_SingleTemplate(
            EngineState s,
            string rawCode,
            string dirName,
            ErrorCheck check = ErrorCheck.Success,
            bool wbBug = false)
        {
            string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandFile"));
            string srcDir = Path.Combine(dirPath, SrcDir_Dir);
            string destDir = Path.Combine(dirPath, DestDir_DirCopy);

            string srcFullPath = Path.Combine(srcDir, dirName);
            string destFullPath = Path.Combine(destDir, dirName);

            if (Directory.Exists(destDir))
                Directory.Delete(destDir, true);

            try
            {
                EngineTests.Eval(s, rawCode, CodeType.DirCopy, check);

                if (check == ErrorCheck.Success)
                {
                    string wildcard = null;
                    if (dirName.IndexOfAny(new char[] { '*', '?' }) != -1)
                        wildcard = dirName;

                    if (wildcard == null)
                    {
                        Assert.IsTrue(Directory.Exists(destFullPath));

                        string[] srcDirs = Directory.GetFiles(srcFullPath, "*", SearchOption.AllDirectories);
                        string[] destDirs = Directory.GetFiles(destFullPath, "*", SearchOption.AllDirectories);

                        Assert.IsTrue(srcDirs.Length == destDirs.Length);

                        for (int i = 0; i < srcDirs.Length; i++)
                            Assert.IsTrue(srcDirs[i].Substring(srcDir.Length).Equals(destDirs[i].Substring(destDir.Length), StringComparison.Ordinal));
                    }
                    else
                    {
                        if (wbBug)
                        {
                            string[] firstSrcFiles = Directory.GetFiles(srcDir, wildcard);
                            string[] firstDestFiles = Directory.GetFiles(destDir, wildcard);
                            Assert.IsTrue(firstDestFiles.Length == firstSrcFiles.Length); 
                        }
                        else
                        {
                            string[] firstDestFiles = Directory.GetFiles(destDir, wildcard);
                            Assert.IsTrue(firstDestFiles.Length == 0);
                        }
                            
                        string[] firstSrcDirs = Directory.GetDirectories(srcDir, wildcard);
                        string[] firstDestDirs = Directory.GetDirectories(destDir, wildcard);
                        Assert.IsTrue(firstSrcDirs.Length == firstDestDirs.Length);

                        for (int i = 0; i < firstSrcDirs.Length; i++)
                        {
                            string[] srcDirs = Directory.GetFiles(firstSrcDirs[i], "*", SearchOption.AllDirectories);
                            string[] destDirs = Directory.GetFiles(firstDestDirs[i], "*", SearchOption.AllDirectories);
                            Assert.IsTrue(srcDirs.Length == destDirs.Length);

                            for (int x = 0; x < srcDirs.Length; x++)
                                Assert.IsTrue(srcDirs[i].Substring(srcDir.Length).Equals(destDirs[i].Substring(destDir.Length), StringComparison.Ordinal));
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
        #endregion

        #region DirDelete
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandFile")]
        public void File_DirDelete()
        { // DirDelete,<DirPath>
            EngineState s = EngineTests.CreateEngineState();

            string scriptDirPath = Path.Combine("%TestBench%", "CommandFile");
            string scriptSrcDir = Path.Combine(scriptDirPath, SrcDir_Dir);
            string scriptDestDir = Path.Combine(scriptDirPath, DestDir_DirDelete);

            DirDelete_Template(s, $@"DirDelete,{scriptDestDir}\ABCD", "ABCD");
            DirDelete_Template(s, $@"DirDelete,{scriptDestDir}\ABDE", "ABDE");
            DirDelete_Template(s, $@"DirDelete,{scriptDestDir}", string.Empty);
            DirDelete_Template(s, $@"DirDelete,{scriptDestDir}\ACDE.txt", "ACDE.txt", ErrorCheck.Error, false);
        }

        private void DirDelete_Template(EngineState s, string rawCode, string dirName, ErrorCheck check = ErrorCheck.Success, bool copyDir = true)
        {
            string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandFile"));
            string srcDir = Path.Combine(dirPath, SrcDir_Dir);
            string destDir = Path.Combine(dirPath, DestDir_DirDelete);

            string srcFullPath = Path.Combine(srcDir, dirName);
            string destFullPath = Path.Combine(destDir, dirName);

            if (Directory.Exists(destDir))
                Directory.Delete(destDir, true);
            try
            {
                if (copyDir)
                    FileHelper.DirectoryCopy(srcFullPath, destFullPath, true, true);

                EngineTests.Eval(s, rawCode, CodeType.DirDelete, check);

                if (check == ErrorCheck.Success)
                {
                    Assert.IsFalse(Directory.Exists(destFullPath));
                }
            }
            finally
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
            }
        }
        #endregion

        #region DirMove
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandFile")]
        public void File_DirMove()
        { // DirMove,<SrcDir>,<DestPath>
            EngineState s = EngineTests.CreateEngineState();

            string scriptDirPath = Path.Combine("%TestBench%", "CommandFile");
            string scriptSrcDir = Path.Combine(scriptDirPath, SrcDir_Dir);
            string scriptDestDir = Path.Combine(scriptDirPath, DestDir_DirMove);

            DirMove_Template(s, $@"DirMove,{scriptDestDir}\ABCD,{scriptDestDir}\XYZ", "ABCD", "XYZ");
            DirMove_Template(s, $@"DirMove,{scriptDestDir}\ABDE,{scriptDestDir}\ABCD", "ABDE", Path.Combine("ABCD", "ABDE"));
            DirMove_Template(s, $@"DirMove,{scriptDestDir}\XYZ,{scriptDestDir}\WUV", "XYZ", "WUV", ErrorCheck.Error);
            DirMove_Template(s, $@"DirMove,{scriptDestDir}\ACDE.txt,{scriptDestDir}\XYZ", "ACDE.txt", "XYZ", ErrorCheck.Error);
        }

        private void DirMove_Template(EngineState s, string rawCode, string srcDirName, string destDirName, ErrorCheck check = ErrorCheck.Success)
        {
            string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandFile"));

            string srcDir = Path.Combine(dirPath, SrcDir_Dir);
            string destDir = Path.Combine(dirPath, DestDir_DirMove);

            string srcFullPath = Path.Combine(destDir, srcDirName);
            string destFullPath = Path.Combine(destDir, destDirName);

            if (Directory.Exists(destDir))
                Directory.Delete(destDir, true);
            FileHelper.DirectoryCopy(srcDir, destDir, true, true);
            try
            {
                EngineTests.Eval(s, rawCode, CodeType.DirMove, check);

                if (check == ErrorCheck.Success)
                {
                    Assert.IsFalse(Directory.Exists(srcFullPath));
                    Assert.IsTrue(Directory.Exists(destFullPath));
                }
            }
            finally
            {
                Directory.Delete(destDir, true);
            }
        }
        #endregion

        #region DirMake
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandFile")]
        public void File_DirMake()
        { // DirMake,<DestDir>
            EngineState s = EngineTests.CreateEngineState();

            string scriptDirPath = Path.Combine("%TestBench%", "CommandFile");
            string scriptSrcDir = Path.Combine(scriptDirPath, SrcDir_Dir);
            string scriptDestDir = Path.Combine(scriptDirPath, DestDir_DirMake);

            DirMake_Template(s, $@"DirMake,{scriptDestDir}\A", "A");
            DirMake_Template(s, $@"DirMake,{scriptDestDir}\A", "A", ErrorCheck.Success, true, false);
            DirMake_Template(s, $@"DirMake,{scriptDestDir}\A", "A", ErrorCheck.Error, false, true);
        }

        private void DirMake_Template(EngineState s, string rawCode, string dirName,
            ErrorCheck check = ErrorCheck.Success, bool createDir = false, bool createFile = false)
        {
            string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandFile"));

            string destDir = Path.Combine(dirPath, DestDir_DirMake);

            string destFullPath = Path.Combine(destDir, dirName);

            if (Directory.Exists(destDir))
                Directory.Delete(destDir, true);

            try
            {
                if (createDir)
                {
                    Directory.CreateDirectory(destFullPath);
                }
                else if (createFile)
                {
                    Directory.CreateDirectory(destDir);
                    File.Create(destFullPath).Close();
                }

                EngineTests.Eval(s, rawCode, CodeType.DirMake, check);

                if (check == ErrorCheck.Success)
                {
                    Assert.IsTrue(Directory.Exists(destFullPath));
                }
            }
            finally
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
            }
        }
        #endregion

        #region DirSize
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandFile")]
        public void File_DirSize()
        { // DirSize,<Path>,<DestVar>
            EngineState s = EngineTests.CreateEngineState();

            string scriptDirPath = Path.Combine("%TestBench%", "CommandFile");
            string scriptSrcDir = Path.Combine(scriptDirPath, SrcDir_Dir);

            // Reuse FileSize_Template
            DirSize_Template(s, $@"DirSize,{scriptSrcDir}\ABCD,%Dest%", "9");
            DirSize_Template(s, $@"DirSize,{scriptSrcDir}\ABDE,%Dest%", "3");
            DirSize_Template(s, $@"DirSize,{scriptSrcDir},%Dest%", "13");
            DirSize_Template(s, $@"Retrieve,FolderSize,{scriptSrcDir},%Dest%", "13");
            DirSize_Template(s, $@"DirSize,{scriptSrcDir}\NotExist,%Dest%", string.Empty, ErrorCheck.Error);
        }

        private void DirSize_Template(EngineState s, string rawCode, string comp, ErrorCheck check = ErrorCheck.Success)
        {
            EngineTests.Eval(s, rawCode, CodeType.DirSize, check);

            if (check == ErrorCheck.Success)
            {
                Assert.IsTrue(s.Variables["Dest"].Equals(comp, StringComparison.Ordinal));
            }
        }
        #endregion

        #region PathMove
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandFile")]
        public void File_PathMove()
        { // PathMove,<SrcPath>,<DestPath>
            EngineState s = EngineTests.CreateEngineState();

            string scriptDirPath = Path.Combine("%TestBench%", "CommandFile");
            string scriptDestDir = Path.Combine(scriptDirPath, DestDir_PathMove);

            PathMove_FileTemplate(s, $@"PathMove,{scriptDestDir}\A.txt,{scriptDestDir}\R.txt", "A.txt", "R.txt");
            PathMove_DirTemplate(s, $@"PathMove,{scriptDestDir}\ABCD,{scriptDestDir}\XYZ", "ABCD", "XYZ");
        }

        private void PathMove_FileTemplate(EngineState s, string rawCode, string srcFileName, string destFileName, ErrorCheck check = ErrorCheck.Success)
        {
            string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandFile"));

            string srcDir = Path.Combine(dirPath, SrcDir_File);
            string destDir = Path.Combine(dirPath, DestDir_PathMove);

            string srcFullPath = Path.Combine(destDir, srcFileName);
            string destFullPath = Path.Combine(destDir, destFileName);

            if (Directory.Exists(destDir))
                Directory.Delete(destDir, true);
            FileHelper.DirectoryCopy(srcDir, destDir, true, true);
            try
            {
                EngineTests.Eval(s, rawCode, CodeType.PathMove, check);

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

        private void PathMove_DirTemplate(EngineState s, string rawCode, string srcFileName, string destFileName, ErrorCheck check = ErrorCheck.Success)
        {
            string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandFile"));

            string srcDir = Path.Combine(dirPath, SrcDir_Dir);
            string destDir = Path.Combine(dirPath, DestDir_PathMove);

            string srcFullPath = Path.Combine(destDir, srcFileName);
            string destFullPath = Path.Combine(destDir, destFileName);

            if (Directory.Exists(destDir))
                Directory.Delete(destDir, true);
            FileHelper.DirectoryCopy(srcDir, destDir, true, true);
            try
            {
                EngineTests.Eval(s, rawCode, CodeType.PathMove, check);

                if (check == ErrorCheck.Success)
                {
                    Assert.IsFalse(Directory.Exists(srcFullPath));
                    Assert.IsTrue(Directory.Exists(destFullPath));
                }
            }
            finally
            {
                Directory.Delete(destDir, true);
            }
        }
        #endregion
    }
}
