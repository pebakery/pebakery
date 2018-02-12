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
    public class UpdateTests
    {
        #region Update
        [TestMethod]
        [TestCategory("WimLib")]
        public void Update()
        {
            string sampleDir = Path.Combine(TestSetup.BaseDir, "Samples");

            void RunUpdateTest(string wimFile, UpdateCommand[] cmds)
            {
                try
                {
                    Update_Template(wimFile, cmds);
                }
                finally
                {
                    for (int i = 0; i < cmds.Length; i++)
                        cmds[i].Dispose();
                }
            }

            RunUpdateTest("XPRESS.wim", new UpdateCommand[2]
            {
                UpdateCommand.Add(Path.Combine(sampleDir, "Append01", "Z.txt"), "ADD", null, AddFlags.DEFAULT),
                UpdateCommand.Add(Path.Combine(sampleDir, "Src03", "가"), "유니코드", null, AddFlags.DEFAULT),
            });

            RunUpdateTest("LZX.wim", new UpdateCommand[2]
            {
                UpdateCommand.Delete("ACDE.txt", DeleteFlags.DEFAULT),
                UpdateCommand.Delete("ABCD", DeleteFlags.RECURSIVE),
            });

            RunUpdateTest("LZMS.wim", new UpdateCommand[2]
            {
                UpdateCommand.Rename("ACDE.txt", "FILE"),
                UpdateCommand.Rename("ABCD", "DIR"),
            });
        }

        public CallbackStatus IterateDirTree_Callback(DirEntry dentry, object userData)
        {
            List<string> entries = userData as List<string>;

            entries.Add(dentry.FullPath);

            return CallbackStatus.CONTINUE;
        }

        public void Update_Template(string fileName, UpdateCommand[] cmds)
        {
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                Directory.CreateDirectory(destDir);

                string srcWimFile = Path.Combine(TestSetup.BaseDir, "Samples", fileName);
                string destWimFile = Path.Combine(destDir, fileName);
                File.Copy(srcWimFile, destWimFile, true);

                using (Wim wim = Wim.OpenWim(destWimFile, OpenFlags.WRITE_ACCESS))
                {
                    wim.UpdateImage(1, cmds, UpdateFlags.SEND_PROGRESS);

                    wim.Overwrite(WriteFlags.DEFAULT, Wim.DefaultThreads);
                }

                List<string> entries = new List<string>();
                using (Wim wim = Wim.OpenWim(destWimFile, OpenFlags.DEFAULT))
                {
                    wim.IterateDirTree(1, @"\", IterateFlags.RECURSIVE, IterateDirTree_Callback, entries);
                }

                foreach (UpdateCommand cmd in cmds)
                {
                    switch (cmd.Op)
                    {
                        case UpdateOp.ADD:
                            {
                                var add = cmd.AddCommand;
                                Assert.IsTrue(entries.Contains(Path.Combine(@"\", add.WimTargetPath), StringComparer.Ordinal));
                            }
                            break;
                        case UpdateOp.DELETE:
                            {
                                var del = cmd.DeleteCommand;
                                Assert.IsFalse(entries.Contains(Path.Combine(@"\", del.WimPath), StringComparer.Ordinal));
                            }
                            break;
                        case UpdateOp.RENAME:
                            {
                                var ren = cmd.RenameCommand;
                                Assert.IsTrue(entries.Contains(Path.Combine(@"\", ren.WimTargetPath), StringComparer.Ordinal));
                            }
                            break;
                    }
                }
            }
            finally
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
            }
        }
        #endregion

        #region UpdateProgress
        [TestMethod]
        [TestCategory("WimLib")]
        public void UpdateProgress()
        {
            string sampleDir = Path.Combine(TestSetup.BaseDir, "Samples");

            void RunUpdateTest(string wimFile, UpdateCommand[] cmds)
            {
                try
                {
                    UpdateProgress_Template(wimFile, cmds);
                }
                finally
                {
                    for (int i = 0; i < cmds.Length; i++)
                        cmds[i].Dispose();
                }
            }

            RunUpdateTest("XPRESS.wim", new UpdateCommand[2]
            {
                UpdateCommand.Add(Path.Combine(sampleDir, "Append01", "Z.txt"), "ADD", null, AddFlags.DEFAULT),
                UpdateCommand.Add(Path.Combine(sampleDir, "Src03", "가"), "유니코드", null, AddFlags.DEFAULT),
            });

            RunUpdateTest("LZX.wim", new UpdateCommand[2]
            {
                UpdateCommand.Delete("ACDE.txt", DeleteFlags.DEFAULT),
                UpdateCommand.Delete("ABCD", DeleteFlags.RECURSIVE),
            });

            RunUpdateTest("LZMS.wim", new UpdateCommand[2]
            {
                UpdateCommand.Rename("ACDE.txt", "FILE"),
                UpdateCommand.Rename("ABCD", "DIR"),
            });
        }

        public CallbackStatus UpdateProgress_Callback(ProgressMsg msg, object info, object progctx)
        {
            CallbackTested tested = progctx as CallbackTested;
            Assert.IsNotNull(tested);

            Console.WriteLine(msg);
            switch (msg)
            {
                case ProgressMsg.UPDATE_BEGIN_COMMAND:
                case ProgressMsg.UPDATE_END_COMMAND:
                    {
                        WimLibProgressInfo_Update m = (WimLibProgressInfo_Update)info;

                        Assert.IsNotNull(m);
                        tested.Set();

                        UpdateCommand cmd = m.Command;
                        switch (cmd.Op)
                        {
                            case UpdateOp.ADD:
                                {
                                    var add = cmd.AddCommand;
                                    Console.WriteLine($"ADD [{add.FsSourcePath}] -> [{add.WimTargetPath}]");
                                }
                                break;
                            case UpdateOp.DELETE:
                                {
                                    var del = cmd.DeleteCommand;
                                    Console.WriteLine($"DELETE [{del.WimPath}]");
                                }
                                break;
                            case UpdateOp.RENAME:
                                {
                                    var ren = cmd.RenameCommand;
                                    Console.WriteLine($"RENAME [{ren.WimSourcePath}] -> [{ren.WimTargetPath}]");
                                }
                                break;
                        }
                    }
                    break;
                default:
                    break;
            }
            return CallbackStatus.CONTINUE;
        }

        public void UpdateProgress_Template(string fileName, UpdateCommand[] cmds)
        {
            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                CallbackTested tested = new CallbackTested(false);
                Directory.CreateDirectory(destDir);

                string srcWimFile = Path.Combine(TestSetup.BaseDir, "Samples", fileName);
                string destWimFile = Path.Combine(destDir, fileName);
                File.Copy(srcWimFile, destWimFile, true);

                using (Wim wim = Wim.OpenWim(destWimFile, OpenFlags.WRITE_ACCESS, UpdateProgress_Callback, tested))
                {
                    wim.UpdateImage(1, cmds, UpdateFlags.SEND_PROGRESS);

                    wim.Overwrite(WriteFlags.DEFAULT, Wim.DefaultThreads);
                }

                List<string> entries = new List<string>();
                using (Wim wim = Wim.OpenWim(destWimFile, OpenFlags.DEFAULT))
                {
                    wim.IterateDirTree(1, @"\", IterateFlags.RECURSIVE, IterateDirTree_Callback, entries);
                }

                Assert.IsTrue(tested.Value);
                foreach (UpdateCommand cmd in cmds)
                {
                    switch (cmd.Op)
                    {
                        case UpdateOp.ADD:
                            {
                                var add = cmd.AddCommand;
                                Assert.IsTrue(entries.Contains(Path.Combine(@"\", add.WimTargetPath), StringComparer.Ordinal));
                            }
                            break;
                        case UpdateOp.DELETE:
                            {
                                var del = cmd.DeleteCommand;
                                Assert.IsFalse(entries.Contains(Path.Combine(@"\", del.WimPath), StringComparer.Ordinal));
                            }
                            break;
                        case UpdateOp.RENAME:
                            {
                                var ren = cmd.RenameCommand;
                                Assert.IsTrue(entries.Contains(Path.Combine(@"\", ren.WimTargetPath), StringComparer.Ordinal));
                            }
                            break;
                    }
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
