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
        #region ExtractImage
        [TestMethod]
        [TestCategory("WimLib")]
        public void ExtractImage()
        {
            ExtractImage_Template("XPRESS.wim");
            ExtractImage_Template("LZX.wim");
            ExtractImage_Template("LZMS.wim");
        }

        public void ExtractImage_Template(string wimFileName)
        {
            string wimFile = Path.Combine(TestSetup.SampleDir, wimFileName);
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                Directory.CreateDirectory(destDir);

                bool[] _checked = new bool[5];
                for (int i = 0; i < _checked.Length; i++)
                    _checked[i] = false;
                CallbackStatus ProgressCallback(ProgressMsg msg, object info, object progctx)
                {
                    switch (msg)
                    {
                        case ProgressMsg.EXTRACT_IMAGE_BEGIN:
                            {
                                ProgressInfo_Extract m = (ProgressInfo_Extract)info;
                                Assert.IsNotNull(m);

                                Assert.IsTrue(m.ImageName.Equals("Sample", StringComparison.Ordinal));
                                _checked[0] = true;
                            }
                            break;
                        case ProgressMsg.EXTRACT_IMAGE_END:
                            {
                                ProgressInfo_Extract m = (ProgressInfo_Extract)info;
                                Assert.IsNotNull(m);

                                Assert.IsTrue(m.ImageName.Equals("Sample", StringComparison.Ordinal));
                                _checked[1] = true;
                            }
                            break;
                        case ProgressMsg.EXTRACT_FILE_STRUCTURE:
                            {
                                ProgressInfo_Extract m = (ProgressInfo_Extract)info;
                                Assert.IsNotNull(m);

                                Assert.IsTrue(m.ImageName.Equals("Sample", StringComparison.Ordinal));
                                _checked[2] = true;
                            }
                            break;
                        case ProgressMsg.EXTRACT_STREAMS:
                            {
                                ProgressInfo_Extract m = (ProgressInfo_Extract)info;
                                Assert.IsNotNull(m);

                                Assert.IsTrue(m.ImageName.Equals("Sample", StringComparison.Ordinal));
                                _checked[3] = true;
                            }
                            break;
                        case ProgressMsg.EXTRACT_METADATA:
                            {
                                ProgressInfo_Extract m = (ProgressInfo_Extract)info;
                                Assert.IsNotNull(m);

                                Assert.IsTrue(m.ImageName.Equals("Sample", StringComparison.Ordinal));
                                _checked[4] = true;
                            }
                            break;
                    }
                    return CallbackStatus.CONTINUE;
                }

                using (Wim wim = Wim.OpenWim(wimFile, OpenFlags.DEFAULT))
                {
                    wim.RegisterCallback(ProgressCallback);

                    WimInfo wi = wim.GetWimInfo();
                    Assert.IsTrue(wi.ImageCount == 1);

                    wim.ExtractImage(1, destDir, ExtractFlags.DEFAULT);
                }

                Assert.IsTrue(_checked.All(x => x));

                TestHelper.CheckFileSystem(SampleSet.Src01, destDir);
            }
            finally
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
            }
        }
        #endregion

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
                string srcDir = Path.Combine(TestSetup.SampleDir);
                string wimFile = Path.Combine(srcDir, fileName);

                bool[] _checked = new bool[5];
                for (int i = 0; i < _checked.Length; i++)
                    _checked[i] = false;
                CallbackStatus ProgressCallback(ProgressMsg msg, object info, object progctx)
                {
                    switch (msg)
                    {
                        case ProgressMsg.EXTRACT_TREE_BEGIN:
                            {
                                ProgressInfo_Extract m = (ProgressInfo_Extract)info;
                                Assert.IsNotNull(m);

                                _checked[0] = true;
                            }
                            break;
                        case ProgressMsg.EXTRACT_TREE_END:
                            {
                                ProgressInfo_Extract m = (ProgressInfo_Extract)info;
                                Assert.IsNotNull(m);

                                _checked[1] = true;
                            }
                            break;
                        case ProgressMsg.EXTRACT_FILE_STRUCTURE:
                            {
                                ProgressInfo_Extract m = (ProgressInfo_Extract)info;
                                Assert.IsNotNull(m);

                                _checked[2] = true;
                            }
                            break;
                        case ProgressMsg.EXTRACT_STREAMS:
                            {
                                ProgressInfo_Extract m = (ProgressInfo_Extract)info;
                                Assert.IsNotNull(m);

                                _checked[3] = true;
                            }
                            break;
                        case ProgressMsg.EXTRACT_METADATA:
                            {
                                ProgressInfo_Extract m = (ProgressInfo_Extract)info;
                                Assert.IsNotNull(m);

                                _checked[4] = true;
                            }
                            break;
                    }
                    return CallbackStatus.CONTINUE;
                }

                using (Wim wim = Wim.OpenWim(wimFile, OpenFlags.DEFAULT))
                {
                    wim.RegisterCallback(ProgressCallback);

                    wim.ExtractPaths(1, destDir, paths, ExtractFlags.GLOB_PATHS);
                }

                Assert.IsTrue(_checked.All(x => x));

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

                bool[] _checked = new bool[5];
                for (int i = 0; i < _checked.Length; i++)
                    _checked[i] = false;
                CallbackStatus ProgressCallback(ProgressMsg msg, object info, object progctx)
                {
                    switch (msg)
                    {
                        case ProgressMsg.EXTRACT_TREE_BEGIN:
                            {
                                ProgressInfo_Extract m = (ProgressInfo_Extract)info;
                                Assert.IsNotNull(m);

                                _checked[0] = true;
                            }
                            break;
                        case ProgressMsg.EXTRACT_TREE_END:
                            {
                                ProgressInfo_Extract m = (ProgressInfo_Extract)info;
                                Assert.IsNotNull(m);

                                _checked[1] = true;
                            }
                            break;
                        case ProgressMsg.EXTRACT_FILE_STRUCTURE:
                            {
                                ProgressInfo_Extract m = (ProgressInfo_Extract)info;
                                Assert.IsNotNull(m);

                                _checked[2] = true;
                            }
                            break;
                        case ProgressMsg.EXTRACT_STREAMS:
                            {
                                ProgressInfo_Extract m = (ProgressInfo_Extract)info;
                                Assert.IsNotNull(m);

                                _checked[3] = true;
                            }
                            break;
                        case ProgressMsg.EXTRACT_METADATA:
                            {
                                ProgressInfo_Extract m = (ProgressInfo_Extract)info;
                                Assert.IsNotNull(m);

                                _checked[4] = true;
                            }
                            break;
                    }
                    return CallbackStatus.CONTINUE;
                }

                string listFile = Path.Combine(destDir, "ListFile.txt");
                using (StreamWriter w = new StreamWriter(listFile, false, Encoding.Unicode))
                {
                    foreach (string path in paths)
                        w.WriteLine(path);
                }

                string wimFile = Path.Combine(TestSetup.SampleDir, fileName);
                using (Wim wim = Wim.OpenWim(wimFile, OpenFlags.DEFAULT))
                {
                    wim.RegisterCallback(ProgressCallback);

                    wim.ExtractPathList(1, destDir, listFile, ExtractFlags.GLOB_PATHS);
                }

                Assert.IsTrue(_checked.All(x => x));

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
