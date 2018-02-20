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
    public class RenameTests
    {
        #region enamePath
        [TestMethod]
        [TestCategory("WimLib")]
        public void RenamePath()
        {
            RenamePath_Template("XPRESS.wim", "ACDE.txt");
            RenamePath_Template("LZX.wim", "ABCD");
            RenamePath_Template("LZMS.wim", "ABDE");
        }

        public void RenamePath_Template(string wimFileName, string srcPath)
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

                    Assert.IsTrue(wim.PathExists(1, srcPath));

                    wim.RenamePath(1, srcPath, "REN");
                    wim.Overwrite(WriteFlags.DEFAULT, Wim.DefaultThreads);

                    Assert.IsTrue(_checked.All(x => x));

                    Assert.IsFalse(wim.PathExists(1, srcPath));
                    Assert.IsTrue(wim.PathExists(1, "REN"));
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
