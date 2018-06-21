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


using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using PEBakery.Core;
using ManagedWimLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PEBakery.Tests.Core.Command
{
    [TestClass]
    [SuppressMessage("ReSharper", "ParameterOnlyUsedForPreconditionCheck.Local")]
    [SuppressMessage("ReSharper", "LocalizableElement")]
    public class CommandWimTests
    {
        #region WimInfo
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandWim")]
        public void WimInfo()
        {
            EngineState s = EngineTests.CreateEngineState();

            string pbSrcDir = Path.Combine("%TestBench%", "CommandWim");

            void SuccessTemplate(string rawCode, string comp, ErrorCheck check = ErrorCheck.Success)
            {
                EngineTests.Eval(s, rawCode, CodeType.WimInfo, check);

                if (check == ErrorCheck.Success || check == ErrorCheck.Warning)
                {
                    string dest = s.Variables["Dest"];
                    Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
                }
            }

            void ErrorTemplate(string rawCode, ErrorCheck check = ErrorCheck.Success)
            {
                EngineTests.Eval(s, rawCode, CodeType.WimInfo, check);
            }

            // Global Information
            SuccessTemplate($@"WimInfo,{pbSrcDir}\LZX.wim,0,ImageCount,%Dest%", "1");
            SuccessTemplate($@"WimInfo,{pbSrcDir}\XPRESS.wim,0,Compression,%Dest%", "XPRESS");
            SuccessTemplate($@"WimInfo,{pbSrcDir}\LZX.wim,0,Compression,%Dest%", "LZX");
            SuccessTemplate($@"WimInfo,{pbSrcDir}\LZMS.wim,0,Compression,%Dest%", "LZMS");
            SuccessTemplate($@"WimInfo,{pbSrcDir}\BootLZX.wim,0,BootIndex,%Dest%", "1");

            // Per-Image Information
            SuccessTemplate($@"WimInfo,{pbSrcDir}\LZX.wim,1,Name,%Dest%", "Sample");
            SuccessTemplate($@"WimInfo,{pbSrcDir}\LZX.wim,1,Dummy,%Dest%", null, ErrorCheck.Error);
            ErrorTemplate($@"WimInfo,{pbSrcDir}\LZX.wim,1,Dummy,%Dest%,NOERR");
        }
        #endregion

        #region WimApply
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandWim")]
        public void WimApply()
        {
            EngineState s = EngineTests.CreateEngineState();
            string pbSampleDir = Path.Combine("%TestBench%", "CommandWim");
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string pbDestDir = StringEscaper.Escape(destDir);

            void Template(string rawCode, ErrorCheck check = ErrorCheck.Success)
            {
                try
                {
                    EngineTests.Eval(s, rawCode, CodeType.WimApply, check);
                    if (check == ErrorCheck.Success)
                    {
                        WimChecker.CheckFileSystem(SampleSet.Src01, destDir);
                    }
                }
                finally
                {
                    if (Directory.Exists(destDir))
                        Directory.Delete(destDir, true);
                }
            }
            Template($@"WimApply,{pbSampleDir}\XPRESS.wim,1,{pbDestDir}");
            Template($@"WimApply,{pbSampleDir}\LZX.wim,1,{pbDestDir}");
            Template($@"WimApply,{pbSampleDir}\LZMS.wim,1,{pbDestDir}");
            Template($@"WimApply,{pbSampleDir}\BootXPRESS.wim,1,{pbDestDir}");
            Template($@"WimApply,{pbSampleDir}\BootLZX.wim,1,{pbDestDir}");

            Template($@"WimApply,{pbSampleDir}\XPRESS.wim,1,{pbDestDir},CHECK");
            Template($@"WimApply,{pbSampleDir}\LZX.wim,1,{pbDestDir},NOACL");
            Template($@"WimApply,{pbSampleDir}\LZMS.wim,1,{pbDestDir},CHECK,NOACL");
            Template($@"WimApply,{pbSampleDir}\BootXPRESS.wim,1,{pbDestDir},NOATTRIB");
            Template($@"WimApply,{pbSampleDir}\BootLZX.wim,1,{pbDestDir},CHECK,NOATTRIB");
            Template($@"WimApply,{pbSampleDir}\BootLZX.wim,1,{pbDestDir},CHECK,NOACL,NOATTRIB");

            Template($@"WimApply,{pbSampleDir}\LZX.wim,1,{pbDestDir},CHECK,NOACL,NOATTRIB,TRASH", ErrorCheck.ParserError);
            Template($@"WimApply,{pbSampleDir}\LZX.wim,2,{pbDestDir}", ErrorCheck.Error);
        }
        #endregion

        #region WimExtract
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandWim")]
        public void WimExtract()
        {
            EngineState s = EngineTests.CreateEngineState();
            string pbSampleDir = Path.Combine("%TestBench%", "CommandWim");
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string pbDestDir = StringEscaper.Escape(destDir);

            void Template(string rawCode, string[] compFiles, ErrorCheck check = ErrorCheck.Success)
            {
                Directory.CreateDirectory(destDir);
                try
                {
                    EngineTests.Eval(s, rawCode, CodeType.WimExtract, check);
                    if (check == ErrorCheck.Success)
                    {
                        if (compFiles.Length == 0)
                        {
                            DirectoryInfo di = new DirectoryInfo(destDir);
                            Assert.IsTrue(di.GetFiles().Length == 0);
                        }
                        else
                        {
                            foreach (string f in compFiles)
                                Assert.IsTrue(File.Exists(Path.Combine(destDir, f)));
                        }
                    }
                }
                finally
                {
                    if (Directory.Exists(destDir))
                        Directory.Delete(destDir, true);
                }
            }

            Template($@"WimExtract,{pbSampleDir}\XPRESS.wim,1,\ACDE.txt,{pbDestDir}", new string[] { "ACDE.txt" });
            Template($@"WimExtract,{pbSampleDir}\LZX.wim,1,\ABCD\Z,{pbDestDir}", new string[]
            {
                Path.Combine("Z", "X.txt"),
                Path.Combine("Z", "Y.ini"),
            });
            Template($@"WimExtract,{pbSampleDir}\LZMS.wim,1,\ABCD\*.txt,{pbDestDir}", new string[]
            {
                "A.txt",
                "B.txt",
                "C.txt",
            });

            Template($@"WimExtract,{pbSampleDir}\BootXPRESS.wim,1,\ABDE\A.txt,{pbDestDir},NOATTRIB", new string[]
            {
                "A.txt",
            });
            Template($@"WimExtract,{pbSampleDir}\BootLZX.wim,1,\ABDE\A.txt,{pbDestDir},CHECK,NOATTRIB", new string[]
            {
                "A.txt",
            });
            Template($@"WimExtract,{pbSampleDir}\BootLZX.wim,1,\ABDE\A.txt,{pbDestDir},CHECK,NOACL,NOATTRIB", new string[]
            {
                "A.txt",
            });

            Template($@"WimExtract,{pbSampleDir}\Split.swm,1,\나,{pbDestDir},Split={pbSampleDir}\Split*.swm", new string[]
            { // Unicode test with Korean letter
                "나",
            });

            Template($@"WimExtract,{pbSampleDir}\LZX.wim,1,\*.exe,{pbDestDir}", new string[0]);

            Template($@"WimExtract,{pbSampleDir}\LZX.wim,1,\ACDE.txt,{pbDestDir},CHECK,NOACL,NOATTRIB,TRASH", null, ErrorCheck.ParserError);
            Template($@"WimExtract,{pbSampleDir}\LZX.wim,2,\ACDE.txt,{pbDestDir}", null, ErrorCheck.Error);
            Template($@"WimExtract,{pbSampleDir}\LZX.wim,1,\Z.txt,{pbDestDir}", null, ErrorCheck.Error);
        }
        #endregion

        #region WimExtractBulk
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandWim")]
        public void WimExtractBulk()
        {
            EngineState s = EngineTests.CreateEngineState();
            string pbSampleDir = Path.Combine("%TestBench%", "CommandWim");
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string pbDestDir = StringEscaper.Escape(destDir);

            void Template(string rawCode, string[] compFiles, ErrorCheck check = ErrorCheck.Success)
            {
                Directory.CreateDirectory(destDir);
                string listFile = Path.Combine(destDir, "ListFile.txt");
                try
                {
                    if (compFiles != null)
                    {
                        using (StreamWriter w = new StreamWriter(listFile, false, Encoding.Unicode))
                        {
                            foreach (string f in compFiles)
                                w.WriteLine(@"\" + f);
                        }
                    }

                    EngineTests.Eval(s, rawCode, CodeType.WimExtractBulk, check);
                    if (check == ErrorCheck.Success)
                    {
                        Debug.Assert(compFiles != null);

                        if (compFiles.Length == 0)
                        {
                            DirectoryInfo di = new DirectoryInfo(destDir);
                            Assert.IsTrue(di.GetFiles().Length == 1); // 1 for listfile
                        }
                        else
                        {
                            foreach (string f in compFiles)
                                Assert.IsTrue(File.Exists(Path.Combine(destDir, f)));
                        }
                    }
                }
                finally
                {
                    if (Directory.Exists(destDir))
                        Directory.Delete(destDir, true);
                    if (File.Exists(listFile))
                        File.Delete(listFile);
                }
            }

            Template($@"WimExtractBulk,{pbSampleDir}\XPRESS.wim,1,{pbDestDir}\ListFile.txt,{pbDestDir}",
                new string[]
                {
                    "ACDE.txt"
                });
            Template($@"WimExtractBulk,{pbSampleDir}\LZX.wim,1,{pbDestDir}\ListFile.txt,{pbDestDir}",
                new string[]
                {
                    Path.Combine("ABCD", "Z", "X.txt"),
                    Path.Combine("ABCD", "Z", "Y.ini"),
                });
            Template($@"WimExtractBulk,{pbSampleDir}\LZMS.wim,1,{pbDestDir}\ListFile.txt,{pbDestDir}",
                new string[]
                {
                    Path.Combine("ABCD", "A.txt"),
                    Path.Combine("ABCD", "B.txt"),
                    Path.Combine("ABCD", "C.txt"),
                });

            Template($@"WimExtractBulk,{pbSampleDir}\BootXPRESS.wim,1,{pbDestDir}\ListFile.txt,{pbDestDir},NOATTRIB",
                new string[]
                {
                    Path.Combine("ABDE", "A.txt"),
                });
            Template($@"WimExtractBulk,{pbSampleDir}\BootLZX.wim,1,{pbDestDir}\ListFile.txt,{pbDestDir},CHECK,NOATTRIB",
                new string[]
                {
                    Path.Combine("ABDE", "A.txt"),
                });
            Template($@"WimExtractBulk,{pbSampleDir}\BootLZX.wim,1,{pbDestDir}\ListFile.txt,{pbDestDir},CHECK,NOACL,NOATTRIB",
                new string[]
                {
                    Path.Combine("ABDE", "A.txt"),
                });

            Template($@"WimExtractBulk,{pbSampleDir}\Split.swm,1,{pbDestDir}\ListFile.txt,{pbDestDir},Split={pbSampleDir}\Split*.swm",
                new string[]
                {
                    // Unicode test with Korean letter
                    "나",
                });

            Template($@"WimExtractBulk,{pbSampleDir}\LZX.wim,1,{pbDestDir}\ListFile.txt,{pbDestDir},CHECK,NOACL,NOATTRIB,TRASH",
                null, ErrorCheck.ParserError);
            Template($@"WimExtractBulk,{pbSampleDir}\LZX.wim,2,{pbDestDir}\ListFile.txt,{pbDestDir}",
                null, ErrorCheck.Error);
            Template($@"WimExtractBulk,{pbSampleDir}\Split.swm,1,{pbDestDir}\ListFile.txt,{pbDestDir},Split={pbSampleDir}\Split*.swm",
                new string[]
                {
                    // Unicode test with Korean letter
                    "나", "다"
                }, ErrorCheck.Error);

            Template($@"WimExtractBulk,{pbSampleDir}\LZX.wim,1,{pbDestDir}\ListFile.txt,{pbDestDir}",
                new string[]
                {
                    Path.Combine("Z.txt"),
                }, ErrorCheck.Error);

            Template($@"WimExtractBulk,{pbSampleDir}\LZX.wim,1,{pbDestDir}\ListFile.txt,{pbDestDir},NOERR",
                new string[]
                {
                    Path.Combine("Z.txt"),
                }, ErrorCheck.Warning);
        }

        #endregion

        #region WimCapture
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandWim")]
        public void WimCapture()
        {
            EngineState s = EngineTests.CreateEngineState();
            string pbSampleDir = Path.Combine("%TestBench%", "CommandWim");
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string pbDestDir = StringEscaper.Escape(destDir);

            void Template(string rawCode, string wimFileName, ErrorCheck check = ErrorCheck.Success)
            {
                string applyDir = Path.Combine(destDir, "CaptureApply");
                string wimFile = Path.Combine(destDir, wimFileName);

                Directory.CreateDirectory(applyDir);
                try
                {
                    EngineTests.Eval(s, rawCode, CodeType.WimCapture, check);
                    if (check == ErrorCheck.Success)
                    {
                        Assert.IsTrue(File.Exists(wimFile));

                        // Try applying
                        using (Wim wim = Wim.OpenWim(wimFile, OpenFlags.DEFAULT))
                        {
                            wim.ExtractImage(1, applyDir, ExtractFlags.DEFAULT);
                        }

                        WimChecker.CheckFileSystem(SampleSet.Src01, applyDir);
                    }
                }
                finally
                {
                    if (Directory.Exists(applyDir))
                        Directory.Delete(applyDir, true);
                    if (File.Exists(wimFile))
                        File.Delete(wimFile);
                }
            }

            Directory.CreateDirectory(destDir);
            try
            {
                Template($@"WimCapture,{pbSampleDir}\Src01,{pbDestDir}\XPRESS.wim,XPRESS", "XPRESS.wim");
                Template($@"WimCapture,{pbSampleDir}\Src01,{pbDestDir}\LZX.wim,LZX", "LZX.wim");
                Template($@"WimCapture,{pbSampleDir}\Src01,{pbDestDir}\LZMS.wim,LZMS", "LZMS.wim");

                Template($@"WimCapture,{pbSampleDir}\Src01,{pbDestDir}\XPRESS.wim,XPRESS,BOOT", "XPRESS.wim");
                Template($@"WimCapture,{pbSampleDir}\Src01,{pbDestDir}\LZX.wim,LZX,NOACL", "LZX.wim");
                Template($@"WimCapture,{pbSampleDir}\Src01,{pbDestDir}\LZMS.wim,LZMS,CHECK", "LZMS.wim");

                Template($@"WimCapture,{pbSampleDir}\Src01,{pbDestDir}\XPRESS.wim,XPRESS,ImageName=NormalCompress", "XPRESS.wim");
                Template($@"WimCapture,{pbSampleDir}\Src01,{pbDestDir}\LZX.wim,LZX,ImageDesc=MaxCompress", "LZX.wim");
                Template($@"WimCapture,{pbSampleDir}\Src01,{pbDestDir}\LZMS.wim,LZMS,Flags=PEBakeryWimFlag", "LZMS.wim");

                Template($@"WimCapture,{pbSampleDir}\Src01,{pbDestDir}\XPRESS.wim,WRONGCOMP", "XPRESS.wim", ErrorCheck.Error);
                Template($@"WimCapture,{pbSampleDir}\Src01,{pbDestDir}\LZX.wim,LZX,TRASH", "LZX.wim", ErrorCheck.ParserError);
            }
            finally
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
            }
        }
        #endregion

        #region WimAppend
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandWim")]
        public void WimAppend()
        {
            EngineState s = EngineTests.CreateEngineState();
            string pbSampleDir = Path.Combine("%TestBench%", "CommandWim");
            string sampleDir = StringEscaper.Preprocess(s, pbSampleDir);
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string pbDestDir = StringEscaper.Escape(destDir);

            void Template(string rawCode, string srcDir, string wimFileName, SampleSet set, ErrorCheck check = ErrorCheck.Success)
            {
                string applyDir = Path.Combine(destDir, "AppendApply");
                string srcWim = Path.Combine(srcDir, wimFileName);
                string destWim = Path.Combine(destDir, wimFileName);

                Directory.CreateDirectory(applyDir);
                try
                {
                    File.Copy(srcWim, destWim, true);

                    uint srcImageCount;
                    using (Wim wim = Wim.OpenWim(destWim, OpenFlags.DEFAULT))
                    {
                        WimInfo wi = wim.GetWimInfo();
                        srcImageCount = wi.ImageCount;
                    }

                    EngineTests.Eval(s, rawCode, CodeType.WimAppend, check);
                    if (check == ErrorCheck.Success)
                    {
                        using (Wim wim = Wim.OpenWim(destWim, OpenFlags.DEFAULT))
                        {
                            WimInfo wi = wim.GetWimInfo();
                            Assert.IsTrue(wi.ImageCount == srcImageCount + 1);

                            wim.ExtractImage((int)(srcImageCount + 1), applyDir, ExtractFlags.DEFAULT);
                        }

                        WimChecker.CheckFileSystem(set, applyDir);
                    }
                }
                finally
                {
                    if (Directory.Exists(applyDir))
                        Directory.Delete(applyDir, true);
                    if (File.Exists(destWim))
                        File.Delete(destWim);
                }
            }

            Directory.CreateDirectory(destDir);
            try
            {
                Template($@"WimAppend,{pbSampleDir}\Src03,{pbDestDir}\XPRESS.wim", sampleDir, "XPRESS.wim", SampleSet.Src03);
                Template($@"WimAppend,{pbSampleDir}\Src03,{pbDestDir}\LZX.wim", sampleDir, "LZX.wim", SampleSet.Src03);
                Template($@"WimAppend,{pbSampleDir}\Src03,{pbDestDir}\LZMS.wim", sampleDir, "LZMS.wim", SampleSet.Src03);

                Template($@"WimAppend,{pbSampleDir}\Src03,{pbDestDir}\XPRESS.wim,BOOT", sampleDir, "XPRESS.wim", SampleSet.Src03);
                Template($@"WimAppend,{pbSampleDir}\Src03,{pbDestDir}\LZX.wim,NOACL", sampleDir, "LZX.wim", SampleSet.Src03);
                Template($@"WimAppend,{pbSampleDir}\Src03,{pbDestDir}\LZMS.wim,CHECK", sampleDir, "LZMS.wim", SampleSet.Src03);

                Template($@"WimAppend,{pbSampleDir}\Src02_2,{pbDestDir}\MultiImage.wim,DeltaIndex=1", sampleDir, "MultiImage.wim", SampleSet.Src02_2);

                Template($@"WimAppend,{pbSampleDir}\Src01,{pbDestDir}\LZX.wim,TRASH", sampleDir, "LZX.wim", SampleSet.Src01, ErrorCheck.ParserError);
                Template($@"WimAppend,{pbSampleDir}\Src01,{pbDestDir}\LZX.wim,LZX,TRASH", sampleDir, "LZX.wim", SampleSet.Src01, ErrorCheck.ParserError);
            }
            finally
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
            }
        }
        #endregion

        #region WimDelete
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandWim")]
        public void WimDelete()
        {
            EngineState s = EngineTests.CreateEngineState();

            string pbSampleDir = Path.Combine("%TestBench%", "CommandWim");
            string sampleDir = StringEscaper.Preprocess(s, pbSampleDir);
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string pbDestDir = StringEscaper.Escape(destDir);

            void Template(string rawCode, string wimFileName, ErrorCheck check = ErrorCheck.Success)
            {
                Directory.CreateDirectory(destDir);
                try
                {
                    string srcWim = Path.Combine(sampleDir, wimFileName);
                    string destWim = Path.Combine(destDir, wimFileName);
                    File.Copy(srcWim, destWim, true);

                    uint srcImageCount;
                    using (Wim wim = Wim.OpenWim(destWim, OpenFlags.DEFAULT))
                    {
                        WimInfo wi = wim.GetWimInfo();
                        srcImageCount = wi.ImageCount;
                    }

                    EngineTests.Eval(s, rawCode, CodeType.WimDelete, check);
                    if (check == ErrorCheck.Success)
                    {
                        using (Wim wim = Wim.OpenWim(destWim, OpenFlags.DEFAULT))
                        {
                            WimInfo wi = wim.GetWimInfo();
                            Assert.IsTrue(wi.ImageCount == srcImageCount - 1);
                        }
                    }
                }
                finally
                {
                    if (Directory.Exists(destDir))
                        Directory.Delete(destDir, true);
                }
            }

            Template($@"WimDelete,{pbDestDir}\MultiImage.wim,1", "MultiImage.wim");
            Template($@"WimDelete,{pbDestDir}\MultiImage.wim,3,CHECK", "MultiImage.wim");

            Template($@"WimDelete,{pbDestDir}\MultiImage.wim,3,TRASH", "MultiImage.wim", ErrorCheck.ParserError);
            Template($@"WimDelete,{pbDestDir}\MultiImage.wim,4", "MultiImage.wim", ErrorCheck.Error);
        }
        #endregion

        #region WimPathAdd
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandWim")]
        public void Wim_WimPathAdd()
        {
            EngineState s = EngineTests.CreateEngineState();

            string pbSampleDir = Path.Combine("%TestBench%", "CommandWim");
            string sampleDir = StringEscaper.Preprocess(s, pbSampleDir);
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string pbDestDir = StringEscaper.Escape(destDir);

            void Template(string rawCode, string wimFileName, string comp, ErrorCheck check = ErrorCheck.Success)
            {
                string srcWim = Path.Combine(sampleDir, wimFileName);
                string destWim = Path.Combine(destDir, wimFileName);

                Directory.CreateDirectory(destDir);
                try
                {
                    File.Copy(srcWim, destWim, true);

                    EngineTests.Eval(s, rawCode, CodeType.WimPathAdd, check);
                    if (check == ErrorCheck.Success)
                    {
                        using (Wim wim = Wim.OpenWim(destWim, OpenFlags.DEFAULT))
                        {
                            bool found = false;
                            CallbackStatus ExistCallback(DirEntry dentry, object userData)
                            {
                                found = true;
                                return CallbackStatus.CONTINUE;
                            }

                            wim.IterateDirTree(1, comp, IterateFlags.DEFAULT, ExistCallback, null);
                            Assert.IsTrue(found);
                        }
                    }
                }
                finally
                {
                    if (Directory.Exists(destDir))
                        Directory.Delete(destDir, true);
                }
            }

            Template($@"WimPathAdd,{pbDestDir}\LZX.wim,1,{pbSampleDir}\Src03\가,\다", "LZX.wim", @"\다");
            Template($@"WimPathAdd,{pbDestDir}\LZX.wim,1,{pbSampleDir}\Src03,\,CHECK", "LZX.wim", @"\");
            Template($@"WimPathAdd,{pbDestDir}\LZX.wim,1,{pbSampleDir}\Src03\나,\ACDE.txt,PRESERVE", "LZX.wim", null, ErrorCheck.Error);

            Template($@"WimPathAdd,{pbDestDir}\LZX.wim,1,{pbSampleDir}\Src03\나,\ACDE.txt,TRASH", "LZX.wim", null, ErrorCheck.ParserError);
            Template($@"WimPathAdd,{pbDestDir}\LZX.wim,2,{pbSampleDir}\Src03\나,\ACDE.txt", "LZX.wim", null, ErrorCheck.Error);
        }
        #endregion

        #region WimPathDelete
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandWim")]
        public void WimPathDelete()
        {
            EngineState s = EngineTests.CreateEngineState();
            string pbSampleDir = Path.Combine("%TestBench%", "CommandWim");
            string sampleDir = StringEscaper.Preprocess(s, pbSampleDir);
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string pbDestDir = StringEscaper.Escape(destDir);

            void Template(string rawCode, string wimFileName, string comp, ErrorCheck check = ErrorCheck.Success)
            {
                string srcWim = Path.Combine(sampleDir, wimFileName);
                string destWim = Path.Combine(destDir, wimFileName);

                Directory.CreateDirectory(destDir);
                try
                {
                    File.Copy(srcWim, destWim, true);

                    EngineTests.Eval(s, rawCode, CodeType.WimPathDelete, check);
                    if (check == ErrorCheck.Success)
                    {
                        using (Wim wim = Wim.OpenWim(destWim, OpenFlags.DEFAULT))
                        {
                            bool deleted = false;
                            CallbackStatus DeletedCallback(DirEntry dentry, object userData) { return CallbackStatus.CONTINUE; }
                            try { wim.IterateDirTree(1, comp, IterateFlags.DEFAULT, DeletedCallback, null); }
                            catch (WimLibException e) when (e.ErrorCode == ErrorCode.PATH_DOES_NOT_EXIST) { deleted = true; }

                            Assert.IsTrue(deleted);
                        }
                    }
                }
                finally
                {
                    if (Directory.Exists(destDir))
                        Directory.Delete(destDir, true);
                }
            }

            Template($@"WimPathDelete,{pbDestDir}\LZX.wim,1,\ACDE.txt", "LZX.wim", @"\ACDE.txt");
            Template($@"WimPathDelete,{pbDestDir}\LZX.wim,1,\ABCD,CHECK", "LZX.wim", @"\ABCD");
            Template($@"WimPathDelete,{pbDestDir}\LZX.wim,1,\ABDE,REBUILD", "LZX.wim", @"\ABDE");

            Template($@"WimPathDelete,{pbDestDir}\LZX.wim,1,\ACDE.txt,TRASH", "LZX.wim", null, ErrorCheck.ParserError);
            Template($@"WimPathDelete,{pbDestDir}\LZX.wim,2,\ACDE.txt", "LZX.wim", null, ErrorCheck.Error);
            Template($@"WimPathDelete,{pbDestDir}\LZX.wim,1,\NONEXIST", "LZX.wim", null, ErrorCheck.Error);
        }
        #endregion

        #region WimPathRename
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandWim")]
        public void WimPathRename()
        {
            EngineState s = EngineTests.CreateEngineState();
            string pbSampleDir = Path.Combine("%TestBench%", "CommandWim");
            string sampleDir = StringEscaper.Preprocess(s, pbSampleDir);
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string pbDestDir = StringEscaper.Escape(destDir);

            void Template(string rawCode, string wimFileName, string originalName, string newName, ErrorCheck check = ErrorCheck.Success)
            {
                string srcWim = Path.Combine(sampleDir, wimFileName);
                string destWim = Path.Combine(destDir, wimFileName);

                Directory.CreateDirectory(destDir);
                try
                {
                    File.Copy(srcWim, destWim, true);

                    EngineTests.Eval(s, rawCode, CodeType.WimPathRename, check);
                    if (check == ErrorCheck.Success)
                    {
                        using (Wim wim = Wim.OpenWim(destWim, OpenFlags.DEFAULT))
                        {
                            bool found = false;
                            CallbackStatus ExistCallback(DirEntry dentry, object userData)
                            {
                                found = true;
                                return CallbackStatus.CONTINUE;
                            }
                            wim.IterateDirTree(1, newName, IterateFlags.DEFAULT, ExistCallback, null);
                            Assert.IsTrue(found);

                            bool deleted = false;
                            CallbackStatus DeleteCallback(DirEntry dentry, object userData) { return CallbackStatus.CONTINUE; }
                            try { wim.IterateDirTree(1, originalName, IterateFlags.DEFAULT, DeleteCallback, null); }
                            catch (WimLibException e) when (e.ErrorCode == ErrorCode.PATH_DOES_NOT_EXIST) { deleted = true; }
                            Assert.IsTrue(deleted);
                        }
                    }
                }
                finally
                {
                    if (Directory.Exists(destDir))
                        Directory.Delete(destDir, true);
                }
            }

            Template($@"WimPathRename,{pbDestDir}\LZX.wim,1,\ACDE.txt,\평창,CHECK", "LZX.wim", @"\ACDE.txt", @"\평창");
            Template($@"WimPathRename,{pbDestDir}\LZX.wim,1,\ABDE,\RENAME,REBUILD", "LZX.wim", @"\ABDE", @"\RENAME");
            Template($@"WimPathRename,{pbDestDir}\LZX.wim,1,\ABCD,\Z", "LZX.wim", @"\ABCD", @"\Z");

            Template($@"WimPathRename,{pbDestDir}\LZX.wim,1,\ERROR,\DUMMY,TRASH", "LZX.wim", null, null, ErrorCheck.ParserError);
            Template($@"WimPathRename,{pbDestDir}\LZX.wim,2,\없음,\DUMMY", "LZX.wim", null, null, ErrorCheck.Error);
        }
        #endregion

        #region WimOptimize
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandWim")]
        public void WimOptimize()
        {
            EngineState s = EngineTests.CreateEngineState();
            string pbSampleDir = Path.Combine("%TestBench%", "CommandWim");
            string sampleDir = StringEscaper.Preprocess(s, pbSampleDir);
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string pbDestDir = StringEscaper.Escape(destDir);

            void Template(string rawCode, string wimFileName, ErrorCheck check = ErrorCheck.Success)
            {
                string srcWim = Path.Combine(sampleDir, wimFileName);
                string destWim = Path.Combine(destDir, wimFileName);

                try
                {
                    File.Copy(srcWim, destWim, true);
                    long oldSize = new FileInfo(destWim).Length;

                    EngineTests.Eval(s, rawCode, CodeType.WimOptimize, check);
                    if (check == ErrorCheck.Success)
                    {
                        long newSize = new FileInfo(destWim).Length;
                        Console.WriteLine($"Before : {oldSize}");
                        Console.WriteLine($"After  : {newSize}");
                        Assert.IsTrue(newSize < oldSize);
                    }
                }
                finally
                {
                    if (File.Exists(destWim))
                        File.Delete(destWim);
                }
            }
            Directory.CreateDirectory(destDir);
            try
            {
                Template($@"WimOptimize,{pbDestDir}\ToOptimize.wim", "ToOptimize.wim");
                Template($@"WimOptimize,{pbDestDir}\ToOptimize.wim,CHECK", "ToOptimize.wim");
                Template($@"WimOptimize,{pbDestDir}\ToOptimize.wim,Recomp=LZMS", "ToOptimize.wim");
            }
            finally
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
            }
        }
        #endregion

        #region WimExport
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandWim")]
        public void WimExport()
        {
            EngineState s = EngineTests.CreateEngineState();
            string pbSampleDir = Path.Combine("%TestBench%", "CommandWim");
            string sampleDir = StringEscaper.Preprocess(s, pbSampleDir);
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string pbDestDir = StringEscaper.Escape(destDir);

            void ExportNewTemplate(string rawCode, string srcWimFileName, string destWimFileName, ErrorCheck check = ErrorCheck.Success)
            {
                string srcWim = Path.Combine(sampleDir, srcWimFileName);
                string destWim = Path.Combine(destDir, destWimFileName);

                try
                {
                    long oldSize = new FileInfo(srcWim).Length;

                    EngineTests.Eval(s, rawCode, CodeType.WimExport, check);
                    if (check == ErrorCheck.Success)
                    {
                        long newSize = new FileInfo(destWim).Length;
                        Console.WriteLine($"Before : {oldSize}");
                        Console.WriteLine($"After  : {newSize}");
                        Assert.IsTrue(newSize < oldSize);
                    }
                }
                finally
                {
                    if (File.Exists(destWim))
                        File.Delete(destWim);
                }
            }

            void ExportExistTemplate(string rawCode, string srcWimFileName, string destWimFileName, ErrorCheck check = ErrorCheck.Success)
            {
                string srcWim = Path.Combine(sampleDir, srcWimFileName);
                string destWim = Path.Combine(destDir, destWimFileName);

                try
                {
                    File.Copy(srcWim, destWim, true);

                    EngineTests.Eval(s, rawCode, CodeType.WimExport, check);
                    if (check == ErrorCheck.Success)
                    {
                        using (Wim wim = Wim.OpenWim(destWim, OpenFlags.DEFAULT))
                        {
                            WimInfo wi = wim.GetWimInfo();

                            Assert.IsTrue(wi.ImageCount == 2);
                        }
                    }
                }
                finally
                {
                    if (File.Exists(destWim))
                        File.Delete(destWim);
                }
            }

            Directory.CreateDirectory(destDir);
            try
            {
                ExportNewTemplate($@"WimExport,{pbSampleDir}\XPRESS.wim,1,{pbDestDir}\LZMS.wim,Recomp=LZMS,NOCHECK", "XPRESS.wim", "LZMS.wim");
                ExportExistTemplate($@"WimExport,{pbSampleDir}\XPRESS.wim,1,{pbDestDir}\LZMS.wim,ImageName=Solid,CHECK", "XPRESS.wim", "LZMS.wim");

                ExportNewTemplate($@"WimExport,{pbSampleDir}\XPRESS.wim,1,{pbDestDir}\LZMS.wim,Recomp=KEEP,CHECK", "XPRESS.wim", "LZMS.wim", ErrorCheck.Error);
                ExportExistTemplate($@"WimExport,{pbSampleDir}\XPRESS.wim,1,{pbDestDir}\LZMS.wim,ImageName=Solid,Recomp=LZMS,CHECK", "XPRESS.wim", "LZMS.wim", ErrorCheck.Error);
            }
            finally
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
            }
        }
        #endregion

        #region Helper
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public enum SampleSet
        {
            // TestSet Src01 is created for basic test and compresstion type test
            Src01,
            // TestSet Src02 is created for multi image and delta image test 
            Src02,
            Src02_1,
            Src02_2,
            Src02_3,
            // TestSet Src03 is created for split wim test and unicode test
            Src03,
        }

        internal class WimChecker
        {
            public static void CheckWimPath(SampleSet set, string wimFile)
            {
                switch (set)
                {
                    case SampleSet.Src01:
                        using (Wim wim = Wim.OpenWim(wimFile, OpenFlags.DEFAULT))
                        {
                            Assert.IsTrue(wim.DirExists(1, Path.Combine(@"\", "ABCD")));
                            Assert.IsTrue(wim.DirExists(1, Path.Combine(@"\", "ABCD", "Z")));
                            Assert.IsTrue(wim.DirExists(1, Path.Combine(@"\", "ABDE")));
                            Assert.IsTrue(wim.DirExists(1, Path.Combine(@"\", "ABDE", "Z")));

                            Assert.IsTrue(wim.FileExists(1, Path.Combine(@"\", "ACDE.txt")));

                            Assert.IsTrue(wim.FileExists(1, Path.Combine(@"\", "ABCD", "A.txt")));
                            Assert.IsTrue(wim.FileExists(1, Path.Combine(@"\", "ABCD", "B.txt")));
                            Assert.IsTrue(wim.FileExists(1, Path.Combine(@"\", "ABCD", "C.txt")));
                            Assert.IsTrue(wim.FileExists(1, Path.Combine(@"\", "ABCD", "D.ini")));

                            Assert.IsTrue(wim.FileExists(1, Path.Combine(@"\", "ABCD", "Z", "X.txt")));
                            Assert.IsTrue(wim.FileExists(1, Path.Combine(@"\", "ABCD", "Z", "Y.ini")));

                            Assert.IsTrue(wim.FileExists(1, Path.Combine(@"\", "ABDE", "A.txt")));

                            Assert.IsTrue(wim.FileExists(1, Path.Combine(@"\", "ABDE", "Z", "X.txt")));
                            Assert.IsTrue(wim.FileExists(1, Path.Combine(@"\", "ABDE", "Z", "Y.ini")));
                        }
                        break;
                    case SampleSet.Src02:
                        using (Wim wim = Wim.OpenWim(wimFile, OpenFlags.DEFAULT))
                        {
                            Assert.IsTrue(wim.DirExists(1, Path.Combine(@"\", "B")));
                            Assert.IsTrue(wim.FileExists(1, Path.Combine(@"\", "A.txt")));
                            Assert.IsTrue(wim.FileExists(1, Path.Combine(@"\", "B", "C.txt")));
                            Assert.IsTrue(wim.FileExists(1, Path.Combine(@"\", "B", "D.ini")));

                            Assert.IsTrue(wim.DirExists(2, Path.Combine(@"\", "B")));
                            Assert.IsTrue(wim.FileExists(2, Path.Combine(@"\", "Z.txt")));
                            Assert.IsTrue(wim.FileExists(2, Path.Combine(@"\", "B", "C.txt")));
                            Assert.IsTrue(wim.FileExists(2, Path.Combine(@"\", "B", "D.ini")));

                            Assert.IsTrue(wim.DirExists(3, Path.Combine(@"\", "B")));
                            Assert.IsTrue(wim.FileExists(3, Path.Combine(@"\", "Y.txt")));
                            Assert.IsTrue(wim.FileExists(3, Path.Combine(@"\", "Z.txt")));
                            Assert.IsTrue(wim.FileExists(3, Path.Combine(@"\", "B", "C.txt")));
                            Assert.IsTrue(wim.FileExists(3, Path.Combine(@"\", "B", "D.ini")));
                        }
                        break;
                    case SampleSet.Src03:
                        break;
                    default:
                        throw new InvalidOperationException("Invalid SampleSet");
                }

            }

            public static void CheckFileSystem(SampleSet set, string dir)
            {
                switch (set)
                {
                    case SampleSet.Src01:
                        Assert.IsTrue(Directory.Exists(Path.Combine(dir, "ABCD")));
                        Assert.IsTrue(Directory.Exists(Path.Combine(dir, "ABCD", "Z")));
                        Assert.IsTrue(Directory.Exists(Path.Combine(dir, "ABDE")));
                        Assert.IsTrue(Directory.Exists(Path.Combine(dir, "ABDE", "Z")));

                        Assert.IsTrue(File.Exists(Path.Combine(dir, "ACDE.txt")));
                        Assert.IsTrue(new FileInfo(Path.Combine(dir, "ACDE.txt")).Length == 1);

                        Assert.IsTrue(File.Exists(Path.Combine(dir, "ABCD", "A.txt")));
                        Assert.IsTrue(File.Exists(Path.Combine(dir, "ABCD", "B.txt")));
                        Assert.IsTrue(File.Exists(Path.Combine(dir, "ABCD", "C.txt")));
                        Assert.IsTrue(File.Exists(Path.Combine(dir, "ABCD", "D.ini")));
                        Assert.IsTrue(new FileInfo(Path.Combine(dir, "ABCD", "A.txt")).Length == 1);
                        Assert.IsTrue(new FileInfo(Path.Combine(dir, "ABCD", "B.txt")).Length == 2);
                        Assert.IsTrue(new FileInfo(Path.Combine(dir, "ABCD", "C.txt")).Length == 3);
                        Assert.IsTrue(new FileInfo(Path.Combine(dir, "ABCD", "D.ini")).Length == 1);

                        Assert.IsTrue(File.Exists(Path.Combine(dir, "ABCD", "Z", "X.txt")));
                        Assert.IsTrue(File.Exists(Path.Combine(dir, "ABCD", "Z", "Y.ini")));
                        Assert.IsTrue(new FileInfo(Path.Combine(dir, "ABCD", "Z", "X.txt")).Length == 1);
                        Assert.IsTrue(new FileInfo(Path.Combine(dir, "ABCD", "Z", "Y.ini")).Length == 1);

                        Assert.IsTrue(File.Exists(Path.Combine(dir, "ABDE", "A.txt")));
                        Assert.IsTrue(new FileInfo(Path.Combine(dir, "ABDE", "A.txt")).Length == 1);

                        Assert.IsTrue(File.Exists(Path.Combine(dir, "ABDE", "Z", "X.txt")));
                        Assert.IsTrue(File.Exists(Path.Combine(dir, "ABDE", "Z", "Y.ini")));
                        Assert.IsTrue(new FileInfo(Path.Combine(dir, "ABDE", "Z", "X.txt")).Length == 1);
                        Assert.IsTrue(new FileInfo(Path.Combine(dir, "ABDE", "Z", "Y.ini")).Length == 1);
                        break;
                    case SampleSet.Src02_1:
                        Assert.IsTrue(Directory.Exists(Path.Combine(dir, "B")));

                        Assert.IsTrue(File.Exists(Path.Combine(dir, "A.txt")));
                        Assert.IsTrue(new FileInfo(Path.Combine(dir, "A.txt")).Length == 1);

                        Assert.IsTrue(File.Exists(Path.Combine(dir, "B", "C.txt")));
                        Assert.IsTrue(File.Exists(Path.Combine(dir, "B", "D.ini")));
                        Assert.IsTrue(new FileInfo(Path.Combine(dir, "B", "C.txt")).Length == 1);
                        Assert.IsTrue(new FileInfo(Path.Combine(dir, "B", "D.ini")).Length == 1);
                        break;
                    case SampleSet.Src02_2:
                        Assert.IsTrue(Directory.Exists(Path.Combine(dir, "B")));

                        Assert.IsTrue(File.Exists(Path.Combine(dir, "Z.txt")));
                        Assert.IsTrue(new FileInfo(Path.Combine(dir, "Z.txt")).Length == 1);

                        Assert.IsTrue(File.Exists(Path.Combine(dir, "B", "C.txt")));
                        Assert.IsTrue(File.Exists(Path.Combine(dir, "B", "D.ini")));
                        Assert.IsTrue(new FileInfo(Path.Combine(dir, "B", "C.txt")).Length == 1);
                        Assert.IsTrue(new FileInfo(Path.Combine(dir, "B", "D.ini")).Length == 1);
                        break;
                    case SampleSet.Src02_3:
                        Assert.IsTrue(Directory.Exists(Path.Combine(dir, "B")));

                        Assert.IsTrue(File.Exists(Path.Combine(dir, "Y.txt")));
                        Assert.IsTrue(File.Exists(Path.Combine(dir, "Z.txt")));
                        Assert.IsTrue(new FileInfo(Path.Combine(dir, "Y.txt")).Length == 1);
                        Assert.IsTrue(new FileInfo(Path.Combine(dir, "Z.txt")).Length == 1);

                        Assert.IsTrue(File.Exists(Path.Combine(dir, "B", "C.txt")));
                        Assert.IsTrue(File.Exists(Path.Combine(dir, "B", "D.ini")));
                        Assert.IsTrue(new FileInfo(Path.Combine(dir, "B", "C.txt")).Length == 1);
                        Assert.IsTrue(new FileInfo(Path.Combine(dir, "B", "D.ini")).Length == 1);
                        break;
                    case SampleSet.Src03:
                        Assert.IsTrue(File.Exists(Path.Combine(dir, "가")));
                        Assert.IsTrue(File.Exists(Path.Combine(dir, "나")));
                        break;
                    default:
                        throw new InvalidOperationException("Invalid SampleSet");
                }
            }

            public static void CheckPathList(SampleSet set, List<Tuple<string, bool>> paths)
            {
                Tuple<string, bool>[] checkList;
                switch (set)
                {
                    case SampleSet.Src01:
                        checkList = new[]
                        {
                        new Tuple<string, bool>(Path.Combine(@"\ABCD"), true),
                        new Tuple<string, bool>(Path.Combine(@"\ABCD", "Z"), true),
                        new Tuple<string, bool>(Path.Combine(@"\ABDE"), true),
                        new Tuple<string, bool>(Path.Combine(@"\ABDE", "Z"), true),

                        new Tuple<string, bool>(Path.Combine(@"\ACDE.txt"), false),

                        new Tuple<string, bool>(Path.Combine(@"\ABCD", "A.txt"), false),
                        new Tuple<string, bool>(Path.Combine(@"\ABCD", "B.txt"), false),
                        new Tuple<string, bool>(Path.Combine(@"\ABCD", "C.txt"), false),
                        new Tuple<string, bool>(Path.Combine(@"\ABCD", "D.ini"), false),

                        new Tuple<string, bool>(Path.Combine(@"\ABCD", "Z", "X.txt"), false),
                        new Tuple<string, bool>(Path.Combine(@"\ABCD", "Z", "Y.ini"), false),

                        new Tuple<string, bool>(Path.Combine(@"\ABDE", "A.txt"), false),

                        new Tuple<string, bool>(Path.Combine(@"\ABDE", "Z", "X.txt"), false),
                        new Tuple<string, bool>(Path.Combine(@"\ABDE", "Z", "Y.ini"), false),
                    };
                        break;
                    case SampleSet.Src02_1:
                        checkList = new[]
                        {
                        new Tuple<string, bool>(Path.Combine(@"B"), true),
                        new Tuple<string, bool>(Path.Combine(@"A.txt"), false),
                        new Tuple<string, bool>(Path.Combine(@"B", "C.txt"), false),
                        new Tuple<string, bool>(Path.Combine(@"B", "D.ini"), false),
                    };
                        break;
                    case SampleSet.Src02_2:
                        checkList = new[]
                        {
                        new Tuple<string, bool>(Path.Combine(@"B"), true),
                        new Tuple<string, bool>(Path.Combine(@"Z.txt"), false),
                        new Tuple<string, bool>(Path.Combine(@"B", "C.txt"), false),
                        new Tuple<string, bool>(Path.Combine(@"B", "D.ini"), false),
                    };
                        break;
                    case SampleSet.Src02_3:
                        checkList = new[]
                        {
                        new Tuple<string, bool>(Path.Combine(@"B"), true),
                        new Tuple<string, bool>(Path.Combine(@"Y.txt"), false),
                        new Tuple<string, bool>(Path.Combine(@"Z.txt"), false),
                        new Tuple<string, bool>(Path.Combine(@"B", "C.txt"), false),
                        new Tuple<string, bool>(Path.Combine(@"B", "D.ini"), false),
                    };
                        break;
                    case SampleSet.Src03:
                        checkList = new[]
                        {
                        new Tuple<string, bool>(Path.Combine(@"\가"), false),
                        new Tuple<string, bool>(Path.Combine(@"\나"), false),
                    };
                        break;
                    default:
                        throw new InvalidOperationException("Invalid SampleSet");
                }

                foreach (var tup in checkList)
                    Assert.IsTrue(paths.Contains(tup, new CheckWimPathComparer()));
            }

            public static void CheckAppend_Src01(string dir)
            {
                Assert.IsTrue(Directory.Exists(Path.Combine(dir, "ABDE")));
                Assert.IsTrue(Directory.Exists(Path.Combine(dir, "ABDE", "Z")));

                Assert.IsTrue(File.Exists(Path.Combine(dir, "Z.txt")));
                Assert.IsTrue(new FileInfo(Path.Combine(dir, "Z.txt")).Length == 1);

                Assert.IsTrue(File.Exists(Path.Combine(dir, "ABDE", "A.txt")));
                Assert.IsTrue(new FileInfo(Path.Combine(dir, "ABDE", "A.txt")).Length == 1);

                Assert.IsTrue(File.Exists(Path.Combine(dir, "ABDE", "Z", "X.txt")));
                Assert.IsTrue(File.Exists(Path.Combine(dir, "ABDE", "Z", "Y.ini")));
                Assert.IsTrue(new FileInfo(Path.Combine(dir, "ABDE", "Z", "X.txt")).Length == 1);
                Assert.IsTrue(new FileInfo(Path.Combine(dir, "ABDE", "Z", "Y.ini")).Length == 1);
            }

            public static List<Tuple<string, bool>> GenerateWimPathList(string wimFile)
            {
                List<Tuple<string, bool>> entries = new List<Tuple<string, bool>>();

                CallbackStatus IterateCallback(DirEntry dentry, object userData)
                {
                    string path = dentry.FullPath;
                    bool isDir = (dentry.Attributes & FileAttribute.DIRECTORY) != 0;
                    entries.Add(new Tuple<string, bool>(path, isDir));

                    return CallbackStatus.CONTINUE;
                }

                using (Wim wim = Wim.OpenWim(wimFile, OpenFlags.DEFAULT))
                {
                    wim.IterateDirTree(1, Wim.RootPath, IterateFlags.RECURSIVE, IterateCallback);
                }

                return entries;
            }

            public class CheckWimPathComparer : IEqualityComparer<Tuple<string, bool>>
            {
                public bool Equals(Tuple<string, bool> x, Tuple<string, bool> y)
                {
                    if (x == null)
                        throw new ArgumentNullException(nameof(x));
                    if (y == null)
                        throw new ArgumentNullException(nameof(y));

                    bool path = x.Item1.Equals(y.Item1, StringComparison.Ordinal);
                    bool isDir = x.Item2 == y.Item2;
                    return path && isDir;
                }

                public int GetHashCode(Tuple<string, bool> x)
                {
                    return x.Item1.GetHashCode() ^ x.Item2.GetHashCode();
                }
            }
        }

        #endregion
    }
}

