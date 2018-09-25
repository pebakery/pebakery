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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Core;
using System;

namespace PEBakery.Tests.Core.Command
{
    [TestClass]
    public class CommandStringTests
    {
        #region IntToBytes
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        public void IntToBytes()
        {
            EngineState s = EngineTests.CreateEngineState();

            // 2 argument
            s.Variables.SetValue(VarsType.Local, "Dest", "2000");
            DestSuccessTemplate(s, "StrFormat,IntToBytes,%Dest%", "1.953KB");

            // 3 argument
            DestSuccessTemplate(s, "StrFormat,IntToBytes,10240,%Dest%", "10KB");
            DestSuccessTemplate(s, "StrFormat,IntToBytes,4404020,%Dest%", "4.2MB");
            DestSuccessTemplate(s, "StrFormat,IntToBytes,5561982650,%Dest%", "5.18GB");
            DestSuccessTemplate(s, "StrFormat,IntToBytes,2193525697413,%Dest%", "1.995TB");
            DestSuccessTemplate(s, "StrFormat,IntToBytes,2270940112101573,%Dest%", "2.017PB");
            DestSuccessTemplate(s, "StrFormat,IntToBytes,2229281815548396000,%Dest%", "1980PB");
            DestErrorTemplate(s, "StrFormat,IntToBytes,WrongInteger,%Dest%", ErrorCheck.Error);
        }
        #endregion

        #region BytesToInt
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        public void BytesToInt()
        {
            EngineState s = EngineTests.CreateEngineState();

            DestSuccessTemplate(s, "StrFormat,BytesToInt,10KB,%Dest%", "10240");
            DestSuccessTemplate(s, "StrFormat,BytesToInt,4.2MB,%Dest%", "4404020");
            DestSuccessTemplate(s, "StrFormat,BytesToInt,5.18GB,%Dest%", "5561982649");
            DestSuccessTemplate(s, "StrFormat,BytesToInt,1.995TB,%Dest%", "2193525697414");
            DestSuccessTemplate(s, "StrFormat,BytesToInt,2.017PB,%Dest%", "2270940112101573");
            DestSuccessTemplate(s, "StrFormat,BytesToInt,1980PB,%Dest%", "2229281815548395520");
            DestErrorTemplate(s, "StrFormat,BytesToInt,WrongBytes,%Dest%", ErrorCheck.Error);
        }
        #endregion

        #region Hex
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        public void Hex()
        {
            EngineState s = EngineTests.CreateEngineState();

            DestSuccessTemplate(s, "StrFormat,Hex,1234,%Dest%", "000004D2");
            DestSuccessTemplate(s, "StrFormat,Hex,-1234,%Dest%", "FFFFFB2E");
            DestErrorTemplate(s, "StrFormat,Hex,ABCD,%Dest%", ErrorCheck.Error);
        }
        #endregion

        #region Ceil
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void Ceil()
        {
            EngineState s = EngineTests.CreateEngineState();

            InitDestErrorTemplate(s, "StrFormat,Ceil,%Dest%,-10", "4", ErrorCheck.Error);
            InitDestSuccessTemplate(s, "StrFormat,Ceil,%Dest%,10", "6", "10");
            InitDestSuccessTemplate(s, "StrFormat,Ceil,%Dest%,800", "799", "800");
            InitDestSuccessTemplate(s, "StrFormat,Ceil,%Dest%,800", "801", "1600");
            InitDestSuccessTemplate(s, "StrFormat,Ceil,%Dest%,K", "1000", "1024");
            InitDestSuccessTemplate(s, "StrFormat,Ceil,%Dest%,K", "1200", "2048");
            InitDestSuccessTemplate(s, "StrFormat,Ceil,%Dest%,M", "1048570", "1048576");
            InitDestSuccessTemplate(s, "StrFormat,Ceil,%Dest%,M", "1048580", "2097152");
        }
        #endregion

