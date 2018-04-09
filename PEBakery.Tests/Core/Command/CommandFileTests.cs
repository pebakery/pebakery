using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Core;
using PEBakery.Helper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PEBakery.Tests.Core.Command
{
    [TestClass]
    [SuppressMessage("ReSharper", "ParameterOnlyUsedForPreconditionCheck.Local")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class CommandFileTests
    {
        #region Const String
        private const string SrcDir_File = "SrcFile";
        private const string SrcDir_Dir = "SrcDir";
        #endregion

        #region FileCopy
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandFile")]
        public void File_FileCopy()
        { // FileCopy,<SrcFile>,<DestPath>[,PRESERVE][,NOWARN][,NOREC]
            EngineState s = EngineTests.CreateEngineState();

            string pbDirPath = Path.Combine("%TestBench%", "CommandFile");
            string pbSrcDir = Path.Combine(pbDirPath, SrcDir_File);
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            FileCopy_SingleTemplate(s, $@"FileCopy,{pbSrcDir}\A.txt,{destDir}", "A.txt", destDir, null);
            FileCopy_SingleTemplate(s, $@"FileCopy,{pbSrcDir}\A.txt,{destDir}\B.txt", "A.txt", destDir, "B.txt");
            FileCopy_SingleTemplate(s, $@"FileCopy,{pbSrcDir}\Z\Y.ini,{destDir}", Path.Combine("Z", "Y.ini"), destDir, "Y.ini");
            FileCopy_MultiTemplate(s, $@"FileCopy,{pbSrcDir}\*.txt,{destDir}", "*.txt", destDir, true);
            FileCopy_MultiTemplate(s, $@"FileCopy,{pbSrcDir}\*.ini,{destDir},NOREC", "*.ini", destDir, false);

            FileCopy_SingleTemplate(s, $@"FileCopy,{pbSrcDir}\P.txt,{destDir}", "P.txt", destDir, null, ErrorCheck.Error);
            FileCopy_SingleTemplate(s, $@"FileCopy,{pbSrcDir}\C.txt,{destDir}", "C.txt", destDir, null, ErrorCheck.Overwrite, true);
            FileCopy_SingleTemplate(s, $@"FileCopy,{pbSrcDir}\C.txt,{destDir},NOWARN", "C.txt", destDir, null, ErrorCheck.Success, true);
            FileCopy_SingleTemplate(s, $@"FileCopy,{pbSrcDir}\C.txt,{destDir},PRESERVE", "C.txt", destDir, null, ErrorCheck.Overwrite, true);
            FileCopy_SingleTemplate(s, $@"FileCopy,{pbSrcDir}\C.txt,{destDir},PRESERVE,NOWARN", "C.txt", destDir, null, ErrorCheck.Success, true, true);
        }

        private void FileCopy_SingleTemplate(
            EngineState s, 
            string rawCode,
            string srcFileName, 
            string destDir,
            string destFileName,
            ErrorCheck check = ErrorCheck.Success,
            bool preserve = false,
            bool ignoreCompare = false)
        {
            if (destFileName == null)
                destFileName = srcFileName;

            string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandFile"));
            string srcDir = Path.Combine(dirPath, SrcDir_File);

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

        private void FileCopy_MultiTemplate(
            EngineState s,
            string rawCode,
            string srcFileWildCard,
            string destDir,
            bool recursive,
            ErrorCheck check = ErrorCheck.Success)
        {
            string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandFile"));
            string srcDir = Path.Combine(dirPath, SrcDir_File);

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
        #endregion

        #region FileDelete
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandFile")]
        public void File_FileDelete()
        { // FileDelete,<FilePath>[,NOWARN][,NOREC]
            EngineState s = EngineTests.CreateEngineState();

            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            FileDelete_SingleTemplate(s, $@"FileDelete,{destDir}\A.txt", "A.txt", destDir);
            FileDelete_SingleTemplate(s, $@"FileDelete,{destDir}\H.txt", "H.txt", destDir, ErrorCheck.Warning);
            FileDelete_SingleTemplate(s, $@"FileDelete,{destDir}\H.txt,NOWARN", "H.txt", destDir, ErrorCheck.Success);
            FileDelete_MultiTemplate(s, $@"FileDelete,{destDir}\*.ini", "*.ini", destDir, ErrorCheck.Success);
            FileDelete_MultiTemplate(s, $@"FileDelete,{destDir}\*.ini,NOREC", "*.ini", destDir, ErrorCheck.Success, false);
        }

        private void FileDelete_SingleTemplate(EngineState s, string rawCode, string fileName, string destDir, ErrorCheck check = ErrorCheck.Success)
        {
            string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandFile"));

            string srcDir = Path.Combine(dirPath, SrcDir_File);

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
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
            }
        }

        private void FileDelete_MultiTemplate(EngineState s, string rawCode, string wildCard, string destDir, ErrorCheck check = ErrorCheck.Success, bool recursive = true)
        {
            string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandFile"));

            string srcDir = Path.Combine(dirPath, SrcDir_File);

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
                if (Directory.Exists(destDir))
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

            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            FileRename_Template(s, $@"FileRename,{destDir}\A.txt,{destDir}\R.txt", "A.txt", destDir, "R.txt");
            FileRename_Template(s, $@"FileRename,{destDir}\A.txt,{destDir}\B.txt", "A.txt", destDir, "B.txt", ErrorCheck.Error);
            FileRename_Template(s, $@"FileRename,{destDir}\R.txt,{destDir}\S.txt", "R.txt", destDir, "S.txt", ErrorCheck.Error);
            FileRename_Template(s, $@"FileMove,{destDir}\A.txt,{destDir}\R.txt", "A.txt", destDir, "R.txt");
        }

        private void FileRename_Template(EngineState s, string rawCode, string srcFileName, string destDir, string destFileName, ErrorCheck check = ErrorCheck.Success)
        {
            string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandFile"));

            string srcDir = Path.Combine(dirPath, SrcDir_File);

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
                if (Directory.Exists(destDir))
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

            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            FileCreateBlank_Template(s, $@"FileCreateBlank,{destDir}\A.txt", destDir, "A.txt", Encoding.Default, false);
            FileCreateBlank_Template(s, $@"FileCreateBlank,{destDir}\A.txt,UTF8", destDir, "A.txt", Encoding.UTF8, false);
            FileCreateBlank_Template(s, $@"FileCreateBlank,{destDir}\A.txt,UTF16", destDir, "A.txt", Encoding.Unicode, false);
            FileCreateBlank_Template(s, $@"FileCreateBlank,{destDir}\A.txt,UTF16BE", destDir, "A.txt", Encoding.BigEndianUnicode, false);
            FileCreateBlank_Template(s, $@"FileCreateBlank,{destDir}\A.txt", destDir, "A.txt", Encoding.Default, true, ErrorCheck.Overwrite);
            FileCreateBlank_Template(s, $@"FileCreateBlank,{destDir}\A.txt,PRESERVE", destDir, "A.txt", Encoding.Default, true, ErrorCheck.Overwrite);
            FileCreateBlank_Template(s, $@"FileCreateBlank,{destDir}\A.txt,PRESERVE", destDir, "A.txt", Encoding.Default, false);
            FileCreateBlank_Template(s, $@"FileCreateBlank,{destDir}\A.txt,NOWARN", destDir, "A.txt", Encoding.Default, true);
            FileCreateBlank_Template(s, $@"FileCreateBlank,{destDir}\A.txt,NOWARN", destDir, "A.txt", Encoding.Default, false);
            FileCreateBlank_Template(s, $@"FileCreateBlank,{destDir}\A.txt,PRESERVE,NOWARN", destDir, "A.txt", Encoding.Default, true);
            FileCreateBlank_Template(s, $@"FileCreateBlank,{destDir}\A.txt,PRESERVE,NOWARN", destDir, "A.txt", Encoding.Default, false);
        }

        private void FileCreateBlank_Template(EngineState s, string rawCode, string destDir, string fileName, Encoding encoding, bool createDummy,  ErrorCheck check = ErrorCheck.Success)
        {
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
                    Assert.IsTrue(FileHelper.DetectTextEncoding(destFullPath).Equals(encoding));
                }
            }
            finally
            {
                if (Directory.Exists(destDir))
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

            string pbDirPath = Path.Combine("%TestBench%", "CommandFile");
            string pbSrcDir = Path.Combine(pbDirPath, SrcDir_Dir);
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            DirCopy_SingleTemplate(s, $@"DirCopy,{pbSrcDir}\ABCD,{destDir}", destDir, "ABCD");
            DirCopy_SingleTemplate(s, $@"DirCopy,{pbSrcDir}\ABDE,{destDir}", destDir, "ABDE");
            DirCopy_SingleTemplate(s, $@"DirCopy,{pbSrcDir}\AB*,{destDir}", destDir, "AB*");
            DirCopy_SingleTemplate(s, $@"DirCopy,{pbSrcDir}\*,{destDir}", destDir, "*");

            s.CompatDirCopyBug = true;
            DirCopy_SingleTemplate(s, $@"DirCopy,{pbSrcDir}\*,{destDir}", destDir, "*", ErrorCheck.Success, true);
        }

        private void DirCopy_SingleTemplate(
            EngineState s,
            string rawCode,
            string destDir,
            string dirName,
            ErrorCheck check = ErrorCheck.Success,
            bool wbBug = false)
        {
            string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandFile"));
            string srcDir = Path.Combine(dirPath, SrcDir_Dir);

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

            string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            Directory.CreateDirectory(tempDir);
            try
            {
                DirDelete_Template(s, $@"DirDelete,{tempDir}\ABCD", tempDir, "ABCD");
                DirDelete_Template(s, $@"DirDelete,{tempDir}\ABDE", tempDir, "ABDE");
                DirDelete_Template(s, $@"DirDelete,{tempDir}", tempDir, string.Empty);
                DirDelete_Template(s, $@"DirDelete,{tempDir}\ACDE.txt", tempDir, "ACDE.txt", ErrorCheck.Error, false);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        private void DirDelete_Template(EngineState s, string rawCode, string destDir, string dirName, ErrorCheck check = ErrorCheck.Success, bool copyDir = true)
        {
            string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandFile"));
            string srcDir = Path.Combine(dirPath, SrcDir_Dir);

            string srcFullPath = Path.Combine(srcDir, dirName);
            string destFullPath = Path.Combine(destDir, dirName);

            if (Directory.Exists(destFullPath))
                Directory.Delete(destFullPath, true);
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
                if (Directory.Exists(destFullPath))
                    Directory.Delete(destFullPath, true);
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

            string varSrcDir = Path.Combine("%TestBench%", "CommandFile", SrcDir_Dir);
            string srcDir = StringEscaper.Preprocess(s, varSrcDir);
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            void Template(string rawCode, string srcDirName, string destDirName, ErrorCheck check = ErrorCheck.Success)
            {
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

            // string scriptDestDir = Path.Combine(scriptDirPath, DestDir_DirMove);
            Template($@"DirMove,{destDir}\ABCD,{destDir}\XYZ", "ABCD", "XYZ");
            Template($@"DirMove,{destDir}\ABDE,{destDir}\ABCD", "ABDE", Path.Combine("ABCD", "ABDE"));
            Template($@"DirMove,{destDir}\XYZ,{destDir}\WUV", "XYZ", "WUV", ErrorCheck.Error);
            Template($@"DirMove,{destDir}\ACDE.txt,{destDir}\XYZ", "ACDE.txt", "XYZ", ErrorCheck.Error);
        }
        #endregion

        #region DirMake
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandFile")]
        public void File_DirMake()
        { // DirMake,<DestDir>
            EngineState s = EngineTests.CreateEngineState();
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            DirMake_Template(s, $@"DirMake,{destDir}\A", destDir, "A");
            DirMake_Template(s, $@"DirMake,{destDir}\A", destDir, "A", ErrorCheck.Success, true, false);
            DirMake_Template(s, $@"DirMake,{destDir}\A", destDir, "A", ErrorCheck.Error, false, true);
        }

        private void DirMake_Template(EngineState s, string rawCode, string destDir, string dirName,
            ErrorCheck check = ErrorCheck.Success, bool createDir = false, bool createFile = false)
        {
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
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            PathMove_FileTemplate(s, $@"PathMove,{destDir}\A.txt,{destDir}\R.txt", "A.txt", destDir, "R.txt");
            PathMove_DirTemplate(s, $@"PathMove,{destDir}\ABCD,{destDir}\XYZ", "ABCD", destDir, "XYZ");
        }

        private void PathMove_FileTemplate(EngineState s, string rawCode, string srcFileName, string destDir, string destFileName, ErrorCheck check = ErrorCheck.Success)
        {
            string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandFile"));

            string srcDir = Path.Combine(dirPath, SrcDir_File);
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
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
            }
        }

        private void PathMove_DirTemplate(EngineState s, string rawCode, string srcFileName, string destDir, string destFileName, ErrorCheck check = ErrorCheck.Success)
        {
            string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandFile"));

            string srcDir = Path.Combine(dirPath, SrcDir_Dir);
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
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
            }
        }
        #endregion
    }
}
