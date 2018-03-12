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
using System.Linq;
using System.Net.NetworkInformation;

namespace PEBakery.Tests.Core.Command
{
    [TestClass]
    public class CommandNetworkTests
    {
        #region WebGet
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandNetwork")]
        public void Network_WebGet()
        { 
            EngineState s = EngineTests.CreateEngineState();

            WebGet_1(s);
            WebGet_2(s);
            WebGet_3(s);
            WebGet_MD5(s);
            WebGet_SHA1(s);
            WebGet_SHA256(s);
            WebGet_SHA384(s);
            WebGet_SHA512(s);
            WebGet_HashError(s);
        }

        public void WebGet_1(EngineState s)
        {
            string tempSrc = CommandHashTests.SampleText();
            string tempDest = Path.GetTempFileName();
            File.Delete(tempDest);

            try
            {
                Uri fileUri = new Uri(tempSrc);
                string rawCode = $"WebGet,\"{fileUri.AbsoluteUri}\",\"{tempDest}\"";
                EngineTests.Eval(s, rawCode, CodeType.WebGet, ErrorCheck.Success);

                Assert.IsTrue(File.Exists(tempDest));
                Assert.IsTrue(s.Variables["StatusCode"].Equals("200", StringComparison.Ordinal));
            }
            finally
            {
                if (File.Exists(tempSrc))
                    File.Delete(tempSrc);
                if (File.Exists(tempDest))
                    File.Delete(tempDest);
            }
        }

        public void WebGet_2(EngineState s)
        {
            string tempSrc = Path.GetTempFileName();
            string tempDest = Path.GetTempFileName();

            try
            {
                File.Delete(tempSrc);
                File.Delete(tempDest);

                Uri fileUri = new Uri(tempSrc);
                string rawCode = $"WebGet,\"{fileUri.AbsoluteUri}\",\"{tempDest}\"";
                EngineTests.Eval(s, rawCode, CodeType.WebGet, ErrorCheck.Error);

                Assert.IsFalse(File.Exists(tempDest));
                Assert.IsTrue(s.Variables["StatusCode"].Equals("0", StringComparison.Ordinal));
            }
            finally
            {
                if (File.Exists(tempSrc))
                    File.Delete(tempSrc);
                if (File.Exists(tempDest))
                    File.Delete(tempDest);
            }
        }

        public void WebGet_3(EngineState s)
        {
            string tempSrc = Path.GetTempFileName();
            string tempDest = Path.GetTempFileName();

            try
            {
                File.Delete(tempSrc);
                File.Delete(tempDest);

                Uri fileUri = new Uri(tempSrc);
                string rawCode = $"WebGet,\"{fileUri.AbsoluteUri}\",\"{tempDest}\",NOERR";
                EngineTests.Eval(s, rawCode, CodeType.WebGet, ErrorCheck.Warning);

                Assert.IsFalse(File.Exists(tempDest));
                Assert.IsTrue(s.Variables["StatusCode"].Equals("0", StringComparison.Ordinal));
            }
            finally
            {
                if (File.Exists(tempSrc))
                    File.Delete(tempSrc);
                if (File.Exists(tempDest))
                    File.Delete(tempDest);
            }
        }

        public void WebGet_MD5(EngineState s)
        {
            string tempSrc = CommandHashTests.SampleText();
            string tempDest = Path.GetTempFileName();
            File.Delete(tempDest);

            try
            {
                Uri fileUri = new Uri(tempSrc);
                string rawCode = $"WebGet,\"{fileUri.AbsoluteUri}\",\"{tempDest}\",MD5=1179cf94187d2d2f94010a8d39099543";
                EngineTests.Eval(s, rawCode, CodeType.WebGet, ErrorCheck.Success);

                Assert.IsTrue(File.Exists(tempDest));
                Assert.IsTrue(s.Variables["StatusCode"].Equals("200", StringComparison.Ordinal));
            }
            finally
            {
                if (File.Exists(tempSrc))
                    File.Delete(tempSrc);
                if (File.Exists(tempDest))
                    File.Delete(tempDest);
            }
        }