        #region Floor
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void Floor()
        {
            EngineState s = EngineTests.CreateEngineState();

            InitDestErrorTemplate(s, "StrFormat,Floor,%Dest%,-10", "4", ErrorCheck.Error);
            InitDestSuccessTemplate(s, "StrFormat,Floor,%Dest%,10", "6", "0");
            InitDestSuccessTemplate(s, "StrFormat,Floor,%Dest%,800", "799", "0");
            InitDestSuccessTemplate(s, "StrFormat,Floor,%Dest%,800", "801", "800");
            InitDestSuccessTemplate(s, "StrFormat,Floor,%Dest%,K", "1000", "0");
            InitDestSuccessTemplate(s, "StrFormat,Floor,%Dest%,K", "1200", "1024");
            InitDestSuccessTemplate(s, "StrFormat,Floor,%Dest%,M", "1048570", "0");
            InitDestSuccessTemplate(s, "StrFormat,Floor,%Dest%,M", "1048580", "1048576");
        }
        #endregion

        #region Round
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void Round()
        {
            EngineState s = EngineTests.CreateEngineState();

            InitDestSuccessTemplate(s, "StrFormat,Round,%Dest%,10", "4", "0");
            InitDestSuccessTemplate(s, "StrFormat,Round,%Dest%,10", "6", "10");
            InitDestSuccessTemplate(s, "StrFormat,Round,%Dest%,800", "350", "0");
            InitDestSuccessTemplate(s, "StrFormat,Round,%Dest%,800", "450", "800");
            InitDestSuccessTemplate(s, "StrFormat,Round,%Dest%,K", "500", "0");
            InitDestSuccessTemplate(s, "StrFormat,Round,%Dest%,K", "600", "1024");
            InitDestSuccessTemplate(s, "StrFormat,Round,%Dest%,M", "524286", "0");
            InitDestSuccessTemplate(s, "StrFormat,Round,%Dest%,M", "524290", "1048576");
        }
        #endregion

        #region Date
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void Date()
        {
            // Cannot compare Date string directly because of processing delay
            // So test only converted .Net Date Strings
            StrFormat_Date_1();
            StrFormat_Date_2();
            StrFormat_Date_3();
            StrFormat_Date_4();
            StrFormat_Date_5();
        }

        public void StrFormat_Date_1()
        {
            const string rawCode = "StrFormat,Date,%Dest%,yyyy-mm-dd_hh:nn:ss.zzz";
            CodeParser parser = new CodeParser(EngineTests.DummySection(), Global.Setting.ExportCodeParserOptions());
            CodeCommand cmd = parser.ParseStatement(rawCode);

            CodeInfo_StrFormat info = cmd.Info.Cast<CodeInfo_StrFormat>();
            StrFormatInfo_Date subInfo = info.SubInfo.Cast<StrFormatInfo_Date>();

            Assert.IsTrue(subInfo.FormatString.Equals("yyyy-MM-dd_HH:mm:ss.fff", StringComparison.Ordinal));
        }

        public void StrFormat_Date_2()
        {
            const string rawCode = "StrFormat,DATE,#9,yyyymmddhhnnsszzz";
            CodeParser parser = new CodeParser(EngineTests.DummySection(), Global.Setting.ExportCodeParserOptions());
            CodeCommand cmd = parser.ParseStatement(rawCode);

            CodeInfo_StrFormat info = cmd.Info.Cast<CodeInfo_StrFormat>();
            StrFormatInfo_Date subInfo = info.SubInfo.Cast<StrFormatInfo_Date>();

            Assert.IsTrue(subInfo.FormatString.Equals("yyyyMMddHHmmssfff", StringComparison.Ordinal));
        }

        public void StrFormat_Date_3()
        {
            const string rawCode = "StrFormat,Date,%Dest%,xxx-mm-dd_hh:nn:ss.zzz";
            CodeParser parser = new CodeParser(EngineTests.DummySection(), Global.Setting.ExportCodeParserOptions());
            CodeCommand cmd = parser.ParseStatement(rawCode);

            // Successfully induced error
            if (cmd.Type == CodeType.Error) return;

            Assert.Fail();
        }

