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
                Assert.AreEqual(EncodingHelper.DetectBom(tempFile), Encoding.Default);

                // UTF-16 LE
                EncodingHelper.WriteTextBom(tempFile, Encoding.Unicode);
                Assert.AreEqual(EncodingHelper.DetectBom(tempFile), Encoding.Unicode);

                // UTF-16 BE
                EncodingHelper.WriteTextBom(tempFile, Encoding.BigEndianUnicode);
                Assert.AreEqual(EncodingHelper.DetectBom(tempFile), Encoding.BigEndianUnicode);

                // UTF-8
                EncodingHelper.WriteTextBom(tempFile, Encoding.UTF8);
                Assert.AreEqual(EncodingHelper.DetectBom(tempFile), Encoding.UTF8);
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

            void TestTemplate(string fileName, bool result)
            {
                string targetFile = Path.Combine(srcDir, fileName);
                Assert.AreEqual(result, EncodingHelper.IsText(targetFile, peekSize));
                Console.WriteLine($"[TEST] {targetFile} pass");
            }

            void LogTemplate(string fileName)
            {
                string targetFile = Path.Combine(srcDir, fileName);
                if (EncodingHelper.IsText(targetFile, peekSize))
                    Console.WriteLine($"[LOG] {targetFile} is a text");
                else
                    Console.WriteLine($"[LOG] {targetFile} is a binary");
            }

            // Print out system's default locale, for debug.
            Console.WriteLine($"System Default Locale CodePage = {EncodingHelper.DefaultAnsi.CodePage}");

            // Test ASCII
            // Cannot test non-ASCII ANSI encoding, results will change in various environments
            Console.WriteLine("* Testing ASCII...");
            LogTemplate("Banner.svg");

            // UTF-16 LE
            Console.WriteLine("* Testing UTF-16 LE...");
            TestTemplate("UTF16LE.txt", true);

            // UTF-16 BE
            Console.WriteLine("* Testing UTF-16 BE...");
            TestTemplate("UTF16BE.txt", true);

            // UTF-8
            Console.WriteLine("* Testing UTF-8...");
            TestTemplate("UTF8.txt", true);

            // Binary
            // Result can change across locale, due to how EncodingHelper.IsText works.
            // (Check if a buffer contains unused area of system default encoding)
            // To counter it, it also use AutoIt.Common.TextEncodingDetect.
            Console.WriteLine("* Testing Binary...");
            LogTemplate("Zero.bin");
            LogTemplate("Type3.pdf");
            LogTemplate("Random.bin");
            LogTemplate("Banner.zip");
            LogTemplate("Banner.7z");
            
        }
        #endregion
    }
}
