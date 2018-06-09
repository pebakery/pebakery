/*
    Copyright (C) 2017-2018 Hajin Jang
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
using System.Text;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Core;

namespace PEBakery.Tests.Core
{
    [TestClass]
    public class StringEscaperTests
    {
        #region Escape
        [TestMethod]
        [TestCategory("StringEscaper")]
        public void StringEscaper_Escape()
        {
            Escape_1();
            Escape_2();
            Escape_3();
            Escape_4();
        }

        public void Escape_1()
        {
            string src = StringEscaperTests.SampleString;
            string dest = StringEscaper.Escape(src, false, false);
            const string comp = "Comma [,]#$xPercent [%]#$xDoubleQuote [#$q]#$xSpace [ ]#$xTab [#$t]#$xSharp [##]#$xNewLine [#$x]";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public void Escape_2()
        {
            string src = StringEscaperTests.SampleString;
            string dest = StringEscaper.Escape(src, true, false);
            const string comp = "Comma#$s[#$c]#$xPercent#$s[%]#$xDoubleQuote#$s[#$q]#$xSpace#$s[#$s]#$xTab#$s[#$t]#$xSharp#$s[##]#$xNewLine#$s[#$x]";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public void Escape_3()
        {
            string src = StringEscaperTests.SampleString;
            string dest = StringEscaper.Escape(src, true, true);
            const string comp = "Comma#$s[#$c]#$xPercent#$s[#$p]#$xDoubleQuote#$s[#$q]#$xSpace#$s[#$s]#$xTab#$s[#$t]#$xSharp#$s[##]#$xNewLine#$s[#$x]";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public void Escape_4()
        {
            string[] srcs = { "Comma [,]", "Space [ ]", "DoubleQuote [\"]" };
            List<string> dests = StringEscaper.Escape(srcs, true);
            string[] comps =
            {
               "Comma#$s[#$c]",
               "Space#$s[#$s]",
               "DoubleQuote#$s[#$q]",
            };

            for (int i = 0; i < dests.Count; i++)
                Assert.IsTrue(dests[i].Equals(comps[i], StringComparison.Ordinal));
        }
        #endregion

        #region QuoteEscape
        [TestMethod]
        [TestCategory("StringEscaper")]
        public void StringEscaper_QuoteEscape()
        {
            QuoteEscape_1();
            QuoteEscape_2();
            QuoteEscape_3();
        }

        public void QuoteEscape_1()
        {
            string src = StringEscaperTests.SampleString;
            string dest = StringEscaper.QuoteEscape(src, false, false);
            string comp = "\"Comma [,]#$xPercent [%]#$xDoubleQuote [#$q]#$xSpace [ ]#$xTab [#$t]#$xSharp [##]#$xNewLine [#$x]\"";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public void QuoteEscape_2()
        {
            string[] srcs = new string[] { "Comma [,]", "Space [ ]", "DoubleQuote [\"]" };
            List<string> dests = StringEscaper.QuoteEscape(srcs);
            string[] comps = new string[]
            {
               "\"Comma [,]\"",
               "\"Space [ ]\"",
               "\"DoubleQuote [#$q]\"",
            };

            for (int i = 0; i < dests.Count; i++)
                Assert.IsTrue(dests[i].Equals(comps[i], StringComparison.Ordinal));
        }

        public void QuoteEscape_3()
        {
            string[] srcs = new string[] { "Comma [,]", "Space [ ]", "DoubleQuote [\"]" };
            List<string> dests = StringEscaper.QuoteEscape(srcs);
            string[] comps = new string[]
            {
               "\"Comma [,]\"",
               "\"Space [ ]\"",
               "\"DoubleQuote [#$q]\"",
            };

            for (int i = 0; i < dests.Count; i++)
                Assert.IsTrue(dests[i].Equals(comps[i], StringComparison.Ordinal));
        }
        #endregion

        #region Unescape
        [TestMethod]
        [TestCategory("StringEscaper")]
        public void StringEscaper_Unescape()
        {
            Unescape_1();
            Unescape_2();
            Unescape_3();
            Unescape_4();
            Unescape_5();
        }

        public void Unescape_1()
        {
            string src = "Comma [,]#$xPercent [%]#$xDoubleQuote [#$q]#$xSpace [ ]#$xTab [#$t]#$xSharp [##]#$xNewLine [#$x]";
            string dest = StringEscaper.Unescape(src, false);
            string comp = StringEscaperTests.SampleString;
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public void Unescape_2()
        {
            string src = "Comma [,]#$xPercent [#$p]#$xDoubleQuote [#$q]#$xSpace [ ]#$xTab [#$t]#$xSharp [##]#$xNewLine [#$x]";
            string dest = StringEscaper.Unescape(src, true);
            string comp = StringEscaperTests.SampleString;
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public void Unescape_3()
        {
            string src = "Comma#$s[#$c]#$xPercent#$s[%]#$xDoubleQuote#$s[#$q]#$xSpace#$s[#$s]#$xTab#$s[#$t]#$xSharp#$s[##]#$xNewLine#$s[#$x]";
            string dest = StringEscaper.Unescape(src, false);
            string comp = StringEscaperTests.SampleString;
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public void Unescape_4()
        {
            string src = "Comma#$s[#$c]#$xPercent#$s[#$p]#$xDoubleQuote#$s[#$q]#$xSpace#$s[#$s]#$xTab#$s[#$t]#$xSharp#$s[##]#$xNewLine#$s[#$x]";
            string dest = StringEscaper.Unescape(src, true);
            string comp = StringEscaperTests.SampleString;
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public void Unescape_5()
        {
            string[] srcs = new string[]
            {
               "Comma#$s[#$c]",
               "Space#$s[#$s]",
               "DoubleQuote#$s[#$q]",
            };
            List<string> dests = StringEscaper.Unescape(srcs);
            string[] comps = new string[]
            {
                "Comma [,]",
                "Space [ ]",
                "DoubleQuote [\"]",
            };

            for (int i = 0; i < dests.Count; i++)
                Assert.IsTrue(dests[i].Equals(comps[i], StringComparison.Ordinal));
        }
        #endregion

        #region QuoteUnescape
        [TestMethod]
        [TestCategory("StringEscaper")]
        public void StringEscaper_QuoteUnescape()
        {
            QuoteUnescape_1();
            QuoteUnescape_2();
        }

        public void QuoteUnescape_1()
        {
            string src = "\"Comma [,]#$xPercent [%]#$xDoubleQuote [#$q]#$xSpace [ ]#$xTab [#$t]#$xSharp [##]#$xNewLine [#$x]\"";
            string dest = StringEscaper.QuoteUnescape(src);
            string comp = StringEscaperTests.SampleString;
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public void QuoteUnescape_2()
        {
            string[] srcs = new string[]
            {
               "\"Comma [#$c]\"",
               "\"Space [ ]\"",
               "\"DoubleQuote [#$q]\"",
            };
            List<string> dests = StringEscaper.QuoteUnescape(srcs);
            string[] comps = new string[]
            {
                "Comma [,]",
                "Space [ ]",
                "DoubleQuote [\"]",
            };

            for (int i = 0; i < dests.Count; i++)
                Assert.IsTrue(dests[i].Equals(comps[i], StringComparison.Ordinal));
        }
        #endregion

        #region ExpandSectionParams
        [TestMethod]
        [TestCategory("StringEscaper")]
        public void StringEscaper_ExpandSectionParams()
        {
            ExpandSectionParams_1();
            ExpandSectionParams_2();
            ExpandSectionParams_3();
            ExpandSectionParams_4();
            ExpandSectionParams_5();
            ExpandSectionParams_6();
            ExpandSectionParams_7();
        }

        public void ExpandSectionParams_1()
        {
            EngineState s = EngineTests.CreateEngineState();
            s.Variables.SetValue(VarsType.Local, "A", "Hello");
            Variables.SetVariable(s, "#1", "World");

            string src = "%A% ##1 #1";
            string dest = StringEscaper.ExpandSectionParams(s, src);
            string comp = "%A% ##1 World";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public void ExpandSectionParams_2()
        {
            EngineState s = EngineTests.CreateEngineState();
            Variables.SetVariable(s, "#1", "World");

            string src = "%A% ##2 #1";
            string dest = StringEscaper.ExpandSectionParams(s, src);
            string comp = "%A% ##2 World";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public void ExpandSectionParams_3()
        {
            EngineState s = EngineTests.CreateEngineState();
            s.Variables.SetValue(VarsType.Local, "A", "Hello");

            string src = "%A% #1";
            string dest = StringEscaper.ExpandSectionParams(s, src);
            string comp = "%A% ##1";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public void ExpandSectionParams_4()
        {
            EngineState s = EngineTests.CreateEngineState();
            s.CurDepth = 2;
            s.Variables.SetValue(VarsType.Local, "A", "Hello");

            string src = "%A% #1";
            string dest = StringEscaper.ExpandSectionParams(s, src);
            string comp = "%A% ";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public void ExpandSectionParams_5()
        {
            EngineState s = EngineTests.CreateEngineState();
            s.CurDepth = 2;
            s.Variables.SetValue(VarsType.Local, "B", "C#");
            Variables.SetVariable(s, "#2", "WPF");

            string[] srcs = new string[]
            {
                "A_%A%",
                "B_%B%",
                "C_#1",
                "D_#2"
            };
            List<string> dests = StringEscaper.ExpandSectionParams(s, srcs);
            string[] comps = new string[]
            {
                "A_%A%",
                "B_%B%",
                "C_",
                "D_WPF"
            };

            for (int i = 0; i < dests.Count; i++)
                Assert.IsTrue(dests[i].Equals(comps[i], StringComparison.Ordinal));
        }

        public void ExpandSectionParams_6()
        {
            EngineState s = EngineTests.CreateEngineState();
            s.CurDepth = 2;
            s.Variables.SetValue(VarsType.Local, "A", "Hello");
            Variables.SetVariable(s, "#1", "#2");
            Variables.SetVariable(s, "#2", "#3");
            Variables.SetVariable(s, "#3", "#1");

            string src = "%A% #1";
            string dest = StringEscaper.ExpandVariables(s, src);
            string comp = "Hello ";

            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public void ExpandSectionParams_7()
        {
            EngineState s = EngineTests.CreateEngineState();
            s.SectionReturnValue = "TEST";

            string src = "##1 ##a ##r #r";
            string dest = StringEscaper.ExpandSectionParams(s, src);
            string comp = "##1 ##a ##r TEST";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }
        #endregion

        #region ExpandVariables
        [TestMethod]
        [TestCategory("StringEscaper")]
        public void StringEscaper_ExpandVariables()
        {
            ExpandVariables_1();
            ExpandVariables_2();
            ExpandVariables_3();
            ExpandVariables_4();
            ExpandVariables_5();
            ExpandVariables_6();
        }

        public void ExpandVariables_1()
        {
            EngineState s = EngineTests.CreateEngineState();
            s.Variables.SetValue(VarsType.Local, "A", "Hello");
            s.CurSectionParams[1] = "World";

            string src = "%A% #1";
            string dest = StringEscaper.ExpandVariables(s, src);
            string comp = "Hello World";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public void ExpandVariables_2()
        {
            EngineState s = EngineTests.CreateEngineState();
            s.CurSectionParams[1] = "World";

            const string src = "%A% #1";
            string dest = StringEscaper.ExpandVariables(s, src);
            const string comp = "#$pA#$p World";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public void ExpandVariables_3()
        {
            EngineState s = EngineTests.CreateEngineState();
            s.Variables.SetValue(VarsType.Local, "A", "Hello");

            const string src = "%A% #1";
            string dest = StringEscaper.ExpandVariables(s, src);
            const string comp = "Hello ##1";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public void ExpandVariables_4()
        {
            EngineState s = EngineTests.CreateEngineState();
            s.CurDepth = 2;
            s.Variables.SetValue(VarsType.Local, "A", "Hello");

            const string src = "%A% #1";
            string dest = StringEscaper.ExpandVariables(s, src);
            const string comp = "Hello ";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public void ExpandVariables_5()
        {
            EngineState s = EngineTests.CreateEngineState();
            s.CurDepth = 2;
            s.Variables.SetValue(VarsType.Local, "B", "C#");
            s.CurSectionParams[2] = "WPF";

            string[] srcs =
            {
                "A_%A%",
                "B_%B%",
                "C_#1",
                "D_#2"
            };
            List<string> dests = StringEscaper.ExpandVariables(s, srcs);
            string[] comps =
            {
                "A_#$pA#$p",
                "B_C#",
                "C_",
                "D_WPF"
            };

            for (int i = 0; i < dests.Count; i++)
                Assert.IsTrue(dests[i].Equals(comps[i], StringComparison.Ordinal));
        }

        public void ExpandVariables_6()
        {
            EngineState s = EngineTests.CreateEngineState();
            s.CurDepth = 2;

            // In real world, a value must be set with SetValue, so circular reference of variables does not happen 
            s.Variables.SetValue(VarsType.Local, "A", "%B%");
            s.Variables.SetValue(VarsType.Local, "B", "%C%");
            s.Variables.SetValue(VarsType.Local, "C", "%A%"); // Set to [#$pC#4p], preventing circular reference
            s.CurSectionParams[1] = "#2";

            const string src = "%A% #1";
            try { StringEscaper.ExpandVariables(s, src); }
            catch (VariableCircularReferenceException) { return; }

            Assert.Fail();
        }
        #endregion

        #region Preprocess
        [TestMethod]
        [TestCategory("StringEscaper")]
        public void StringEscaper_Preprocess()
        {
            Preprocess_1();
            Preprocess_2();
            Preprocess_3();
            Preprocess_4();
            Preprocess_5();
            Preprocess_6();
        }

        public void Preprocess_1()
        {
            EngineState s = EngineTests.CreateEngineState();
            s.Variables.SetValue(VarsType.Local, "A", "Hello");
            Variables.SetVariable(s, "#1", "World");

            const string src = "%A% #1";
            string dest = StringEscaper.Preprocess(s, src);
            const string comp = "Hello World";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public void Preprocess_2()
        {
            EngineState s = EngineTests.CreateEngineState();
            Variables.SetVariable(s, "#1", "World");

            const string src = "%A% #1";
            string dest = StringEscaper.Preprocess(s, src);
            const string comp = "%A% World";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public void Preprocess_3()
        {
            EngineState s = EngineTests.CreateEngineState();
            s.Variables.SetValue(VarsType.Local, "A", "Hello");

            const string src = "%A% #1";
            string dest = StringEscaper.Preprocess(s, src);
            const string comp = "Hello #1";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public void Preprocess_4()
        {
            EngineState s = EngineTests.CreateEngineState();
            s.CurDepth = 2;
            s.Variables.SetValue(VarsType.Local, "A", "Hello");

            const string src = "%A% #1";
            string dest = StringEscaper.Preprocess(s, src);
            const string comp = "Hello ";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public void Preprocess_5()
        {
            EngineState s = EngineTests.CreateEngineState();
            s.CurDepth = 2;
            s.Variables.SetValue(VarsType.Local, "B", "C#");
            Variables.SetVariable(s, "#2", "WPF");

            string[] srcs =
            {
                "A_%A%",
                "B_%B%",
                "C_#1",
                "D_#2"
            };
            List<string> dests = StringEscaper.Preprocess(s, srcs);
            string[] comps =
            {
                "A_%A%",
                "B_C#",
                "C_",
                "D_WPF"
            };

            for (int i = 0; i < dests.Count; i++)
                Assert.IsTrue(dests[i].Equals(comps[i], StringComparison.Ordinal));
        }

        public void Preprocess_6()
        {
            EngineState s = EngineTests.CreateEngineState();
            s.CurDepth = 2;

            // In real world, a value must be set with SetVariables, so circular reference of variables does not happen 
            s.Variables.SetValue(VarsType.Local, "A", "%B%");
            s.Variables.SetValue(VarsType.Local, "B", "%C%");
            s.Variables.SetValue(VarsType.Local, "C", "%A%");
            Variables.SetVariable(s, "#1", "#2");
            Variables.SetVariable(s, "#2", "#3");
            Variables.SetVariable(s, "#3", "#1");

            const string src = "%A% #1";
            try { StringEscaper.Preprocess(s, src); }
            catch (VariableCircularReferenceException) { return; }

            Assert.Fail();
        }
        #endregion

        #region PathSecurityCheck
        [TestMethod]
        [TestCategory("StringEscaper")]
        public void StringEscaper_PathSecurityCheck()
        {
            PathSecurityCheck_1();
            PathSecurityCheck_2();
            PathSecurityCheck_3();
            PathSecurityCheck_4();
        }

        public void PathSecurityCheck_1()
        {
            string path = Path.Combine(Path.GetTempPath(), "notepad.exe");
            Assert.IsTrue(StringEscaper.PathSecurityCheck(path, out _));
        }

        public void PathSecurityCheck_2()
        {
            string windir = Environment.GetEnvironmentVariable("windir");
            Assert.IsNotNull(windir);
            string path = Path.Combine(windir, "System32", "notepad.exe");
            Assert.IsFalse(StringEscaper.PathSecurityCheck(path, out _));
        }

        public void PathSecurityCheck_3()
        {
            string windir = Environment.GetEnvironmentVariable("ProgramFiles");
            Assert.IsNotNull(windir);
            string path = Path.Combine(windir, "System32", "notepad.exe");
            Assert.IsFalse(StringEscaper.PathSecurityCheck(path, out _));
        }

        public void PathSecurityCheck_4()
        {
            if (Environment.Is64BitProcess)
            { // Only in 64bit process
                string windir = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
                Assert.IsNotNull(windir);
                string path = Path.Combine(windir, "System32", "notepad.exe");
                Assert.IsFalse(StringEscaper.PathSecurityCheck(path, out _));
            }
        }
        #endregion

        #region IsPathValid
        [TestMethod]
        [TestCategory("StringEscaper")]
        public void StringEscaper_IsPathValid()
        {
            void Template(string path, bool result, IEnumerable<char> more = null)
            {
                Assert.IsTrue(StringEscaper.IsPathValid(path, more) == result);
            }

            Template(@"notepad.exe", true);
            Template(@"\notepad.exe", true);
            Template(@"C:\notepad.exe", true);

            Template(@"AB?C", false);
            Template(@"\AB?C", false);
            Template(@"Z:\AB?C", false);

            Template(@"A[BC", true);
            Template(@"\A[BC", true);
            Template(@"E:\A[BC", true);

            Template(@"A[BC", false, new char[] { '[' });
            Template(@"\A[BC", false, new char[] { '[' });
            Template(@"A:\A[BC", false, new char[] { '[' });
        }
        #endregion

        #region IsFileNameValid
        [TestMethod]
        [TestCategory("StringEscaper")]
        public void StringEscaper_IsFileNameValid()
        {
            void Template(string path, bool result, IEnumerable<char> more = null)
            {
                Assert.IsTrue(StringEscaper.IsFileNameValid(path, more) == result);
            }

            Template(@"notepad.exe", true);
            Template(@"\notepad.exe", false);
            Template(@"C:\notepad.exe", false);

            Template(@"AB?C", false);
            Template(@"\AB?C", false);
            Template(@"Z:\AB?C", false);

            Template(@"A[BC", true);
            Template(@"\A[BC", false);
            Template(@"E:\A[BC", false);

            Template(@"A[BC", false, new char[] { '[' });
            Template(@"\A[BC", false, new char[] { '[' });
            Template(@"A:\A[BC", false, new char[] { '[' });
        }
        #endregion

        #region PackRegBinary
        [TestMethod]
        [TestCategory("StringEscaper")]
        public void StringEscaper_PackRegBinary()
        {
            PackRegBinary_1();
            PackRegBinary_2();
        }

        public void PackRegBinary_1()
        {
            byte[] src = Encoding.Unicode.GetBytes("C:\\");
            string dest = StringEscaper.PackRegBinary(src);
            string comp = "43,00,3A,00,5C,00";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public void PackRegBinary_2()
        {
            byte[] src = new byte[] { 0x43, 0x00, 0x3A, 0x00, 0x5C, 0x00 };
            string dest = StringEscaper.PackRegBinary(src);
            string comp = "43,00,3A,00,5C,00";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }
        #endregion

        #region UnpackRegBinary
        [TestMethod]
        [TestCategory("StringEscaper")]
        public void StringEscaper_UnpackRegBinary()
        {
            UnpackRegBinary_1();
        }

        public void UnpackRegBinary_1()
        {
            string src = "43,00,3A,00,5C,00";
            Assert.IsTrue(StringEscaper.UnpackRegBinary(src, out byte[] dest));
            byte[] comp = new byte[] { 0x43, 0x00, 0x3A, 0x00, 0x5C, 0x00 };
            for (int i = 0; i < dest.Length; i++)
                Assert.IsTrue(dest[i] == comp[i]);
        }
        #endregion

        #region PackRegMultiBinary
        [TestMethod]
        [TestCategory("StringEscaper")]
        public void StringEscaper_PackRegMultiBinary()
        {
            PackRegMultiBinary_1();
        }

        public void PackRegMultiBinary_1()
        {
            string[] src = new string[]
            {
                "C:\\",
                "Hello",
                "World",
            };
            string dest = StringEscaper.PackRegMultiBinary(src);
            string comp = "43,00,3A,00,5C,00,00,00,48,00,65,00,6C,00,6C,00,6F,00,00,00,57,00,6F,00,72,00,6C,00,64,00";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }
        #endregion

        #region PackRegMultiString
        [TestMethod]
        [TestCategory("StringEscaper")]
        public void StringEscaper_PackRegMultiString()
        {
            PackRegMultiString_1();
        }

        public void PackRegMultiString_1()
        {
            string[] src = new string[]
            {
                "C:\\",
                "Hello",
                "World",
            };
            string dest = StringEscaper.PackRegMultiString(src);
            string comp = "C:\\#$zHello#$zWorld";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }
        #endregion

        #region UnpackRegMultiString
        [TestMethod]
        [TestCategory("StringEscaper")]
        public void StringEscaper_UnpackRegMultiString()
        {
            UnpackRegMultiString_1();
        }

        public void UnpackRegMultiString_1()
        {
            string src = "C:\\#$zHello#$zWorld";
            List<string> dests = StringEscaper.UnpackRegMultiString(src);
            string[] comps = new string[]
            {
                "C:\\",
                "Hello",
                "World",
            };

            for (int i = 0; i < dests.Count; i++)
                Assert.IsTrue(dests[i].Equals(comps[i], StringComparison.Ordinal));
        }
        #endregion

        #region Utility
        private static string sampleString = null;
        public static string SampleString
        {
            get
            {
                if (sampleString == null)
                {
                    StringBuilder b = new StringBuilder();
                    b.AppendLine("Comma [,]");
                    b.AppendLine("Percent [%]");
                    b.AppendLine("DoubleQuote [\"]");
                    b.AppendLine("Space [ ]");
                    b.AppendLine("Tab [	]");
                    b.AppendLine("Sharp [#]");
                    b.AppendLine("NewLine [");
                    b.Append("]");
                    return sampleString = b.ToString();
                }
                else
                {
                    return sampleString;
                }
            }
        }
        #endregion
    }
}
