/*
    Copyright (C) 2017-2018 Hajin Jang
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

    Additional permission under GNU GPL version 3 section 7

    If you modify this program, or any covered work, by linking
    or combining it with external libraries, containing parts
    covered by the terms of various license, the licensors of
    this program grant you additional permission to convey the
    resulting work. An external library is a library which is
    not derived from or based on this program. 
*/

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using PEBakery.Core;
using PEBakery.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PEBakery.Tests.Core.Command
{
    [TestClass]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class CommandHashTests
    {
        #region Hash
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandHash")]
        public void MD5()
        {
            string tempFile = SampleText();
            string rawCode = $"Hash,MD5,{tempFile},%Dest%";
            EngineState s = EngineTests.CreateEngineState();
            EngineTests.Eval(s, rawCode, CodeType.Hash, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            const string comp = "1179cf94187d2d2f94010a8d39099543";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));

            File.Delete(tempFile);
        }

        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandHash")]
        public void SHA1()
        {
            string tempFile = SampleText();
            string rawCode = $"Hash,SHA1,{tempFile},%Dest%";
            EngineState s = EngineTests.CreateEngineState();
            EngineTests.Eval(s, rawCode, CodeType.Hash, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            const string comp = "0aaac8883f1c8dd48dbf974299a9422f1ab437ee";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));

            File.Delete(tempFile);
        }

        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandHash")]
        public void SHA256()
        {
            string tempFile = SampleText();
            string rawCode = $"Hash,SHA256,{tempFile},%Dest%";
            EngineState s = EngineTests.CreateEngineState();
            EngineTests.Eval(s, rawCode, CodeType.Hash, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            const string comp = "3596bc5a263736c9d5b9a06e85a66ed2a866b457a44e5ed8548e504ca5599772";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));

            File.Delete(tempFile);
        }

        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandHash")]
        public void SHA384()
        {
            string tempFile = SampleText();
            string rawCode = $"Hash,SHA384,{tempFile},%Dest%";
            EngineState s = EngineTests.CreateEngineState();
            EngineTests.Eval(s, rawCode, CodeType.Hash, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            const string comp = "e068a3ac0b4ab4b37306dc354af6b8a4c89ef3fbbf1db969ec6d6a4281f1ab1f472fcd7bc2f16c0cf41c1991056846a6";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));

            File.Delete(tempFile);
        }

        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandHash")]
        public void SHA512()
        {
            string tempFile = CommandHashTests.SampleText();
            string rawCode = $"Hash,SHA512,{tempFile},%Dest%";
            EngineState s = EngineTests.CreateEngineState();
            EngineTests.Eval(s, rawCode, CodeType.Hash, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            const string comp = "f5829cb5e052ab5ef6820630fd992acabb798512d21b5c5295fb81b88b74f3812863c0804e730f26e166b51d77eb5f1de200fd75913278522da78fbb269600cc";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));

            File.Delete(tempFile);
        }
        #endregion

        #region Utility
        internal static string SampleText()
        {
            string tempFile = Path.GetTempFileName();
            FileHelper.WriteTextBom(tempFile, Encoding.UTF8);
            using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.UTF8))
            {
                w.Write("Hello\r\nHash\r\nPEBakery\r\nUnitTest");
            }

            return tempFile;
        }
        #endregion
    }
}
