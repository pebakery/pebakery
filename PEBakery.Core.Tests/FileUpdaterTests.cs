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
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Helper;
using PEBakery.Ini;

namespace PEBakery.Core.Tests
{
    [TestClass]
    public class FileUpdaterTests
    {
        #region Fields and Properties
        #endregion

        #region Class Init/Cleanup
#pragma warning disable IDE0060
        [ClassInitialize]
        public static void ServerInit(TestContext testContext)
        {
            TestSetup.StartWebFileServer();
        }

        [ClassCleanup]
        public static void ServerCleanup()
        {
        }
#pragma warning restore IDE0060
        #endregion

        #region ServerStatus - Is ASP .Net Core successfully running?
        [TestMethod]
        [TestCategory("FileUpdater")]
        public void ServerStatus()
        {
            string destFile = FileHelper.ReserveTempFile("html");
            try
            {
                string srcFile = Path.Combine(TestSetup.WebRoot, "index.html");
                Uri uri = new Uri($"{TestSetup.UrlRoot}/index.html");

                Task<bool> task = DownloadFile(uri, destFile);
                task.Wait();
                bool result = task.Result;

                Assert.IsTrue(result);
                Assert.IsTrue(File.Exists(destFile));
                Assert.IsTrue(TestSetup.FileEqual(srcFile, destFile));
            }
            finally
            {
                if (File.Exists(destFile))
                    File.Delete(destFile);
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
                string srcScriptFile = Path.Combine(TestSetup.WebRoot, "Updater", "Standalone", "PreserveInterface_r1.script");
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
                Script newScript = updater.UpdateScript(sc, true);

                // Validate updated script
                foreach (LogInfo log in updater.Logs)
                    Console.WriteLine($"[{log.State}] {log.Message}");
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
                    new IniKey("ThirdInterface", "RadioGroup01"),
                    new IniKey("FourthInterface", "RadioButton01"),
                    new IniKey("FourthInterface", "RadioButton02"),
                    new IniKey("FourthInterface", "RadioButton03"),
                };

                keys = IniReadWriter.ReadKeys(newScript.RealPath, keys);
                Dictionary<string, string> ifaceDict = keys.Where(x => x.Section.Equals("Interface"))
                    .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
                Assert.IsTrue(ifaceDict["checkbox02"].Equals("checkbox02,1,3,10,10,200,18,False", StringComparison.Ordinal));
                Assert.IsTrue(ifaceDict["ComboBox02"].Equals("C,1,4,262,120,150,22,A,B,C", StringComparison.Ordinal));
                Assert.IsTrue(ifaceDict["bvl_Top"].Equals("\"Updated\",1,12,254,101,228,70,8,Normal", StringComparison.Ordinal));
                Assert.IsTrue(ifaceDict["ComboBox01"].Equals("A,1,4,42,66,150,21,A,B,C", StringComparison.Ordinal));
                Assert.IsTrue(ifaceDict["CheckBox01"].Equals("CheckBox01,1,3,42,98,173,18,False", StringComparison.Ordinal));
                Assert.IsTrue(ifaceDict["Button01"].Equals("Button01,1,8,262,46,80,25,TestSection,0,False", StringComparison.Ordinal));
                Assert.IsTrue(ifaceDict["CheckBox03"].Equals("CheckBox03,1,3,100,400,200,18,True", StringComparison.Ordinal));

                ifaceDict = keys.Where(x => x.Section.Equals("ThirdInterface"))
                    .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
                Assert.IsTrue(ifaceDict["RadioGroup01"].Equals("RadioGroup01,1,14,10,30,150,60,A,B,C,1", StringComparison.Ordinal));

                ifaceDict = keys.Where(x => x.Section.Equals("FourthInterface"))
                    .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
                Assert.IsTrue(ifaceDict["RadioButton01"].Equals("A,1,11,10,30,120,20,False", StringComparison.Ordinal));
                Assert.IsTrue(ifaceDict["RadioButton02"].Equals("B,1,11,10,50,120,20,False", StringComparison.Ordinal));
                Assert.IsTrue(ifaceDict["RadioButton03"].Equals("C,1,11,10,70,120,20,True", StringComparison.Ordinal));

                Assert.IsFalse(IniReadWriter.ContainsSection(newScript.RealPath, "SecondInterface"));
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
