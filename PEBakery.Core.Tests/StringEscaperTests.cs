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
using System.Runtime.InteropServices;
using System.Text;

namespace PEBakery.Core.Tests
{
    [TestClass]
    [TestCategory(nameof(StringEscaper))]
    public class StringEscaperTests
    {
        #region Escape
        [TestMethod]
        public void Escape()
        {
            // SampleString
            EscapeTemplate(SampleString, false, false, "Comma [,]#$xPercent [%]#$xDoubleQuote [#$q]#$xSpace [ ]#$xTab [#$t]#$xSharp [##]#$xNewLine [#$x]");
            EscapeTemplate(SampleString, true, false, "Comma#$s[#$c]#$xPercent#$s[%]#$xDoubleQuote#$s[#$q]#$xSpace#$s[#$s]#$xTab#$s[#$t]#$xSharp#$s[##]#$xNewLine#$s[#$x]");
            EscapeTemplate(SampleString, true, true, "Comma#$s[#$c]#$xPercent#$s[#$p]#$xDoubleQuote#$s[#$q]#$xSpace#$s[#$s]#$xTab#$s[#$t]#$xSharp#$s[##]#$xNewLine#$s[#$x]");
            EscapeTemplate(SampleString, false, false, "Comma [,]#$xPercent [%]#$xDoubleQuote [#$q]#$xSpace [ ]#$xTab [#$t]#$xSharp [##]#$xNewLine [#$x]");
            EscapeTemplate("Hello#$xWorld", false, false, "Hello##$xWorld");

            // Overload of IEnumerable<string>
            string[] srcStrs =
            {
                "Comma [,]",
                "Space [ ]",
                "DoubleQuote [\"]"
            };
            string[] comps =
            {
               "Comma#$s[#$c]",
               "Space#$s[#$s]",
               "DoubleQuote#$s[#$q]",
            };
            EscapeArrayTemplate(srcStrs, true, false, comps);

            // #$x issue
            string srcStr = "Hello#$xWorld";
            string expectStr = "Hello##$xWorld";
            EscapeTemplate(srcStr, false, false, expectStr);
        }

        public static void EscapeTemplate(string srcStr, bool fullEscape, bool escapePercent, string expected)
        {
            string dest = StringEscaper.Escape(srcStr, fullEscape, escapePercent);
            Assert.IsTrue(dest.Equals(expected, StringComparison.Ordinal));
        }

        public static void EscapeArrayTemplate(string[] srcStrs, bool fullEscape, bool escapePercent, string[] expected)
        {
            List<string> destStrs = StringEscaper.Escape(srcStrs, fullEscape, escapePercent);
            for (int i = 0; i < destStrs.Count; i++)
                Assert.IsTrue(destStrs[i].Equals(expected[i], StringComparison.Ordinal));
        }
        #endregion

        #region QuoteEscape
        [TestMethod]
        public void QuoteEscape()
        {
            // SampleString
            QuoteEscapeTemplate(SampleString, false, false, "\"Comma [,]#$xPercent [%]#$xDoubleQuote [#$q]#$xSpace [ ]#$xTab [#$t]#$xSharp [##]#$xNewLine [#$x]\"");

            string[] srcs = new string[] { "Comma [,]", "Space [ ]", "DoubleQuote [\"]" };
            string[] expects = new string[]
            {
               "\"Comma [,]\"",
               "\"Space [ ]\"",
               "\"DoubleQuote [#$q]\"",
            };
            QuoteEscapeArrayTemplate(srcs, false, false, expects);
        }

        public static void QuoteEscapeTemplate(string srcStr, bool fullEscape, bool escapePercent, string expected)
        {
            string dest = StringEscaper.QuoteEscape(srcStr, fullEscape, escapePercent);
            Assert.IsTrue(dest.Equals(expected, StringComparison.Ordinal));
        }

        public static void QuoteEscapeArrayTemplate(string[] srcStrs, bool fullEscape, bool escapePercent, string[] expected)
        {
            List<string> destStrs = StringEscaper.QuoteEscape(srcStrs, fullEscape, escapePercent);
            for (int i = 0; i < destStrs.Count; i++)
                Assert.IsTrue(destStrs[i].Equals(expected[i], StringComparison.Ordinal));
        }
        #endregion

