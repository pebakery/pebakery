using ManagedWimLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PEBakery.Tests.Core.Command
{
    [TestClass]
    public class CommandWimTests
    {
        #region WimInfo
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandWim")]
        public void Wim_WimInfo()
        {
            EngineState s = EngineTests.CreateEngineState();

            string pbSrcDir = Path.Combine("%TestBench%", "CommandWim");
            string srcDir = StringEscaper.Preprocess(s, pbSrcDir);

            // Global Information
            Info_Template(s, $@"WimInfo,{pbSrcDir}\LZX.wim,0,ImageCount,%Dest%", "1");
            Info_Template(s, $@"WimInfo,{pbSrcDir}\XPRESS.wim,0,Compression,%Dest%", "XPRESS");
            Info_Template(s, $@"WimInfo,{pbSrcDir}\LZX.wim,0,Compression,%Dest%", "LZX");
            Info_Template(s, $@"WimInfo,{pbSrcDir}\LZMS.wim,0,Compression,%Dest%", "LZMS");
            Info_Template(s, $@"WimInfo,{pbSrcDir}\BootLZX.wim,0,BootIndex,%Dest%", "1");

            // Per-Image Information
            Info_Template(s, $@"WimInfo,{pbSrcDir}\LZX.wim,1,Name,%Dest%", "Sample");
        }

        public void Info_Template(EngineState s, string rawCode, string comp, ErrorCheck check = ErrorCheck.Success)
        {
            EngineTests.Eval(s, rawCode, CodeType.WimInfo, check);

            if (check == ErrorCheck.Success || check == ErrorCheck.Warning)
            {
                string dest = s.Variables["Dest"];
                Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
            }
        }
        #endregion

        #region WimApply
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandWim")]
        public void Wim_WimApply()
        {
            EngineState s = EngineTests.CreateEngineState();

            string pbSampleDir = Path.Combine("%TestBench%", "CommandWim");
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string pbDestDir = StringEscaper.Escape(destDir);

            Apply_Template(s, $@"WimApply,{pbSampleDir}\XPRESS.wim,1,{pbDestDir}", destDir);
            Apply_Template(s, $@"WimApply,{pbSampleDir}\LZX.wim,1,{pbDestDir}", destDir);
            Apply_Template(s, $@"WimApply,{pbSampleDir}\LZMS.wim,1,{pbDestDir}", destDir);
            Apply_Template(s, $@"WimApply,{pbSampleDir}\BootXPRESS.wim,1,{pbDestDir}", destDir);
            Apply_Template(s, $@"WimApply,{pbSampleDir}\BootLZX.wim,1,{pbDestDir}", destDir);

            Apply_Template(s, $@"WimApply,{pbSampleDir}\XPRESS.wim,1,{pbDestDir},CHECK", destDir);
            Apply_Template(s, $@"WimApply,{pbSampleDir}\LZX.wim,1,{pbDestDir},NOACL", destDir);
            Apply_Template(s, $@"WimApply,{pbSampleDir}\LZMS.wim,1,{pbDestDir},CHECK,NOACL", destDir);
            Apply_Template(s, $@"WimApply,{pbSampleDir}\BootXPRESS.wim,1,{pbDestDir},NOATTRIB", destDir);
            Apply_Template(s, $@"WimApply,{pbSampleDir}\BootLZX.wim,1,{pbDestDir},CHECK,NOATTRIB", destDir);
            Apply_Template(s, $@"WimApply,{pbSampleDir}\BootLZX.wim,1,{pbDestDir},CHECK,NOACL,NOATTRIB", destDir);

            Apply_Template(s, $@"WimApply,{pbSampleDir}\LZX.wim,1,{pbDestDir},CHECK,NOACL,NOATTRIB,TRASH", destDir, ErrorCheck.ParserError);
            Apply_Template(s, $@"WimApply,{pbSampleDir}\LZX.wim,2,{pbDestDir}", destDir, ErrorCheck.Error);
        }

        public void Apply_Template(EngineState s, string rawCode, string destDir, ErrorCheck check = ErrorCheck.Success)
        {
            try
            {
                EngineTests.Eval(s, rawCode, CodeType.WimApply, check);
                if (check == ErrorCheck.Success)
                {
                    CheckDir_Src01(destDir);
                }
            }
            finally
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
            }        
        }
        #endregion

        #region WimExtract
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandWim")]
        public void Wim_WimExtract()
        {
            EngineState s = EngineTests.CreateEngineState();

            string pbSampleDir = Path.Combine("%TestBench%", "CommandWim");
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string pbDestDir = StringEscaper.Escape(destDir);

            Extract_Template(s, $@"WimExtract,{pbSampleDir}\XPRESS.wim,1,\ACDE.txt,{pbDestDir}", destDir, new string[] { "ACDE.txt" });
            Extract_Template(s, $@"WimExtract,{pbSampleDir}\LZX.wim,1,\ABCD\Z,{pbDestDir}", destDir, new string[]
            {
                Path.Combine("Z", "X.txt"),
                Path.Combine("Z", "Y.ini"),
            });
            Extract_Template(s, $@"WimExtract,{pbSampleDir}\LZMS.wim,1,\ABCD\*.txt,{pbDestDir}", destDir, new string[]
            {
                "A.txt",
                "B.txt",
                "C.txt",
            });

            Extract_Template(s, $@"WimExtract,{pbSampleDir}\BootXPRESS.wim,1,\ABDE\A.txt,{pbDestDir},NOATTRIB", destDir, new string[] 
            {
                "A.txt",
            });
            Extract_Template(s, $@"WimExtract,{pbSampleDir}\BootLZX.wim,1,\ABDE\A.txt,{pbDestDir},CHECK,NOATTRIB", destDir, new string[]
            {
                "A.txt",
            });
            Extract_Template(s, $@"WimExtract,{pbSampleDir}\BootLZX.wim,1,\ABDE\A.txt,{pbDestDir},CHECK,NOACL,NOATTRIB", destDir, new string[]
            {
                "A.txt",
            });

            Extract_Template(s, $@"WimExtract,{pbSampleDir}\LZX.wim,1,\*.exe,{pbDestDir}", destDir, new string[0]);

            Extract_Template(s, $@"WimExtract,{pbSampleDir}\Split.swm,1,\나,{pbDestDir},Split={pbSampleDir}\Split*.swm", destDir, new string[]
            { // Unicode test with Korean letter
                "나",
            });

            Extract_Template(s, $@"WimExtract,{pbSampleDir}\LZX.wim,1,\ACDE.txt,{pbDestDir},CHECK,NOACL,NOATTRIB,TRASH", destDir, null, ErrorCheck.ParserError);
            Extract_Template(s, $@"WimExtract,{pbSampleDir}\LZX.wim,2,\ACDE.txt,{pbDestDir}", destDir, null, ErrorCheck.Error);
        }

        public void Extract_Template(EngineState s, string rawCode, string destDir, string[] compFiles, ErrorCheck check = ErrorCheck.Success)
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
        #endregion

        #region WimExtractBulk
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandWim")]
        public void Wim_WimExtractBulk()
        {
            EngineState s = EngineTests.CreateEngineState();

            string pbSampleDir = Path.Combine("%TestBench%", "CommandWim");
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string pbDestDir = StringEscaper.Escape(destDir);

            ExtractBulk_Template(s, $@"WimExtractBulk,{pbSampleDir}\XPRESS.wim,1,{pbDestDir}\ListFile.txt,{pbDestDir}", destDir, new string[] { "ACDE.txt" });
            ExtractBulk_Template(s, $@"WimExtractBulk,{pbSampleDir}\LZX.wim,1,{pbDestDir}\ListFile.txt,{pbDestDir}", destDir, new string[]
            {
                Path.Combine("ABCD", "Z", "X.txt"),
                Path.Combine("ABCD", "Z", "Y.ini"),
            });
            ExtractBulk_Template(s, $@"WimExtractBulk,{pbSampleDir}\LZMS.wim,1,{pbDestDir}\ListFile.txt,{pbDestDir}", destDir, new string[]
            {
                Path.Combine("ABCD", "A.txt"),
                Path.Combine("ABCD", "B.txt"),
                Path.Combine("ABCD", "C.txt"),
            });

            ExtractBulk_Template(s, $@"WimExtractBulk,{pbSampleDir}\BootXPRESS.wim,1,{pbDestDir}\ListFile.txt,{pbDestDir},NOATTRIB", destDir, new string[]
            {
                Path.Combine("ABDE", "A.txt"),
            });
            ExtractBulk_Template(s, $@"WimExtractBulk,{pbSampleDir}\BootLZX.wim,1,{pbDestDir}\ListFile.txt,{pbDestDir},CHECK,NOATTRIB", destDir, new string[]
            {
                Path.Combine("ABDE", "A.txt"),
            });
            ExtractBulk_Template(s, $@"WimExtractBulk,{pbSampleDir}\BootLZX.wim,1,{pbDestDir}\ListFile.txt,{pbDestDir},CHECK,NOACL,NOATTRIB", destDir, new string[]
            {
                Path.Combine("ABDE", "A.txt"),
            });

            ExtractBulk_Template(s, $@"WimExtractBulk,{pbSampleDir}\Split.swm,1,{pbDestDir}\ListFile.txt,{pbDestDir},Split={pbSampleDir}\Split*.swm", destDir, new string[]
            { // Unicode test with Korean letter
                "나",
            });

            ExtractBulk_Template(s, $@"WimExtractBulk,{pbSampleDir}\LZX.wim,1,{pbDestDir}\ListFile.txt,{pbDestDir},CHECK,NOACL,NOATTRIB,TRASH", destDir, null, ErrorCheck.ParserError);
            ExtractBulk_Template(s, $@"WimExtractBulk,{pbSampleDir}\LZX.wim,2,{pbDestDir}\ListFile.txt,{pbDestDir}", destDir, null, ErrorCheck.Error);
        }

        public void ExtractBulk_Template(EngineState s, string rawCode, string destDir, string[] compFiles, ErrorCheck check = ErrorCheck.Success)
        {
            Directory.CreateDirectory(destDir);
            string listFile = Path.Combine(destDir, "ListFile.txt");
            try
            {
                if (compFiles != null)
                {
                    using (StreamWriter w = new StreamWriter(listFile, false, Encoding.Unicode))
                    {
                        for (int i = 0; i < compFiles.Length; i++)
                            w.WriteLine(@"\" + compFiles[i]);
                    }
                }

                EngineTests.Eval(s, rawCode, CodeType.WimExtractBulk, check);
                if (check == ErrorCheck.Success)
                {
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
        #endregion

        #region WimCapture
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandWim")]
        public void Wim_WimCapture()
        {
            EngineState s = EngineTests.CreateEngineState();

            string pbSampleDir = Path.Combine("%TestBench%", "CommandWim");
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string pbDestDir = StringEscaper.Escape(destDir);

            Directory.CreateDirectory(destDir);
            try
            {
                Capture_Template(s, $@"WimCapture,{pbSampleDir}\Src01,{pbDestDir}\XPRESS.wim,XPRESS", destDir, "XPRESS.wim");
                Capture_Template(s, $@"WimCapture,{pbSampleDir}\Src01,{pbDestDir}\LZX.wim,LZX", destDir, "LZX.wim");
                Capture_Template(s, $@"WimCapture,{pbSampleDir}\Src01,{pbDestDir}\LZMS.wim,LZMS", destDir, "LZMS.wim");

                Capture_Template(s, $@"WimCapture,{pbSampleDir}\Src01,{pbDestDir}\XPRESS.wim,XPRESS,BOOT", destDir, "XPRESS.wim");
                Capture_Template(s, $@"WimCapture,{pbSampleDir}\Src01,{pbDestDir}\LZX.wim,LZX,NOACL", destDir, "LZX.wim");
                Capture_Template(s, $@"WimCapture,{pbSampleDir}\Src01,{pbDestDir}\LZMS.wim,LZMS,CHECK", destDir, "LZMS.wim");

                Capture_Template(s, $@"WimCapture,{pbSampleDir}\Src01,{pbDestDir}\XPRESS.wim,XPRESS,ImageName=NormalCompress", destDir, "XPRESS.wim");
                Capture_Template(s, $@"WimCapture,{pbSampleDir}\Src01,{pbDestDir}\LZX.wim,LZX,ImageDesc=MaxCompress", destDir, "LZX.wim");
                Capture_Template(s, $@"WimCapture,{pbSampleDir}\Src01,{pbDestDir}\LZMS.wim,LZMS,Flags=PEBakeryWimFlag", destDir, "LZMS.wim");

                Capture_Template(s, $@"WimCapture,{pbSampleDir}\Src01,{pbDestDir}\XPRESS.wim,WRONGCOMP", destDir, "XPRESS.wim", ErrorCheck.Error);
                Capture_Template(s, $@"WimCapture,{pbSampleDir}\Src01,{pbDestDir}\LZX.wim,LZX,TRASH", destDir, "LZX.wim", ErrorCheck.ParserError);
            }
            finally
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
            }
        }

        public void Capture_Template(EngineState s, string rawCode, string destDir, string wimFileName, ErrorCheck check = ErrorCheck.Success)
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

                    CheckDir_Src01(applyDir);
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
        #endregion

        #region WimAppend
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandWim")]
        public void Wim_WimAppend()
        { // WimAppend,<SrcDir>,<DestWim>,[IMAGENAME=STR],[ImageDesc=STR],[Flags=STR],[DeltaIndex=INT],[BOOT],[CHECK],[NOACL]
            EngineState s = EngineTests.CreateEngineState();

            string pbSampleDir = Path.Combine("%TestBench%", "CommandWim");
            string sampleDir = StringEscaper.Preprocess(s, pbSampleDir);
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string pbDestDir = StringEscaper.Escape(destDir);

            Directory.CreateDirectory(destDir);
            try
            {
                Append_Template(s, $@"WimAppend,{pbSampleDir}\Src03,{pbDestDir}\XPRESS.wim", sampleDir, destDir, "XPRESS.wim");
                Append_Template(s, $@"WimAppend,{pbSampleDir}\Src03,{pbDestDir}\LZX.wim", sampleDir, destDir, "LZX.wim");
                Append_Template(s, $@"WimAppend,{pbSampleDir}\Src03,{pbDestDir}\LZMS.wim", sampleDir, destDir, "LZMS.wim");

                Append_Template(s, $@"WimAppend,{pbSampleDir}\Src03,{pbDestDir}\XPRESS.wim,BOOT", sampleDir, destDir, "XPRESS.wim");
                Append_Template(s, $@"WimAppend,{pbSampleDir}\Src03,{pbDestDir}\LZX.wim,NOACL", sampleDir, destDir, "LZX.wim");
                Append_Template(s, $@"WimAppend,{pbSampleDir}\Src03,{pbDestDir}\LZMS.wim,CHECK", sampleDir, destDir, "LZMS.wim");

                Append_Template(s, $@"WimAppend,{pbSampleDir}\Src01,{pbDestDir}\LZX.wim,TRASH", sampleDir, destDir, "LZX.wim", ErrorCheck.ParserError);
                Append_Template(s, $@"WimAppend,{pbSampleDir}\Src01,{pbDestDir}\LZX.wim,LZX,TRASH", sampleDir, destDir, "LZX.wim", ErrorCheck.ParserError);
            }
            finally
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
            }
        }

        public void Append_Template(EngineState s, string rawCode, string srcDir, string destDir, string wimFileName, ErrorCheck check = ErrorCheck.Success)
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
                        Assert.IsTrue(wi.ImageCount == (srcImageCount + 1));

                        wim.ExtractImage((int)(srcImageCount + 1), applyDir, ExtractFlags.DEFAULT);
                    }

                    CheckDir_Src03(applyDir);
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
        #endregion

        #region WimDelete
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandWim")]
        public void Wim_WimDelete()
        {
            EngineState s = EngineTests.CreateEngineState();

            string pbSampleDir = Path.Combine("%TestBench%", "CommandWim");
            string sampleDir = StringEscaper.Preprocess(s, pbSampleDir);
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string pbDestDir = StringEscaper.Escape(destDir);

            Delete_Template(s, $@"WimDelete,{pbDestDir}\MultiImage.wim,1", sampleDir, destDir, "MultiImage.wim");
            Delete_Template(s, $@"WimDelete,{pbDestDir}\MultiImage.wim,3,CHECK", sampleDir, destDir, "MultiImage.wim");
            
            Delete_Template(s, $@"WimDelete,{pbDestDir}\MultiImage.wim,3,TRASH", sampleDir, destDir, "MultiImage.wim", ErrorCheck.ParserError);
            Delete_Template(s, $@"WimDelete,{pbDestDir}\MultiImage.wim,4", sampleDir, destDir, "MultiImage.wim", ErrorCheck.Error);
        }

        public void Delete_Template(EngineState s, string rawCode, string srcDir, string destDir, string wimFileName, ErrorCheck check = ErrorCheck.Success)
        {
            Directory.CreateDirectory(destDir);
            try
            {
                string srcWim = Path.Combine(srcDir, wimFileName);
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
                        Assert.IsTrue(wi.ImageCount == (srcImageCount - 1));
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

            PathAdd_Template(s, $@"WimPathAdd,{pbDestDir}\LZX.wim,1,{pbSampleDir}\Src03\가,\다", sampleDir, destDir, "LZX.wim", @"\다");
            PathAdd_Template(s, $@"WimPathAdd,{pbDestDir}\LZX.wim,1,{pbSampleDir}\Src03,\,CHECK", sampleDir, destDir, "LZX.wim", @"\");
            PathAdd_Template(s, $@"WimPathAdd,{pbDestDir}\LZX.wim,1,{pbSampleDir}\Src03\나,\ACDE.txt,PRESERVE", sampleDir, destDir,
                "LZX.wim", null, ErrorCheck.Error);

            PathAdd_Template(s, $@"WimPathAdd,{pbDestDir}\LZX.wim,1,{pbSampleDir}\Src03\나,\ACDE.txt,TRASH", sampleDir, destDir, 
                "LZX.wim", null, ErrorCheck.ParserError);
            PathAdd_Template(s, $@"WimPathAdd,{pbDestDir}\LZX.wim,2,{pbSampleDir}\Src03\나,\ACDE.txt", sampleDir, destDir, 
                "LZX.wim", null, ErrorCheck.Error);
        }

        public void PathAdd_Template(EngineState s, string rawCode, string srcDir, string destDir, string wimFileName, string comp,
            ErrorCheck check = ErrorCheck.Success)
        {
            string srcWim = Path.Combine(srcDir, wimFileName);
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
        #endregion

        #region WimPathDelete
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandWim")]
        public void Wim_WimPathDelete()
        { // WimPathDelete,<WimFile>,<ImageIndex>,<Path>,[CHECK],[REBUILD]
            EngineState s = EngineTests.CreateEngineState();

            string pbSampleDir = Path.Combine("%TestBench%", "CommandWim");
            string sampleDir = StringEscaper.Preprocess(s, pbSampleDir);
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string pbDestDir = StringEscaper.Escape(destDir);

            PathDelete_Template(s, $@"WimPathDelete,{pbDestDir}\LZX.wim,1,\ACDE.txt", sampleDir, destDir, "LZX.wim", @"\ACDE.txt");
            PathDelete_Template(s, $@"WimPathDelete,{pbDestDir}\LZX.wim,1,\ABCD,CHECK", sampleDir, destDir, "LZX.wim", @"\ABCD");
            PathDelete_Template(s, $@"WimPathDelete,{pbDestDir}\LZX.wim,1,\ABDE,REBUILD", sampleDir, destDir, "LZX.wim", @"\ABDE");

            PathDelete_Template(s, $@"WimPathDelete,{pbDestDir}\LZX.wim,1,\ACDE.txt,TRASH", sampleDir, destDir,
                "LZX.wim", null, ErrorCheck.ParserError);
            PathDelete_Template(s, $@"WimPathDelete,{pbDestDir}\LZX.wim,2,\ACDE.txt", sampleDir, destDir,
                "LZX.wim", null, ErrorCheck.Error);
            PathDelete_Template(s, $@"WimPathDelete,{pbDestDir}\LZX.wim,1,\NONEXIST", sampleDir, destDir,
                "LZX.wim", null, ErrorCheck.Error);
        }

        public void PathDelete_Template(EngineState s, string rawCode, string srcDir, string destDir, string wimFileName, string comp,
            ErrorCheck check = ErrorCheck.Success)
        {
            string srcWim = Path.Combine(srcDir, wimFileName);
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
        #endregion

        #region WimPathRename
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandWim")]
        public void Wim_WimPathRename()
        {
            EngineState s = EngineTests.CreateEngineState();

            string pbSampleDir = Path.Combine("%TestBench%", "CommandWim");
            string sampleDir = StringEscaper.Preprocess(s, pbSampleDir);
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string pbDestDir = StringEscaper.Escape(destDir);

            PathAddRename_Template(s, $@"WimPathRename,{pbDestDir}\LZX.wim,1,\ACDE.txt,\평창,CHECK", sampleDir, destDir, 
                "LZX.wim", @"\ACDE.txt", @"\평창");
            PathAddRename_Template(s, $@"WimPathRename,{pbDestDir}\LZX.wim,1,\ABDE,\RENAME,REBUILD", sampleDir, destDir,
                "LZX.wim", @"\ABDE", @"\RENAME");
            PathAddRename_Template(s, $@"WimPathRename,{pbDestDir}\LZX.wim,1,\ABCD,\Z", sampleDir, destDir,
                "LZX.wim", @"\ABCD", @"\Z");

            PathAddRename_Template(s, $@"WimPathRename,{pbDestDir}\LZX.wim,1,\ERROR,\DUMMY,TRASH", sampleDir, destDir,
                "LZX.wim", null, null, ErrorCheck.ParserError);
            PathAddRename_Template(s, $@"WimPathRename,{pbDestDir}\LZX.wim,2,\없음,\DUMMY", sampleDir, destDir,
                "LZX.wim", null, null, ErrorCheck.Error);
        }

        public void PathAddRename_Template(EngineState s, string rawCode, string srcDir, string destDir, string wimFileName, 
            string originalName, string newName, ErrorCheck check = ErrorCheck.Success)
        {
            string srcWim = Path.Combine(srcDir, wimFileName);
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
        #endregion

        #region WimOptimize
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandWim")]
        public void Wim_WimOptimize()
        { // WimOptimize,<WimFile>,[RECOMPRESS[=STR]],[CHECK|NOCHECK]
            EngineState s = EngineTests.CreateEngineState();

            string pbSampleDir = Path.Combine("%TestBench%", "CommandWim");
            string sampleDir = StringEscaper.Preprocess(s, pbSampleDir);
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string pbDestDir = StringEscaper.Escape(destDir);

            Directory.CreateDirectory(destDir);
            try
            {
                Optimize_Template(s, $@"WimOptimize,{pbDestDir}\ToOptimize.wim", sampleDir, destDir, "ToOptimize.wim");
                Optimize_Template(s, $@"WimOptimize,{pbDestDir}\ToOptimize.wim,CHECK", sampleDir, destDir, "ToOptimize.wim");
                Optimize_Template(s, $@"WimOptimize,{pbDestDir}\ToOptimize.wim,RECOMPRESS=LZMS", sampleDir, destDir, "ToOptimize.wim");
            }
            finally
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
            }
        }

        public void Optimize_Template(EngineState s, string rawCode, string srcDir, string destDir, string wimFileName, ErrorCheck check = ErrorCheck.Success)
        {
            string srcWim = Path.Combine(srcDir, wimFileName);
            string destWim = Path.Combine(destDir, wimFileName);

            try
            {
                File.Copy(srcWim, destWim, true);
                long oldSize = new FileInfo(destWim).Length;

                EngineTests.Eval(s, rawCode, CodeType.WimOptimize, check);
                if (check == ErrorCheck.Success)
                {
                    long newSize = new FileInfo(destWim).Length;
                    Assert.IsTrue(newSize < oldSize);
                }
            }
            finally
            {
                if (File.Exists(destWim))
                    File.Delete(destWim);
            }
        }
        #endregion

        #region Utility
        public static void CheckDir_Src01(string dir)
        {
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
        }

        public static void CheckDir_Src02_1(string dir)
        {
            Assert.IsTrue(Directory.Exists(Path.Combine(dir, "B")));

            Assert.IsTrue(File.Exists(Path.Combine(dir, "A.txt")));
            Assert.IsTrue(new FileInfo(Path.Combine(dir, "A.txt")).Length == 1);

            Assert.IsTrue(File.Exists(Path.Combine(dir, "B", "C.txt")));
            Assert.IsTrue(new FileInfo(Path.Combine(dir, "B", "C.txt")).Length == 1);
            Assert.IsTrue(File.Exists(Path.Combine(dir, "B", "D.txt")));
            Assert.IsTrue(new FileInfo(Path.Combine(dir, "B", "D.txt")).Length == 1);
        }

        public static void CheckDir_Src03(string dir)
        {
            Assert.IsTrue(File.Exists(Path.Combine(dir, "가")));
            Assert.IsTrue(new FileInfo(Path.Combine(dir, "가")).Length == 1024 * 1024);
            Assert.IsTrue(File.Exists(Path.Combine(dir, "나")));
            Assert.IsTrue(new FileInfo(Path.Combine(dir, "나")).Length == 1024 * 1024);
        }
        #endregion
    }
}