        public void StrFormat_Date_4()
        {
            const string rawCode = "StrFormat,Date,%Dest%,qqqmdd_hhnnss.zzz";
            CodeParser parser = new CodeParser(EngineTests.DummySection(), Global.Setting.ExportCodeParserOptions());
            CodeCommand cmd = parser.ParseStatement(rawCode);

            // Successfully induced error
            if (cmd.Type == CodeType.Error) return;

            Assert.Fail();
        }

        public void StrFormat_Date_5()
        {
            const string rawCode = "StrFormat,DATE,#9,yyyymmddhhnnsszzz am/pm";
            CodeParser parser = new CodeParser(EngineTests.DummySection(), Global.Setting.ExportCodeParserOptions());
            CodeCommand cmd = parser.ParseStatement(rawCode);

            CodeInfo_StrFormat info = cmd.Info.Cast<CodeInfo_StrFormat>();
            StrFormatInfo_Date subInfo = info.SubInfo.Cast<StrFormatInfo_Date>();

            Assert.IsTrue(subInfo.FormatString.Equals("yyyyMMddhhmmssfff tt", StringComparison.Ordinal));
        }
        #endregion

        #region FileName
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void FileName()
        {
            EngineState s = EngineTests.CreateEngineState();

            DestSuccessTemplate(s, @"StrFormat,FileName,C:\Windows\System32\notepad.exe,%Dest%", "notepad.exe");
            DestSuccessTemplate(s, @"StrFormat,FileName,C:\Windows\System32\,%Dest%", string.Empty);
            DestSuccessTemplate(s, @"StrFormat,FileName,,%Dest%", string.Empty);
            DestSuccessTemplate(s, @"StrFormat,FileName,https://github.com/ied206/PEBakery.git,%Dest%", "PEBakery.git");
        }
        #endregion

