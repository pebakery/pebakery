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

            string srcDir = Path.Combine(dirPath, SrcDir);
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
    }
}
