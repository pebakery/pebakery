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

