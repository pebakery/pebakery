/*
    Copyright (C) 2017 Hajin Jang
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
*/

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Core;
using PEBakery.Core.Commands;
using System.Collections.Generic;
using PEBakery.Exceptions;
using System.Diagnostics;

namespace PEBakery.Tests.Core.Command
{
    [TestClass]
    public class CommandStringTests
    {
        #region IntToBytes
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        public void StrFormat_IntToBytes()
        {
            EngineState s = EngineTests.CreateEngineState();

            // 2 argument
            s.Variables.SetValue(VarsType.Local, "Dest", "2000");
            StrFormat_Dest_Template(s, "StrFormat,IntToBytes,%Dest%", "1.953KB");

            // 3 argument
            StrFormat_Dest_Template(s, "StrFormat,IntToBytes,10240,%Dest%", "10KB");
            StrFormat_Dest_Template(s, "StrFormat,IntToBytes,4404020,%Dest%", "4.2MB");
            StrFormat_Dest_Template(s, "StrFormat,IntToBytes,5561982650,%Dest%", "5.18GB");
            StrFormat_Dest_Template(s, "StrFormat,IntToBytes,2193525697413,%Dest%", "1.995TB");
            StrFormat_Dest_Template(s, "StrFormat,IntToBytes,2270940112101573,%Dest%", "2.017PB");
            StrFormat_Dest_Template(s, "StrFormat,IntToBytes,2229281815548396000,%Dest%", "1980PB");
            StrFormat_Dest_Template_Error(s, "StrFormat,IntToBytes,WrongInteger,%Dest%", ErrorCheck.Error);
        }
        #endregion

        #region BytesToInt
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        public void StrFormat_BytesToInt()
        {
            EngineState s = EngineTests.CreateEngineState();

            StrFormat_Dest_Template(s, "StrFormat,BytesToInt,10KB,%Dest%", "10240");
            StrFormat_Dest_Template(s, "StrFormat,BytesToInt,4.2MB,%Dest%", "4404020");
            StrFormat_Dest_Template(s, "StrFormat,BytesToInt,5.18GB,%Dest%", "5561982649");
            StrFormat_Dest_Template(s, "StrFormat,BytesToInt,1.995TB,%Dest%", "2193525697414");
            StrFormat_Dest_Template(s, "StrFormat,BytesToInt,2.017PB,%Dest%", "2270940112101573");
            StrFormat_Dest_Template(s, "StrFormat,BytesToInt,1980PB,%Dest%", "2229281815548395520");
            StrFormat_Dest_Template_Error(s, "StrFormat,BytesToInt,WrongBytes,%Dest%", ErrorCheck.Error);
        }
        #endregion

        #region Hex
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        public void StrFormat_Hex()
        {
            EngineState s = EngineTests.CreateEngineState();

            StrFormat_Dest_Template(s, "StrFormat,Hex,1234,%Dest%", "000004D2");
            StrFormat_Dest_Template(s, "StrFormat,Hex,-1234,%Dest%", "FFFFFB2E");
            StrFormat_Dest_Template_Error(s, "StrFormat,Hex,ABCD,%Dest%", ErrorCheck.Error);
        }
        #endregion

        #region Ceil
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_Ceil()
        {
            EngineState s = EngineTests.CreateEngineState();

            StrFormat_InitDest_Template_Error(s, "StrFormat,Ceil,%Dest%,-10", "4", ErrorCheck.Error);
            StrFormat_InitDest_Template(s, "StrFormat,Ceil,%Dest%,10", "6", "10");
            StrFormat_InitDest_Template(s, "StrFormat,Ceil,%Dest%,800", "799", "800");
            StrFormat_InitDest_Template(s, "StrFormat,Ceil,%Dest%,800", "801", "1600");
            StrFormat_InitDest_Template(s, "StrFormat,Ceil,%Dest%,K", "1000", "1024");
            StrFormat_InitDest_Template(s, "StrFormat,Ceil,%Dest%,K", "1200", "2048");
            StrFormat_InitDest_Template(s, "StrFormat,Ceil,%Dest%,M", "1048570", "1048576");
            StrFormat_InitDest_Template(s, "StrFormat,Ceil,%Dest%,M", "1048580", "2097152");
        }
        #endregion

