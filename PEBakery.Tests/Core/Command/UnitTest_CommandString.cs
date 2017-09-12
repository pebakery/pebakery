using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Core;
using PEBakery.Core.Commands;
using System.Collections.Generic;
using PEBakery.Exceptions;
using System.Diagnostics;

namespace UnitTest.Core.Command
{
    [TestClass]
    public class UnitTest_CommandString
    {
        #region IntToBytes
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_IntToBytes()
        {
            StrFormat_IntToBytes_1();
            StrFormat_IntToBytes_2();
            StrFormat_IntToBytes_3();
            StrFormat_IntToBytes_4();
            StrFormat_IntToBytes_5();
            StrFormat_IntToBytes_6();
            StrFormat_IntToBytes_7();
            StrFormat_IntToBytes_8();
        }

        public void StrFormat_IntToBytes_1()
        {
            string rawCode = "StrFormat,IntToBytes,10240,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("10KB", StringComparison.Ordinal));
        }

        public void StrFormat_IntToBytes_2()
        {
            string rawCode = "StrFormat,IntToBytes,4404020,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("4.2MB", StringComparison.Ordinal));
        }

        public void StrFormat_IntToBytes_3()
        {
            string rawCode = "StrFormat,IntToBytes,5561982650,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("5.18GB", StringComparison.Ordinal));
        }

        public void StrFormat_IntToBytes_4()
        {
            string rawCode = "StrFormat,IntToBytes,2193525697413,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("1.995TB", StringComparison.Ordinal));
        }

        public void StrFormat_IntToBytes_5()
        {
            string rawCode = "StrFormat,IntToBytes,2270940112101573,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("2.017PB", StringComparison.Ordinal));
        }

        public void StrFormat_IntToBytes_6()
        {
            string rawCode = "StrFormat,IntToBytes,2229281815548396000,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("1980PB", StringComparison.Ordinal));
        }

        public void StrFormat_IntToBytes_7()
        {
            string rawCode = "StrFormat,IntToBytes,WrongInteger,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Error);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals(string.Empty, StringComparison.Ordinal));
        }

