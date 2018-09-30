/*
    Copyright (c) 2017 Hajin Jang

    Licensed under the Apache License, Version 2.0 (the "License");
    you may not use this file except in compliance with the License.
    You may obtain a copy of the License at

        http://www.apache.org/licenses/LICENSE-2.0

    Unless required by applicable law or agreed to in writing, software
    distributed under the License is distributed on an "AS IS" BASIS,
    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
    See the License for the specific language governing permissions and
    limitations under the License.
*/

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace PEBakery.Cab.Tests
{
    [TestClass]
    public class TestHelper
    {
        public static string BaseDir { get; private set; }
        public static string DestDir { get; private set; }

        [AssemblyInitialize]
        public static void Init(TestContext ctx)
        {
            BaseDir = Path.Combine("..", "..", "Samples");
            DestDir = Path.Combine(BaseDir, "Dest");
        }

        [AssemblyCleanup]
        public static void Cleanup()
        {
            Directory.Delete(DestDir, true);
        }

        public static byte[] SHA256Digest(Stream stream)
        {
            HashAlgorithm hash = SHA256.Create();
            return hash.ComputeHash(stream);
        }

        public static byte[] SHA256Digest(byte[] input)
        {
            HashAlgorithm hash = SHA256.Create();
            return hash.ComputeHash(input);
        }

        public static byte[] SHA256Digest(string filePath)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return SHA256Digest(fs);
            }
        }
    }

    [TestClass]
    public class CabLibTests
    {
        [TestMethod]
        [TestCategory("PEBakery.Cab")]
        public void CabExtract_1()
        {
            string originPath = Path.Combine(TestHelper.BaseDir, "ex1.jpg");
            string compPath = Path.Combine(TestHelper.BaseDir, "ex1.cab");
            string decompDir = Path.Combine(TestHelper.DestDir, "ex1");
            using (FileStream fs = new FileStream(compPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (CabExtract cab = new CabExtract(fs))
            {
                Assert.IsTrue(cab.ExtractAll(decompDir, out List<string> fileList));
                Assert.IsTrue(fileList.Count == 1);
                Assert.IsTrue(fileList[0].Equals("ex1.jpg"));
            }

            // Compare SHA256 Digest
            byte[] originDigest = TestHelper.SHA256Digest(originPath);
            byte[] decompDigest = TestHelper.SHA256Digest(Path.Combine(decompDir, "ex1.jpg"));
            Assert.IsTrue(decompDigest.SequenceEqual(originDigest));
        }

        [TestMethod]
        [TestCategory("PEBakery.Cab")]
        public void CabExtract_2()
        {
            string originPath = Path.Combine(TestHelper.BaseDir, "ex2.jpg");
            string compPath = Path.Combine(TestHelper.BaseDir, "ex2.cab");
            string decompDir = Path.Combine(TestHelper.DestDir, "ex2");
            using (FileStream fs = new FileStream(compPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (CabExtract cab = new CabExtract(fs))
            {
                Assert.IsFalse(cab.ExtractSingleFile("ex3.jpg", decompDir)); // ex3.jpg does not exist in ex2.cab
                Assert.IsTrue(cab.ExtractSingleFile("ex2.jpg", decompDir));
            }

            // Compare SHA256 Digest
            byte[] originDigest = TestHelper.SHA256Digest(originPath);
            byte[] decompDigest = TestHelper.SHA256Digest(Path.Combine(decompDir, "ex2.jpg"));
            Assert.IsTrue(decompDigest.SequenceEqual(originDigest));
        }

        [TestMethod]
        [TestCategory("PEBakery.Cab")]
        public void CabExtract_3()
        {
            string originPath = Path.Combine(TestHelper.BaseDir, "ex3.jpg");
            string compPath = Path.Combine(TestHelper.BaseDir, "ex3.cab");
            string decompDir = Path.Combine(TestHelper.DestDir, "ex3");
            using (FileStream fs = new FileStream(compPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (CabExtract cab = new CabExtract(fs))
            {
                Assert.IsFalse(cab.ExtractSingleFile("ex2.jpg", decompDir)); // ex2.jpg does not exist in ex3.cab
                Assert.IsTrue(cab.ExtractAll(decompDir, out List<string> fileList));
                Assert.IsTrue(fileList.Count == 1);
                Assert.IsTrue(fileList[0].Equals("ex3.jpg"));
            }

            // Compare SHA256 Digest
            byte[] originDigest = TestHelper.SHA256Digest(originPath);
            byte[] decompDigest = TestHelper.SHA256Digest(Path.Combine(decompDir, "ex3.jpg"));
            Assert.IsTrue(decompDigest.SequenceEqual(originDigest));
        }

        [TestMethod]
        [TestCategory("PEBakery.Cab")]
        public void CabExtract_4()
        {
            string compPath = Path.Combine(TestHelper.BaseDir, "ex4.cab");
            string decompDir = Path.Combine(TestHelper.DestDir, "ex4");
            using (FileStream fs = new FileStream(compPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (CabExtract cab = new CabExtract(fs))
            {
                Assert.IsTrue(cab.ExtractAll(decompDir, out List<string> fileList));
                Assert.IsTrue(fileList.Count == 3);
                Assert.IsTrue(fileList.Contains("ex1.jpg"));
                Assert.IsTrue(fileList.Contains("ex2.jpg"));
                Assert.IsTrue(fileList.Contains("ex3.jpg"));
            }

            // Compare SHA256 Digest
            byte[] originDigest1 = TestHelper.SHA256Digest(Path.Combine(TestHelper.BaseDir, "ex1.jpg"));
            byte[] decompDigest1 = TestHelper.SHA256Digest(Path.Combine(decompDir, "ex1.jpg"));
            Assert.IsTrue(decompDigest1.SequenceEqual(originDigest1));

            byte[] originDigest2 = TestHelper.SHA256Digest(Path.Combine(TestHelper.BaseDir, "ex2.jpg"));
            byte[] decompDigest2 = TestHelper.SHA256Digest(Path.Combine(decompDir, "ex2.jpg"));
            Assert.IsTrue(decompDigest2.SequenceEqual(originDigest2));

            byte[] originDigest3 = TestHelper.SHA256Digest(Path.Combine(TestHelper.BaseDir, "ex3.jpg"));
            byte[] decompDigest3 = TestHelper.SHA256Digest(Path.Combine(decompDir, "ex3.jpg"));
            Assert.IsTrue(decompDigest3.SequenceEqual(originDigest3));
        }
    }
}
