﻿/*
    Copyright (C) 2017-2023 Hajin Jang
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
using PEBakery.Core.Commands;
using PEBakery.Helper;
using PEBakery.Ini;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;


namespace PEBakery.Core.Tests.Command
{
    [TestClass]
    public class CommandInterfaceTests
    {
        #region Const String
        private const string TestSuiteInterface = "Interface";
        #endregion

        #region Visible
        [TestMethod]
        [TestCategory(nameof(Command))]
        [TestCategory(nameof(CommandInterface))]
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
                    Script? sc = s.Project.LoadScriptRuntime(scriptFile, new LoadScriptRuntimeOptions());
                    Assert.IsNotNull(sc);
                    ScriptSection section = sc.Sections["Process"];

                    // Enable Visible command
                    CodeParser.Options opts = CodeParser.Options.CreateOptions(Global.Setting, EngineTests.Project.Compat);
                    opts.AllowLegacyInterfaceCommand = true;
                    CodeParser parser = new CodeParser(section, opts);

                    try
                    {
                        EngineTests.Eval(s, parser, rawCode, CodeType.Visible, check);
                        if (check == ErrorCheck.Success && check == ErrorCheck.Warning)
                        {
                            string? dest = IniReadWriter.ReadKey(scriptFile, "Interface", key);
                            Assert.IsNotNull(dest);
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
                    Script? sc = s.Project.LoadScriptRuntime(scriptFile, new LoadScriptRuntimeOptions());
                    Assert.IsNotNull(sc);
                    ScriptSection section = sc.Sections["Process"];

                    // Enable Visible command
                    CodeParser.Options opts = CodeParser.Options.CreateOptions(Global.Setting, EngineTests.Project.Compat);
                    opts.AllowLegacyInterfaceCommand = true;
                    CodeParser parser = new CodeParser(section, opts);

                    try
                    {
                        CodeType? opType = optSuccess ? CodeType.VisibleOp : null;
                        EngineTests.EvalOptLines(s, parser, section, opType, rawCodes, check);
                        if (check == ErrorCheck.Success || check == ErrorCheck.Warning)
                        {
                            foreach ((string key, string value) in compTuples)
                            {
                                string? dest = IniReadWriter.ReadKey(scriptFile, "Interface", key);
                                Assert.IsNotNull(dest);
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
        [TestCategory(nameof(Command))]
        [TestCategory(nameof(CommandInterface))]
        public void ReadInterface()
        {
            EngineState s = EngineTests.CreateEngineState();
            string scriptFile = Path.Combine("%ProjectDir%", TestSuiteInterface, "ReadInterface.script");

            void SingleTemplate(string rawCode, string? expected, ErrorCheck check = ErrorCheck.Success)
            {
                EngineTests.Eval(s, rawCode, CodeType.ReadInterface, check);
                if (check == ErrorCheck.Success)
                {
                    string dest = s.Variables["Dest"];
                    Assert.IsTrue(dest.Equals(expected, StringComparison.Ordinal));
                }
            }
            void OptTemplate(List<string> rawCodes, string[] expectStrs, bool optSuccess, ErrorCheck check = ErrorCheck.Success)
            {
                CodeType? opType = optSuccess ? CodeType.ReadInterfaceOp : null;
                EngineTests.EvalOptLines(s, opType, rawCodes, check);
                if (check == ErrorCheck.Success)
                {
                    for (int i = 0; i < expectStrs.Length; i++)
                    {
                        string comp = expectStrs[i];
                        string dest = s.Variables[$"Dest{i}"];
                        Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
                    }
                }
            }

            // 0 - TextBox
            SingleTemplate($@"ReadInterface,Text,{scriptFile},Interface,pTextBox1,%Dest%", @"Display");
            SingleTemplate($@"ReadInterface,Visible,{scriptFile},Interface,pTextBox1,%Dest%", @"True");
            SingleTemplate($@"ReadInterface,PosX,{scriptFile},Interface,pTextBox1,%Dest%", @"20");
            SingleTemplate($@"ReadInterface,PosY,{scriptFile},Interface,pTextBox1,%Dest%", @"20");
            SingleTemplate($@"ReadInterface,Width,{scriptFile},Interface,pTextBox1,%Dest%", @"200");
            SingleTemplate($@"ReadInterface,Height,{scriptFile},Interface,pTextBox1,%Dest%", @"21");
            SingleTemplate($@"ReadInterface,Value,{scriptFile},Interface,pTextBox1,%Dest%", @"StringValue");
            SingleTemplate($@"ReadInterface,ToolTip,{scriptFile},Interface,pTextBox1,%Dest%", string.Empty);

            // 1 - TextLabel
            SingleTemplate($@"ReadInterface,Text,{scriptFile},Interface,pTextLabel1,%Dest%", @"Display");
            SingleTemplate($@"ReadInterface,Visible,{scriptFile},Interface,pTextLabel1,%Dest%", @"True");
            SingleTemplate($@"ReadInterface,PosX,{scriptFile},Interface,pTextLabel1,%Dest%", @"20");
            SingleTemplate($@"ReadInterface,PosY,{scriptFile},Interface,pTextLabel1,%Dest%", @"50");
            SingleTemplate($@"ReadInterface,Width,{scriptFile},Interface,pTextLabel1,%Dest%", @"230");
            SingleTemplate($@"ReadInterface,Height,{scriptFile},Interface,pTextLabel1,%Dest%", @"18");
            SingleTemplate($@"ReadInterface,Value,{scriptFile},Interface,pTextLabel1,%Dest%", @"Display");
            SingleTemplate($@"ReadInterface,ToolTip,{scriptFile},Interface,pTextLabel1,%Dest%", string.Empty);
            SingleTemplate($@"ReadInterface,FontSize,{scriptFile},Interface,pTextLabel1,%Dest%", @"8");
            SingleTemplate($@"ReadInterface,FontWeight,{scriptFile},Interface,pTextLabel1,%Dest%", @"Normal");

            // 2 - NumberBox
            SingleTemplate($@"ReadInterface,Text,{scriptFile},Interface,pNumberBox1,%Dest%", @"pNumberBox1");
            SingleTemplate($@"ReadInterface,Visible,{scriptFile},Interface,pNumberBox1,%Dest%", @"True");
            SingleTemplate($@"ReadInterface,PosX,{scriptFile},Interface,pNumberBox1,%Dest%", @"20");
            SingleTemplate($@"ReadInterface,PosY,{scriptFile},Interface,pNumberBox1,%Dest%", @"70");
            SingleTemplate($@"ReadInterface,Width,{scriptFile},Interface,pNumberBox1,%Dest%", @"40");
            SingleTemplate($@"ReadInterface,Height,{scriptFile},Interface,pNumberBox1,%Dest%", @"22");
            SingleTemplate($@"ReadInterface,Value,{scriptFile},Interface,pNumberBox1,%Dest%", @"3");
            SingleTemplate($@"ReadInterface,ToolTip,{scriptFile},Interface,pNumberBox1,%Dest%", string.Empty);
            SingleTemplate($@"ReadInterface,NumberMin,{scriptFile},Interface,pNumberBox1,%Dest%", @"0");
            SingleTemplate($@"ReadInterface,NumberMax,{scriptFile},Interface,pNumberBox1,%Dest%", @"100");
            SingleTemplate($@"ReadInterface,NumberTick,{scriptFile},Interface,pNumberBox1,%Dest%", @"1");

            // 3 - CheckBox
            SingleTemplate($@"ReadInterface,Text,{scriptFile},Interface,pCheckBox1,%Dest%", @"pCheckBox1");
            SingleTemplate($@"ReadInterface,Visible,{scriptFile},Interface,pCheckBox1,%Dest%", @"True");
            SingleTemplate($@"ReadInterface,PosX,{scriptFile},Interface,pCheckBox1,%Dest%", @"20");
            SingleTemplate($@"ReadInterface,PosY,{scriptFile},Interface,pCheckBox1,%Dest%", @"100");
            SingleTemplate($@"ReadInterface,Width,{scriptFile},Interface,pCheckBox1,%Dest%", @"200");
            SingleTemplate($@"ReadInterface,Height,{scriptFile},Interface,pCheckBox1,%Dest%", @"18");
            SingleTemplate($@"ReadInterface,Value,{scriptFile},Interface,pCheckBox1,%Dest%", @"True");
            SingleTemplate($@"ReadInterface,ToolTip,{scriptFile},Interface,pCheckBox1,%Dest%", string.Empty);
            SingleTemplate($@"ReadInterface,SectionName,{scriptFile},Interface,pCheckBox1,%Dest%", string.Empty);
            SingleTemplate($@"ReadInterface,HideProgress,{scriptFile},Interface,pCheckBox1,%Dest%", "None");

            // 4 - ComboBox
            SingleTemplate($@"ReadInterface,Text,{scriptFile},Interface,pComboBox1,%Dest%", @"A");
            SingleTemplate($@"ReadInterface,Visible,{scriptFile},Interface,pComboBox1,%Dest%", @"True");
            SingleTemplate($@"ReadInterface,PosX,{scriptFile},Interface,pComboBox1,%Dest%", @"20");
            SingleTemplate($@"ReadInterface,PosY,{scriptFile},Interface,pComboBox1,%Dest%", @"130");
            SingleTemplate($@"ReadInterface,Width,{scriptFile},Interface,pComboBox1,%Dest%", @"150");
            SingleTemplate($@"ReadInterface,Height,{scriptFile},Interface,pComboBox1,%Dest%", @"21");
            SingleTemplate($@"ReadInterface,Value,{scriptFile},Interface,pComboBox1,%Dest%", @"A");
            SingleTemplate($@"ReadInterface,ToolTip,{scriptFile},Interface,pComboBox1,%Dest%", string.Empty);
            SingleTemplate($@"ReadInterface,Items,{scriptFile},Interface,pComboBox1,%Dest%", @"A|B|C|D");
            SingleTemplate($@"ReadInterface,Items,{scriptFile},Interface,pComboBox1,%Dest%,Delim=$", @"A$B$C$D");
            SingleTemplate($@"ReadInterface,SectionName,{scriptFile},Interface,pComboBox1,%Dest%", string.Empty);
            SingleTemplate($@"ReadInterface,HideProgress,{scriptFile},Interface,pComboBox1,%Dest%", "None");

            // 5 - Image
            SingleTemplate($@"ReadInterface,Text,{scriptFile},Interface,pImage1,%Dest%", @"Logo.jpg");
            SingleTemplate($@"ReadInterface,Resource,{scriptFile},Interface,pImage1,%Dest%", @"Logo.jpg");
            SingleTemplate($@"ReadInterface,Visible,{scriptFile},Interface,pImage1,%Dest%", @"True");
            SingleTemplate($@"ReadInterface,PosX,{scriptFile},Interface,pImage1,%Dest%", @"20");
            SingleTemplate($@"ReadInterface,PosY,{scriptFile},Interface,pImage1,%Dest%", @"230");
            SingleTemplate($@"ReadInterface,Width,{scriptFile},Interface,pImage1,%Dest%", @"40");
            SingleTemplate($@"ReadInterface,Height,{scriptFile},Interface,pImage1,%Dest%", @"40");
            SingleTemplate($@"ReadInterface,Value,{scriptFile},Interface,pImage1,%Dest%", null, ErrorCheck.RuntimeError);
            SingleTemplate($@"ReadInterface,ToolTip,{scriptFile},Interface,pImage1,%Dest%", string.Empty);
            SingleTemplate($@"ReadInterface,Url,{scriptFile},Interface,pImage1,%Dest%", string.Empty);

            // 6 - TextFile
            SingleTemplate($@"ReadInterface,Text,{scriptFile},Interface,pTextFile1,%Dest%", @"HelpMsg.txt");
            SingleTemplate($@"ReadInterface,Visible,{scriptFile},Interface,pTextFile1,%Dest%", @"True");
            SingleTemplate($@"ReadInterface,PosX,{scriptFile},Interface,pTextFile1,%Dest%", @"240");
            SingleTemplate($@"ReadInterface,PosY,{scriptFile},Interface,pTextFile1,%Dest%", @"20");
            SingleTemplate($@"ReadInterface,Width,{scriptFile},Interface,pTextFile1,%Dest%", @"200");
            SingleTemplate($@"ReadInterface,Height,{scriptFile},Interface,pTextFile1,%Dest%", @"86");
            SingleTemplate($@"ReadInterface,Value,{scriptFile},Interface,pTextFile1,%Dest%", null, ErrorCheck.RuntimeError);
            SingleTemplate($@"ReadInterface,ToolTip,{scriptFile},Interface,pTextFile1,%Dest%", string.Empty);

            // 8 - Button
            SingleTemplate($@"ReadInterface,Text,{scriptFile},Interface,pButton1,%Dest%", @"ShowProgress");
            SingleTemplate($@"ReadInterface,Text,{scriptFile},Interface,pButton2,%Dest%", @"HideProgress");
            SingleTemplate($@"ReadInterface,Resource,{scriptFile},Interface,pButton1,%Dest%", "");
            SingleTemplate($@"ReadInterface,Resource,{scriptFile},Interface,pButton2,%Dest%", @"Logo.jpg");
            SingleTemplate($@"ReadInterface,Visible,{scriptFile},Interface,pButton1,%Dest%", @"True");
            SingleTemplate($@"ReadInterface,PosX,{scriptFile},Interface,pButton1,%Dest%", @"240");
            SingleTemplate($@"ReadInterface,PosY,{scriptFile},Interface,pButton1,%Dest%", @"115");
            SingleTemplate($@"ReadInterface,Width,{scriptFile},Interface,pButton1,%Dest%", @"80");
            SingleTemplate($@"ReadInterface,Height,{scriptFile},Interface,pButton1,%Dest%", @"25");
            SingleTemplate($@"ReadInterface,Value,{scriptFile},Interface,pButton1,%Dest%", null, ErrorCheck.RuntimeError);
            SingleTemplate($@"ReadInterface,ToolTip,{scriptFile},Interface,pButton1,%Dest%", string.Empty);
            SingleTemplate($@"ReadInterface,SectionName,{scriptFile},Interface,pButton1,%Dest%", @"Hello");
            SingleTemplate($@"ReadInterface,HideProgress,{scriptFile},Interface,pButton1,%Dest%", @"False");

            // 10 - WebLabel
            SingleTemplate($@"ReadInterface,Text,{scriptFile},Interface,pWebLabel1,%Dest%", @"GitHub");
            SingleTemplate($@"ReadInterface,Visible,{scriptFile},Interface,pWebLabel1,%Dest%", @"True");
            SingleTemplate($@"ReadInterface,PosX,{scriptFile},Interface,pWebLabel1,%Dest%", @"250");
            SingleTemplate($@"ReadInterface,PosY,{scriptFile},Interface,pWebLabel1,%Dest%", @"160");
            SingleTemplate($@"ReadInterface,Width,{scriptFile},Interface,pWebLabel1,%Dest%", @"32");
            SingleTemplate($@"ReadInterface,Height,{scriptFile},Interface,pWebLabel1,%Dest%", @"18");
            SingleTemplate($@"ReadInterface,Value,{scriptFile},Interface,pWebLabel1,%Dest%", null, ErrorCheck.RuntimeError);
            SingleTemplate($@"ReadInterface,ToolTip,{scriptFile},Interface,pWebLabel1,%Dest%", string.Empty);
            SingleTemplate($@"ReadInterface,Url,{scriptFile},Interface,pWebLabel1,%Dest%", @"https://github.com/pebakery/pebakery");

            // 11 - RadioButton
            SingleTemplate($@"ReadInterface,Text,{scriptFile},Interface,pRadioButton1,%Dest%", @"pRadioButton1");
            SingleTemplate($@"ReadInterface,Visible,{scriptFile},Interface,pRadioButton1,%Dest%", @"True");
            SingleTemplate($@"ReadInterface,PosX,{scriptFile},Interface,pRadioButton1,%Dest%", @"250");
            SingleTemplate($@"ReadInterface,PosY,{scriptFile},Interface,pRadioButton1,%Dest%", @"180");
            SingleTemplate($@"ReadInterface,Width,{scriptFile},Interface,pRadioButton1,%Dest%", @"100");
            SingleTemplate($@"ReadInterface,Height,{scriptFile},Interface,pRadioButton1,%Dest%", @"20");
            SingleTemplate($@"ReadInterface,Value,{scriptFile},Interface,pRadioButton1,%Dest%", @"False");
            SingleTemplate($@"ReadInterface,ToolTip,{scriptFile},Interface,pRadioButton1,%Dest%", string.Empty);
            SingleTemplate($@"ReadInterface,SectionName,{scriptFile},Interface,pRadioButton1,%Dest%", string.Empty);
            SingleTemplate($@"ReadInterface,HideProgress,{scriptFile},Interface,pRadioButton1,%Dest%", "None");

            // 12 - Bevel
            SingleTemplate($@"ReadInterface,Text,{scriptFile},Interface,pBevel1,%Dest%", @"pBevel1");
            SingleTemplate($@"ReadInterface,Visible,{scriptFile},Interface,pBevel1,%Dest%", @"True");
            SingleTemplate($@"ReadInterface,PosX,{scriptFile},Interface,pBevel1,%Dest%", @"240");
            SingleTemplate($@"ReadInterface,PosY,{scriptFile},Interface,pBevel1,%Dest%", @"150");
            SingleTemplate($@"ReadInterface,Width,{scriptFile},Interface,pBevel1,%Dest%", @"235");
            SingleTemplate($@"ReadInterface,Height,{scriptFile},Interface,pBevel1,%Dest%", @"60");
            SingleTemplate($@"ReadInterface,Value,{scriptFile},Interface,pBevel1,%Dest%", null, ErrorCheck.RuntimeError);
            SingleTemplate($@"ReadInterface,ToolTip,{scriptFile},Interface,pBevel1,%Dest%", string.Empty);

            // 13 - FileBox
            SingleTemplate($@"ReadInterface,Text,{scriptFile},Interface,pFileBox1,%Dest%", @"C:\Windows\notepad.exe");
            SingleTemplate($@"ReadInterface,Visible,{scriptFile},Interface,pFileBox1,%Dest%", @"True");
            SingleTemplate($@"ReadInterface,PosX,{scriptFile},Interface,pFileBox1,%Dest%", @"240");
            SingleTemplate($@"ReadInterface,PosY,{scriptFile},Interface,pFileBox1,%Dest%", @"230");
            SingleTemplate($@"ReadInterface,Width,{scriptFile},Interface,pFileBox1,%Dest%", @"200");
            SingleTemplate($@"ReadInterface,Height,{scriptFile},Interface,pFileBox1,%Dest%", @"20");
            SingleTemplate($@"ReadInterface,Value,{scriptFile},Interface,pFileBox1,%Dest%", @"C:\Windows\notepad.exe");
            SingleTemplate($@"ReadInterface,ToolTip,{scriptFile},Interface,pFileBox1,%Dest%", string.Empty);

            SingleTemplate($@"ReadInterface,Text,{scriptFile},Interface,pFileBox2,%Dest%", @"E:\WinPE\");
            SingleTemplate($@"ReadInterface,Visible,{scriptFile},Interface,pFileBox2,%Dest%", @"True");
            SingleTemplate($@"ReadInterface,PosX,{scriptFile},Interface,pFileBox2,%Dest%", @"240");
            SingleTemplate($@"ReadInterface,PosY,{scriptFile},Interface,pFileBox2,%Dest%", @"260");
            SingleTemplate($@"ReadInterface,Width,{scriptFile},Interface,pFileBox2,%Dest%", @"200");
            SingleTemplate($@"ReadInterface,Height,{scriptFile},Interface,pFileBox2,%Dest%", @"20");
            SingleTemplate($@"ReadInterface,Value,{scriptFile},Interface,pFileBox2,%Dest%", @"E:\WinPE\");
            SingleTemplate($@"ReadInterface,ToolTip,{scriptFile},Interface,pFileBox2,%Dest%", string.Empty);

            // 14 - RadioGroup
            SingleTemplate($@"ReadInterface,Text,{scriptFile},Interface,pRadioGroup1,%Dest%", @"pRadioGroup1");
            SingleTemplate($@"ReadInterface,Visible,{scriptFile},Interface,pRadioGroup1,%Dest%", @"True");
            SingleTemplate($@"ReadInterface,PosX,{scriptFile},Interface,pRadioGroup1,%Dest%", @"20");
            SingleTemplate($@"ReadInterface,PosY,{scriptFile},Interface,pRadioGroup1,%Dest%", @"160");
            SingleTemplate($@"ReadInterface,Width,{scriptFile},Interface,pRadioGroup1,%Dest%", @"150");
            SingleTemplate($@"ReadInterface,Height,{scriptFile},Interface,pRadioGroup1,%Dest%", @"60");
            SingleTemplate($@"ReadInterface,Value,{scriptFile},Interface,pRadioGroup1,%Dest%", @"0");
            SingleTemplate($@"ReadInterface,Items,{scriptFile},Interface,pRadioGroup1,%Dest%", @"Option1|Option2|Option3");
            SingleTemplate($@"ReadInterface,Items,{scriptFile},Interface,pRadioGroup1,%Dest%,Delim=$", @"Option1$Option2$Option3");
            SingleTemplate($@"ReadInterface,SectionName,{scriptFile},Interface,pRadioGroup1,%Dest%", string.Empty);
            SingleTemplate($@"ReadInterface,HideProgress,{scriptFile},Interface,pRadioGroup1,%Dest%", "None");

            // 20 - PathBox
            SingleTemplate($@"ReadInterface,Text,{scriptFile},Interface,pPathBox1,%Dest%", @"C:\Windows\notepad.exe");
            SingleTemplate($@"ReadInterface,Visible,{scriptFile},Interface,pPathBox1,%Dest%", @"True");
            SingleTemplate($@"ReadInterface,PosX,{scriptFile},Interface,pPathBox1,%Dest%", @"240");
            SingleTemplate($@"ReadInterface,PosY,{scriptFile},Interface,pPathBox1,%Dest%", @"290");
            SingleTemplate($@"ReadInterface,Width,{scriptFile},Interface,pPathBox1,%Dest%", @"200");
            SingleTemplate($@"ReadInterface,Height,{scriptFile},Interface,pPathBox1,%Dest%", @"20");
            SingleTemplate($@"ReadInterface,Value,{scriptFile},Interface,pPathBox1,%Dest%", @"C:\Windows\notepad.exe");
            SingleTemplate($@"ReadInterface,ToolTip,{scriptFile},Interface,pPathBox1,%Dest%", string.Empty);
            SingleTemplate($@"ReadInterface,SectionName,{scriptFile},Interface,pPathBox1,%Dest%", "Hello");
            SingleTemplate($@"ReadInterface,HideProgress,{scriptFile},Interface,pPathBox1,%Dest%", "True");

            SingleTemplate($@"ReadInterface,Text,{scriptFile},Interface,pPathBox2,%Dest%", @"E:\WinPE\");
            SingleTemplate($@"ReadInterface,Visible,{scriptFile},Interface,pPathBox2,%Dest%", @"True");
            SingleTemplate($@"ReadInterface,PosX,{scriptFile},Interface,pPathBox2,%Dest%", @"240");
            SingleTemplate($@"ReadInterface,PosY,{scriptFile},Interface,pPathBox2,%Dest%", @"320");
            SingleTemplate($@"ReadInterface,Width,{scriptFile},Interface,pPathBox2,%Dest%", @"200");
            SingleTemplate($@"ReadInterface,Height,{scriptFile},Interface,pPathBox2,%Dest%", @"20");
            SingleTemplate($@"ReadInterface,Value,{scriptFile},Interface,pPathBox2,%Dest%", @"E:\WinPE\");
            SingleTemplate($@"ReadInterface,ToolTip,{scriptFile},Interface,pPathBox2,%Dest%", @"EXPECTED");
            SingleTemplate($@"ReadInterface,SectionName,{scriptFile},Interface,pPathBox2,%Dest%", string.Empty);
            SingleTemplate($@"ReadInterface,HideProgress,{scriptFile},Interface,pPathBox2,%Dest%", "None");

            // Visible - False
            SingleTemplate($@"ReadInterface,Visible,{scriptFile},Interface,pTextLabel2,%Dest%", @"False");

            // ToolTip
            SingleTemplate($@"ReadInterface,ToolTip,{scriptFile},Interface,pTextLabel3,%Dest%", @"PEBakery");

            // Optimization
            OptTemplate(new List<string>
            {
                $@"ReadInterface,Text,{scriptFile},Interface,pTextBox1,%Dest0%",
                $@"ReadInterface,PosX,{scriptFile},Interface,pButton1,%Dest1%",
                $@"ReadInterface,ToolTip,{scriptFile},Interface,pTextLabel3,%Dest2%",
            }, new string[]
            {
                @"Display",
                @"240",
                @"PEBakery"
            }, true);
        }
        #endregion

        #region WriteInterface
        [TestMethod]
        [TestCategory(nameof(Command))]
        [TestCategory(nameof(CommandInterface))]
        public void WriteInterface()
        {
            EngineState s = EngineTests.CreateEngineState();
            string srcFile = StringEscaper.Preprocess(s, Path.Combine("%ProjectDir%", TestSuiteInterface, "ReadInterface.script"));
            string scriptFile = FileHelper.GetTempFile();

            void SingleTemplate(string rawCode, string key, string? varResult, string? lineResult, ErrorCheck check = ErrorCheck.Success)
            {
                File.Copy(srcFile, scriptFile, true);
                try
                {
                    EngineTests.Eval(s, rawCode, CodeType.WriteInterface, check);
                    if (check == ErrorCheck.Success)
                    {
                        if (varResult != null)
                        {
                            Assert.IsTrue(s.Variables.ContainsKey(VarsType.Local, key));
                            Assert.IsTrue(varResult.Equals(s.Variables.GetValue(VarsType.Local, key), StringComparison.Ordinal));
                        }

                        string? dest = IniReadWriter.ReadKey(scriptFile, "Interface", key);
                        Assert.IsNotNull(dest);
                        Assert.IsTrue(dest.Equals(lineResult, StringComparison.Ordinal));
                    }
                }
                finally
                {
                    if (File.Exists(scriptFile))
                        File.Delete(scriptFile);
                }
            }
            void OptTemplate(List<string> rawCodes, (string key, string? varResult, string lineResult)[] resultTuples, bool optSuccess, ErrorCheck check = ErrorCheck.Success)
            {
                File.Copy(srcFile, scriptFile, true);
                try
                {
                    CodeType? opType = optSuccess ? CodeType.WriteInterfaceOp : null;
                    EngineTests.EvalOptLines(s, opType, rawCodes, check);
                    if (check == ErrorCheck.Success)
                    {
                        for (int i = 0; i < resultTuples.Length; i++)
                        {
                            string key = resultTuples[i].key;
                            string? varResult = resultTuples[i].varResult;
                            string lineResult = resultTuples[i].lineResult;

                            if (varResult != null)
                            {
                                Assert.IsTrue(s.Variables.ContainsKey(VarsType.Local, key));
                                Assert.IsTrue(varResult.Equals(s.Variables.GetValue(VarsType.Local, key), StringComparison.Ordinal));
                            }

                            string? dest = IniReadWriter.ReadKey(scriptFile, "Interface", key);
                            Assert.IsNotNull(dest);
                            Assert.IsTrue(dest.Equals(lineResult, StringComparison.Ordinal));
                        }
                    }
                }
                finally
                {
                    if (File.Exists(scriptFile))
                        File.Delete(scriptFile);
                }
            }

            // Common
            SingleTemplate($@"WriteInterface,Text,{scriptFile},Interface,pTextBox1,PEBakery", @"pTextBox1",
                null, @"PEBakery,1,0,20,20,200,21,StringValue");
            SingleTemplate($@"WriteInterface,Visible,{scriptFile},Interface,pTextBox1,False", @"pTextBox1",
                null, @"Display,0,0,20,20,200,21,StringValue");
            SingleTemplate($@"WriteInterface,PosX,{scriptFile},Interface,pTextBox1,30", @"pTextBox1",
                null, @"Display,1,0,30,20,200,21,StringValue");
            SingleTemplate($@"WriteInterface,PosY,{scriptFile},Interface,pTextBox1,30", @"pTextBox1",
                null, @"Display,1,0,20,30,200,21,StringValue");
            SingleTemplate($@"WriteInterface,Width,{scriptFile},Interface,pTextBox1,30", @"pTextBox1",
                null, @"Display,1,0,20,20,30,21,StringValue");
            SingleTemplate($@"WriteInterface,Height,{scriptFile},Interface,pTextBox1,10", @"pTextBox1",
                null, @"Display,1,0,20,20,200,10,StringValue");
            SingleTemplate($@"WriteInterface,ToolTip,{scriptFile},Interface,pTextBox1,PEBakery", @"pTextBox1",
                null, @"Display,1,0,20,20,200,21,StringValue,__PEBakery");

            // 0 - TextBox
            SingleTemplate($@"WriteInterface,Value,{scriptFile},Interface,pTextBox1,PEBakery", @"pTextBox1",
                "PEBakery", @"Display,1,0,20,20,200,21,PEBakery");

            // 1 - TextLabel
            SingleTemplate($@"WriteInterface,Text,{scriptFile},Interface,pTextLabel1,PEBakery", @"pTextLabel1",
                null, @"PEBakery,1,1,20,50,230,18,8,Normal");
            SingleTemplate($@"WriteInterface,Value,{scriptFile},Interface,pTextLabel1,PEBakery", @"pTextLabel1",
                null, @"PEBakery,1,1,20,50,230,18,8,Normal");
            SingleTemplate($@"WriteInterface,FontSize,{scriptFile},Interface,pTextLabel1,10", @"pTextLabel1",
                null, @"Display,1,1,20,50,230,18,10,Normal");
            SingleTemplate($@"WriteInterface,FontWeight,{scriptFile},Interface,pTextLabel1,Bold", @"pTextLabel1",
                null, @"Display,1,1,20,50,230,18,8,Bold");
            SingleTemplate($@"WriteInterface,FontWeight,{scriptFile},Interface,pTextLabel1,Error", @"pTextLabel1",
                null, null, ErrorCheck.RuntimeError);

            // 2 - NumberBox
            SingleTemplate($@"WriteInterface,Value,{scriptFile},Interface,pNumberBox1,2", @"pNumberBox1",
                "2", @"pNumberBox1,1,2,20,70,40,22,2,0,100,1");
            SingleTemplate($@"WriteInterface,Value,{scriptFile},Interface,pNumberBox1,200", @"pNumberBox1",
                null, null, ErrorCheck.RuntimeError);
            SingleTemplate($@"WriteInterface,Value,{scriptFile},Interface,pNumberBox1,Str", @"pNumberBox1",
                null, null, ErrorCheck.RuntimeError);
            SingleTemplate($@"WriteInterface,NumberMin,{scriptFile},Interface,pNumberBox1,10", @"pNumberBox1",
                "10", @"pNumberBox1,1,2,20,70,40,22,10,10,100,1");
            SingleTemplate($@"WriteInterface,NumberMax,{scriptFile},Interface,pNumberBox1,1", @"pNumberBox1",
                "1", @"pNumberBox1,1,2,20,70,40,22,1,0,1,1");
            SingleTemplate($@"WriteInterface,NumberTick,{scriptFile},Interface,pNumberBox1,5", @"pNumberBox1",
                null, @"pNumberBox1,1,2,20,70,40,22,3,0,100,5");
            SingleTemplate($@"WriteInterface,NumberMin,{scriptFile},Interface,pNumberBox1,Error", @"pNumberBox1",
                null, null, ErrorCheck.RuntimeError);

            // 3 - CheckBox
            SingleTemplate($@"WriteInterface,Value,{scriptFile},Interface,pCheckBox1,False", @"pCheckBox1",
                "False", @"pCheckBox1,1,3,20,100,200,18,False");
            SingleTemplate($@"WriteInterface,SectionName,{scriptFile},Interface,pCheckBox1,Hello", @"pCheckBox1",
                null, @"pCheckBox1,1,3,20,100,200,18,True,_Hello_,False");
            SingleTemplate($@"WriteInterface,HideProgress,{scriptFile},Interface,pCheckBox1,None", @"pCheckBox1",
                null, @"pCheckBox1,1,3,20,100,200,18,True");
            SingleTemplate($@"WriteInterface,HideProgress,{scriptFile},Interface,pCheckBox1,True", @"pCheckBox1",
                null, null, ErrorCheck.RuntimeError);

            // 4 - ComboBox
            SingleTemplate($@"WriteInterface,Value,{scriptFile},Interface,pComboBox1,B", @"pComboBox1",
                "B", @"B,1,4,20,130,150,21,A,B,C,D");
            SingleTemplate($@"WriteInterface,Value,{scriptFile},Interface,pComboBox1,E", @"pComboBox1",
                null, null, ErrorCheck.RuntimeError);
            SingleTemplate($@"WriteInterface,Items,{scriptFile},Interface,pComboBox1,X|Y|Z", @"pComboBox1",
                "X", @"X,1,4,20,130,150,21,X,Y,Z");
            SingleTemplate($@"WriteInterface,Items,{scriptFile},Interface,pComboBox1,X$Y$Z,Delim=$", @"pComboBox1",
                "X", @"X,1,4,20,130,150,21,X,Y,Z");
            SingleTemplate($@"WriteInterface,SectionName,{scriptFile},Interface,pComboBox1,Hello", @"pComboBox1",
                null, @"A,1,4,20,130,150,21,A,B,C,D,_Hello_,False");
            SingleTemplate($@"WriteInterface,HideProgress,{scriptFile},Interface,pComboBox1,None", @"pComboBox1",
                null, @"A,1,4,20,130,150,21,A,B,C,D");
            SingleTemplate($@"WriteInterface,HideProgress,{scriptFile},Interface,pComboBox1,True", @"pComboBox1",
                null, null, ErrorCheck.RuntimeError);

            // 5 - Image
            SingleTemplate($@"WriteInterface,Value,{scriptFile},Interface,pImage1,PEBakery", @"pImage1",
                null, null, ErrorCheck.RuntimeError);
            SingleTemplate($@"WriteInterface,Resource,{scriptFile},Interface,pImage1,Logo2.jpg", @"pImage1",
                null, @"Logo2.jpg,1,5,20,230,40,40");
            SingleTemplate($@"WriteInterface,Resource,{scriptFile},Interface,pImage1,", @"pImage1",
                null, @"none,1,5,20,230,40,40");
            SingleTemplate($@"WriteInterface,Resource,{scriptFile},Interface,pImage1,Nil", @"pImage1",
                null, @"none,1,5,20,230,40,40");
            SingleTemplate($@"WriteInterface,Url,{scriptFile},Interface,pImage1,https://github.com/pebakery/pebakery", @"pImage1",
                null, @"Logo.jpg,1,5,20,230,40,40,https://github.com/pebakery/pebakery");

            // 6 - TextFile
            SingleTemplate($@"WriteInterface,Value,{scriptFile},Interface,pTextFile1,PEBakery", @"pTextFile1",
                null, null, ErrorCheck.RuntimeError);
            SingleTemplate($@"WriteInterface,Resource,{scriptFile},Interface,pTextFile1,", @"pTextFile1",
                null, @"none,1,6,240,20,200,86");
            SingleTemplate($@"WriteInterface,Resource,{scriptFile},Interface,pTextFile1,Nil", @"pTextFile1",
                null, @"none,1,6,240,20,200,86");
            SingleTemplate($@"WriteInterface,Resource,{scriptFile},Interface,pTextFile1,Dummy.txt", @"pTextFile1",
                null, @"Dummy.txt,1,6,240,20,200,86");

            // 8 - Button
            SingleTemplate($@"WriteInterface,Resource,{scriptFile},Interface,pButton1,Logo.jpg", @"pButton1",
                null, @"ShowProgress,1,8,240,115,80,25,Hello,Logo.jpg,False");
            SingleTemplate($@"WriteInterface,Resource,{scriptFile},Interface,pButton1,", @"pButton1",
                null, @"ShowProgress,1,8,240,115,80,25,Hello,0,False");
            SingleTemplate($@"WriteInterface,Resource,{scriptFile},Interface,pButton1,Nil", @"pButton1",
                null, @"ShowProgress,1,8,240,115,80,25,Hello,0,False");
            SingleTemplate($@"WriteInterface,Value,{scriptFile},Interface,pButton1,PEBakery", @"pButton1",
                null, null, ErrorCheck.RuntimeError);
            SingleTemplate($@"WriteInterface,SectionName,{scriptFile},Interface,pButton1,World", @"pButton1",
                null, @"ShowProgress,1,8,240,115,80,25,World,0,False");
            SingleTemplate($@"WriteInterface,SectionName,{scriptFile},Interface,pButton1,""""", @"pButton1",
                null, null, ErrorCheck.RuntimeError);
            SingleTemplate($@"WriteInterface,HideProgress,{scriptFile},Interface,pButton1,None", @"pButton1",
                null, null, ErrorCheck.RuntimeError);
            SingleTemplate($@"WriteInterface,HideProgress,{scriptFile},Interface,pButton1,True", @"pButton1",
                null, @"ShowProgress,1,8,240,115,80,25,Hello,0,True");

            // 10 - WebLabel
            SingleTemplate($@"WriteInterface,Value,{scriptFile},Interface,pWebLabel1,PEBakery", @"pWebLabel1",
                null, null, ErrorCheck.RuntimeError);
            SingleTemplate($@"WriteInterface,Url,{scriptFile},Interface,pWebLabel1,https://github.com/pebakery", @"pWebLabel1",
                null, @"GitHub,1,10,250,160,32,18,https://github.com/pebakery");

            // 11 - RadioButton
            SingleTemplate($@"WriteInterface,Value,{scriptFile},Interface,pRadioButton1,True", @"pRadioButton1",
                "True", @"pRadioButton1,1,11,250,180,100,20,True");
            SingleTemplate($@"WriteInterface,SectionName,{scriptFile},Interface,pRadioButton1,Hello", @"pRadioButton1",
                null, @"pRadioButton1,1,11,250,180,100,20,False,_Hello_,False");
            SingleTemplate($@"WriteInterface,HideProgress,{scriptFile},Interface,pRadioButton1,None", @"pRadioButton1",
                null, @"pRadioButton1,1,11,250,180,100,20,False");
            SingleTemplate($@"WriteInterface,HideProgress,{scriptFile},Interface,pRadioButton1,True", @"pRadioButton1",
                null, null, ErrorCheck.RuntimeError);

            // 12 - Bevel
            SingleTemplate($@"WriteInterface,Value,{scriptFile},Interface,pBevel1,PEBakery", @"pBevel1",
                null, null, ErrorCheck.RuntimeError);

            // 13 - FileBox
            SingleTemplate($@"WriteInterface,Text,{scriptFile},Interface,pFileBox1,D:\PEBakery\Launcher.exe", @"pFileBox1",
                @"D:\PEBakery\Launcher.exe", @"D:\PEBakery\Launcher.exe,1,13,240,230,200,20,file");
            SingleTemplate($@"WriteInterface,Value,{scriptFile},Interface,pFileBox1,D:\PEBakery\Launcher.exe", @"pFileBox1",
                @"D:\PEBakery\Launcher.exe", @"D:\PEBakery\Launcher.exe,1,13,240,230,200,20,file");

            // 14 - RadioGroup
            SingleTemplate($@"WriteInterface,Value,{scriptFile},Interface,pRadioGroup1,2", @"pRadioGroup1",
                "2", @"pRadioGroup1,1,14,20,160,150,60,Option1,Option2,Option3,2");
            SingleTemplate($@"WriteInterface,Value,{scriptFile},Interface,pRadioGroup1,3", @"pRadioGroup1",
                null, null, ErrorCheck.RuntimeError);
            SingleTemplate($@"WriteInterface,Value,{scriptFile},Interface,pRadioGroup1,Items", @"pRadioGroup1",
                null, null, ErrorCheck.RuntimeError);
            SingleTemplate($@"WriteInterface,Items,{scriptFile},Interface,pRadioGroup1,X|Y|Z", @"pRadioGroup1",
                "0", @"pRadioGroup1,1,14,20,160,150,60,X,Y,Z,0");
            SingleTemplate($@"WriteInterface,Items,{scriptFile},Interface,pRadioGroup1,X$Y$Z,Delim=$", @"pRadioGroup1",
                "0", @"pRadioGroup1,1,14,20,160,150,60,X,Y,Z,0");
            SingleTemplate($@"WriteInterface,SectionName,{scriptFile},Interface,pRadioGroup1,Hello", @"pRadioGroup1",
                null, @"pRadioGroup1,1,14,20,160,150,60,Option1,Option2,Option3,0,_Hello_,False");
            SingleTemplate($@"WriteInterface,HideProgress,{scriptFile},Interface,pRadioGroup1,None", @"pRadioGroup1",
                null, @"pRadioGroup1,1,14,20,160,150,60,Option1,Option2,Option3,0");
            SingleTemplate($@"WriteInterface,HideProgress,{scriptFile},Interface,pRadioGroup1,True", @"pRadioGroup1",
                null, null, ErrorCheck.RuntimeError);

            // 20 - PathBox
            SingleTemplate($@"WriteInterface,Text,{scriptFile},Interface,pPathBox1,D:\PEBakery\Launcher.exe", @"pPathBox1",
                @"D:\PEBakery\Launcher.exe", @"D:\PEBakery\Launcher.exe,1,20,240,290,200,20,file,""Filter=Executable Files|*.exe"",_Hello_,True");
            SingleTemplate($@"WriteInterface,Value,{scriptFile},Interface,pPathBox1,D:\PEBakery\Launcher.exe", @"pPathBox1",
                @"D:\PEBakery\Launcher.exe", @"D:\PEBakery\Launcher.exe,1,20,240,290,200,20,file,""Filter=Executable Files|*.exe"",_Hello_,True");
            SingleTemplate($@"WriteInterface,SectionName,{scriptFile},Interface,pPathBox1,World", @"pPathBox1",
                null, @"C:\Windows\notepad.exe,1,20,240,290,200,20,file,""Filter=Executable Files|*.exe"",_World_,True");
            SingleTemplate($@"WriteInterface,HideProgress,{scriptFile},Interface,pPathBox1,None", @"pPathBox1",
                null, @"C:\Windows\notepad.exe,1,20,240,290,200,20,file,""Filter=Executable Files|*.exe""");
            SingleTemplate($@"WriteInterface,HideProgress,{scriptFile},Interface,pPathBox1,False", @"pPathBox1",
                null, @"C:\Windows\notepad.exe,1,20,240,290,200,20,file,""Filter=Executable Files|*.exe"",_Hello_,False");

            // Optimization
            OptTemplate(new List<string>
            {
                $@"WriteInterface,Value,{scriptFile},Interface,pRadioGroup1,2",
                $@"WriteInterface,Items,{scriptFile},Interface,pComboBox1,X|Y|Z",
                $@"WriteInterface,ToolTip,{scriptFile},Interface,pTextBox1,PEBakery",
            }, new (string, string?, string)[]
            {
                ("pRadioGroup1", "2", @"pRadioGroup1,1,14,20,160,150,60,Option1,Option2,Option3,2"),
                (@"pComboBox1", "X", @"X,1,4,20,130,150,21,X,Y,Z"),
                ("pTextBox1", null, @"Display,1,0,20,20,200,21,StringValue,__PEBakery"),
            }, true);
        }
        #endregion

        #region AddInterface
        [TestMethod]
        [TestCategory(nameof(Command))]
        [TestCategory(nameof(CommandInterface))]
        public void AddInterface()
        {
            EngineState s = EngineTests.CreateEngineState();
            string scriptFile = Path.Combine("%ProjectDir%", TestSuiteInterface, "AddInterface.script");

            void SingleTemplate(string rawCode, (string, string)[] comps, ErrorCheck check = ErrorCheck.Success)
            {
                Dictionary<string, string> beforeDict = s.Variables.GetVarDict(VarsType.Local);
                EngineTests.Eval(s, rawCode, CodeType.AddInterface, check);
                if (check == ErrorCheck.Success)
                {
                    Dictionary<string, string> afterDict = s.Variables.GetVarDict(VarsType.Local);
                    Assert.AreEqual(comps.Length, afterDict.Count - beforeDict.Count);
                    foreach ((string key, string value) in comps)
                    {
                        string dest = s.Variables[key];
                        Assert.IsTrue(dest.Equals(value, StringComparison.Ordinal));
                    }
                }
            }

            SingleTemplate($"AddInterface,{scriptFile},VerboseInterface,\"\"", new (string, string)[]
            {
                ("pTextLabel1", "Display"),
                ("pTextLabel2", "Hidden"),
                ("pTextLabel3", "ToolTip"),
                ("pNumberBox1", "3"),
                ("pCheckBox1", "True"),
                ("pComboBox1", "A"),
                ("pRadioGroup1", "0"),
                ("pRadioButton1", "False"),
                ("pFileBox1", @"C:\Windows\notepad.exe"),
                ("pFileBox2", @"E:\WinPE\"),
            });
            SingleTemplate($"AddInterface,{scriptFile},VerboseInterface,V", new (string, string)[]
            {
                ("V_pTextLabel1", "Display"),
                ("V_pTextLabel2", "Hidden"),
                ("V_pTextLabel3", "ToolTip"),
                ("V_pNumberBox1", "3"),
                ("V_pCheckBox1", "True"),
                ("V_pComboBox1", "A"),
                ("V_pRadioGroup1", "0"),
                ("V_pRadioButton1", "False"),
                ("V_pFileBox1", @"C:\Windows\notepad.exe"),
                ("V_pFileBox2", @"E:\WinPE\"),
            });
        }
        #endregion

        #region Echo
        [TestMethod]
        [TestCategory(nameof(Command))]
        [TestCategory(nameof(CommandInterface))]
        public void Echo()
        {
            EngineState s = EngineTests.CreateEngineState();
            void SingleTemplate(string rawCode, string msg, bool warn, ErrorCheck check = ErrorCheck.Success)
            {
                List<LogInfo> logs = EngineTests.Eval(s, rawCode, CodeType.Echo, check);
                if (check == ErrorCheck.Success)
                {
                    Assert.AreEqual(1, logs.Count);

                    LogInfo log = logs[0];
                    Assert.AreEqual(warn ? LogState.Warning : LogState.Success, log.State);
                    Assert.IsTrue(log.Message.Equals(msg, StringComparison.Ordinal));
                }
            }

            SingleTemplate(@"Echo,Hello World!", @"Hello World!", false, ErrorCheck.Success);
            SingleTemplate(@"Echo,PEBakery,WARN", @"PEBakery", true, ErrorCheck.Warning);
        }
        #endregion

        #region EchoFile
        [TestMethod]
        [TestCategory(nameof(Command))]
        [TestCategory(nameof(CommandInterface))]
        public void EchoFile()
        {
            EngineState s = EngineTests.CreateEngineState();

            string srcFile = Path.GetTempFileName();
            string noFile = FileHelper.GetTempFile();
            File.Delete(noFile);
            try
            {
                StringBuilder b = new StringBuilder();
                b.AppendLine("A");
                b.AppendLine("B");
                b.AppendLine("C");
                b.AppendLine("가");
                b.AppendLine("나");
                b.AppendLine("다");
                string sampleStr = b.ToString();

                using (StreamWriter w = new StreamWriter(srcFile, false, Encoding.UTF8))
                {
                    w.Write(sampleStr);
                }

                void SuccessTemplate(string rawCode, string msg, ErrorCheck check = ErrorCheck.Success)
                {
                    List<LogInfo> logs = EngineTests.Eval(s, rawCode, CodeType.EchoFile, check);
                    if (check == ErrorCheck.Success || check == ErrorCheck.Warning)
                    {
                        Assert.AreEqual(2, logs.Count);

                        if (check == ErrorCheck.Success)
                            Assert.AreEqual(LogState.Success, logs[0].State);
                        else if (check == ErrorCheck.Warning)
                            Assert.AreEqual(LogState.Warning, logs[0].State);
                        Assert.AreEqual(LogState.Info, logs[1].State);
                        Assert.IsTrue(logs[1].Message.Equals(msg.Trim(), StringComparison.Ordinal));
                    }
                }

                void FailTemplate(string rawCode)
                {
                    List<LogInfo> logs = EngineTests.Eval(s, rawCode, CodeType.EchoFile, ErrorCheck.Warning);
                    Assert.AreEqual(1, logs.Count);

                    LogInfo log = logs[0];
                    Assert.AreEqual(LogState.Warning, log.State);
                }

                SuccessTemplate($@"EchoFile,{srcFile}", sampleStr, ErrorCheck.Success);
                SuccessTemplate($@"EchoFile,{srcFile},WARN", sampleStr, ErrorCheck.Warning);
                FailTemplate($@"EchoFile,{noFile}");
            }
            finally
            {
                if (File.Exists(srcFile))
                    File.Delete(srcFile);
            }
        }
        #endregion
    }
}
