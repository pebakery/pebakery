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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PEBakery.LZ4Lib.Tests
{
    [TestClass]
    public class DecompressTests
    {
        [TestMethod]
        [TestCategory("LZ4Lib")]
        public void LZ4Lib_Decompress()
        {
            Decompress_Template("A.pdf.lz4", "A.pdf"); // -12
            Decompress_Template("B.txt.lz4", "B.txt"); // -9
            Decompress_Template("C.bin.lz4", "C.bin"); // -1
        }

        public void Decompress_Template(string lz4FileName, string originFileName)
        {
            byte[] decompDigest;
            byte[] originDigest;

            string lz4File = Path.Combine(TestSetup.SampleDir, lz4FileName);
            string originFile = Path.Combine(TestSetup.SampleDir, originFileName);
            using (MemoryStream decompMs = new MemoryStream())
            {
                using (FileStream compFs = new FileStream(lz4File, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (LZ4FrameStream xz = new LZ4FrameStream(compFs, LZ4Mode.Decompress))
                {
                    xz.CopyTo(decompMs);
                }
                decompMs.Position = 0;

                HashAlgorithm hash = SHA256.Create();
                decompDigest = hash.ComputeHash(decompMs);
            }

            using (FileStream originFs = new FileStream(originFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                HashAlgorithm hash = SHA256.Create();
                originDigest = hash.ComputeHash(originFs);
            }

            Assert.IsTrue(decompDigest.SequenceEqual(originDigest));
        }
    }
}
