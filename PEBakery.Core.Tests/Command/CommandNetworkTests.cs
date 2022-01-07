/*
    Copyright (C) 2017-2022 Hajin Jang
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
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace PEBakery.Core.Tests.Command
{
    [TestClass]
    public class CommandNetworkTests
    {
        #region Fields and Properties
        // ReSharper disable StringLiteralTypo
        private static string _sampleSrcFile;
        private static string _sampleFileUrl;
        private static readonly Dictionary<HashType, string> SampleDigestDict =
            new Dictionary<HashType, string>
            {
                [HashType.MD5] = "ddc79d50c92bba1ce70529c2999b2849",
                [HashType.SHA1] = "c565c60689bd51a0a0f5013290c1dfbcefd4a318",
                [HashType.SHA256] = "0f197f2578c73cf86e3b6c6f053a790dd2438102fb245694958c59f0cd1733d5",
                [HashType.SHA384] = "ff284e5211277d364a15514b465afd86c980658af814decc7782786e56547bfe420fe0590b1c27005cba8e78a46f9b3c",
                [HashType.SHA512] = "de4faf4db469c022c1731fde2a2383466e378709259d9734e64090a27b937c52a9a079867eefb6e7b0751815dc6362e6f162a6f2e6e01b7c3fa924ebe2decaee"
            };
        // ReSharper restore StringLiteralTypo

        private readonly string[] _userAgentPool =
        {
            // Edge 
            @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/96.0.4664.110 Safari/537.36 Edg/96.0.1054.62",
            // Firefox
            @"Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:91.0) Gecko/20100101 Firefox/91.0",
            @"Mozilla/5.0 (X11; Ubuntu; Linux x86_64; rv:91.0) Gecko/20100101 Firefox/91.0",
            @"Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:95.0) Gecko/20100101 Firefox/95.0",
            @"Mozilla/5.0 (X11; Ubuntu; Linux x86_64; rv:95.0) Gecko/20100101 Firefox/95.0",
            @"Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:96.0) Gecko/20100101 Firefox/96.0",
            @"Mozilla/5.0 (X11; Ubuntu; Linux x86_64; rv:96.0) Gecko/20100101 Firefox/96.0",
            // Wget
            @"Wget/1.20.3 (linux-gnu)",
            @"Wget/1.20.3",
            // curl
            @"curl/7.80.0",
        };
        #endregion

        #region Class Init/Cleanup
#pragma warning disable IDE0060
        [ClassInitialize]
        public static void ServerInit(TestContext testContext)
        {
            TestSetup.StartWebFileServer();

            _sampleSrcFile = Path.Combine(TestSetup.WebRoot, "CommandNetwork", "xz-a4.pdf");
            _sampleFileUrl = $"{TestSetup.UrlRoot}/CommandNetwork/xz-a4.pdf";
        }

        [ClassCleanup]
        public static void ServerCleanup()
        {
            _sampleSrcFile = null;
            _sampleFileUrl = null;
        }
#pragma warning restore IDE0060
        #endregion

        #region WebGet
        [TestMethod]
        [TestCategory("CommandNetwork")]
        public void WebGet()
        {
            if (EngineTests.IsOnline)
            {
                Console.WriteLine(@"Network is online. Running WebGet tests...");
            }
            else
            {
                Console.WriteLine(@"Network is offline. Ignoring some WebGet tests...");
                return;
            }

            EngineState s = EngineTests.CreateEngineState();

            // Avoid using PEBakery default user agent so often to specific homepage.
            int idx = s.Random.Next(_userAgentPool.Length);
            s.CustomUserAgent = _userAgentPool[idx];

            // IsOnline ensures access only to GitHub!
            WebGet_Http(s);
            WebGet_Compat(s);
            WebGet_TimeOut(s);
            WebGet_Referer(s);
            WebGet_HashSuccess(s);
            WebGet_HashError(s);

            if (EngineTests.IsOnline)
            {
                WebGet_Https(s);
                WebGet_NonExistDomain(s);
            }
        }

        public void WebGet_Http(EngineState s)
        {
            // FileHelper.GetTempFile ensures very high possibility that returned temp file path is unique per call.
            string destFile = FileHelper.ReserveTempFile("html");
            try
            {
                s.ReturnValue = string.Empty;

                // Try downloading index.html from GitHub.
                string srcFile = Path.Combine(TestSetup.WebRoot, "index.html");
                string rawCode = $"WebGet,\"{TestSetup.UrlRoot}/index.html\",\"{destFile}\"";
                EngineTests.Eval(s, rawCode, CodeType.WebGet, ErrorCheck.Success);

                Assert.IsTrue(File.Exists(destFile));
                Assert.IsTrue(TestSetup.FileEqual(srcFile, destFile));
                Assert.IsTrue(s.ReturnValue.Equals("200", StringComparison.Ordinal));
            }
            finally
            {
                if (File.Exists(destFile))
                    File.Delete(destFile);
            }
        }

        public void WebGet_Https(EngineState s)
        {
            // FileHelper.GetTempFile ensures very high possibility that returned temp file path is unique per call.
            string destFile = FileHelper.ReserveTempFile("html");
            try
            {
                s.ReturnValue = string.Empty;

                // Try downloading index.html from GitHub.
                string rawCode = $"WebGet,\"https://github.com\",\"{destFile}\"";
                EngineTests.Eval(s, rawCode, CodeType.WebGet, ErrorCheck.Success);

                Assert.IsTrue(File.Exists(destFile));
                Assert.IsTrue(s.ReturnValue.Equals("200", StringComparison.Ordinal));
            }
            finally
            {
                if (File.Exists(destFile))
                    File.Delete(destFile);
            }
        }

        public void WebGet_NonExistDomain(EngineState s)
        {
            // FileHelper.GetTempFile ensures very high possibility that returned temp file path is unique per call.
            string destFile = FileHelper.ReserveTempFile("html");
            try
            {
                s.ReturnValue = string.Empty;

                // Test without NOERR
                string testUrl = GenerateNeverExistUrl();
                string rawCode = $"WebGet,\"{testUrl}/Sample.txt\",\"{destFile}\"";
                EngineTests.Eval(s, rawCode, CodeType.WebGet, ErrorCheck.RuntimeError);

                Assert.IsFalse(File.Exists(destFile));
                Assert.IsTrue(s.ReturnValue.Equals("0", StringComparison.Ordinal));

                // Test with NOERR
                File.Delete(destFile);
                s.ReturnValue = string.Empty;

                rawCode = $"WebGet,\"{testUrl}/Sample.txt\",\"{destFile}\",NOERR";
                EngineTests.Eval(s, rawCode, CodeType.WebGet, ErrorCheck.Warning);

                Assert.IsFalse(File.Exists(destFile));
                Assert.IsTrue(s.ReturnValue.Equals("0", StringComparison.Ordinal));
            }
            finally
            {
                if (File.Exists(destFile))
                    File.Delete(destFile);
            }
        }

        public void WebGet_Compat(EngineState s)
        {
            // FileHelper.GetTempFile ensures very high possibility that returned temp file path is unique per call.
            string destFile = FileHelper.GetTempFile("html");
            try
            {
                File.Delete(destFile);
                s.ReturnValue = string.Empty;

                string srcFile = Path.Combine(TestSetup.WebRoot, "index.html");
                string rawCode = $"WebGet,\"{TestSetup.UrlRoot}/index.html\",\"{destFile}\"";
                EngineTests.Eval(s, rawCode, CodeType.WebGet, ErrorCheck.Success, new CompatOption { DisableExtendedSectionParams = true });

                Assert.IsTrue(File.Exists(destFile));
                Assert.IsTrue(TestSetup.FileEqual(srcFile, destFile));
                Assert.IsTrue(s.ReturnValue.Length == 0);
            }
            finally
            {
                if (File.Exists(destFile))
                    File.Delete(destFile);
            }
        }

        public void WebGet_TimeOut(EngineState s)
        {
            // FileHelper.GetTempFile ensures very high possibility that returned temp file path is unique per call.
            string destFile = FileHelper.ReserveTempFile("html");
            try
            {
                string srcFile = Path.Combine(TestSetup.WebRoot, "index.html");

                // Download index.html (1/3) - Success
                s.ReturnValue = string.Empty;

                string rawCode = $"WebGet,\"{TestSetup.UrlRoot}/index.html\",\"{destFile}\",TimeOut=30";
                EngineTests.Eval(s, rawCode, CodeType.WebGet, ErrorCheck.Success);

                Assert.IsTrue(File.Exists(destFile));
                Assert.IsTrue(TestSetup.FileEqual(srcFile, destFile));
                Assert.IsTrue(s.ReturnValue.Equals("200", StringComparison.Ordinal));

                // Download index.html (2/3) - Fail
                File.Delete(destFile);
                s.ReturnValue = string.Empty;

                rawCode = $"WebGet,\"{TestSetup.UrlRoot}/index.html\",\"{destFile}\",TimeOut=0";
                EngineTests.Eval(s, rawCode, CodeType.WebGet, ErrorCheck.RuntimeError);

                Assert.IsFalse(File.Exists(destFile));
                Assert.IsTrue(s.ReturnValue.Length == 0);

                // Download index.html (3/3) - Fail
                File.Delete(destFile);
                s.ReturnValue = string.Empty;

                rawCode = $"WebGet,\"{TestSetup.UrlRoot}/index.html\",\"{destFile}\",TimeOut=-10";
                EngineTests.Eval(s, rawCode, CodeType.WebGet, ErrorCheck.RuntimeError);

                Assert.IsFalse(File.Exists(destFile));
                Assert.IsTrue(s.ReturnValue.Length == 0);
            }
            finally
            {
                if (File.Exists(destFile))
                    File.Delete(destFile);
            }
        }

        public void WebGet_Referer(EngineState s)
        {
            // FileHelper.GetTempFile ensures very high possibility that returned temp file path is unique per call.
            string destFile = FileHelper.ReserveTempFile("html");
            try
            {
                string srcFile = Path.Combine(TestSetup.WebRoot, "index.html");

                // Download index.html (1/2) - Success
                s.ReturnValue = string.Empty;

                string rawCode = $"WebGet,\"{TestSetup.UrlRoot}/index.html\",\"{destFile}\",Referer=https://www.google.com";
                EngineTests.Eval(s, rawCode, CodeType.WebGet, ErrorCheck.Success);

                Assert.IsTrue(File.Exists(destFile));
                Assert.IsTrue(TestSetup.FileEqual(srcFile, destFile));
                Assert.IsTrue(s.ReturnValue.Equals("200", StringComparison.Ordinal));

                // Download index.html (2/2) - Fail (Invalid referer uri)
                File.Delete(destFile);
                s.ReturnValue = string.Empty;

                rawCode = $"WebGet,\"{TestSetup.UrlRoot}/index.html\",\"{destFile}\",Referer=www.google.com";
                EngineTests.Eval(s, rawCode, CodeType.WebGet, ErrorCheck.RuntimeError);

                Assert.IsFalse(File.Exists(destFile));
                Assert.IsTrue(s.ReturnValue.Equals("0", StringComparison.Ordinal));
            }
            finally
            {
                if (File.Exists(destFile))
                    File.Delete(destFile);
            }
        }

        public void WebGet_UserAgent(EngineState s)
        {
            // FileHelper.GetTempFile ensures very high possibility that returned temp file path is unique per call.
            string destFile = FileHelper.ReserveTempFile("html");
            try
            {
                string srcFile = Path.Combine(TestSetup.WebRoot, "index.html");

                // Download index.html - Success
                s.ReturnValue = string.Empty;

                string rawCode = $"WebGet,\"{TestSetup.UrlRoot}/index.html\",\"{destFile}\",\"UserAgent=Wget/1.20.3 (linux-gnu)\"";
                EngineTests.Eval(s, rawCode, CodeType.WebGet, ErrorCheck.Success);

                Assert.IsTrue(File.Exists(destFile));
                Assert.IsTrue(TestSetup.FileEqual(srcFile, destFile));
                Assert.IsTrue(s.ReturnValue.Equals("200", StringComparison.Ordinal));
            }
            finally
            {
                if (File.Exists(destFile))
                    File.Delete(destFile);
            }
        }

        public void WebGet_HashSuccess(EngineState s)
        {
            foreach (HashType hashType in SampleDigestDict.Keys)
            {
                string destFile = FileHelper.ReserveTempFile("html");
                try
                {
                    s.ReturnValue = string.Empty;

                    string rawCode = $"WebGet,\"{_sampleFileUrl}\",\"{destFile}\",{hashType}={SampleDigestDict[hashType]}";
                    EngineTests.Eval(s, rawCode, CodeType.WebGet, ErrorCheck.Success);

                    Assert.IsTrue(File.Exists(destFile));
                    Assert.IsTrue(TestSetup.FileEqual(_sampleSrcFile, destFile));
                    Assert.IsTrue(s.ReturnValue.Equals("200", StringComparison.Ordinal));
                }
                finally
                {
                    if (File.Exists(destFile))
                        File.Delete(destFile);
                }
            }
        }

        public void WebGet_HashError(EngineState s)
        {
            string destFile = FileHelper.ReserveTempFile("html");
            try
            {
                // Try different MD5 digest
                s.ReturnValue = string.Empty;

                string rawCode = $"WebGet,\"{_sampleFileUrl}\",\"{destFile}\",MD5=00000000000000000000000000000000";
                EngineTests.Eval(s, rawCode, CodeType.WebGet, ErrorCheck.RuntimeError);

                Assert.IsFalse(File.Exists(destFile));
                Assert.IsTrue(s.ReturnValue.Equals("1", StringComparison.Ordinal));

                // Try invalid MD5 digest
                File.Delete(destFile);
                s.ReturnValue = string.Empty;

                rawCode = $"WebGet,\"{_sampleFileUrl}\",\"{destFile}\",MD5=0";
                EngineTests.Eval(s, rawCode, CodeType.WebGet, ErrorCheck.RuntimeError);

                Assert.IsFalse(File.Exists(destFile));
                Assert.IsTrue(s.ReturnValue.Length == 0);
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
            const string baseUrl = "절대로존재하지않는無도메인";
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
