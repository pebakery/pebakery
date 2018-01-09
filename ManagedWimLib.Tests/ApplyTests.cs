using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ManagedWimLib;
using System.IO;
using System.Threading;

namespace ManagedWimLib.Tests
{
    [TestClass]
    public class ApplyTests
    {
        #region Apply
        [TestMethod]
        [TestCategory("WimLib")]
        [TestCategory("WimLib_Apply")]
        public void Apply()
        {
            Apply_Template("XPRESS.wim");
            Apply_Template("LZX.wim");
            Apply_Template("LZMS.wim");
            Apply_Template("BootLZX.wim");
            Apply_Template("BootXPRESS.wim");
        }

        public void Apply_Template(string fileName)
        {
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                string wimFile = Path.Combine(TestSetup.BaseDir, "Samples", fileName);
                using (Wim wim = Wim.OpenWim(wimFile, WimLibOpenFlags.DEFAULT))
                {
                    wim.ExtractImage(1, destDir, WimLibExtractFlags.DEFAULT);
                }

                TestHelper.CheckSample01(destDir);
            }
            finally
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
            }
        }
        #endregion

        #region ApplyProgress
        [TestMethod]
        [TestCategory("WimLib")]
        [TestCategory("WimLib_Apply")]
        public void ApplyProgress()
        {
            ApplyProgress_Template("XPRESS.wim");
            ApplyProgress_Template("LZX.wim");
            ApplyProgress_Template("LZMS.wim");
            ApplyProgress_Template("BootLZX.wim");
            ApplyProgress_Template("BootXPRESS.wim");
        }

        public WimLibProgressStatus ApplyProgress_Callback(WimLibProgressMsg msg, object info, object progctx)
        {
            CallbackTested tested = progctx as CallbackTested;
            Assert.IsNotNull(tested);

            switch (msg)
            {
                case WimLibProgressMsg.EXTRACT_STREAMS:
                    { // Extract of one file
                        WimLibProgressInfoExtract m = (WimLibProgressInfoExtract) info;
                        Assert.IsNotNull(m);

                        tested.Set();

                        Console.WriteLine($"Extract {m.CompletedBytes * 100 / m.TotalBytes}%");
                    }
                    break;
                default:
                    break;
            }
            return WimLibProgressStatus.CONTINUE;
        }

        public void ApplyProgress_Template(string fileName)
        {
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                CallbackTested tested = new CallbackTested(false);

                string wimFile = Path.Combine(TestSetup.BaseDir, "Samples", fileName);
                using (Wim wim = Wim.OpenWim(wimFile, WimLibOpenFlags.DEFAULT, ApplyProgress_Callback, tested))
                {
                    wim.ExtractImage(1, destDir, WimLibExtractFlags.DEFAULT);
                }

                Assert.IsTrue(tested.Value);

                TestHelper.CheckSample01(destDir);
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
