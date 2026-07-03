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

                EngineTests.Eval(s, $@"XMLQuery,{xmlFile},//_:items/_:item,%Items%,Delim=;", CodeType.XMLQuery, ErrorCheck.Success);
                Assert.AreEqual("A;B", s.Variables["Items"]);
                Assert.AreEqual("0", s.ReturnValue);

                EngineTests.Eval(s, $@"XMLQuery,{xmlFile},//_:items/_:itemZ,%Items%,Delim=;,NoErr", CodeType.XMLQuery, ErrorCheck.Success);
                Assert.AreEqual("2", s.ReturnValue); // XPath not found.

                EngineTests.Eval(s, $@"XMLCount,{xmlFile},//_:items/_:item,%Count%", CodeType.XMLCount, ErrorCheck.Success);
                Assert.AreEqual("2", s.Variables["Count"]);

                EngineTests.Eval(s, $@"XMLReadList,{xmlFile},//_:items/_:item,%List%,Delim=;", CodeType.XMLReadList, ErrorCheck.Success);
                Assert.AreEqual("A;B", s.Variables["List"]);

                EngineTests.Eval(s, $@"XMLValidate,{xmlFile},%Valid%", CodeType.XMLValidate, ErrorCheck.Success);
                Assert.AreEqual("True", s.Variables["Valid"]);

                EngineTests.Eval(s, $@"XMLFormat,{xmlFile},Compact", CodeType.XMLFormat, ErrorCheck.Success);
                Assert.IsFalse(File.ReadAllText(xmlFile).Contains(Environment.NewLine, StringComparison.Ordinal));

                EngineTests.Eval(s, $@"XMLAdd,Subnode,{xmlFile},//_:wlan/_:SSIDConfig/_:SSID,Element,key", CodeType.XMLAdd, ErrorCheck.Success); // Full spelling of 'Element' XMLAddType
                EngineTests.Eval(s, $@"XMLAdd,Insert,{xmlFile},//_:wlan/_:SSIDConfig/_:SSID/key[not(@type)],Attribute,type,WPA2", CodeType.XMLAdd, ErrorCheck.Success); // Full spelling of 'Attribute' XMLAddType
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [TestMethod]
        public void XmlPrefixedNamespace()
        {
            // Tests prefixed namespace handling (e.g. oor:) across all XML commands
            EngineState s = EngineTests.CreateEngineState();
            string tempDir = FileHelper.GetTempDir();
            try
            {
                string xmlFile = Path.Combine(tempDir, "registrymodifications.xcu");
                File.WriteAllText(xmlFile,
                    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                    "<oor:items xmlns:oor=\"http://openoffice.org/2001/registry\" " +
                    "xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">\n" +
                    "  <item oor:path=\"/org.openoffice.Office.Common/Help\">\n" +
                    "    <prop oor:name=\"ExtendedTip\" oor:op=\"fuse\">\n" +
                    "      <value>false</value>\n" +
                    "    </prop>\n" +
                    "  </item>\n" +
                    "</oor:items>");

                // XMLRead - basic prefixed namespace XPath
                EngineTests.Eval(s, $@"XMLRead,{xmlFile},oor:items/item[@oor:path='/org.openoffice.Office.Common/Help']/prop[@oor:name='ExtendedTip']/value,%Value%", CodeType.XMLRead, ErrorCheck.Success);
                Assert.AreEqual("false", s.Variables["Value"]);

                // XMLUpdate - update value via prefixed namespace XPath
                EngineTests.Eval(s, $@"XMLUpdate,{xmlFile},oor:items/item[@oor:path='/org.openoffice.Office.Common/Help']/prop[@oor:name='ExtendedTip']/value,true", CodeType.XMLUpdate, ErrorCheck.Success);
                Assert.AreEqual("0", s.ReturnValue);
                EngineTests.Eval(s, $@"XMLRead,{xmlFile},oor:items/item[@oor:path='/org.openoffice.Office.Common/Help']/prop[@oor:name='ExtendedTip']/value,%Value%", CodeType.XMLRead, ErrorCheck.Success);
                Assert.AreEqual("true", s.Variables["Value"]);

                // XMLAdd - append a new <item> with namespaced attributes (exercises ResolveXName)
                EngineTests.Eval(s, $@"XMLAdd,Append,{xmlFile},oor:items,elem,item", CodeType.XMLAdd, ErrorCheck.Success);
                EngineTests.Eval(s, $@"XMLAdd,Append,{xmlFile},oor:items/item[last()],attr,oor:path,/org.openoffice.Office.Common/Misc", CodeType.XMLAdd, ErrorCheck.Success);
                EngineTests.Eval(s, $@"XMLAdd,Append,{xmlFile},oor:items/item[last()],elem,prop", CodeType.XMLAdd, ErrorCheck.Success);
                EngineTests.Eval(s, $@"XMLAdd,Append,{xmlFile},oor:items/item[last()]/prop,attr,oor:name,FirstRun", CodeType.XMLAdd, ErrorCheck.Success);
                EngineTests.Eval(s, $@"XMLAdd,Append,{xmlFile},oor:items/item[last()]/prop,attr,oor:op,fuse", CodeType.XMLAdd, ErrorCheck.Success);
                EngineTests.Eval(s, $@"XMLAdd,Append,{xmlFile},oor:items/item[last()]/prop,elem,value,false", CodeType.XMLAdd, ErrorCheck.Success);

                // Verify the new item was written correctly including namespaced attributes
                EngineTests.Eval(s, $@"XMLRead,{xmlFile},oor:items/item[@oor:path='/org.openoffice.Office.Common/Misc']/prop[@oor:name='FirstRun']/value,%Value%", CodeType.XMLRead, ErrorCheck.Success);
                Assert.AreEqual("false", s.Variables["Value"]);

                // XMLCount - verify both items exist
                EngineTests.Eval(s, $@"XMLCount,{xmlFile},oor:items/item,%Count%", CodeType.XMLCount, ErrorCheck.Success);
                Assert.AreEqual("2", s.Variables["Count"]);

                // XMLRename - rename a non-namespaced element
                EngineTests.Eval(s, $@"XMLRename,{xmlFile},oor:items/item[@oor:path='/org.openoffice.Office.Common/Misc']/prop[@oor:name='FirstRun']/value,val", CodeType.XMLRename, ErrorCheck.Success);
                EngineTests.Eval(s, $@"XMLRead,{xmlFile},oor:items/item[@oor:path='/org.openoffice.Office.Common/Misc']/prop[@oor:name='FirstRun']/val,%Value%", CodeType.XMLRead, ErrorCheck.Success);
                Assert.AreEqual("false", s.Variables["Value"]);

                // XMLRename - rename a namespaced attribute (exercises ResolveXName in RenameXmlObject)
                EngineTests.Eval(s, $@"XMLRename,{xmlFile},oor:items/item[@oor:path='/org.openoffice.Office.Common/Misc']/prop/@oor:op,oor:type", CodeType.XMLRename, ErrorCheck.Success);
                EngineTests.Eval(s, $@"XMLRead,{xmlFile},oor:items/item[@oor:path='/org.openoffice.Office.Common/Misc']/prop/@oor:type,%Value%", CodeType.XMLRead, ErrorCheck.Success);
                Assert.AreEqual("fuse", s.Variables["Value"]);

                // XMLDelete - delete by prefixed namespace XPath
                EngineTests.Eval(s, $@"XMLDelete,{xmlFile},oor:items/item[@oor:path='/org.openoffice.Office.Common/Misc']", CodeType.XMLDelete, ErrorCheck.Success);
                EngineTests.Eval(s, $@"XMLCount,{xmlFile},oor:items/item,%Count%", CodeType.XMLCount, ErrorCheck.Success);
                Assert.AreEqual("1", s.Variables["Count"]);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [TestMethod]
        public void XmlDeclarationPreserved()
        {
            // Tests that SaveXml preserves the <?xml?> declaration after any write operation
            EngineState s = EngineTests.CreateEngineState();
            string tempDir = FileHelper.GetTempDir();
            try
            {
                string xmlFile = Path.Combine(tempDir, "declared.xml");
                const string declaration = "<?xml version=\"1.0\" encoding=\"utf-8\"?>"; // XmlWriter outputs 'utf-8' as lowercase, so we need to test with lowercase.
                File.WriteAllText(xmlFile,
                    declaration + "\n" +
                    "<configuration>\n" +
                    "  <setting name=\"Theme\">Light</setting>\n" +
                    "</configuration>");

                // Trigger a save via XMLUpdate
                EngineTests.Eval(s, $@"XMLUpdate,{xmlFile},/configuration/setting[@name='Theme'],Dark", CodeType.XMLUpdate, ErrorCheck.Success);

                string saved = File.ReadAllText(xmlFile);
                Assert.IsTrue(saved.StartsWith(declaration, StringComparison.Ordinal),
                    "XML declaration should be preserved after save");
                Assert.IsFalse(saved.Contains('\uFEFF'),
                    "UTF-8 BOM should not be written");

                // Trigger a save via XMLAdd
                EngineTests.Eval(s, $@"XMLAdd,Append,{xmlFile},/configuration,elem,setting", CodeType.XMLAdd, ErrorCheck.Success);
                saved = File.ReadAllText(xmlFile);
                Assert.IsTrue(saved.StartsWith(declaration, StringComparison.Ordinal),
                    "XML declaration should be preserved after XMLAdd save");

                // Trigger a save via XMLFormat
                EngineTests.Eval(s, $@"XMLFormat,{xmlFile},Compact", CodeType.XMLFormat, ErrorCheck.Success);
                saved = File.ReadAllText(xmlFile);
                Assert.IsTrue(saved.StartsWith(declaration, StringComparison.Ordinal),
                    "XML declaration should be preserved after XMLFormat save");
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [TestMethod]
        public void XmlNamespaceManagerReservedPrefixes()
        {
            // Tests that CreateNamespaceManager doesn't throw on reserved xml/xmlns prefixes
            // and correctly registers the default namespace as '_'
            EngineState s = EngineTests.CreateEngineState();
            string tempDir = FileHelper.GetTempDir();
            try
            {
                // File with xml:lang (reserved 'xml' prefix on an attribute)
                string xmlLangFile = Path.Combine(tempDir, "xmllang.xml");
                File.WriteAllText(xmlLangFile,
                    "<root xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">\n" +
                    "  <item xml:lang=\"en\" xsi:nil=\"false\">Hello</item>\n" +
                    "</root>");

                // Should not throw ArgumentException on reserved 'xml' prefix
                EngineTests.Eval(s, $@"XMLRead,{xmlLangFile},/root/item,%Value%", CodeType.XMLRead, ErrorCheck.Success);
                Assert.AreEqual("Hello", s.Variables["Value"]);

                // File with default namespace (exposed as '_:')
                string defaultNsFile = Path.Combine(tempDir, "defaultns.xml");
                File.WriteAllText(defaultNsFile,
                    "<root xmlns=\"urn:test\" xmlns:ext=\"urn:ext\">\n" +
                    "  <item ext:type=\"primary\">Value</item>\n" +
                    "</root>");

                // Default namespace via '_:' prefix
                EngineTests.Eval(s, $@"XMLRead,{defaultNsFile},/_:root/_:item,%Value%", CodeType.XMLRead, ErrorCheck.Success);
                Assert.AreEqual("Value", s.Variables["Value"]);

                // Secondary prefixed namespace alongside default
                EngineTests.Eval(s, $@"XMLRead,{defaultNsFile},/_:root/_:item/@ext:type,%Value%", CodeType.XMLRead, ErrorCheck.Success);
                Assert.AreEqual("primary", s.Variables["Value"]);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [TestMethod]
        public void XmlScalarXPathRejected()
        {
            // XPath expressions that evaluate to a scalar (double/bool/string), not a node-set,
            // must be rejected by XMLUpdate/XMLAdd/XMLDelete/XMLRename instead of silently
            // "succeeding" without touching the document.
            EngineState s = EngineTests.CreateEngineState();
            string tempDir = FileHelper.GetTempDir();
            try
            {
                string xmlFile = Path.Combine(tempDir, "scalar.xml");
                const string original = "<root><book id=\"1\" /><book id=\"2\" /></root>";
                File.WriteAllText(xmlFile, original);

                EngineTests.Eval(s, $@"XMLUpdate,{xmlFile},count(/root/book),NewValue", CodeType.XMLUpdate, ErrorCheck.RuntimeError);
                EngineTests.Eval(s, $@"XMLDelete,{xmlFile},boolean(/root/book[@id='1'])", CodeType.XMLDelete, ErrorCheck.RuntimeError);
                EngineTests.Eval(s, $@"XMLRename,{xmlFile},string(/root/book[1]/@id),newname", CodeType.XMLRename, ErrorCheck.RuntimeError);
                EngineTests.Eval(s, $@"XMLAdd,Append,{xmlFile},count(/root/book),elem,child", CodeType.XMLAdd, ErrorCheck.RuntimeError);
                EngineTests.Eval(s, $@"XMLDelete,{xmlFile},/root/book[1]=/root/book[2]", CodeType.XMLDelete, ErrorCheck.RuntimeError);

                // None of the rejected calls should have triggered a save - document must be untouched.
                Assert.AreEqual(original, File.ReadAllText(xmlFile));

                EngineTests.Eval(s, $@"XMLCount,{xmlFile},/root/book,%Count%", CodeType.XMLCount, ErrorCheck.Success);
                Assert.AreEqual("2", s.Variables["Count"]);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [TestMethod]
        public void XmlMalformedXPathRejected()
        {
            // Invalid XPath syntax must produce a controlled error, not an unhandled
            // XPathException thrown out of the command.
            EngineState s = EngineTests.CreateEngineState();
            string tempDir = FileHelper.GetTempDir();
            try
            {
                string xmlFile = Path.Combine(tempDir, "malformed.xml");
                const string original = "<root><book id=\"1\" /></root>";
                File.WriteAllText(xmlFile, original);

                EngineTests.Eval(s, $@"XMLDelete,{xmlFile},//book[", CodeType.XMLDelete, ErrorCheck.RuntimeError);
                EngineTests.Eval(s, $@"XMLUpdate,{xmlFile},///bad::axis::,X", CodeType.XMLUpdate, ErrorCheck.RuntimeError);

                Assert.AreEqual(original, File.ReadAllText(xmlFile));
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [TestMethod]
        public void XmlDeleteParentAndChildOverlapDoesNotThrow()
        {
            // A single XPath match set can contain both an element and its own descendant
            // (e.g. via a union expression). Removing the parent first must not cause removing
            // the already-orphaned child to throw an exception.
            EngineState s = EngineTests.CreateEngineState();
            string tempDir = FileHelper.GetTempDir();
            try
            {
                string xmlFile = Path.Combine(tempDir, "overlap.xml");
                File.WriteAllText(xmlFile,
                    "<library>" +
                    "<book id=\"1\"><author>Jane</author></book>" +
                    "<book id=\"2\"><author>John</author></book>" +
                    "</library>");

                EngineTests.Eval(s, $@"XMLDelete,{xmlFile},//book[@id='1'] | //book[@id='1']/author", CodeType.XMLDelete, ErrorCheck.Success);

                EngineTests.Eval(s, $@"XMLCount,{xmlFile},//book,%Count%", CodeType.XMLCount, ErrorCheck.Success);
                Assert.AreEqual("1", s.Variables["Count"]);

                EngineTests.Eval(s, $@"XMLRead,{xmlFile},//book/@id,%Value%", CodeType.XMLRead, ErrorCheck.Success);
                Assert.AreEqual("2", s.Variables["Value"]);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [TestMethod]
        public void XmlMultiMatchAppliesToEveryNode()
        {
            // XMLDelete/XMLUpdate/XMLRename must apply to every node in a multi-match result,
            // not just the first one.
            EngineState s = EngineTests.CreateEngineState();
            string tempDir = FileHelper.GetTempDir();
            try
            {
                // Multi-match delete of attributes across sibling elements
                string deleteFile = Path.Combine(tempDir, "multidelete.xml");
                File.WriteAllText(deleteFile,
                    "<library>" +
                    "<book id=\"1\" available=\"true\" />" +
                    "<book id=\"2\" available=\"false\" />" +
                    "<book id=\"3\" available=\"true\" />" +
                    "</library>");

                EngineTests.Eval(s, $@"XMLDelete,{deleteFile},//book/@available", CodeType.XMLDelete, ErrorCheck.Success);
                EngineTests.Eval(s, $@"XMLCount,{deleteFile},//book/@available,%Count%", CodeType.XMLCount, ErrorCheck.Success);
                Assert.AreEqual("0", s.Variables["Count"]);
                EngineTests.Eval(s, $@"XMLCount,{deleteFile},//book,%Count%", CodeType.XMLCount, ErrorCheck.Success);
                Assert.AreEqual("3", s.Variables["Count"]); // Elements themselves must survive

                // Multi-match update across sibling elements
                string updateFile = Path.Combine(tempDir, "multiupdate.xml");
                File.WriteAllText(updateFile,
                    "<library>" +
                    "<item status=\"pending\" />" +
                    "<item status=\"pending\" />" +
                    "<item status=\"pending\" />" +
                    "</library>");

                EngineTests.Eval(s, $@"XMLUpdate,{updateFile},//item/@status,done", CodeType.XMLUpdate, ErrorCheck.Success);
                EngineTests.Eval(s, $@"XMLReadList,{updateFile},//item/@status,%List%,Delim=;", CodeType.XMLReadList, ErrorCheck.Success);
                Assert.AreEqual("done;done;done", s.Variables["List"]);

                // Multi-match rename across sibling elements
                string renameFile = Path.Combine(tempDir, "multirename.xml");
                File.WriteAllText(renameFile, "<library><oldname>A</oldname><oldname>B</oldname></library>");

                EngineTests.Eval(s, $@"XMLRename,{renameFile},//oldname,newname", CodeType.XMLRename, ErrorCheck.Success);
                EngineTests.Eval(s, $@"XMLCount,{renameFile},//newname,%Count%", CodeType.XMLCount, ErrorCheck.Success);
                Assert.AreEqual("2", s.Variables["Count"]);
                EngineTests.Eval(s, $@"XMLCount,{renameFile},//oldname,%Count%", CodeType.XMLCount, ErrorCheck.Success);
                Assert.AreEqual("0", s.Variables["Count"]);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
        [TestMethod]
        public void XmlNoErrDoesNotSuppressMalformedXPath()
        {
            // NoErr exists to tolerate a syntactically valid XPath that legitimately matches nothing.
            // It must NOT suppress genuine usage errors like invalid XPath syntax or a scalar (non-node-set) result
            // - those should always be ErrorCheck.RuntimeError
            EngineState s = EngineTests.CreateEngineState();
            string tempDir = FileHelper.GetTempDir();
            try
            {
                string xmlFile = Path.Combine(tempDir, "noerr.xml");
                const string original = "<root><book id=\"1\" /><book id=\"2\" /></root>";
                File.WriteAllText(xmlFile, original);

                // Sanity check: NoErr DOES suppress a legitimately-absent path (returns "2", no Error log).
                EngineTests.Eval(s, $@"XMLRead,{xmlFile},/root/missing,%Value%,NoErr", CodeType.XMLRead, ErrorCheck.Success);
                Assert.AreEqual("2", s.ReturnValue);

                // But NoErr must NOT suppress malformed XPath syntax...
                EngineTests.Eval(s, $@"XMLRead,{xmlFile},//book[,%Value%,NoErr", CodeType.XMLRead, ErrorCheck.RuntimeError);
                EngineTests.Eval(s, $@"XMLUpdate,{xmlFile},//book[,X,NoErr", CodeType.XMLUpdate, ErrorCheck.RuntimeError);
                EngineTests.Eval(s, $@"XMLQuery,{xmlFile},//book[,%Value%,NoErr", CodeType.XMLQuery, ErrorCheck.RuntimeError);

                // ...or a scalar (non-node-set) XPath result on a mutating command.
                EngineTests.Eval(s, $@"XMLUpdate,{xmlFile},count(/root/book),X,NoErr", CodeType.XMLUpdate, ErrorCheck.RuntimeError);

                // Document must remain untouched by any of the above.
                Assert.AreEqual(original, File.ReadAllText(xmlFile));
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
