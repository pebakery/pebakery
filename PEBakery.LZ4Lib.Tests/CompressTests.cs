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

namespace PEBakery.LZ4Lib.Tests
{
    [TestClass]
    public class CompressTests
    {
        [TestMethod]
        [TestCategory("LZ4Lib")]
        public void LZ4Lib_Compress()
        {
            Compress_Template("A.pdf", CompressionLevel.Fast);
            Compress_Template("B.txt", CompressionLevel.High);
            Compress_Template("C.bin", CompressionLevel.VeryHigh);
        }

        public void Compress_Template(string sampleFileName, CompressionLevel compLevel)
        {
            if (sampleFileName == null)
                throw new ArgumentNullException(nameof(sampleFileName));

            string destDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(destDir);
            try
            {
                string tempDecompFile = Path.Combine(destDir, Path.GetFileName(sampleFileName));
                string tempLz4File = tempDecompFile + ".lz4";

                string sampleFile = Path.Combine(TestSetup.SampleDir, sampleFileName);
                using (FileStream lz4CompFs = new FileStream(tempLz4File, FileMode.Create, FileAccess.Write, FileShare.None))
                using (FileStream sampleFs = new FileStream(sampleFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (LZ4FrameStream lzs = new LZ4FrameStream(lz4CompFs, LZ4Mode.Compress, compLevel, true))
                {
                    sampleFs.CopyTo(lzs);
                    lzs.Flush();

                    Assert.AreEqual(sampleFs.Length, lzs.TotalIn);
                    Assert.AreEqual(lz4CompFs.Length, lzs.TotalOut);
                }

                Process proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        FileName = Path.Combine(TestSetup.SampleDir, "lz4.exe"),
                        Arguments = $"-k -d {tempLz4File}",
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
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
            }
        }
    }
}
