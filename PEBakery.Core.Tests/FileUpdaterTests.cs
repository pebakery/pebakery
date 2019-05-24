/*
    Copyright (C) 2019 Hajin Jang
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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Helper;

namespace PEBakery.Core.Tests
{
    [TestClass]
    public class FileUpdaterTests
    {
        #region Fields and Properties
        public const int ServerPort = 8380;
        private static string _serverUrl;
        private static string _webRoot;

        private static Task _fileServerTask;
        private static CancellationTokenSource _fileServerCancel;
        #endregion

        #region Class Init/Cleanup
#pragma warning disable IDE0060
        [ClassInitialize]
        public static void ServerInit(TestContext testContext)
        {
            _webRoot = Path.Combine(EngineTests.BaseDir, "Updater", "WebRoot");

            IWebHost host = new WebHostBuilder()
                .UseKestrel()
                .UseWebRoot(_webRoot)
                .Configure(x => x.UseFileServer())
                .ConfigureKestrel((ctx, opts) => { opts.Listen(IPAddress.Loopback, ServerPort); })
                .Build();

            _fileServerCancel = new CancellationTokenSource();
            _fileServerTask = host.RunAsync(_fileServerCancel.Token);

            _serverUrl = $@"http://localhost:{ServerPort}";
        }

        [ClassCleanup]
        public static void ServerCleanup()
        {
            _fileServerCancel.Cancel();
            _fileServerTask.Wait();
        }
#pragma warning restore IDE0060
        #endregion

        #region ServerStatus - Is ASP .Net Core successfully running?
        [TestMethod]
        [TestCategory("FileUpdater")]
        public void ServerStatus()
        {
            const string fileName = "index.html";
            Uri uri = new Uri($"{_serverUrl}/{fileName}");

            string destDir = FileHelper.GetTempDir();
            try
            {
                string destFile = Path.Combine(destDir, fileName);

                Task<bool> task = DownloadFile(uri, destFile);
                task.Wait();
                bool result = task.Result;

                Assert.IsTrue(result);
                Assert.IsTrue(File.Exists(destFile));
            }
            finally
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
            }
        }
        #endregion

        #region Utility
        public static async Task<bool> DownloadFile(Uri uri, string destPath)
        {
            bool result;
            using (HttpClientHandler handler = new HttpClientHandler())
            {
                handler.AllowAutoRedirect = true;
                handler.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;

                using (HttpClient client = new HttpClient(handler))
                {
                    // Set Timeout
                    client.Timeout = TimeSpan.FromSeconds(3);

                    // Download file from uri
                    using (FileStream fs = new FileStream(destPath, FileMode.Create, FileAccess.Write))
                    {
                        HttpClientDownloader downloader = new HttpClientDownloader(client, uri, fs);
                        try
                        {
                            await downloader.DownloadAsync();
                            result = true;
                        }
                        catch (HttpRequestException)
                        {
                            result = false;
                        }
                    }
                }
            }

            return result;
        }
        #endregion
    }
}
