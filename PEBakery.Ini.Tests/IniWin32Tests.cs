/*
    Copyright (C) 2019-2023 Hajin Jang
 
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
using PEBakery.Helper;
using System;
using System.IO;

namespace PEBakery.Ini.Tests
{
    [TestClass]
    [TestCategory("PEBakery.Ini")]
    public class IniWin32Tests
    {
        private const string SampleDirName = "IniWin32";

        #region WriteKey
        [TestMethod]
        public void WriteKey()
        {
            bool DeleteKeyString(string destFile) => IniWin32.WriteKey(destFile, "Section3", "K", "V");
            bool DeleteKeyStruct(string destFile) => IniWin32.WriteKey(destFile, new IniKey("Section3", "K", "V"));
            WriteTemplate("Before.ini", "AfterWriteKey.ini", DeleteKeyString);
            WriteTemplate("Before.ini", "AfterWriteKey.ini", DeleteKeyStruct);
        }
        #endregion

        #region DeleteKey
        [TestMethod]
        public void DeleteKey()
        {
            bool DeleteKeyString(string destFile) => IniWin32.DeleteKey(destFile, "Section1", "A");
            bool DeleteKeyStruct(string destFile) => IniWin32.DeleteKey(destFile, new IniKey("Section1", "A"));
            WriteTemplate("Before.ini", "AfterDeleteKey.ini", DeleteKeyString);
            WriteTemplate("Before.ini", "AfterDeleteKey.ini", DeleteKeyStruct);
        }
        #endregion

        #region DeleteSection
        [TestMethod]
        public void DeleteSection()
        {
            bool DeleteSectionString(string destFile) => IniWin32.DeleteSection(destFile, "Section1");
            bool DeleteSectionStruct(string destFile) => IniWin32.DeleteSection(destFile, new IniKey("Section1"));
            WriteTemplate("Before.ini", "AfterDeleteSection.ini", DeleteSectionString);
            WriteTemplate("Before.ini", "AfterDeleteSection.ini", DeleteSectionStruct);
        }
        #endregion

        #region WriteTemplate
        static void WriteTemplate(string srcFileName, string expectFileName, Func<string, bool> testFunc)
        {
            string srcFilePath = Path.Combine(TestSetup.SampleDir, SampleDirName, srcFileName);
            string expectFilePath = Path.Combine(TestSetup.SampleDir, SampleDirName, expectFileName);
            string destFilePath = FileHelper.GetTempFile(".ini");
            try
            {
                File.Copy(srcFilePath, destFilePath, true);
                Assert.IsTrue(testFunc.Invoke(destFilePath));

                byte[] destDigest = HashHelper.GetHash(HashType.SHA256, destFilePath);
                byte[] expectDigest = HashHelper.GetHash(HashType.SHA256, expectFilePath);
                Assert.IsTrue(HashHelper.IsHashBytesEqual(destDigest, expectDigest));
            }
            finally
            {
                if (File.Exists(destFilePath))
                    File.Delete(destFilePath);
            }
        }
        #endregion
    }
}
