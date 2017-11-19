using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;
using PEBakery.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PEBakery.Tests.Core.Command
{
    [TestClass]
    public class CommandRegistryTests
    {
        #region Const String
        private const string Dest_Root = @"Software\PEBakery";
        private const string Dest_RegWrite = @"Software\PEBakery\RegWrite";
        private const string Dest_RegDelete = @"Software\PEBakery\RegDelete";
        private const string Dest_RegMulti = @"Software\PEBakery\RegMulti";
        #endregion

        #region ClassCleanup
        [ClassCleanup]
        public static void ClassCleanup()
        {
            Registry.CurrentUser.DeleteSubKeyTree(Dest_Root, false);
        }
        #endregion

        #region RegRead
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandRegistry")]
        public void Reg_RegRead()
        { // RegRead,<HKey>,<KeyPath>,<ValueName>,<DestVar>
            EngineState s = EngineTests.CreateEngineState();

            // Use subKey which will not be changed

            // REG_SZ
            RegDestVar_Template(s, CodeType.RegRead, @"RegRead,HKLM,SOFTWARE\Microsoft\DirectX,Version,%Dest%", "4.09.00.0904");

            // REG_BINARY
            RegDestVar_Template(s, CodeType.RegRead, @"RegRead,HKLM,SOFTWARE\Microsoft\DirectX,InstalledVersion,%Dest%", "00,00,00,09,00,00,00,00");

            // REG_MULTI_SZ -> Will be expanded automatically by Windows
            RegDestVar_Template(s, CodeType.RegRead, @"RegRead,HKLM,SOFTWARE\Microsoft\DirectMusic,GMFilePath,%Dest%", @"#$pSystemRoot#$p\system32\drivers\GM.DLS");

            // REG_DWORD
            RegDestVar_Template(s, CodeType.RegRead, @"RegRead,HKLM,""SOFTWARE\Microsoft\Internet Explorer"",IntegratedBrowser,%Dest%", "1");

            // Error
            RegDestVar_Template(s, CodeType.RegRead, @"RegRead,HKLM,SOFTWARE\Microsoft\DirectX,NotExistValue,%Dest%", string.Empty, ErrorCheck.Error);
        }

        #endregion

        #region RegWrite
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandRegistry")]
        public void Reg_RegWrite()
        { // RegWrite,<HKey>,<ValueType>,<KeyPath>,<ValueName>,<ValueData | ValueDatas>,[NOWARN]
            EngineState s = EngineTests.CreateEngineState();

            string subKey = Dest_RegWrite;
            Registry.CurrentUser.DeleteSubKeyTree(subKey, false);

            RegWrite_Template(s, $@"RegWrite,HKCU,0x0,{subKey},None",
                Registry.CurrentUser, RegistryValueKind.None, subKey, "None", null);
            RegWrite_Template(s, $@"RegWrite,HKCU,0x1,{subKey},String,SZ",
                Registry.CurrentUser, RegistryValueKind.String, subKey, "String", "SZ");
            RegWrite_Template(s, $@"RegWrite,HKCU,0x2,{subKey},ExpandString,#$pSystemRoot#$p\System32\notepad.exe",
                Registry.CurrentUser, RegistryValueKind.ExpandString, subKey, "ExpandString", @"%SystemRoot%\System32\notepad.exe");
            RegWrite_Template(s, $@"RegWrite,HKCU,0x7,{subKey},MultiString,1,2,3",
                Registry.CurrentUser, RegistryValueKind.MultiString, subKey, "MultiString", new string[] { "1", "2", "3" });
            RegWrite_Template(s, $@"RegWrite,HKCU,0x3,{subKey},Binary,00,01,02",
                Registry.CurrentUser, RegistryValueKind.Binary, subKey, "Binary", new byte[] { 00, 01, 02 });
            RegWrite_Template(s, $@"RegWrite,HKCU,0x3,{subKey},Binary,""03,04""",
                Registry.CurrentUser, RegistryValueKind.Binary, subKey, "Binary", new byte[] { 03, 04 },
                ErrorCheck.Warning);
            RegWrite_Template(s, $@"RegWrite,HKCU,0x3,{subKey},Binary,05,06,07,NOWARN",
                Registry.CurrentUser, RegistryValueKind.Binary, subKey, "Binary", new byte[] { 05, 06, 07 });
            RegWrite_Template(s, $@"RegWrite,HKCU,0x3,{subKey},Binary,""08,09"",NOWARN",
                Registry.CurrentUser, RegistryValueKind.Binary, subKey, "Binary", new byte[] { 08, 09 });
            RegWrite_Template(s, $@"RegWrite,HKCU,0x4,{subKey},DWORD,1234",
                Registry.CurrentUser, RegistryValueKind.DWord, subKey, "DWORD", (uint)1234);
            RegWrite_Template(s, $@"RegWrite,HKCU,0x4,{subKey},DWORD,-1",
                Registry.CurrentUser, RegistryValueKind.DWord, subKey, "DWORD", (uint)4294967295,
                ErrorCheck.Warning);
            RegWrite_Template(s, $@"RegWrite,HKCU,0x4,{subKey},DWORD,4294967295",
                Registry.CurrentUser, RegistryValueKind.DWord, subKey, "DWORD", (uint)4294967295,
                ErrorCheck.Warning);
            RegWrite_Template(s, $@"RegWrite,HKCU,0xB,{subKey},QWORD,4294967296",
                Registry.CurrentUser, RegistryValueKind.QWord, subKey, "QWORD", (ulong)4294967296);

            RegWrite_Template_Error(s, $@"RegWrite,HKCU,0x4,{subKey}", ErrorCheck.ParserError);

            Registry.CurrentUser.DeleteSubKeyTree(subKey, false);
        }

        private void RegWrite_Template(EngineState s, string rawCode,
            RegistryKey hKey, RegistryValueKind compKind, string keyPath, string valueName, object comp,
            ErrorCheck check = ErrorCheck.Success)
        {
            EngineTests.Eval(s, rawCode, CodeType.RegWrite, check);

            if (check == ErrorCheck.Success || check == ErrorCheck.Warning)
            {
                string valueDataStr;
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
                            valueDataStr = StringEscaper.PackRegMultiString((string[])valueData);
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

        private void RegWrite_Template_Error(EngineState s, string rawCode, ErrorCheck check)
        {
            EngineTests.Eval(s, rawCode, CodeType.RegWrite, check);
        }
        #endregion

        #region RegDelete
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandRegistry")]
        public void Reg_RegDelete()
        { // RegDelete,<HKey>,<KeyPath>,[ValueName]
            EngineState s = EngineTests.CreateEngineState();

            Registry.CurrentUser.DeleteSubKeyTree(Dest_RegDelete, false);

            // Success
            RegDelete_Template(s, $@"RegDelete,HKCU,{Dest_RegDelete},ValueName", Registry.CurrentUser, Dest_RegDelete, "ValueName");
            RegDelete_Template(s, $@"RegDelete,HKCU,{Dest_RegDelete}", Registry.CurrentUser, Dest_RegDelete, null);

            // Warning
            RegDelete_Template(s, $@"RegDelete,HKCU,{Dest_RegDelete},ValueName", Registry.CurrentUser, Dest_RegDelete, "ValueName", false, ErrorCheck.Warning);
            RegDelete_Template(s, $@"RegDelete,HKCU,{Dest_RegDelete}", Registry.CurrentUser, Dest_RegDelete, null, false, ErrorCheck.Warning);

            Registry.CurrentUser.DeleteSubKeyTree(Dest_RegDelete, false);
        }

        private void RegDelete_Template(EngineState s, string rawCode, RegistryKey hKey, string keyPath, string valueName, bool createDummy = true, ErrorCheck check = ErrorCheck.Success)
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
        #endregion

        #region RegMulti
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandRegistry")]
        public void Reg_RegMulti()
        { // RegMulti,<HKey>,<KeyPath>,<ValueName>,<Action>,<Arg1>,[Arg2]
            EngineState s = EngineTests.CreateEngineState();

            string subKeyStr = Dest_RegMulti;
            Registry.CurrentUser.DeleteSubKeyTree(subKeyStr, false);

            // Append
            RegMulti_Template(s, $@"RegMulti,HKCU,{subKeyStr},Key,Append,C", new string[] { "A", "B", "C" });
            RegMulti_Template(s, $@"RegMulti,HKCU,{subKeyStr},Key,Append,B", new string[] { "A", "B" }, ErrorCheck.Warning);

            // Prepend
            RegMulti_Template(s, $@"RegMulti,HKCU,{subKeyStr},Key,Prepend,C", new string[] { "C", "A", "B" });
            RegMulti_Template(s, $@"RegMulti,HKCU,{subKeyStr},Key,Prepend,A", new string[] { "A", "B" }, ErrorCheck.Warning);

            // Before
            RegMulti_Template(s, $@"RegMulti,HKCU,{subKeyStr},Key,Before,A,C", new string[] { "C", "A", "B" });
            RegMulti_Template(s, $@"RegMulti,HKCU,{subKeyStr},Key,Before,B,C", new string[] { "A", "C", "B" });
            RegMulti_Template(s, $@"RegMulti,HKCU,{subKeyStr},Key,Before,D,C", new string[] { "A", "B" }, ErrorCheck.Error);
            RegMulti_Template(s, $@"RegMulti,HKCU,{subKeyStr},Key,Before,A,B", new string[] { "A", "B" }, ErrorCheck.Warning);

            // Behind
            RegMulti_Template(s, $@"RegMulti,HKCU,{subKeyStr},Key,Behind,A,C", new string[] { "A", "C", "B" });
            RegMulti_Template(s, $@"RegMulti,HKCU,{subKeyStr},Key,Behind,B,C", new string[] { "A", "B", "C" });
            RegMulti_Template(s, $@"RegMulti,HKCU,{subKeyStr},Key,Behind,D,C", new string[] { "A", "B" }, ErrorCheck.Error);
            RegMulti_Template(s, $@"RegMulti,HKCU,{subKeyStr},Key,Behind,A,B", new string[] { "A", "B" }, ErrorCheck.Warning);

            // Place
            RegMulti_Template(s, $@"RegMulti,HKCU,{subKeyStr},Key,Place,1,C", new string[] { "C", "A", "B" });
            RegMulti_Template(s, $@"RegMulti,HKCU,{subKeyStr},Key,Place,2,C", new string[] { "A", "C", "B" });
            RegMulti_Template(s, $@"RegMulti,HKCU,{subKeyStr},Key,Place,3,C", new string[] { "A", "B", "C" });
            RegMulti_Template(s, $@"RegMulti,HKCU,{subKeyStr},Key,Place,0,C", new string[] { "C", "A", "B" }, ErrorCheck.Error);
            RegMulti_Template(s, $@"RegMulti,HKCU,{subKeyStr},Key,Place,4,C", new string[] { "C", "A", "B" }, ErrorCheck.Error);
            RegMulti_Template(s, $@"RegMulti,HKCU,{subKeyStr},Key,Place,1,B", new string[] { "A", "B" }, ErrorCheck.Warning);

            // Delete
            RegMulti_Template(s, $@"RegMulti,HKCU,{subKeyStr},Key,Delete,A", new string[] { "B" });
            RegMulti_Template(s, $@"RegMulti,HKCU,{subKeyStr},Key,Delete,B", new string[] { "A" });
            RegMulti_Template(s, $@"RegMulti,HKCU,{subKeyStr},Key,Delete,C", new string[] { "A", "B" }, ErrorCheck.Error);

            // Index
            RegMulti_IndexTemplate(s, $@"RegMulti,HKCU,{subKeyStr},Key,Index,A,%Dest%", 1, "A");
            RegMulti_IndexTemplate(s, $@"RegMulti,HKCU,{subKeyStr},Key,Index,B,%Dest%", 2, "B");
            RegMulti_IndexTemplate(s, $@"RegMulti,HKCU,{subKeyStr},Key,Index,C,%Dest%", 0, "C");
            RegMulti_IndexTemplate(s, $@"RegMulti,HKCU,{subKeyStr},Key,Index,A", 1, "A", ErrorCheck.Error);

            // Error
            RegMulti_Template_Error(s, $@"RegMulti,HKCU,{subKeyStr},Key,Place,1,C,E", ErrorCheck.ParserError);

            Registry.CurrentUser.DeleteSubKeyTree(subKeyStr, false);
        }

        private void RegMulti_Template(EngineState s, string rawCode, string[] compStrs, ErrorCheck check = ErrorCheck.Success)
        {
            using (RegistryKey subKey = Registry.CurrentUser.CreateSubKey(Dest_RegMulti, true))
            {
                subKey.SetValue("Key", new string[] { "A", "B" }, RegistryValueKind.MultiString);
            }

            EngineTests.Eval(s, rawCode, CodeType.RegMulti, check);

            if (check == ErrorCheck.Success || check == ErrorCheck.Warning)
            {
                using (RegistryKey subKey = Registry.CurrentUser.OpenSubKey(Dest_RegMulti, false))
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

        private void RegMulti_IndexTemplate(EngineState s, string rawCode, int compIdx, string compStr, ErrorCheck check = ErrorCheck.Success)
        {
            using (RegistryKey subKey = Registry.CurrentUser.CreateSubKey(Dest_RegMulti, true))
            {
                subKey.SetValue("Key", new string[] { "A", "B" }, RegistryValueKind.MultiString);
            }

            EngineTests.Eval(s, rawCode, CodeType.RegMulti, check);

            if (check == ErrorCheck.Success || check == ErrorCheck.Warning)
            {
                using (RegistryKey subKey = Registry.CurrentUser.OpenSubKey(Dest_RegMulti, false))
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

        private void RegMulti_Template_Error(EngineState s, string rawCode, ErrorCheck check)
        {
            EngineTests.Eval(s, rawCode, CodeType.RegWrite, check);
        }
        #endregion

        #region Utility
        private void RegDestVar_Template(EngineState s, CodeType type, string rawCode, string comp, ErrorCheck check = ErrorCheck.Success)
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
