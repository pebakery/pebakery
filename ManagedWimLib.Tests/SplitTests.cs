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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ManagedWimLib.Tests
{
    [TestClass]
    public class SplitTests
    {
        #region SplitImage
        [TestMethod]
        [TestCategory("WimLib")]
        public void Split()
        {
            Split_Template("Src03", (1024 + 512) * 1024);
        }

        public void Split_Template(string testSet, ulong partSize)
        {
            string srcDir = Path.Combine(TestSetup.SampleDir, testSet);
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string wimFile = Path.Combine(destDir, "LZX.wim");
            // Strange, using "%TEMP%\{Path.GetRandomFileName()}" folder for split wim's dest raises an error
            // Bug of wimlib. Until it is fixed, use just temp directory (so there should be no dot in path)
            // string splitWimFile = Path.Combine(destDir, "Split.swm");
            // string splitWildcard = Path.Combine(destDir, "Split*.swm");
            string splitWimFile = Path.Combine(Path.GetTempPath(), "Split.swm");
            string splitWildcard = Path.Combine(Path.GetTempPath(), "Split*.swm");

            // TroubleShoot : iztfjh2x.fty*.swm -> wimlib generates iztfjh2x2.fty.swm, instead of iztfjh2x.fty2.swm
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
                        case ProgressMsg.SPLIT_BEGIN_PART:
                            {
                                ProgressInfo_Split m = (ProgressInfo_Split)info;
                                Assert.IsNotNull(info);

                                _checked[0] = true;
                            }
                            break;
                        case ProgressMsg.SPLIT_END_PART:
                            {
                                ProgressInfo_Split m = (ProgressInfo_Split)info;
                                Assert.IsNotNull(info);

                                _checked[1] = true;
                            }
                            break;
                    }
                    return CallbackStatus.CONTINUE;
                }
               
                using (Wim wim = Wim.CreateNewWim(CompressionType.LZX))
                {
                    wim.AddImage(srcDir, "UnitTest", null, AddFlags.NO_ACLS);
                    wim.Write(wimFile, Wim.AllImages, WriteFlags.DEFAULT, Wim.DefaultThreads);
                }

                TestHelper.CheckWimPath(SampleSet.Src03, wimFile);

                using (Wim wim = Wim.OpenWim(wimFile, OpenFlags.DEFAULT, ProgressCallback))
                {
                    wim.Split(splitWimFile, partSize, WriteFlags.DEFAULT);
                }

                Assert.IsTrue(_checked.All(x => x));

                List<Tuple<string, bool>> entries = new List<Tuple<string, bool>>();
                CallbackStatus IterateCallback(DirEntry dentry, object userData)
                {
                    string path = dentry.FullPath;
                    bool isDir = (dentry.Attributes & FileAttribute.DIRECTORY) != 0;
                    entries.Add(new Tuple<string, bool>(path, isDir));

                    return CallbackStatus.CONTINUE;
                }

                using (Wim wim = Wim.OpenWim(splitWimFile, OpenFlags.DEFAULT))
                {
                    wim.ReferenceResourceFile(splitWildcard, RefFlags.GLOB_ENABLE | RefFlags.GLOB_ERR_ON_NOMATCH, OpenFlags.DEFAULT);

                    WimInfo wi = wim.GetWimInfo();
                    Assert.IsTrue(wi.ImageCount == 1);

                    wim.IterateDirTree(1, Wim.RootPath, IterateFlags.RECURSIVE, IterateCallback);
                }

                TestHelper.CheckPathList(SampleSet.Src03, entries);
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
