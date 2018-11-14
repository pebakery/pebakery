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

using System;
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

            void Template(string fileName, bool result)
            {
                string targetFile = Path.Combine(srcDir, fileName);
                Assert.AreEqual(result, EncodingHelper.IsText(targetFile, peekSize));
                Console.WriteLine($"{targetFile} test passed");
            }

            // Test ANSI
            // Cannot test non-ASCII ANSI encoding, will differ in various environment
            Template("Banner.svg", true);
            Console.WriteLine("ASCII test passed");

            // UTF-16 LE
            Template("UTF16LE.txt", true);
            Console.WriteLine("UTF-16 LE test passed");

            // UTF-16 BE
            Template("UTF16BE.txt", true);
            Console.WriteLine("UTF-16 BE test passed");

            // UTF-8
            Template("UTF8.txt", true);
            Console.WriteLine("UTF8 test passed");

            // Binary
            // Result can change across locale, due to how EncodingHelper.IsText works.
            // (Check if a buffer contains unused area of system default encoding)
            // To counter it, it also use AutoIt.Common.TextEncodingDetect.
            Template("Zero.bin", false);
            Template("Type3.pdf", false);
            Template("Random.bin", false);
            Template("Banner.zip", false);
            Template("Banner.7z", false);
            Console.WriteLine("Binary test passed");
        }
        #endregion
    }
}