        #region Unescape
        [TestMethod]
        public void Unescape()
        {
            UnescapeTemplate("Comma [,]#$xPercent [%]#$xDoubleQuote [#$q]#$xSpace [ ]#$xTab [#$t]#$xSharp [##]#$xNewLine [#$x]", false, SampleString);
            UnescapeTemplate("Comma [,]#$xPercent [#$p]#$xDoubleQuote [#$q]#$xSpace [ ]#$xTab [#$t]#$xSharp [##]#$xNewLine [#$x]", true, SampleString);
            UnescapeTemplate("Comma#$s[#$c]#$xPercent#$s[%]#$xDoubleQuote#$s[#$q]#$xSpace#$s[#$s]#$xTab#$s[#$t]#$xSharp#$s[##]#$xNewLine#$s[#$x]", false, SampleString);
            UnescapeTemplate("Comma#$s[#$c]#$xPercent#$s[#$p]#$xDoubleQuote#$s[#$q]#$xSpace#$s[#$s]#$xTab#$s[#$t]#$xSharp#$s[##]#$xNewLine#$s[#$x]", true, SampleString);
            UnescapeTemplate("Incomplete#$", false, "Incomplete#$");

            string[] srcs = new string[]
            {
               "Comma#$s[#$c]",
               "Space#$s[#$s]",
               "DoubleQuote#$s[#$q]",
            };
            string[] expects = new string[]
            {
                "Comma [,]",
                "Space [ ]",
                "DoubleQuote [\"]",
            };
            UnescapeArrayTemplate(srcs, false, expects);
        }

        public static void UnescapeTemplate(string src, bool escapePercent, string expected)
        {
            string dest = StringEscaper.Unescape(src, escapePercent);
            Assert.IsTrue(dest.Equals(expected, StringComparison.Ordinal));
        }

        public static void UnescapeArrayTemplate(string[] srcs, bool escapePercent, string[] expects)
        {
            List<string> dests = StringEscaper.Unescape(srcs, escapePercent);
            for (int i = 0; i < dests.Count; i++)
                Assert.IsTrue(dests[i].Equals(expects[i], StringComparison.Ordinal));
        }
        #endregion

        #region QuoteUnescape
        [TestMethod]
        public void QuoteUnescape()
        {
            QuoteUnescapeTemplate("\"Comma [,]#$xPercent [%]#$xDoubleQuote [#$q]#$xSpace [ ]#$xTab [#$t]#$xSharp [##]#$xNewLine [#$x]\"", false, SampleString);

            string[] srcs = new string[]
            {
               "\"Comma [#$c]\"",
               "\"Space [ ]\"",
               "\"DoubleQuote [#$q]\"",
            };
            string[] expects = new string[]
            {
                "Comma [,]",
                "Space [ ]",
                "DoubleQuote [\"]",
            };
            QuoteUnescapeArrayTemplate(srcs, false, expects);
        }

        public static void QuoteUnescapeTemplate(string src, bool escapePercent, string expected)
        {
            string dest = StringEscaper.QuoteUnescape(src, escapePercent);
            Assert.IsTrue(dest.Equals(expected, StringComparison.Ordinal));
        }

        public static void QuoteUnescapeArrayTemplate(string[] srcs, bool escapePercent, string[] expects)
        {
            List<string> dests = StringEscaper.QuoteUnescape(srcs, escapePercent);
            for (int i = 0; i < dests.Count; i++)
                Assert.IsTrue(dests[i].Equals(expects[i], StringComparison.Ordinal));
        }
        #endregion

