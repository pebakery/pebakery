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
    public class IterateTests
    {
        #region IterateDirTree
        [TestMethod]
        [TestCategory("WimLib")]
        public void IterateDirTree()
        {
            IterateDirTree_Template("XPRESS.wim");
            IterateDirTree_Template("LZX.wim");
            IterateDirTree_Template("LZMS.wim");
        }

        public void IterateDirTree_Template(string wimFileName)
        {
            List<Tuple<string, bool>> entries = new List<Tuple<string, bool>>();

            CallbackStatus IterateCallback(DirEntry dentry, object userData)
            {
                string path = dentry.FullPath;
                bool isDir = (dentry.Attributes & FileAttribute.DIRECTORY) != 0;
                entries.Add(new Tuple<string, bool>(path, isDir));

                return CallbackStatus.CONTINUE;
            }

            string wimFile = Path.Combine(TestSetup.SampleDir, wimFileName);
            using (Wim wim = Wim.OpenWim(wimFile, OpenFlags.DEFAULT))
            {
                wim.IterateDirTree(1, Wim.RootPath, IterateFlags.RECURSIVE, IterateCallback);
            }

            TestHelper.CheckPathList(SampleSet.Src01, entries);
        }
        #endregion

        #region IterateLookupTable
        [TestMethod]
        [TestCategory("WimLib")]
        public void IterateLookupTable()
        {
            IterateLookupTable_Template("XPRESS.wim", false);
            IterateLookupTable_Template("LZX.wim", false);
            IterateLookupTable_Template("LZMS.wim", true);
        }

        public void IterateLookupTable_Template(string wimFileName, bool compSolid)
        {
            bool isSolid = false;
            CallbackStatus IterateCallback(ResourceEntry resource, object userData)
            {
                if (resource.Packed)
                    isSolid = true;

                return CallbackStatus.CONTINUE;
            }

            string wimFile = Path.Combine(TestSetup.SampleDir, wimFileName);
            using (Wim wim = Wim.OpenWim(wimFile, OpenFlags.DEFAULT))
            {
                wim.IterateLookupTable(IterateCallback);
            }

            Assert.AreEqual(compSolid, isSolid);
        }
        #endregion
    }
}
