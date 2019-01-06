using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text;
using System.Threading;

// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local

namespace PEBakery.Helper.Tests
{
    #region HashHelper
    [TestClass]
    public class HashHelperTests
    {
        #region GetHash
        [TestMethod]
        [TestCategory("Helper")]
        [TestCategory("HashHelper")]
        public void GetHash()
        {
            void ArrayTemplate(HashHelper.HashType type, byte[] input, string expected)
            {
                byte[] digest = HashHelper.GetHash(type, input);
                string actual = StringHelper.ToHexStr(digest);
                Assert.IsTrue(actual.Equals(expected, StringComparison.Ordinal));
            }

            void StreamTemplate(HashHelper.HashType type, Stream stream, string expected)
            {
                stream.Position = 0;
                byte[] digest = HashHelper.GetHash(type, stream);
                string actual = StringHelper.ToHexStr(digest);
                Assert.IsTrue(actual.Equals(expected, StringComparison.Ordinal));
            }

            const string md5Digest = "68e109f0f40ca72a15e05cc22786f8e6";
            const string sha1Digest = "db8ac1c259eb89d4a131b253bacfca5f319d54f2";
            const string sha256Digest = "872e4e50ce9990d8b041330c47c9ddd11bec6b503ae9386a99da8584e9bb12c4";
            const string sha384Digest = "293cd96eb25228a6fb09bfa86b9148ab69940e68903cbc0527a4fb150eec1ebe0f1ffce0bc5e3df312377e0a68f1950a";
            const string sha512Digest = "8ae6ae71a75d3fb2e0225deeb004faf95d816a0a58093eb4cb5a3aa0f197050d7a4dc0a2d5c6fbae5fb5b0d536a0a9e6b686369fa57a027687c3630321547596";

            byte[] buffer = Encoding.UTF8.GetBytes("HelloWorld");
            ArrayTemplate(HashHelper.HashType.MD5, buffer, md5Digest);
            ArrayTemplate(HashHelper.HashType.SHA1, buffer, sha1Digest);
            ArrayTemplate(HashHelper.HashType.SHA256, buffer, sha256Digest);
            ArrayTemplate(HashHelper.HashType.SHA384, buffer, sha384Digest);
            ArrayTemplate(HashHelper.HashType.SHA512, buffer, sha512Digest);

            using (MemoryStream ms = new MemoryStream(buffer))
            {
                StreamTemplate(HashHelper.HashType.MD5, ms, md5Digest);
                StreamTemplate(HashHelper.HashType.SHA1, ms, sha1Digest);
                StreamTemplate(HashHelper.HashType.SHA256, ms, sha256Digest);
                StreamTemplate(HashHelper.HashType.SHA384, ms, sha384Digest);
                StreamTemplate(HashHelper.HashType.SHA512, ms, sha512Digest);
            }
        }
        #endregion

        #region GetHashProgress
        [TestMethod]
        [TestCategory("Helper")]
        [TestCategory("HashHelper")]
        public void GetHashProgress()
        {
            void ArrayTemplate(HashHelper.HashType type, byte[] input, string expected)
            {
                int idx = 0;
                IProgress<long> progress = new Progress<long>(x =>
                {
                    int thisIdx = Interlocked.Increment(ref idx);
                    switch (thisIdx)
                    {
                        default:
                            Assert.AreEqual(thisIdx * 1024 * 1024, x);
                            break;
                        case 4:
                            Assert.AreEqual((3 * 1024 + 512) * 1024, x);
                            break;
                    }
                });

                byte[] digest = HashHelper.GetHash(type, input, 1024 * 1024, progress);
                string actual = StringHelper.ToHexStr(digest);
                Assert.IsTrue(actual.Equals(expected, StringComparison.Ordinal));
            }

            void StreamTemplate(HashHelper.HashType type, Stream stream, string expected)
            {
                int idx = 0;
                IProgress<long> progress = new Progress<long>(x =>
                {
                    int thisIdx = Interlocked.Increment(ref idx);
                    Assert.AreEqual(thisIdx * 1024 * 1024, x);
                });

                stream.Position = 0;
                byte[] digest = HashHelper.GetHash(type, stream, 1024 * 1024, progress);
                string actual = StringHelper.ToHexStr(digest);
                Assert.IsTrue(actual.Equals(expected, StringComparison.Ordinal));
            }

            string srcDir = Path.Combine(TestSetup.SampleDir, "HashHelper");
            string srcFile = Path.Combine(srcDir, "sample.bin");

            FileInfo fi = new FileInfo(srcFile);
            byte[] buffer = new byte[fi.Length];

            using (FileStream fs = new FileStream(srcFile, FileMode.Open))
            {
                fs.Read(buffer, 0, buffer.Length);
            }

            const string md5Digest = "656aca9ddca96c931e397c64afa3d838";
            const string sha1Digest = "9c2603f1a3b5156eb66c9975d3ebbe229cc9dbc0";
            const string sha256Digest = "4d9cbaf3aa0935a8c113f139691b3daf9c94c8d6c278aedc8eec66a4b9f6c8ae";
            const string sha384Digest = "4e7027f3ff93f86f805a91abba7f7d16918493f464bdf5211ad8768d2b4a22ca5d7c235f1f81992140cbf3efa405558e";
            const string sha512Digest = "7f1bc5840d0e5b1b1e9aedecfed4d4de3249839de8ab33b1cea1873c1f4e453c3bb5e08260e0a567d561a19cd54597c41c5f983bb0515392caaefdb5158df281";

            ArrayTemplate(HashHelper.HashType.MD5, buffer, md5Digest);
            ArrayTemplate(HashHelper.HashType.SHA1, buffer, sha1Digest);
            ArrayTemplate(HashHelper.HashType.SHA256, buffer, sha256Digest);
            ArrayTemplate(HashHelper.HashType.SHA384, buffer, sha384Digest);
            ArrayTemplate(HashHelper.HashType.SHA512, buffer, sha512Digest);

            using (MemoryStream ms = new MemoryStream(buffer))
            {
                StreamTemplate(HashHelper.HashType.MD5, ms, md5Digest);
                StreamTemplate(HashHelper.HashType.SHA1, ms, sha1Digest);
                StreamTemplate(HashHelper.HashType.SHA256, ms, sha256Digest);
                StreamTemplate(HashHelper.HashType.SHA384, ms, sha384Digest);
                StreamTemplate(HashHelper.HashType.SHA512, ms, sha512Digest);
            }
        }
        #endregion
    }
    #endregion
}
