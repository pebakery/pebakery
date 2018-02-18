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

            if (IntPtr.Size == 8)
                NativeMethods.AssemblyInit(Path.Combine("x64", "libwim-15.dll"));
            else if (IntPtr.Size == 4)
                NativeMethods.AssemblyInit(Path.Combine("x86", "libwim-15.dll"));
            else
                throw new PlatformNotSupportedException();
        }

        [AssemblyCleanup]
        public static void Cleanup()
        {
            NativeMethods.AssemblyCleanup();
        }
    }

    #region Helper
    public class TestHelper
    {
        public static string GetProgramAbsolutePath()
        {
            string path = AppDomain.CurrentDomain.BaseDirectory;
            if (Path.GetDirectoryName(path) != null)
                path = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return path;
        }

        public static void CheckWimPath_Src01(string wimFile)
        {
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
        }

        public static void CheckFileSystem_Src01(string dir)
        {
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
        }

        public static void CheckList_Src01(List<string> files)
        {
            Assert.IsTrue(files.Contains(Path.Combine(@"\ABCD"), StringComparer.Ordinal));
            Assert.IsTrue(files.Contains(Path.Combine(@"\ABCD", "Z"), StringComparer.Ordinal));
            Assert.IsTrue(files.Contains(Path.Combine(@"\ABDE"), StringComparer.Ordinal));
            Assert.IsTrue(files.Contains(Path.Combine(@"\ABDE", "Z"), StringComparer.Ordinal));

            Assert.IsTrue(files.Contains(Path.Combine(@"\ACDE.txt"), StringComparer.Ordinal));

            Assert.IsTrue(files.Contains(Path.Combine(@"\ABCD", "A.txt"), StringComparer.Ordinal));
            Assert.IsTrue(files.Contains(Path.Combine(@"\ABCD", "B.txt"), StringComparer.Ordinal));
            Assert.IsTrue(files.Contains(Path.Combine(@"\ABCD", "C.txt"), StringComparer.Ordinal));
            Assert.IsTrue(files.Contains(Path.Combine(@"\ABCD", "D.ini"), StringComparer.Ordinal));

            Assert.IsTrue(files.Contains(Path.Combine(@"\ABCD", "Z", "X.txt"), StringComparer.Ordinal));
            Assert.IsTrue(files.Contains(Path.Combine(@"\ABCD", "Z", "Y.ini"), StringComparer.Ordinal));

            Assert.IsTrue(files.Contains(Path.Combine(@"\ABDE", "A.txt"), StringComparer.Ordinal));

            Assert.IsTrue(files.Contains(Path.Combine(@"\ABDE", "Z", "X.txt"), StringComparer.Ordinal));
            Assert.IsTrue(files.Contains(Path.Combine(@"\ABDE", "Z", "Y.ini"), StringComparer.Ordinal));
        }

        public static void CheckAppend_Src01(string dir)
        {
            Assert.IsTrue(Directory.Exists(Path.Combine(dir, "ABDE")));
            Assert.IsTrue(Directory.Exists(Path.Combine(dir, "ABDE", "Z")));

            Assert.IsTrue(File.Exists(Path.Combine(dir, "Z.txt")));
            Assert.IsTrue(new FileInfo(Path.Combine(dir, "Z.txt")).Length == 1);

            Assert.IsTrue(File.Exists(Path.Combine(dir, "ABDE", "A.txt")));
            Assert.IsTrue(new FileInfo(Path.Combine(dir, "ABDE", "A.txt")).Length == 1);

            Assert.IsTrue(File.Exists(Path.Combine(dir, "ABDE", "Z", "X.txt")));
            Assert.IsTrue(File.Exists(Path.Combine(dir, "ABDE", "Z", "Y.ini")));
            Assert.IsTrue(new FileInfo(Path.Combine(dir, "ABDE", "Z", "X.txt")).Length == 1);
            Assert.IsTrue(new FileInfo(Path.Combine(dir, "ABDE", "Z", "Y.ini")).Length == 1);
        }
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
