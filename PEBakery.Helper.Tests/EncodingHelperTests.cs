/*
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Text;

namespace PEBakery.Helper.Tests
{
    [TestClass]
    public class EncodingHelperTests
    {
        #region DetectTextEncoding
        [TestMethod]
        [TestCategory("Helper")]
        [TestCategory("EncodingHelper")]
        public void DetectTextEncoding()
        {
            string tempDir = FileHelper.GetTempDir();
            string tempFile = Path.Combine(tempDir, "Sample.txt");

            try
            {
                // Empty -> ANSI
                File.Create(tempFile).Close();
                Assert.AreEqual(EncodingHelper.DetectTextEncoding(tempFile), Encoding.Default);

                // UTF-16 LE
                EncodingHelper.WriteTextBom(tempFile, Encoding.Unicode);
                Assert.AreEqual(EncodingHelper.DetectTextEncoding(tempFile), Encoding.Unicode);

                // UTF-16 BE
                EncodingHelper.WriteTextBom(tempFile, Encoding.BigEndianUnicode);
                Assert.AreEqual(EncodingHelper.DetectTextEncoding(tempFile), Encoding.BigEndianUnicode);

                // UTF-8
                EncodingHelper.WriteTextBom(tempFile, Encoding.UTF8);
                Assert.AreEqual(EncodingHelper.DetectTextEncoding(tempFile), Encoding.UTF8);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
        #endregion

        #region IsText
        [TestMethod]
        [TestCategory("Helper")]
        [TestCategory("EncodingHelper")]
        public void IsText()
        {
            const int peekSize = 16 * 1024;
            string srcDir = Path.Combine(TestSetup.SampleDir, "EncodingHelper");

            // Test ANSI
            // Cannot test non-ASCII ANSI encoding, will differ in various environment
            string targetFile = Path.Combine(srcDir, "Banner.svg");
            Assert.IsTrue(EncodingHelper.IsText(targetFile, peekSize));
            targetFile = Path.Combine(srcDir, "Zero.bin");
            Assert.IsTrue(EncodingHelper.IsText(targetFile, peekSize));

            // UTF-16 LE
            targetFile = Path.Combine(srcDir, "UTF16LE.txt");
            Assert.IsTrue(EncodingHelper.IsText(targetFile, peekSize));

            // UTF-16 BE
            targetFile = Path.Combine(srcDir, "UTF16BE.txt");
            Assert.IsTrue(EncodingHelper.IsText(targetFile, peekSize));

            // UTF-8
            targetFile = Path.Combine(srcDir, "UTF8.txt");
            Assert.IsTrue(EncodingHelper.IsText(targetFile, peekSize));

            // Binary
            targetFile = Path.Combine(srcDir, "Type3.pdf");
            Assert.IsFalse(EncodingHelper.IsText(targetFile, peekSize));
            targetFile = Path.Combine(srcDir, "Random.bin");
            Assert.IsFalse(EncodingHelper.IsText(targetFile, peekSize));
            targetFile = Path.Combine(srcDir, "Banner.zip");
            Assert.IsFalse(EncodingHelper.IsText(targetFile, peekSize));
            targetFile = Path.Combine(srcDir, "Banner.7z");
            Assert.IsFalse(EncodingHelper.IsText(targetFile, peekSize));
        }
        #endregion
    }
}
