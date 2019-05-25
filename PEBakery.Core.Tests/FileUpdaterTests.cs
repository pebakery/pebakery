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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Helper;
using PEBakery.Ini;

namespace PEBakery.Core.Tests
{
    [TestClass]
    public class FileUpdaterTests
    {
        #region Fields and Properties
        public const int ServerPort = 8380;
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
                .Configure(app =>
                {
                    app.UseStaticFiles(new StaticFileOptions
                    {
                        ServeUnknownFileTypes = true,
                        DefaultContentType = "text/plain",
                    });
                    app.UseDefaultFiles();
                    app.UseDirectoryBrowser();
                })
                .ConfigureKestrel((ctx, opts) => { opts.Listen(IPAddress.Loopback, ServerPort); })
                .Build();

            _fileServerCancel = new CancellationTokenSource();
            _fileServerTask = host.RunAsync(_fileServerCancel.Token);
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

            string destDir = FileHelper.GetTempDir();
            try
            {
                string srcFile = Path.Combine(_webRoot, fileName);
                string destFile = Path.Combine(destDir, fileName);
                Uri uri = new Uri($"http://localhost:{ServerPort}/{fileName}");

                Task<bool> task = DownloadFile(uri, destFile);
                task.Wait();
                bool result = task.Result;

                Assert.IsTrue(result);
                Assert.IsTrue(File.Exists(destFile));
                Assert.IsTrue(EngineTests.FileEqual(srcFile, destFile));
            }
            finally
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
            }
        }
        #endregion

        #region UpdateScript
        [TestMethod]
        [TestCategory("FileUpdater")]
        public void UpdateScript()
        {
            string destDir = FileHelper.GetTempDir();
            try
            {
                // Prepare running FileUpdater
                string srcScriptFile = Path.Combine(_webRoot, "pebakery", "Standalone", "PreserveInterface_r1.script");
                string workScriptFile = Path.Combine(destDir, "PreserveInterface.script");
                string workScriptTreePath = Path.Combine("TestSuite", "Updater", "PreserveInterface.script");
                File.Copy(srcScriptFile, workScriptFile);

                Project p = EngineTests.Project;
                Script sc = p.LoadScriptRuntime(workScriptFile, workScriptTreePath, new LoadScriptRuntimeOptions
                {
                    AddToProjectTree = true,
                    IgnoreMain = false,
                    OverwriteToProjectTree = true,
                });

                // Run an update
                FileUpdater updater = new FileUpdater(EngineTests.Project, null, null);
                (Script newScript, _) = updater.UpdateScript(sc, true);

                // Validate updated script
                Assert.IsNotNull(newScript);
                Assert.IsTrue(newScript.TidyVersion.Equals("1.2", StringComparison.Ordinal));
                Assert.AreEqual(SelectedState.True, newScript.Selected);
                IniKey[] keys =
                {
                    new IniKey("Interface", "checkbox02"),
                    new IniKey("Interface", "ComboBox02"),
                    new IniKey("Interface", "bvl_Top"),
                    new IniKey("Interface", "ComboBox01"),
                    new IniKey("Interface", "CheckBox01"),
                    new IniKey("Interface", "Button01"),
                    new IniKey("Interface", "CheckBox03"),
                };
                keys = IniReadWriter.ReadKeys(newScript.RealPath, keys);
                Dictionary<string, string> ifaceDict = keys.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
                Assert.IsTrue(ifaceDict["checkbox02"].Equals("checkbox02,1,3,10,10,200,18,False", StringComparison.Ordinal));
                Assert.IsTrue(ifaceDict["ComboBox02"].Equals("C,1,4,262,120,150,22,A,B,C", StringComparison.Ordinal));
                Assert.IsTrue(ifaceDict["bvl_Top"].Equals("\"Updated\",1,12,254,101,228,70,8,Normal", StringComparison.Ordinal));
                Assert.IsTrue(ifaceDict["ComboBox01"].Equals("A,1,4,42,66,150,21,A,B,C", StringComparison.Ordinal));
                Assert.IsTrue(ifaceDict["CheckBox01"].Equals("CheckBox01,1,3,42,98,173,18,False", StringComparison.Ordinal));
                Assert.IsTrue(ifaceDict["Button01"].Equals("Button01,1,8,262,46,80,25,TestSection,0,False", StringComparison.Ordinal));
                Assert.IsTrue(ifaceDict["CheckBox03"].Equals("CheckBox03,1,3,100,400,200,18,True", StringComparison.Ordinal));
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
