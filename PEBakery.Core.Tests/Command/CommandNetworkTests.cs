/*
    Copyright (C) 2017-2019 Hajin Jang
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Helper;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace PEBakery.Core.Tests.Command
{
    [TestClass]
    public class CommandNetworkTests
    {
        #region WebGet
        private const string GnuHelloSigUrl = "https://ftp.gnu.org/gnu/hello/hello-2.10.tar.gz.sig";
        private const string GnuHelloSigMD5 = "e6074bb23a0f184e00fdfb5c546b3bc2";
        private const string GnuHelloSigSHA1 = "9dc7a584db576910856ac7aa5cffbaeefe9cf427";
        private const string GnuHelloSigSHA256 = "4ea69de913428a4034d30dcdcb34ab84f5c4a76acf9040f3091f0d3fac411b60";
        private const string GnuHelloSigSHA384 = "a4f4a418eb3c6d94bf5d6e2542055df12ef9f503a28a4d5ee02fedc56a5c6d11975e76327274c9a3e386cc39618e4445";
        private const string GnuHelloSigSHA512 = "9584e91bc471c69a1e0ecb90fc69649170c0b43c966a3b932cf9c87c12c8b33f142af06520f61039189691a3e16b826f4e79dba17b7174c17f6bd6c77472c18c";

        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandNetwork")]
        public void WebGet()
        {
            if (EngineTests.IsOnline)
            {
                Console.WriteLine(@"Network is online. Running WebGet tests...");
            }
            else
            {
                Console.WriteLine(@"Network is offline. Ignoring WebGet tests...");
                return;
            }

            EngineState s = EngineTests.CreateEngineState();

            // IsOnline ensures access to only GitHub!
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
            // FileHelper.GetTempFile ensures very high possibility that returned temp file path is unique per call.
            string destFile = FileHelper.GetTempFile("html");
            try
            {
                File.Delete(destFile);

                // Try downloading index.html from GitHub.
                string rawCode = $"WebGet,\"https://github.com\",\"{destFile}\"";
                EngineTests.Eval(s, rawCode, CodeType.WebGet, ErrorCheck.Success);

                Assert.IsTrue(File.Exists(destFile));
                Assert.IsTrue(s.Variables["StatusCode"].Equals("200", StringComparison.Ordinal));
            }
            finally
            {
                if (File.Exists(destFile))
                    File.Delete(destFile);
            }
        }

        public void WebGet_2(EngineState s)
        {
            // FileHelper.GetTempFile ensures very high possibility that returned temp file path is unique per call.
            string destFile = FileHelper.GetTempFile("html");
            try
            {
                File.Delete(destFile);

                string testUrl = GenerateNeverExistUrl();
                string rawCode = $"WebGet,\"{testUrl}/Sample.txt\",\"{destFile}\"";
                EngineTests.Eval(s, rawCode, CodeType.WebGet, ErrorCheck.Error);

                Assert.IsFalse(File.Exists(destFile));
                Assert.IsTrue(s.Variables["StatusCode"].Equals("0", StringComparison.Ordinal));
            }
            finally
            {
                if (File.Exists(destFile))
                    File.Delete(destFile);
            }
        }

        public void WebGet_3(EngineState s)
        {
            // FileHelper.GetTempFile ensures very high possibility that returned temp file path is unique per call.
            string destFile = FileHelper.GetTempFile("html");
            try
            {
                File.Delete(destFile);

                string testUrl = GenerateNeverExistUrl();
                string rawCode = $"WebGet,\"{testUrl}/Sample.txt\",\"{destFile}\",NOERR";
                EngineTests.Eval(s, rawCode, CodeType.WebGet, ErrorCheck.Warning);

                Assert.IsFalse(File.Exists(destFile));
                Assert.IsTrue(s.Variables["StatusCode"].Equals("0", StringComparison.Ordinal));
            }
            finally
            {
                if (File.Exists(destFile))
                    File.Delete(destFile);
            }
        }

        public void WebGet_MD5(EngineState s)
        {
            // FileHelper.GetTempFile ensures very high possibility that returned temp file path is unique per call.
            string destFile = FileHelper.GetTempFile("html");
            try
            {
                File.Delete(destFile);

                string rawCode = $"WebGet,\"{GnuHelloSigUrl}\",\"{destFile}\",MD5={GnuHelloSigMD5}";
                EngineTests.Eval(s, rawCode, CodeType.WebGet, ErrorCheck.Success);

                Assert.IsTrue(File.Exists(destFile));
                Assert.IsTrue(s.Variables["StatusCode"].Equals("200", StringComparison.Ordinal));
            }
            finally
            {
                if (File.Exists(destFile))
                    File.Delete(destFile);
            }
        }

        public void WebGet_SHA1(EngineState s)
        {
            // FileHelper.GetTempFile ensures very high possibility that returned temp file path is unique per call.
            string destFile = FileHelper.GetTempFile("html");
            try
            {
                File.Delete(destFile);

                string rawCode = $"WebGet,\"{GnuHelloSigUrl}\",\"{destFile}\",SHA1={GnuHelloSigSHA1}";
                EngineTests.Eval(s, rawCode, CodeType.WebGet, ErrorCheck.Success);

                Assert.IsTrue(File.Exists(destFile));
                Assert.IsTrue(s.Variables["StatusCode"].Equals("200", StringComparison.Ordinal));
            }
            finally
            {
                if (File.Exists(destFile))
                    File.Delete(destFile);
            }
        }

        public void WebGet_SHA256(EngineState s)
        {
            // FileHelper.GetTempFile ensures very high possibility that returned temp file path is unique per call.
            string destFile = FileHelper.GetTempFile("html");
            try
            {
                File.Delete(destFile);

                string rawCode = $"WebGet,\"{GnuHelloSigUrl}\",\"{destFile}\",SHA256={GnuHelloSigSHA256}";
                EngineTests.Eval(s, rawCode, CodeType.WebGet, ErrorCheck.Success);

                Assert.IsTrue(File.Exists(destFile));
                Assert.IsTrue(s.Variables["StatusCode"].Equals("200", StringComparison.Ordinal));
            }
            finally
            {
                if (File.Exists(destFile))
                    File.Delete(destFile);
            }
        }

        public void WebGet_SHA384(EngineState s)
        {
            // FileHelper.GetTempFile ensures very high possibility that returned temp file path is unique per call.
            string destFile = FileHelper.GetTempFile("html");
            try
            {
                File.Delete(destFile);

                string rawCode = $"WebGet,\"{GnuHelloSigUrl}\",\"{destFile}\",SHA384={GnuHelloSigSHA384}";
                EngineTests.Eval(s, rawCode, CodeType.WebGet, ErrorCheck.Success);

                Assert.IsTrue(File.Exists(destFile));
                Assert.IsTrue(s.Variables["StatusCode"].Equals("200", StringComparison.Ordinal));
            }
            finally
            {
                if (File.Exists(destFile))
                    File.Delete(destFile);
            }
        }

        public void WebGet_SHA512(EngineState s)
        {
            // FileHelper.GetTempFile ensures very high possibility that returned temp file path is unique per call.
            string destFile = FileHelper.GetTempFile("html");
            try
            {
                File.Delete(destFile);

                string rawCode = $"WebGet,\"{GnuHelloSigUrl}\",\"{destFile}\",SHA512={GnuHelloSigSHA512}";
                EngineTests.Eval(s, rawCode, CodeType.WebGet, ErrorCheck.Success);

                Assert.IsTrue(File.Exists(destFile));
                Assert.IsTrue(s.Variables["StatusCode"].Equals("200", StringComparison.Ordinal));
            }
            finally
            {
                if (File.Exists(destFile))
                    File.Delete(destFile);
            }
        }

        public void WebGet_HashError(EngineState s)
        {
            // FileHelper.GetTempFile ensures very high possibility that returned temp file path is unique per call.
            string destFile = FileHelper.GetTempFile("html");
            try
            {
                File.Delete(destFile);

                string rawCode = $"WebGet,\"{GnuHelloSigUrl}\",\"{destFile}\",MD5=00000000000000000000000000000000";
                EngineTests.Eval(s, rawCode, CodeType.WebGet, ErrorCheck.Error);

                Assert.IsFalse(File.Exists(destFile));
                Assert.IsTrue(s.Variables["StatusCode"].Equals("1", StringComparison.Ordinal));
            }
            finally
            {
                if (File.Exists(destFile))
                    File.Delete(destFile);
            }
        }
        #endregion

        #region Utility
        private static string GenerateNeverExistUrl()
        {
            // Let's try a domain which never exists.
            // No one wants to create a domain named 'Never-exist-domain + rand hex' in Korean, right?
            const string baseUrl = "절대로존재하지않는도메인";
            Random rand = new Random();
            string testUrl = null;
            int counter = 0;

            // Very unlikely, but if a villain already created 16 randomized domains, then give up. Test will fail.
            while (testUrl == null && counter < 16)
            {
                int repeat = rand.Next(1, 4);
                StringBuilder b = new StringBuilder(8);
                b.Append(baseUrl);
                b.Append('_');
                for (int i = 0; i < repeat; i++)
                    b.Append(rand.Next().ToString("X8"));

                string url = b.ToString();

                try
                {    
                    Dns.GetHostEntry(url);

                    // Generated url already exists.
                    // Let's try again with another randomized numbers...
                    counter += 1;
                }
                catch (SocketException)
                {
                    // Great! 
                    testUrl = url; 
                }
            }

            if (testUrl == null)
                return null;
            else
                return "https://" + testUrl;
        }
        #endregion
    }
}
