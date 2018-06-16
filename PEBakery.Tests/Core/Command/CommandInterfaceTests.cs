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

using System;
using System.Collections.Generic;
using PEBakery.Core;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.IniLib;
using PEBakery.Helper;
// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local

namespace PEBakery.Tests.Core.Command
{
    [TestClass]
    public class CommandInterfaceTests
    {
        #region Const String
        private const string TestSuiteInterface = "Interface";
        #endregion

        #region Visible
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandInterface")]
        public void Visible()
        {
            string srcFile = Path.Combine(EngineTests.Project.ProjectDir, TestSuiteInterface, "ReadInterface.script");
            string scriptFile = Path.GetTempFileName();

            try
            {
                void SingleTemplate(string rawCode, string key, string compStr, ErrorCheck check = ErrorCheck.Success)
                {
                    File.Copy(srcFile, scriptFile, true);

                    EngineState s = EngineTests.CreateEngineState();
                    Script sc = s.Project.LoadScriptMonkeyPatch(scriptFile);
                    SectionAddress addr = new SectionAddress(sc, sc.GetInterface(out _));
                    try
                    {
                        EngineTests.Eval(s, addr, rawCode, CodeType.Visible, check);
                        if (check == ErrorCheck.Success)
                        {
                            string dest = Ini.ReadKey(scriptFile, "Interface", key);
                            Assert.IsTrue(dest.Equals(compStr, StringComparison.Ordinal));
                        }
                    }
                    finally
                    {
                        if (File.Exists(scriptFile))
                            File.Delete(scriptFile);
                    }
                }

                void OptTemplate(List<string> rawCodes, (string key, string value)[] compTuples, bool optSuccess, ErrorCheck check = ErrorCheck.Success)
                {
                    File.Copy(srcFile, scriptFile, true);

                    EngineState s = EngineTests.CreateEngineState();
                    Script sc = s.Project.LoadScriptMonkeyPatch(scriptFile);
                    SectionAddress addr = new SectionAddress(sc, sc.GetInterface(out _));
                    try
                    {
                        CodeType? opType = optSuccess ? (CodeType?)CodeType.VisibleOp : null;
                        EngineTests.EvalOptLines(s, addr, opType, rawCodes, check);
                        if (check == ErrorCheck.Success)
                        {
                            foreach ((string key, string value) in compTuples)
                            {
                                string dest = Ini.ReadKey(scriptFile, "Interface", key);
                                Assert.IsTrue(dest.Equals(value, StringComparison.Ordinal));
                            }
                        }
                    }
                    finally
                    {
                        if (File.Exists(scriptFile))
                            File.Delete(scriptFile);
                    }
                }

                SingleTemplate(@"Visible,%pTextBox1%,True", @"pTextBox1", @"Display,1,0,20,20,200,21,StringValue");
                SingleTemplate(@"Visible,%pTextBox1%,False", @"pTextBox1", @"Display,0,0,20,20,200,21,StringValue");
                OptTemplate(new List<string>
                {
                    @"Visible,%pTextBox1%,False",
                    @"Visible,%pNumberBox1%,False",
                    @"Visible,%pCheckBox1%,False",
                }, new (string, string)[]
                {
                    ("pTextBox1", @"Display,0,0,20,20,200,21,StringValue"),
                    ("pNumberBox1", @"pNumberBox1,0,2,20,70,40,22,3,0,100,1"),
                    ("pCheckBox1", @"pCheckBox1,0,3,20,100,200,18,True"),
                }, true);
            }
            finally
            {
                if (File.Exists(scriptFile))
                    File.Delete(scriptFile);
            }
        }
        #endregion

