/*
    Copyright (C) 2026-present Jonathan Holmgren (Homes32)
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
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace PEBakery.Core.Tests
{
    // PEBakery script validation uses the same CodeParser-based validation the "Syntax Check" button on
    // UtilityWindow's Syntax Checker tab performs. Every RegWrite/RegWriteEx/RegDelete line that
    // ConvertRegToScript outputs gets run back through the PEBakery parser, so a
    // RegistryConverter change that produces invalid script syntax can be caught.
    [TestClass]
    [TestCategory(nameof(RegistryConverter))]
    public class RegistryConverterTests
    {
        #region Test helpers

        /// <summary>
        /// Splits converter output into trimmed, non-empty lines, in output order.
        /// Used instead of comparing the whole Output string so tests don't depend on
        /// exact blank-line placement or the environment's newline convention.
        /// </summary>
        private static List<string> NonEmptyLines(string output)
        {
            return output
                .Replace("\r\n", "\n")
                .Split('\n')
                .Select(l => l.TrimEnd())
                .Where(l => l.Length > 0)
                .ToList();
        }

        private static void AssertHasLine(string output, string expectedLine)
        {
            List<string> lines = NonEmptyLines(output);
            Assert.IsTrue(lines.Contains(expectedLine),
                $"Expected line not found:\n  {expectedLine}\nActual output:\n{output}");
        }

        private static void AssertNoLineContains(string output, string forbiddenSubstring)
        {
            foreach (string line in NonEmptyLines(output))
                Assert.IsFalse(line.Contains(forbiddenSubstring), $"Line unexpectedly contained '{forbiddenSubstring}': {line}");
        }

        private static void AssertAnyWarningContains(ConversionResult result, string substring)
        {
            Assert.IsTrue(result.Warnings.Any(w => w.Contains(substring)),
                $"Expected a warning containing '{substring}'. Actual warnings:\n{string.Join("\n", result.Warnings)}");
        }

        private static RegConvertOptions NoPrefix() => new RegConvertOptions { HivePrefix = string.Empty };

        /// <summary>
        /// Validates RegConverter's Registry to Script conversion by running the results
        /// through the CodeParser (Syntax Check) and ensuring the parser reports zero errors.
        ///
        /// Lines are passed through without any Trim() of our own because CodeParser.ParseCommand
        /// Trim()s every raw line itself before parsing it (both the initial line and, for a
        /// ",\"-continued statement, the next line pulled in). We want this helper to feed CodeParser
        /// the text exactly as GenerateScript produced it, matching how a real caller
        /// (EngineTests.EvalLines, an actual script file) would receive it.
        ///
        /// This still only proves the wrapped statement has valid syntax.
        /// It does not verify the resolved byte values come out correct at runtime. That's covered separately
        /// by CommandRegistryTests.RegWrite_MultiLineContinuation_ArbitraryWhitespaceIsIgnored(),
        /// which runs the same wrapped output through the PEBakery Engine/CommandRegistry against
        /// an actual registry key.
        /// </summary>
        private static void AssertValidPebakeryScript(string scriptText)
        {
            EngineState s = EngineTests.CreateEngineState();
            Project p = s.Project;
            Script sc = p.MainScript;

            ScriptSection section = sc.Sections.ContainsKey(ScriptSection.Names.Process)
                ? sc.Sections[ScriptSection.Names.Process]
                : new ScriptSection(sc, ScriptSection.Names.Process, SectionType.CodeOrUnknown, Array.Empty<string>(), 1);

            List<string> lines = scriptText
                .Replace("\r\n", "\n")
                .Split('\n')
                .ToList();

            CodeParser parser = new CodeParser(section, Global.Setting, sc.Project.Compat);
            (CodeCommand[] _, List<LogInfo> errorLogs) = parser.ParseStatements(lines);

            if (0 < errorLogs.Count)
            {
                StringBuilder b = new StringBuilder();
                b.AppendLine("CodeParser reported errors for generated script:");
                foreach (LogInfo log in errorLogs)
                    b.AppendLine($"  {log.Message} ({log.Command})");
                b.AppendLine("--- Script ---");
                b.AppendLine(scriptText);
                Assert.Fail(b.ToString());
            }
        }

        #endregion

        #region ConvertRegToScript - Basic value types
        [TestMethod]
        public void RegToScript_StringValue()
        {
            const string reg = "Windows Registry Editor Version 5.00\r\n" +
                                "\r\n" +
                                "[HKEY_CURRENT_USER\\Software\\Foo]\r\n" +
                                "\"Bar\"=\"Baz\"\r\n";

            ConversionResult result = RegistryConverter.ConvertRegToScript(reg, NoPrefix());

            Assert.AreEqual("//", result.CommentPrefix);
            Assert.AreEqual(0, result.Warnings.Count);
            AssertHasLine(result.Output, "RegWrite,HKCU,REG_SZ,\"Software\\Foo\",\"Bar\",\"Baz\"");
            AssertValidPebakeryScript(result.Output);
        }

        [TestMethod]
        public void RegToScript_DwordAndDefaultValue()
        {
            // "@" targets the key's unnamed default value; RegistryConverter maps it
            // to an empty ValueName, which is rendered back out as "" (quoted-empty),
            // not @, on the script side.
            const string reg = "Windows Registry Editor Version 5.00\r\n" +
                                "\r\n" +
                                "[HKEY_LOCAL_MACHINE\\Software\\Foo]\r\n" +
                                "@=\"DefaultVal\"\r\n" +
                                "\"Num\"=dword:0000002a\r\n";

            ConversionResult result = RegistryConverter.ConvertRegToScript(reg, NoPrefix());

            Assert.AreEqual(0, result.Warnings.Count);
            AssertHasLine(result.Output, "RegWrite,HKLM,REG_SZ,\"Software\\Foo\",\"\",\"DefaultVal\"");
            AssertHasLine(result.Output, "RegWrite,HKLM,REG_DWORD,\"Software\\Foo\",\"Num\",42");
            AssertValidPebakeryScript(result.Output);
        }
        [TestMethod]
        public void RegToScript_QwordBinaryAndNone()
        {
            const string reg = "Windows Registry Editor Version 5.00\r\n" +
                                "\r\n" +
                                "[HKEY_CURRENT_USER\\Software\\Foo]\r\n" +
                                "\"Big\"=hex(b):01,00,00,00,00,00,00,00\r\n" +
                                "\"Bin\"=hex:01,02,03,04\r\n" +
                                "\"Empty\"=hex(0):\r\n";

            ConversionResult result = RegistryConverter.ConvertRegToScript(reg, NoPrefix());

            Assert.AreEqual(0, result.Warnings.Count);
            AssertHasLine(result.Output, "RegWrite,HKCU,REG_QWORD,\"Software\\Foo\",\"Big\",1");
            AssertHasLine(result.Output, "RegWrite,HKCU,REG_BINARY,\"Software\\Foo\",\"Bin\",01,02,03,04");
            AssertHasLine(result.Output, "RegWrite,HKCU,REG_NONE,\"Software\\Foo\",\"Empty\",\"\"");
            AssertValidPebakeryScript(result.Output);
        }

        [TestMethod]
        public void RegToScript_ExpandStringViaHex2()
        {
            // hex(2) payload is UTF-16LE "abc" + NUL terminator: 61,00,62,00,63,00,00,00
            const string reg = "Windows Registry Editor Version 5.00\r\n" +
                                "\r\n" +
                                "[HKEY_CURRENT_USER\\Software\\Foo]\r\n" +
                                "\"Exp\"=hex(2):61,00,62,00,63,00,00,00\r\n";

            ConversionResult result = RegistryConverter.ConvertRegToScript(reg, NoPrefix());

            Assert.AreEqual(0, result.Warnings.Count);
            AssertHasLine(result.Output, "RegWrite,HKCU,REG_EXPAND_SZ,\"Software\\Foo\",\"Exp\",\"abc\"");
            AssertValidPebakeryScript(result.Output);
        }

        [TestMethod]
        public void RegToScript_MultiStringViaHex7()
        {
            // "One\0Two\0\0" as UTF-16LE:
            //   O  n  e  \0 T  w  o  \0 \0(final terminator)
            const string reg = "Windows Registry Editor Version 5.00\r\n" +
                                "\r\n" +
                                "[HKEY_CURRENT_USER\\Software\\Foo]\r\n" +
                                "\"Multi\"=hex(7):4f,00,6e,00,65,00,00,00,54,00,77,00,6f,00,00,00,00,00,00,00\r\n";

            ConversionResult result = RegistryConverter.ConvertRegToScript(reg, NoPrefix());

            Assert.AreEqual(0, result.Warnings.Count);
            AssertHasLine(result.Output, "RegWrite,HKCU,REG_MULTI_SZ,\"Software\\Foo\",\"Multi\",\"One\",\"Two\"");
            AssertValidPebakeryScript(result.Output);
        }

        [TestMethod]
        public void RegToScript_DeleteKeyAndDeleteValue()
        {
            const string reg = "Windows Registry Editor Version 5.00\r\n" +
                                "\r\n" +
                                "[-HKEY_CURRENT_USER\\Software\\DeadKey]\r\n" +
                                "\r\n" +
                                "[HKEY_CURRENT_USER\\Software\\Foo]\r\n" +
                                "\"DeadValue\"=-\r\n";

            ConversionResult result = RegistryConverter.ConvertRegToScript(reg, NoPrefix());

            Assert.AreEqual(0, result.Warnings.Count);
            AssertHasLine(result.Output, "RegDelete,HKCU,\"Software\\DeadKey\"");
            AssertHasLine(result.Output, "RegDelete,HKCU,\"Software\\Foo\",\"DeadValue\"");
            AssertValidPebakeryScript(result.Output);
        }
        #endregion

        #region ConvertRegToScript - Escaping
        [TestMethod]
        public void RegToScript_CommaAndSpaceForceQuoting()
        {
            const string reg = "Windows Registry Editor Version 5.00\r\n" +
                                "\r\n" +
                                "[HKEY_CURRENT_USER\\Software\\Foo]\r\n" +
                                "\"Name With Space\"=\"Value with, comma\"\r\n";

            ConversionResult result = RegistryConverter.ConvertRegToScript(reg, NoPrefix());

            AssertHasLine(result.Output, "RegWrite,HKCU,REG_SZ,\"Software\\Foo\",\"Name With Space\",\"Value with, comma\"");
            AssertValidPebakeryScript(result.Output);
        }

        [TestMethod]
        public void RegToScript_HashCharacterIsEscaped()
        {
            // StringEscaper.QuoteEscape always escapes '#' regardless of fullEscape
            const string reg = "Windows Registry Editor Version 5.00\r\n" +
                                "\r\n" +
                                "[HKEY_CURRENT_USER\\Software\\Foo]\r\n" +
                                "\"Bar\"=\"Value#WithHash\"\r\n";

            ConversionResult result = RegistryConverter.ConvertRegToScript(reg, NoPrefix());

            AssertHasLine(result.Output, "RegWrite,HKCU,REG_SZ,\"Software\\Foo\",\"Bar\",\"Value#$hWithHash\"");
            AssertValidPebakeryScript(result.Output);
        }
        #endregion

        #region ConvertRegToScript - Embedded-newline .reg quirk (MergeUnterminatedQuotedValues)
        [TestMethod]
        public void RegToScript_RawEmbeddedNewlineInQuotedValue_MergesAndEscapes()
        {
            const string reg = "Windows Registry Editor Version 5.00\r\n" +
                                "\r\n" +
                                "[HKEY_LOCAL_MACHINE\\SYSTEM\\Foo]\r\n" +
                                "\"DeviceId\"=\"Line1\r\n" +
                                "Line2\r\n" +
                                "Line3\"\r\n";

            ConversionResult result = RegistryConverter.ConvertRegToScript(reg, NoPrefix());

            Assert.AreEqual(0, result.Warnings.Count);
            AssertHasLine(result.Output, "RegWrite,HKLM,REG_SZ,\"SYSTEM\\Foo\",\"DeviceId\",\"Line1#$xLine2#$xLine3\"");
            AssertValidPebakeryScript(result.Output);
        }

        [TestMethod]
        public void RegToScript_NeverClosingQuotedValue_WarnsAndSkipsEntry()
        {
            const string reg = "Windows Registry Editor Version 5.00\r\n" +
                                "\r\n" +
                                "[HKEY_LOCAL_MACHINE\\SYSTEM\\Foo]\r\n" +
                                "\"DeviceId\"=\"Line1\r\n" +
                                "Line2\r\n";

            ConversionResult result = RegistryConverter.ConvertRegToScript(reg, NoPrefix());

            AssertAnyWarningContains(result, "closing quote was never found");
            AssertNoLineContains(result.Output, "DeviceId");
        }
        #endregion

        #region ConvertRegToScript - Legacy REGEDIT4 ANSI decoding
        [TestMethod]
        public void RegToScript_Regedit4Header_DecodesHexStringsAsAnsi()
        {
            // Under a modern v5.00 header, hex(1) is always UTF-16LE. Under REGEDIT4 it's
            // single-byte ANSI. 61,62,63,00 is "abc"+NUL in ANSI, but would decode to
            // garbage under a UTF-16 assumption. This is the differentiator that proves
            // ConvertRegToScript actually branches on the legacy header rather than always
            // assuming UTF-16.
            const string reg = "REGEDIT4\r\n" +
                                "\r\n" +
                                "[HKEY_CURRENT_USER\\Software\\Foo]\r\n" +
                                "\"Str\"=hex(1):61,62,63,00\r\n";

            ConversionResult result = RegistryConverter.ConvertRegToScript(reg, NoPrefix());

            AssertAnyWarningContains(result, "legacy REGEDIT4 header");
            AssertHasLine(result.Output, "RegWrite,HKCU,REG_SZ,\"Software\\Foo\",\"Str\",\"abc\"");
            AssertValidPebakeryScript(result.Output);
        }
        #endregion

        #region ConvertRegToScript - Legacy Section Param
        [TestMethod]
        public void RegToScript_HashFollowedByDigit_IsAlwaysEscaped()
        {
            // EscapeToken hash-escapes '#' unconditionally, so a raw "#1"-shaped sequence from
            // .reg data can never survive into generated script text as a bare "#1", 
            // it always comes out as "#$h1". This makes the "Legacy Section Param" compat option
            // a non-issue for this converter's output, regardless of that option's value.
            const string reg = "Windows Registry Editor Version 5.00\r\n" +
                                "\r\n" +
                                "[HKEY_CURRENT_USER\\Software\\Foo]\r\n" +
                                "\"Bar\"=\"Revision#1 Build#a Count#c Ret#r\"\r\n";

            ConversionResult result = RegistryConverter.ConvertRegToScript(reg, NoPrefix());

            AssertHasLine(result.Output, "RegWrite,HKCU,REG_SZ,\"Software\\Foo\",\"Bar\",\"Revision#$h1 Build#$ha Count#$hc Ret#$hr\"");
            AssertValidPebakeryScript(result.Output);
        }
        #endregion

        #region ConvertRegToScript - Malformed input
        [TestMethod]
        public void RegToScript_UnrecognizedHive_WarnsAndSkipsSection()
        {
            const string reg = "Windows Registry Editor Version 5.00\r\n" +
                                "\r\n" +
                                "[HKZZZ\\Software\\Foo]\r\n" +
                                "\"Bar\"=\"Baz\"\r\n";

            ConversionResult result = RegistryConverter.ConvertRegToScript(reg, NoPrefix());

            AssertAnyWarningContains(result, "Unrecognized hive");
            Assert.AreEqual(0, NonEmptyLines(result.Output).Count);
        }

        [TestMethod]
        public void RegToScript_UnparsableValueLine_Warns()
        {
            const string reg = "Windows Registry Editor Version 5.00\r\n" +
                                "\r\n" +
                                "[HKEY_CURRENT_USER\\Software\\Foo]\r\n" +
                                "ThisIsNotAValueLine\r\n";

            ConversionResult result = RegistryConverter.ConvertRegToScript(reg, NoPrefix());

            AssertAnyWarningContains(result, "Could not parse line");
            Assert.AreEqual(0, NonEmptyLines(result.Output).Count);
        }

        [TestMethod]
        public void RegToScript_UnrecognizedValueData_Warns()
        {
            const string reg = "Windows Registry Editor Version 5.00\r\n" +
                                "\r\n" +
                                "[HKEY_CURRENT_USER\\Software\\Foo]\r\n" +
                                "\"Bar\"=weird_data\r\n";

            ConversionResult result = RegistryConverter.ConvertRegToScript(reg, NoPrefix());

            AssertAnyWarningContains(result, "Unrecognized value data");
            Assert.AreEqual(0, NonEmptyLines(result.Output).Count);
        }

        [TestMethod]
        public void RegToScript_ValueLineBeforeAnySection_WarnsAndIsIgnored()
        {
            const string reg = "Windows Registry Editor Version 5.00\r\n" +
                                "\r\n" +
                                "\"Orphan\"=\"Value\"\r\n" +
                                "\r\n" +
                                "[HKEY_CURRENT_USER\\Software\\Foo]\r\n" +
                                "\"Bar\"=\"Baz\"\r\n";

            ConversionResult result = RegistryConverter.ConvertRegToScript(reg, NoPrefix());

            // Warn if there is a value line before any section header. It's malformed input.
            Assert.AreEqual(1, result.Warnings.Count);
            StringAssert.Contains(result.Warnings[0], "Line 3");
            StringAssert.Contains(result.Warnings[0], "Orphan");

            AssertNoLineContains(result.Output, "Orphan");
            AssertHasLine(result.Output, "RegWrite,HKCU,REG_SZ,\"Software\\Foo\",\"Bar\",\"Baz\"");
        }
        #endregion

        #region ConvertRegToScript - Hive-prefix rewriting (.reg -> script only)
        [TestMethod]
        public void RegToScript_HivePrefix_HKLM_RenamesFirstSegmentAndRewritesControlSet()
        {
            const string reg = "Windows Registry Editor Version 5.00\r\n" +
                                "\r\n" +
                                "[HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Services\\Foo]\r\n" +
                                "\"Start\"=dword:00000002\r\n";

            RegConvertOptions opt = new RegConvertOptions { HivePrefix = "Tmp_", ControlSetOverride = "ControlSet001" };
            ConversionResult result = RegistryConverter.ConvertRegToScript(reg, opt);

            AssertAnyWarningContains(result, "Rewrote 'CurrentControlSet' to 'ControlSet001'");
            AssertHasLine(result.Output, "RegWrite,HKLM,REG_DWORD,\"Tmp_SYSTEM\\ControlSet001\\Services\\Foo\",\"Start\",2");
            AssertValidPebakeryScript(result.Output);
        }

        [TestMethod]
        public void RegToScript_HivePrefix_HKCU_DefaultMountSegment()
        {
            const string reg = "Windows Registry Editor Version 5.00\r\n" +
                                "\r\n" +
                                "[HKEY_CURRENT_USER\\Software\\Foo]\r\n" +
                                "\"Bar\"=\"Baz\"\r\n";

            RegConvertOptions opt = new RegConvertOptions { HivePrefix = "Tmp_" };
            ConversionResult result = RegistryConverter.ConvertRegToScript(reg, opt);

            AssertHasLine(result.Output, "RegWrite,HKLM,REG_SZ,\"Tmp_Default\\Software\\Foo\",\"Bar\",\"Baz\"");
            AssertValidPebakeryScript(result.Output);
        }

        [TestMethod]
        public void RegToScript_HivePrefix_HKCR_MapsToSoftwareClasses()
        {
            const string reg = "Windows Registry Editor Version 5.00\r\n" +
                                "\r\n" +
                                "[HKEY_CLASSES_ROOT\\.txt]\r\n" +
                                "\"Bar\"=\"Baz\"\r\n";

            RegConvertOptions opt = new RegConvertOptions { HivePrefix = "Tmp_" };
            ConversionResult result = RegistryConverter.ConvertRegToScript(reg, opt);

            AssertHasLine(result.Output, "RegWrite,HKLM,REG_SZ,\"Tmp_Software\\Classes\\.txt\",\"Bar\",\"Baz\"");
            AssertValidPebakeryScript(result.Output);
        }

        [TestMethod]
        public void RegToScript_HivePrefix_HKUDefault_IsRewritten()
        {
            const string reg = "Windows Registry Editor Version 5.00\r\n" +
                                "\r\n" +
                                "[HKEY_USERS\\.DEFAULT\\Software\\Foo]\r\n" +
                                "\"Bar\"=\"Baz\"\r\n";

            RegConvertOptions opt = new RegConvertOptions { HivePrefix = "Tmp_" };
            ConversionResult result = RegistryConverter.ConvertRegToScript(reg, opt);

            AssertHasLine(result.Output, "RegWrite,HKLM,REG_SZ,\"Tmp_Default\\Software\\Foo\",\"Bar\",\"Baz\"");
            AssertValidPebakeryScript(result.Output);
        }

        [TestMethod]
        public void RegToScript_HivePrefix_HKUNonDefaultSid_IsSkippedWithWarning()
        {
            const string reg = "Windows Registry Editor Version 5.00\r\n" +
                                "\r\n" +
                                "[HKEY_USERS\\S-1-5-21-1111\\Software\\Foo]\r\n" +
                                "\"Bar\"=\"Baz\"\r\n";

            RegConvertOptions opt = new RegConvertOptions { HivePrefix = "Tmp_" };
            ConversionResult result = RegistryConverter.ConvertRegToScript(reg, opt);

            AssertAnyWarningContains(result, "only HKU\\.DEFAULT maps to");
            Assert.AreEqual(0, NonEmptyLines(result.Output).Count);
        }

        [TestMethod]
        public void RegToScript_HivePrefix_HKCC_IsSkippedWithWarning()
        {
            const string reg = "Windows Registry Editor Version 5.00\r\n" +
                                "\r\n" +
                                "[HKEY_CURRENT_CONFIG\\Software\\Foo]\r\n" +
                                "\"Bar\"=\"Baz\"\r\n";

            RegConvertOptions opt = new RegConvertOptions { HivePrefix = "Tmp_" };
            ConversionResult result = RegistryConverter.ConvertRegToScript(reg, opt);

            AssertAnyWarningContains(result, "HKCC is a live-kernel view");
            Assert.AreEqual(0, NonEmptyLines(result.Output).Count);
        }

        [TestMethod]
        public void RegToScript_HivePrefix_Empty_DisablesRewrite()
        {
            const string reg = "Windows Registry Editor Version 5.00\r\n" +
                                "\r\n" +
                                "[HKEY_CURRENT_USER\\Software\\Foo]\r\n" +
                                "\"Bar\"=\"Baz\"\r\n";

            RegConvertOptions opt = new RegConvertOptions { HivePrefix = "" };
            ConversionResult result = RegistryConverter.ConvertRegToScript(reg, opt);

            AssertHasLine(result.Output, "RegWrite,HKCU,REG_SZ,\"Software\\Foo\",\"Bar\",\"Baz\"");
        }

        [TestMethod]
        public void RegToScript_HivePrefix_CurrentControlSetWarning_IsDeduplicatedAcrossMultipleEntries()
        {
            const string reg = "Windows Registry Editor Version 5.00\r\n" +
                                "\r\n" +
                                "[HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Services\\Foo]\r\n" +
                                "\"Start\"=dword:00000002\r\n" +
                                "\"ImagePath\"=\"C:\\\\foo.sys\"\r\n" +
                                "\r\n" +
                                "[HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Services\\Bar]\r\n" +
                                "\"Start\"=dword:00000003\r\n";

            RegConvertOptions opt = new RegConvertOptions { HivePrefix = "Tmp_", ControlSetOverride = "ControlSet001" };
            ConversionResult result = RegistryConverter.ConvertRegToScript(reg, opt);

            // Only ONE warning, despite 3 affected entries across 2 different key paths.
            Assert.AreEqual(1, result.Warnings.Count(w => w.Contains("Rewrote 'CurrentControlSet'")));

            // But every entry's path is still rewritten correctly.
            AssertHasLine(result.Output, "RegWrite,HKLM,REG_DWORD,\"Tmp_SYSTEM\\ControlSet001\\Services\\Foo\",\"Start\",2");
            AssertHasLine(result.Output, "RegWrite,HKLM,REG_SZ,\"Tmp_SYSTEM\\ControlSet001\\Services\\Foo\",\"ImagePath\",\"C:\\foo.sys\"");
            AssertHasLine(result.Output, "RegWrite,HKLM,REG_DWORD,\"Tmp_SYSTEM\\ControlSet001\\Services\\Bar\",\"Start\",3");
            AssertValidPebakeryScript(result.Output);
        }
        #endregion

        #region ConvertRegToScript - Line wrapping/continuation
        [TestMethod]
        public void RegToScript_BinaryValueLongerThan80Columns_WrapsWithContinuation_AndParses()
        {
            // 30 one-byte tokens plus the RegWrite,... head comfortably exceeds the
            // 80-column regedit-style wrap width, forcing a wrap into 2+ physical lines.
            string[] tokens = Enumerable.Range(0, 30).Select(i => (i % 256).ToString("x2")).ToArray();
            string reg = "Windows Registry Editor Version 5.00\r\n" +
                          "\r\n" +
                          "[HKEY_CURRENT_USER\\Software\\Foo]\r\n" +
                          $"\"Bin\"=hex:{string.Join(",", tokens)}\r\n";

            RegConvertOptions opt = new RegConvertOptions { HivePrefix = "" };
            ConversionResult result = RegistryConverter.ConvertRegToScript(reg, opt);

            Assert.AreEqual(0, result.Warnings.Count);
            // Continuation line must exist, and the whole thing still has to be one
            // valid statement as far as CodeParser is concerned.
            Assert.IsTrue(result.Output.Contains(",\\"), "Expected a '\\' line continuation for the wrapped byte list.");
            AssertValidPebakeryScript(result.Output);
        }

        [TestMethod]
        public void RoundTrip_WrappedBinaryValue_ScriptIndentDoesNotCorruptBytes()
        {
            // Companion to RegToScript_BinaryValueLongerThan80Columns_WrapsWithContinuation_AndParses.
            // That test only proves the wrapped RegWrite statement has valid syntax (via CodeParser).
			// We cannot provide independent proof that the 2-space continuation indent
			// AppendWrappedCommand uses is actually stripped by the RegWrite command becaue the
			// CodeParser cannot resolved argument values.
			// This test round-trips the wrapped script back through ConvertScriptToReg and confirms every
            // original byte token survives, in order, with none dropped/duplicated/mangled
            // by the leading whitespace. 
			// Note that this test only proves consistency within this converter's own
            // SplitArgsRespectingQuotes/StripOuterQuotes parsing. True verification is performed in
			// CommandRegistryTests.RegWrite_MultiLineContinuation_ArbitraryWhitespaceIsIgnored()
			// which runs a wrapped statement through the PEBakery Engine and checks the actual
			// bytes written to the registry.
            string[] tokens = Enumerable.Range(0, 40).Select(i => (i % 256).ToString("x2")).ToArray();
            string reg = "Windows Registry Editor Version 5.00\r\n" +
                          "\r\n" +
                          "[HKEY_CURRENT_USER\\Software\\Foo]\r\n" +
                          $"\"Bin\"=hex:{string.Join(",", tokens)}\r\n";

            RegConvertOptions opt = new RegConvertOptions { HivePrefix = "" };
            ConversionResult toScript = RegistryConverter.ConvertRegToScript(reg, opt);

            Assert.AreEqual(0, toScript.Warnings.Count);
            Assert.IsTrue(toScript.Output.Contains(",\\"), "Expected the byte list to wrap into multiple physical lines.");
            AssertValidPebakeryScript(toScript.Output);

            ConversionResult backToReg = RegistryConverter.ConvertScriptToReg(toScript.Output);

            Assert.AreEqual(0, backToReg.Warnings.Count);

            // The round-tripped .reg value is long enough to wrap again on its own (regedit-
            // style), so reconstruct the token list from the (possibly multi-line) output
            // rather than expecting a single unwrapped line.
            List<string> lines = NonEmptyLines(backToReg.Output);
            List<string> valueLines = lines.Where(l => l.Contains("\"Bin\"=hex:") || l.StartsWith("  ")).ToList();
            Assert.IsTrue(valueLines.Count > 0, "Expected to find the 'Bin' value in the round-tripped .reg output.");
            string joined = string.Concat(valueLines.Select(l => l.TrimEnd('\\').Replace("\"Bin\"=hex:", "").TrimStart()));
            CollectionAssert.AreEqual(tokens, joined.Split(',').Where(t => t.Length > 0).ToArray(),
                "Every original byte token must survive the reg -> script -> reg round trip, in order, unaffected by the script's continuation indent.");
        }

        [TestMethod]
        public void ScriptToReg_LongNonstandardBinaryValue_WrapsAtRegeditColumnWidth()
        {
            // Type 0xb is REG_QWORD (single numeric value), so a nonstandard type is used
            // here (0x1234, matching this suite's existing convention for RegWriteEx
            // nonstandard-type tests) to force the byte-list path through ByteTokens and
            // exercise AppendWrappedRegValue's regedit-style 80-column wrapping, rather
            // than the single-value Qword path. 45 one-byte tokens forces at least one
            // continuation split regardless of the exact head-prefix length.
            string[] tokens = Enumerable.Range(0, 45).Select(i => (i % 256).ToString("x2")).ToArray();
            string script = $"RegWriteEx,HKLM,0x1234,\"System\\Foo\",\"Big\",{string.Join(",", tokens)}\r\n";

            ConversionResult result = RegistryConverter.ConvertScriptToReg(script);

            Assert.AreEqual(0, result.Warnings.Count);
            List<string> lines = NonEmptyLines(result.Output);
            List<string> valueLines = lines.Where(l => l.Contains("\"Big\"") || l.StartsWith("  ")).ToList();

            Assert.IsTrue(valueLines.Count >= 2, "Expected the wrapped value to span at least two physical lines.");
            Assert.IsTrue(valueLines[0].StartsWith("\"Big\"=hex(1234):"));
            Assert.IsTrue(valueLines[0].EndsWith(",\\"), "First line should end with a continuation marker.");
            Assert.IsTrue(valueLines[valueLines.Count - 1].StartsWith("  "), "Continuation line should carry the two-space indent.");
            Assert.IsFalse(valueLines[valueLines.Count - 1].EndsWith(",\\"), "Last physical line should not end with a continuation marker.");
            // Every token must appear exactly once, in order, across the joined,
            // de-wrapped output confirms wrapping never drops, duplicates, or
            // reorders bytes regardless of where the line breaks land.
            string joined = string.Concat(valueLines.Select(l =>
                l.TrimEnd('\\').Replace("\"Big\"=hex(1234):", "").TrimStart()));
            CollectionAssert.AreEqual(tokens, joined.Split(',').Where(t => t.Length > 0).ToArray());
        }

        [TestMethod]
        public void RegToScript_RegFileContinuationLines_AreMerged()
        {
            // .reg's own '\' continuation (independent from PEBakery script's) should
            // already be resolved into a single logical hex: value before parsing.
            const string reg = "Windows Registry Editor Version 5.00\r\n" +
                                "\r\n" +
                                "[HKEY_CURRENT_USER\\Software\\Foo]\r\n" +
                                "\"Bin\"=hex:01,02,03,04,\\\r\n" +
                                "  05,06,07,08\r\n";

            ConversionResult result = RegistryConverter.ConvertRegToScript(reg, NoPrefix());

            Assert.AreEqual(0, result.Warnings.Count);
            AssertHasLine(result.Output, "RegWrite,HKCU,REG_BINARY,\"Software\\Foo\",\"Bin\",01,02,03,04,05,06,07,08");
            AssertValidPebakeryScript(result.Output);
        }
        #endregion

        #region ConvertScriptToReg - Basic value types
        [TestMethod]
        public void ScriptToReg_StringValue()
        {
            const string script = "RegWrite,HKCU,REG_SZ,\"Software\\Foo\",\"Bar\",\"Baz\"\r\n";

            ConversionResult result = RegistryConverter.ConvertScriptToReg(script);

            Assert.AreEqual(";", result.CommentPrefix);
            Assert.AreEqual(0, result.Warnings.Count);
            AssertHasLine(result.Output, "[HKEY_CURRENT_USER\\Software\\Foo]");
            AssertHasLine(result.Output, "\"Bar\"=\"Baz\"");
        }

        [TestMethod]
        public void ScriptToReg_DwordAndEmptyValueName()
        {
            const string script = "RegWrite,HKLM,REG_SZ,\"Software\\Foo\",\"\",\"DefaultVal\"\r\n" +
                                   "RegWrite,HKLM,REG_DWORD,\"Software\\Foo\",\"Num\",42\r\n";

            ConversionResult result = RegistryConverter.ConvertScriptToReg(script);

            Assert.AreEqual(0, result.Warnings.Count);
            AssertHasLine(result.Output, "@=\"DefaultVal\"");
            AssertHasLine(result.Output, "\"Num\"=dword:0000002a");
        }

        [TestMethod]
        public void ScriptToReg_NegativeDwordIsAcceptedAndWrittenAsUnsignedHex()
        {
            // Script authors sometimes derive a DWORD flag value with a signed calculator
            // (or a tool that prints signed output) and paste in the result verbatim rather
            // than converting back to hex. -1332477852 is the two's-complement decimal
            // form of 0xB0940064.
            // Since a DWORD is just a 32-bit pattern with no inherent sign, that's a
            // legitimate way to spell the same bits, so it must convert cleanly, with no
            // warning, to the equivalent unsigned hex the .reg format requires.
            const string script = "RegWrite,HKLM,REG_DWORD,\"Software\\Foo\",\"Attributes\",-1332477852\r\n";

            ConversionResult result = RegistryConverter.ConvertScriptToReg(script);

            Assert.AreEqual(0, result.Warnings.Count);
            AssertHasLine(result.Output, "\"Attributes\"=dword:b0940064");
        }

        [TestMethod]
        public void ScriptToReg_NegativeQwordIsAcceptedAndWrittenAsUnsignedHex()
        {
            const string script = "RegWrite,HKLM,REG_QWORD,\"Software\\Foo\",\"Num\",-1\r\n";

            ConversionResult result = RegistryConverter.ConvertScriptToReg(script);

            Assert.AreEqual(0, result.Warnings.Count);
            AssertHasLine(result.Output, "\"Num\"=hex(b):ff,ff,ff,ff,ff,ff,ff,ff");
        }

        [TestMethod]
        public void ScriptToReg_HexDwordTokenIsAccepted()
        {
            // NumberHelper.ParseInt32/ParseUInt32 both accept a "0x"-prefixed literal;
            // the converter previously only handled bare decimal, so this would have
            // thrown a FormatException from ulong.Parse before the fix.
            const string script = "RegWrite,HKLM,REG_DWORD,\"Software\\Foo\",\"Mask\",0x80000000\r\n";

            ConversionResult result = RegistryConverter.ConvertScriptToReg(script);

            Assert.AreEqual(0, result.Warnings.Count);
            AssertHasLine(result.Output, "\"Mask\"=dword:80000000");
        }

        [TestMethod]
        public void ScriptToReg_DwordValueOutOfRangeForSignedAndUnsigned32Bit_Warns()
        {
            // Out of range even as UInt32 (max 4294967295), a genuine authoring error,
            // not a signed/unsigned ambiguity, so this must still fail like PEBakery itself
            // would at runtime, with a message describing the actual DWORD constraint
            // rather than leaking a raw UInt64-parse exception message.
            const string script = "RegWrite,HKLM,REG_DWORD,\"Software\\Foo\",\"Num\",9999999999\r\n";

            ConversionResult result = RegistryConverter.ConvertScriptToReg(script);

            AssertAnyWarningContains(result, "is not a valid DWORD");
            AssertNoLineContains(result.Output, "\"Num\"=dword:");
        }

        [TestMethod]
        public void ScriptToReg_DeleteKeyAndDeleteValue()
        {
            const string script = "RegDelete,HKCU,\"Software\\DeadKey\"\r\n" +
                                   "RegDelete,HKCU,\"Software\\Foo\",\"DeadValue\"\r\n";

            ConversionResult result = RegistryConverter.ConvertScriptToReg(script);

            Assert.AreEqual(0, result.Warnings.Count);
            AssertHasLine(result.Output, "[-HKEY_CURRENT_USER\\Software\\DeadKey]");
            AssertHasLine(result.Output, "\"DeadValue\"=-");
        }

        [TestMethod]
        public void ScriptToReg_NowarnFlagIsStrippedFromOutput()
        {
            const string script = "RegWrite,HKCU,REG_SZ,\"Software\\Foo\",\"Bar\",\"Baz\",NOWARN\r\n";

            ConversionResult result = RegistryConverter.ConvertScriptToReg(script);

            Assert.AreEqual(0, result.Warnings.Count);
            AssertHasLine(result.Output, "\"Bar\"=\"Baz\"");
            AssertNoLineContains(result.Output, "NOWARN");
        }
        #endregion

        #region ConvertScriptToReg - Malformed/unsupported input
        [TestMethod]
        public void ScriptToReg_MalformedRegWrite_Warns()
        {
            // Fewer than the 3 required arguments (HKey,ValueType,KeyPath) is malformed.
            const string script = "RegWrite,HKLM\r\n";

            ConversionResult result = RegistryConverter.ConvertScriptToReg(script);

            AssertAnyWarningContains(result, "Malformed RegWrite line");
            Assert.IsFalse(result.Output.Contains("["), "Expected no registry entries in the output.");
        }

        [TestMethod]
        public void ScriptToReg_RegWriteTwoArgs_StillMalformed()
        {
            // HKey,ValueType alone is still too few arguments, KeyPath is required.
            const string script = "RegWrite,HKLM,REG_NONE\r\n";

            ConversionResult result = RegistryConverter.ConvertScriptToReg(script);

            AssertAnyWarningContains(result, "Malformed RegWrite line");
            Assert.IsFalse(result.Output.Contains("["), "Expected no registry entries in the output.");
        }

        [TestMethod]
        public void ScriptToReg_RegWriteThreeArgs_CreatesKeyOnly_NoValueLine()
        {
            // HKey,ValueType,KeyPath only (no ValueName/Value) is legal PEBakery syntax: it
            // creates the key itself with no value written, mirroring a bare "[HIVE\Path]"
            // .reg section header with no value lines beneath it.
            const string script = "RegWrite,HKLM,REG_NONE,\"Tmp\\Moo\"\r\n";

            ConversionResult result = RegistryConverter.ConvertScriptToReg(script);

            Assert.AreEqual(0, result.Warnings.Count);
            List<string> lines = NonEmptyLines(result.Output);
            int keyIdx = lines.FindIndex(l => l == "[HKEY_LOCAL_MACHINE\\Tmp\\Moo]");
            Assert.AreNotEqual(-1, keyIdx, "Expected the key-only section header in the output.");
            Assert.AreEqual(keyIdx, lines.Count - 1, "Expected no value line to follow the key-only section header.");
        }

        [TestMethod]
        public void ScriptToReg_RegWriteFourArgs_ValueNameOnly_DefaultsToEmptyValue()
        {
            // HKey,ValueType,KeyPath,ValueName (no Value) is still legal, the existing
            // per-Kind trailing-args defaulting kicks in for the missing Value.
            const string script = "RegWrite,HKCU,REG_SZ,\"Software\\Foo\",\"Bar\"\r\n";

            ConversionResult result = RegistryConverter.ConvertScriptToReg(script);

            Assert.AreEqual(0, result.Warnings.Count);
            AssertHasLine(result.Output, "\"Bar\"=\"\"");
        }

        [TestMethod]
        public void ScriptToReg_MalformedRegDelete_Warns()
        {
            const string script = "RegDelete,HKLM\r\n";

            ConversionResult result = RegistryConverter.ConvertScriptToReg(script);

            AssertAnyWarningContains(result, "Malformed RegDelete line");
        }

        [TestMethod]
        public void ScriptToReg_UnrecognizedHkey_Warns()
        {
            const string script = "RegWrite,HKZZZ,REG_SZ,\"Key\",\"Name\",\"Val\"\r\n";

            ConversionResult result = RegistryConverter.ConvertScriptToReg(script);

            AssertAnyWarningContains(result, "Unrecognized HKEY");
        }

        [TestMethod]
        public void ScriptToReg_RegMulti_WarnsAndCannotBeReconstructed()
        {
            const string script = "RegMulti,HKLM,REG_MULTI_SZ,\"Software\\Foo\",\"List\",Set,\"NewItem\"\r\n";

            ConversionResult result = RegistryConverter.ConvertScriptToReg(script);

            AssertAnyWarningContains(result, "RegMulti can only modify an existing value");
            Assert.IsFalse(result.Output.Contains("["), "Expected no registry entries in the output.");
        }

        [TestMethod]
        public void ScriptToReg_NonRegCommands_AreIgnoredSilently()
        {
            const string script = "Echo,\"Hello\"\r\n" +
                                   "RegWrite,HKCU,REG_SZ,\"Software\\Foo\",\"Bar\",\"Baz\"\r\n";

            ConversionResult result = RegistryConverter.ConvertScriptToReg(script);

            Assert.AreEqual(0, result.Warnings.Count);
            AssertHasLine(result.Output, "\"Bar\"=\"Baz\"");
        }

        [TestMethod]
        public void ScriptToReg_LoadedHiveKeyPath_FlagsAmbiguity()
        {
            const string script = "RegHiveLoad,LoadedHive,\"C:\\Mount\\SYSTEM\"\r\n" +
                                   "RegWrite,HKLM,REG_DWORD,\"LoadedHive\\ControlSet001\\Services\\Foo\",\"Start\",2\r\n" +
                                   "RegHiveUnLoad,LoadedHive\r\n";

            ConversionResult result = RegistryConverter.ConvertScriptToReg(script);

            AssertAnyWarningContains(result, "Path was assigned by RegHiveLoad using a variable [LoadedHive].");
            AssertHasLine(result.Output, "; [WARNING] Path was assigned by RegHiveLoad using a variable [LoadedHive]. The true root hive " +
                                         "(SYSTEM/SOFTWARE/NTUSER.DAT/etc.) cannot be recovered from the script and must be fixed manually in the .reg output.");
            AssertHasLine(result.Output, "[HKEY_LOCAL_MACHINE\\LoadedHive\\ControlSet001\\Services\\Foo]");
        }
        #endregion

        #region ConvertScriptToReg - Unresolved %Variable% tokens
        [TestMethod]
        public void ScriptToReg_UnresolvedVariable_ValueLineIsCommentedOut()
        {
            const string script = "RegWrite,HKLM,REG_SZ,\"Software\\Foo\",\"Bar\",\"%SomeVar%\"\r\n";

            ConversionResult result = RegistryConverter.ConvertScriptToReg(script);

            AssertAnyWarningContains(result, "unresolved PEBakery variable");

            List<string> lines = NonEmptyLines(result.Output);
            int valueIdx = lines.FindIndex(l => l.Contains("\"Bar\"=\"%SomeVar%\""));
            Assert.AreNotEqual(-1, valueIdx, "Expected the value line (with the literal unresolved variable) to still appear.");
            Assert.IsTrue(lines[valueIdx].StartsWith(";"), "Value line containing an unresolved variable must be commented out.");
        }

        [TestMethod]
        public void ScriptToReg_UnresolvedVariableInKeyPath_KeyOnlyLineIsCommentedOut()
        {
            // Combines with the RegWrite key-only form (HKey,ValueType,KeyPath) to confirm
            // both fixes compose correctly.
            const string script = "RegWrite,HKLM,REG_NONE,\"%Base%\\Moo\"\r\n";

            ConversionResult result = RegistryConverter.ConvertScriptToReg(script);

            AssertAnyWarningContains(result, "unresolved PEBakery variable");

            List<string> lines = NonEmptyLines(result.Output);
            int keyIdx = lines.FindIndex(l => l.Contains("[HKEY_LOCAL_MACHINE\\%Base%\\Moo]"));
            Assert.AreNotEqual(-1, keyIdx);
            Assert.IsTrue(lines[keyIdx].StartsWith(";"), "Key-only line containing an unresolved variable must be commented out.");
        }

        [TestMethod]
        public void ScriptToReg_UnresolvedVariableInDwordValue_ValueLineIsCommentedOut()
        {
            const string script = "RegWrite,HKLM,REG_DWORD,\"Software\\Foo\",\"Bar\",%SomeVar%\r\n";

            ConversionResult result = RegistryConverter.ConvertScriptToReg(script);

            AssertAnyWarningContains(result, "unresolved PEBakery variable");

            List<string> lines = NonEmptyLines(result.Output);
            int valueIdx = lines.FindIndex(l => l.Contains("\"Bar\"=dword:%SomeVar%"));
            Assert.AreNotEqual(-1, valueIdx, "Expected the value line (with the literal unresolved variable) to still appear.");
            Assert.IsTrue(lines[valueIdx].StartsWith(";"), "Value line containing an unresolved variable must be commented out.");
        }

        [TestMethod]
        public void ScriptToReg_UnresolvedVariableOnFirstValue_DoesNotSuppressLoadedHiveNoteForLaterValues()
        {
            // Regression guard: RegEntry.LoadedHiveNote (loaded-hive ambiguity, printed once at
            // the key header) and the per-entry unresolved-%Variable% note used to share the
            // same field. GenerateRegFile only prints the once-per-section header note when
            // that section's FIRST entry has neither a BlockNote nor HasUnresolvedVariable
            // so when the very first value under a key happened to reference an unresolved
            // %Variable%, the shared field was never even inspected for the header print,
            // silently dropping the loaded-hive note for every OTHER value sharing that same
            // key. "Bar" (first, references %SomeVar%) must not suppress the note that "Baz"
            // (second, no variable) still needs.
            const string script =
                "RegHiveLoad,Tmp_Software,\"C:\\Mount\\SOFTWARE\"\r\n" +
                "RegWrite,HKLM,REG_SZ,\"Tmp_Software\\Foo\",\"Bar\",\"%SomeVar%\"\r\n" +
                "RegWrite,HKLM,REG_DWORD,\"Tmp_Software\\Foo\",\"Baz\",42\r\n" +
                "RegHiveUnLoad,Tmp_Software\r\n";

            ConversionResult result = RegistryConverter.ConvertScriptToReg(script);

            List<string> lines = NonEmptyLines(result.Output);

            int keyIdx = lines.FindIndex(l => l == "[HKEY_LOCAL_MACHINE\\Tmp_Software\\Foo]");
            Assert.AreNotEqual(-1, keyIdx, "Expected the key header.");

            // The loaded-hive note must still appear once, right under the key header,
            // regardless of "Bar" also having an unresolved variable.
            Assert.IsTrue(lines[keyIdx + 1].Contains("Path was assigned by RegHiveLoad"),
                $"Expected the loaded-hive note directly under the key header. Actual line: {lines[keyIdx + 1]}");

            // "Bar" still gets its own per-value unresolved-variable warning and is commented out.
            int barWarningIdx = lines.FindIndex(l => l.Contains("[WARNING]") && l.Contains("%SomeVar%"));
            Assert.AreNotEqual(-1, barWarningIdx, "Expected a per-value unresolved-variable warning for Bar.");
            AssertHasLine(result.Output, "; \"Bar\"=\"%SomeVar%\"");

            // "Baz" has no variable and no BlockNote, so it must NOT be commented out, and
            // must NOT have the loaded-hive note repeated in front of it again.
            AssertHasLine(result.Output, "\"Baz\"=dword:0000002a");
            int bazIdx = lines.FindIndex(l => l == "\"Baz\"=dword:0000002a");
            Assert.IsFalse(lines[bazIdx - 1].Contains("Path was assigned by RegHiveLoad"),
                "The loaded-hive note should only appear once, at the header, not repeated before Baz.");
        }
        #endregion

        #region ConvertScriptToReg - Non-standard types (RegWriteEx)
        [TestMethod]
        public void ScriptToReg_RegWriteExNonstandardType_UsesHexTypeId()
        {
            const string script = "RegWriteEx,HKLM,0x1234,\"System\\Foo\",\"Bar\",01,02,03\r\n";

            ConversionResult result = RegistryConverter.ConvertScriptToReg(script);

            Assert.AreEqual(0, result.Warnings.Count);
            AssertHasLine(result.Output, "\"Bar\"=hex(1234):01,02,03");
        }

        [TestMethod]
        public void ScriptToReg_BinaryValueBundledAsSingleEscapedToken_IsUnescapedAndResplit()
        {
            // Regression guard: this converter's OWN ConvertRegToScript always outputs a byte
            // list as N separate raw, unescaped tokens (9E,3E,07,...), since
            // SplitArgsRespectingQuotes already separates them at the top level. But a
            // hand-written or third-party-generated script can instead bundle the whole byte
            // list into a SINGLE quoted argument using PEBakery's own "#$c" comma-escape (the
            // same convention StringVal/MultiString values already rely on), so
            // SplitArgsRespectingQuotes sees only one token. Previously that token was only
            // quote-stripped, never unescaped, so the literal "#$c" text leaked verbatim into
            // the generated .reg file instead of being reversed back into real commas and
            // re-split into individual bytes.
            const string script = "RegWrite,HKCU,REG_BINARY,\"Software\\Foo\",\"Bin\",\"9E#$c3E#$c07#$c80\"\r\n";

            ConversionResult result = RegistryConverter.ConvertScriptToReg(script);

            Assert.AreEqual(0, result.Warnings.Count);
            AssertHasLine(result.Output, "\"Bin\"=hex:9E,3E,07,80");
            AssertNoLineContains(result.Output, "#$c");
        }

        [TestMethod]
        public void ScriptToReg_BinaryValueNormalRawTokens_StillUnaffected()
        {
            // Companion to the bundled-token regression test above: confirms the fix doesn't
            // disturb the far more common shape (N separate raw tokens, unquoted, no escaping
            // involved at all).
            const string script = "RegWrite,HKCU,REG_BINARY,\"Software\\Foo\",\"Bin\",9E,3E,07,80\r\n";

            ConversionResult result = RegistryConverter.ConvertScriptToReg(script);

            Assert.AreEqual(0, result.Warnings.Count);
            AssertHasLine(result.Output, "\"Bin\"=hex:9E,3E,07,80");
        }
        #endregion

        #region ConvertScriptToReg - Conditionals
        [TestMethod]
        public void ScriptToReg_SingleLineConditionalRegWrite_ExtractedAndCommentedOut()
        {
            // A compound one-liner with two nested If conditions in front of the RegWrite.
            // Nothing here opens a Begin..End block, so this exercises the fallback path
            // alone: cmd starts as "If", RegInsideConditionalRegex locates "RegWrite", and
            // everything before it becomes the BlockNote's condition text.
            string script = "If,EXISTFILE,Foo,If,%Blah%,Equal,True,RegWrite,HKLM,0x1,\"Software\\App\",\"Setting\",\"hello\"\r\n";

            ConversionResult result = RegistryConverter.ConvertScriptToReg(script);

            // The value must still be extracted (not silently dropped) and the summary
            // warning must reflect exactly one commented-out value.
            Assert.AreEqual(1, result.Warnings.Count);
            StringAssert.Contains(result.Warnings[0], "1 value(s)");
            StringAssert.Contains(result.Warnings[0], "commented out");

            List<string> lines = NonEmptyLines(result.Output);

            int noteIdx = lines.FindIndex(l => l.Contains("Single-line conditional"));
            Assert.AreNotEqual(-1, noteIdx, "Expected a single-line-conditional BlockNote comment in the output.");
            Assert.IsTrue(lines[noteIdx].StartsWith("; "));
            StringAssert.Contains(lines[noteIdx], "If,EXISTFILE,Foo,If,%Blah%,Equal,True");
            StringAssert.Contains(lines[noteIdx], "(line 1)");

            // The very next non-empty line should be the commented-out value itself.
            string valueLine = lines[noteIdx + 1];
            Assert.IsTrue(valueLine.StartsWith(";"), "Extracted value line should be commented out.");
            StringAssert.Contains(valueLine, "\"Setting\"=\"hello\"");
        }

        [TestMethod]
        public void ScriptToReg_SingleLineConditionalRegDelete_ExtractedAndCommentedOut()
        {
            // Same fallback path, but for RegDelete (deleting a specific value, since a
            // ValueName argument "Setting" is present).
            string script = "If,EXISTFILE,Foo,RegDelete,HKLM,\"Software\\App\",\"Setting\"\r\n";

            ConversionResult result = RegistryConverter.ConvertScriptToReg(script);

            Assert.AreEqual(1, result.Warnings.Count);

            List<string> lines = NonEmptyLines(result.Output);

            int noteIdx = lines.FindIndex(l => l.Contains("Single-line conditional"));
            Assert.AreNotEqual(-1, noteIdx);
            StringAssert.Contains(lines[noteIdx], "If,EXISTFILE,Foo");
            StringAssert.Contains(lines[noteIdx], "(line 1)");

            string valueLine = lines[noteIdx + 1];
            Assert.IsTrue(valueLine.StartsWith(";"));
            StringAssert.Contains(valueLine, "\"Setting\"=-");
        }

        [TestMethod]
        public void ScriptToReg_SingleLineConditionalInsideBeginEndBlock_CombinesBothNotes()
        {
            // A compound one-liner (If,EXISTFILE,Foo,RegWrite,...) sitting *inside* a normal
            // Begin..End block. Line 2's leading token is "If" but it doesn't end in
            // ",Begin", so it must NOT be treated as another block-opener, it should hit
            // the single-line-conditional fallback, then TagEntryWithBlockInfo should
            // combine the enclosing block's context into the SAME sentence (via
            // RegEntry.BlockNote's "Combined conditional: X inside Y" combination) rather
            // than gluing two independently-tagged clauses together on one line. Uses
            // "Combined", not "Nested", so it doesn't collide with the unrelated
            // "[nested N levels deep]" suffix TagEntryWithBlockInfo attaches to describe
            // pure Begin..End block depth.
            string script =
                "If,%OS%,Equal,WinBuild10240,Begin\r\n" +
                "If,EXISTFILE,Foo,RegWrite,HKLM,0x4,\"Software\\App\",\"Count\",\"5\"\r\n" +
                "End\r\n";

            ConversionResult result = RegistryConverter.ConvertScriptToReg(script);

            Assert.AreEqual(1, result.Warnings.Count);

            List<string> lines = NonEmptyLines(result.Output);

            int noteIdx = lines.FindIndex(l => l.Contains("Combined conditional"));
            Assert.AreNotEqual(-1, noteIdx, "Expected a combined 'Combined conditional' BlockNote comment in the output.");

            // Exactly one [WARNING] tag on the line, not one per clause.
            int warningCount = lines[noteIdx].Split(new[] { "[WARNING]" }, StringSplitOptions.None).Length - 1;
            Assert.AreEqual(1, warningCount, $"Expected exactly one [WARNING] tag on the combined conditional line. Actual: {lines[noteIdx]}");

            // Both the inline condition (line 2) AND the enclosing Begin..End block's
            // condition (line 1) must be present together in the single combined sentence.
            StringAssert.Contains(lines[noteIdx], "If,EXISTFILE,Foo (line 2)");
            StringAssert.Contains(lines[noteIdx], "If,%OS%,Equal,WinBuild10240,Begin (line 1)");
            StringAssert.Contains(lines[noteIdx], "inside");

            string valueLine = lines[noteIdx + 1];
            Assert.IsTrue(valueLine.StartsWith(";"));
            StringAssert.Contains(valueLine, "\"Count\"=dword:00000005");
        }

        [TestMethod]
        public void ScriptToReg_MultipleElseBlocksAtSameDepth_DoesNotDuplicateMutualExclusivityNote()
        {
            // Regression guard for a bug where a THIRD block (a second Else,Begin) at the
            // same nesting depth as an already-paired If/Else pair would incorrectly
            // re-pair with the ALREADY-paired If because only an "If" close (not an
            // "Else" close) ever refreshed lastClosedIfAtDepth, silently duplicating the
            // "MUTUALLY EXCLUSIVE" clause on every entry the If block had already
            // extracted (e.g. "...at line 4 MUTUALLY EXCLUSIVE with Else branch at
            // line 7" stacked twice on one entry).
            string script =
                "If,Foo,Equal,1,Begin\r\n" +
                "RegWrite,HKLM,0x1,\"System\\Foo\",\"Big\",\"hello\"\r\n" +
                "End\r\n" +
                "Else,Begin\r\n" +
                "RegWrite,HKLM,0x1,\"System\\Bar\",\"Big\",\"goodbye\"\r\n" +
                "End\r\n" +
                "Else,Begin\r\n" +
                "RegWrite,HKLM,0x1,\"System\\Baz\",\"Big\",\"maybe\"\r\n" +
                "End\r\n";

            ConversionResult result = RegistryConverter.ConvertScriptToReg(script);

            List<string> lines = NonEmptyLines(result.Output);
            int ifNoteIdx = lines.FindIndex(l => l.Contains("If,Foo,Equal,1,Begin"));
            Assert.AreNotEqual(-1, ifNoteIdx);

            // The If branch's entry must be flagged mutually exclusive with the FIRST
            // Else (line 4) exactly once, not duplicated by the second, unrelated
            // Else,Begin at line 7.
            int mutualCount = lines[ifNoteIdx].Split(new[] { "mutually exclusive" }, StringSplitOptions.None).Length - 1;
            Assert.AreEqual(1, mutualCount, $"Expected exactly one mutually exclusive clause, got: {lines[ifNoteIdx]}");
            StringAssert.Contains(lines[ifNoteIdx], "mutually exclusive with the Else branch at line 4");
        }

        [TestMethod]
        public void ScriptToReg_SingleLineConditionalWithUnresolvedVariable_BothWarningsPrint()
        {
            // Regression guard: when BlockNote and UnresolvedVarNote both apply to the same
            // entry, GenerateRegFile used to print only the BlockNote (an "if (BlockNote != null)
            // ... else if (HasUnresolvedVariable) ..." chain), silently dropping the
            // unresolved-variable warning whenever a conditional-extracted line also
            // referenced a %Variable%. Both must now appear.
            string script = "If,EXISTFILE,Foo,RegWrite,HKLM,0x1,\"Software\\App\",\"Setting\",\"%SomeVar%\"\r\n";

            ConversionResult result = RegistryConverter.ConvertScriptToReg(script);

            List<string> lines = NonEmptyLines(result.Output);

            int blockNoteIdx = lines.FindIndex(l => l.Contains("Single-line conditional"));
            Assert.AreNotEqual(-1, blockNoteIdx, "Expected the single-line-conditional BlockNote.");

            int varNoteIdx = lines.FindIndex(l => l.Contains("[WARNING]") && l.Contains("%SomeVar%"));
            Assert.AreNotEqual(-1, varNoteIdx, "Expected a separate unresolved-variable warning in addition to the BlockNote.");
            Assert.AreNotEqual(blockNoteIdx, varNoteIdx, "The two warnings must be on separate lines, not one overwriting the other.");

            string valueLine = lines[varNoteIdx + 1];
            Assert.IsTrue(valueLine.StartsWith(";"), "Value line should still be commented out.");
            StringAssert.Contains(valueLine, "\"Setting\"=\"%SomeVar%\"");
        }

        [TestMethod]
        public void ScriptToReg_DeeplyNestedSingleLineConditional_StillExtracted()
        {
            // Three chained If conditions in front of the RegWrite. Confirms the
            // RegInsideConditionalRegex-based extraction doesn't care how many If's
            // precede the embedded command, it just needs to find the RegWrite/
            // RegWriteEx/RegDelete keyword and slice from there.
            string script = "If,A,Equal,1,If,B,Equal,2,If,C,Equal,3,RegWrite,HKLM,0x1,\"Software\\App\",\"Deep\",\"x\"\r\n";

            ConversionResult result = RegistryConverter.ConvertScriptToReg(script);

            Assert.AreEqual(1, result.Warnings.Count);

            List<string> lines = NonEmptyLines(result.Output);

            int noteIdx = lines.FindIndex(l => l.Contains("Single-line conditional"));
            Assert.AreNotEqual(-1, noteIdx);
            StringAssert.Contains(lines[noteIdx], "If,A,Equal,1,If,B,Equal,2,If,C,Equal,3");

            string valueLine = lines[noteIdx + 1];
            Assert.IsTrue(valueLine.StartsWith(";"));
            StringAssert.Contains(valueLine, "\"Deep\"=\"x\"");
        }

        [TestMethod]
        public void ScriptToReg_MutuallyExclusiveIfElseBlock_StillWorksAfterSingleLineChange()
        {
            // Regression guard: the earlier Begin..End / If-Else mutual-exclusivity
            // feature must be completely unaffected by the single-line-conditional
            // fallback added afterward, since both paths now share TagEntryWithBlockInfo.
            string script =
                "If,Foo,Equal,1,Begin\r\n" +
                "RegWrite,HKLM,0x1,\"System\\Foo\",\"Big\",\"hello\"\r\n" +
                "End\r\n" +
                "Else,Begin\r\n" +
                "RegWrite,HKLM,0x1,\"System\\Foo\",\"Big\",\"goodbye\"\r\n" +
                "End\r\n";

            ConversionResult result = RegistryConverter.ConvertScriptToReg(script);

            List<string> lines = NonEmptyLines(result.Output);

            int ifNoteIdx = lines.FindIndex(l => l.Contains("If,Foo,Equal,1,Begin"));
            int elseNoteIdx = lines.FindIndex(l => l.Contains("paired with If at line 1"));

            Assert.AreNotEqual(-1, ifNoteIdx);
            Assert.AreNotEqual(-1, elseNoteIdx);
            StringAssert.Contains(lines[ifNoteIdx], "mutually exclusive with the Else branch at line 4");
            StringAssert.Contains(lines[elseNoteIdx], "mutually exclusive with the If branch at line 1");

            // No "single-line conditional" note should appear anywhere; this script
            // only uses Begin/End blocks, never a compound one-liner.
            Assert.IsFalse(lines.Any(l => l.Contains("Single-line conditional")));
        }

        #endregion

        #region ConvertScriptToReg - Sections
        [TestMethod]
        public void ScriptToReg_SectionHeaders_ProduceBannerOnChange()
        {
            const string script = "[Process]\r\n" +
                                   "RegWrite,HKCU,REG_SZ,\"Software\\Foo\",\"Bar\",\"Baz\"\r\n" +
                                   "[RegConfig]\r\n" +
                                   "RegWrite,HKCU,REG_SZ,\"Software\\Foo\",\"Qux\",\"Val\"\r\n";

            ConversionResult result = RegistryConverter.ConvertScriptToReg(script);

            AssertHasLine(result.Output, "; [Process]");
            AssertHasLine(result.Output, "; [RegConfig]");
        }

        [TestMethod]
        public void ScriptToReg_NoSectionHeaders_NoBannerEmitted()
        {
            const string script = "RegWrite,HKCU,REG_SZ,\"Software\\Foo\",\"Bar\",\"Baz\"\r\n";

            ConversionResult result = RegistryConverter.ConvertScriptToReg(script);

            // The disclaimer header also uses '=' dividers, so check for the exact
            // per-section banner line, which only appears when a script
            // [Section] header was encountered.
            List<string> lines = NonEmptyLines(result.Output);
            Assert.IsFalse(lines.Contains("; =================================================="),
                "Did not expect a per-section banner line when the script has no [Section] headers.");
        }
        #endregion

        #region Round-trip
        [TestMethod]
        public void RoundTrip_RegToScriptToReg_PreservesStandardValues()
        {
            const string originalReg = "Windows Registry Editor Version 5.00\r\n" +
                                        "\r\n" +
                                        "[HKEY_CURRENT_USER\\Software\\Foo]\r\n" +
                                        "\"Str\"=\"Hello, World\"\r\n" +
                                        "\"Num\"=dword:0000002a\r\n" +
                                        "\"Bin\"=hex:01,02,03,04\r\n";

            ConversionResult toScript = RegistryConverter.ConvertRegToScript(originalReg, NoPrefix());
            Assert.AreEqual(0, toScript.Warnings.Count);
            AssertValidPebakeryScript(toScript.Output);

            ConversionResult backToReg = RegistryConverter.ConvertScriptToReg(toScript.Output);
            Assert.AreEqual(0, backToReg.Warnings.Count);

            AssertHasLine(backToReg.Output, "\"Str\"=\"Hello, World\"");
            AssertHasLine(backToReg.Output, "\"Num\"=dword:0000002a");
            AssertHasLine(backToReg.Output, "\"Bin\"=hex:01,02,03,04");
        }

        [TestMethod]
        public void RoundTrip_ScriptToRegToScript_PreservesNonstandardType()
        {
            const string originalScript = "RegWriteEx,HKLM,0x1234,\"System\\Foo\",\"Bar\",01,02,03\r\n";

            ConversionResult toReg = RegistryConverter.ConvertScriptToReg(originalScript);
            Assert.AreEqual(0, toReg.Warnings.Count);

            ConversionResult backToScript = RegistryConverter.ConvertRegToScript(toReg.Output, NoPrefix());
            Assert.AreEqual(0, backToScript.Warnings.Count);

            AssertHasLine(backToScript.Output, "RegWriteEx,HKLM,0x1234,\"System\\Foo\",\"Bar\",01,02,03");
            AssertValidPebakeryScript(backToScript.Output);
        }
        #endregion

        #region SanitizeHivePrefix
        [TestMethod]
        public void SanitizeHivePrefix_StripsUnsafeCharacters()
        {
            Assert.AreEqual("TmpFoo123", RegistryConverter.SanitizeHivePrefix("Tmp\\Foo,123 !"));
            Assert.AreEqual(string.Empty, RegistryConverter.SanitizeHivePrefix(null));
            Assert.AreEqual(string.Empty, RegistryConverter.SanitizeHivePrefix("   "));
            Assert.AreEqual(string.Empty, RegistryConverter.SanitizeHivePrefix("\\,\" "));
        }
        #endregion

        #region End-to-end syntax check on a realistic multi-entry .reg file
        [TestMethod]
        public void RegToScript_RealisticMultiEntryFile_ProducesFullyValidScript()
        {
            const string reg = "Windows Registry Editor Version 5.00\r\n" +
                                "\r\n" +
                                "[HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Services\\Foo]\r\n" +
                                "\"Start\"=dword:00000002\r\n" +
                                "\"ImagePath\"=\"C:\\\\Windows\\\\System32\\\\foo.sys\"\r\n" +
                                "\"DependOnGroup\"=hex(7):41,00,42,00,00,00,00,00\r\n" +
                                "\r\n" +
                                "[HKEY_CURRENT_USER\\Software\\Foo]\r\n" +
                                "\"Setting, With Comma\"=\"Some # value\"\r\n" +
                                "\r\n" +
                                "[-HKEY_CURRENT_USER\\Software\\Obsolete]\r\n";

            RegConvertOptions opt = new RegConvertOptions { HivePrefix = "Tmp_" };
            ConversionResult result = RegistryConverter.ConvertRegToScript(reg, opt);

            AssertValidPebakeryScript(result.Output);
        }
        #endregion
    }
}