        #region DirPath, Path
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void DirPath()
        {
            EngineState s = EngineTests.CreateEngineState();

            DestSuccessTemplate(s, @"StrFormat,DirPath,C:\Windows\System32\notepad.exe,%Dest%", @"C:\Windows\System32\");
            DestSuccessTemplate(s, @"StrFormat,DirPath,C:\Windows\System32,%Dest%", @"C:\Windows\");
            DestSuccessTemplate(s, @"StrFormat,DirPath,,%Dest%", string.Empty);
            DestSuccessTemplate(s, @"StrFormat,DirPath,https://github.com/ied206/PEBakery.git,%Dest%", "https://github.com/ied206/");
            DestSuccessTemplate(s, @"StrFormat,Path,C:\Windows\System32\notepad.exe,%Dest%", @"C:\Windows\System32\");
            DestSuccessTemplate(s, @"StrFormat,Path,C:\Windows\System32,%Dest%", @"C:\Windows\");
            DestSuccessTemplate(s, @"StrFormat,Path,,%Dest%", string.Empty);
            DestSuccessTemplate(s, @"StrFormat,Path,https://github.com/ied206/PEBakery.git,%Dest%", "https://github.com/ied206/");
            DestErrorTemplate(s, @"StrFormat,DirPath,https://github.com/ied206\PEBakery.git,%Dest%", ErrorCheck.Error);
        }
        #endregion

        #region Ext
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void Ext()
        {
            EngineState s = EngineTests.CreateEngineState();

            DestSuccessTemplate(s, @"StrFormat,Ext,C:\Windows\System32\notepad.exe,%Dest%", @".exe");
            DestSuccessTemplate(s, @"StrFormat,Ext,C:\Windows\System32\,%Dest%", string.Empty);
            DestSuccessTemplate(s, @"StrFormat,Ext,,%Dest%", string.Empty);
            DestSuccessTemplate(s, @"StrFormat,Ext,https://github.com/ied206/PEBakery.git,%Dest%", ".git");
            DestSuccessTemplate(s, @"StrFormat,Ext,https://github.com/ied206/PEBakery,%Dest%", string.Empty);
        }
        #endregion

        #region PathCombine
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void PathCombine()
        {
            EngineState s = EngineTests.CreateEngineState();

            DestSuccessTemplate(s, @"StrFormat,PathCombine,C:\Windows\System32,notepad.exe,%Dest%", @"C:\Windows\System32\notepad.exe");
            DestSuccessTemplate(s, @"StrFormat,PathCombine,C:\Windows,System32\notepad.exe,%Dest%", @"C:\Windows\System32\notepad.exe");
            DestSuccessTemplate(s, @"StrFormat,PathCombine,C:\,Windows\System32\notepad.exe,%Dest%", @"C:\Windows\System32\notepad.exe");
            DestSuccessTemplate(s, @"StrFormat,PathCombine,C:,Windows\System32\notepad.exe,%Dest%", @"C:\Windows\System32\notepad.exe");
            DestSuccessTemplate(s, @"StrFormat,PathCombine,D:\Joveler,Korea,%Dest%", @"D:\Joveler\Korea");
        }
        #endregion

        #region Inc
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void Inc()
        {
            EngineState s = EngineTests.CreateEngineState();

            InitDestSuccessTemplate(s, @"StrFormat,Inc,%Dest%,20", "15", "35");
            InitDestSuccessTemplate(s, @"StrFormat,Inc,%Dest%,20", "0x0F", "35");
            InitDestSuccessTemplate(s, @"StrFormat,Inc,%Dest%,1", "Y", "Z");
            InitDestErrorTemplate(s, @"StrFormat,Inc,%Dest%,2", "Y", ErrorCheck.Error);
            InitDestErrorTemplate(s, @"StrFormat,Inc,%Dest%,20", string.Empty, ErrorCheck.Error);
            InitDestSuccessTemplate(s, @"StrFormat,Inc,%Dest%,20", "-5", "15");
            InitDestSuccessTemplate(s, @"StrFormat,Inc,%Dest%,-5", "20", "15");
        }
        #endregion

        #region Dec
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void Dec()
        {
            EngineState s = EngineTests.CreateEngineState();

            InitDestSuccessTemplate(s, @"StrFormat,Dec,%Dest%,20", "15", "-5");
            InitDestSuccessTemplate(s, @"StrFormat,Dec,%Dest%,0x0F", "20", "5");
            InitDestSuccessTemplate(s, @"StrFormat,Dec,%Dest%,1", "B", "A");
            InitDestErrorTemplate(s, @"StrFormat,Dec,%Dest%,2", "B", ErrorCheck.Error);
            InitDestErrorTemplate(s, @"StrFormat,Dec,%Dest%,20", "a", ErrorCheck.Error);
            InitDestErrorTemplate(s, @"StrFormat,Dec,%Dest%,20", string.Empty, ErrorCheck.Error);
            InitDestSuccessTemplate(s, @"StrFormat,Dec,%Dest%,20", "-5", "-25");
            InitDestSuccessTemplate(s, @"StrFormat,Dec,%Dest%,-5", "20", "25");
        }
        #endregion

        #region Mult
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void Mult()
        {
            EngineState s = EngineTests.CreateEngineState();

            InitDestSuccessTemplate(s, @"StrFormat,Mult,%Dest%,20", "4", "80");
            InitDestSuccessTemplate(s, @"StrFormat,Mult,%Dest%,0x0F", "20", "300");
            // WB082 reports error
            InitDestErrorTemplate(s, @"StrFormat,Mult,%Dest%,2", "a", ErrorCheck.Error);
            InitDestErrorTemplate(s, @"StrFormat,Mult,%Dest%,20", string.Empty, ErrorCheck.Error);
            InitDestSuccessTemplate(s, @"StrFormat,Mult,%Dest%,20", "-5", "-100");
            InitDestSuccessTemplate(s, @"StrFormat,Mult,%Dest%,-5", "20", "-100");
        }
        #endregion

        #region Div
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void Div()
        {
            EngineState s = EngineTests.CreateEngineState();

            InitDestSuccessTemplate(s, @"StrFormat,Div,%Dest%,20", "81", "4");
            InitDestSuccessTemplate(s, @"StrFormat,Div,%Dest%,0x0F", "20", "1");
            // WB082 reports error
            InitDestErrorTemplate(s, @"StrFormat,Div,%Dest%,2", "a", ErrorCheck.Error);
            InitDestErrorTemplate(s, @"StrFormat,Div,%Dest%,20", string.Empty, ErrorCheck.Error);
            InitDestSuccessTemplate(s, @"StrFormat,Div,%Dest%,20", "-25", "-1");
            InitDestSuccessTemplate(s, @"StrFormat,Div,%Dest%,-5", "20", "-4");
        }
        #endregion

        #region Left
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void Left()
        {
            EngineState s = EngineTests.CreateEngineState();

            DestSuccessTemplate(s, @"StrFormat,Left,PEBakery,3,%Dest%", "PEB");
            // StrFormat,Left,%A%,1, -> Causes WB082 access violation
            DestErrorTemplate(s, @"StrFormat,Left,%Dest%,1,", ErrorCheck.ParserError);
            DestSuccessTemplate(s, "StrFormat,Left,PE,3,%Dest%", "PE");
        }
        #endregion

        #region Right
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void Right()
        {
            EngineState s = EngineTests.CreateEngineState();

            DestSuccessTemplate(s, @"StrFormat,Right,PEBakery,3,%Dest%", "ery");
            // StrFormat,Right,%A%,1, -> Causes WB082 access violation
            DestErrorTemplate(s, "StrFormat,Right,%Dest%,1,", ErrorCheck.ParserError);
            DestSuccessTemplate(s, "StrFormat,Right,PE,3,%Dest%", "PE");
        }
        #endregion

        #region SubStr
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void SubStr()
        {
            EngineState s = EngineTests.CreateEngineState();

            DestSuccessTemplate(s, @"StrFormat,SubStr,PEBakery,3,2,%Dest%", "Ba");
            DestSuccessTemplate(s, @"StrFormat,SubStr,PEBakery,4,3,%Dest%", "ake");
            DestErrorTemplate(s, @"StrFormat,SubStr,PEBakery,0,2,%Dest%", ErrorCheck.Error);
            DestErrorTemplate(s, @"StrFormat,SubStr,PEBakery,3,0,%Dest%", ErrorCheck.Error);
            DestErrorTemplate(s, @"StrFormat,SubStr,Joveler,10,2,%Dest%", ErrorCheck.Error);
            DestErrorTemplate(s, @"StrFormat,SubStr,Joveler,3,10,%Dest%", ErrorCheck.Error);
        }
        #endregion

        #region Len
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void Len()
        {
            EngineState s = EngineTests.CreateEngineState();

            DestSuccessTemplate(s, @"StrFormat,Len,PEBakery,%Dest%", "8");
            DestSuccessTemplate(s, @"StrFormat,Len,,%Dest%", "0");
        }
        #endregion

        #region LTrim
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void LTrim()
        {
            EngineState s = EngineTests.CreateEngineState();

            DestSuccessTemplate(s, @"StrFormat,LTrim,PEBakery,3,%Dest%", "akery");
            DestSuccessTemplate(s, @"StrFormat,LTrim,PEBakery,10,%Dest%", string.Empty);
            DestSuccessTemplate(s, @"StrFormat,LTrim,PEBakery,-1,%Dest%", "PEBakery");
            DestSuccessTemplate(s, @"StrFormat,LTrim,,3,%Dest%", string.Empty);
        }
        #endregion

        #region RTrim
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void RTrim()
        {
            EngineState s = EngineTests.CreateEngineState();

            DestSuccessTemplate(s, @"StrFormat,RTrim,PEBakery,3,%Dest%", "PEBak");
            DestSuccessTemplate(s, @"StrFormat,RTrim,PEBakery,10,%Dest%", string.Empty);
            DestSuccessTemplate(s, @"StrFormat,RTrim,PEBakery,-1,%Dest%", "PEBakery");
            DestSuccessTemplate(s, @"StrFormat,RTrim,,3,%Dest%", string.Empty);
        }
        #endregion

        #region CTrim
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void CTrim()
        {
            EngineState s = EngineTests.CreateEngineState();

            // In WB082, it returns "-PEBakery-", because WB082 uses only first character
            DestSuccessTemplate(s, @"StrFormat,CTrim,_-PEBakery-_,_-,%Dest%", "PEBakery");
            DestSuccessTemplate(s, "StrFormat,CTrim, PEBakery ,\" \",%Dest%", "PEBakery");
            // Access violation in WB082
            DestErrorTemplate(s, "StrFormat,CTrim,PEBakery,,%Dest%", ErrorCheck.Error);
        }
        #endregion

        #region NTrim
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void NTrim()
        {
            EngineState s = EngineTests.CreateEngineState();

            DestSuccessTemplate(s, @"StrFormat,NTrim,PEBakery100,%Dest%", "PEBakery");
            DestSuccessTemplate(s, @"StrFormat,NTrim,PEBakery,%Dest%", "PEBakery");
        }
        #endregion

        #region UCase
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        public void UCase()
        {
            EngineState s = EngineTests.CreateEngineState();

            DestSuccessTemplate(s, "StrFormat,UCase,abcDEF,%Dest%", "ABCDEF");
            DestSuccessTemplate(s, "StrFormat,UCase,가나다라,%Dest%", "가나다라");
            DestSuccessTemplate(s, "StrFormat,UCase,abcDEF가나다라,%Dest%", "ABCDEF가나다라");
        }
        #endregion

        #region LCase
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        public void LCase()
        {
            EngineState s = EngineTests.CreateEngineState();

            DestSuccessTemplate(s, "StrFormat,LCase,abcDEF,%Dest%", "abcdef");
            DestSuccessTemplate(s, "StrFormat,LCase,가나다라,%Dest%", "가나다라");
            DestSuccessTemplate(s, "StrFormat,LCase,abcDEF가나다라,%Dest%", "abcdef가나다라");
        }
        #endregion

        #region Pos
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void Pos()
        {
            EngineState s = EngineTests.CreateEngineState();

            DestSuccessTemplate(s, @"StrFormat,Pos,SouthKorea,thK,%Dest%", "4");
            DestSuccessTemplate(s, @"StrFormat,Pos,SouthKorea,hk,%Dest%", "5");
            DestSuccessTemplate(s, @"StrFormat,Pos,SouthKorea,abc,%Dest%", "0");
            DestSuccessTemplate(s, @"StrFormat,Pos,SouthKorea,,%Dest%", "0");
        }
        #endregion

        #region PosX
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void PosX()
        {
            EngineState s = EngineTests.CreateEngineState();

            DestSuccessTemplate(s, @"StrFormat,PosX,SouthKorea,thK,%Dest%", "4");
            DestSuccessTemplate(s, @"StrFormat,PosX,SouthKorea,thk,%Dest%", "0");
            DestSuccessTemplate(s, @"StrFormat,PosX,SouthKorea,abc,%Dest%", "0");
            DestSuccessTemplate(s, @"StrFormat,PosX,SouthKorea,,%Dest%", "0");
        }
        #endregion

        #region Replace
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void Replace()
        {
            EngineState s = EngineTests.CreateEngineState();

            DestSuccessTemplate(s, @"StrFormat,Replace,PEBakery,Bake,Pake,%Dest%", "PEPakery");
            DestSuccessTemplate(s, @"StrFormat,Replace,PEBakery,bake,Pake,%Dest%", "PEPakery");
            DestSuccessTemplate(s, @"StrFormat,Replace,PEBakery,_,__,%Dest%", "PEBakery");
            DestSuccessTemplate(s, @"StrFormat,Replace,SouthKorea,,_,%Dest%", "SouthKorea");
        }
        #endregion

        #region ReplaceX
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void ReplaceX()
        {
            EngineState s = EngineTests.CreateEngineState();

            DestSuccessTemplate(s, @"StrFormat,ReplaceX,PEBakery,Bake,Pake,%Dest%", "PEPakery");
            DestSuccessTemplate(s, @"StrFormat,ReplaceX,PEBakery,bake,Pake,%Dest%", "PEBakery");
            DestSuccessTemplate(s, @"StrFormat,ReplaceX,PEBakery,_,__,%Dest%", "PEBakery");
            DestSuccessTemplate(s, @"StrFormat,ReplaceX,SouthKorea,,_,%Dest%", "SouthKorea");
        }
        #endregion

        #region Split
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void Split()
        {
            EngineState s = EngineTests.CreateEngineState();

            DestSuccessTemplate(s, @"StrFormat,Split,A/B/C/D/E/F,/,0,%Dest%", "6");
            DestSuccessTemplate(s, @"StrFormat,Split,A/B/C/D/E/F,/,2,%Dest%", "B");
            DestSuccessTemplate(s, @"StrFormat,Split,A/B/C/D/E/F,/,5,%Dest%", "E");
            InitDestSuccessTemplate(s, @"StrFormat,Split,A/B/C/D/E/F,/,7,%Dest%", string.Empty, string.Empty);
        }
        #endregion

        #region PadLeft
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        public void PadLeft()
        {
            EngineState s = EngineTests.CreateEngineState();

            DestSuccessTemplate(s, @"StrFormat,PadLeft,A,3,0,%Dest%", "00A");
            DestSuccessTemplate(s, @"StrFormat,PadLeft,장,3,張,%Dest%", "張張장");
            DestSuccessTemplate(s, @"StrFormat,PadLeft,ABC,1,z,%Dest%", "ABC");
            DestErrorTemplate(s, @"StrFormat,PadLeft,A,3,%Dest%", ErrorCheck.ParserError);
        }
        #endregion

        #region PadRight
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        public void PadRight()
        {
            EngineState s = EngineTests.CreateEngineState();

            DestSuccessTemplate(s, @"StrFormat,PadRight,A,3,0,%Dest%", "A00");
            DestSuccessTemplate(s, @"StrFormat,PadRight,장,3,張,%Dest%", "장張張");
            DestSuccessTemplate(s, @"StrFormat,PadRight,ABC,1,z,%Dest%", "ABC");
            DestErrorTemplate(s, @"StrFormat,PadRight,A,3,%Dest%", ErrorCheck.ParserError);
        }
        #endregion

        #region Utility
        public void InitDestSuccessTemplate(EngineState s, string rawCode, string initStr, string destStr)
        {
            s.Variables["Dest"] = initStr;
            EngineTests.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals(destStr, StringComparison.Ordinal));
        }

        public void InitDestErrorTemplate(EngineState s, string rawCode, string initStr, ErrorCheck check)
        {
            s.Variables["Dest"] = initStr;
            EngineTests.Eval(s, rawCode, CodeType.StrFormat, check);
        }

        public void DestSuccessTemplate(EngineState s, string rawCode, string destStr)
        {
            EngineTests.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Success);

            if (destStr != null)
            {
                string dest = s.Variables["Dest"];
                Assert.IsTrue(dest.Equals(destStr, StringComparison.Ordinal));
            }
        }

        public void DestErrorTemplate(EngineState s, string rawCode, ErrorCheck check)
        {
            EngineTests.Eval(s, rawCode, CodeType.StrFormat, check);
        }
        #endregion
    }
}


