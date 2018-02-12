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
    public class AppendTests
    {
        #region Append
        [TestMethod]
        [TestCategory("WimLib")]
        public void Append()
        {
            Append_Template("XPRESS.wim", false);
            Append_Template("LZX.wim", false);
            Append_Template("LZMS.wim", false);
            Append_Template("XPRESS.wim", false, AddFlags.BOOT);
            Append_Template("BootLZX.wim", false, AddFlags.BOOT);

            Append_Template("XPRESS.wim", true);
            Append_Template("LZX.wim", true);
            Append_Template("LZMS.wim", true);
            Append_Template("XPRESS.wim", true, AddFlags.BOOT);
            Append_Template("BootLZX.wim", true, AddFlags.BOOT);
        }

        public void Append_Template(string wimFileName, bool delta, AddFlags addFlags = AddFlags.DEFAULT)
        {
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                Directory.CreateDirectory(destDir);

                string srcDir = Path.Combine(TestSetup.BaseDir, "Samples", "Append01");
                string srcWimFile = Path.Combine(TestSetup.BaseDir, "Samples", wimFileName);
                string destWimFile = Path.Combine(destDir, wimFileName);
                File.Copy(srcWimFile, destWimFile, true);

                using (Wim wim = Wim.OpenWim(destWimFile, OpenFlags.WRITE_ACCESS))
                {
                    wim.AddImage(srcDir, "AppendTest", null, addFlags);

                    if (delta)
                        wim.ReferenceTemplateImage(2, 1);

                    wim.Overwrite(WriteFlags.DEFAULT, Wim.DefaultThreads);
                }

                // Apply it, to test if wim was successfully captured
                using (Wim wim = Wim.OpenWim(destWimFile, OpenFlags.DEFAULT))
                {
                    wim.ExtractImage(2, destDir, ExtractFlags.DEFAULT);
                }

                TestHelper.CheckAppend_Src01(destDir);
            }
            finally
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
            }
        }
        #endregion

        #region AppendProgress
        [TestMethod]
        [TestCategory("WimLib")]
        public void AppendProgress()
        {
            AppendProgress_Template("XPRESS.wim", false);
            AppendProgress_Template("LZX.wim", false);
            AppendProgress_Template("LZMS.wim", false);
            AppendProgress_Template("XPRESS.wim", false, AddFlags.BOOT);
            AppendProgress_Template("BootLZX.wim", false, AddFlags.BOOT);

            AppendProgress_Template("XPRESS.wim", true);
            AppendProgress_Template("LZX.wim", true);
            AppendProgress_Template("LZMS.wim", true);
            AppendProgress_Template("XPRESS.wim", true, AddFlags.BOOT);
            AppendProgress_Template("BootLZX.wim", true, AddFlags.BOOT);
        }

        public CallbackStatus AppendProgress_Callback(ProgressMsg msg, object info, object progctx)
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

        public void AppendProgress_Template(string wimFileName, bool delta, AddFlags addFlags = AddFlags.DEFAULT)
        {
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                Directory.CreateDirectory(destDir);
                CallbackTested tested = new CallbackTested(false);

                string srcDir = Path.Combine(TestSetup.BaseDir, "Samples", "Append01");
                string srcWimFile = Path.Combine(TestSetup.BaseDir, "Samples", wimFileName);
                string destWimFile = Path.Combine(destDir, wimFileName);
                File.Copy(srcWimFile, destWimFile, true);

                using (Wim wim = Wim.OpenWim(destWimFile, OpenFlags.WRITE_ACCESS))
                {
                    wim.RegisterCallback(AppendProgress_Callback, tested);
                    wim.AddImage(srcDir, "AppendTest", null, addFlags);

                    if (delta)
                        wim.ReferenceTemplateImage(2, 1);

                    wim.Overwrite(WriteFlags.DEFAULT, Wim.DefaultThreads);
                }

                // Apply it, to test if wim was successfully captured
                using (Wim wim = Wim.OpenWim(destWimFile, OpenFlags.DEFAULT))
                {
                    wim.ExtractImage(2, destDir, ExtractFlags.DEFAULT);
                }

                Assert.IsTrue(tested.Value);
                TestHelper.CheckAppend_Src01(destDir);
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