        #region ReadInterface
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandInterface")]
        public void ReadInterface()
        {
            EngineState s = EngineTests.CreateEngineState();
            string scriptFile = Path.Combine("%ProjectDir%", TestSuiteInterface, "ReadInterface.script");

            void Template(string rawCode, string comp, ErrorCheck check = ErrorCheck.Success)
            {
                EngineTests.Eval(s, rawCode, CodeType.ReadInterface, check);
                if (check == ErrorCheck.Success)
                {
                    string dest = s.Variables["Dest"];
                    Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
                }
            }

            // 0 - TextBox
            Template($@"ReadInterface,Text,{scriptFile},Interface,pTextBox1,%Dest%", @"Display");
            Template($@"ReadInterface,Visible,{scriptFile},Interface,pTextBox1,%Dest%", @"True");
            Template($@"ReadInterface,PosX,{scriptFile},Interface,pTextBox1,%Dest%", @"20");
            Template($@"ReadInterface,PosY,{scriptFile},Interface,pTextBox1,%Dest%", @"20");
            Template($@"ReadInterface,Width,{scriptFile},Interface,pTextBox1,%Dest%", @"200");
            Template($@"ReadInterface,Height,{scriptFile},Interface,pTextBox1,%Dest%", @"21");
            Template($@"ReadInterface,Value,{scriptFile},Interface,pTextBox1,%Dest%", @"StringValue");
            Template($@"ReadInterface,ToolTip,{scriptFile},Interface,pTextBox1,%Dest%", string.Empty);

            // 1 - TextLabel
            Template($@"ReadInterface,Text,{scriptFile},Interface,pTextLabel1,%Dest%", @"Display");
            Template($@"ReadInterface,Visible,{scriptFile},Interface,pTextLabel1,%Dest%", @"True");
            Template($@"ReadInterface,PosX,{scriptFile},Interface,pTextLabel1,%Dest%", @"20");
            Template($@"ReadInterface,PosY,{scriptFile},Interface,pTextLabel1,%Dest%", @"50");
            Template($@"ReadInterface,Width,{scriptFile},Interface,pTextLabel1,%Dest%", @"230");
            Template($@"ReadInterface,Height,{scriptFile},Interface,pTextLabel1,%Dest%", @"18");
            Template($@"ReadInterface,Value,{scriptFile},Interface,pTextLabel1,%Dest%", null, ErrorCheck.Error);
            Template($@"ReadInterface,ToolTip,{scriptFile},Interface,pTextLabel1,%Dest%", string.Empty);
            Template($@"ReadInterface,FontSize,{scriptFile},Interface,pTextLabel1,%Dest%", @"8");
            Template( $@"ReadInterface,FontWeight,{scriptFile},Interface,pTextLabel1,%Dest%", @"Normal");

            // 2 - NumberBox
            Template($@"ReadInterface,Text,{scriptFile},Interface,pNumberBox1,%Dest%", @"pNumberBox1");
            Template($@"ReadInterface,Visible,{scriptFile},Interface,pNumberBox1,%Dest%", @"True");
            Template($@"ReadInterface,PosX,{scriptFile},Interface,pNumberBox1,%Dest%", @"20");
            Template($@"ReadInterface,PosY,{scriptFile},Interface,pNumberBox1,%Dest%", @"70");
            Template($@"ReadInterface,Width,{scriptFile},Interface,pNumberBox1,%Dest%", @"40");
            Template($@"ReadInterface,Height,{scriptFile},Interface,pNumberBox1,%Dest%", @"22");
            Template($@"ReadInterface,Value,{scriptFile},Interface,pNumberBox1,%Dest%", @"3");
            Template($@"ReadInterface,ToolTip,{scriptFile},Interface,pNumberBox1,%Dest%", string.Empty);
            Template($@"ReadInterface,NumberMin,{scriptFile},Interface,pNumberBox1,%Dest%", @"0");
            Template($@"ReadInterface,NumberMax,{scriptFile},Interface,pNumberBox1,%Dest%", @"100");
            Template($@"ReadInterface,NumberTick,{scriptFile},Interface,pNumberBox1,%Dest%", @"1");

            // 3 - CheckBox
            Template($@"ReadInterface,Text,{scriptFile},Interface,pCheckBox1,%Dest%", @"pCheckBox1");
            Template($@"ReadInterface,Visible,{scriptFile},Interface,pCheckBox1,%Dest%", @"True");
            Template($@"ReadInterface,PosX,{scriptFile},Interface,pCheckBox1,%Dest%", @"20");
            Template($@"ReadInterface,PosY,{scriptFile},Interface,pCheckBox1,%Dest%", @"100");
            Template($@"ReadInterface,Width,{scriptFile},Interface,pCheckBox1,%Dest%", @"200");
            Template($@"ReadInterface,Height,{scriptFile},Interface,pCheckBox1,%Dest%", @"18");
            Template($@"ReadInterface,Value,{scriptFile},Interface,pCheckBox1,%Dest%", @"True");
            Template($@"ReadInterface,ToolTip,{scriptFile},Interface,pCheckBox1,%Dest%", string.Empty);
            Template($@"ReadInterface,SectionName,{scriptFile},Interface,pCheckBox1,%Dest%", string.Empty);
            Template($@"ReadInterface,HideProgress,{scriptFile},Interface,pCheckBox1,%Dest%", "None");

            // 4 - ComboBox
            Template($@"ReadInterface,Text,{scriptFile},Interface,pComboBox1,%Dest%", @"A");
            Template($@"ReadInterface,Visible,{scriptFile},Interface,pComboBox1,%Dest%", @"True");
            Template($@"ReadInterface,PosX,{scriptFile},Interface,pComboBox1,%Dest%", @"20");
            Template($@"ReadInterface,PosY,{scriptFile},Interface,pComboBox1,%Dest%", @"130");
            Template($@"ReadInterface,Width,{scriptFile},Interface,pComboBox1,%Dest%", @"150");
            Template($@"ReadInterface,Height,{scriptFile},Interface,pComboBox1,%Dest%", @"21");
            Template($@"ReadInterface,Value,{scriptFile},Interface,pComboBox1,%Dest%", @"A");
            Template($@"ReadInterface,ToolTip,{scriptFile},Interface,pComboBox1,%Dest%", string.Empty);
            Template($@"ReadInterface,Items,{scriptFile},Interface,pComboBox1,%Dest%", @"A|B|C|D");
            Template($@"ReadInterface,SectionName,{scriptFile},Interface,pComboBox1,%Dest%", string.Empty);
            Template($@"ReadInterface,HideProgress,{scriptFile},Interface,pComboBox1,%Dest%", "None");
            
            // 5 - Image
            Template($@"ReadInterface,Text,{scriptFile},Interface,pImage1,%Dest%", @"Logo.jpg");
            Template($@"ReadInterface,Visible,{scriptFile},Interface,pImage1,%Dest%", @"True");
            Template($@"ReadInterface,PosX,{scriptFile},Interface,pImage1,%Dest%", @"20");
            Template($@"ReadInterface,PosY,{scriptFile},Interface,pImage1,%Dest%", @"230");
            Template($@"ReadInterface,Width,{scriptFile},Interface,pImage1,%Dest%", @"40");
            Template($@"ReadInterface,Height,{scriptFile},Interface,pImage1,%Dest%", @"40");
            Template($@"ReadInterface,Value,{scriptFile},Interface,pImage1,%Dest%", null, ErrorCheck.Error);
            Template($@"ReadInterface,ToolTip,{scriptFile},Interface,pImage1,%Dest%", string.Empty);
            Template($@"ReadInterface,Url,{scriptFile},Interface,pImage1,%Dest%", string.Empty);

            // 6 - TextFile
            Template($@"ReadInterface,Text,{scriptFile},Interface,pTextFile1,%Dest%", @"HelpMsg.txt");
            Template($@"ReadInterface,Visible,{scriptFile},Interface,pTextFile1,%Dest%", @"True");
            Template($@"ReadInterface,PosX,{scriptFile},Interface,pTextFile1,%Dest%", @"240");
            Template($@"ReadInterface,PosY,{scriptFile},Interface,pTextFile1,%Dest%", @"20");
            Template($@"ReadInterface,Width,{scriptFile},Interface,pTextFile1,%Dest%", @"200");
            Template($@"ReadInterface,Height,{scriptFile},Interface,pTextFile1,%Dest%", @"86");
            Template($@"ReadInterface,Value,{scriptFile},Interface,pTextFile1,%Dest%", null, ErrorCheck.Error);
            Template($@"ReadInterface,ToolTip,{scriptFile},Interface,pTextFile1,%Dest%", string.Empty);

            // 8 - Button
            Template($@"ReadInterface,Text,{scriptFile},Interface,pButton1,%Dest%", @"ShowProgress");
            Template($@"ReadInterface,Text,{scriptFile},Interface,pButton2,%Dest%", @"HideProgress");
            Template($@"ReadInterface,Visible,{scriptFile},Interface,pButton1,%Dest%", @"True");
            Template($@"ReadInterface,PosX,{scriptFile},Interface,pButton1,%Dest%", @"240");
            Template($@"ReadInterface,PosY,{scriptFile},Interface,pButton1,%Dest%", @"115");
            Template($@"ReadInterface,Width,{scriptFile},Interface,pButton1,%Dest%", @"80");
            Template($@"ReadInterface,Height,{scriptFile},Interface,pButton1,%Dest%", @"25");
            Template($@"ReadInterface,Value,{scriptFile},Interface,pButton1,%Dest%", null, ErrorCheck.Error);
            Template($@"ReadInterface,ToolTip,{scriptFile},Interface,pButton1,%Dest%", string.Empty);
            Template($@"ReadInterface,SectionName,{scriptFile},Interface,pButton1,%Dest%", @"Hello");
            Template($@"ReadInterface,HideProgress,{scriptFile},Interface,pButton1,%Dest%", @"False");

            // 10 - WebLabel
            Template($@"ReadInterface,Text,{scriptFile},Interface,pWebLabel1,%Dest%", @"GitHub");
            Template($@"ReadInterface,Visible,{scriptFile},Interface,pWebLabel1,%Dest%", @"True");
            Template($@"ReadInterface,PosX,{scriptFile},Interface,pWebLabel1,%Dest%", @"250");
            Template($@"ReadInterface,PosY,{scriptFile},Interface,pWebLabel1,%Dest%", @"160");
            Template($@"ReadInterface,Width,{scriptFile},Interface,pWebLabel1,%Dest%", @"32");
            Template($@"ReadInterface,Height,{scriptFile},Interface,pWebLabel1,%Dest%", @"18");
            Template($@"ReadInterface,Value,{scriptFile},Interface,pWebLabel1,%Dest%", null, ErrorCheck.Error);
            Template($@"ReadInterface,ToolTip,{scriptFile},Interface,pWebLabel1,%Dest%", string.Empty);
            Template($@"ReadInterface,Url,{scriptFile},Interface,pWebLabel1,%Dest%", @"https://github.com/pebakery/pebakery");

            // 11 - RadioButton
            Template($@"ReadInterface,Text,{scriptFile},Interface,pRadioButton1,%Dest%", @"pRadioButton1");
            Template($@"ReadInterface,Visible,{scriptFile},Interface,pRadioButton1,%Dest%", @"True");
            Template($@"ReadInterface,PosX,{scriptFile},Interface,pRadioButton1,%Dest%", @"250");
            Template($@"ReadInterface,PosY,{scriptFile},Interface,pRadioButton1,%Dest%", @"180");
            Template($@"ReadInterface,Width,{scriptFile},Interface,pRadioButton1,%Dest%", @"100");
            Template($@"ReadInterface,Height,{scriptFile},Interface,pRadioButton1,%Dest%", @"20");
            Template($@"ReadInterface,Value,{scriptFile},Interface,pRadioButton1,%Dest%", @"False");
            Template($@"ReadInterface,ToolTip,{scriptFile},Interface,pRadioButton1,%Dest%", string.Empty);
            Template($@"ReadInterface,SectionName,{scriptFile},Interface,pRadioButton1,%Dest%", string.Empty);
            Template($@"ReadInterface,HideProgress,{scriptFile},Interface,pRadioButton1,%Dest%", "None");

            // 12 - Bevel
            Template($@"ReadInterface,Text,{scriptFile},Interface,pBevel1,%Dest%", @"pBevel1");
            Template($@"ReadInterface,Visible,{scriptFile},Interface,pBevel1,%Dest%", @"True");
            Template($@"ReadInterface,PosX,{scriptFile},Interface,pBevel1,%Dest%", @"240");
            Template($@"ReadInterface,PosY,{scriptFile},Interface,pBevel1,%Dest%", @"150");
            Template($@"ReadInterface,Width,{scriptFile},Interface,pBevel1,%Dest%", @"235");
            Template($@"ReadInterface,Height,{scriptFile},Interface,pBevel1,%Dest%", @"60");
            Template($@"ReadInterface,Value,{scriptFile},Interface,pBevel1,%Dest%", null, ErrorCheck.Error);
            Template($@"ReadInterface,ToolTip,{scriptFile},Interface,pBevel1,%Dest%", string.Empty);

            // 13 - FileBox
            Template($@"ReadInterface,Text,{scriptFile},Interface,pFileBox1,%Dest%", @"C:\Windows\notepad.exe");
            Template($@"ReadInterface,Visible,{scriptFile},Interface,pFileBox1,%Dest%", @"True");
            Template($@"ReadInterface,PosX,{scriptFile},Interface,pFileBox1,%Dest%", @"240");
            Template($@"ReadInterface,PosY,{scriptFile},Interface,pFileBox1,%Dest%", @"230");
            Template($@"ReadInterface,Width,{scriptFile},Interface,pFileBox1,%Dest%", @"200");
            Template($@"ReadInterface,Height,{scriptFile},Interface,pFileBox1,%Dest%", @"20");
            Template($@"ReadInterface,Value,{scriptFile},Interface,pFileBox1,%Dest%", @"C:\Windows\notepad.exe");
            Template($@"ReadInterface,ToolTip,{scriptFile},Interface,pFileBox1,%Dest%", string.Empty);

            Template($@"ReadInterface,Text,{scriptFile},Interface,pFileBox2,%Dest%", @"E:\WinPE\");
            Template($@"ReadInterface,Visible,{scriptFile},Interface,pFileBox2,%Dest%", @"True");
            Template($@"ReadInterface,PosX,{scriptFile},Interface,pFileBox2,%Dest%", @"240");
            Template($@"ReadInterface,PosY,{scriptFile},Interface,pFileBox2,%Dest%", @"260");
            Template($@"ReadInterface,Width,{scriptFile},Interface,pFileBox2,%Dest%", @"200");
            Template($@"ReadInterface,Height,{scriptFile},Interface,pFileBox2,%Dest%", @"20");
            Template($@"ReadInterface,Value,{scriptFile},Interface,pFileBox2,%Dest%", @"E:\WinPE\");
            Template($@"ReadInterface,ToolTip,{scriptFile},Interface,pFileBox2,%Dest%", string.Empty);

            // 14 - RadioGroup
            Template($@"ReadInterface,Text,{scriptFile},Interface,pRadioGroup1,%Dest%", @"pRadioGroup1");
            Template($@"ReadInterface,Visible,{scriptFile},Interface,pRadioGroup1,%Dest%", @"True");
            Template($@"ReadInterface,PosX,{scriptFile},Interface,pRadioGroup1,%Dest%", @"20");
            Template($@"ReadInterface,PosY,{scriptFile},Interface,pRadioGroup1,%Dest%", @"160");
            Template($@"ReadInterface,Width,{scriptFile},Interface,pRadioGroup1,%Dest%", @"150");
            Template($@"ReadInterface,Height,{scriptFile},Interface,pRadioGroup1,%Dest%", @"60");
            Template($@"ReadInterface,Value,{scriptFile},Interface,pRadioGroup1,%Dest%", @"0");
            Template($@"ReadInterface,Items,{scriptFile},Interface,pRadioGroup1,%Dest%", @"Option1|Option2|Option3");
            Template($@"ReadInterface,SectionName,{scriptFile},Interface,pRadioGroup1,%Dest%", string.Empty);
            Template($@"ReadInterface,HideProgress,{scriptFile},Interface,pRadioGroup1,%Dest%", "None");

            // Visible - False
            Template($@"ReadInterface,Visible,{scriptFile},Interface,pTextLabel2,%Dest%", @"False");

            // ToolTip
            Template($@"ReadInterface,ToolTip,{scriptFile},Interface,pTextLabel3,%Dest%", @"PEBakery");
        }
        #endregion

        #region WriteInterface
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandInterface")]
        public void WriteInterface()
        {
            EngineState s = EngineTests.CreateEngineState();
            string scriptFile = Path.Combine("%ProjectTemp%", "WriteInterface.script");

            void Template(string rawCode, string key, string comp, ErrorCheck check = ErrorCheck.Success)
            {
                string srcFile = StringEscaper.Preprocess(s, Path.Combine("%ProjectDir%", TestSuiteInterface, "ReadInterface.script"));
                string destFile = StringEscaper.Preprocess(s, Path.Combine("%ProjectTemp%", "WriteInterface.script"));

                File.Copy(srcFile, destFile, true);
                try
                {
                    EngineTests.Eval(s, rawCode, CodeType.WriteInterface, check);
                    if (check == ErrorCheck.Success)
                    {
                        string dest = Ini.ReadKey(destFile, "Interface", key);
                        Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
                    }
                }
                finally
                {
                    if (File.Exists(destFile))
                        File.Delete(destFile);
                }
            }

            // Common
            Template($@"WriteInterface,Text,{scriptFile},Interface,pTextBox1,PEBakery", @"pTextBox1",
                @"PEBakery,1,0,20,20,200,21,StringValue");
            Template($@"WriteInterface,Visible,{scriptFile},Interface,pTextBox1,False", @"pTextBox1",
                @"Display,0,0,20,20,200,21,StringValue");
            Template($@"WriteInterface,PosX,{scriptFile},Interface,pTextBox1,30", @"pTextBox1",
                @"Display,1,0,30,20,200,21,StringValue");
            Template($@"WriteInterface,PosY,{scriptFile},Interface,pTextBox1,30", @"pTextBox1",
                @"Display,1,0,20,30,200,21,StringValue");
            Template($@"WriteInterface,Width,{scriptFile},Interface,pTextBox1,30", @"pTextBox1",
                @"Display,1,0,20,20,30,21,StringValue");
            Template($@"WriteInterface,Height,{scriptFile},Interface,pTextBox1,10", @"pTextBox1",
                @"Display,1,0,20,20,200,10,StringValue");
            Template($@"WriteInterface,ToolTip,{scriptFile},Interface,pTextBox1,PEBakery", @"pTextBox1",
                @"Display,1,0,20,20,200,21,StringValue,__PEBakery");

            // 0 - TextBox
            Template($@"WriteInterface,Value,{scriptFile},Interface,pTextBox1,PEBakery", @"pTextBox1",
                @"Display,1,0,20,20,200,21,PEBakery");

            // 1 - TextLabel
            Template($@"WriteInterface,Value,{scriptFile},Interface,pTextLabel1,PEBakery", @"pTextLabel1", 
                null, ErrorCheck.Error);
            Template($@"WriteInterface,FontSize,{scriptFile},Interface,pTextLabel1,10", @"pTextLabel1",
                @"Display,1,1,20,50,230,18,10,Normal");
            Template($@"WriteInterface,FontWeight,{scriptFile},Interface,pTextLabel1,Bold", @"pTextLabel1",
                @"Display,1,1,20,50,230,18,8,Bold");
            Template($@"WriteInterface,FontWeight,{scriptFile},Interface,pTextLabel1,Error", @"pTextLabel1",
                null, ErrorCheck.Error);

            // 2 - NumberBox
            Template($@"WriteInterface,Value,{scriptFile},Interface,pNumberBox1,2", @"pNumberBox1",
                @"pNumberBox1,1,2,20,70,40,22,2,0,100,1");
            Template($@"WriteInterface,Value,{scriptFile},Interface,pNumberBox1,200", @"pNumberBox1",
                null, ErrorCheck.Error);
            Template($@"WriteInterface,Value,{scriptFile},Interface,pNumberBox1,Str", @"pNumberBox1",
                null, ErrorCheck.Error);
            Template($@"WriteInterface,NumberMin,{scriptFile},Interface,pNumberBox1,10", @"pNumberBox1",
                @"pNumberBox1,1,2,20,70,40,22,10,10,100,1");
            Template($@"WriteInterface,NumberMax,{scriptFile},Interface,pNumberBox1,1", @"pNumberBox1",
                @"pNumberBox1,1,2,20,70,40,22,1,0,1,1");
            Template($@"WriteInterface,NumberTick,{scriptFile},Interface,pNumberBox1,5", @"pNumberBox1",
                @"pNumberBox1,1,2,20,70,40,22,3,0,100,5");
            Template($@"WriteInterface,NumberMin,{scriptFile},Interface,pNumberBox1,Error", @"pNumberBox1",
                null, ErrorCheck.Error);

            // 3 - CheckBox
            Template($@"WriteInterface,Value,{scriptFile},Interface,pCheckBox1,False", @"pCheckBox1",
                @"pCheckBox1,1,3,20,100,200,18,False");
            Template($@"WriteInterface,SectionName,{scriptFile},Interface,pCheckBox1,Hello", @"pCheckBox1",
                @"pCheckBox1,1,3,20,100,200,18,True,_Hello_,False");
            Template($@"WriteInterface,HideProgress,{scriptFile},Interface,pCheckBox1,None", @"pCheckBox1",
                @"pCheckBox1,1,3,20,100,200,18,True");
            Template($@"WriteInterface,HideProgress,{scriptFile},Interface,pCheckBox1,True", @"pCheckBox1",
                null, ErrorCheck.Error);

            // 4 - ComboBox
            Template($@"WriteInterface,Value,{scriptFile},Interface,pComboBox1,B", @"pComboBox1",
                @"B,1,4,20,130,150,21,A,B,C,D");
            Template($@"WriteInterface,Value,{scriptFile},Interface,pComboBox1,E", @"pComboBox1",
                null, ErrorCheck.Error);
            Template($@"WriteInterface,Items,{scriptFile},Interface,pComboBox1,X|Y|Z", @"pComboBox1",
                @"X,1,4,20,130,150,21,X,Y,Z");
            Template($@"WriteInterface,SectionName,{scriptFile},Interface,pComboBox1,Hello", @"pComboBox1",
                @"A,1,4,20,130,150,21,A,B,C,D,_Hello_,False");
            Template($@"WriteInterface,HideProgress,{scriptFile},Interface,pComboBox1,None", @"pComboBox1",
                @"A,1,4,20,130,150,21,A,B,C,D");
            Template($@"WriteInterface,HideProgress,{scriptFile},Interface,pComboBox1,True", @"pComboBox1",
                null, ErrorCheck.Error);

            // 5 - Image
            Template($@"WriteInterface,Value,{scriptFile},Interface,pImage1,PEBakery", @"pImage1", 
                null, ErrorCheck.Error);
            Template($@"WriteInterface,Url,{scriptFile},Interface,pImage1,https://github.com/pebakery/pebakery", @"pImage1",
                @"Logo.jpg,1,5,20,230,40,40,https://github.com/pebakery/pebakery");

            // 6 - TextFile
            Template($@"WriteInterface,Value,{scriptFile},Interface,pTextFile1,PEBakery", @"pTextFile1",
                null, ErrorCheck.Error);

            // 8 - Button
            Template($@"WriteInterface,Value,{scriptFile},Interface,pButton1,PEBakery", @"pButton1", 
                null, ErrorCheck.Error);
            Template($@"WriteInterface,SectionName,{scriptFile},Interface,pButton1,World", @"pButton1",
                @"ShowProgress,1,8,240,115,80,25,World,0,False");
            Template($@"WriteInterface,SectionName,{scriptFile},Interface,pButton1,""""", @"pButton1",
                null, ErrorCheck.Error);
            Template($@"WriteInterface,HideProgress,{scriptFile},Interface,pButton1,None", @"pButton1",
                null, ErrorCheck.Error);
            Template($@"WriteInterface,HideProgress,{scriptFile},Interface,pButton1,True", @"pButton1",
                @"ShowProgress,1,8,240,115,80,25,Hello,0,True");

            // 10 - WebLabel
            Template($@"WriteInterface,Value,{scriptFile},Interface,pWebLabel1,PEBakery", @"pWebLabel1",
                null, ErrorCheck.Error);
            Template($@"WriteInterface,Url,{scriptFile},Interface,pWebLabel1,https://github.com/pebakery", @"pWebLabel1",
                @"GitHub,1,10,250,160,32,18,https://github.com/pebakery");

            // 11 - RadioButton
            Template($@"WriteInterface,Value,{scriptFile},Interface,pRadioButton1,True", @"pRadioButton1",
                @"pRadioButton1,1,11,250,180,100,20,True");
            Template($@"WriteInterface,SectionName,{scriptFile},Interface,pRadioButton1,Hello", @"pRadioButton1",
                @"pRadioButton1,1,11,250,180,100,20,False,_Hello_,False");
            Template($@"WriteInterface,HideProgress,{scriptFile},Interface,pRadioButton1,None", @"pRadioButton1",
                @"pRadioButton1,1,11,250,180,100,20,False");
            Template($@"WriteInterface,HideProgress,{scriptFile},Interface,pRadioButton1,True", @"pRadioButton1",
                null, ErrorCheck.Error);

            // 12 - Bevel
            Template($@"WriteInterface,Value,{scriptFile},Interface,pBevel1,PEBakery", @"pBevel1",
                null, ErrorCheck.Error);

            // 13 - FileBox
            Template($@"WriteInterface,Value,{scriptFile},Interface,pFileBox1,D:\PEBakery\Launcher.exe", @"pFileBox1",
                @"D:\PEBakery\Launcher.exe,1,13,240,230,200,20,file");

            // 14 - RadioGroup
            Template($@"WriteInterface,Value,{scriptFile},Interface,pRadioGroup1,2", @"pRadioGroup1",
                @"pRadioGroup1,1,14,20,160,150,60,Option1,Option2,Option3,2");
            Template($@"WriteInterface,Value,{scriptFile},Interface,pRadioGroup1,3", @"pRadioGroup1",
                null, ErrorCheck.Error);
            Template($@"WriteInterface,Value,{scriptFile},Interface,pRadioGroup1,Items", @"pRadioGroup1",
                null, ErrorCheck.Error);
            Template($@"WriteInterface,Items,{scriptFile},Interface,pRadioGroup1,X|Y|Z", @"pRadioGroup1",
                @"pRadioGroup1,1,14,20,160,150,60,X,Y,Z,0");
            Template($@"WriteInterface,SectionName,{scriptFile},Interface,pRadioGroup1,Hello", @"pRadioGroup1",
                @"pRadioGroup1,1,14,20,160,150,60,Option1,Option2,Option3,0,_Hello_,False");
            Template($@"WriteInterface,HideProgress,{scriptFile},Interface,pRadioGroup1,None", @"pRadioGroup1",
                @"pRadioGroup1,1,14,20,160,150,60,Option1,Option2,Option3,0");
            Template($@"WriteInterface,HideProgress,{scriptFile},Interface,pRadioGroup1,True", @"pRadioGroup1",
                null, ErrorCheck.Error);
        }
        #endregion

        #region AddInterface
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandInterface")]
        public void AddInterface()
        {

        }
        #endregion
    }
}
