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
    public class SetInfoTests
    {
        #region SetImageInfo
        [TestMethod]
        [TestCategory("WimLib")]
        public void SetImageInfo()
        {
            SetImageInfo_Template("LZX.wim", 1);
            SetImageInfo_Template("MultiImage.wim", 2);
            SetImageInfo_Template("MultiImage.wim", 3);
        }

        public void SetImageInfo_Template(string wimFileName, int imageIndex)
        {
            string srcWim = Path.Combine(TestSetup.SampleDir, wimFileName);
            string destWim = Path.GetTempFileName();
            try
            {
                File.Copy(srcWim, destWim, true);

                using (Wim wim = Wim.OpenWim(destWim, OpenFlags.WRITE_ACCESS))
                {
                    string imageName = wim.GetImageName(imageIndex);
                    string imageDesc = wim.GetImageDescription(imageIndex);
                    string imageFlags = wim.GetImageProperty(imageIndex, "FLAGS");

                    Assert.IsNotNull(imageName);
                    Assert.IsNull(imageDesc);
                    Assert.IsNull(imageFlags);

                    wim.SetImageName(imageIndex, "NEW_IMAGE");
                    wim.SetImageDescription(imageIndex, "NEW_DESCRIPTION");
                    wim.SetImageFlags(imageIndex, "NEW_FLAGS");

                    Assert.IsFalse(imageName.Equals(wim.GetImageName(imageIndex), StringComparison.Ordinal));
                    Assert.IsTrue("NEW_IMAGE".Equals(wim.GetImageName(imageIndex), StringComparison.Ordinal));
                    Assert.IsTrue("NEW_DESCRIPTION".Equals(wim.GetImageDescription(imageIndex)));
                    Assert.IsTrue("NEW_FLAGS".Equals(wim.GetImageProperty(imageIndex, "FLAGS")));
                }
            }
            finally
            {
                if (File.Exists(destWim))
                    File.Delete(destWim);
            }
        }
        #endregion

        #region SetWimInfo
        [TestMethod]
        [TestCategory("WimLib")]
        public void SetWimInfo()
        {
            SetWimInfo_Template("MultiImage.wim", 2u);
        }

        public void SetWimInfo_Template(string wimFileName, uint bootIndex)
        {
            string srcWim = Path.Combine(TestSetup.SampleDir, wimFileName);
            string destWim = Path.GetTempFileName();
            try
            {
                File.Copy(srcWim, destWim, true);

                using (Wim wim = Wim.OpenWim(destWim, OpenFlags.WRITE_ACCESS))
                {
                    WimInfo info = new WimInfo
                    {
                        BootIndex = bootIndex,
                    };

                    wim.SetWimInfo(info, ChangeFlags.BOOT_INDEX);
                    wim.Overwrite(WriteFlags.DEFAULT, Wim.DefaultThreads);
                }

                using (Wim wim = Wim.OpenWim(destWim, OpenFlags.DEFAULT))
                {
                    WimInfo info = wim.GetWimInfo();

                    Assert.IsTrue(info.BootIndex == bootIndex);
                }
            }
            finally
            {
                if (File.Exists(destWim))
                    File.Delete(destWim);
            }
        }
        #endregion
    }
}


