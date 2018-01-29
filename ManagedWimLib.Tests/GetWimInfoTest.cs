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
    public class GetWimInfoTest
    {
        #region GetWimInfo
        [TestMethod]
        [TestCategory("WimLib")]
        public void GetWimInfo()
        {
            GetWimInfo_Template("XPRESS.wim", WimLibCompressionType.XPRESS, false);
            GetWimInfo_Template("LZX.wim", WimLibCompressionType.LZX, false);
            GetWimInfo_Template("LZMS.wim", WimLibCompressionType.LZMS, false);
            GetWimInfo_Template("BootXPRESS.wim", WimLibCompressionType.XPRESS, true);
            GetWimInfo_Template("BootLZX.wim", WimLibCompressionType.LZX, true);
        }

        public void GetWimInfo_Template(string fileName, WimLibCompressionType compType, bool boot)
        {
            string wimFile = Path.Combine(TestSetup.BaseDir, "Samples", fileName);
            using (Wim wim = Wim.OpenWim(wimFile, WimLibOpenFlags.DEFAULT))
            {
                WimInfo info = wim.GetWimInfo();

                if (boot)
                    Assert.IsTrue(info.BootIndex == 1);
                else
                    Assert.IsTrue(info.BootIndex == 0);
                Assert.IsTrue(info.ImageCount == 1);
                Assert.IsTrue(info.CompressionType == compType);
            }
        }
        #endregion
    }
}
