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
    public class ExtractTests
    {
        #region ExtractPath, ExtractPaths
        [TestMethod]
        [TestCategory("WimLib")]
        public void ExtractPath()
        {
            ExtractPath_Template("XPRESS.wim", @"\ACDE.txt");
            ExtractPath_Template("LZX.wim", @"\ABCD\*.txt");
            ExtractPath_Template("LZMS.wim", @"\ABDE\Z\Y.ini");
            ExtractPath_Template("BootXPRESS.wim", @"\?CDE.txt");
            ExtractPath_Template("BootLZX.wim", @"\ACDE.txt");
        }

        public void ExtractPath_Template(string fileName, string path)
        {
            ExtractPaths_Template(fileName, new string[] { path });
        }

        [TestMethod]
        [TestCategory("WimLib")]
        public void ExtractPaths()
        {
            string[] paths = new string[] { @"\ACDE.txt", @"\ABCD\*.txt", @"\?CDE.txt" };
            ExtractPaths_Template("XPRESS.wim", paths);
            ExtractPaths_Template("LZX.wim", paths);
            ExtractPaths_Template("LZMS.wim", paths);
            ExtractPaths_Template("BootXPRESS.wim", paths);
            ExtractPaths_Template("BootLZX.wim", paths);
        }

        public void ExtractPaths_Template(string fileName, string[] paths)
        {
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                string wimFile = Path.Combine(TestSetup.BaseDir, "Samples", fileName);
                using (Wim wim = Wim.OpenWim(wimFile, WimLibOpenFlags.DEFAULT))
                {
                    wim.ExtractPaths(1, destDir, paths, WimLibExtractFlags.GLOB_PATHS);
                }

                foreach (string path in paths.Select(x => x.TrimStart('\\')))
                {
                    if (path.IndexOfAny(new char[] { '*', '?' }) == -1)
                    { // No wlidcard
                        Assert.IsTrue(File.Exists(Path.Combine(destDir, path)));
                    }
                    else
                    { // With wildcard
                        string destFullPath = Path.Combine(destDir, path);
                        string[] files = Directory.GetFiles(Path.GetDirectoryName(destFullPath), Path.GetFileName(destFullPath), SearchOption.AllDirectories);
                        Assert.IsTrue(0 < files.Length);
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

        #region ExtractProgress
        [TestMethod]
        [TestCategory("WimLib")]
        public void ExtractProgress()
        {
            ExtractProgress_Template("XPRESS.wim", @"\ACDE.txt");
            ExtractProgress_Template("LZX.wim", @"\ABCD\*.txt");
            ExtractProgress_Template("LZMS.wim", @"\ABDE\Z\Y.ini");
            ExtractProgress_Template("BootXPRESS.wim", @"\?CDE.txt");
            ExtractProgress_Template("BootLZX.wim", @"\ACDE.txt");
        }

        public WimLibCallbackStatus ExtractProgress_Callback(WimLibProgressMsg msg, object info, object progctx)
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
            return WimLibCallbackStatus.CONTINUE;
        }

        public void ExtractProgress_Template(string fileName, string target)
        {
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                CallbackTested tested = new CallbackTested(false);

                string wimFile = Path.Combine(TestSetup.BaseDir, "Samples", fileName);
                using (Wim wim = Wim.OpenWim(wimFile, WimLibOpenFlags.DEFAULT, ExtractProgress_Callback, tested))
                {
                    wim.ExtractPath(1, destDir, target, WimLibExtractFlags.GLOB_PATHS);
                }

                Assert.IsTrue(tested.Value);

                target = target.TrimStart('\\');
                if (target.IndexOfAny(new char[] { '*', '?' }) == -1)
                { // No wlidcard
                    Assert.IsTrue(File.Exists(Path.Combine(destDir, target)));
                }
                else
                { // With wildcard
                    string destFullPath = Path.Combine(destDir, target);
                    string[] files = Directory.GetFiles(Path.GetDirectoryName(destFullPath), Path.GetFileName(destFullPath), SearchOption.AllDirectories);
                    Assert.IsTrue(0 < files.Length);
                }
            }
            finally
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
            }
        }
        #endregion

        #region ExtractList
        [TestMethod]
        [TestCategory("WimLib")]
        public void ExtractList()
        {
            string[] paths = new string[] { @"\ACDE.txt", @"\ABCD\*.txt", @"\?CDE.txt" };
            ExtractList_Template("XPRESS.wim", paths);
            ExtractList_Template("LZX.wim", paths);
            ExtractList_Template("LZMS.wim", paths);
            ExtractList_Template("BootXPRESS.wim", paths);
            ExtractList_Template("BootLZX.wim", paths);
        }

        public void ExtractList_Template(string fileName, string[] paths)
        {
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                Directory.CreateDirectory(destDir);

                string listFile = Path.Combine(destDir, "ListFile.txt");
                using (StreamWriter w = new StreamWriter(listFile, false, Encoding.Unicode))
                {
                    foreach (string path in paths)
                        w.WriteLine(path);
                }

                string wimFile = Path.Combine(TestSetup.BaseDir, "Samples", fileName);
                using (Wim wim = Wim.OpenWim(wimFile, WimLibOpenFlags.DEFAULT))
                {
                    wim.ExtractPathList(1, destDir, listFile, WimLibExtractFlags.GLOB_PATHS);
                }

                foreach (string path in paths.Select(x => x.TrimStart('\\')))
                {
                    if (path.IndexOfAny(new char[] { '*', '?' }) == -1)
                    { // No wlidcard
                        Assert.IsTrue(File.Exists(Path.Combine(destDir, path)));
                    }
                    else
                    { // With wildcard
                        string destFullPath = Path.Combine(destDir, path);
                        string[] files = Directory.GetFiles(Path.GetDirectoryName(destFullPath), Path.GetFileName(destFullPath), SearchOption.AllDirectories);
                        Assert.IsTrue(0 < files.Length);
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
    }
}
