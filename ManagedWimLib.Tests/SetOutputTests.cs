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

namespace ManagedWimLib.Tests
{
    [TestClass]
    public class SetOutputTests
    {
        #region SetOutputChunkSize
        [TestMethod]
        [TestCategory("WimLib")]
        public void SetOutputChunkSize()
        {
            SetOutputChunkSize_Template("XPRESS.wim", CompressionType.XPRESS, 16384, true);
            SetOutputChunkSize_Template("XPRESS.wim", CompressionType.XPRESS, 1024, false);
        }

        public void SetOutputChunkSize_Template(string wimFileName, CompressionType compType, uint chunkSize, bool success)
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

                // Capture Wim
                string srcDir = Path.Combine(TestSetup.SampleDir, "Src01");
                string wimFile = Path.Combine(destDir, wimFileName);
                using (Wim wim = Wim.CreateNewWim(compType))
                {
                    wim.RegisterCallback(ProgressCallback);
                    try
                    {
                        wim.SetOutputChunkSize(chunkSize);
                    }
                    catch (WimLibException)
                    {
                        if (success)
                            Assert.Fail();
                        else
                            return;
                    }

                    wim.AddImage(srcDir, "UnitTest", null, AddFlags.DEFAULT);
                    wim.Write(wimFile, Wim.AllImages, WriteFlags.DEFAULT, Wim.DefaultThreads);

                    WimInfo wi = wim.GetWimInfo();
                    Assert.IsTrue(wi.ImageCount == 1);
                }

                TestHelper.CheckWimPath(SampleSet.Src01, wimFile);
            }
            finally
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
            }
        }
        #endregion

        #region SetOutputPackChunkSize
        [TestMethod]
        [TestCategory("WimLib")]
        public void SetOutputPackChunkSize()
        {
            SetOutputPackChunkSize_Template("LZMS.wim", CompressionType.LZMS, 65536, true);
            SetOutputPackChunkSize_Template("LZMS.wim", CompressionType.LZMS, 1024, false);
        }

        public void SetOutputPackChunkSize_Template(string wimFileName, CompressionType compType, uint chunkSize, bool success)
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

                // Capture Wim
                string srcDir = Path.Combine(TestSetup.SampleDir, "Src01");
                string wimFile = Path.Combine(destDir, wimFileName);
                using (Wim wim = Wim.CreateNewWim(compType))
                {
                    wim.RegisterCallback(ProgressCallback);
                    try
                    {
                        wim.SetOutputPackChunkSize(chunkSize);
                    }
                    catch (WimLibException)
                    {
                        if (success)
                            Assert.Fail();
                        else
                            return;
                    }

                    wim.AddImage(srcDir, "UnitTest", null, AddFlags.DEFAULT);
                    wim.Write(wimFile, Wim.AllImages, WriteFlags.SOLID, Wim.DefaultThreads);

                    WimInfo wi = wim.GetWimInfo();
                    Assert.IsTrue(wi.ImageCount == 1);
                }

                TestHelper.CheckWimPath(SampleSet.Src01, wimFile);
            }
            finally
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
            }
        }
        #endregion

        #region SetOutputCompressionType
        [TestMethod]
        [TestCategory("WimLib")]
        public void SetOutputCompressionType()
        {
            SetOutputCompressionType_Template("XPRESS.wim", CompressionType.XPRESS);
            SetOutputCompressionType_Template("LZX.wim", CompressionType.LZX);
            SetOutputCompressionType_Template("LZMS.wim", CompressionType.LZMS);
        }

        public void SetOutputCompressionType_Template(string wimFileName, CompressionType compType)
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

                // Capture Wim
                string srcDir = Path.Combine(TestSetup.SampleDir, "Src01");
                string wimFile = Path.Combine(destDir, wimFileName);
                using (Wim wim = Wim.CreateNewWim(CompressionType.NONE))
                {
                    wim.RegisterCallback(ProgressCallback);
                    wim.SetOutputCompressionType(compType);

                    wim.AddImage(srcDir, "UnitTest", null, AddFlags.DEFAULT);
                    wim.Write(wimFile, Wim.AllImages, WriteFlags.DEFAULT, Wim.DefaultThreads);
                }

                using (Wim wim = Wim.OpenWim(wimFile, OpenFlags.DEFAULT))
                {
                    WimInfo wi = wim.GetWimInfo();
                    Assert.AreEqual(wi.ImageCount, 1u);
                    Assert.AreEqual(wi.CompressionType, compType);
                }

                TestHelper.CheckWimPath(SampleSet.Src01, wimFile);
            }
            finally
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
            }
        }
        #endregion

        #region SetOutputPackCompressionType
        [TestMethod]
        [TestCategory("WimLib")]
        public void SetOutputPackCompressionType()
        {
            SetOutputPackCompressionType_Template("XPRESS.wim", CompressionType.XPRESS);
            SetOutputPackCompressionType_Template("LZX.wim", CompressionType.LZX);
            SetOutputPackCompressionType_Template("LZMS.wim", CompressionType.LZMS);
        }

        public void SetOutputPackCompressionType_Template(string wimFileName, CompressionType compType)
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

                // Capture Wim
                string srcDir = Path.Combine(TestSetup.SampleDir, "Src01");
                string wimFile = Path.Combine(destDir, wimFileName);
                using (Wim wim = Wim.CreateNewWim(CompressionType.NONE))
                {
                    wim.RegisterCallback(ProgressCallback);
                    wim.SetOutputCompressionType(compType);
                    wim.SetOutputPackCompressionType(compType);

                    wim.AddImage(srcDir, "UnitTest", null, AddFlags.DEFAULT);
                    wim.Write(wimFile, Wim.AllImages, WriteFlags.SOLID, Wim.DefaultThreads);
                }

                using (Wim wim = Wim.OpenWim(wimFile, OpenFlags.DEFAULT))
                {
                    WimInfo wi = wim.GetWimInfo();
                    Assert.AreEqual(wi.ImageCount, 1u);
                    Assert.AreEqual(wi.CompressionType, compType);
                }

                TestHelper.CheckWimPath(SampleSet.Src01, wimFile);
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
