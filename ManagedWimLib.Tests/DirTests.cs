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

using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ManagedWimLib;
using System.IO;
using System.Threading;
using System.Collections.Generic;

namespace ManagedWimLib.Tests
{
    [TestClass]
    public class DirTests
    {
        #region DirProgress
        [TestMethod]
        [TestCategory("WimLib")]
        public void DirProgress()
        {
            DirProgress_Template("XPRESS.wim");
            DirProgress_Template("LZX.wim");
            DirProgress_Template("LZMS.wim");
            DirProgress_Template("BootLZX.wim");
            DirProgress_Template("BootXPRESS.wim");
        }

        public WimLibProgressStatus DirProgress_Callback(WimLibProgressMsg msg, object info, object progctx)
        {
            CallbackTested tested = progctx as CallbackTested;
            Assert.IsNotNull(tested);

            switch (msg)
            {
                case WimLibProgressMsg.EXTRACT_STREAMS:
                    { // Extract of one file
                        WimLibProgressInfo_Extract m = (WimLibProgressInfo_Extract)info;
                        Assert.IsNotNull(m);

                        tested.Set();

                        Console.WriteLine($"Extracting {m.WimFileName} ({m.CompletedBytes * 100 / m.TotalBytes}%)");
                    }
                    break;
                default:
                    break;
            }
            return WimLibProgressStatus.CONTINUE;
        }

        public int IterateDirTree_Callback(DirEntry dentry, object userData)
        {
            List<string> entries = userData as List<string>;

            entries.Add(dentry.FullPath);
            Console.WriteLine(dentry.FullPath);

            return 0;
        }

        public void DirProgress_Template(string fileName)
        {
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                CallbackTested tested = new CallbackTested(false);
                List<string> entries = new List<string>();

                string wimFile = Path.Combine(TestSetup.BaseDir, "Samples", fileName);
                using (Wim wim = Wim.OpenWim(wimFile, WimLibOpenFlags.DEFAULT, DirProgress_Callback, tested))
                {
                    wim.IterateDirTree(1, @"\", WimLibIterateFlags.RECURSIVE, IterateDirTree_Callback, entries);
                }

                Assert.IsTrue(tested.Value);
                // TestHelper.CheckSrc01(destDir);
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
