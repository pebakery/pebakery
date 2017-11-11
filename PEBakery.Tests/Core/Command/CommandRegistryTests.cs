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
        { // RegWrite,<HKey>,<ValueType>,<KeyPath>,<ValueName>,<ValueData | ValueDatas>
            EngineState s = EngineTests.CreateEngineState();

            Registry.CurrentUser.DeleteSubKeyTree(@"Software\PEBakery", false);

            RegWrite_Template(s, @"RegWrite,HKCU,0x0,Software\PEBakery,None",
                Registry.CurrentUser, RegistryValueKind.None, @"Software\PEBakery", "None", null);
            RegWrite_Template(s, @"RegWrite,HKCU,0x1,Software\PEBakery,String,SZ",
                Registry.CurrentUser, RegistryValueKind.String, @"Software\PEBakery", "String", "SZ");
            RegWrite_Template(s, @"RegWrite,HKCU,0x2,Software\PEBakery,ExpandString,#$pSystemRoot#$p\System32\notepad.exe",
                Registry.CurrentUser, RegistryValueKind.ExpandString, @"Software\PEBakery", "ExpandString", @"%SystemRoot%\System32\notepad.exe");
            RegWrite_Template(s, @"RegWrite,HKCU,0x7,Software\PEBakery,MultiString,1,2,3",
                Registry.CurrentUser, RegistryValueKind.MultiString, @"Software\PEBakery", "MultiString", new string[] { "1", "2", "3" });
            RegWrite_Template(s, @"RegWrite,HKCU,0x3,Software\PEBakery,Binary,00,01,02",
                Registry.CurrentUser, RegistryValueKind.Binary, @"Software\PEBakery", "Binary", new byte[] { 00, 01, 02 });
            RegWrite_Template(s, @"RegWrite,HKCU,0x3,Software\PEBakery,Binary,""03,04""",
                Registry.CurrentUser, RegistryValueKind.Binary, @"Software\PEBakery", "Binary", new byte[] { 03, 04 },
                ErrorCheck.Warning);
            RegWrite_Template(s, @"RegWrite,HKCU,0x3,Software\PEBakery,Binary,05,06,07,NOWARN",
                Registry.CurrentUser, RegistryValueKind.Binary, @"Software\PEBakery", "Binary", new byte[] { 05, 06, 07 });
            RegWrite_Template(s, @"RegWrite,HKCU,0x3,Software\PEBakery,Binary,""08,09"",NOWARN",
                Registry.CurrentUser, RegistryValueKind.Binary, @"Software\PEBakery", "Binary", new byte[] { 08, 09 });
            RegWrite_Template(s, @"RegWrite,HKCU,0x4,Software\PEBakery,DWORD,1234",
                Registry.CurrentUser, RegistryValueKind.DWord, @"Software\PEBakery", "DWORD", (uint)1234);
            RegWrite_Template(s, @"RegWrite,HKCU,0x4,Software\PEBakery,DWORD,-1",
                Registry.CurrentUser, RegistryValueKind.DWord, @"Software\PEBakery", "DWORD", (uint)4294967295,
                ErrorCheck.Warning);
            RegWrite_Template(s, @"RegWrite,HKCU,0x4,Software\PEBakery,DWORD,4294967295",
                Registry.CurrentUser, RegistryValueKind.DWord, @"Software\PEBakery", "DWORD", (uint)4294967295,
                ErrorCheck.Warning);
            RegWrite_Template(s, @"RegWrite,HKCU,0xB,Software\PEBakery,QWORD,4294967296",
                Registry.CurrentUser, RegistryValueKind.QWord, @"Software\PEBakery", "QWORD", (ulong)4294967296);

            RegWrite_Template_Error(s, @"RegWrite,HKCU,0x4,Software\PEBakery", ErrorCheck.ParserError);

            Registry.CurrentUser.DeleteSubKeyTree(@"Software\PEBakery", false);
        }

        private void RegWrite_Template(EngineState s, string rawCode,
            RegistryKey hKey, RegistryValueKind compKind, string keyPath, string valueName, object comp,
            ErrorCheck check = ErrorCheck.Success)
        {
            EngineTests.Eval(s, rawCode, CodeType.RegWrite, check);

            if (check == ErrorCheck.Success || check == ErrorCheck.Success)
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
