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
        [TestMethod]
        public void StrFormat_Date()
        {
            // Cannot compare Date string directly because of processing delay
            // So test only converted .Net Date Strings
            StrFormat_Date_1();
            StrFormat_Date_2();
            StrFormat_Date_3();
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

        public void StrFormat_Date_3()
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
    }
}


