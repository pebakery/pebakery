﻿/*
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;


namespace PEBakery.Core.Tests.Command
{
    [TestClass]
    [SuppressMessage("ReSharper", "ParameterOnlyUsedForPreconditionCheck.Local")]
    public class CommandRegistryTests
    {
        #region Const String
        private const string RootPath = @"Software\PEBakery\Tests";
        private const string RegWritePath = @"Software\PEBakery\Tests\RegWrite";
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
        { // Note: Only subKey is not going to be changed in later release of Windows should be used!
            EngineState s = EngineTests.CreateEngineState();

            // REG_SZ
            ReadTemplate(s, CodeType.RegRead, @"RegRead,HKLM,SOFTWARE\Microsoft\DirectX,Version,%Dest%", "4.09.00.0904");
            // REG_BINARY
            ReadTemplate(s, CodeType.RegRead, @"RegRead,HKLM,SOFTWARE\Microsoft\DirectX,InstalledVersion,%Dest%", "00,00,00,09,00,00,00,00");
            // REG_MULTI_SZ -> Will be expanded automatically by Windows
            ReadTemplate(s, CodeType.RegRead, @"RegRead,HKLM,SOFTWARE\Microsoft\DirectMusic,GMFilePath,%Dest%", @"#$pSystemRoot#$p\system32\drivers\GM.DLS");
            // REG_DWORD
            ReadTemplate(s, CodeType.RegRead, @"RegRead,HKLM,""SOFTWARE\Microsoft\Internet Explorer"",IntegratedBrowser,%Dest%", "1");
            // Error
            ReadTemplate(s, CodeType.RegRead, @"RegRead,HKLM,SOFTWARE\Microsoft\DirectX,NotExistValue,%Dest%", string.Empty, ErrorCheck.Error);
        }
        #endregion

        #region RegWrite
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandRegistry")]
        public void RegWrite()
        {
            EngineState s = EngineTests.CreateEngineState();

            void SuccessTemplate(string rawCode,
                RegistryKey hKey, RegistryValueKind compKind, string keyPath, string valueName, object comp,
                ErrorCheck check = ErrorCheck.Success)
            {
                EngineTests.Eval(s, rawCode, CodeType.RegWrite, check);

                if (check == ErrorCheck.Success || check == ErrorCheck.Warning || check == ErrorCheck.Overwrite)
                {
                    using (RegistryKey subKey = hKey.OpenSubKey(keyPath, false))
                    {
                        Assert.IsNotNull(subKey);

                        RegistryValueKind kind = subKey.GetValueKind(valueName);
                        Assert.IsTrue(kind == compKind);

                        object valueData = subKey.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                        Assert.IsNotNull(valueData);

                        switch (kind)
                        {
                            case RegistryValueKind.None:
                                break;
                            case RegistryValueKind.String:
                            case RegistryValueKind.ExpandString:
                                {
                                    string destStr = (string)valueData;
                                    string compStr = (string)comp;
                                    Assert.IsTrue(destStr.Equals(compStr, StringComparison.Ordinal));
                                }
                                break;
                            case RegistryValueKind.MultiString:
                                {
                                    string[] destStrs = (string[])valueData;
                                    string[] compStrs = (string[])comp;

                                    Assert.IsTrue(destStrs.Length == compStrs.Length);
                                    for (int i = 0; i < destStrs.Length; i++)
                                        Assert.IsTrue(destStrs[i].Equals(compStrs[i], StringComparison.Ordinal));
                                }
                                break;
                            case RegistryValueKind.Binary:
                                {
                                    byte[] destBin = (byte[])valueData;
                                    byte[] compBin = (byte[])comp;
                                    Assert.IsTrue(destBin.SequenceEqual(compBin));
                                }
                                break;
                            case RegistryValueKind.DWord:
                                {
                                    uint destInt = (uint)(int)valueData;
                                    uint compInt = (uint)comp;
                                    Assert.IsTrue(destInt == compInt);
                                }
                                break;
                            case RegistryValueKind.QWord:
                                {
                                    ulong destInt = (ulong)(long)valueData;
                                    ulong compInt = (ulong)comp;
                                    Assert.IsTrue(destInt == compInt);
                                }
                                break;
                            default:
                                Assert.Fail();
                                break;
                        }
                    }
                }
            }
            void ErrorTemplate(string rawCode, ErrorCheck check)
            {
                EngineTests.Eval(s, rawCode, CodeType.RegWrite, check);
            }

            string subKeyStr = RegWritePath;
            Registry.CurrentUser.DeleteSubKeyTree(subKeyStr, false);

            SuccessTemplate($@"RegWrite,HKCU,0x0,{subKeyStr},None",
                Registry.CurrentUser, RegistryValueKind.None, subKeyStr, "None", null);
            SuccessTemplate($@"RegWrite,HKCU,0x1,{subKeyStr},String,SZ",
                Registry.CurrentUser, RegistryValueKind.String, subKeyStr, "String", "SZ");
            SuccessTemplate($@"RegWrite,HKCU,0x2,{subKeyStr},ExpandString,#$pSystemRoot#$p\System32\notepad.exe",
                Registry.CurrentUser, RegistryValueKind.ExpandString, subKeyStr, "ExpandString", @"%SystemRoot%\System32\notepad.exe");
            SuccessTemplate($@"RegWrite,HKCU,0x7,{subKeyStr},MultiString,1,2,3",
                Registry.CurrentUser, RegistryValueKind.MultiString, subKeyStr, "MultiString", new string[] { "1", "2", "3" });
            SuccessTemplate($@"RegWrite,HKCU,0x3,{subKeyStr},Binary,00,01,02",
                Registry.CurrentUser, RegistryValueKind.Binary, subKeyStr, "Binary", new byte[] { 00, 01, 02 });
            SuccessTemplate($@"RegWrite,HKCU,0x3,{subKeyStr},Binary,""03,04""",
                Registry.CurrentUser, RegistryValueKind.Binary, subKeyStr, "Binary", new byte[] { 03, 04 },
                ErrorCheck.Overwrite);
            SuccessTemplate($@"RegWrite,HKCU,0x3,{subKeyStr},Binary,05,06,07,NOWARN",
                Registry.CurrentUser, RegistryValueKind.Binary, subKeyStr, "Binary", new byte[] { 05, 06, 07 });
            SuccessTemplate($@"RegWrite,HKCU,0x3,{subKeyStr},Binary,""08,09"",NOWARN",
                Registry.CurrentUser, RegistryValueKind.Binary, subKeyStr, "Binary", new byte[] { 08, 09 });
            SuccessTemplate($@"RegWrite,HKCU,0x4,{subKeyStr},DWORD,1234",
                Registry.CurrentUser, RegistryValueKind.DWord, subKeyStr, "DWORD", 1234u);
            SuccessTemplate($@"RegWrite,HKCU,0x4,{subKeyStr},DWORD,-1",
                Registry.CurrentUser, RegistryValueKind.DWord, subKeyStr, "DWORD", 4294967295u,
                ErrorCheck.Overwrite);
            SuccessTemplate($@"RegWrite,HKCU,0x4,{subKeyStr},DWORD,4294967295",
                Registry.CurrentUser, RegistryValueKind.DWord, subKeyStr, "DWORD", 4294967295u,
                ErrorCheck.Overwrite);
            SuccessTemplate($@"RegWrite,HKCU,0xB,{subKeyStr},QWORD,4294967296",
                Registry.CurrentUser, RegistryValueKind.QWord, subKeyStr, "QWORD", (ulong)4294967296);
            ErrorTemplate($@"RegWrite,HKCU,0x4,{subKeyStr}", ErrorCheck.ParserError);

            Registry.CurrentUser.DeleteSubKeyTree(subKeyStr, false);
        }
        #endregion

        #region RegDelete
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandRegistry")]
        public void RegDelete()
        {
            EngineState s = EngineTests.CreateEngineState();

            Registry.CurrentUser.DeleteSubKeyTree(RegDeletePath, false);
            try
            {
                void Template(string rawCode, RegistryKey hKey, string keyPath, string valueName, bool createDummy = true, ErrorCheck check = ErrorCheck.Success)
                { // RegDelete,<HKey>,<KeyPath>,[ValueName]
                    if (createDummy)
                    {
                        using (RegistryKey subKey = hKey.CreateSubKey(keyPath, true))
                        {
                            Assert.IsNotNull(subKey);

                            subKey.SetValue(valueName, 0, RegistryValueKind.DWord);
                        }
                    }

                    EngineTests.Eval(s, rawCode, CodeType.RegDelete, check);

                    if (check == ErrorCheck.Success || check == ErrorCheck.Warning)
                    {
                        if (valueName == null)
                        {
                            using (RegistryKey subKey = hKey.OpenSubKey(keyPath, false))
                            {
                                Assert.IsNull(subKey);
                            }
                        }
                        else
                        {
                            using (RegistryKey subKey = hKey.OpenSubKey(keyPath, false))
                            {
                                if (createDummy)
                                {
                                    Assert.IsNotNull(subKey);

                                    object valueData = subKey.GetValue(valueName);
                                    Assert.IsNull(valueData);
                                }
                                else
                                {
                                    Assert.IsNull(subKey);
                                }
                            }
                        }
                    }
                }

                // Success
                Template($@"RegDelete,HKCU,{RegDeletePath},ValueName", Registry.CurrentUser, RegDeletePath, "ValueName");
                Template($@"RegDelete,HKCU,{RegDeletePath}", Registry.CurrentUser, RegDeletePath, null);

                // Warning
                Template($@"RegDelete,HKCU,{RegDeletePath},ValueName", Registry.CurrentUser, RegDeletePath, "ValueName", false, ErrorCheck.Warning);
                Template($@"RegDelete,HKCU,{RegDeletePath}", Registry.CurrentUser, RegDeletePath, null, false, ErrorCheck.Warning);
            }
            finally
            {
                Registry.CurrentUser.DeleteSubKeyTree(RegDeletePath, false);
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
                    using (RegistryKey subKey = Registry.CurrentUser.OpenSubKey(RegMultiPath, false))
                    {
                        Assert.IsNotNull(subKey);

                        RegistryValueKind kind = subKey.GetValueKind("Key");
                        Assert.IsTrue(kind == RegistryValueKind.MultiString);

                        object valueData = subKey.GetValue("Key", null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                        Assert.IsNotNull(valueData);

                        string[] destStrs = (string[])valueData;
                        Assert.IsTrue(destStrs.Length == compStrs.Length);
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
                    using (RegistryKey subKey = Registry.CurrentUser.OpenSubKey(RegMultiPath, false))
                    {
                        Assert.IsNotNull(subKey);

                        RegistryValueKind kind = subKey.GetValueKind("Key");
                        Assert.IsTrue(kind == RegistryValueKind.MultiString);

                        object valueData = subKey.GetValue("Key", null, RegistryValueOptions.DoNotExpandEnvironmentNames);
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
            }
            void ErrorTemplate(string rawCode, ErrorCheck check)
            {
                EngineTests.Eval(s, rawCode, CodeType.RegWrite, check);
            }

            string subKeyStr = RegMultiPath;
            Registry.CurrentUser.DeleteSubKeyTree(subKeyStr, false);
            try
            {
                // Append
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Append,C", new string[] { "A", "B", "C" });
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Append,B", new string[] { "A", "B" }, ErrorCheck.Warning);
                // Prepend
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Prepend,C", new string[] { "C", "A", "B" });
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Prepend,A", new string[] { "A", "B" }, ErrorCheck.Warning);
                // Before
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Before,A,C", new string[] { "C", "A", "B" });
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Before,B,C", new string[] { "A", "C", "B" });
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Before,D,C", new string[] { "A", "B" }, ErrorCheck.Error);
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Before,A,B", new string[] { "A", "B" }, ErrorCheck.Warning);
                // Behind
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Behind,A,C", new string[] { "A", "C", "B" });
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Behind,B,C", new string[] { "A", "B", "C" });
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Behind,D,C", new string[] { "A", "B" }, ErrorCheck.Error);
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Behind,A,B", new string[] { "A", "B" }, ErrorCheck.Warning);
                // Place
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Place,1,C", new string[] { "C", "A", "B" });
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Place,2,C", new string[] { "A", "C", "B" });
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Place,3,C", new string[] { "A", "B", "C" });
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Place,0,C", new string[] { "C", "A", "B" }, ErrorCheck.Error);
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Place,4,C", new string[] { "C", "A", "B" }, ErrorCheck.Error);
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Place,1,B", new string[] { "A", "B" }, ErrorCheck.Warning);
                // Delete
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Delete,A", new string[] { "B" });
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Delete,B", new string[] { "A" });
                NormalTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Delete,C", new string[] { "A", "B" }, ErrorCheck.Error);
                // Index
                IndexTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Index,A,%Dest%", 1, "A");
                IndexTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Index,B,%Dest%", 2, "B");
                IndexTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Index,C,%Dest%", 0, "C");
                IndexTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Index,A", 1, "A", ErrorCheck.Error);
                // Error
                ErrorTemplate($@"RegMulti,HKCU,{subKeyStr},Key,Place,1,C,E", ErrorCheck.ParserError);
            }
            finally
            {
                Registry.CurrentUser.DeleteSubKeyTree(subKeyStr, false);
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
            Registry.CurrentUser.DeleteSubKeyTree(subKeyStr, false);
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

                using (RegistryKey subKey = Registry.CurrentUser.CreateSubKey(subKeyStr, false))
                {
                    Assert.AreEqual(RegistryValueKind.String, subKey.GetValueKind("String"));
                    string stringValue = subKey.GetValue("String", null) as string;
                    Assert.IsNotNull(stringValue);
                    Assert.IsTrue(stringValue.Equals("Str"));

                    Assert.AreEqual(RegistryValueKind.ExpandString, subKey.GetValueKind("ExpandString"));
                    stringValue = subKey.GetValue("ExpandString", null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                    Assert.IsNotNull(stringValue);
                    Assert.IsTrue(stringValue.Equals("%WinDir%"));

                    Assert.AreEqual(RegistryValueKind.Binary, subKey.GetValueKind("Binary"));
                    byte[] byteValue = subKey.GetValue("Binary", null) as byte[];
                    Assert.IsNotNull(byteValue);
                    Assert.IsTrue(byteValue.SequenceEqual(new byte[] { 0xA0, 0xA1, 0xA2 }));

                    Assert.AreEqual(RegistryValueKind.DWord, subKey.GetValueKind("DWORD"));
                    Assert.IsNotNull(subKey.GetValue("DWORD", null));
                    uint dword = (uint)(int)subKey.GetValue("DWORD", null);
                    Assert.AreEqual(0x03u, dword);

                    Assert.AreEqual(RegistryValueKind.QWord, subKey.GetValueKind("QWORD"));
                    Assert.IsNotNull(subKey.GetValue("QWORD", null));
                    ulong qword = (ulong)(long)subKey.GetValue("QWORD", null);
                    Assert.AreEqual(0x100000000u, qword);
                }
            }
            finally
            {
                Registry.CurrentUser.DeleteSubKeyTree(subKeyStr, false);
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
            Registry.CurrentUser.DeleteSubKeyTree(subKeyStr, false);
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

                using (RegistryKey subKey = Registry.CurrentUser.CreateSubKey(subKeyStr, true))
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
                Registry.CurrentUser.DeleteSubKeyTree(subKeyStr, false);
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

            // Success
            Single_Template($@"RegCopy,HKCU,{singleSrcSet},HKCU,{destSet}", Registry.CurrentUser,
                singleSrcSet, destSet);
            Wildcard_Template($@"RegCopy,HKCU,{multiSrcSet1},HKCU,{destSet},WILDCARD", Registry.CurrentUser,
                RegCopyPath, new string[] { "Set10", "Set20", "Set31" },
                destSet, new string[] { "Set10", "Set20", "Set31" });
            Wildcard_Template($@"RegCopy,HKCU,{multiSrcSet2},HKCU,{destSet},WILDCARD", Registry.CurrentUser,
                RegCopyPath, new string[] { "Set10", "Set20", "Set31" },
                destSet, new string[] { "Set10", "Set20" });

            // Error
            Single_Template($@"RegCopy,HKCU,{singleSrcSet},HKCU,{destSet},WILDCARD", Registry.CurrentUser,
                singleSrcSet, destSet, ErrorCheck.Error);
            Wildcard_Template($@"RegCopy,HKCU,{multiSrcSet1},HKCU,{destSet}", Registry.CurrentUser,
                RegCopyPath, new string[] { "Set10", "Set20", "Set31" },
                destSet, new string[] { "Set10", "Set20", "Set31" },
                ErrorCheck.Error);
            Wildcard_Template($@"RegCopy,HKCU,{multiSrcSet2},HKCU,{destSet}", Registry.CurrentUser,
                RegCopyPath, new string[] { "Set10", "Set20", "Set31" },
                destSet, new string[] { "Set10", "Set20" },
                ErrorCheck.Error);

            #region CreateRegValues, CheckRegValues
            void CreateRegValues(RegistryKey hKey, string subKeyPath)
            {
                using (RegistryKey key = hKey.CreateSubKey(subKeyPath, true))
                {
                    Assert.IsNotNull(key);

                    key.SetValue("Binary", new byte[] { 0x01, 0x02, 0x03 }, RegistryValueKind.Binary);
                    key.SetValue("Integer", 1225, RegistryValueKind.DWord);
                    key.SetValue("String", "English", RegistryValueKind.String);

                    using (RegistryKey subKey = key.CreateSubKey("SubKey", true))
                    {
                        Assert.IsNotNull(subKey);

                        subKey.SetValue("Unicode", "한국어", RegistryValueKind.ExpandString);
                        subKey.SetValue("WinDir", "%WinDir%", RegistryValueKind.ExpandString);
                    }
                }
            }

            void CheckRegValues(RegistryKey hKey, string subKeyPath)
            {
                using (RegistryKey key = hKey.OpenSubKey(subKeyPath, false))
                {
                    Assert.IsNotNull(key);

                    byte[] bin = key.GetValue("Binary") as byte[];
                    Assert.IsNotNull(bin);
                    Assert.IsTrue(bin.SequenceEqual(new byte[] { 0x01, 0x02, 0x03 }));

                    int dword = (int)key.GetValue("Integer");
                    Assert.AreEqual(dword, 1225);

                    string str = key.GetValue("String") as string;
                    Assert.IsNotNull(str);
                    Assert.IsTrue(str.Equals("English", StringComparison.Ordinal));

                    using (RegistryKey subKey = key.OpenSubKey("SubKey", false))
                    {
                        Assert.IsNotNull(subKey);

                        str = subKey.GetValue("Unicode") as string;
                        Assert.IsNotNull(str);
                        Assert.IsTrue(str.Equals("한국어", StringComparison.Ordinal));

                        str = subKey.GetValue("WinDir", null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                        Assert.IsNotNull(str);
                        Assert.IsTrue(str.Equals("%WinDir%", StringComparison.Ordinal));
                    }
                }
            }
            #endregion

            #region Template
            void Single_Template(string rawCode, RegistryKey hKey,
                string srcKeyPath,
                string destKeyPath,
                ErrorCheck check = ErrorCheck.Success)
            { // RegCopy,<SrcKey>,<SrcKeyPath>,<DestKey>,<DestKeyPath>,[WILDCARD]
                Registry.CurrentUser.DeleteSubKeyTree(RegCopyPath, false);
                try
                {
                    CreateRegValues(hKey, srcKeyPath);

                    EngineTests.Eval(s, rawCode, CodeType.RegCopy, check);

                    if (check == ErrorCheck.Success || check == ErrorCheck.Warning)
                    {
                        CheckRegValues(hKey, destKeyPath);
                    }
                }
                finally
                {
                    Registry.CurrentUser.DeleteSubKeyTree(RegCopyPath, false);
                }
            }

            void Wildcard_Template(string rawCode, RegistryKey hKey,
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
                        CreateRegValues(hKey, t);
                    }

                    EngineTests.Eval(s, rawCode, CodeType.RegCopy, check);

                    if (check == ErrorCheck.Success || check == ErrorCheck.Warning)
                    {
                        foreach (string target in destTargets)
                        {
                            string t = Path.Combine(destKeyPath, target);
                            CheckRegValues(hKey, t);
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
        private static void ReadTemplate(EngineState s, CodeType type, string rawCode, string comp, ErrorCheck check = ErrorCheck.Success)
        {
            EngineTests.Eval(s, rawCode, type, check);

            if (check == ErrorCheck.Success)
            {
                Assert.IsTrue(s.Variables["Dest"].Equals(comp, StringComparison.Ordinal));
            }
        }
        #endregion
    }
}