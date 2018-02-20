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
    public class ExportTests
    {
        #region ExportImage
        [TestMethod]
        [TestCategory("WimLib")]
        public void ExportImage()
        {
            ExportImage_Template("MultiImage.wim", 1, "Base");
            ExportImage_Template("MultiImage.wim", 2, "Changes");
            ExportImage_Template("MultiImage.wim", 3, "Delta");
        }

        public void ExportImage_Template(string wimFileName, int imageIndex, string destImageName)
        {
            string srcWimPath = Path.Combine(TestSetup.SampleDir, wimFileName);
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string destWimPath = Path.Combine(destDir, wimFileName);
            try
            {
                Directory.CreateDirectory(destDir);

                bool[] _checked = new bool[3];
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

                                Assert.AreEqual(m.CompressionType, CompressionType.LZMS);
                                _checked[0] = true;
                            }
                            break;
                        case ProgressMsg.WRITE_METADATA_BEGIN:
                            Assert.IsNull(info);
                            _checked[1] = true;
                            break;
                        case ProgressMsg.WRITE_METADATA_END:
                            Assert.IsNull(info);
                            _checked[2] = true;
                            break;
                    }
                    return CallbackStatus.CONTINUE;
                }

                using (Wim srcWim = Wim.OpenWim(srcWimPath, OpenFlags.DEFAULT))
                {
                    WimInfo swi = srcWim.GetWimInfo();
                    Assert.IsTrue(swi.ImageCount == 3);

                    string imageName = srcWim.GetImageName(imageIndex);
                    Assert.IsTrue(imageName.Equals(destImageName, StringComparison.Ordinal));

                    using (Wim destWim = Wim.CreateNewWim(CompressionType.LZMS))
                    {
                        destWim.RegisterCallback(ProgressCallback);

                        srcWim.ExportImage(imageIndex, destWim, destImageName, null, ExportFlags.GIFT);
                        destWim.Write(destWimPath, Wim.AllImages, WriteFlags.DEFAULT, Wim.DefaultThreads);
                    }

                    for (int i = 0; i < _checked.Length; i++)
                        _checked[i] = false;   
                }

                using (Wim destWim = Wim.OpenWim(destWimPath, OpenFlags.DEFAULT))
                {
                    WimInfo dwi = destWim.GetWimInfo();
                    Assert.IsTrue(dwi.ImageCount == 1);
                    string imageName = destWim.GetImageName(1);
                    Assert.IsTrue(imageName.Equals(destImageName, StringComparison.Ordinal));
                }

                long srcSize = new FileInfo(srcWimPath).Length;
                long destSize = new FileInfo(destWimPath).Length;
                Assert.IsTrue(destSize < srcSize);
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
