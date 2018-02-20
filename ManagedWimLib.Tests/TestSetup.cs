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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ManagedWimLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ManagedWimLib.Tests
{
    [TestClass]
    public class TestSetup
    {
        public static string BaseDir;
        public static string SampleDir;

        [AssemblyInitialize]
        public static void Init(TestContext context)
        {
            BaseDir = Path.GetFullPath(Path.Combine(TestHelper.GetProgramAbsolutePath(), "..", ".."));
            SampleDir = Path.Combine(BaseDir, "Samples");

            switch (IntPtr.Size)
            {
                case 8:
                    Wim.GlobalInit(Path.Combine("x64", "libwim-15.dll"));
                    break;
                case 4:
                    Wim.GlobalInit(Path.Combine("x86", "libwim-15.dll"));
                    break;
                default:
                    throw new PlatformNotSupportedException();
            }
        }

        [AssemblyCleanup]
        public static void Cleanup()
        {
            Wim.GlobalCleanup();
        }
    }

    #region Helper
    public enum SampleSet
    {
        // TestSet Src01 is created for basic test and compresstion type test
        Src01,
        // TestSet Src02 is created for multi image and delta image test 
        Src02,
        Src02_1,
        Src02_2,
        Src02_3,
        // TestSet Src03 is created for split wim test and unicode test
        Src03,
    }

    public class TestHelper
    {
        public static string GetProgramAbsolutePath()
        {
            string path = AppDomain.CurrentDomain.BaseDirectory;
            if (Path.GetDirectoryName(path) != null)
                path = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return path;
        }

        #region File Check
        public static void CheckWimPath(SampleSet set, string wimFile)
        {
            switch (set)
            {
                case SampleSet.Src01:
                    using (Wim wim = Wim.OpenWim(wimFile, OpenFlags.DEFAULT))
                    {
                        Assert.IsTrue(wim.DirExists(1, Path.Combine(@"\", "ABCD")));
                        Assert.IsTrue(wim.DirExists(1, Path.Combine(@"\", "ABCD", "Z")));
                        Assert.IsTrue(wim.DirExists(1, Path.Combine(@"\", "ABDE")));
                        Assert.IsTrue(wim.DirExists(1, Path.Combine(@"\", "ABDE", "Z")));

                        Assert.IsTrue(wim.FileExists(1, Path.Combine(@"\", "ACDE.txt")));

                        Assert.IsTrue(wim.FileExists(1, Path.Combine(@"\", "ABCD", "A.txt")));
                        Assert.IsTrue(wim.FileExists(1, Path.Combine(@"\", "ABCD", "B.txt")));
                        Assert.IsTrue(wim.FileExists(1, Path.Combine(@"\", "ABCD", "C.txt")));
                        Assert.IsTrue(wim.FileExists(1, Path.Combine(@"\", "ABCD", "D.ini")));

                        Assert.IsTrue(wim.FileExists(1, Path.Combine(@"\", "ABCD", "Z", "X.txt")));
                        Assert.IsTrue(wim.FileExists(1, Path.Combine(@"\", "ABCD", "Z", "Y.ini")));

                        Assert.IsTrue(wim.FileExists(1, Path.Combine(@"\", "ABDE", "A.txt")));

                        Assert.IsTrue(wim.FileExists(1, Path.Combine(@"\", "ABDE", "Z", "X.txt")));
                        Assert.IsTrue(wim.FileExists(1, Path.Combine(@"\", "ABDE", "Z", "Y.ini")));
                    }
                    break;
                case SampleSet.Src02:
                    using (Wim wim = Wim.OpenWim(wimFile, OpenFlags.DEFAULT))
                    {
                        Assert.IsTrue(wim.DirExists(1, Path.Combine(@"\", "B")));
                        Assert.IsTrue(wim.FileExists(1, Path.Combine(@"\", "A.txt")));
                        Assert.IsTrue(wim.FileExists(1, Path.Combine(@"\", "B", "C.txt")));
                        Assert.IsTrue(wim.FileExists(1, Path.Combine(@"\", "B", "D.ini")));

                        Assert.IsTrue(wim.DirExists(2, Path.Combine(@"\", "B")));
                        Assert.IsTrue(wim.FileExists(2, Path.Combine(@"\", "Z.txt")));
                        Assert.IsTrue(wim.FileExists(2, Path.Combine(@"\", "B", "C.txt")));
                        Assert.IsTrue(wim.FileExists(2, Path.Combine(@"\", "B", "D.ini")));

                        Assert.IsTrue(wim.DirExists(3, Path.Combine(@"\", "B")));
                        Assert.IsTrue(wim.FileExists(3, Path.Combine(@"\", "Y.txt")));
                        Assert.IsTrue(wim.FileExists(3, Path.Combine(@"\", "Z.txt")));
                        Assert.IsTrue(wim.FileExists(3, Path.Combine(@"\", "B", "C.txt")));
                        Assert.IsTrue(wim.FileExists(3, Path.Combine(@"\", "B", "D.ini")));
                    }
                    break;
                case SampleSet.Src03:
                    break;
                default:
                    throw new NotImplementedException();
            }
            
        }

        public static void CheckFileSystem(SampleSet set, string dir)
        {
            switch (set)
            {
                case SampleSet.Src01:
                    Assert.IsTrue(Directory.Exists(Path.Combine(dir, "ABCD")));
                    Assert.IsTrue(Directory.Exists(Path.Combine(dir, "ABCD", "Z")));
                    Assert.IsTrue(Directory.Exists(Path.Combine(dir, "ABDE")));
                    Assert.IsTrue(Directory.Exists(Path.Combine(dir, "ABDE", "Z")));

                    Assert.IsTrue(File.Exists(Path.Combine(dir, "ACDE.txt")));
                    Assert.IsTrue(new FileInfo(Path.Combine(dir, "ACDE.txt")).Length == 1);

                    Assert.IsTrue(File.Exists(Path.Combine(dir, "ABCD", "A.txt")));
                    Assert.IsTrue(File.Exists(Path.Combine(dir, "ABCD", "B.txt")));
                    Assert.IsTrue(File.Exists(Path.Combine(dir, "ABCD", "C.txt")));
                    Assert.IsTrue(File.Exists(Path.Combine(dir, "ABCD", "D.ini")));
                    Assert.IsTrue(new FileInfo(Path.Combine(dir, "ABCD", "A.txt")).Length == 1);
                    Assert.IsTrue(new FileInfo(Path.Combine(dir, "ABCD", "B.txt")).Length == 2);
                    Assert.IsTrue(new FileInfo(Path.Combine(dir, "ABCD", "C.txt")).Length == 3);
                    Assert.IsTrue(new FileInfo(Path.Combine(dir, "ABCD", "D.ini")).Length == 1);

                    Assert.IsTrue(File.Exists(Path.Combine(dir, "ABCD", "Z", "X.txt")));
                    Assert.IsTrue(File.Exists(Path.Combine(dir, "ABCD", "Z", "Y.ini")));
                    Assert.IsTrue(new FileInfo(Path.Combine(dir, "ABCD", "Z", "X.txt")).Length == 1);
                    Assert.IsTrue(new FileInfo(Path.Combine(dir, "ABCD", "Z", "Y.ini")).Length == 1);

                    Assert.IsTrue(File.Exists(Path.Combine(dir, "ABDE", "A.txt")));
                    Assert.IsTrue(new FileInfo(Path.Combine(dir, "ABDE", "A.txt")).Length == 1);

                    Assert.IsTrue(File.Exists(Path.Combine(dir, "ABDE", "Z", "X.txt")));
                    Assert.IsTrue(File.Exists(Path.Combine(dir, "ABDE", "Z", "Y.ini")));
                    Assert.IsTrue(new FileInfo(Path.Combine(dir, "ABDE", "Z", "X.txt")).Length == 1);
                    Assert.IsTrue(new FileInfo(Path.Combine(dir, "ABDE", "Z", "Y.ini")).Length == 1);
                    break;
                case SampleSet.Src02_1:
                    Assert.IsTrue(Directory.Exists(Path.Combine(dir, "B")));

                    Assert.IsTrue(File.Exists(Path.Combine(dir, "A.txt")));
                    Assert.IsTrue(new FileInfo(Path.Combine(dir, "A.txt")).Length == 1);

                    Assert.IsTrue(File.Exists(Path.Combine(dir, "B", "C.txt")));
                    Assert.IsTrue(File.Exists(Path.Combine(dir, "B", "D.ini")));
                    Assert.IsTrue(new FileInfo(Path.Combine(dir, "B", "C.txt")).Length == 1);
                    Assert.IsTrue(new FileInfo(Path.Combine(dir, "B", "D.ini")).Length == 1);
                    break;
                case SampleSet.Src02_2:
                    Assert.IsTrue(Directory.Exists(Path.Combine(dir, "B")));

                    Assert.IsTrue(File.Exists(Path.Combine(dir, "Z.txt")));
                    Assert.IsTrue(new FileInfo(Path.Combine(dir, "Z.txt")).Length == 1);

                    Assert.IsTrue(File.Exists(Path.Combine(dir, "B", "C.txt")));
                    Assert.IsTrue(File.Exists(Path.Combine(dir, "B", "D.ini")));
                    Assert.IsTrue(new FileInfo(Path.Combine(dir, "B", "C.txt")).Length == 1);
                    Assert.IsTrue(new FileInfo(Path.Combine(dir, "B", "D.ini")).Length == 1);
                    break;
                case SampleSet.Src02_3:
                    Assert.IsTrue(Directory.Exists(Path.Combine(dir, "B")));

                    Assert.IsTrue(File.Exists(Path.Combine(dir, "Y.txt")));
                    Assert.IsTrue(File.Exists(Path.Combine(dir, "Z.txt")));
                    Assert.IsTrue(new FileInfo(Path.Combine(dir, "Y.txt")).Length == 1);
                    Assert.IsTrue(new FileInfo(Path.Combine(dir, "Z.txt")).Length == 1);

                    Assert.IsTrue(File.Exists(Path.Combine(dir, "B", "C.txt")));
                    Assert.IsTrue(File.Exists(Path.Combine(dir, "B", "D.ini")));
                    Assert.IsTrue(new FileInfo(Path.Combine(dir, "B", "C.txt")).Length == 1);
                    Assert.IsTrue(new FileInfo(Path.Combine(dir, "B", "D.ini")).Length == 1);
                    break;
                case SampleSet.Src03:
                    Assert.IsTrue(File.Exists(Path.Combine(dir, "가")));
                    Assert.IsTrue(File.Exists(Path.Combine(dir, "나")));
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        public static void CheckPathList(SampleSet set, List<Tuple<string, bool>> paths)
        {
            Tuple<string, bool>[] checkList;
            switch (set)
            {
                case SampleSet.Src01:
                    checkList = new[]
                    {
                        new Tuple<string, bool>(Path.Combine(@"\ABCD"), true),
                        new Tuple<string, bool>(Path.Combine(@"\ABCD", "Z"), true),
                        new Tuple<string, bool>(Path.Combine(@"\ABDE"), true),
                        new Tuple<string, bool>(Path.Combine(@"\ABDE", "Z"), true),

                        new Tuple<string, bool>(Path.Combine(@"\ACDE.txt"), false),

                        new Tuple<string, bool>(Path.Combine(@"\ABCD", "A.txt"), false),
                        new Tuple<string, bool>(Path.Combine(@"\ABCD", "B.txt"), false),
                        new Tuple<string, bool>(Path.Combine(@"\ABCD", "C.txt"), false),
                        new Tuple<string, bool>(Path.Combine(@"\ABCD", "D.ini"), false),

                        new Tuple<string, bool>(Path.Combine(@"\ABCD", "Z", "X.txt"), false),
                        new Tuple<string, bool>(Path.Combine(@"\ABCD", "Z", "Y.ini"), false),

                        new Tuple<string, bool>(Path.Combine(@"\ABDE", "A.txt"), false),

                        new Tuple<string, bool>(Path.Combine(@"\ABDE", "Z", "X.txt"), false),
                        new Tuple<string, bool>(Path.Combine(@"\ABDE", "Z", "Y.ini"), false),
                    };
                    break;
                case SampleSet.Src02_1:
                    checkList = new[]
                    {
                        new Tuple<string, bool>(Path.Combine(@"\B"), true),
                        new Tuple<string, bool>(Path.Combine(@"\A.txt"), false),
                        new Tuple<string, bool>(Path.Combine(@"\B", "C.txt"), false),
                        new Tuple<string, bool>(Path.Combine(@"\B", "D.ini"), false),
                    };
                    break;
                case SampleSet.Src02_2:
                    checkList = new[]
                    {
                        new Tuple<string, bool>(Path.Combine(@"\B"), true),
                        new Tuple<string, bool>(Path.Combine(@"\Z.txt"), false),
                        new Tuple<string, bool>(Path.Combine(@"\B", "C.txt"), false),
                        new Tuple<string, bool>(Path.Combine(@"\B", "D.ini"), false),
                    };
                    break;
                case SampleSet.Src02_3:
                    checkList = new[]
                    {
                        new Tuple<string, bool>(Path.Combine(@"\B"), true),
                        new Tuple<string, bool>(Path.Combine(@"\Y.txt"), false),
                        new Tuple<string, bool>(Path.Combine(@"\Z.txt"), false),
                        new Tuple<string, bool>(Path.Combine(@"\B", "C.txt"), false),
                        new Tuple<string, bool>(Path.Combine(@"\B", "D.ini"), false),
                    };
                    break;
                case SampleSet.Src03:
                    checkList = new[]
                    {
                        new Tuple<string, bool>(Path.Combine(@"\가"), false),
                        new Tuple<string, bool>(Path.Combine(@"\나"), false),
                    };
                    break;
                default:
                    throw new NotImplementedException();
            }

            foreach (var tup in checkList)
                Assert.IsTrue(paths.Contains(tup, new CheckWimPathComparer()));
        }

        public static List<Tuple<string, bool>> GenerateWimPathList(string wimFile)
        {
            List<Tuple<string, bool>> entries = new List<Tuple<string, bool>>();

            CallbackStatus IterateCallback(DirEntry dentry, object userData)
            {
                string path = dentry.FullPath;
                bool isDir = (dentry.Attributes & FileAttribute.DIRECTORY) != 0;
                entries.Add(new Tuple<string, bool>(path, isDir));

                return CallbackStatus.CONTINUE;
            }

            using (Wim wim = Wim.OpenWim(wimFile, OpenFlags.DEFAULT))
            {
                wim.IterateDirTree(1, Wim.RootPath, IterateFlags.RECURSIVE, IterateCallback);
            }

            return entries;
        }

        public class CheckWimPathComparer : IEqualityComparer<Tuple<string, bool>>
        {
            public bool Equals(Tuple<string, bool> x, Tuple<string, bool> y)
            {
                bool path = x.Item1.Equals(y.Item1, StringComparison.Ordinal);
                bool isDir = x.Item2 == y.Item2;
                return path && isDir;
            }

            public int GetHashCode(Tuple<string, bool> x)
            {
                return x.Item1.GetHashCode() ^ x.Item2.GetHashCode();
            }
        }
        #endregion
    }
    #endregion

    #region CallbackTested
    public class CallbackTested
    {
        public bool Value = false;

        public CallbackTested(bool initValue)
        {
            Value = initValue;
        }

        public void Set()
        {
            Value = true;
        }

        public void Reset()
        {
            Value = false;
        }
    }
    #endregion
}
