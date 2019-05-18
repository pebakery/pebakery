using System;
using System.IO;
using System.Windows.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PEBakery.Core.Tests
{
    [TestClass]
    public class SettingTests
    {
        [TestMethod]
        [TestCategory("Setting")]
        public void ReadFromFile()
        {
            string testBench = EngineTests.Project.Variables.Expand("%TestBench%");
            string srcDir = Path.Combine(testBench, "Setting");

            string settingFile = Path.Combine(srcDir, "SubSetting1.ini");
            Setting setting = new Setting(settingFile);

            // TODO: Add more setting properties

            Assert.IsTrue(setting.Project.DefaultProject.Length == 0);

            Assert.IsTrue(setting.General.UseCustomUserAgent);
            Assert.IsTrue(setting.General.CustomUserAgent.Equals("Wget/1.20.3 (linux-gnu)", StringComparison.Ordinal));

            Assert.IsTrue(setting.Interface.UseCustomTitle);
            Assert.IsTrue(setting.Interface.CustomTitle.Equals("PEBakery.Core.Tests", StringComparison.Ordinal));
            Assert.AreEqual(150, setting.Interface.ScaleFactor);
            Assert.IsTrue(setting.Interface.DisplayShellExecuteConOut);
            Assert.AreEqual(Setting.InterfaceSize.Adaptive, setting.Interface.InterfaceSize);

            Assert.IsTrue(setting.Script.EnableCache);
            Assert.IsFalse(setting.Script.AutoSyntaxCheck);

            Assert.AreEqual(LogDebugLevel.Production, setting.Log.DebugLevel);
            Assert.IsFalse(setting.Log.DeferredLogging);
            Assert.IsFalse(setting.Log.MinifyHtmlExport);

            Assert.AreEqual(Setting.ThemeType.Custom, setting.Theme.ThemeType);
            Assert.AreEqual(Color.FromRgb(255, 255, 255), setting.Theme.CustomTopPanelBackground);
            Assert.AreEqual(Color.FromRgb(0, 0, 0), setting.Theme.CustomTopPanelForeground);
        }
    }
}
