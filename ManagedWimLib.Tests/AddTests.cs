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
    public class AddTests
    {
        #region AddEmptyImage
        [TestMethod]
        [TestCategory("WimLib")]
        public void AddEmptyImage()
        {
            AddEmptyImage_Template(CompressionType.XPRESS, "XPRESS.wim");
        }

        public void AddEmptyImage_Template(CompressionType compType, string wimFileName, AddFlags addFlags = AddFlags.DEFAULT)
        {
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                Directory.CreateDirectory(destDir);

                bool[] _checked = new bool[2];
                for (int i = 0; i < _checked.Length; i++)
                    _checked[i] = false;
                CallbackStatus ProgressCallback(ProgressMsg msg, object info, object progctx)
                {
                    switch (msg)
                    {
                        case ProgressMsg.WRITE_METADATA_BEGIN:
                            Assert.IsNull(info);
                            _checked[0] = true;
                            break;
                        case ProgressMsg.WRITE_METADATA_END:
                            Assert.IsNull(info);
                            _checked[1] = true;
                            break;
                    }
                    return CallbackStatus.CONTINUE;
                }

                // Capture Wim
                string wimFile = Path.Combine(destDir, wimFileName);
                using (Wim wim = Wim.CreateNewWim(compType))
                {
                    wim.RegisterCallback(ProgressCallback);
                    wim.AddEmptyImage("UnitTest");
                    wim.Write(wimFile, Wim.AllImages, WriteFlags.DEFAULT, Wim.DefaultThreads);

                    WimInfo wi = wim.GetWimInfo();
                    Assert.IsTrue(wi.ImageCount == 1);
                }

                for (int i = 0; i < _checked.Length; i++)
                    Assert.IsTrue(_checked[i]);
            }
            finally
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
            }
        }
        #endregion

        #region AddImage
        [TestMethod]
        [TestCategory("WimLib")]
        public void AddImage()
        {
            AddImage_Template("NONE.wim", CompressionType.NONE);
            AddImage_Template("XPRESS.wim", CompressionType.XPRESS);
            AddImage_Template("LZX.wim", CompressionType.LZX);
            AddImage_Template("LZMS.wim", CompressionType.LZMS);

            AddImage_Template("NONE.wim", CompressionType.NONE, AddFlags.BOOT);
            AddImage_Template("XPRESS.wim", CompressionType.XPRESS, AddFlags.BOOT);
            AddImage_Template("LZX.wim", CompressionType.LZX,  AddFlags.BOOT);
            AddImage_Template("LZMS.wim", CompressionType.LZMS, AddFlags.BOOT);
        }

        public void AddImage_Template(string wimFileName, CompressionType compType, AddFlags addFlags = AddFlags.DEFAULT)
        {
            string srcDir = Path.Combine(TestSetup.SampleDir, "Src01");
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string wimFile = Path.Combine(destDir, wimFileName);
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
                        case ProgressMsg.SCAN_BEGIN:
                            {
                                ProgressInfo_Scan m = (ProgressInfo_Scan)info;
                                Assert.IsNotNull(info);

                                _checked[0] = true;
                            }
                            break;
                        case ProgressMsg.SCAN_END:
                            {
                                ProgressInfo_Scan m = (ProgressInfo_Scan)info;
                                Assert.IsNotNull(info);

                                _checked[1] = true;
                            }
                            break;
                        case ProgressMsg.WRITE_METADATA_BEGIN:
                            Assert.IsNull(info);
                            _checked[2] = true;
                            break;
                        case ProgressMsg.WRITE_STREAMS:
                            {
                                ProgressInfo_WriteStreams m = (ProgressInfo_WriteStreams)info;
                                Assert.IsNotNull(m);

                                _checked[3] = true;
                            }
                            break;
                        case ProgressMsg.WRITE_METADATA_END:
                            Assert.IsNull(info);
                            _checked[4] = true;
                            break;
                    }
                    return CallbackStatus.CONTINUE;
                }

                using (Wim wim = Wim.CreateNewWim(compType))
                {
                    wim.RegisterCallback(ProgressCallback);
                    wim.AddImage(srcDir, "UnitTest", null, addFlags);
                    wim.Write(wimFile, Wim.AllImages, WriteFlags.DEFAULT, Wim.DefaultThreads);

                    WimInfo wi = wim.GetWimInfo();
                    Assert.IsTrue(wi.ImageCount == 1);
                }

                Assert.IsTrue(_checked.All(x => x));

                TestHelper.CheckWimPath(SampleSet.Src01, wimFile);
            }
            finally
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
            }
        }
        #endregion

        #region AddImageMultiSource
        [TestMethod]
        [TestCategory("WimLib")]
        public void AddImageMultiSource()
        {
            AddImageMultiSource_Template(CompressionType.NONE, "NONE.wim");
            AddImageMultiSource_Template(CompressionType.XPRESS, "XPRESS.wim");
            AddImageMultiSource_Template(CompressionType.LZX, "LZX.wim");
            AddImageMultiSource_Template(CompressionType.LZMS, "LZMS.wim");
        }

        public void AddImageMultiSource_Template(CompressionType compType, string wimFileName, AddFlags addFlags = AddFlags.DEFAULT)
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
                        case ProgressMsg.SCAN_BEGIN:
                            {
                                ProgressInfo_Scan m = (ProgressInfo_Scan)info;
                                Assert.IsNotNull(info);

                                _checked[0] = true;
                            }
                            break;
                        case ProgressMsg.SCAN_END:
                            {
                                ProgressInfo_Scan m = (ProgressInfo_Scan)info;
                                Assert.IsNotNull(info);

                                _checked[1] = true;
                            }
                            break;
                        case ProgressMsg.WRITE_METADATA_BEGIN:
                            Assert.IsNull(info);
                            _checked[2] = true;
                            break;
                        case ProgressMsg.WRITE_STREAMS:
                            {
                                ProgressInfo_WriteStreams m = (ProgressInfo_WriteStreams)info;
                                Assert.IsNotNull(m);

                                _checked[3] = true;
                            }
                            break;
                        case ProgressMsg.WRITE_METADATA_END:
                            Assert.IsNull(info);
                            _checked[4] = true;
                            break;
                    }
                    return CallbackStatus.CONTINUE;
                }

                string srcDir1 = Path.Combine(TestSetup.SampleDir, "Src01");
                string srcDir3 = Path.Combine(TestSetup.SampleDir, "Src03");
                string wimFile = Path.Combine(destDir, wimFileName);
                using (Wim wim = Wim.CreateNewWim(compType))
                {
                    wim.RegisterCallback(ProgressCallback);
                    
                    CaptureSource[] srcs = new CaptureSource[]
                    {
                        new CaptureSource(srcDir1, @"\A"),
                        new CaptureSource(srcDir3, @"\Z"),
                    };
                    
                    wim.AddImageMultiSource(srcs, "UnitTest", null, addFlags);
                    wim.Write(wimFile, Wim.AllImages, WriteFlags.DEFAULT, Wim.DefaultThreads);

                    WimInfo wi = wim.GetWimInfo();
                    Assert.IsTrue(wi.ImageCount == 1);
                    Assert.IsTrue(wim.DirExists(1, "A"));
                    Assert.IsTrue(wim.DirExists(1, "Z"));
                }

                for (int i = 0; i < _checked.Length; i++)
                    Assert.IsTrue(_checked[i]);
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