        #region Floor
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_Floor()
        {
            EngineState s = EngineTests.CreateEngineState();

            StrFormat_InitDest_Template_Error(s, "StrFormat,Floor,%Dest%,-10", "4", ErrorCheck.Error);
            StrFormat_InitDest_Template(s, "StrFormat,Floor,%Dest%,10", "6", "0");
            StrFormat_InitDest_Template(s, "StrFormat,Floor,%Dest%,800", "799", "0");
            StrFormat_InitDest_Template(s, "StrFormat,Floor,%Dest%,800", "801", "800");
            StrFormat_InitDest_Template(s, "StrFormat,Floor,%Dest%,K", "1000", "0");
            StrFormat_InitDest_Template(s, "StrFormat,Floor,%Dest%,K", "1200", "1024");
            StrFormat_InitDest_Template(s, "StrFormat,Floor,%Dest%,M", "1048570", "0");
            StrFormat_InitDest_Template(s, "StrFormat,Floor,%Dest%,M", "1048580", "1048576");
        }
        #endregion

        #region Round
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_Round()
        {
            EngineState s = EngineTests.CreateEngineState();

            StrFormat_InitDest_Template(s, "StrFormat,Round,%Dest%,10", "4", "0");
            StrFormat_InitDest_Template(s, "StrFormat,Round,%Dest%,10", "6", "10");
            StrFormat_InitDest_Template(s, "StrFormat,Round,%Dest%,800", "350", "0");
            StrFormat_InitDest_Template(s, "StrFormat,Round,%Dest%,800", "450", "800");
            StrFormat_InitDest_Template(s, "StrFormat,Round,%Dest%,K", "500", "0");
            StrFormat_InitDest_Template(s, "StrFormat,Round,%Dest%,K", "600", "1024");
            StrFormat_InitDest_Template(s, "StrFormat,Round,%Dest%,M", "524286", "0");
            StrFormat_InitDest_Template(s, "StrFormat,Round,%Dest%,M", "524290", "1048576");
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
            SectionAddress addr = EngineTests.DummySectionAddress();
            CodeCommand cmd = CodeParser.ParseRawLine(rawCode, addr);

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_StrFormat));
            CodeInfo_StrFormat info = cmd.Info as CodeInfo_StrFormat;

            Debug.Assert(info.SubInfo.GetType() == typeof(StrFormatInfo_Date));
            StrFormatInfo_Date subInfo = info.SubInfo as StrFormatInfo_Date;

