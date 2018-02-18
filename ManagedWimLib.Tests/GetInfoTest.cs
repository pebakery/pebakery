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
    public class GetInfoTest
    {
        #region GetImageInfo
        [TestMethod]
        [TestCategory("WimLib")]
        public void GetImageInfo()
        {
            GetImageInfo_Template("MultiImage.wim", 1, "Base", null);
            GetImageInfo_Template("MultiImage.wim", 2, "Changes", null);
            GetImageInfo_Template("MultiImage.wim", 3, "Delta", null);
        }

        public void GetImageInfo_Template(string wimFileName, int imageIndex, string imageName, string imageDesc)
        {
            string wimFile = Path.Combine(TestSetup.SampleDir, wimFileName);

            using (Wim wim = Wim.OpenWim(wimFile, OpenFlags.DEFAULT))
            {
                Assert.IsTrue(imageName.Equals(wim.GetImageName(imageIndex), StringComparison.Ordinal));
                Assert.IsNull(wim.GetImageDescription(imageIndex));
                Assert.IsNull(wim.GetImageProperty(imageIndex, "DESCRIPTION"));
            }
        }
        #endregion

        #region GetWimInfo
        [TestMethod]
        [TestCategory("WimLib")]
        public void GetWimInfo()
        {
            GetWimInfo_Template("XPRESS.wim", CompressionType.XPRESS, false);
            GetWimInfo_Template("LZX.wim", CompressionType.LZX, false);
            GetWimInfo_Template("LZMS.wim", CompressionType.LZMS, false);
            GetWimInfo_Template("BootXPRESS.wim", CompressionType.XPRESS, true);
            GetWimInfo_Template("BootLZX.wim", CompressionType.LZX, true);
        }

        public void GetWimInfo_Template(string fileName, CompressionType compType, bool boot)
        {
            string wimFile = Path.Combine(TestSetup.SampleDir, fileName);
            using (Wim wim = Wim.OpenWim(wimFile, OpenFlags.DEFAULT))
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

        #region GetWimInfo
        [TestMethod]
        [TestCategory("WimLib")]
        public void GetXmlData()
        {
            GetXmlData_Template("XPRESS.wim", @"<WIM><IMAGE INDEX=""1""><NAME>Sample</NAME><DIRCOUNT>5</DIRCOUNT><FILECOUNT>10</FILECOUNT><TOTALBYTES>13</TOTALBYTES><HARDLINKBYTES>0</HARDLINKBYTES><CREATIONTIME><HIGHPART>0x01D386AC</HIGHPART><LOWPART>0xFC4DC1F4</LOWPART></CREATIONTIME><LASTMODIFICATIONTIME><HIGHPART>0x01D386AC</HIGHPART><LOWPART>0xFC4E2202</LOWPART></LASTMODIFICATIONTIME></IMAGE><TOTALBYTES>1411</TOTALBYTES></WIM>");
            GetXmlData_Template("LZX.wim", @"<WIM><IMAGE INDEX=""1""><NAME>Sample</NAME><DIRCOUNT>5</DIRCOUNT><FILECOUNT>10</FILECOUNT><TOTALBYTES>13</TOTALBYTES><HARDLINKBYTES>0</HARDLINKBYTES><CREATIONTIME><HIGHPART>0x01D386AD</HIGHPART><LOWPART>0x036C2DDA</LOWPART></CREATIONTIME><LASTMODIFICATIONTIME><HIGHPART>0x01D386AD</HIGHPART><LOWPART>0x036C6899</LOWPART></LASTMODIFICATIONTIME></IMAGE><TOTALBYTES>1239</TOTALBYTES></WIM>");
            GetXmlData_Template("LZMS.wim", @"<WIM><IMAGE INDEX=""1""><NAME>Sample</NAME><DIRCOUNT>5</DIRCOUNT><FILECOUNT>10</FILECOUNT><TOTALBYTES>13</TOTALBYTES><HARDLINKBYTES>0</HARDLINKBYTES><CREATIONTIME><HIGHPART>0x01D386AD</HIGHPART><LOWPART>0x0A9BB0B2</LOWPART></CREATIONTIME><LASTMODIFICATIONTIME><HIGHPART>0x01D386AD</HIGHPART><LOWPART>0x0A9C1288</LOWPART></LASTMODIFICATIONTIME></IMAGE><TOTALBYTES>1177</TOTALBYTES></WIM>");
            GetXmlData_Template("MultiImage.wim", @"<WIM><IMAGE INDEX=""1""><NAME>Base</NAME><DIRCOUNT>2</DIRCOUNT><FILECOUNT>3</FILECOUNT><TOTALBYTES>3</TOTALBYTES><HARDLINKBYTES>0</HARDLINKBYTES><CREATIONTIME><HIGHPART>0x01D3A74E</HIGHPART><LOWPART>0x97C28976</LOWPART></CREATIONTIME><LASTMODIFICATIONTIME><HIGHPART>0x01D3A74E</HIGHPART><LOWPART>0x97C5056B</LOWPART></LASTMODIFICATIONTIME></IMAGE><IMAGE INDEX=""2""><NAME>Changes</NAME><DIRCOUNT>2</DIRCOUNT><FILECOUNT>3</FILECOUNT><TOTALBYTES>3</TOTALBYTES><HARDLINKBYTES>0</HARDLINKBYTES><CREATIONTIME><HIGHPART>0x01D3A74E</HIGHPART><LOWPART>0xBC468ECE</LOWPART></CREATIONTIME><LASTMODIFICATIONTIME><HIGHPART>0x01D3A74E</HIGHPART><LOWPART>0xBC470453</LOWPART></LASTMODIFICATIONTIME></IMAGE><IMAGE INDEX=""3""><NAME>Delta</NAME><DIRCOUNT>2</DIRCOUNT><FILECOUNT>4</FILECOUNT><TOTALBYTES>4</TOTALBYTES><HARDLINKBYTES>0</HARDLINKBYTES><CREATIONTIME><HIGHPART>0x01D3A74E</HIGHPART><LOWPART>0xC58E4622</LOWPART></CREATIONTIME><LASTMODIFICATIONTIME><HIGHPART>0x01D3A74E</HIGHPART><LOWPART>0xC58E947B</LOWPART></LASTMODIFICATIONTIME></IMAGE><TOTALBYTES>4332</TOTALBYTES></WIM>");
        }

        public void GetXmlData_Template(string fileName, string compXml)
        {
            string wimFile = Path.Combine(TestSetup.SampleDir, fileName);
            using (Wim wim = Wim.OpenWim(wimFile, OpenFlags.DEFAULT))
            {
                string wimXml = wim.GetXmlData();

                Assert.IsTrue(wimXml.Equals(compXml, StringComparison.InvariantCulture));
            }
        }
        #endregion
    }
}
