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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ManagedWimLib.Tests
{
    [TestClass]
    public class VerifyTests
    {
        #region Verify
        [TestMethod]
        [TestCategory("WimLib")]
        public void Verify()
        {
            Verify_Template("VerifySuccess.wim", true);
            Verify_Template("VerifyFail.wim", false);
            VerifySplit_Template("Split.swm", "Split*.swm", true);
        }

        public void Verify_Template(string wimFileName, bool result)
        {
            string wimFile = Path.Combine(TestSetup.SampleDir, wimFileName);

            bool[] _checked = new bool[3];
            for (int i = 0; i < _checked.Length; i++)
                _checked[i] = false;
            CallbackStatus ProgressCallback(ProgressMsg msg, object info, object progctx)
            {
                switch (msg)
                {
                    case ProgressMsg.BEGIN_VERIFY_IMAGE:
                        {
                            ProgressInfo_VerifyImage m = (ProgressInfo_VerifyImage)info;
                            Assert.IsNotNull(info);

                            _checked[0] = true;
                        }
                        break;
                    case ProgressMsg.END_VERIFY_IMAGE:
                        {
                            ProgressInfo_VerifyImage m = (ProgressInfo_VerifyImage)info;
                            Assert.IsNotNull(info);

                            _checked[1] = true;
                        }
                        break;
                    case ProgressMsg.VERIFY_STREAMS:
                        {
                            ProgressInfo_VerifyStreams m = (ProgressInfo_VerifyStreams)info;
                            Assert.IsNotNull(info);

                            _checked[2] = true;
                        }
                        break;
                }
                return CallbackStatus.CONTINUE;
            }

            try
            {
                using (Wim wim = Wim.OpenWim(wimFile, OpenFlags.DEFAULT))
                {
                    wim.RegisterCallback(ProgressCallback);

                    wim.VerifyWim();
                }
            }
            catch (WimLibException)
            {
                if (result)
                    Assert.Fail();
                else
                    return;
            }

            Assert.IsTrue(_checked.All(x => x));
        }

        public void VerifySplit_Template(string wimFileName, string splitWildcard, bool result)
        {
            string wimFile = Path.Combine(TestSetup.SampleDir, wimFileName);
            string splitWimFiles = Path.Combine(TestSetup.SampleDir, splitWildcard);

            bool[] _checked = new bool[3];
            for (int i = 0; i < _checked.Length; i++)
                _checked[i] = false;
            CallbackStatus ProgressCallback(ProgressMsg msg, object info, object progctx)
            {
                switch (msg)
                {
                    case ProgressMsg.BEGIN_VERIFY_IMAGE:
                    {
                        ProgressInfo_VerifyImage m = (ProgressInfo_VerifyImage)info;
                        Assert.IsNotNull(info);

                        _checked[0] = true;
                    }
                        break;
                    case ProgressMsg.END_VERIFY_IMAGE:
                    {
                        ProgressInfo_VerifyImage m = (ProgressInfo_VerifyImage)info;
                        Assert.IsNotNull(info);

                        _checked[1] = true;
                    }
                        break;
                    case ProgressMsg.VERIFY_STREAMS:
                        {
                            ProgressInfo_VerifyStreams m = (ProgressInfo_VerifyStreams)info;
                            Assert.IsNotNull(info);

                            _checked[2] = true;
                        }
                        break;
                }
                return CallbackStatus.CONTINUE;
            }

            try
            {
                using (Wim wim = Wim.OpenWim(wimFile, OpenFlags.DEFAULT))
                {
                    wim.RegisterCallback(ProgressCallback);

                    wim.ReferenceResourceFile(splitWimFiles, RefFlags.GLOB_ENABLE | RefFlags.GLOB_ERR_ON_NOMATCH, OpenFlags.DEFAULT);

                    wim.VerifyWim();
                }
            }
            catch (WimLibException)
            {
                if (result)
                    Assert.Fail();
                else
                    return;
            }

            Assert.IsTrue(_checked.All(x => x));
        }
        #endregion
    }
}
