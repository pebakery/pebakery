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
    public class DeleteTests
    {
        #region DeleteImage
        [TestMethod]
        [TestCategory("WimLib")]
        public void DeleteImage()
        {
            DeleteImage_Template("MultiImage.wim", 1, "Base");
            DeleteImage_Template("MultiImage.wim", 2, "Changes");
            DeleteImage_Template("MultiImage.wim", 3, "Delta");
        }

        public void DeleteImage_Template(string wimFileName, int deleteIndex, string deleteImageName)
        {
            string srcWim = Path.Combine(TestSetup.SampleDir, wimFileName);
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string destWim = Path.Combine(destDir, wimFileName);
            try
            {
                Directory.CreateDirectory(destDir);
                File.Copy(srcWim, destWim, true);

                bool[] _checked = new bool[2];
                for (int i = 0; i < _checked.Length; i++)
                    _checked[i] = false;
                CallbackStatus ProgressCallback(ProgressMsg msg, object info, object progctx)
                {
                    switch (msg)
                    {
                        case ProgressMsg.WRITE_STREAMS:
                            {
                                ProgressInfo_WriteStreams m = (ProgressInfo_WriteStreams)info;
                                Assert.IsNotNull(m);

                                Assert.AreEqual(m.CompressionType, CompressionType.LZX);
                                _checked[0] = true;
                            }
                            break;
                        case ProgressMsg.RENAME:
                            {
                                ProgressInfo_Rename m = (ProgressInfo_Rename)info;
                                Assert.IsNotNull(m);

                                Assert.IsNotNull(m.From);
                                Assert.IsNotNull(m.To);
                                _checked[1] = true;
                            }
                            break;
                    }
                    return CallbackStatus.CONTINUE;
                }

                using (Wim wim = Wim.OpenWim(destWim, OpenFlags.WRITE_ACCESS))
                {
                    wim.RegisterCallback(ProgressCallback);

                    WimInfo swi = wim.GetWimInfo();
                    Assert.IsTrue(swi.ImageCount == 3);

                    string imageName = wim.GetImageName(deleteIndex);
                    Assert.IsTrue(imageName.Equals(deleteImageName, StringComparison.Ordinal));

                    wim.DeleteImage(deleteIndex);
                    wim.Overwrite(WriteFlags.DEFAULT, Wim.DefaultThreads);

                    for (int i = 0; i < _checked.Length; i++)
                        _checked[i] = false;

                    WimInfo dwi = wim.GetWimInfo();
                    Assert.IsTrue(dwi.ImageCount == 2);
                    for (int i = 1; i <= dwi.ImageCount; i++)
                    {
                        imageName = wim.GetImageName(i);
                        Assert.IsFalse(imageName.Equals(deleteImageName, StringComparison.Ordinal));
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

        #region DeletePath
        [TestMethod]
        [TestCategory("WimLib")]
        public void DeletePath()
        {
            DeletePath_Template("XPRESS.wim", "ACDE.txt");
            DeletePath_Template("LZX.wim", "ABCD");
            DeletePath_Template("LZMS.wim", "ABDE");
        }

        public void DeletePath_Template(string wimFileName, string deletePath)
        {
            string srcWim = Path.Combine(TestSetup.SampleDir, wimFileName);
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string destWim = Path.Combine(destDir, wimFileName);
            try
            {
                Directory.CreateDirectory(destDir);
                File.Copy(srcWim, destWim, true);

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

                using (Wim wim = Wim.OpenWim(destWim, OpenFlags.WRITE_ACCESS))
                {
                    wim.RegisterCallback(ProgressCallback);

                    Assert.IsTrue(wim.PathExists(1, deletePath));

                    wim.DeletePath(1, deletePath, DeleteFlags.RECURSIVE);
                    wim.Overwrite(WriteFlags.DEFAULT, Wim.DefaultThreads);

                    for (int i = 0; i < _checked.Length; i++)
                        Assert.IsTrue(_checked[i]);

                    Assert.IsFalse(wim.PathExists(1, deletePath));
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
