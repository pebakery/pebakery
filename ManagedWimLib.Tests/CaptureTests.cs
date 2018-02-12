/*
    Licensed under LGPLv3

    Derived from wimlib's original header files
    Copyright (C) 2012, 2013, 2014 Eric Biggers

    C# Wrapper written by Hajin Jang
    Copyright (C) 2017-2018 Hajin Jang

    This file is free software; you can redistribute it and/or modify it under
    the terms of the GNU Lesser General Public License as published by the Free
    Software Foundation; either version 3 of the License, or (at your option) any
    later version.

    This file is distributed in the hope that it will be useful, but WITHOUT
    ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
    FOR A PARTICULAR PURPOSE. See the GNU Lesser General Public License for more
    details.

    You should have received a copy of the GNU Lesser General Public License
    along with this file; if not, see http://www.gnu.org/licenses/.
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
            Capture_Template(CompressionType.NONE, "NONE.wim");
            Capture_Template(CompressionType.XPRESS, "XPRESS.wim");
            Capture_Template(CompressionType.LZX, "LZX.wim");
            Capture_Template(CompressionType.LZMS, "LZMS.wim");

            Capture_Template(CompressionType.NONE, "NONE.wim", AddFlags.BOOT);
            Capture_Template(CompressionType.XPRESS, "XPRESS.wim", AddFlags.BOOT);
            Capture_Template(CompressionType.LZX, "LZX.wim", AddFlags.BOOT);
            Capture_Template(CompressionType.LZMS, "LZMS.wim", AddFlags.BOOT);
        }

        public void Capture_Template(CompressionType compType, string wimFileName, AddFlags addFlags = AddFlags.DEFAULT)
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
                    wim.Write(wimFile, Wim.AllImages, WriteFlags.DEFAULT, Wim.DefaultThreads);
                }

                // Apply it, to test if wim was successfully captured
                using (Wim wim = Wim.OpenWim(wimFile, OpenFlags.DEFAULT))
                {
                    wim.ExtractImage(1, destDir, ExtractFlags.DEFAULT);
                }

                TestHelper.CheckDir_Src01(destDir);
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
            CaptureProgress_Template(CompressionType.NONE, "NONE.wim");
            CaptureProgress_Template(CompressionType.XPRESS, "XPRESS.wim");
            CaptureProgress_Template(CompressionType.LZX, "LZX.wim");
            CaptureProgress_Template(CompressionType.LZMS, "LZMS.wim");

            CaptureProgress_Template(CompressionType.NONE, "NONE.wim", AddFlags.WIMBOOT);
            CaptureProgress_Template(CompressionType.XPRESS, "XPRESS.wim", AddFlags.WIMBOOT);
            CaptureProgress_Template(CompressionType.LZX, "LZX.wim", AddFlags.WIMBOOT);
            CaptureProgress_Template(CompressionType.LZMS, "LZMS.wim", AddFlags.WIMBOOT);
        }

        public CallbackStatus CaptureProgress_Callback(ProgressMsg msg, object info, object progctx)
        {
            CallbackTested tested = progctx as CallbackTested;
            Assert.IsNotNull(tested);

            switch (msg)
            {
                case ProgressMsg.WRITE_STREAMS:
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
            return CallbackStatus.CONTINUE;
        }

        public void CaptureProgress_Template(CompressionType compType, string wimFileName, AddFlags addFlags = AddFlags.DEFAULT)
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
                    wim.Write(wimFile, Wim.AllImages, WriteFlags.DEFAULT, Wim.DefaultThreads);
                }

                // Apply it, to test if wim was successfully captured
                using (Wim wim = Wim.OpenWim(wimFile, OpenFlags.DEFAULT))
                {
                    wim.ExtractImage(1, destDir, ExtractFlags.DEFAULT);
                }

                Assert.IsTrue(tested.Value);
                TestHelper.CheckDir_Src01(destDir);
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