            Assert.IsTrue(subInfo.FormatString.Equals("yyyy-MM-dd_HH:mm:ss.fff", StringComparison.Ordinal));
        }

        public void StrFormat_Date_2()
        {
            string rawCode = "StrFormat,DATE,#9,yyyymmddhhnnsszzz";
            SectionAddress addr = EngineTests.DummySectionAddress();
            CodeCommand cmd = CodeParser.ParseRawLine(rawCode, addr);

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_StrFormat));
            CodeInfo_StrFormat info = cmd.Info as CodeInfo_StrFormat;

            Debug.Assert(info.SubInfo.GetType() == typeof(StrFormatInfo_Date));
            StrFormatInfo_Date subInfo = info.SubInfo as StrFormatInfo_Date;

            Assert.IsTrue(subInfo.FormatString.Equals("yyyyMMddHHmmssfff", StringComparison.Ordinal));
        }

        public void StrFormat_Date_3()
        {
            string rawCode = "StrFormat,Date,%Dest%,yyy-mm-dd_hh:nn:ss.zzz";
            SectionAddress addr = EngineTests.DummySectionAddress();
            try
            {
                CodeCommand cmd = CodeParser.ParseRawLine(rawCode, addr);
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
            SectionAddress addr = EngineTests.DummySectionAddress();
            try
            {
                CodeCommand cmd = CodeParser.ParseRawLine(rawCode, addr);
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
            EngineState s = EngineTests.CreateEngineState();

            StrFormat_Dest_Template(s, @"StrFormat,FileName,C:\Windows\System32\notepad.exe,%Dest%", "notepad.exe");
            StrFormat_Dest_Template(s, @"StrFormat,FileName,C:\Windows\System32\,%Dest%", string.Empty);
            StrFormat_Dest_Template(s, @"StrFormat,FileName,,%Dest%", string.Empty);
            StrFormat_Dest_Template(s, @"StrFormat,FileName,https://github.com/ied206/PEBakery.git,%Dest%", "PEBakery.git");
        }
        #endregion

        #region DirPath
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_DirPath()
        {
            EngineState s = EngineTests.CreateEngineState();

            StrFormat_Dest_Template(s, @"StrFormat,DirPath,C:\Windows\System32\notepad.exe,%Dest%", @"C:\Windows\System32");
            StrFormat_Dest_Template(s, @"StrFormat,DirPath,C:\Windows\System32,%Dest%", @"C:\Windows");
            StrFormat_Dest_Template(s, @"StrFormat,DirPath,,%Dest%", string.Empty);
            StrFormat_Dest_Template(s, @"StrFormat,DirPath,https://github.com/ied206/PEBakery.git,%Dest%", "https://github.com/ied206");
            StrFormat_Dest_Template_Error(s, @"StrFormat,DirPath,https://github.com/ied206\PEBakery.git,%Dest%", ErrorCheck.Error);
        }
        #endregion

        #region Path
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_Path()
        {
            EngineState s = EngineTests.CreateEngineState();

            StrFormat_Dest_Template(s, @"StrFormat,Path,C:\Windows\System32\notepad.exe,%Dest%", @"C:\Windows\System32\");
            StrFormat_Dest_Template(s, @"StrFormat,Path,C:\Windows\System32,%Dest%", @"C:\Windows\");
            StrFormat_Dest_Template(s, @"StrFormat,Path,,%Dest%", string.Empty);
            StrFormat_Dest_Template(s, @"StrFormat,Path,https://github.com/ied206/PEBakery.git,%Dest%", "https://github.com/ied206/");
            StrFormat_Dest_Template_Error(s, @"StrFormat,Path,https://github.com/ied206\PEBakery.git,%Dest%", ErrorCheck.Error);
        }
        #endregion

        #region Ext
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_Ext()
        {
            EngineState s = EngineTests.CreateEngineState();

            StrFormat_Dest_Template(s, @"StrFormat,Ext,C:\Windows\System32\notepad.exe,%Dest%", @".exe");
            StrFormat_Dest_Template(s, @"StrFormat,Ext,C:\Windows\System32\,%Dest%", string.Empty);
            StrFormat_Dest_Template(s, @"StrFormat,Ext,,%Dest%", string.Empty);
            StrFormat_Dest_Template(s, @"StrFormat,Ext,https://github.com/ied206/PEBakery.git,%Dest%", ".git");
            StrFormat_Dest_Template(s, @"StrFormat,Ext,https://github.com/ied206/PEBakery,%Dest%", string.Empty);
        }
        #endregion

        #region PathCombine
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_PathCombine()
        {
            EngineState s = EngineTests.CreateEngineState();

            StrFormat_Dest_Template(s, @"StrFormat,PathCombine,C:\Windows\System32,notepad.exe,%Dest%", @"C:\Windows\System32\notepad.exe");
            StrFormat_Dest_Template(s, @"StrFormat,PathCombine,C:\Windows,System32\notepad.exe,%Dest%", @"C:\Windows\System32\notepad.exe");
            StrFormat_Dest_Template(s, @"StrFormat,PathCombine,C:\,Windows\System32\notepad.exe,%Dest%", @"C:\Windows\System32\notepad.exe");
            StrFormat_Dest_Template(s, @"StrFormat,PathCombine,C:,Windows\System32\notepad.exe,%Dest%", @"C:\Windows\System32\notepad.exe");

            StrFormat_Dest_Template(s, @"StrFormat,PathCombine,D:\Joveler,Korea,%Dest%", @"D:\Joveler\Korea");
        }
        #endregion

        #region Inc
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_Inc()
        {
            EngineState s = EngineTests.CreateEngineState();

            StrFormat_InitDest_Template(s, @"StrFormat,Inc,%Dest%,20", "15", "35");
            StrFormat_InitDest_Template(s, @"StrFormat,Inc,%Dest%,20", "0x0F", "35");
            // TODO: WB082 returns 'u', does Win10PESE utliize this case?
            StrFormat_InitDest_Template_Error(s, @"StrFormat,Inc,%Dest%,20", "a", ErrorCheck.Error);
            StrFormat_InitDest_Template_Error(s, @"StrFormat,Inc,%Dest%,20", string.Empty, ErrorCheck.Error);
            StrFormat_InitDest_Template(s, @"StrFormat,Inc,%Dest%,20", "-5", "15");
            StrFormat_InitDest_Template(s, @"StrFormat,Inc,%Dest%,-5", "20", "15");
        }
        #endregion

        #region Dec
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_Dec()
        {
            EngineState s = EngineTests.CreateEngineState();

            StrFormat_InitDest_Template(s, @"StrFormat,Dec,%Dest%,20", "15", "-5");
            StrFormat_InitDest_Template(s, @"StrFormat,Dec,%Dest%,0x0F", "20", "5");
            // TODO: WB082 returns 'M', does Win10PESE utliize this case?
            StrFormat_InitDest_Template_Error(s, @"StrFormat,Dec,%Dest%,20", "a", ErrorCheck.Error);
            StrFormat_InitDest_Template_Error(s, @"StrFormat,Dec,%Dest%,20", string.Empty, ErrorCheck.Error);
            StrFormat_InitDest_Template(s, @"StrFormat,Dec,%Dest%,20", "-5", "-25");
            StrFormat_InitDest_Template(s, @"StrFormat,Dec,%Dest%,-5", "20", "25");
        }
        #endregion

        #region Mult
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_Mult()
        {
            EngineState s = EngineTests.CreateEngineState();

            StrFormat_InitDest_Template(s, @"StrFormat,Mult,%Dest%,20", "4", "80");
            StrFormat_InitDest_Template(s, @"StrFormat,Mult,%Dest%,0x0F", "20", "300");
            // WB082 reports error
            StrFormat_InitDest_Template_Error(s, @"StrFormat,Mult,%Dest%,2", "a", ErrorCheck.Error);
            StrFormat_InitDest_Template_Error(s, @"StrFormat,Mult,%Dest%,20", string.Empty, ErrorCheck.Error);
            StrFormat_InitDest_Template(s, @"StrFormat,Mult,%Dest%,20", "-5", "-100");
            StrFormat_InitDest_Template(s, @"StrFormat,Mult,%Dest%,-5", "20", "-100");
        }
        #endregion

        #region Div
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_Div()
        {
            EngineState s = EngineTests.CreateEngineState();

            StrFormat_InitDest_Template(s, @"StrFormat,Div,%Dest%,20", "81", "4");
            StrFormat_InitDest_Template(s, @"StrFormat,Div,%Dest%,0x0F", "20", "1");
            // WB082 reports error
            StrFormat_InitDest_Template_Error(s, @"StrFormat,Div,%Dest%,2", "a", ErrorCheck.Error);
            StrFormat_InitDest_Template_Error(s, @"StrFormat,Div,%Dest%,20", string.Empty, ErrorCheck.Error);
            StrFormat_InitDest_Template(s, @"StrFormat,Div,%Dest%,20", "-25", "-1");
            StrFormat_InitDest_Template(s, @"StrFormat,Div,%Dest%,-5", "20", "-4");
        }
        #endregion

        #region Left
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_Left()
        {
            EngineState s = EngineTests.CreateEngineState();

            StrFormat_Dest_Template(s, @"StrFormat,Left,PEBakery,3,%Dest%", "PEB");
            // StrFormat,Left,%A%,1, -> Causes WB082 access violation
            StrFormat_Dest_Template_Error(s, @"StrFormat,Left,%Dest%,1,", ErrorCheck.ParserError);
            StrFormat_Dest_Template(s, "StrFormat,Left,PE,3,%Dest%", "PE");
        }
        #endregion

        #region Right
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_Right()
        {
            EngineState s = EngineTests.CreateEngineState();

            StrFormat_Dest_Template(s, @"StrFormat,Right,PEBakery,3,%Dest%", "ery");
            // StrFormat,Right,%A%,1, -> Causes WB082 access violation
            StrFormat_Dest_Template_Error(s, "StrFormat,Right,%Dest%,1,", ErrorCheck.ParserError);
            StrFormat_Dest_Template(s, "StrFormat,Right,PE,3,%Dest%", "PE");
        }
        #endregion

        #region SubStr
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_SubStr()
        { // StrFormat,SubStr,<SrcStr>,<StartPos>,<Length>,<DestVar>
            EngineState s = EngineTests.CreateEngineState();

            StrFormat_Dest_Template(s, @"StrFormat,SubStr,PEBakery,3,2,%Dest%", "Ba");
            StrFormat_Dest_Template(s, @"StrFormat,SubStr,PEBakery,4,3,%Dest%", "ake");
            StrFormat_Dest_Template_Error(s, @"StrFormat,SubStr,PEBakery,0,2,%Dest%", ErrorCheck.Error);
            StrFormat_Dest_Template_Error(s, @"StrFormat,SubStr,PEBakery,3,0,%Dest%", ErrorCheck.Error);
            StrFormat_Dest_Template_Error(s, @"StrFormat,SubStr,Joveler,10,2,%Dest%", ErrorCheck.Error);
            StrFormat_Dest_Template_Error(s, @"StrFormat,SubStr,Joveler,3,10,%Dest%", ErrorCheck.Error);
        }
        #endregion

        #region Len
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_Len()
        {
            EngineState s = EngineTests.CreateEngineState();

            StrFormat_Dest_Template(s, @"StrFormat,Len,PEBakery,%Dest%", "8");
            StrFormat_Dest_Template(s, @"StrFormat,Len,,%Dest%", "0");
        }
        #endregion

        #region LTrim
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_LTrim()
        {
            EngineState s = EngineTests.CreateEngineState();

            StrFormat_Dest_Template(s, @"StrFormat,LTrim,PEBakery,3,%Dest%", "akery");
            StrFormat_Dest_Template(s, @"StrFormat,LTrim,PEBakery,10,%Dest%", string.Empty);
            StrFormat_Dest_Template(s, @"StrFormat,LTrim,PEBakery,-1,%Dest%", "PEBakery");
            StrFormat_Dest_Template(s, @"StrFormat,LTrim,,3,%Dest%", string.Empty);
        }
        #endregion

        #region RTrim
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_RTrim()
        {
            EngineState s = EngineTests.CreateEngineState();

            StrFormat_Dest_Template(s, @"StrFormat,RTrim,PEBakery,3,%Dest%", "PEBak");
            StrFormat_Dest_Template(s, @"StrFormat,RTrim,PEBakery,10,%Dest%", string.Empty);
            StrFormat_Dest_Template(s, @"StrFormat,RTrim,PEBakery,-1,%Dest%", "PEBakery");
            StrFormat_Dest_Template(s, @"StrFormat,RTrim,,3,%Dest%", string.Empty);
        }
        #endregion

        #region CTrim
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_CTrim()
        {
            EngineState s = EngineTests.CreateEngineState();

            // In WB082, it returns "-PEBakery-", because WB082 uses only first character
            StrFormat_Dest_Template(s, @"StrFormat,CTrim,_-PEBakery-_,_-,%Dest%", "PEBakery");
            StrFormat_Dest_Template(s, "StrFormat,CTrim, PEBakery ,\" \",%Dest%", "PEBakery");
            // Access violation in WB082
            StrFormat_Dest_Template_Error(s, "StrFormat,CTrim,PEBakery,,%Dest%", ErrorCheck.Error);
        }
        #endregion

        #region NTrim
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_NTrim()
        {
            EngineState s = EngineTests.CreateEngineState();

            StrFormat_Dest_Template(s, @"StrFormat,NTrim,PEBakery100,%Dest%", "PEBakery");
            StrFormat_Dest_Template(s, @"StrFormat,NTrim,PEBakery,%Dest%", "PEBakery");
        }
        #endregion

        #region UCase
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        public void StrFormat_UCase()
        {
            EngineState s = EngineTests.CreateEngineState();

            StrFormat_Dest_Template(s, "StrFormat,UCase,abcDEF,%Dest%", "ABCDEF");
            StrFormat_Dest_Template(s, "StrFormat,UCase,가나다라,%Dest%", "가나다라");
            StrFormat_Dest_Template(s, "StrFormat,UCase,abcDEF가나다라,%Dest%", "ABCDEF가나다라");
        }
        #endregion

        #region LCase
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        public void StrFormat_LCase()
        {
            EngineState s = EngineTests.CreateEngineState();

            StrFormat_Dest_Template(s, "StrFormat,LCase,abcDEF,%Dest%", "abcdef");
            StrFormat_Dest_Template(s, "StrFormat,LCase,가나다라,%Dest%", "가나다라");
            StrFormat_Dest_Template(s, "StrFormat,LCase,abcDEF가나다라,%Dest%", "abcdef가나다라");
        }
        #endregion

        #region Pos
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_Pos()
        {
            EngineState s = EngineTests.CreateEngineState();

            StrFormat_Dest_Template(s, @"StrFormat,Pos,SouthKorea,thK,%Dest%", "4");
            StrFormat_Dest_Template(s, @"StrFormat,Pos,SouthKorea,hk,%Dest%", "5");
            StrFormat_Dest_Template(s, @"StrFormat,Pos,SouthKorea,abc,%Dest%", "0");
            StrFormat_Dest_Template(s, @"StrFormat,Pos,SouthKorea,,%Dest%", "0");
        }
        #endregion

        #region PosX
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_PosX()
        {
            EngineState s = EngineTests.CreateEngineState();

            StrFormat_Dest_Template(s, @"StrFormat,PosX,SouthKorea,thK,%Dest%", "4");
            StrFormat_Dest_Template(s, @"StrFormat,PosX,SouthKorea,thk,%Dest%", "0");
            StrFormat_Dest_Template(s, @"StrFormat,PosX,SouthKorea,abc,%Dest%", "0");
            StrFormat_Dest_Template(s, @"StrFormat,PosX,SouthKorea,,%Dest%", "0");
        }
        #endregion

        #region Replace
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_Replace()
        {
            EngineState s = EngineTests.CreateEngineState();

            StrFormat_Dest_Template(s, @"StrFormat,Replace,PEBakery,Bake,Pake,%Dest%", "PEPakery");
            StrFormat_Dest_Template(s, @"StrFormat,Replace,PEBakery,bake,Pake,%Dest%", "PEPakery");
            StrFormat_Dest_Template(s, @"StrFormat,Replace,PEBakery,_,__,%Dest%", "PEBakery");
            StrFormat_Dest_Template(s, @"StrFormat,Replace,SouthKorea,,_,%Dest%", "SouthKorea");
        }
        #endregion

        #region ReplaceX
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_ReplaceX()
        {
            EngineState s = EngineTests.CreateEngineState();

            StrFormat_Dest_Template(s, @"StrFormat,ReplaceX,PEBakery,Bake,Pake,%Dest%", "PEPakery");
            StrFormat_Dest_Template(s, @"StrFormat,ReplaceX,PEBakery,bake,Pake,%Dest%", "PEBakery");
            StrFormat_Dest_Template(s, @"StrFormat,ReplaceX,PEBakery,_,__,%Dest%", "PEBakery");
            StrFormat_Dest_Template(s, @"StrFormat,ReplaceX,SouthKorea,,_,%Dest%", "SouthKorea");
        }
        #endregion

        #region Split
        [TestCategory("Command")]
        [TestCategory("CommandString")]
        [TestMethod]
        public void StrFormat_Split()
        {
            EngineState s = EngineTests.CreateEngineState();

            StrFormat_Dest_Template(s, @"StrFormat,Split,A/B/C/D/E/F,/,0,%Dest%", "6");
            StrFormat_Dest_Template(s, @"StrFormat,Split,A/B/C/D/E/F,/,2,%Dest%", "B");
            StrFormat_Dest_Template(s, @"StrFormat,Split,A/B/C/D/E/F,/,5,%Dest%", "E");
            StrFormat_InitDest_Template(s, @"StrFormat,Split,A/B/C/D/E/F,/,7,%Dest%", string.Empty, string.Empty);
        }
        #endregion

        #region Utility
        public void StrFormat_InitDest_Template(EngineState s, string rawCode, string initStr, string destStr)
        {
            s.Variables["Dest"] = initStr;
            EngineTests.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Success);

            string dest = s.Variables["Dest"];
            Assert.IsTrue(dest.Equals(destStr, StringComparison.Ordinal));
        }

        public void StrFormat_InitDest_Template_Error(EngineState s, string rawCode, string initStr, ErrorCheck check)
        {
            s.Variables["Dest"] = initStr;
            EngineTests.Eval(s, rawCode, CodeType.StrFormat, check);
        }

        public void StrFormat_Dest_Template(EngineState s, string rawCode, string destStr)
        {
            EngineTests.Eval(s, rawCode, CodeType.StrFormat, ErrorCheck.Success);

            if (destStr != null)
            {
                string dest = s.Variables["Dest"];
                Assert.IsTrue(dest.Equals(destStr, StringComparison.Ordinal));
            }
        }

        public void StrFormat_Dest_Template_Error(EngineState s, string rawCode, ErrorCheck check)
        {
            EngineTests.Eval(s, rawCode, CodeType.StrFormat, check);
        }
        #endregion
    }
}