        #region EscapeThenUnescape
        /// <summary>
        /// Check if escaping and then unescaping given string results the same string
        /// </summary>
        [TestMethod]
        public void EscapeThenUnescape()
        {
            EscapeThenUnescapeTemplate(SampleString, false, false);
            EscapeThenUnescapeTemplate(SampleString, true, false);
            EscapeThenUnescapeTemplate(SampleString, false, true);
            EscapeThenUnescapeTemplate(SampleString, true, true);
            EscapeThenUnescapeTemplate("Hello#$xWorld", false, false);

            string[] srcs = new string[]
            {
               "\"Comma [#$c]\"",
               "\"Space [ ]\"",
               "\"DoubleQuote [#$q]\"",
            };
            EscapeThenUnescapeArrayTemplate(srcs, false, false);
            EscapeThenUnescapeArrayTemplate(srcs, true, false);
            EscapeThenUnescapeArrayTemplate(srcs, false, true);
            EscapeThenUnescapeArrayTemplate(srcs, true, false);
        }

        public static void EscapeThenUnescapeTemplate(string src, bool fullEscape, bool escapePercent)
        {
            string escaped = StringEscaper.Escape(src, fullEscape, escapePercent);
            string unescaped = StringEscaper.Unescape(escaped, escapePercent);
            Assert.IsTrue(unescaped.Equals(src, StringComparison.Ordinal));
        }

        public static void EscapeThenUnescapeArrayTemplate(string[] srcs, bool fullEscape, bool escapePercent)
        {
            List<string> escaped = StringEscaper.Escape(srcs, fullEscape, escapePercent);
            List<string> unescaped = StringEscaper.Unescape(escaped, escapePercent);
            for (int i = 0; i < unescaped.Count; i++)
                Assert.IsTrue(unescaped[i].Equals(srcs[i], StringComparison.Ordinal));
        }

        #endregion

        #region ExpandSectionParams
        [TestMethod]
        public void ExpandSectionParams()
        {
            ExpandSectionParams_1();
            ExpandSectionParams_2();
            ExpandSectionParams_3();
            ExpandSectionParams_4();
            ExpandSectionParams_5();
            ExpandSectionParams_6();
            ExpandSectionParams_7();
        }

