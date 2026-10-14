/*
    Copyright (C) 2017-present Hajin Jang
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
using Microsoft.Win32;
using PEBakery.Helper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;


namespace PEBakery.Core.Tests.Command
{
    [TestClass]
    public class CommandRegistryTests
    {
        #region Const String
        private const string RootPath = @"Software\PEBakery\Tests";
        private const string RegReadPath = @"Software\PEBakery\Tests\RegRead";
        private const string RegWritePath = @"Software\PEBakery\Tests\RegWrite";
        private const string RegWriteExPath = @"Software\PEBakery\Tests\RegWriteEx";
        private const string RegDeletePath = @"Software\PEBakery\Tests\RegDelete";
        private const string RegMultiPath = @"Software\PEBakery\Tests\RegMulti";
        private const string RegImportPath = @"Software\PEBakery\Tests\RegImport";
        private const string RegExportPath = @"Software\PEBakery\Tests\RegExport";
        private const string RegCopyPath = @"Software\PEBakery\Tests\RegCopy";
        #endregion

        #region ClassCleanup
        [ClassCleanup]
        public static void ClassCleanup()
        {
            Registry.CurrentUser.DeleteSubKeyTree(RootPath, false);
        }
        #endregion

        #region RegRead
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandRegistry")]
        public void RegRead()
        {
            EngineState s = EngineTests.CreateEngineState();

            // Read Windows' default key
            // Note: Only subKeys which are not going to be changed in later release of Windows should be used!
            // REG_SZ
            ReadTemplate(s, CodeType.RegRead, @"RegRead,HKLM,SOFTWARE\Microsoft\DirectX,Version,%Dest%", "4.09.00.0904");
            // REG_BINARY
            ReadTemplate(s, CodeType.RegRead, @"RegRead,HKLM,SOFTWARE\Microsoft\DirectX,InstalledVersion,%Dest%", "00,00,00,09,00,00,00,00");
            // REG_MULTI_SZ -> Will be expanded automatically by Windows
            // DirectMusic key does not exist on ARM64 devices
            //ReadTemplate(s, CodeType.RegRead, @"RegRead,HKLM,SOFTWARE\Microsoft\DirectMusic,GMFilePath,%Dest%", @"#$pSystemRoot#$p\system32\drivers\GM.DLS");
            // REG_DWORD
            ReadTemplate(s, CodeType.RegRead, @"RegRead,HKLM,""SOFTWARE\Microsoft\Internet Explorer"",IntegratedBrowser,%Dest%", "1");
            // Error
            ReadTemplate(s, CodeType.RegRead, @"RegRead,HKLM,SOFTWARE\Microsoft\DirectX,NotExistValue,%Dest%", string.Empty, ErrorCheck.RuntimeError);

            // Read and write arbitrary keys
            const string subKeyStr = RegReadPath;
            using (RegistryKey rootKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
            {
                rootKey.DeleteSubKeyTree(subKeyStr, false);
            }
            try
            {
                // [*] Test fixed HKEY roots
                // Unknown
                ReadWriteTemplate(s, CodeType.RegRead, $@"RegRead,HKCU,{subKeyStr},Extra,%Dest%", "00,01,02",
                    RegistryHive.CurrentUser, 0x200000, subKeyStr, "Extra", new byte[] { 00, 01, 02 });
                ReadWriteTemplate(s, CodeType.RegRead, $@"RegRead,HKCU,{subKeyStr},Extra,%Dest%", "03,04",
                    RegistryHive.CurrentUser, 0x100000, subKeyStr, "Extra", new byte[] { 03, 04 });
                ReadWriteTemplate(s, CodeType.RegRead, $@"RegRead,HKCU,{subKeyStr},Extra,%Dest%", "05,06,07",
                    RegistryHive.CurrentUser, 0xFFff0009, subKeyStr, "Extra", new byte[] { 05, 06, 07 });
                ReadWriteTemplate(s, CodeType.RegRead, $@"RegRead,HKCU,{subKeyStr},Extra,%Dest%", "08,09",
                    RegistryHive.CurrentUser, 0xffFF100d, subKeyStr, "Extra", new byte[] { 08, 09 });
                ReadWriteTemplate(s, CodeType.RegRead, $@"RegRead,HKCU,{subKeyStr},Extra,%Dest%", string.Empty,
                    RegistryHive.CurrentUser, 0xFFff2012, subKeyStr, "Extra", Array.Empty<byte>());
                // REG_DWORD
                ReadWriteTemplate(s, CodeType.RegRead, $@"RegRead,HKCU,{subKeyStr},UInt32,%Dest%", "1234",
                    RegistryHive.CurrentUser, (uint)RegistryValueKind.DWord, subKeyStr, "UInt32", 1234u);
                ReadWriteTemplate(s, CodeType.RegRead, $@"RegRead,HKCU,{subKeyStr},UInt32,%Dest%", "4294967295",
                    RegistryHive.CurrentUser, (uint)RegistryValueKind.DWord, subKeyStr, "UInt32", 4294967295u);
                // REG_QWORD
                ReadWriteTemplate(s, CodeType.RegRead, $@"RegRead,HKCU,{subKeyStr},UInt64,%Dest%", "4294967296",
                    RegistryHive.CurrentUser, (uint)RegistryValueKind.QWord, subKeyStr, "UInt64", 4294967296ul);

                // [*] Test HKEY roots in variable
                s.ReturnValue = "HKCU";
                // Unknown
                ReadWriteTemplate(s, CodeType.RegRead, $@"RegRead,%^RET%,{subKeyStr},Extra,%Dest%", "00,01,02",
                    RegistryHive.CurrentUser, 0x200000, subKeyStr, "Extra", new byte[] { 00, 01, 02 });
                ReadWriteTemplate(s, CodeType.RegRead, $@"RegRead,%^RET%,{subKeyStr},Extra,%Dest%", "03,04",
                    RegistryHive.CurrentUser, 0x100000, subKeyStr, "Extra", new byte[] { 03, 04 });
                ReadWriteTemplate(s, CodeType.RegRead, $@"RegRead,%^RET%,{subKeyStr},Extra,%Dest%", "05,06,07",
                    RegistryHive.CurrentUser, 0xFFff0009, subKeyStr, "Extra", new byte[] { 05, 06, 07 });
                ReadWriteTemplate(s, CodeType.RegRead, $@"RegRead,%^RET%,{subKeyStr},Extra,%Dest%", "08,09",
                    RegistryHive.CurrentUser, 0xffFF100d, subKeyStr, "Extra", new byte[] { 08, 09 });
                ReadWriteTemplate(s, CodeType.RegRead, $@"RegRead,%^RET%,{subKeyStr},Extra,%Dest%", string.Empty,
                    RegistryHive.CurrentUser, 0xFFff2012, subKeyStr, "Extra", Array.Empty<byte>());
                // REG_DWORD
                ReadWriteTemplate(s, CodeType.RegRead, $@"RegRead,%^RET%,{subKeyStr},UInt32,%Dest%", "1234",
                    RegistryHive.CurrentUser, (uint)RegistryValueKind.DWord, subKeyStr, "UInt32", 1234u);
                ReadWriteTemplate(s, CodeType.RegRead, $@"RegRead,%^RET%,{subKeyStr},UInt32,%Dest%", "4294967295",
                    RegistryHive.CurrentUser, (uint)RegistryValueKind.DWord, subKeyStr, "UInt32", 4294967295u);
                // REG_QWORD
                ReadWriteTemplate(s, CodeType.RegRead, $@"RegRead,%^RET%,{subKeyStr},UInt64,%Dest%", "4294967296",
                    RegistryHive.CurrentUser, (uint)RegistryValueKind.QWord, subKeyStr, "UInt64", 4294967296ul);
            }
            finally
            {
                using RegistryKey rootKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);

                rootKey.DeleteSubKeyTree(subKeyStr, false);
                s.ReturnValue = string.Empty;
            }
        }
        #endregion

        #region RegWrite, RegWriteEx
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandRegistry")]
        public void RegWrite()
        {
            EngineState s = EngineTests.CreateEngineState();

            const string subKeyStr = RegWritePath;
            using (RegistryKey rootKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
            {
                rootKey.DeleteSubKeyTree(subKeyStr, false);
            }
            try
            {
                // Success
                WriteSuccessTemplate(s, CodeType.RegWrite, $@"RegWrite,HKCU,0x0,{subKeyStr},None",
                    RegistryHive.CurrentUser, RegistryValueKind.None, subKeyStr, "None", null);
                WriteSuccessTemplate(s, CodeType.RegWrite, $@"RegWrite,HKCU,0,{subKeyStr},None",
                    RegistryHive.CurrentUser, RegistryValueKind.None, subKeyStr, "None", null);
                WriteSuccessTemplate(s, CodeType.RegWrite, $@"RegWrite,HKCU,REG_NONE,{subKeyStr},None",
                    RegistryHive.CurrentUser, RegistryValueKind.None, subKeyStr, "None", null);
                WriteSuccessTemplate(s, CodeType.RegWrite, $@"RegWrite,HKCU,REG_NONE,{subKeyStr},None,FF",
                    RegistryHive.CurrentUser, RegistryValueKind.None, subKeyStr, "None", "0xFF");
                WriteSuccessTemplate(s, CodeType.RegWrite, $@"RegWrite,HKCU,REG_NONE,{subKeyStr},None,""DE,AD,BE,EF""",
                    RegistryHive.CurrentUser, RegistryValueKind.None, subKeyStr, "None", "0xDEADBEEF");

                WriteSuccessTemplate(s, CodeType.RegWrite, $@"RegWrite,HKCU,0x1,{subKeyStr},String,SZ",
                    RegistryHive.CurrentUser, RegistryValueKind.String, subKeyStr, "String", "SZ");
                WriteSuccessTemplate(s, CodeType.RegWrite, $@"RegWrite,HKCU,1,{subKeyStr},String,SZ",
                    RegistryHive.CurrentUser, RegistryValueKind.String, subKeyStr, "String", "SZ");
                WriteSuccessTemplate(s, CodeType.RegWrite, $@"RegWrite,HKCU,REG_SZ,{subKeyStr},String,SZ",
                    RegistryHive.CurrentUser, RegistryValueKind.String, subKeyStr, "String", "SZ");

                WriteSuccessTemplate(s, CodeType.RegWrite, $@"RegWrite,HKCU,0x2,{subKeyStr},ExpandString,#$pSystemRoot#$p\System32\notepad.exe",
                    RegistryHive.CurrentUser, RegistryValueKind.ExpandString, subKeyStr, "ExpandString", @"%SystemRoot%\System32\notepad.exe");
                WriteSuccessTemplate(s, CodeType.RegWrite, $@"RegWrite,HKCU,2,{subKeyStr},ExpandString,#$pSystemRoot#$p\System32\notepad.exe",
                    RegistryHive.CurrentUser, RegistryValueKind.ExpandString, subKeyStr, "ExpandString", @"%SystemRoot%\System32\notepad.exe");
                WriteSuccessTemplate(s, CodeType.RegWrite, $@"RegWrite,HKCU,REG_EXPAND_SZ,{subKeyStr},ExpandString,#$pSystemRoot#$p\System32\notepad.exe",
                    RegistryHive.CurrentUser, RegistryValueKind.ExpandString, subKeyStr, "ExpandString", @"%SystemRoot%\System32\notepad.exe");

                WriteSuccessTemplate(s, CodeType.RegWrite, $@"RegWrite,HKCU,0x7,{subKeyStr},MultiString,1,2,3",
                    RegistryHive.CurrentUser, RegistryValueKind.MultiString, subKeyStr, "MultiString", new string[] { "1", "2", "3" });
                WriteSuccessTemplate(s, CodeType.RegWrite, $@"RegWrite,HKCU,7,{subKeyStr},MultiString,1,2,3",
                    RegistryHive.CurrentUser, RegistryValueKind.MultiString, subKeyStr, "MultiString", new string[] { "1", "2", "3" });
                WriteSuccessTemplate(s, CodeType.RegWrite, $@"RegWrite,HKCU,REG_MULTI_SZ,{subKeyStr},MultiString,1,2,3",
                    RegistryHive.CurrentUser, RegistryValueKind.MultiString, subKeyStr, "MultiString", new string[] { "1", "2", "3" });

                WriteSuccessTemplate(s, CodeType.RegWrite, $@"RegWrite,HKCU,0x3,{subKeyStr},Binary,00,01,02",
                    RegistryHive.CurrentUser, RegistryValueKind.Binary, subKeyStr, "Binary", new byte[] { 00, 01, 02 });
                WriteSuccessTemplate(s, CodeType.RegWrite, $@"RegWrite,HKCU,3,{subKeyStr},Binary,00,01,02",
                    RegistryHive.CurrentUser, RegistryValueKind.Binary, subKeyStr, "Binary", new byte[] { 00, 01, 02 });
                WriteSuccessTemplate(s, CodeType.RegWrite, $@"RegWrite,HKCU,REG_BINARY,{subKeyStr},Binary,00,01,02",
                    RegistryHive.CurrentUser, RegistryValueKind.Binary, subKeyStr, "Binary", new byte[] { 00, 01, 02 });
                WriteSuccessTemplate(s, CodeType.RegWrite, $@"RegWrite,HKCU,0x3,{subKeyStr},Binary,""03,04""",
                    RegistryHive.CurrentUser, RegistryValueKind.Binary, subKeyStr, "Binary", new byte[] { 03, 04 },
                    null, ErrorCheck.Overwrite);
                WriteSuccessTemplate(s, CodeType.RegWrite, $@"RegWrite,HKCU,0x3,{subKeyStr},Binary,05,06,07,NOWARN",
                    RegistryHive.CurrentUser, RegistryValueKind.Binary, subKeyStr, "Binary", new byte[] { 05, 06, 07 });
                WriteSuccessTemplate(s, CodeType.RegWrite, $@"RegWrite,HKCU,0x3,{subKeyStr},Binary,""08,09"",NOWARN",
                    RegistryHive.CurrentUser, RegistryValueKind.Binary, subKeyStr, "Binary", new byte[] { 08, 09 });

                WriteSuccessTemplate(s, CodeType.RegWrite, $@"RegWrite,HKCU,0x4,{subKeyStr},DWORD,1234",
                    RegistryHive.CurrentUser, RegistryValueKind.DWord, subKeyStr, "DWORD", 1234u);
                WriteSuccessTemplate(s, CodeType.RegWrite, $@"RegWrite,HKCU,4,{subKeyStr},DWORD,1234",
                    RegistryHive.CurrentUser, RegistryValueKind.DWord, subKeyStr, "DWORD", 1234u);
                WriteSuccessTemplate(s, CodeType.RegWrite, $@"RegWrite,HKCU,REG_DWORD,{subKeyStr},DWORD,1234",
                    RegistryHive.CurrentUser, RegistryValueKind.DWord, subKeyStr, "DWORD", 1234u);
                WriteSuccessTemplate(s, CodeType.RegWrite, $@"RegWrite,HKCU,0x4,{subKeyStr},DWORD,-1",
                    RegistryHive.CurrentUser, RegistryValueKind.DWord, subKeyStr, "DWORD", 4294967295u,
                    null, ErrorCheck.Overwrite);
                WriteSuccessTemplate(s, CodeType.RegWrite, $@"RegWrite,HKCU,0x4,{subKeyStr},DWORD,4294967295",
                    RegistryHive.CurrentUser, RegistryValueKind.DWord, subKeyStr, "DWORD", 4294967295u,
                    null, ErrorCheck.Overwrite);

                WriteSuccessTemplate(s, CodeType.RegWrite, $@"RegWrite,HKCU,0xB,{subKeyStr},QWORD,4294967296",
                    RegistryHive.CurrentUser, RegistryValueKind.QWord, subKeyStr, "QWORD", 4294967296ul);
                WriteSuccessTemplate(s, CodeType.RegWrite, $@"RegWrite,HKCU,11,{subKeyStr},QWORD,4294967296",
                    RegistryHive.CurrentUser, RegistryValueKind.QWord, subKeyStr, "QWORD", 4294967296ul);
                WriteSuccessTemplate(s, CodeType.RegWrite, $@"RegWrite,HKCU,REG_QWORD,{subKeyStr},QWORD,4294967296",
                    RegistryHive.CurrentUser, RegistryValueKind.QWord, subKeyStr, "QWORD", 4294967296ul);

                // was RegWriteLegacy
                WriteVarSuccessTemplate(s, CodeType.RegWrite, $@"RegWrite,%^RET%,0x4,{subKeyStr},DWORD,1234", "HKCU",
                    RegistryHive.CurrentUser, RegistryValueKind.DWord, subKeyStr, "DWORD", 1234u,
                    new CompatOption { LegacyRegWrite = true });
                WriteVarSuccessTemplate(s, CodeType.RegWrite, $@"RegWrite,%^RET%,0x4,{subKeyStr},DWORD,1234", "HKCU",
                    RegistryHive.CurrentUser, RegistryValueKind.DWord, subKeyStr, "DWORD", 1234u, 
                    new CompatOption());

                // still is RegWriteLegacy
                WriteVarSuccessTemplate(s, CodeType.RegWriteLegacy, $@"RegWrite,HKCU,%^RET%,{subKeyStr},DWORD,1234", "0x4",
                    RegistryHive.CurrentUser, RegistryValueKind.DWord, subKeyStr, "DWORD", 1234u,
                    new CompatOption { LegacyRegWrite = true });
                WriteVarSuccessTemplate(s, CodeType.RegWriteLegacy, $@"RegWrite,HKCU,%^RET%,{subKeyStr},DWORD,1234", "0x4",
                    RegistryHive.CurrentUser, RegistryValueKind.DWord, subKeyStr, "DWORD", 1234u,
                    new CompatOption(), ErrorCheck.ParserError);


                // Error
                WriteErrorTemplate(s, CodeType.RegWrite, $@"RegWrite,HKCU,0x4,{subKeyStr}", ErrorCheck.ParserError);
                WriteErrorTemplate(s, CodeType.RegWrite, $@"RegWrite,HKCU,0x200000,{subKeyStr},Extra,00,01,02", ErrorCheck.ParserError);
            }
            finally
            {
                using RegistryKey rootKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);

                rootKey.DeleteSubKeyTree(subKeyStr, false);
            }
        }

        // Test of PEBakery.Core.CodeParser's line-continuation handling.
        // (in RegistryConverterTests.cs), which only proves self-consistency within
        // RegistryConverter's own parser. This test instead runs the exact wrapped
        // This test constructs its own multi-line, irregularly-indented RegWrite containing
        // mixed tabs/spaces, varying indent per line, and trailing whitespace and runs it
		// through CodeParser + CommandRegistry (via EngineTests.EvalLines) and checks the bytes
        // actually written to a real registry key. This provides end-to-end confirmation that 
        // indentation of RegWrite/RegWriteEx line continuations do not corrupt the values,
        // regardless of how the whitespace got there (via RegistryConverter, or if extra
        // intentation/whitespace was added to the script directly by the user).
        //
        // CodeParser.ParseCommand directly Trim()s every raw line before parsing it,
        // including a continuation line pulled in by a trailing ",\" so this 2-space indent
        // is stripped before argument splitting.
        // That makes it this test's job to prove the end-to-end result (the actual bytes)
        // written to the registry match, via the PEBakery Engine/CommandRegistry, rather than
        // proving the indent survives untouched (it doesn't, and isn't meant to).
        //
        // Deliberately uses EvalLines, not Eval: Eval calls CodeParser.ParseStatement
        // (singular) on one raw string with no multi-line continuation merging, so it
        // cannot parse a wrapped statement. EvalLines calls CodeParser.ParseStatements(List<string>),
        // which is the real line-merging path.
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandRegistry")]
        public void RegWrite_MultiLineContinuation_ArbitraryWhitespaceIsIgnored()
        {
            byte[] expectedBytes = { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0a, 0x0b, 0x0c, 0x0d, 0x0e };

            // Three physical lines chained by ",\" continuations, each indented differently
            // (tab, spaces, mixed, trailing whitespace) -- deliberately irregular, the way a
            // user editing a script by hand might leave it, rather than one consistent
            // machine-generated indent.
            List<string> rawLines = new List<string>
            {
                $"RegWrite,HKCU,0x3,{RegWritePath},Wrapped,00,01,02,03,04,\\",
                "\t05,06,07,08,\\",
                "      09,0a,0b,\\",
                "  0c,0d,0e   "
            };

            EngineState s = EngineTests.CreateEngineState();
            using (RegistryKey rootKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
            {
                rootKey.DeleteSubKeyTree(RegWritePath, false);
            }
            try
            {
                List<LogInfo> logs = EngineTests.EvalLines(s, rawLines, ErrorCheck.Success);
                EngineTests.CheckErrorLogs(logs, ErrorCheck.Success);

                using RegistryKey rootKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
                using RegistryKey? subKey = rootKey.OpenSubKey(RegWritePath, false);
                Assert.IsNotNull(subKey);

                RegistryValueKind kind = subKey.GetValueKind("Wrapped");
                Assert.AreEqual(RegistryValueKind.Binary, kind);

                byte[]? actualBytes = subKey.GetValue("Wrapped", null, RegistryValueOptions.DoNotExpandEnvironmentNames) as byte[];
                Assert.IsNotNull(actualBytes);
                CollectionAssert.AreEqual(expectedBytes, actualBytes,
                    "Bytes written via an irregularly-indented, multi-line continued RegWrite statement must exactly match the intended values.");
            }
            finally
            {
                using RegistryKey rootKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
                rootKey.DeleteSubKeyTree(RegWritePath, false);
            }
        }

        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandRegistry")]
        public void RegWriteEx()
        {
            EngineState s = EngineTests.CreateEngineState();

            const string subKeyStr = RegWriteExPath;
            using (RegistryKey rootKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
            {
                rootKey.DeleteSubKeyTree(subKeyStr, false);
            }

            try
            {
                // Success - Fixed HKEY roots
                WriteSuccessTemplate(s, CodeType.RegWriteEx, $@"RegWriteEx,HKCU,0x200000,{subKeyStr},Extra,00,01,02",
                    RegistryHive.CurrentUser, RegistryValueKind.Unknown, subKeyStr, "Extra", new byte[] { 00, 01, 02 });
                WriteSuccessTemplate(s, CodeType.RegWriteEx, $@"RegWriteEx,HKCU,0x100000,{subKeyStr},Extra,""03,04""",
                    RegistryHive.CurrentUser, RegistryValueKind.Unknown, subKeyStr, "Extra", new byte[] { 03, 04 },
                    null, ErrorCheck.Overwrite);
                WriteSuccessTemplate(s, CodeType.RegWriteEx, $@"RegWriteEx,HKCU,0xFFff0009,{subKeyStr},Extra,05,06,07,NOWARN",
                    RegistryHive.CurrentUser, RegistryValueKind.Unknown, subKeyStr, "Extra", new byte[] { 05, 06, 07 });
                WriteSuccessTemplate(s, CodeType.RegWriteEx, $@"RegWriteEx,HKCU,0xffFF100d,{subKeyStr},Extra,""08,09"",NOWARN",
                    RegistryHive.CurrentUser, RegistryValueKind.Unknown, subKeyStr, "Extra", new byte[] { 08, 09 });
                WriteSuccessTemplate(s, CodeType.RegWriteEx, $@"RegWriteEx,HKCU,0xFFff2012,{subKeyStr},Extra,,NOWARN",
                    RegistryHive.CurrentUser, RegistryValueKind.Unknown, subKeyStr, "Extra", Array.Empty<byte>());

                // Success - HKEY roots in variables
                s.ReturnValue = "HKCU";
                try
                {
                    WriteSuccessTemplate(s, CodeType.RegWriteEx, $@"RegWriteEx,%^RET%,0x200000,{subKeyStr},Extra,00,01,02",
                    RegistryHive.CurrentUser, RegistryValueKind.Unknown, subKeyStr, "Extra", new byte[] { 00, 01, 02 });
                    WriteSuccessTemplate(s, CodeType.RegWriteEx, $@"RegWriteEx,%^RET%,0x100000,{subKeyStr},Extra,""03,04""",
                        RegistryHive.CurrentUser, RegistryValueKind.Unknown, subKeyStr, "Extra", new byte[] { 03, 04 },
                        null, ErrorCheck.Overwrite);
                    WriteSuccessTemplate(s, CodeType.RegWriteEx, $@"RegWriteEx,%^RET%,0xFFff0009,{subKeyStr},Extra,05,06,07,NOWARN",
                        RegistryHive.CurrentUser, RegistryValueKind.Unknown, subKeyStr, "Extra", new byte[] { 05, 06, 07 });
                    WriteSuccessTemplate(s, CodeType.RegWriteEx, $@"RegWriteEx,%^RET%,0xffFF100d,{subKeyStr},Extra,""08,09"",NOWARN",
                        RegistryHive.CurrentUser, RegistryValueKind.Unknown, subKeyStr, "Extra", new byte[] { 08, 09 });
                    WriteSuccessTemplate(s, CodeType.RegWriteEx, $@"RegWriteEx,%^RET%,0xFFff2012,{subKeyStr},Extra,,NOWARN",
                        RegistryHive.CurrentUser, RegistryValueKind.Unknown, subKeyStr, "Extra", Array.Empty<byte>());
                }
                finally
                {
                    s.ReturnValue = string.Empty;
                }
                
                // Error
                WriteErrorTemplate(s, CodeType.RegWrite, $@"RegWrite,HKCU,0x200000,{subKeyStr},Extra,00,01,02", ErrorCheck.ParserError);
            }
            finally
            {
                using RegistryKey rootKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);

                rootKey.DeleteSubKeyTree(subKeyStr, false);
            }
        }
        #endregion

        #region RegDelete
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandRegistry")]
        public void RegDelete()
        {
            EngineState s = EngineTests.CreateEngineState();

            using (RegistryKey rootKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
            {
                rootKey.DeleteSubKeyTree(RegDeletePath, false);
            }
            try
            {
                void Template(string rawCode, RegistryHive hive, string keyPath, string? valueName, bool createDummy = true, ErrorCheck check = ErrorCheck.Success)
                { // RegDelete,<HKey>,<KeyPath>,[ValueName]
                    using RegistryKey rootKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);

                    if (createDummy)
                    {
                        using RegistryKey subKey = rootKey.CreateSubKey(keyPath, true);

                        subKey.SetValue(valueName, 0, RegistryValueKind.DWord);
                    }

                    EngineTests.Eval(s, rawCode, CodeType.RegDelete, check);

                    if (check == ErrorCheck.Success || check == ErrorCheck.Warning)
                    {
                        if (valueName == null)
                        {
                            using RegistryKey? subKey = rootKey.OpenSubKey(keyPath, false);

                            Assert.IsNull(subKey);
                        }
                        else
                        {
                            using RegistryKey? subKey = rootKey.OpenSubKey(keyPath, false);

                            if (createDummy)
                            {
                                Assert.IsNotNull(subKey);

                                object? valueData = subKey.GetValue(valueName);
                                Assert.IsNull(valueData);
                            }
                            else
                            {
                                Assert.IsNull(subKey);
                            }
                        }
                    }
                }

                // Success - HKEY roots in constant
                Template($@"RegDelete,HKCU,{RegDeletePath},ValueName", RegistryHive.CurrentUser, RegDeletePath, "ValueName");
                Template($@"RegDelete,HKCU,{RegDeletePath}", RegistryHive.CurrentUser, RegDeletePath, null);

                // Success - HKEY roots in variable
                s.ReturnValue = "HKCU";
                try
                {
                    Template($@"RegDelete,%^RET%,{RegDeletePath},ValueName", RegistryHive.CurrentUser, RegDeletePath, "ValueName");
                    Template($@"RegDelete,%^RET%,{RegDeletePath}", RegistryHive.CurrentUser, RegDeletePath, null);
                }
                finally
                {
                    s.ReturnValue = string.Empty;
                }

                // Warning
                Template($@"RegDelete,HKCU,{RegDeletePath},ValueName", RegistryHive.CurrentUser, RegDeletePath, "ValueName", false, ErrorCheck.Warning);
                Template($@"RegDelete,HKCU,{RegDeletePath}", RegistryHive.CurrentUser, RegDeletePath, null, false, ErrorCheck.Warning);
            }
            finally
            {
                using RegistryKey rootKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);

                rootKey.DeleteSubKeyTree(RegDeletePath, false);
            }
        }
        #endregion

        #region RegMulti
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandRegistry")]
        public void RegMulti()
        {
            EngineState s = EngineTests.CreateEngineState();

            void NormalTemplate(string rawCode, string[] compStrs, ErrorCheck check = ErrorCheck.Success)
            {
                using (RegistryKey subKey = Registry.CurrentUser.CreateSubKey(RegMultiPath, true))
                {
                    subKey.SetValue("Key", new string[] { "A", "B" }, RegistryValueKind.MultiString);
                }

                EngineTests.Eval(s, rawCode, CodeType.RegMulti, check);

                if (check == ErrorCheck.Success || check == ErrorCheck.Warning)
                {
                    using (RegistryKey? subKey = Registry.CurrentUser.OpenSubKey(RegMultiPath, false))
                    {
                        Assert.IsNotNull(subKey);

                        RegistryValueKind kind = subKey.GetValueKind("Key");
                        Assert.AreEqual(RegistryValueKind.MultiString, kind);

                        object? valueData = subKey.GetValue("Key", null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                        Assert.IsNotNull(valueData);

                        string[] destStrs = (string[])valueData;
                        Assert.AreEqual(compStrs.Length, destStrs.Length);
                        for (int i = 0; i < destStrs.Length; i++)
                            Assert.IsTrue(destStrs[i].Equals(compStrs[i], StringComparison.Ordinal));
                    }
                }
            }
            void IndexTemplate(string rawCode, int compIdx, string compStr, ErrorCheck check = ErrorCheck.Success)
            {
                using (RegistryKey subKey = Registry.CurrentUser.CreateSubKey(RegMultiPath, true))
                {
                    subKey.SetValue("Key", new string[] { "A", "B" }, RegistryValueKind.MultiString);
                }

                EngineTests.Eval(s, rawCode, CodeType.RegMulti, check);

                if (check == ErrorCheck.Success || check == ErrorCheck.Warning)
                {
                    using RegistryKey? subKey = Registry.CurrentUser.OpenSubKey(RegMultiPath, false);
                    Assert.IsNotNull(subKey);

                    RegistryValueKind kind = subKey.GetValueKind("Key");
                    Assert.IsTrue(kind == RegistryValueKind.MultiString);

                    object? valueData = subKey.GetValue("Key", null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                    Assert.IsNotNull(valueData);

                    string[] destStrs = (string[])valueData;
                    Assert.IsTrue(0 <= compIdx && compIdx <= destStrs.Length);
                    if (1 <= compIdx && compIdx <= destStrs.Length)
                    {
                        Assert.IsTrue(destStrs[compIdx - 1].Equals(compStr, StringComparison.Ordinal));
                        Assert.IsTrue(s.Variables["Dest"].Equals(compIdx.ToString(), StringComparison.Ordinal));
                    }
                }
            }
            void ErrorTemplate(string rawCode, ErrorCheck check)
            {
                EngineTests.Eval(s, rawCode, CodeType.RegWrite, check);
            }

            const string subKeyStr = RegMultiPath;
            using (RegistryKey rootKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
            {
                rootKey.DeleteSubKeyTree(subKeyStr, false);
            }

            try
            {
                // Append
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Append,C", ["A", "B", "C"]);
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Append,B", ["A", "B"], ErrorCheck.Warning);
                // Prepend
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Prepend,C", ["C", "A", "B"]);
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Prepend,A", ["A", "B"], ErrorCheck.Warning);
                // Before
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Before,A,C", ["C", "A", "B"]);
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Before,B,C", ["A", "C", "B"]);
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Before,D,C", ["A", "B"], ErrorCheck.RuntimeError);
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Before,A,B", ["A", "B"], ErrorCheck.Warning);
                // Behind
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Behind,A,C", ["A", "C", "B"]);
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Behind,B,C", ["A", "B", "C"]);
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Behind,D,C", ["A", "B"], ErrorCheck.RuntimeError);
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Behind,A,B", ["A", "B"], ErrorCheck.Warning);
                // Place
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Place,1,C", ["C", "A", "B"]);
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Place,2,C", ["A", "C", "B"]);
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Place,3,C", ["A", "B", "C"]);
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Place,0,C", ["C", "A", "B"], ErrorCheck.RuntimeError);
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Place,4,C", ["C", "A", "B"], ErrorCheck.RuntimeError);
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Place,1,B", ["A", "B"], ErrorCheck.Warning);
                // Delete
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Delete,A", ["B"]);
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Delete,B", ["A"]);
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Delete,C", ["A", "B"], ErrorCheck.RuntimeError);
                // Index
                IndexTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Index,A,%Dest%", 1, "A");
                IndexTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Index,B,%Dest%", 2, "B");
                IndexTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Index,C,%Dest%", 0, "C");
                IndexTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Index,A", 1, "A", ErrorCheck.RuntimeError);
                // Success - HKEY roots in constant
                s.ReturnValue = "HKCU";
                try
                {
                    NormalTemplate($@"RegMulti,%^RET%,{subKeyStr},Key,Append,C", ["A", "B", "C"]);
                    NormalTemplate($@"RegMulti,%^RET%,{subKeyStr},Key,Prepend,A", ["A", "B"], ErrorCheck.Warning);
                    NormalTemplate($@"RegMulti,%^RET%,{subKeyStr},Key,Before,D,C", ["A", "B"], ErrorCheck.RuntimeError);
                    NormalTemplate($@"RegMulti,%^RET%,{subKeyStr},Key,Behind,B,C", ["A", "B", "C"]);
                    NormalTemplate($@"RegMulti,%^RET%,{subKeyStr},Key,Place,1,B", ["A", "B"], ErrorCheck.Warning);
                    NormalTemplate($@"RegMulti,%^RET%,{subKeyStr},Key,Delete,A", ["B"]);
                    IndexTemplate($@"RegMulti,%^RET%,{subKeyStr},Key,Index,B,%Dest%", 2, "B");
                }
                finally
                {
                    s.ReturnValue = string.Empty;
                }
                // Error
                ErrorTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Place,1,C,E", ErrorCheck.ParserError);
            }
            finally
            {
                using RegistryKey rootKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);

                rootKey.DeleteSubKeyTree(subKeyStr, false);
            }
        }
        #endregion

        #region RegImport
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandRegistry")]
        public void RegImport()
        {
            EngineState s = EngineTests.CreateEngineState();

            const string subKeyStr = RegImportPath;
            string tempFile = Path.GetTempFileName();
            using (RegistryKey rootKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
            {
                rootKey.DeleteSubKeyTree(subKeyStr, false);
            }
            try
            {
                StringBuilder b = new StringBuilder();
                b.AppendLine("Windows Registry Editor Version 5.00");
                b.AppendLine();
                b.AppendLine("[HKEY_CURRENT_USER\\Software\\PEBakery\\Tests\\RegImport]");
                b.AppendLine("\"String\"=\"Str\"");
                b.AppendLine("\"ExpandString\"=hex(2):25,00,57,00,69,00,6e,00,44,00,69,00,72,00,25,00,00,00");
                b.AppendLine("\"Binary\"=hex:a0,a1,a2");
                b.AppendLine("\"DWORD\"=dword:00000003");
                b.AppendLine("\"QWORD\"=hex(b):00,00,00,00,01,00,00,00");
                b.AppendLine();
                using (StreamWriter w = new StreamWriter(tempFile, false, Encoding.Unicode))
                {
                    w.Write(b.ToString());
                }

                EngineTests.Eval(s, $@"RegImport,{tempFile}", CodeType.RegImport, ErrorCheck.Success);

                using (RegistryKey rootKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
                using (RegistryKey subKey = rootKey.CreateSubKey(subKeyStr, false))
                {
                    Assert.AreEqual(RegistryValueKind.String, subKey.GetValueKind("String"));
                    string? stringValue = subKey.GetValue("String", null) as string;
                    Assert.IsNotNull(stringValue);
                    Assert.IsTrue(stringValue.Equals("Str"));

                    Assert.AreEqual(RegistryValueKind.ExpandString, subKey.GetValueKind("ExpandString"));
                    stringValue = subKey.GetValue("ExpandString", null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                    Assert.IsNotNull(stringValue);
                    Assert.IsTrue(stringValue.Equals("%WinDir%"));

                    Assert.AreEqual(RegistryValueKind.Binary, subKey.GetValueKind("Binary"));
                    byte[]? byteValue = subKey.GetValue("Binary", null) as byte[];
                    Assert.IsNotNull(byteValue);
                    Assert.IsTrue(byteValue.SequenceEqual(new byte[] { 0xA0, 0xA1, 0xA2 }));

                    Assert.AreEqual(RegistryValueKind.DWord, subKey.GetValueKind("DWORD"));
                    Assert.IsNotNull(subKey.GetValue("DWORD", null));
                    object? valObj = subKey.GetValue("DWORD", null);
                    Assert.IsNotNull(valObj);
                    uint dword = (uint)(int)valObj;
                    Assert.AreEqual(0x03u, dword);

                    Assert.AreEqual(RegistryValueKind.QWord, subKey.GetValueKind("QWORD"));
                    Assert.IsNotNull(subKey.GetValue("QWORD", null));
                    valObj = subKey.GetValue("QWORD", null);
                    Assert.IsNotNull(valObj);
                    ulong qword = (ulong)(long)valObj;
                    Assert.AreEqual(0x100000000u, qword);
                }
            }
            finally
            {
                using (RegistryKey rootKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
                {
                    rootKey.DeleteSubKeyTree(subKeyStr, false);
                }

                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }
        #endregion

        #region RegExport
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandRegistry")]
        public void RegExport()
        {
            EngineState s = EngineTests.CreateEngineState();

            const string subKeyStr = RegExportPath;
            string tempFile = Path.GetTempFileName();
            using (RegistryKey rootKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
            {
                rootKey.DeleteSubKeyTree(subKeyStr, false);
            }
                
            try
            {
                void Template(string rawCode, string compStr, ErrorCheck check = ErrorCheck.Success)
                {
                    EngineTests.Eval(s, rawCode, CodeType.RegExport, check);
                    if (check == ErrorCheck.Success || check == ErrorCheck.Warning || check == ErrorCheck.Overwrite)
                    {
                        string exportStr;
                        using (StreamReader r = new StreamReader(tempFile, Encoding.UTF8))
                        {
                            exportStr = r.ReadToEnd();
                        }

                        Assert.IsTrue(exportStr.Equals(compStr, StringComparison.Ordinal));
                    }
                }

                using (RegistryKey rootKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
                using (RegistryKey subKey = rootKey.CreateSubKey(subKeyStr, true))
                {
                    subKey.SetValue("String", "Str", RegistryValueKind.String);
                    subKey.SetValue("ExpandString", "%WinDir%", RegistryValueKind.ExpandString);
                    subKey.SetValue("Binary", new byte[] { 0xA0, 0xA1, 0xA2 }, RegistryValueKind.Binary);
                    subKey.SetValue("DWORD", 0x03u, RegistryValueKind.DWord);
                    subKey.SetValue("QWORD", 0x100000000u, RegistryValueKind.QWord);
                }

                StringBuilder b = new StringBuilder();
                b.AppendLine("Windows Registry Editor Version 5.00");
                b.AppendLine();
                b.AppendLine("[HKEY_CURRENT_USER\\Software\\PEBakery\\Tests\\RegExport]");
                b.AppendLine("\"String\"=\"Str\"");
                b.AppendLine("\"ExpandString\"=hex(2):25,00,57,00,69,00,6e,00,44,00,69,00,72,00,25,00,00,00");
                b.AppendLine("\"Binary\"=hex:a0,a1,a2");
                b.AppendLine("\"DWORD\"=dword:00000003");
                b.AppendLine("\"QWORD\"=hex(b):00,00,00,00,01,00,00,00");
                b.AppendLine();
                string resultStr = b.ToString();
                Template($@"RegExport,HKCU,Software\PEBakery\Tests\RegExport,{tempFile}", resultStr);
            }
            finally
            {
                using (RegistryKey rootKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
                {
                    rootKey.DeleteSubKeyTree(subKeyStr, false);
                }
                
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }
        #endregion

        #region RegCopy
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandRegistry")]
        public void RegCopy()
        {
            EngineState s = EngineTests.CreateEngineState();

            string singleSrcSet = Path.Combine(RegCopyPath, "Src");
            string multiSrcSet1 = Path.Combine(RegCopyPath, "Set*");
            string multiSrcSet2 = Path.Combine(RegCopyPath, "Set?0");
            string destSet = Path.Combine(RegCopyPath, "Dest");

            // Success - HKEY root in a constant
            SingleTemplate($@"RegCopy,HKCU,{singleSrcSet},HKCU,{destSet}", RegistryHive.CurrentUser,
                singleSrcSet, destSet);
            WildcardTemplate($@"RegCopy,HKCU,{multiSrcSet1},HKCU,{destSet},WILDCARD", RegistryHive.CurrentUser,
                RegCopyPath, ["Set10", "Set20", "Set31"],
                destSet, ["Set10", "Set20", "Set31"]);
            WildcardTemplate($@"RegCopy,HKCU,{multiSrcSet2},HKCU,{destSet},WILDCARD", RegistryHive.CurrentUser,
                RegCopyPath, ["Set10", "Set20", "Set31"],
                destSet, ["Set10", "Set20"]);

            // Success - HKEY root in a variable
            s.ReturnValue = "HKCU";
            try
            {
                SingleTemplate($@"RegCopy,%^RET%,{singleSrcSet},%^RET%,{destSet}", RegistryHive.CurrentUser,
                singleSrcSet, destSet);
                WildcardTemplate($@"RegCopy,%^RET%,{multiSrcSet1},%^RET%,{destSet},WILDCARD", RegistryHive.CurrentUser,
                    RegCopyPath, ["Set10", "Set20", "Set31"],
                    destSet, ["Set10", "Set20", "Set31"]);
                WildcardTemplate($@"RegCopy,%^RET%,{multiSrcSet2},%^RET%,{destSet},WILDCARD", RegistryHive.CurrentUser,
                    RegCopyPath, ["Set10", "Set20", "Set31"],
                    destSet, ["Set10", "Set20"]);
            }
            finally
            {
                s.ReturnValue = string.Empty;
            }

            // Error
            SingleTemplate($@"RegCopy,HKCU,{singleSrcSet},HKCU,{destSet},WILDCARD", RegistryHive.CurrentUser,
                singleSrcSet, destSet, ErrorCheck.RuntimeError);
            WildcardTemplate($@"RegCopy,HKCU,{multiSrcSet1},HKCU,{destSet}", RegistryHive.CurrentUser,
                RegCopyPath, ["Set10", "Set20", "Set31"],
                destSet, ["Set10", "Set20", "Set31"],
                ErrorCheck.RuntimeError);
            WildcardTemplate($@"RegCopy,HKCU,{multiSrcSet2},HKCU,{destSet}", RegistryHive.CurrentUser,
                RegCopyPath, ["Set10", "Set20", "Set31"],
                destSet, ["Set10", "Set20"],
                ErrorCheck.RuntimeError);

            #region CreateRegValues, CheckRegValues
            void CreateRegValues(RegistryHive hive, string subKeyPath)
            {
                using RegistryKey rootKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                using RegistryKey key = rootKey.CreateSubKey(subKeyPath, true);

                Assert.IsNotNull(key);

                key.SetValue("None", Array.Empty<byte>(), RegistryValueKind.None);
                key.SetValue("Binary", new byte[] { 0x01, 0x02, 0x03 }, RegistryValueKind.Binary);
                key.SetValue("Integer", 1225, RegistryValueKind.DWord);
                key.SetValue("String", "English", RegistryValueKind.String);

                // .Net Framework's RegistryKey.SetValue do not allow arbitrary type, so call Win32 API directly.
                RegistryHelper.RegSetValue(rootKey, subKeyPath, "Strange10", Array.Empty<byte>(), 0x100000);
                RegistryHelper.RegSetValue(rootKey, subKeyPath, "Strange20", new byte[] { 0x01, 0x02, 0x03 }, 0x200000);

                using (RegistryKey subKey = key.CreateSubKey("SubKey", true))
                {
                    Assert.IsNotNull(subKey);

                    subKey.SetValue("Unicode", "한국어", RegistryValueKind.ExpandString);
                    subKey.SetValue("WinDir", "%WinDir%", RegistryValueKind.ExpandString);
                }
            }

            void CheckRegValues(RegistryHive hive, string subKeyPath)
            {
                using RegistryKey rootKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                using RegistryKey? key = rootKey.OpenSubKey(subKeyPath, false);

                Assert.IsNotNull(key);

                byte[]? bin = key.GetValue("None") as byte[];
                Assert.IsNotNull(bin);
                Assert.AreEqual(0, bin.Length);

                bin = key.GetValue("Binary") as byte[];
                Assert.IsNotNull(bin);
                Assert.IsTrue(bin.SequenceEqual(new byte[] { 0x01, 0x02, 0x03 }));

                object? intObj = key.GetValue("Integer");
                Assert.IsNotNull(intObj);
                int dword = (int)intObj;
                Assert.AreEqual(1225, dword);

                object? strObj = key.GetValue("String") as string;
                Assert.IsNotNull(strObj);
                string str = (string)strObj;
                Assert.IsNotNull(str);
                Assert.IsTrue(str.Equals("English", StringComparison.Ordinal));

                // .NET's RegistryKey.GetValue cannot handle arbitrary type. Call Win32 API directly.
                bin = RegistryHelper.RegGetValue(rootKey, subKeyPath, "Strange10", RegistryValueKind.Unknown) as byte[];
                Assert.IsNotNull(bin);
                Assert.AreEqual(0, bin.Length);

                // .NET's RegistryKey.GetValue cannot handle arbitrary type. Call Win32 API directly.
                bin = RegistryHelper.RegGetValue(rootKey, subKeyPath, "Strange20", RegistryValueKind.Unknown) as byte[];
                Assert.IsNotNull(bin);
                Assert.IsTrue(bin.SequenceEqual(new byte[] { 0x01, 0x02, 0x03 }));

                using (RegistryKey? subKey = key.OpenSubKey("SubKey", false))
                {
                    Assert.IsNotNull(subKey);

                    strObj = subKey.GetValue("Unicode");
                    Assert.IsNotNull(strObj);
                    str = (string)strObj;
                    Assert.IsTrue(str.Equals("한국어", StringComparison.Ordinal));

                    strObj = subKey.GetValue("WinDir", null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                    Assert.IsNotNull(strObj);
                    str = (string)strObj;
                    Assert.IsTrue(str.Equals("%WinDir%", StringComparison.Ordinal));
                }
            }
            #endregion

            #region Template
            void SingleTemplate(string rawCode, RegistryHive hive,
                string srcKeyPath,
                string destKeyPath,
                ErrorCheck check = ErrorCheck.Success)
            { // RegCopy,<SrcKey>,<SrcKeyPath>,<DestKey>,<DestKeyPath>,[WILDCARD]
                using (RegistryKey rootKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
                {
                    rootKey.DeleteSubKeyTree(RegCopyPath, false);
                }

                try
                {
                    CreateRegValues(hive, srcKeyPath);

                    EngineTests.Eval(s, rawCode, CodeType.RegCopy, check);

                    if (check == ErrorCheck.Success || check == ErrorCheck.Warning)
                    {
                        CheckRegValues(hive, destKeyPath);
                    }
                }
                finally
                {
                    using RegistryKey rootKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);

                    rootKey.DeleteSubKeyTree(RegCopyPath, false);
                }
            }

            void WildcardTemplate(string rawCode, RegistryHive hive,
                string srcKeyPath, string[] srcTargets,
                string destKeyPath, string[] destTargets,
                ErrorCheck check = ErrorCheck.Success)
            { // RegCopy,<SrcKey>,<SrcKeyPath>,<DestKey>,<DestKeyPath>,[WILDCARD]
                Registry.CurrentUser.DeleteSubKeyTree(RegCopyPath, false);

                try
                {
                    foreach (string target in srcTargets)
                    {
                        string t = Path.Combine(srcKeyPath, target);
                        CreateRegValues(hive, t);
                    }

                    EngineTests.Eval(s, rawCode, CodeType.RegCopy, check);

                    if (check == ErrorCheck.Success || check == ErrorCheck.Warning)
                    {
                        foreach (string target in destTargets)
                        {
                            string t = Path.Combine(destKeyPath, target);
                            CheckRegValues(hive, t);
                        }
                    }
                }
                finally
                {
                    Registry.CurrentUser.DeleteSubKeyTree(RegCopyPath, false);
                }
            }
            #endregion
        }
        #endregion

        #region Utility
        private static void ReadTemplate(EngineState s, CodeType type, string rawCode, string compStr, ErrorCheck check = ErrorCheck.Success)
        {
            EngineTests.Eval(s, rawCode, type, check);

            if (check == ErrorCheck.Success)
            {
                Assert.IsTrue(s.Variables.ContainsKey("Dest"));
                Assert.IsTrue(s.Variables["Dest"].Equals(compStr, StringComparison.Ordinal));
            }
        }

        private static void ReadWriteTemplate(EngineState s, CodeType type, string rawCode, string compStr,
            RegistryHive hive, uint compKindInt, string keyPath, string valueName, object valueData,
            ErrorCheck check = ErrorCheck.Success)
        {
            using RegistryKey rootKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);

            RegistryHelper.RegSetValue(rootKey, keyPath, valueName, valueData, compKindInt);
            ReadTemplate(s, type, rawCode, compStr, check);
        }

        private static void WriteVarSuccessTemplate(EngineState s, CodeType codeType, string rawCode, string varVal,
                RegistryHive hive, RegistryValueKind compKind, string keyPath, string valueName, object? expect,
                CompatOption? opts = null, ErrorCheck check = ErrorCheck.Success)
        {
            string valBak = s.ReturnValue;
            try
            {
                s.ReturnValue = varVal;
                WriteSuccessTemplate(s, codeType, rawCode, hive, compKind, keyPath, valueName, expect, opts, check);
            }
            finally
            {
                s.ReturnValue = valBak;
            }
        }

        private static void WriteSuccessTemplate(EngineState s, CodeType codeType, string rawCode,
                RegistryHive hive, RegistryValueKind compKind, string keyPath, string valueName, object? expect,
                CompatOption? opts = null, ErrorCheck check = ErrorCheck.Success)
        {
            if (opts == null)
                EngineTests.Eval(s, rawCode, codeType, check);
            else
                EngineTests.Eval(s, rawCode, codeType, check, opts);

            if (!(check == ErrorCheck.Success || check == ErrorCheck.Warning || check == ErrorCheck.Overwrite))
                return;

            using RegistryKey rootKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using RegistryKey? subKey = rootKey.OpenSubKey(keyPath, false);

            Assert.IsNotNull(subKey);

            RegistryValueKind kind = subKey.GetValueKind(valueName);
            Assert.IsTrue(kind == compKind);

            object? valueData;
            if (kind == RegistryValueKind.Unknown)
                valueData = RegistryHelper.RegGetValue(rootKey, keyPath, valueName, RegistryValueKind.Unknown);
            else
                valueData = subKey.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            Assert.IsNotNull(valueData);

            switch (kind)
            {
                case RegistryValueKind.Unknown:
                    { // RegWriteEx
                        Assert.IsNotNull(expect);
                        byte[] destBin = (byte[])valueData;
                        byte[] compBin = (byte[])expect;
                        Assert.IsTrue(destBin.SequenceEqual(compBin));
                    }
                    break;
                case RegistryValueKind.None:
                    break;
                case RegistryValueKind.String:
                case RegistryValueKind.ExpandString:
                    {
                        Assert.IsNotNull(expect);
                        string destStr = (string)valueData;
                        string compStr = (string)expect;
                        Assert.IsTrue(destStr.Equals(compStr, StringComparison.Ordinal));
                    }
                    break;
                case RegistryValueKind.MultiString:
                    {
                        Assert.IsNotNull(expect);
                        string[] destStrs = (string[])valueData;
                        string[] compStrs = (string[])expect;

                        Assert.IsTrue(destStrs.Length == compStrs.Length);
                        for (int i = 0; i < destStrs.Length; i++)
                            Assert.IsTrue(destStrs[i].Equals(compStrs[i], StringComparison.Ordinal));
                    }
                    break;
                case RegistryValueKind.Binary:
                    {
                        Assert.IsNotNull(expect);
                        byte[] destBin = (byte[])valueData;
                        byte[] compBin = (byte[])expect;
                        Assert.IsTrue(destBin.SequenceEqual(compBin));
                    }
                    break;
                case RegistryValueKind.DWord:
                    {
                        Assert.IsNotNull(expect);
                        uint destInt = (uint)(int)valueData;
                        uint compInt = (uint)expect;
                        Assert.AreEqual(compInt, destInt);
                    }
                    break;
                case RegistryValueKind.QWord:
                    {
                        Assert.IsNotNull(expect);
                        ulong destInt = (ulong)(long)valueData;
                        ulong compInt = (ulong)expect;
                        Assert.AreEqual(compInt, destInt);
                    }
                    break;
                default:
                    Assert.Fail();
                    break;
            }
        }
        private static void WriteErrorTemplate(EngineState s, CodeType codeType, string rawCode, ErrorCheck check)
        {
            EngineTests.Eval(s, rawCode, codeType, check);
        }
        #endregion
    }
}
