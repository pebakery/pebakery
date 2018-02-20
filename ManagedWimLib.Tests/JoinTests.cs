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
    public class JoinTests
    {
        #region Join
        [TestMethod]
        [TestCategory("WimLib")]
        public void Join()
        {
            Join_Template(new string[] { "Split.swm", "Split2.swm" });
        }

        public void Join_Template(string[] splitWimNames)
        {
            string[] splitWims = splitWimNames.Select(x => Path.Combine(TestSetup.SampleDir, x)).ToArray();
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string destWim = Path.Combine(TestSetup.SampleDir, "Dest.wim");
            try
            {
                Directory.CreateDirectory(destDir);

                Wim.Join(splitWims, destWim, OpenFlags.DEFAULT, WriteFlags.DEFAULT);

                TestHelper.CheckWimPath(SampleSet.Src03, destWim);
            }
            finally
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
            }
        }
        #endregion

        #region JoinProgress
        [TestMethod]
        [TestCategory("WimLib")]
        public void JoinProgress()
        {
            JoinProgress_Template(new string[] { "Split.swm", "Split2.swm" });
        }

        public void JoinProgress_Template(string[] splitWimNames)
        {
            string[] splitWims = splitWimNames.Select(x => Path.Combine(TestSetup.SampleDir, x)).ToArray();
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string destWim = Path.Combine(TestSetup.SampleDir, "Dest.wim");
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
                        case ProgressMsg.WRITE_METADATA_BEGIN:
                            Assert.IsNull(info);
                            _checked[0] = true;
                            break;
                        case ProgressMsg.WRITE_METADATA_END:
                            Assert.IsNull(info);
                            _checked[1] = true;
                            break;
                        case ProgressMsg.WRITE_STREAMS:
                            {
                                ProgressInfo_WriteStreams m = (ProgressInfo_WriteStreams)info;
                                Assert.IsNotNull(m);

                                Assert.AreEqual(m.CompressionType, CompressionType.LZX);
                                _checked[2] = true;
                            }
                            break;
                    }
                    return CallbackStatus.CONTINUE;
                }

                Wim.Join(splitWims, destWim, OpenFlags.DEFAULT, WriteFlags.DEFAULT, ProgressCallback);

                Assert.IsTrue(_checked.All(x => x));

                TestHelper.CheckWimPath(SampleSet.Src03, destWim);
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
