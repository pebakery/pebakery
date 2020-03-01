/*
    Copyright (C) 2019-2020 Hajin Jang
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
using System.Text;

namespace PEBakery.Core.Tests
{
    [TestClass]
    public class UpdateJsonTests
    {
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

        #region CreateScriptMetaJson
        [TestMethod]
        public void CreateScriptMetaJson()
        {
            string destDir = FileHelper.GetTempDir();

            try
            {
                // Prepare running FileUpdater
                string srcScriptFile = Path.Combine(TestSetup.WebRoot, "Updater", "Standalone", "PreserveInterface_r2.script");
                string workScriptFile = Path.Combine(destDir, "PreserveInterface.script");
                string workScriptTreePath = Path.Combine("TestSuite", "Updater", "PreserveInterface.script");
                string destJson = Path.Combine(destDir, "PreserveInterface_r2.meta.json");
                File.Copy(srcScriptFile, workScriptFile);

                Project p = EngineTests.Project;
                Script sc = p.LoadScriptRuntime(workScriptFile, workScriptTreePath, new LoadScriptRuntimeOptions
                {
                    AddToProjectTree = true,
                    IgnoreMain = false,
                    OverwriteToProjectTree = true,
                });

                // Create a script meta json
                UpdateJson.CreateScriptUpdateJson(sc, destJson);

                // Print metaJsonText (Log)
                string metaJsonText;
                using (StreamReader sr = new StreamReader(destJson, new UTF8Encoding(false), false))
                {
                    metaJsonText = sr.ReadToEnd();
                }
                Console.WriteLine(metaJsonText);

                // Check sanity of created script meta json
                ResultReport<UpdateJson.Root> readReport = UpdateJson.ReadUpdateJson(destJson);
                Assert.IsNotNull(readReport.Result);
                Assert.IsTrue(readReport.Success);
                UpdateJson.Root root = readReport.Result;

                ResultReport checkReport = root.Validate();
                Assert.IsTrue(readReport.Success);
            }
            finally
            {
                if (Directory.Exists(destDir))
                    Directory.Delete(destDir, true);
            }
        }
        #endregion
    }
}
