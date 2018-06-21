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
    public class CommandFileTests
    {
        #region Const String
        private const string SrcDirFile = "SrcFile";
        private const string SrcDirDir = "SrcDir";
        #endregion

        #region FileCopy
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandFile")]
        public void FileCopy()
        {
            EngineState s = EngineTests.CreateEngineState();

            string pbDirPath = Path.Combine("%TestBench%", "CommandFile");
            string pbSrcDir = Path.Combine(pbDirPath, SrcDirFile);
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            void SingleTemplate(string rawCode, string srcFileName, string destFileName,
                ErrorCheck check = ErrorCheck.Success, bool preserve = false, bool ignoreCompare = false)
            {
                if (destFileName == null)
                    destFileName = srcFileName;

                string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandFile"));
                string srcDir = Path.Combine(dirPath, SrcDirFile);

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

                    if (check == ErrorCheck.Success && !ignoreCompare)
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

            void MultiTemplate(string rawCode, string srcFileWildCard, bool recursive, ErrorCheck check = ErrorCheck.Success)
            {
                string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandFile"));
                string srcDir = Path.Combine(dirPath, SrcDirFile);

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

            SingleTemplate($@"FileCopy,{pbSrcDir}\A.txt,{destDir}", "A.txt", null);
            SingleTemplate($@"FileCopy,{pbSrcDir}\A.txt,{destDir}\B.txt", "A.txt", "B.txt");
            SingleTemplate($@"FileCopy,{pbSrcDir}\Z\Y.ini,{destDir}", Path.Combine("Z", "Y.ini"), "Y.ini");
            MultiTemplate($@"FileCopy,{pbSrcDir}\*.txt,{destDir}", "*.txt", true);
            MultiTemplate($@"FileCopy,{pbSrcDir}\*.ini,{destDir},NOREC", "*.ini", false);

            SingleTemplate($@"FileCopy,{pbSrcDir}\P.txt,{destDir}", "P.txt", null, ErrorCheck.Error);
            SingleTemplate($@"FileCopy,{pbSrcDir}\C.txt,{destDir}", "C.txt", null, ErrorCheck.Overwrite, true);
            SingleTemplate($@"FileCopy,{pbSrcDir}\C.txt,{destDir},NOWARN", "C.txt", null, ErrorCheck.Success, true);
            SingleTemplate($@"FileCopy,{pbSrcDir}\C.txt,{destDir},PRESERVE", "C.txt", null, ErrorCheck.Overwrite, true);
            SingleTemplate($@"FileCopy,{pbSrcDir}\C.txt,{destDir},PRESERVE,NOWARN", "C.txt", null, ErrorCheck.Success, true, true);
        }
        #endregion

        #region FileDelete
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandFile")]
        public void FileDelete()
        {
            EngineState s = EngineTests.CreateEngineState();
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            void SingleTemplate(string rawCode, string fileName, ErrorCheck check = ErrorCheck.Success)
            {
                string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandFile"));

                string srcDir = Path.Combine(dirPath, SrcDirFile);

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

            void MultiTemplate(string rawCode, string wildCard, ErrorCheck check = ErrorCheck.Success, bool recursive = true)
            {
                string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandFile"));
                string srcDir = Path.Combine(dirPath, SrcDirFile);
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

            SingleTemplate($@"FileDelete,{destDir}\A.txt", "A.txt");
            SingleTemplate($@"FileDelete,{destDir}\H.txt", "H.txt", ErrorCheck.Warning);
            SingleTemplate($@"FileDelete,{destDir}\H.txt,NOWARN", "H.txt", ErrorCheck.Success);
            MultiTemplate($@"FileDelete,{destDir}\*.ini", "*.ini", ErrorCheck.Success);
            MultiTemplate($@"FileDelete,{destDir}\*.ini,NOREC", "*.ini", ErrorCheck.Success, false);
        }
        #endregion

        #region FileRename, FileMove
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandFile")]
        public void FileRename()
        {
            EngineState s = EngineTests.CreateEngineState();
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            void Template(string rawCode, string srcFileName, string destFileName, ErrorCheck check = ErrorCheck.Success)
            {
                string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandFile"));
                string srcDir = Path.Combine(dirPath, SrcDirFile);
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
            Template($@"FileRename,{destDir}\A.txt,{destDir}\R.txt", "A.txt", "R.txt");
            Template($@"FileRename,{destDir}\A.txt,{destDir}\B.txt", "A.txt", "B.txt", ErrorCheck.Error);
            Template($@"FileRename,{destDir}\R.txt,{destDir}\S.txt", "R.txt", "S.txt", ErrorCheck.Error);
            Template($@"FileMove,{destDir}\A.txt,{destDir}\R.txt", "A.txt", "R.txt");
        }
        #endregion

        #region FileCreateBlank
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandFile")]
        public void FileCreateBlank()
        {
            EngineState s = EngineTests.CreateEngineState();
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            void Template(string rawCode, string fileName, Encoding encoding, bool createDummy, ErrorCheck check = ErrorCheck.Success)
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

            Template($@"FileCreateBlank,{destDir}\A.txt", "A.txt", Encoding.Default, false);
            Template($@"FileCreateBlank,{destDir}\A.txt,UTF8", "A.txt", Encoding.UTF8, false);
            Template($@"FileCreateBlank,{destDir}\A.txt,UTF16", "A.txt", Encoding.Unicode, false);
            Template($@"FileCreateBlank,{destDir}\A.txt,UTF16BE", "A.txt", Encoding.BigEndianUnicode, false);
            Template($@"FileCreateBlank,{destDir}\A.txt", "A.txt", Encoding.Default, true, ErrorCheck.Overwrite);
            Template($@"FileCreateBlank,{destDir}\A.txt,PRESERVE", "A.txt", Encoding.Default, true, ErrorCheck.Overwrite);
            Template($@"FileCreateBlank,{destDir}\A.txt,PRESERVE", "A.txt", Encoding.Default, false);
            Template($@"FileCreateBlank,{destDir}\A.txt,NOWARN", "A.txt", Encoding.Default, true);
            Template($@"FileCreateBlank,{destDir}\A.txt,NOWARN", "A.txt", Encoding.Default, false);
            Template($@"FileCreateBlank,{destDir}\A.txt,PRESERVE,NOWARN", "A.txt", Encoding.Default, true);
            Template($@"FileCreateBlank,{destDir}\A.txt,PRESERVE,NOWARN", "A.txt", Encoding.Default, false);
        }
        #endregion

        #region FileSize
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandFile")]
        public void FileSize()
        {
            EngineState s = EngineTests.CreateEngineState();
            string scriptDirPath = Path.Combine("%TestBench%", "CommandFile");
            string scriptSrcDir = Path.Combine(scriptDirPath, SrcDirFile);

            void Template(string rawCode, string comp, ErrorCheck check = ErrorCheck.Success)
            {
                EngineTests.Eval(s, rawCode, CodeType.FileSize, check);
                if (check == ErrorCheck.Success)
                {
                    Assert.IsTrue(s.Variables["Dest"].Equals(comp, StringComparison.Ordinal));
                }
            }

            Template($@"FileSize,{scriptSrcDir}\A.txt,%Dest%", "1");
            Template($@"FileSize,{scriptSrcDir}\B.txt,%Dest%", "2");
            Template($@"FileSize,{scriptSrcDir}\C.txt,%Dest%", "3");
            Template($@"Retrieve,FileSize,{scriptSrcDir}\C.txt,%Dest%", "3");
            Template($@"FileSize,{scriptSrcDir}\NotExist,%Dest%", string.Empty, ErrorCheck.Error);
        }
        #endregion

        #region FileVersion
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandFile")]
        public void FileVersion()
        {
            EngineState s = EngineTests.CreateEngineState();
            string scriptDirPath = Path.Combine("%TestBench%", "CommandFile");
            string scriptSrcDir = Path.Combine(scriptDirPath, SrcDirFile);

            void Template(string rawCode, string comp, ErrorCheck check = ErrorCheck.Success)
            {
                EngineTests.Eval(s, rawCode, CodeType.FileVersion, check);
                if (check == ErrorCheck.Success)
                {
                    Assert.IsTrue(s.Variables["Dest"].Equals(comp, StringComparison.Ordinal));
                }
            }

            // bt11_exe and bt20_exe BatteryLine's binary (https://github.com/ied206/BatteryLine)
            Template($@"FileVersion,{scriptSrcDir}\bt11_exe,%Dest%", "0.0.0.0");
            Template($@"FileVersion,{scriptSrcDir}\bt20_exe,%Dest%", "2.0.0.0");
            Template($@"Retrieve,FileVersion,{scriptSrcDir}\bt20_exe,%Dest%", "2.0.0.0");
        }
        #endregion

        #region DirCopy
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandFile")]
        public void DirCopy()
        {
            EngineState s = EngineTests.CreateEngineState();
            string pbDirPath = Path.Combine("%TestBench%", "CommandFile");
            string pbSrcDir = Path.Combine(pbDirPath, SrcDirDir);
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            void Template(string rawCode, string dirName, ErrorCheck check = ErrorCheck.Success, bool wbBug = false)
            {
                string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandFile"));
                string srcDir = Path.Combine(dirPath, SrcDirDir);

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

            Template($@"DirCopy,{pbSrcDir}\ABCD,{destDir}", "ABCD");
            Template($@"DirCopy,{pbSrcDir}\ABDE,{destDir}", "ABDE");
            Template($@"DirCopy,{pbSrcDir}\AB*,{destDir}", "AB*");
            Template($@"DirCopy,{pbSrcDir}\*,{destDir}", "*");

            s.CompatDirCopyBug = true;
            Template($@"DirCopy,{pbSrcDir}\*,{destDir}", "*", ErrorCheck.Success, true);
        }
        #endregion

        #region DirDelete
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandFile")]
        public void DirDelete()
        {
            EngineState s = EngineTests.CreateEngineState();
            string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            void Template(string rawCode, string dirName, ErrorCheck check = ErrorCheck.Success, bool copyDir = true)
            {
                string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandFile"));
                string srcDir = Path.Combine(dirPath, SrcDirDir);
                string srcFullPath = Path.Combine(srcDir, dirName);
                string destFullPath = Path.Combine(tempDir, dirName);

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

            Directory.CreateDirectory(tempDir);
            try
            {
                Template($@"DirDelete,{tempDir}\ABCD", "ABCD");
                Template($@"DirDelete,{tempDir}\ABDE", "ABDE");
                Template($@"DirDelete,{tempDir}", string.Empty);
                Template($@"DirDelete,{tempDir}\ACDE.txt", "ACDE.txt", ErrorCheck.Error, false);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
        #endregion

        #region DirMove
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandFile")]
        public void DirMove()
        {
            EngineState s = EngineTests.CreateEngineState();
            string varSrcDir = Path.Combine("%TestBench%", "CommandFile", SrcDirDir);
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
        public void DirMake()
        {
            EngineState s = EngineTests.CreateEngineState();
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            void Template(string rawCode, string dirName, ErrorCheck check = ErrorCheck.Success, bool createDir = false, bool createFile = false)
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

            Template($@"DirMake,{destDir}\A", "A");
            Template($@"DirMake,{destDir}\A", "A", ErrorCheck.Success, true, false);
            Template($@"DirMake,{destDir}\A", "A", ErrorCheck.Error, false, true);
        }
        #endregion

        #region DirSize
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandFile")]
        public void DirSize()
        {
            EngineState s = EngineTests.CreateEngineState();
            string scriptDirPath = Path.Combine("%TestBench%", "CommandFile");
            string scriptSrcDir = Path.Combine(scriptDirPath, SrcDirDir);

            void Template(string rawCode, string comp, ErrorCheck check = ErrorCheck.Success)
            {
                EngineTests.Eval(s, rawCode, CodeType.DirSize, check);

                if (check == ErrorCheck.Success)
                {
                    Assert.IsTrue(s.Variables["Dest"].Equals(comp, StringComparison.Ordinal));
                }
            }

            // Reuse FileSize_Template
            Template($@"DirSize,{scriptSrcDir}\ABCD,%Dest%", "9");
            Template($@"DirSize,{scriptSrcDir}\ABDE,%Dest%", "3");
            Template($@"DirSize,{scriptSrcDir},%Dest%", "13");
            Template($@"Retrieve,FolderSize,{scriptSrcDir},%Dest%", "13");
            Template($@"DirSize,{scriptSrcDir}\NotExist,%Dest%", string.Empty, ErrorCheck.Error);
        }
        #endregion

        #region PathMove
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandFile")]
        public void PathMove()
        {
            EngineState s = EngineTests.CreateEngineState();
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            void FileTemplate(string rawCode, string srcFileName, string destFileName, ErrorCheck check = ErrorCheck.Success)
            {
                string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandFile"));
                string srcDir = Path.Combine(dirPath, SrcDirFile);
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

            void DirTemplate(string rawCode, string srcFileName, string destFileName, ErrorCheck check = ErrorCheck.Success)
            {
                string dirPath = StringEscaper.Preprocess(s, Path.Combine("%TestBench%", "CommandFile"));
                string srcDir = Path.Combine(dirPath, SrcDirDir);
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

            FileTemplate($@"PathMove,{destDir}\A.txt,{destDir}\R.txt", "A.txt", "R.txt");
            DirTemplate($@"PathMove,{destDir}\ABCD,{destDir}\XYZ", "ABCD", "XYZ");
        }
        #endregion
    }
}
