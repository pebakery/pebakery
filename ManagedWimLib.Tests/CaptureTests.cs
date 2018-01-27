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
*/

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ManagedWimLib;
using System.IO;

namespace ManagedWimLib.Tests
{
    [TestClass]
    public class CaptureTests
    {
        #region Capture
        [TestMethod]
        [TestCategory("WimLib")]
        public void Capture()
        {
            Capture_Template(WimLibCompressionType.NONE, "NONE.wim");
            Capture_Template(WimLibCompressionType.XPRESS, "XPRESS.wim");
            Capture_Template(WimLibCompressionType.LZX, "LZX.wim");
            Capture_Template(WimLibCompressionType.LZMS, "LZMS.wim");

            Capture_Template(WimLibCompressionType.NONE, "NONE.wim", WimLibAddFlags.WIMBOOT);
            Capture_Template(WimLibCompressionType.XPRESS, "XPRESS.wim", WimLibAddFlags.WIMBOOT);
            Capture_Template(WimLibCompressionType.LZX, "LZX.wim", WimLibAddFlags.WIMBOOT);
            Capture_Template(WimLibCompressionType.LZMS, "LZMS.wim", WimLibAddFlags.WIMBOOT);
        }

        public void Capture_Template(WimLibCompressionType compType, string wimFileName, WimLibAddFlags addFlags = WimLibAddFlags.DEFAULT)
        {
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                Directory.CreateDirectory(destDir);

                // Capture Wim
                string srcDir = Path.Combine(TestSetup.BaseDir, "Samples", "Src01");
                string wimFile = Path.Combine(destDir, wimFileName);
                using (Wim wim = Wim.CreateNewWim(compType))
                {
                    wim.AddImage(srcDir, "UnitTest", null, addFlags);
                    wim.Write(wimFile, Wim.AllImages, WimLibWriteFlags.DEFAULT, Wim.DefaultThreads);
                }

                // Apply it, to test if wim was successfully captured
                using (Wim wim = Wim.OpenWim(wimFile, WimLibOpenFlags.DEFAULT))
                {
                    wim.ExtractImage(1, destDir, WimLibExtractFlags.DEFAULT);
                }

                TestHelper.CheckSrc01(destDir);
            }
            finally
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
            }
        }
        #endregion

        #region CaptureProgress
        [TestMethod]
        [TestCategory("WimLib")]
        public void CaptureProgress()
        {
            CaptureProgress_Template(WimLibCompressionType.NONE, "NONE.wim");
            CaptureProgress_Template(WimLibCompressionType.XPRESS, "XPRESS.wim");
            CaptureProgress_Template(WimLibCompressionType.LZX, "LZX.wim");
            CaptureProgress_Template(WimLibCompressionType.LZMS, "LZMS.wim");

            CaptureProgress_Template(WimLibCompressionType.NONE, "NONE.wim", WimLibAddFlags.WIMBOOT);
            CaptureProgress_Template(WimLibCompressionType.XPRESS, "XPRESS.wim", WimLibAddFlags.WIMBOOT);
            CaptureProgress_Template(WimLibCompressionType.LZX, "LZX.wim", WimLibAddFlags.WIMBOOT);
            CaptureProgress_Template(WimLibCompressionType.LZMS, "LZMS.wim", WimLibAddFlags.WIMBOOT);
        }

        public WimLibProgressStatus CaptureProgress_Callback(WimLibProgressMsg msg, object info, object progctx)
        {
            CallbackTested tested = progctx as CallbackTested;
            Assert.IsNotNull(tested);

            switch (msg)
            {
                case WimLibProgressMsg.WRITE_STREAMS:
                    { // Extract of one file
                        WimLibProgressInfo_WriteStreams m = (WimLibProgressInfo_WriteStreams)info;
                        Assert.IsNotNull(m);

                        tested.Set();

                        Console.WriteLine($"Capturing... ({m.CompletedBytes * 100 / m.TotalBytes}%)");
                    }
                    break;
                default:
                    break;
            }
            return WimLibProgressStatus.CONTINUE;
        }

        public void CaptureProgress_Template(WimLibCompressionType compType, string wimFileName, WimLibAddFlags addFlags = WimLibAddFlags.DEFAULT)
        {
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                Directory.CreateDirectory(destDir);
                CallbackTested tested = new CallbackTested(false);

                // Capture Wim
                string srcDir = Path.Combine(TestSetup.BaseDir, "Samples", "Src01");
                string wimFile = Path.Combine(destDir, wimFileName);
                using (Wim wim = Wim.CreateNewWim(compType))
                {
                    wim.RegisterCallback(CaptureProgress_Callback, tested);
                    wim.AddImage(srcDir, "UnitTest", null, addFlags);
                    wim.Write(wimFile, Wim.AllImages, WimLibWriteFlags.DEFAULT, Wim.DefaultThreads);
                }

                // Apply it, to test if wim was successfully captured
                using (Wim wim = Wim.OpenWim(wimFile, WimLibOpenFlags.DEFAULT))
                {
                    wim.ExtractImage(1, destDir, WimLibExtractFlags.DEFAULT);
                }

                Assert.IsTrue(tested.Value);
                TestHelper.CheckSrc01(destDir);
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