        public void StrFormat_IntToBytes_8()
        {
            string rawCode = "StrFormat,IntToBytes,%Wrong%,WrongDest";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.ParserError);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals(string.Empty, StringComparison.Ordinal));
        }
        #endregion

        #region BytesToInt
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_BytesToInt()
        {
            StrFormat_BytesToInt_1();
            StrFormat_BytesToInt_2();
            StrFormat_BytesToInt_3();
            StrFormat_BytesToInt_4();
            StrFormat_BytesToInt_5();
            StrFormat_BytesToInt_6();
            StrFormat_BytesToInt_7();
        }

        public void StrFormat_BytesToInt_1()
        {
            string rawCode = "StrFormat,BytesToInt,10KB,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("10240", StringComparison.Ordinal));
        }

        public void StrFormat_BytesToInt_2()
        {
            string rawCode = "StrFormat,BytesToInt,4.2MB,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("4404020", StringComparison.Ordinal));
        }

        public void StrFormat_BytesToInt_3()
        {
            string rawCode = "StrFormat,BytesToInt,5.18GB,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("5561982649", StringComparison.Ordinal));
        }

        public void StrFormat_BytesToInt_4()
        {
            string rawCode = "StrFormat,BytesToInt,1.995TB,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("2193525697414", StringComparison.Ordinal));
        }

        public void StrFormat_BytesToInt_5()
        {
            string rawCode = "StrFormat,BytesToInt,2.017PB,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("2270940112101573", StringComparison.Ordinal));
        }

        public void StrFormat_BytesToInt_6()
        {
            string rawCode = "StrFormat,BytesToInt,1980PB,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("2229281815548395520", StringComparison.Ordinal));
        }

        public void StrFormat_BytesToInt_7()
        {
            string rawCode = "StrFormat,BytesToInt,WrongBytes,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Error);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals(string.Empty, StringComparison.Ordinal));
        }
        #endregion

        #region Ceil
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_Ceil()
        {
            StrFormat_Ceil_1();
            StrFormat_Ceil_2();
            StrFormat_Ceil_3();
            StrFormat_Ceil_4();
            StrFormat_Ceil_5();
            StrFormat_Ceil_6();
            StrFormat_Ceil_7();
            StrFormat_Ceil_8();
        }

        public void StrFormat_Ceil_1()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "4";

            string rawCode = "StrFormat,Ceil,%Dest%,-10";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Error);
        }

        public void StrFormat_Ceil_2()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "6";

            string rawCode = "StrFormat,Ceil,%Dest%,10";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("10", StringComparison.Ordinal));
        }

        public void StrFormat_Ceil_3()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "799";

            string rawCode = "StrFormat,Ceil,%Dest%,800";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("800", StringComparison.Ordinal));
        }

        public void StrFormat_Ceil_4()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "801";

            string rawCode = "StrFormat,Ceil,%Dest%,800";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("1600", StringComparison.Ordinal));
        }

        public void StrFormat_Ceil_5()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "1000";

            string rawCode = "StrFormat,Ceil,%Dest%,K";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("1024", StringComparison.Ordinal));
        }

        public void StrFormat_Ceil_6()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "1200";

            string rawCode = "StrFormat,Ceil,%Dest%,K";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("2048", StringComparison.Ordinal));
        }

        public void StrFormat_Ceil_7()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "1048570";

            string rawCode = "StrFormat,Ceil,%Dest%,M";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("1048576", StringComparison.Ordinal));
        }

        public void StrFormat_Ceil_8()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "1048580";

            string rawCode = "StrFormat,Ceil,%Dest%,M";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("2097152", StringComparison.Ordinal));
        }
        #endregion

        #region Floor
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_Floor()
        {
            StrFormat_Floor_1();
            StrFormat_Floor_2();
            StrFormat_Floor_3();
            StrFormat_Floor_4();
            StrFormat_Floor_5();
            StrFormat_Floor_6();
            StrFormat_Floor_7();
            StrFormat_Floor_8();
        }

        public void StrFormat_Floor_1()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "4";

            string rawCode = "StrFormat,Floor,%Dest%,-10";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Error);
        }

        public void StrFormat_Floor_2()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "6";

            string rawCode = "StrFormat,Floor,%Dest%,10";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("0", StringComparison.Ordinal));
        }

        public void StrFormat_Floor_3()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "799";

            string rawCode = "StrFormat,Floor,%Dest%,800";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("0", StringComparison.Ordinal));
        }

        public void StrFormat_Floor_4()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "801";

            string rawCode = "StrFormat,Floor,%Dest%,800";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("800", StringComparison.Ordinal));
        }

        public void StrFormat_Floor_5()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "1000";

            string rawCode = "StrFormat,Floor,%Dest%,K";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("0", StringComparison.Ordinal));
        }

        public void StrFormat_Floor_6()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "1200";

            string rawCode = "StrFormat,Floor,%Dest%,K";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("1024", StringComparison.Ordinal));
        }

        public void StrFormat_Floor_7()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "1048570";

            string rawCode = "StrFormat,Floor,%Dest%,M";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("0", StringComparison.Ordinal));
        }

        public void StrFormat_Floor_8()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "1048580";

            string rawCode = "StrFormat,Floor,%Dest%,M";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("1048576", StringComparison.Ordinal));
        }
        #endregion

        #region Round
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_Round()
        {
            StrFormat_Round_1();
            StrFormat_Round_2();
            StrFormat_Round_3();
            StrFormat_Round_4();
            StrFormat_Round_5();
            StrFormat_Round_6();
            StrFormat_Round_7();
            StrFormat_Round_8();
        }

        public void StrFormat_Round_1()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "4";

            string rawCode = "StrFormat,Round,%Dest%,10";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("0", StringComparison.Ordinal));
        }

        public void StrFormat_Round_2()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "6";

            string rawCode = "StrFormat,Round,%Dest%,10";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("10", StringComparison.Ordinal));
        }

        public void StrFormat_Round_3()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "350";

            string rawCode = "StrFormat,Round,%Dest%,800";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("0", StringComparison.Ordinal));
        }

        public void StrFormat_Round_4()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "450";

            string rawCode = "StrFormat,Round,%Dest%,800";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("800", StringComparison.Ordinal));
        }

        public void StrFormat_Round_5()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "500";

            string rawCode = "StrFormat,Round,%Dest%,K";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("0", StringComparison.Ordinal));
        }

        public void StrFormat_Round_6()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "600";

            string rawCode = "StrFormat,Round,%Dest%,K";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("1024", StringComparison.Ordinal));
        }

        public void StrFormat_Round_7()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "524286";

            string rawCode = "StrFormat,Round,%Dest%,M";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("0", StringComparison.Ordinal));
        }

        public void StrFormat_Round_8()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "524290";

            string rawCode = "StrFormat,Round,%Dest%,M";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("1048576", StringComparison.Ordinal));
        }
        #endregion

        #region Date
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_Date()
        {
            // Cannot compare Date string directly because of processing delay
            // So test only converted .Net Date Strings
            StrFormat_Date_1();
            StrFormat_Date_2();
            StrFormat_Date_3();
            StrFormat_Date_4();
        }

        public void StrFormat_Date_1()
        {
            string rawCode = "StrFormat,Date,%Dest%,yyyy-mm-dd_hh:nn:ss.zzz";
            SectionAddress addr = UnitTest_Engine.DummySectionAddress();
            CodeCommand cmd = CodeParser.ParseOneRawLine(rawCode, addr);

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_StrFormat));
            CodeInfo_StrFormat info = cmd.Info as CodeInfo_StrFormat;

            Debug.Assert(info.SubInfo.GetType() == typeof(StrFormatInfo_Date));
            StrFormatInfo_Date subInfo = info.SubInfo as StrFormatInfo_Date;

            Assert.IsTrue(subInfo.FormatString.Equals("yyyy-MM-dd_HH:mm:ss.fff", StringComparison.Ordinal));
        }

        public void StrFormat_Date_2()
        {
            string rawCode = "StrFormat,DATE,#9,yyyymmddhhnnsszzz";
            SectionAddress addr = UnitTest_Engine.DummySectionAddress();
            CodeCommand cmd = CodeParser.ParseOneRawLine(rawCode, addr);

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_StrFormat));
            CodeInfo_StrFormat info = cmd.Info as CodeInfo_StrFormat;

            Debug.Assert(info.SubInfo.GetType() == typeof(StrFormatInfo_Date));
            StrFormatInfo_Date subInfo = info.SubInfo as StrFormatInfo_Date;

            Assert.IsTrue(subInfo.FormatString.Equals("yyyyMMddHHmmssfff", StringComparison.Ordinal));
        }

        public void StrFormat_Date_3()
        {
            string rawCode = "StrFormat,Date,%Dest%,yyy-mm-dd_hh:nn:ss.zzz";
            SectionAddress addr = UnitTest_Engine.DummySectionAddress();
            try
            {
                CodeCommand cmd = CodeParser.ParseOneRawLine(rawCode, addr);
            }
            catch (InvalidCodeCommandException)
            { // Successfully induced error
                return;
            }

            Assert.Fail();
        }

        public void StrFormat_Date_4()
        {
            string rawCode = "StrFormat,Date,%Dest%,yyymdd_hhnnss.zzz";
            SectionAddress addr = UnitTest_Engine.DummySectionAddress();
            try
            {
                CodeCommand cmd = CodeParser.ParseOneRawLine(rawCode, addr);
            }
            catch (InvalidCodeCommandException)
            { // Successfully induced error
                return;
            }

            Assert.Fail();
        }
        #endregion

        #region FileName
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_FileName()
        {
            StrFormat_FileName_1();
            StrFormat_FileName_2();
            StrFormat_FileName_3();
            StrFormat_FileName_4();
        }

        public void StrFormat_FileName_1()
        {
            string rawCode = @"StrFormat,FileName,C:\Windows\System32\notepad.exe,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("notepad.exe", StringComparison.Ordinal));
        }

        public void StrFormat_FileName_2()
        {
            string rawCode = @"StrFormat,FileName,C:\Windows\System32\,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals(string.Empty, StringComparison.Ordinal));
        }

        public void StrFormat_FileName_3()
        {
            string rawCode = @"StrFormat,FileName,,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals(string.Empty, StringComparison.Ordinal));
        }

        public void StrFormat_FileName_4()
        {
            string rawCode = @"StrFormat,FileName,https://github.com/ied206/PEBakery.git,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("PEBakery.git", StringComparison.Ordinal));
        }
        #endregion

        #region DirPath
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_DirPath()
        {
            StrFormat_DirPath_1();
            StrFormat_DirPath_2();
            StrFormat_DirPath_3();
            StrFormat_DirPath_4();
            StrFormat_DirPath_5();
        }

        public void StrFormat_DirPath_1()
        {
            string rawCode = @"StrFormat,DirPath,C:\Windows\System32\notepad.exe,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals(@"C:\Windows\System32", StringComparison.Ordinal));
        }

        public void StrFormat_DirPath_2()
        {
            string rawCode = @"StrFormat,DirPath,C:\Windows\System32,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals(@"C:\Windows", StringComparison.Ordinal));
        }

        public void StrFormat_DirPath_3()
        {
            string rawCode = @"StrFormat,DirPath,,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals(string.Empty, StringComparison.Ordinal));
        }

        public void StrFormat_DirPath_4()
        {
            string rawCode = @"StrFormat,DirPath,https://github.com/ied206/PEBakery.git,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("https://github.com/ied206", StringComparison.Ordinal));
        }

        public void StrFormat_DirPath_5()
        {
            string rawCode = @"StrFormat,DirPath,https://github.com/ied206\PEBakery.git,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Error);
        }
        #endregion

        #region Path
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_Path()
        {
            StrFormat_Path_1();
            StrFormat_Path_2();
            StrFormat_Path_3();
            StrFormat_Path_4();
            StrFormat_Path_5();
        }

        public void StrFormat_Path_1()
        {
            string rawCode = @"StrFormat,Path,C:\Windows\System32\notepad.exe,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals(@"C:\Windows\System32\", StringComparison.Ordinal));
        }

        public void StrFormat_Path_2()
        {
            string rawCode = @"StrFormat,Path,C:\Windows\System32,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals(@"C:\Windows\", StringComparison.Ordinal));
        }

        public void StrFormat_Path_3()
        {
            string rawCode = @"StrFormat,Path,,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals(string.Empty, StringComparison.Ordinal));
        }

        public void StrFormat_Path_4()
        {
            string rawCode = @"StrFormat,Path,https://github.com/ied206/PEBakery.git,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("https://github.com/ied206/", StringComparison.Ordinal));
        }

        public void StrFormat_Path_5()
        {
            string rawCode = @"StrFormat,Path,https://github.com/ied206\PEBakery.git,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Error);
        }
        #endregion

        #region Ext
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_Ext()
        {
            StrFormat_Ext_1();
            StrFormat_Ext_2();
            StrFormat_Ext_3();
            StrFormat_Ext_4();
            StrFormat_Ext_5();
        }

        public void StrFormat_Ext_1()
        {
            string rawCode = @"StrFormat,Ext,C:\Windows\System32\notepad.exe,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals(".exe", StringComparison.Ordinal));
        }

        public void StrFormat_Ext_2()
        {
            string rawCode = @"StrFormat,Ext,C:\Windows\System32\,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals(string.Empty, StringComparison.Ordinal));
        }

        public void StrFormat_Ext_3()
        {
            string rawCode = @"StrFormat,Ext,,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals(string.Empty, StringComparison.Ordinal));
        }

        public void StrFormat_Ext_4()
        {
            string rawCode = @"StrFormat,Ext,https://github.com/ied206/PEBakery.git,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals(".git", StringComparison.Ordinal));
        }

        public void StrFormat_Ext_5()
        {
            string rawCode = @"StrFormat,Ext,https://github.com/ied206/PEBakery,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals(string.Empty, StringComparison.Ordinal));
        }
        #endregion

        #region Inc
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_Inc()
        {
            StrFormat_Inc_1();
            StrFormat_Inc_2();
            StrFormat_Inc_3();
            StrFormat_Inc_4();
            StrFormat_Inc_5();
            StrFormat_Inc_6();
        }

        public void StrFormat_Inc_1()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "15";

            string rawCode = @"StrFormat,Inc,%Dest%,20";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("35", StringComparison.Ordinal));
        }

        public void StrFormat_Inc_2()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "0x0F";

            string rawCode = @"StrFormat,Inc,%Dest%,20";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("35", StringComparison.Ordinal));
        }

        public void StrFormat_Inc_3()
        { // TODO: WB082 returns 'u', does Win10PESE utliize this case?
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "a";

            string rawCode = @"StrFormat,Inc,%Dest%,20";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Error);
        }

        public void StrFormat_Inc_4()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = string.Empty;

            string rawCode = @"StrFormat,Inc,%Dest%,20";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Error);
        }

        public void StrFormat_Inc_5()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "-5";

            string rawCode = @"StrFormat,Inc,%Dest%,20";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("15", StringComparison.Ordinal));
        }

        public void StrFormat_Inc_6()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "20";

            string rawCode = @"StrFormat,Inc,%Dest%,-5";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Error);
        }
        #endregion

        #region Dec
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_Dec()
        {
            StrFormat_Dec_1();
            StrFormat_Dec_2();
            StrFormat_Dec_3();
            StrFormat_Dec_4();
            StrFormat_Dec_5();
            StrFormat_Dec_6();
        }

        public void StrFormat_Dec_1()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "15";

            string rawCode = @"StrFormat,Dec,%Dest%,20";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("-5", StringComparison.Ordinal));
        }

        public void StrFormat_Dec_2()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "20";

            string rawCode = @"StrFormat,Dec,%Dest%,0x0F";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("5", StringComparison.Ordinal));
        }

        public void StrFormat_Dec_3()
        { // TODO: WB082 returns 'M', does Win10PESE utliize this case?
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "a";

            string rawCode = @"StrFormat,Dec,%Dest%,20";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Error);
        }

        public void StrFormat_Dec_4()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = string.Empty;

            string rawCode = @"StrFormat,Dec,%Dest%,20";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Error);
        }

        public void StrFormat_Dec_5()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "-5";

            string rawCode = @"StrFormat,Dec,%Dest%,20";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("-25", StringComparison.Ordinal));
        }

        public void StrFormat_Dec_6()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "20";

            string rawCode = @"StrFormat,Dec,%Dest%,-5";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Error);
        }
        #endregion

        #region Mult
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_Mult()
        {
            StrFormat_Mult_1();
            StrFormat_Mult_2();
            StrFormat_Mult_3();
            StrFormat_Mult_4();
            StrFormat_Mult_5();
            StrFormat_Mult_6();
        }

        public void StrFormat_Mult_1()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "4";

            string rawCode = @"StrFormat,Mult,%Dest%,20";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("80", StringComparison.Ordinal));
        }

        public void StrFormat_Mult_2()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "20";

            string rawCode = @"StrFormat,Mult,%Dest%,0x0F";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("300", StringComparison.Ordinal));
        }

        public void StrFormat_Mult_3()
        { // WB082 shows error in this case
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "a";

            string rawCode = @"StrFormat,Mult,%Dest%,2";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Error);
        }

        public void StrFormat_Mult_4()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = string.Empty;

            string rawCode = @"StrFormat,Mult,%Dest%,20";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Error);
        }

        public void StrFormat_Mult_5()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "-5";

            string rawCode = @"StrFormat,Mult,%Dest%,20";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("-100", StringComparison.Ordinal));
        }

        public void StrFormat_Mult_6()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "20";

            string rawCode = @"StrFormat,Mult,%Dest%,-5";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Error);
        }
        #endregion

        #region Div
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_Div()
        {
            StrFormat_Div_1();
            StrFormat_Div_2();
            StrFormat_Div_3();
            StrFormat_Div_4();
            StrFormat_Div_5();
            StrFormat_Div_6();
        }

        public void StrFormat_Div_1()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "81";

            string rawCode = @"StrFormat,Div,%Dest%,20";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("4", StringComparison.Ordinal));
        }

        public void StrFormat_Div_2()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "20";

            string rawCode = @"StrFormat,Div,%Dest%,0x0F";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("1", StringComparison.Ordinal));
        }

        public void StrFormat_Div_3()
        { // WB082 reports error
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "a";

            string rawCode = @"StrFormat,Div,%Dest%,2";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Error);
        }

        public void StrFormat_Div_4()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = string.Empty;

            string rawCode = @"StrFormat,Div,%Dest%,20";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Error);
        }

        public void StrFormat_Div_5()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "-25";

            string rawCode = @"StrFormat,Div,%Dest%,20";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("-1", StringComparison.Ordinal));
        }

        public void StrFormat_Div_6()
        {
            EngineState s = UnitTest_Engine.CreateEngineState();
            s.Variables["Dest"] = "20";

            string rawCode = @"StrFormat,Div,%Dest%,-5";
            UnitTest_Engine.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Error);
        }
        #endregion

        #region Left
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_Left()
        {
            StrFormat_Left_1();
            StrFormat_Left_2();
            StrFormat_Left_3();
        }

        public void StrFormat_Left_1()
        {
            string rawCode = "StrFormat,Left,PEBakery,3,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("PEB", StringComparison.Ordinal));
        }

        public void StrFormat_Left_2()
        { // StrFormat,Left,%A%,1, -> Causes WB082 access violation
            string rawCode = "StrFormat,Left,%Dest%,1,";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.ParserError);
        }

        public void StrFormat_Left_3()
        {
            string rawCode = "StrFormat,Left,PE,3,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("PE", StringComparison.Ordinal));
        }
        #endregion

        #region Right
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_Right()
        {
            StrFormat_Right_1();
            StrFormat_Right_2();
            StrFormat_Right_3();
        }

        public void StrFormat_Right_1()
        {
            string rawCode = "StrFormat,Right,PEBakery,3,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("ery", StringComparison.Ordinal));
        }

        public void StrFormat_Right_2()
        { // StrFormat,Left,%A%,1, -> Causes WB082 access violation
            string rawCode = "StrFormat,Right,%Dest%,1,";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.ParserError);
        }

        public void StrFormat_Right_3()
        {
            string rawCode = "StrFormat,Right,PE,3,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("PE", StringComparison.Ordinal));
        }
        #endregion

        #region SubStr
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_SubStr()
        { // StrFormat,SubStr,<SrcStr>,<StartPos>,<Length>,<DestVar>
            StrFormat_SubStr_1();
            StrFormat_SubStr_2();
            StrFormat_SubStr_3();
            StrFormat_SubStr_4();
            StrFormat_SubStr_5();
            StrFormat_SubStr_6();
        }

        public void StrFormat_SubStr_1()
        {
            string rawCode = "StrFormat,SubStr,PEBakery,3,2,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("Ba", StringComparison.Ordinal));
        }

        public void StrFormat_SubStr_2()
        {
            string rawCode = "StrFormat,SubStr,PEBakery,4,3,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("ake", StringComparison.Ordinal));
        }

        public void StrFormat_SubStr_3()
        {
            string rawCode = "StrFormat,SubStr,PEBakery,0,2,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Error);
        }

        public void StrFormat_SubStr_4()
        {
            string rawCode = "StrFormat,SubStr,PEBakery,3,0,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Error);
        }

        public void StrFormat_SubStr_5()
        {
            string rawCode = "StrFormat,SubStr,Joveler,10,2,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Error);
        }

        public void StrFormat_SubStr_6()
        {
            string rawCode = "StrFormat,SubStr,Joveler,3,10,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Error);
        }


        #endregion

        #region Len
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_Len()
        {
            StrFormat_Len_1();
            StrFormat_Len_2();
        }

        public void StrFormat_Len_1()
        {
            string rawCode = "StrFormat,Len,PEBakery,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("8", StringComparison.Ordinal));
        }

        public void StrFormat_Len_2()
        {
            string rawCode = "StrFormat,Len,,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("0", StringComparison.Ordinal));
        }
        #endregion

        #region LTrim
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_LTrim()
        {
            StrFormat_LTrim_1();
            StrFormat_LTrim_2();
        }

        public void StrFormat_LTrim_1()
        {
            string rawCode = "StrFormat,LTrim,PEBakery,3,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("akery", StringComparison.Ordinal));
        }

        public void StrFormat_LTrim_2()
        {
            string rawCode = "StrFormat,LTrim,,3,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Error);
        }
        #endregion

        #region RTrim
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_RTrim()
        {
            StrFormat_RTrim_1();
            StrFormat_RTrim_2();
        }

        public void StrFormat_RTrim_1()
        {
            string rawCode = "StrFormat,RTrim,PEBakery,3,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("PEBak", StringComparison.Ordinal));
        }

        public void StrFormat_RTrim_2()
        {
            string rawCode = "StrFormat,RTrim,,3,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Error);
        }
        #endregion

        #region CTrim
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_CTrim()
        {
            StrFormat_CTrim_1();
            StrFormat_CTrim_2();
            StrFormat_CTrim_3();
        }

        public void StrFormat_CTrim_1()
        { // In WB082, it returns "-PEBakery-", because WB082 uses only first character
            string rawCode = "StrFormat,CTrim,_-PEBakery-_,_-,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("PEBakery", StringComparison.Ordinal));
        }

        public void StrFormat_CTrim_2()
        {
            string rawCode = "StrFormat,CTrim, PEBakery ,\" \",%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("PEBakery", StringComparison.Ordinal));
        }

        public void StrFormat_CTrim_3()
        { // Access violation in WB082
            string rawCode = "StrFormat,CTrim,PEBakery,,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Error);
        }
        #endregion

        #region NTrim
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_NTrim()
        {
            StrFormat_NTrim_1();
            StrFormat_NTrim_2();
        }

        public void StrFormat_NTrim_1()
        {
            string rawCode = "StrFormat,NTrim,PEBakery100,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("PEBakery", StringComparison.Ordinal));
        }

        public void StrFormat_NTrim_2()
        {
            string rawCode = "StrFormat,NTrim,PEBakery,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("PEBakery", StringComparison.Ordinal));
        }

        #endregion

        #region Pos
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_Pos()
        {
            StrFormat_Pos_1();
            StrFormat_Pos_2();
            StrFormat_Pos_3();
            StrFormat_Pos_4();
        }

        public void StrFormat_Pos_1()
        {
            string rawCode = "StrFormat,Pos,SouthKorea,thK,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("4", StringComparison.Ordinal));
        }

        public void StrFormat_Pos_2()
        {
            string rawCode = "StrFormat,Pos,SouthKorea,thk,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("4", StringComparison.Ordinal));
        }

        public void StrFormat_Pos_3()
        {
            string rawCode = "StrFormat,Pos,SouthKorea,abc,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("0", StringComparison.Ordinal));
        }

        public void StrFormat_Pos_4()
        {
            string rawCode = "StrFormat,Pos,SouthKorea,,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("0", StringComparison.Ordinal));
        }
        #endregion

        #region PosX
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_PosX()
        {
            StrFormat_PosX_1();
            StrFormat_PosX_2();
            StrFormat_PosX_3();
            StrFormat_PosX_4();
        }

        public void StrFormat_PosX_1()
        {
            string rawCode = "StrFormat,PosX,SouthKorea,thK,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("4", StringComparison.Ordinal));
        }

        public void StrFormat_PosX_2()
        {
            string rawCode = "StrFormat,PosX,SouthKorea,thk,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("0", StringComparison.Ordinal));
        }

        public void StrFormat_PosX_3()
        {
            string rawCode = "StrFormat,PosX,SouthKorea,abc,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("0", StringComparison.Ordinal));
        }

        public void StrFormat_PosX_4()
        {
            string rawCode = "StrFormat,PosX,SouthKorea,,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("0", StringComparison.Ordinal));
        }
        #endregion

        #region Replace
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_Replace()
        {
            StrFormat_Replace_1();
            StrFormat_Replace_2();
            StrFormat_Replace_3();
            StrFormat_Replace_4();
        }

        public void StrFormat_Replace_1()
        {
            string rawCode = "StrFormat,Replace,PEBakery,Bake,Pake,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("PEPakery", StringComparison.Ordinal));
        }

        public void StrFormat_Replace_2()
        {
            string rawCode = "StrFormat,Replace,PEBakery,bake,Pake,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("PEPakery", StringComparison.Ordinal));
        }

        public void StrFormat_Replace_3()
        {
            string rawCode = "StrFormat,Replace,PEBakery,_,__,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("PEBakery", StringComparison.Ordinal));
        }

        public void StrFormat_Replace_4()
        {
            string rawCode = "StrFormat,Replace,SouthKorea,,_,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("SouthKorea", StringComparison.Ordinal));
        }
        #endregion

        #region ReplaceX
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_ReplaceX()
        {
            StrFormat_ReplaceX_1();
            StrFormat_ReplaceX_2();
            StrFormat_ReplaceX_3();
            StrFormat_ReplaceX_4();
        }

        public void StrFormat_ReplaceX_1()
        {
            string rawCode = "StrFormat,ReplaceX,PEBakery,Bake,Pake,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("PEPakery", StringComparison.Ordinal));
        }

        public void StrFormat_ReplaceX_2()
        {
            string rawCode = "StrFormat,ReplaceX,PEBakery,bake,Pake,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("PEBakery", StringComparison.Ordinal));
        }

        public void StrFormat_ReplaceX_3()
        {
            string rawCode = "StrFormat,ReplaceX,PEBakery,_,__,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("PEBakery", StringComparison.Ordinal));
        }

        public void StrFormat_ReplaceX_4()
        {
            string rawCode = "StrFormat,ReplaceX,SouthKorea,,_,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("SouthKorea", StringComparison.Ordinal));
        }
        #endregion

        #region Split
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_Split()
        {
            StrFormat_Split_1();
            StrFormat_Split_2();
            StrFormat_Split_3();
            StrFormat_Split_4();
        }

        public void StrFormat_Split_1()
        {
            string rawCode = "StrFormat,Split,A/B/C/D/E/F,/,0,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("6", StringComparison.Ordinal));
        }

        public void StrFormat_Split_2()
        {
            string rawCode = "StrFormat,Split,A/B/C/D/E/F,/,2,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("B", StringComparison.Ordinal));
        }

        public void StrFormat_Split_3()
        {
            string rawCode = "StrFormat,Split,A/B/C/D/E/F,/,5,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals("E", StringComparison.Ordinal));
        }

        public void StrFormat_Split_4()
        {
            string rawCode = "StrFormat,Split,A/B/C/D/E/F,/,7,%Dest%";
            EngineState s = UnitTest_Engine.Eval(rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals(string.Empty, StringComparison.Ordinal));
        }
        #endregion
    }
}


