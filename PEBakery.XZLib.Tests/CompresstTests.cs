/*
    Derived from liblzma header files (Public Domain)

    C# Wrapper written by Hajin Jang
    Copyright (C) 2018 Hajin Jang

    MIT License

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
*/

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PEBakery.XZLib.Tests
{
    [TestClass]
    public class CompresstTests
    {
        [TestMethod]
        [TestCategory("XZLib")]
        public void Compress()
        {
            Compress_Template("A.pdf", 1, 9);
            Compress_Template("B.txt", 1, XZStream.DefaultPreset);
            Compress_Template("C.bin", 1, 1);
        }

        [TestMethod]
        [TestCategory("XZLib")]
        public void CompressMultithread()
        {
            Compress_Template("A.pdf", 2, 6);
            Compress_Template("B.txt", 2, 3);
            Compress_Template("C.bin", Environment.ProcessorCount, 1);
        }

        public void Compress_Template(string sampleFileName, int threads, uint preset)
        {
            string tempDecompFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string tempXzFile = tempDecompFile + ".xz";
            try
            {
                string sampleFile = Path.Combine(TestSetup.SampleDir, sampleFileName);
                using (FileStream xzCompFs = new FileStream(tempXzFile, FileMode.Create, FileAccess.Write, FileShare.None))
                using (FileStream sampleFs = new FileStream(sampleFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (XZStream xzs = new XZStream(xzCompFs, LzmaMode.Compress, preset, threads, true))
                {
                    sampleFs.CopyTo(xzs);
                }

                Process proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        FileName = Path.Combine(TestSetup.SampleDir, "xz.exe"),
                        Arguments = $"-k -d {tempXzFile}",
                    }
                };
                proc.Start();
                proc.WaitForExit();
                Assert.IsTrue(proc.ExitCode == 0);

                byte[] decompDigest;
                byte[] originDigest;
                using (FileStream fs = new FileStream(sampleFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    HashAlgorithm hash = SHA256.Create();
                    originDigest = hash.ComputeHash(fs);
                }

                using (FileStream fs = new FileStream(tempDecompFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    HashAlgorithm hash = SHA256.Create();
                    decompDigest = hash.ComputeHash(fs);
                }

                Assert.IsTrue(originDigest.SequenceEqual(decompDigest));

            }
            finally
            {
                if (File.Exists(tempXzFile))
                    File.Delete(tempXzFile);
                if (File.Exists(tempDecompFile))
                    File.Delete(tempDecompFile);
            }
        }
    }
}
