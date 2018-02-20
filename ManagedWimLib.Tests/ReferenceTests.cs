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
    public class ReferenceTests
    {
        #region ReferenceTemplateImage
        [TestMethod]
        [TestCategory("WimLib")]
        public void ReferenceTemplateImage()
        {
            ReferenceTemplateImage_Template("MultiImage.wim", "Src02_2", SampleSet.Src02_2);
        }

        public void ReferenceTemplateImage_Template(string wimFileName, string captureDir, SampleSet set)
        {
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                Directory.CreateDirectory(destDir);

                string srcDir = Path.Combine(TestSetup.SampleDir, captureDir);
                string srcWimFile = Path.Combine(TestSetup.SampleDir, wimFileName);
                string destWimFile = Path.Combine(destDir, wimFileName);
                File.Copy(srcWimFile, destWimFile, true);

                int imageCount;
                using (Wim wim = Wim.OpenWim(destWimFile, OpenFlags.WRITE_ACCESS))
                {
                    WimInfo wi = wim.GetWimInfo();
                    imageCount = (int)wi.ImageCount;

                    wim.AddImage(srcDir, "UnitTest", null, AddFlags.DEFAULT);
                    wim.ReferenceTemplateImage(imageCount + 1, 1);

                    wim.Overwrite(WriteFlags.DEFAULT, Wim.DefaultThreads);
                }

                List<Tuple<string, bool>> entries = new List<Tuple<string, bool>>();

                CallbackStatus IterateCallback(DirEntry dentry, object userData)
                {
                    string path = dentry.FullPath;
                    bool isDir = (dentry.Attributes & FileAttribute.DIRECTORY) != 0;
                    entries.Add(new Tuple<string, bool>(path, isDir));

                    return CallbackStatus.CONTINUE;
                }

                string wimFile = Path.Combine(TestSetup.SampleDir, wimFileName);
                using (Wim wim = Wim.OpenWim(destWimFile, OpenFlags.DEFAULT))
                {
                    wim.IterateDirTree(imageCount + 1, Wim.RootPath, IterateFlags.RECURSIVE, IterateCallback);
                }

                TestHelper.CheckPathList(set, entries);
            }
            finally
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
            }
        }

        public void ReferenceTemplateImage_Template(string[] splitWimNames, RefFlags refFlags = RefFlags.DEFAULT, bool failure = false)
        {
            string[] splitWims = splitWimNames.Select(x => Path.Combine(TestSetup.SampleDir, x)).ToArray();
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                Directory.CreateDirectory(destDir);

                bool[] _checked = new bool[5];
                for (int i = 0; i < _checked.Length; i++)
                    _checked[i] = false;
                CallbackStatus ProgressCallback(ProgressMsg msg, object info, object progctx)
                {
                    switch (msg)
                    {
                        case ProgressMsg.EXTRACT_IMAGE_BEGIN:
                            {
                                ProgressInfo_Extract m = (ProgressInfo_Extract)info;
                                Assert.IsNotNull(m);

                                _checked[0] = true;
                            }
                            break;
                        case ProgressMsg.EXTRACT_IMAGE_END:
                            {
                                ProgressInfo_Extract m = (ProgressInfo_Extract)info;
                                Assert.IsNotNull(m);

                                _checked[1] = true;
                            }
                            break;
                        case ProgressMsg.EXTRACT_FILE_STRUCTURE:
                            {
                                ProgressInfo_Extract m = (ProgressInfo_Extract)info;
                                Assert.IsNotNull(m);

                                _checked[2] = true;
                            }
                            break;
                        case ProgressMsg.EXTRACT_STREAMS:
                            {
                                ProgressInfo_Extract m = (ProgressInfo_Extract)info;
                                Assert.IsNotNull(m);

                                _checked[3] = true;
                            }
                            break;
                        case ProgressMsg.EXTRACT_METADATA:
                            {
                                ProgressInfo_Extract m = (ProgressInfo_Extract)info;
                                Assert.IsNotNull(m);

                                _checked[4] = true;
                            }
                            break;
                    }
                    return CallbackStatus.CONTINUE;
                }

                try
                {
                    using (Wim wim = Wim.OpenWim(splitWims[0], OpenFlags.DEFAULT, ProgressCallback))
                    {
                        var leftSplitWims = splitWims.Skip(1);
                        wim.ReferenceResourceFiles(leftSplitWims, refFlags, OpenFlags.DEFAULT);

                        wim.ExtractImage(1, destDir, ExtractFlags.NO_ACLS);
                    }
                }
                catch (WimLibException)
                {
                    if (failure)
                        return;
                    else
                        Assert.Fail();
                }

                Assert.IsTrue(_checked.All(x => x));

                TestHelper.CheckFileSystem(SampleSet.Src03, destDir);
            }
            finally
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
            }
        }
        #endregion

        #region ReferenceResourceFiles
        [TestMethod]
        [TestCategory("WimLib")]
        public void ReferenceResourceFiles()
        {
            ReferenceResourceFiles_Template(new[] { "Split.swm", "Split2.swm" });
            ReferenceResourceFiles_Template(new[] { "Split.swm", "Split*.swm" }, RefFlags.GLOB_ENABLE | RefFlags.GLOB_ERR_ON_NOMATCH);
            ReferenceResourceFiles_Template(new[] { "Split.swm", "Split*.swm" }, RefFlags.GLOB_ENABLE | RefFlags.GLOB_ERR_ON_NOMATCH, true);
        }

        public void ReferenceResourceFiles_Template(string[] splitWimNames, RefFlags refFlags = RefFlags.DEFAULT, bool failure = false)
        {
            string[] splitWims = splitWimNames.Select(x => Path.Combine(TestSetup.SampleDir, x)).ToArray();
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                Directory.CreateDirectory(destDir);

                bool[] _checked = new bool[5];
                for (int i = 0; i < _checked.Length; i++)
                    _checked[i] = false;
                CallbackStatus ProgressCallback(ProgressMsg msg, object info, object progctx)
                {
                    switch (msg)
                    {
                        case ProgressMsg.EXTRACT_IMAGE_BEGIN:
                            {
                                ProgressInfo_Extract m = (ProgressInfo_Extract)info;
                                Assert.IsNotNull(m);

                                _checked[0] = true;
                            }
                            break;
                        case ProgressMsg.EXTRACT_IMAGE_END:
                            {
                                ProgressInfo_Extract m = (ProgressInfo_Extract)info;
                                Assert.IsNotNull(m);

                                _checked[1] = true;
                            }
                            break;
                        case ProgressMsg.EXTRACT_FILE_STRUCTURE:
                            {
                                ProgressInfo_Extract m = (ProgressInfo_Extract)info;
                                Assert.IsNotNull(m);

                                _checked[2] = true;
                            }
                            break;
                        case ProgressMsg.EXTRACT_STREAMS:
                            {
                                ProgressInfo_Extract m = (ProgressInfo_Extract)info;
                                Assert.IsNotNull(m);

                                _checked[3] = true;
                            }
                            break;
                        case ProgressMsg.EXTRACT_METADATA:
                            {
                                ProgressInfo_Extract m = (ProgressInfo_Extract)info;
                                Assert.IsNotNull(m);

                                _checked[4] = true;
                            }
                            break;
                    }
                    return CallbackStatus.CONTINUE;
                }

                try
                {
                    using (Wim wim = Wim.OpenWim(splitWims[0], OpenFlags.DEFAULT))
                    {
                        wim.RegisterCallback(ProgressCallback);

                        var leftSplitWims = splitWims.Skip(1);
                        wim.ReferenceResourceFiles(leftSplitWims, refFlags, OpenFlags.DEFAULT);

                        wim.ExtractImage(1, destDir, ExtractFlags.NO_ACLS);
                    }
                }
                catch (WimLibException)
                {
                    if (failure)
                        return;
                    else
                        Assert.Fail();
                }

                Assert.IsTrue(_checked.All(x => x));

                TestHelper.CheckFileSystem(SampleSet.Src03, destDir);
            }
            finally
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
            }
        }
        #endregion

        #region ReferenceResources
        [TestMethod]
        [TestCategory("WimLib")]
        public void ReferenceResources()
        {
            ReferenceResources_Template(new[] { "Split.swm", "Split2.swm" });
        }

        public void ReferenceResources_Template(string[] splitWimNames, RefFlags refFlags = RefFlags.DEFAULT, bool failure = false)
        {
            string[] splitWimPaths = splitWimNames.Select(x => Path.Combine(TestSetup.SampleDir, x)).ToArray();
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                Directory.CreateDirectory(destDir);

                bool[] _checked = new bool[5];
                for (int i = 0; i < _checked.Length; i++)
                    _checked[i] = false;
                CallbackStatus ProgressCallback(ProgressMsg msg, object info, object progctx)
                {
                    switch (msg)
                    {
                        case ProgressMsg.EXTRACT_IMAGE_BEGIN:
                            {
                                ProgressInfo_Extract m = (ProgressInfo_Extract)info;
                                Assert.IsNotNull(m);

                                _checked[0] = true;
                            }
                            break;
                        case ProgressMsg.EXTRACT_IMAGE_END:
                            {
                                ProgressInfo_Extract m = (ProgressInfo_Extract)info;
                                Assert.IsNotNull(m);

                                _checked[1] = true;
                            }
                            break;
                        case ProgressMsg.EXTRACT_FILE_STRUCTURE:
                            {
                                ProgressInfo_Extract m = (ProgressInfo_Extract)info;
                                Assert.IsNotNull(m);

                                _checked[2] = true;
                            }
                            break;
                        case ProgressMsg.EXTRACT_STREAMS:
                            {
                                ProgressInfo_Extract m = (ProgressInfo_Extract)info;
                                Assert.IsNotNull(m);

                                _checked[3] = true;
                            }
                            break;
                        case ProgressMsg.EXTRACT_METADATA:
                            {
                                ProgressInfo_Extract m = (ProgressInfo_Extract)info;
                                Assert.IsNotNull(m);

                                _checked[4] = true;
                            }
                            break;
                    }
                    return CallbackStatus.CONTINUE;
                }

                try
                {
                    using (Wim wim = Wim.OpenWim(splitWimPaths[0], OpenFlags.DEFAULT, ProgressCallback))
                    {
                        Wim[] splitWims = new Wim[splitWimPaths.Length - 1];
                        try
                        {
                            for (int i = 0; i < splitWims.Length; i++)
                                splitWims[i] = Wim.OpenWim(splitWimPaths[i + 1], OpenFlags.DEFAULT);

                            wim.ReferenceResources(splitWims);
                            wim.ExtractImage(1, destDir, ExtractFlags.NO_ACLS);
                        }
                        finally
                        {
                            foreach (var t in splitWims)
                                t?.Dispose();
                        }
                    }
                }
                catch (WimLibException)
                {
                    if (failure)
                        return;
                    else
                        Assert.Fail();
                }

                Assert.IsTrue(_checked.All(x => x));

                TestHelper.CheckFileSystem(SampleSet.Src03, destDir);
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
