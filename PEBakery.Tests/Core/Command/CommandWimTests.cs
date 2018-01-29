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
                    CheckWimSrc01(destDir);
                }
            }
            finally
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
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

        public void Capture_Template(EngineState s, string rawCode, string destDir, string destWim, ErrorCheck check = ErrorCheck.Success)
        {
            string applyDir = Path.Combine(destDir, "CaptureApply");
            string wimFile = Path.Combine(destDir, destWim);

            Directory.CreateDirectory(applyDir);
            try
            {
                EngineTests.Eval(s, rawCode, CodeType.WimCapture, check);
                if (check == ErrorCheck.Success)
                {
                    Assert.IsTrue(File.Exists(wimFile));

                    // Try applying
                    using (Wim wim = Wim.OpenWim(wimFile, WimLibOpenFlags.DEFAULT))
                    {
                        wim.ExtractImage(1, destDir, WimLibExtractFlags.DEFAULT);
                    }

                    CheckWimSrc01(destDir);
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

        #region Utility
        public static void CheckWimSrc01(string dir)
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
        #endregion
    }
}

