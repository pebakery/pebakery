/*
    Copyright (C) 2018-present Hajin Jang
    Licensed under GPL 3.0
*/

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Helper;
using System;
using System.IO;

namespace PEBakery.Core.Tests.Command
{
    [TestClass]
    [TestCategory(nameof(Command))]
    [TestCategory(nameof(Commands.CommandXml))]
    public class CommandXmlTests
    {
        [TestMethod]
        public void XmlPhoenixCompatibility()
        {
            EngineState s = EngineTests.CreateEngineState();
            string tempDir = FileHelper.GetTempDir();
            try
            {
                string xmlFile = Path.Combine(tempDir, "config.xml");
                File.WriteAllText(xmlFile, @"
<configuration>
  <userSettings>
    <App.Properties.Settings>
      <setting name=""Theme"" serializeAs=""String"">
        <value>Light</value>
      </setting>
    </App.Properties.Settings>
  </userSettings>
</configuration>");

                string themePath = "/configuration/userSettings/App.Properties.Settings/setting[@name='Theme']/value";
                EngineTests.Eval(s, $@"XMLRead,{xmlFile},{themePath},%Value%", CodeType.XMLRead, ErrorCheck.Success);
                Assert.AreEqual("Light", s.Variables["Value"]);

                EngineTests.Eval(s, $@"XMLUpdate,{xmlFile},{themePath},Dark", CodeType.XMLUpdate, ErrorCheck.Success);
                Assert.AreEqual("0", s.ReturnValue);
                EngineTests.Eval(s, $@"XMLRead,{xmlFile},{themePath},%Value%", CodeType.XMLRead, ErrorCheck.Success);
                Assert.AreEqual("Dark", s.Variables["Value"]);

                string settingsPath = "/configuration/userSettings/App.Properties.Settings";
                EngineTests.Eval(s, $@"XMLAdd,Subnode,{xmlFile},{settingsPath},elem,setting", CodeType.XMLAdd, ErrorCheck.Success);
                EngineTests.Eval(s, $@"XMLAdd,Insert,{xmlFile},{settingsPath}/setting[not(@name)],attr,name,Language", CodeType.XMLAdd, ErrorCheck.Success);
                EngineTests.Eval(s, $@"XMLAdd,Subnode,{xmlFile},{settingsPath}/setting[@name='Language'],elem,value,ko-KR", CodeType.XMLAdd, ErrorCheck.Success);
                EngineTests.Eval(s, $@"XMLRead,{xmlFile},{settingsPath}/setting[@name='Language']/value,%Value%", CodeType.XMLRead, ErrorCheck.Success);
                Assert.AreEqual("ko-KR", s.Variables["Value"]);

                EngineTests.Eval(s, $@"XMLRename,{xmlFile},{settingsPath}/setting[@name='Language']/value,lang", CodeType.XMLRename, ErrorCheck.Success);
                EngineTests.Eval(s, $@"XMLRead,{xmlFile},{settingsPath}/setting[@name='Language']/lang,%Value%", CodeType.XMLRead, ErrorCheck.Success);
                Assert.AreEqual("ko-KR", s.Variables["Value"]);

                EngineTests.Eval(s, $@"XMLDelete,{xmlFile},{settingsPath}/setting[@name='Language']/@name", CodeType.XMLDelete, ErrorCheck.Success);
                EngineTests.Eval(s, $@"XMLRead,{xmlFile},{settingsPath}/setting[not(@name)]/lang,%Value%", CodeType.XMLRead, ErrorCheck.Success);
                Assert.AreEqual("ko-KR", s.Variables["Value"]);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [TestMethod]
        public void XmlNativeHelpers()
        {
            EngineState s = EngineTests.CreateEngineState();
            string tempDir = FileHelper.GetTempDir();
            try
            {
                string xmlFile = Path.Combine(tempDir, "wireless.xml");
                File.WriteAllText(xmlFile, @"<wlan xmlns=""urn:test""><SSIDConfig><SSID><name>WiFi</name></SSID></SSIDConfig><items><item>A</item><item>B</item></items></wlan>");

                EngineTests.Eval(s, $@"XMLRead,{xmlFile},//_:SSIDConfig/_:SSID/_:name/text(),%Value%", CodeType.XMLRead, ErrorCheck.Success);
                Assert.AreEqual("WiFi", s.Variables["Value"]);

                EngineTests.Eval(s, $@"XMLQuery,{xmlFile},//_:items/_:item,%Items%", CodeType.XMLQuery, ErrorCheck.Success);
                Assert.AreEqual("A|B", s.Variables["Items"]);

                EngineTests.Eval(s, $@"XMLCount,{xmlFile},//_:items/_:item,%Count%", CodeType.XMLCount, ErrorCheck.Success);
                Assert.AreEqual("2", s.Variables["Count"]);

                EngineTests.Eval(s, $@"XMLReadList,{xmlFile},//_:items/_:item,%List%,Delim=;", CodeType.XMLReadList, ErrorCheck.Success);
                Assert.AreEqual("A;B", s.Variables["List"]);

                EngineTests.Eval(s, $@"XMLValidate,{xmlFile},%Valid%", CodeType.XMLValidate, ErrorCheck.Success);
                Assert.AreEqual("True", s.Variables["Valid"]);

                EngineTests.Eval(s, $@"XMLFormat,{xmlFile},Compact", CodeType.XMLFormat, ErrorCheck.Success);
                Assert.IsFalse(File.ReadAllText(xmlFile).Contains(Environment.NewLine, StringComparison.Ordinal));
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