        public static void ExpandSectionParams_1()
        {
            EngineState s = EngineTests.CreateEngineState();
            EngineTests.PushDepthInfo(s, 1);

            s.Variables.SetValue(VarsType.Local, "A", "Hello");
            Variables.SetVariable(s, "#1", "World");

            const string src = "%A% ##1 #1";
            string dest = StringEscaper.ExpandSectionParams(s, src);
            const string comp = "%A% ##1 World";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public static void ExpandSectionParams_2()
        {
            EngineState s = EngineTests.CreateEngineState();
            EngineTests.PushDepthInfo(s, 1);

            Variables.SetVariable(s, "#1", "World");

            const string src = "%A% ##2 #1";
            string dest = StringEscaper.ExpandSectionParams(s, src);
            const string comp = "%A% ##2 World";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public static void ExpandSectionParams_3()
        {
            EngineState s = EngineTests.CreateEngineState();
            EngineTests.PushDepthInfo(s, 1);

            s.Variables.SetValue(VarsType.Local, "A", "Hello");

            const string src = "%A% #1";
            string dest = StringEscaper.ExpandSectionParams(s, src);
            const string comp = "%A% ##1";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public static void ExpandSectionParams_4()
        {
            EngineState s = EngineTests.CreateEngineState();
            EngineTests.PushDepthInfo(s, 2);

            s.Variables.SetValue(VarsType.Local, "A", "Hello");

            const string src = "%A% #1";
            string dest = StringEscaper.ExpandSectionParams(s, src);
            const string comp = "%A% ";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public static void ExpandSectionParams_5()
        {
            EngineState s = EngineTests.CreateEngineState();
            EngineTests.PushDepthInfo(s, 2);

            s.Variables.SetValue(VarsType.Local, "B", "C#");
            Variables.SetVariable(s, "#2", "WPF");

            string[] srcs =
            {
                "A_%A%",
                "B_%B%",
                "C_#1",
                "D_#2"
            };
            List<string> dests = StringEscaper.ExpandSectionParams(s, srcs);
            string[] comps =
            {
                "A_%A%",
                "B_%B%",
                "C_",
                "D_WPF"
            };

            for (int i = 0; i < dests.Count; i++)
                Assert.IsTrue(dests[i].Equals(comps[i], StringComparison.Ordinal));
        }

        public static void ExpandSectionParams_6()
        {
            EngineState s = EngineTests.CreateEngineState();
            EngineTests.PushDepthInfo(s, 2);

            s.Variables.SetValue(VarsType.Local, "A", "Hello");
            Variables.SetVariable(s, "#1", "#2");
            Variables.SetVariable(s, "#2", "#3");
            Variables.SetVariable(s, "#3", "#1");

            const string src = "%A% #1";
            string dest = StringEscaper.ExpandVariables(s, src);
            const string comp = "Hello ";

            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public static void ExpandSectionParams_7()
        {
            EngineState s = EngineTests.CreateEngineState();
            EngineTests.PushDepthInfo(s, 1);
            s.ReturnValue = "TEST";

            const string src = "##1 ##a ##r #r";
            string dest = StringEscaper.ExpandSectionParams(s, src);
            const string comp = "##1 ##a ##r TEST";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }
        #endregion

        #region ExpandVariables
        [TestMethod]
        public void ExpandVariables()
        {
            ExpandVariables_1();
            ExpandVariables_2();
            ExpandVariables_3();
            ExpandVariables_4();
            ExpandVariables_5();
            ExpandVariables_6();
        }

        public static void ExpandVariables_1()
        {
            EngineState s = EngineTests.CreateEngineState();

            s.Variables.SetValue(VarsType.Local, "A", "Hello");
            s.CurSectionInParams[1] = "World";

            const string src = "%A% #1";
            string dest = StringEscaper.ExpandVariables(s, src);
            const string comp = "Hello World";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public static void ExpandVariables_2()
        {
            EngineState s = EngineTests.CreateEngineState();
            s.CurSectionInParams[1] = "World";

            const string src = "%A% #1";
            string dest = StringEscaper.ExpandVariables(s, src);
            const string comp = "#$pA#$p World";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public static void ExpandVariables_3()
        {
            EngineState s = EngineTests.CreateEngineState();
            EngineTests.PushDepthInfo(s, 1);

            s.Variables.SetValue(VarsType.Local, "A", "Hello");

            const string src = "%A% #1";
            string dest = StringEscaper.ExpandVariables(s, src);
            const string comp = "Hello ##1";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public static void ExpandVariables_4()
        {
            EngineState s = EngineTests.CreateEngineState();
            EngineTests.PushDepthInfo(s, 2);

            s.Variables.SetValue(VarsType.Local, "A", "Hello");

            const string src = "%A% #1";
            string dest = StringEscaper.ExpandVariables(s, src);
            const string comp = "Hello ";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public static void ExpandVariables_5()
        {
            EngineState s = EngineTests.CreateEngineState();
            EngineTests.PushDepthInfo(s, 2);

            s.Variables.SetValue(VarsType.Local, "B", "C#");
            s.CurSectionInParams[2] = "WPF";

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

        public static void ExpandVariables_6()
        {
            EngineState s = EngineTests.CreateEngineState();
            EngineTests.PushDepthInfo(s, 2);

            // In real world, a value must be set with SetValue, so circular reference of variables does not happen 
            s.Variables.SetValue(VarsType.Local, "A", "%B%");
            s.Variables.SetValue(VarsType.Local, "B", "%C%");
            s.Variables.SetValue(VarsType.Local, "C", "%A%"); // Set to [#$pC#4p], preventing circular reference
            s.CurSectionInParams[1] = "#2";

            const string src = "%A% #1";
            try { StringEscaper.ExpandVariables(s, src); }
            catch (VariableCircularReferenceException) { return; }

            Assert.Fail();
        }
        #endregion

        #region Preprocess
        [TestMethod]
        public void Preprocess()
        {
            Preprocess_1();
            Preprocess_2();
            Preprocess_3();
            Preprocess_4();
            Preprocess_5();
            Preprocess_6();
        }

        public static void Preprocess_1()
        {
            EngineState s = EngineTests.CreateEngineState();
            s.Variables.SetValue(VarsType.Local, "A", "Hello");
            Variables.SetVariable(s, "#1", "World");

            const string src = "%A% #1";
            string dest = StringEscaper.Preprocess(s, src);
            const string comp = "Hello World";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public static void Preprocess_2()
        {
            EngineState s = EngineTests.CreateEngineState();
            Variables.SetVariable(s, "#1", "World");

            const string src = "%A% #1";
            string dest = StringEscaper.Preprocess(s, src);
            const string comp = "%A% World";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public static void Preprocess_3()
        {
            EngineState s = EngineTests.CreateEngineState();
            EngineTests.PushDepthInfo(s, 1);

            s.Variables.SetValue(VarsType.Local, "A", "Hello");

            const string src = "%A% #1";
            string dest = StringEscaper.Preprocess(s, src);
            const string comp = "Hello #1";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public static void Preprocess_4()
        {
            EngineState s = EngineTests.CreateEngineState();
            EngineTests.PushDepthInfo(s, 2);

            s.Variables.SetValue(VarsType.Local, "A", "Hello");

            const string src = "%A% #1";
            string dest = StringEscaper.Preprocess(s, src);
            const string comp = "Hello ";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public static void Preprocess_5()
        {
            EngineState s = EngineTests.CreateEngineState();
            EngineTests.PushDepthInfo(s, 2);

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

        public static void Preprocess_6()
        {
            EngineState s = EngineTests.CreateEngineState();
            EngineTests.PushDepthInfo(s, 2);

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
        public void PathSecurityCheck()
        {
            static void Template(string path, bool expected)
            {
                bool result = StringEscaper.PathSecurityCheck(path, out _);
                Assert.AreEqual(expected, result);
            }

            string normalDir = FileHelper.GetTempDir();
            try
            {
                // Valid paths
                Template(Path.Combine(normalDir, "PEBakery.exe"), true);
                Template(Path.Combine(normalDir, "Wildcard.*"), true);
                Template(Path.Combine(normalDir, "Wild*.???"), true);
            }
            finally
            {
                if (Directory.Exists(normalDir))
                    Directory.Delete(normalDir, true);
            }

            // Valid paths
            Template("C:\\", true);
            Template("Wildcard.*", true);
            Template("Wild*.???", true);
            Template(string.Empty, true);

            // Invalid paths
            Template("*\\program.exe", false);

            // %WinDir%
            string winDir = Environment.GetEnvironmentVariable("WinDir");
            Assert.IsNotNull(winDir);
            Template(Path.Combine(winDir, "System32", "notepad.exe"), false);

            // %ProgramFiles%
            string programFiles = Environment.GetEnvironmentVariable("ProgramFiles");
            Assert.IsNotNull(programFiles);
            Template(Path.Combine(programFiles, "PEBakery", "PEBakery.ini"), false);

            // %ProgramFiles(x86)%
            switch (RuntimeInformation.ProcessArchitecture)
            {
                case Architecture.Arm64:
                case Architecture.X64:
                    // Only in 64bit process
                    string programFiles86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
                    Assert.IsNotNull(programFiles86);
                    Template(Path.Combine(programFiles86, "PEBakery", "PEBakery.ini"), false);
                    break;
            }
        }
        #endregion

        #region IsPathValid
        [TestMethod]
        public void IsPathValid()
        {
            static void Template(string path, bool result, IEnumerable<char> more = null)
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
        public void IsFileNameValid()
        {
            static void Template(string path, bool result, IEnumerable<char> more = null)
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

        #region IsFileFilterValid
        [TestMethod]
        public void IsFileFilterValid()
        {
            static void Template(string filter, bool result)
            {
                Assert.IsTrue(StringEscaper.IsFileFilterValid(filter) == result);
            }

            Template(@"Txt Files|*.txt;*.log|All Files|*.*", true);
            Template(@"Txt Files", false);
            Template(@"Txt Files|", false);
            Template(@"Office Files|*.doc;*.xls*.ppt", true);
            Template(@"Office Files|*.doc;*.xls;*.ppt;", true);
            Template(@"Office Files|*.doc;*.xls;*.ppt;;;;;dummy", true);
            Template(@"Word Documents|*.doc|Excel Worksheets|*.xls|PowerPoint Presentations|*.ppt", true);
            Template(@"Word Documents|*.doc|Excel Worksheets|filename", true);
            Template(@"Word Documents|*.doc|Excel Worksheets", false);
            Template(@"Word Documents|*.doc|Excel Worksheets|", false);
        }
        #endregion

        #region PackRegBinary
        [TestMethod]
        public void PackRegBinary()
        {
            PackRegBinary_1();
            PackRegBinary_2();
        }

        public static void PackRegBinary_1()
        {
            byte[] src = Encoding.Unicode.GetBytes("C:\\");
            string dest = StringEscaper.PackRegBinary(src);
            string comp = "43,00,3A,00,5C,00";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }

        public static void PackRegBinary_2()
        {
            byte[] src = new byte[] { 0x43, 0x00, 0x3A, 0x00, 0x5C, 0x00 };
            string dest = StringEscaper.PackRegBinary(src);
            string comp = "43,00,3A,00,5C,00";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }
        #endregion

        #region UnpackRegBinary
        [TestMethod]
        public void UnpackRegBinary()
        {
            UnpackRegBinary_1();
        }

        public static void UnpackRegBinary_1()
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
        public void PackRegMultiBinary()
        {
            PackRegMultiBinary_1();
        }

        public static void PackRegMultiBinary_1()
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
        public void PackRegMultiString()
        {
            string[] src =
            {
                "C:\\",
                "Hello",
                "World",
            };
            string dest = StringEscaper.PackRegMultiString(src);
            const string comp = "C:\\#$zHello#$zWorld";
            Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
        }
        #endregion

        #region UnpackRegMultiString
        [TestMethod]
        public void UnpackRegMultiString()
        {
            const string src = "C:\\#$zHello#$zWorld";
            List<string> dests = StringEscaper.UnpackRegMultiString(src);
            string[] comps =
            {
                "C:\\",
                "Hello",
                "World",
            };

            for (int i = 0; i < dests.Count; i++)
                Assert.IsTrue(dests[i].Equals(comps[i], StringComparison.Ordinal));
        }
        #endregion

        #region List as Concatinated String
        [TestMethod]
        public void UnpackListStr()
        {
            static void Template(string listStr, string delimiter, List<string> compList)
            {
                List<string> destList = StringEscaper.UnpackListStr(listStr, delimiter);
                Assert.AreEqual(compList.Count, destList.Count);
                for (int i = 0; i < destList.Count; i++)
                    Assert.IsTrue(destList[i].Equals(compList[i], StringComparison.Ordinal));
            }

            Template("1|2|3|4|5", "|", new List<string>
            {
                "1",
                "2",
                "3",
                "4",
                "5"
            });
            Template("1|2|3|4|5", "3", new List<string>
            {
                "1|2|",
                "|4|5"
            });
            Template("1|2|3|4|5", "$", new List<string>
            {
                "1|2|3|4|5"
            });
            Template("1|2|3|4|5", "|3|", new List<string>
            {
                "1|2",
                "4|5"
            });

            Template("|a", "|", new List<string>
            {
                string.Empty,
                "a",
            });
            Template("a|", "|", new List<string>
            {
                "a",
                string.Empty,
            });
            Template("|10|98||50|32||0|1|5|2|4|3|", "|", new List<string>
            {
                string.Empty,
                "10",
                "98",
                string.Empty,
                "50",
                "32",
                string.Empty,
                "0",
                "1",
                "5",
                "2",
                "4",
                "3",
                string.Empty,
            });
        }

        [TestMethod]

        public void PackListStr()
        {
            static void Template(List<string> list, string delimiter, string comp)
            {
                string dest = StringEscaper.PackListStr(list, delimiter);
                Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
            }

            Template(new List<string>
            {
                "1",
                "2",
                "3",
                "4",
                "5"
            }, "|", "1|2|3|4|5");
            Template(new List<string>
            {
                "1",
                "2",
                "3",
                "4",
                "5"
            }, "$", "1$2$3$4$5");
            Template(new List<string>
            {
                "1",
                "2",
                "3",
                "4",
                "5"
            }, "12", "1122123124125");
        }
        #endregion

        #region Utility
        private static string _sampleString = null;
        public static string SampleString
        {
            get
            {
                if (_sampleString == null)
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
                    return _sampleString = b.ToString();
                }
                return _sampleString;
            }
        }
        #endregion
    }
}