        public void WebGet_SHA1(EngineState s)
        {
            string tempSrc = CommandHashTests.SampleText();
            string tempDest = Path.GetTempFileName();
            File.Delete(tempDest);

            try
            {
                Uri fileUri = new Uri(tempSrc);
                string rawCode = $"WebGet,\"{fileUri.AbsoluteUri}\",\"{tempDest}\",SHA1=0aaac8883f1c8dd48dbf974299a9422f1ab437ee";
                EngineTests.Eval(s, rawCode, CodeType.WebGet, ErrorCheck.Success);

                Assert.IsTrue(File.Exists(tempDest));
                Assert.IsTrue(s.Variables["StatusCode"].Equals("200", StringComparison.Ordinal));
            }
            finally
            {
                if (File.Exists(tempSrc))
                    File.Delete(tempSrc);
                if (File.Exists(tempDest))
                    File.Delete(tempDest);
            }
        }

        public void WebGet_SHA256(EngineState s)
        {
            string tempSrc = CommandHashTests.SampleText();
            string tempDest = Path.GetTempFileName();
            File.Delete(tempDest);

            try
            {
                Uri fileUri = new Uri(tempSrc);
                string rawCode = $"WebGet,\"{fileUri.AbsoluteUri}\",\"{tempDest}\",SHA256=3596bc5a263736c9d5b9a06e85a66ed2a866b457a44e5ed8548e504ca5599772";
                EngineTests.Eval(s, rawCode, CodeType.WebGet, ErrorCheck.Success);

                Assert.IsTrue(File.Exists(tempDest));
                Assert.IsTrue(s.Variables["StatusCode"].Equals("200", StringComparison.Ordinal));
            }
            finally
            {
                if (File.Exists(tempSrc))
                    File.Delete(tempSrc);
                if (File.Exists(tempDest))
                    File.Delete(tempDest);
            }
        }

        public void WebGet_SHA384(EngineState s)
        {
            string tempSrc = CommandHashTests.SampleText();
            string tempDest = Path.GetTempFileName();
            File.Delete(tempDest);

            try
            {
                Uri fileUri = new Uri(tempSrc);
                string rawCode = $"WebGet,\"{fileUri.AbsoluteUri}\",\"{tempDest}\",SHA384=e068a3ac0b4ab4b37306dc354af6b8a4c89ef3fbbf1db969ec6d6a4281f1ab1f472fcd7bc2f16c0cf41c1991056846a6";
                EngineTests.Eval(s, rawCode, CodeType.WebGet, ErrorCheck.Success);

                Assert.IsTrue(File.Exists(tempDest));
                Assert.IsTrue(s.Variables["StatusCode"].Equals("200", StringComparison.Ordinal));
            }
            finally
            {
                if (File.Exists(tempSrc))
                    File.Delete(tempSrc);
                if (File.Exists(tempDest))
                    File.Delete(tempDest);
            }
        }

        public void WebGet_SHA512(EngineState s)
        {
            string tempSrc = CommandHashTests.SampleText();
            string tempDest = Path.GetTempFileName();
            File.Delete(tempDest);

            try
            {
                Uri fileUri = new Uri(tempSrc);
                string rawCode = $"WebGet,\"{fileUri.AbsoluteUri}\",\"{tempDest}\",SHA512=f5829cb5e052ab5ef6820630fd992acabb798512d21b5c5295fb81b88b74f3812863c0804e730f26e166b51d77eb5f1de200fd75913278522da78fbb269600cc";
                EngineTests.Eval(s, rawCode, CodeType.WebGet, ErrorCheck.Success);

                Assert.IsTrue(File.Exists(tempDest));
                Assert.IsTrue(s.Variables["StatusCode"].Equals("200", StringComparison.Ordinal));
            }
            finally
            {
                if (File.Exists(tempSrc))
                    File.Delete(tempSrc);
                if (File.Exists(tempDest))
                    File.Delete(tempDest);
            }
        }

        public void WebGet_HashError(EngineState s)
        {
            string tempSrc = CommandHashTests.SampleText();
            string tempDest = Path.GetTempFileName();
            File.Delete(tempDest);

            try
            {
                Uri fileUri = new Uri(tempSrc);
                string rawCode = $"WebGet,\"{fileUri.AbsoluteUri}\",\"{tempDest}\",MD5=00000000000000000000000000000000";
                EngineTests.Eval(s, rawCode, CodeType.WebGet, ErrorCheck.Error);

                Assert.IsFalse(File.Exists(tempDest));
                Assert.IsTrue(s.Variables["StatusCode"].Equals("1", StringComparison.Ordinal));
            }
            finally
            {
                if (File.Exists(tempSrc))
                    File.Delete(tempSrc);
                if (File.Exists(tempDest))
                    File.Delete(tempDest);
            }
        }
        #endregion
    }
}
