/*
    Copyright (C) 2017 Hajin Jang
    Licensed under GPL 3.0
 
    PEBakery is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Core;
using System.IO;
using PEBakery.Helper;
using System.Text;

namespace PEBakery.Tests.Core.Command
{
    [TestClass]
    public class CommandHashTests
    {
        #region Hash
        [TestCategory("Command")]
        [TestCategory("CommandHash")]
        [TestMethod]
        public void Hash_MD5()
        { // Hash,<HashType>,<FilePath>,<DestVar>
            string tempFile = CommandHashTests.SampleText();

            string rawCode = $"Hash,MD5,{tempFile},%Dest%";
            EngineState s = EngineTests.Eval(rawCode, CodeType.Hash, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            string comp = "1179cf94187d2d2f94010a8d39099543";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));

            File.Delete(tempFile);
        }

        [TestCategory("Command")]
        [TestCategory("CommandHash")]
        [TestMethod]
        public void Hash_SHA1()
        { // Hash,<HashType>,<FilePath>,<DestVar>
            string tempFile = CommandHashTests.SampleText();

            string rawCode = $"Hash,SHA1,{tempFile},%Dest%";
            EngineState s = EngineTests.Eval(rawCode, CodeType.Hash, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            string comp = "0aaac8883f1c8dd48dbf974299a9422f1ab437ee";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));

            File.Delete(tempFile);
        }

        [TestCategory("Command")]
        [TestCategory("CommandHash")]
        [TestMethod]
        public void Hash_SHA256()
        { // Hash,<HashType>,<FilePath>,<DestVar>
            string tempFile = CommandHashTests.SampleText();

            string rawCode = $"Hash,SHA256,{tempFile},%Dest%";
            EngineState s = EngineTests.Eval(rawCode, CodeType.Hash, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            string comp = "3596bc5a263736c9d5b9a06e85a66ed2a866b457a44e5ed8548e504ca5599772";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));

            File.Delete(tempFile);
        }

        [TestCategory("Command")]
        [TestCategory("CommandHash")]
        [TestMethod]
        public void Hash_SHA384()
        { // Hash,<HashType>,<FilePath>,<DestVar>
            string tempFile = CommandHashTests.SampleText();

            string rawCode = $"Hash,SHA384,{tempFile},%Dest%";
            EngineState s = EngineTests.Eval(rawCode, CodeType.Hash, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            string comp = "e068a3ac0b4ab4b37306dc354af6b8a4c89ef3fbbf1db969ec6d6a4281f1ab1f472fcd7bc2f16c0cf41c1991056846a6";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));

            File.Delete(tempFile);
        }

        [TestCategory("Command")]
        [TestCategory("CommandHash")]
        [TestMethod]
        public void Hash_SHA512()
        { // Hash,<HashType>,<FilePath>,<DestVar>
            string tempFile = CommandHashTests.SampleText();

            string rawCode = $"Hash,SHA512,{tempFile},%Dest%";
            EngineState s = EngineTests.Eval(rawCode, CodeType.Hash, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            string comp = "f5829cb5e052ab5ef6820630fd992acabb798512d21b5c5295fb81b88b74f3812863c0804e730f26e166b51d77eb5f1de200fd75913278522da78fbb269600cc";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));

            File.Delete(tempFile);
        }
        #endregion

        #region Utility
        internal static string SampleText()
        {
            string tempFile = Path.GetTempFileName();
            FileHelper.WriteTextBOM(tempFile, Encoding.UTF8);
            using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
            {
                w.Write("Hello\r\nHash\r\nPEBakery\r\nUnitTest");
            }

            return tempFile;
        }
        #endregion
    }
}
