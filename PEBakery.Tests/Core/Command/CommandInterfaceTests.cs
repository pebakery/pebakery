using System;
using PEBakery.Core;
using System.IO;
using PEBakery.Helper;
using System.Text;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.IniLib;

namespace PEBakery.Tests.Core.Command
{
    [TestClass]
    public class CommandInterfaceTests
    {
        #region Const String
        private const string TestSuite_Interface = "Interface";
        private const string SrcDir = "Src";
        private const string DestDir_ReadInterface = "Dest_ReadInterface";
        private const string DestDir_WriteInterface = "Dest_WriteInterface";
        #endregion

        #region ReadInterface
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandInterface")]
        public void Interface_ReadInterface()
        { // ReadInterface,<Element>,<PluginFile>,<Section>,<Key>,<DestVar>
            // Element = Text, Visible, PosX, PosY, Width, Height, Value
            EngineState s = EngineTests.CreateEngineState();

            string scriptFile = Path.Combine("%ProjectDir%", TestSuite_Interface, "ReadInterface.script");

            // 0 - TextBox
            ReadInterface_Template(s, $@"ReadInterface,Text,{scriptFile},Interface,pTextBox1,%Dest%", @"Display");
            ReadInterface_Template(s, $@"ReadInterface,Visible,{scriptFile},Interface,pTextBox1,%Dest%", @"True");
            ReadInterface_Template(s, $@"ReadInterface,PosX,{scriptFile},Interface,pTextBox1,%Dest%", @"20");
            ReadInterface_Template(s, $@"ReadInterface,PosY,{scriptFile},Interface,pTextBox1,%Dest%", @"20");
            ReadInterface_Template(s, $@"ReadInterface,Width,{scriptFile},Interface,pTextBox1,%Dest%", @"200");
            ReadInterface_Template(s, $@"ReadInterface,Height,{scriptFile},Interface,pTextBox1,%Dest%", @"21");
            ReadInterface_Template(s, $@"ReadInterface,Value,{scriptFile},Interface,pTextBox1,%Dest%", @"StringValue");

            // 1 - TextLabel
            ReadInterface_Template(s, $@"ReadInterface,Text,{scriptFile},Interface,pTextLabel1,%Dest%", @"Display");
            ReadInterface_Template(s, $@"ReadInterface,Visible,{scriptFile},Interface,pTextLabel1,%Dest%", @"True");
            ReadInterface_Template(s, $@"ReadInterface,Visible,{scriptFile},Interface,pTextLabel2,%Dest%", @"False");
            ReadInterface_Template(s, $@"ReadInterface,PosX,{scriptFile},Interface,pTextLabel1,%Dest%", @"20");
            ReadInterface_Template(s, $@"ReadInterface,PosY,{scriptFile},Interface,pTextLabel1,%Dest%", @"50");
            ReadInterface_Template(s, $@"ReadInterface,Width,{scriptFile},Interface,pTextLabel1,%Dest%", @"230");
            ReadInterface_Template(s, $@"ReadInterface,Height,{scriptFile},Interface,pTextLabel1,%Dest%", @"18");
            ReadInterface_Template(s, $@"ReadInterface,Value,{scriptFile},Interface,pTextLabel1,%Dest%", null, ErrorCheck.Error);

            // 2 - NumberBox
            ReadInterface_Template(s, $@"ReadInterface,Text,{scriptFile},Interface,pNumberBox1,%Dest%", @"pNumberBox1");
            ReadInterface_Template(s, $@"ReadInterface,Visible,{scriptFile},Interface,pNumberBox1,%Dest%", @"True");
            ReadInterface_Template(s, $@"ReadInterface,PosX,{scriptFile},Interface,pNumberBox1,%Dest%", @"20");
            ReadInterface_Template(s, $@"ReadInterface,PosY,{scriptFile},Interface,pNumberBox1,%Dest%", @"70");
            ReadInterface_Template(s, $@"ReadInterface,Width,{scriptFile},Interface,pNumberBox1,%Dest%", @"40");
            ReadInterface_Template(s, $@"ReadInterface,Height,{scriptFile},Interface,pNumberBox1,%Dest%", @"22");
            ReadInterface_Template(s, $@"ReadInterface,Value,{scriptFile},Interface,pNumberBox1,%Dest%", @"3");

            // 3 - CheckBox
            ReadInterface_Template(s, $@"ReadInterface,Text,{scriptFile},Interface,pCheckBox1,%Dest%", @"pCheckBox1");
            ReadInterface_Template(s, $@"ReadInterface,Visible,{scriptFile},Interface,pCheckBox1,%Dest%", @"True");
            ReadInterface_Template(s, $@"ReadInterface,PosX,{scriptFile},Interface,pCheckBox1,%Dest%", @"20");
            ReadInterface_Template(s, $@"ReadInterface,PosY,{scriptFile},Interface,pCheckBox1,%Dest%", @"100");
            ReadInterface_Template(s, $@"ReadInterface,Width,{scriptFile},Interface,pCheckBox1,%Dest%", @"200");
            ReadInterface_Template(s, $@"ReadInterface,Height,{scriptFile},Interface,pCheckBox1,%Dest%", @"18");
            ReadInterface_Template(s, $@"ReadInterface,Value,{scriptFile},Interface,pCheckBox1,%Dest%", @"True");

            // 4 - ComboBox
            ReadInterface_Template(s, $@"ReadInterface,Text,{scriptFile},Interface,pComboBox1,%Dest%", @"A");
            ReadInterface_Template(s, $@"ReadInterface,Visible,{scriptFile},Interface,pComboBox1,%Dest%", @"True");
            ReadInterface_Template(s, $@"ReadInterface,PosX,{scriptFile},Interface,pComboBox1,%Dest%", @"20");
            ReadInterface_Template(s, $@"ReadInterface,PosY,{scriptFile},Interface,pComboBox1,%Dest%", @"130");
            ReadInterface_Template(s, $@"ReadInterface,Width,{scriptFile},Interface,pComboBox1,%Dest%", @"150");
            ReadInterface_Template(s, $@"ReadInterface,Height,{scriptFile},Interface,pComboBox1,%Dest%", @"21");
            ReadInterface_Template(s, $@"ReadInterface,Value,{scriptFile},Interface,pComboBox1,%Dest%", @"A");

            // 5 - Image
            ReadInterface_Template(s, $@"ReadInterface,Text,{scriptFile},Interface,pImage1,%Dest%", @"Logo.jpg");
            ReadInterface_Template(s, $@"ReadInterface,Visible,{scriptFile},Interface,pImage1,%Dest%", @"True");
            ReadInterface_Template(s, $@"ReadInterface,PosX,{scriptFile},Interface,pImage1,%Dest%", @"20");
            ReadInterface_Template(s, $@"ReadInterface,PosY,{scriptFile},Interface,pImage1,%Dest%", @"230");
            ReadInterface_Template(s, $@"ReadInterface,Width,{scriptFile},Interface,pImage1,%Dest%", @"40");
            ReadInterface_Template(s, $@"ReadInterface,Height,{scriptFile},Interface,pImage1,%Dest%", @"40");
            ReadInterface_Template(s, $@"ReadInterface,Value,{scriptFile},Interface,pImage1,%Dest%", null, ErrorCheck.Error);

            // 6 - TextFile
            ReadInterface_Template(s, $@"ReadInterface,Text,{scriptFile},Interface,pTextFile1,%Dest%", @"HelpMsg.txt");
            ReadInterface_Template(s, $@"ReadInterface,Visible,{scriptFile},Interface,pTextFile1,%Dest%", @"True");
            ReadInterface_Template(s, $@"ReadInterface,PosX,{scriptFile},Interface,pTextFile1,%Dest%", @"240");
            ReadInterface_Template(s, $@"ReadInterface,PosY,{scriptFile},Interface,pTextFile1,%Dest%", @"20");
            ReadInterface_Template(s, $@"ReadInterface,Width,{scriptFile},Interface,pTextFile1,%Dest%", @"200");
            ReadInterface_Template(s, $@"ReadInterface,Height,{scriptFile},Interface,pTextFile1,%Dest%", @"86");
            ReadInterface_Template(s, $@"ReadInterface,Value,{scriptFile},Interface,pTextFile1,%Dest%", null, ErrorCheck.Error);

            // 8 - Button
            ReadInterface_Template(s, $@"ReadInterface,Text,{scriptFile},Interface,pButton1,%Dest%", @"ShowProgress");
            ReadInterface_Template(s, $@"ReadInterface,Text,{scriptFile},Interface,pButton2,%Dest%", @"HideProgress");
            ReadInterface_Template(s, $@"ReadInterface,Visible,{scriptFile},Interface,pButton1,%Dest%", @"True");
            ReadInterface_Template(s, $@"ReadInterface,PosX,{scriptFile},Interface,pButton1,%Dest%", @"240");
            ReadInterface_Template(s, $@"ReadInterface,PosY,{scriptFile},Interface,pButton1,%Dest%", @"115");
            ReadInterface_Template(s, $@"ReadInterface,Width,{scriptFile},Interface,pButton1,%Dest%", @"80");
            ReadInterface_Template(s, $@"ReadInterface,Height,{scriptFile},Interface,pButton1,%Dest%", @"25");
            ReadInterface_Template(s, $@"ReadInterface,Value,{scriptFile},Interface,pButton1,%Dest%", null, ErrorCheck.Error);

            // 10 - WebLabel
            ReadInterface_Template(s, $@"ReadInterface,Text,{scriptFile},Interface,pWebLabel1,%Dest%", @"GitHub");
            ReadInterface_Template(s, $@"ReadInterface,Visible,{scriptFile},Interface,pWebLabel1,%Dest%", @"True");
            ReadInterface_Template(s, $@"ReadInterface,PosX,{scriptFile},Interface,pWebLabel1,%Dest%", @"250");
            ReadInterface_Template(s, $@"ReadInterface,PosY,{scriptFile},Interface,pWebLabel1,%Dest%", @"160");
            ReadInterface_Template(s, $@"ReadInterface,Width,{scriptFile},Interface,pWebLabel1,%Dest%", @"32");
            ReadInterface_Template(s, $@"ReadInterface,Height,{scriptFile},Interface,pWebLabel1,%Dest%", @"18");
            ReadInterface_Template(s, $@"ReadInterface,Value,{scriptFile},Interface,pWebLabel1,%Dest%", null, ErrorCheck.Error);

            // 11 - RadioButton
            ReadInterface_Template(s, $@"ReadInterface,Text,{scriptFile},Interface,pRadioButton1,%Dest%", @"pRadioButton1");
            ReadInterface_Template(s, $@"ReadInterface,Visible,{scriptFile},Interface,pRadioButton1,%Dest%", @"True");
            ReadInterface_Template(s, $@"ReadInterface,PosX,{scriptFile},Interface,pRadioButton1,%Dest%", @"250");
            ReadInterface_Template(s, $@"ReadInterface,PosY,{scriptFile},Interface,pRadioButton1,%Dest%", @"180");
            ReadInterface_Template(s, $@"ReadInterface,Width,{scriptFile},Interface,pRadioButton1,%Dest%", @"100");
            ReadInterface_Template(s, $@"ReadInterface,Height,{scriptFile},Interface,pRadioButton1,%Dest%", @"20");
            ReadInterface_Template(s, $@"ReadInterface,Value,{scriptFile},Interface,pRadioButton1,%Dest%", @"False");

            // 12 - Bevel
            ReadInterface_Template(s, $@"ReadInterface,Text,{scriptFile},Interface,pBevel1,%Dest%", @"pBevel1");
            ReadInterface_Template(s, $@"ReadInterface,Visible,{scriptFile},Interface,pBevel1,%Dest%", @"True");
            ReadInterface_Template(s, $@"ReadInterface,PosX,{scriptFile},Interface,pBevel1,%Dest%", @"240");
            ReadInterface_Template(s, $@"ReadInterface,PosY,{scriptFile},Interface,pBevel1,%Dest%", @"150");
            ReadInterface_Template(s, $@"ReadInterface,Width,{scriptFile},Interface,pBevel1,%Dest%", @"235");
            ReadInterface_Template(s, $@"ReadInterface,Height,{scriptFile},Interface,pBevel1,%Dest%", @"60");
            ReadInterface_Template(s, $@"ReadInterface,Value,{scriptFile},Interface,pBevel1,%Dest%", null, ErrorCheck.Error);

            // 13 - FileBox
            ReadInterface_Template(s, $@"ReadInterface,Text,{scriptFile},Interface,pFileBox1,%Dest%", @"C:\Windows\notepad.exe");
            ReadInterface_Template(s, $@"ReadInterface,Visible,{scriptFile},Interface,pFileBox1,%Dest%", @"True");
            ReadInterface_Template(s, $@"ReadInterface,PosX,{scriptFile},Interface,pFileBox1,%Dest%", @"240");
            ReadInterface_Template(s, $@"ReadInterface,PosY,{scriptFile},Interface,pFileBox1,%Dest%", @"230");
            ReadInterface_Template(s, $@"ReadInterface,Width,{scriptFile},Interface,pFileBox1,%Dest%", @"200");
            ReadInterface_Template(s, $@"ReadInterface,Height,{scriptFile},Interface,pFileBox1,%Dest%", @"20");
            ReadInterface_Template(s, $@"ReadInterface,Value,{scriptFile},Interface,pFileBox1,%Dest%", @"C:\Windows\notepad.exe");

            ReadInterface_Template(s, $@"ReadInterface,Text,{scriptFile},Interface,pFileBox2,%Dest%", @"E:\WinPE\");
            ReadInterface_Template(s, $@"ReadInterface,Visible,{scriptFile},Interface,pFileBox2,%Dest%", @"True");
            ReadInterface_Template(s, $@"ReadInterface,PosX,{scriptFile},Interface,pFileBox2,%Dest%", @"240");
            ReadInterface_Template(s, $@"ReadInterface,PosY,{scriptFile},Interface,pFileBox2,%Dest%", @"260");
            ReadInterface_Template(s, $@"ReadInterface,Width,{scriptFile},Interface,pFileBox2,%Dest%", @"200");
            ReadInterface_Template(s, $@"ReadInterface,Height,{scriptFile},Interface,pFileBox2,%Dest%", @"20");
            ReadInterface_Template(s, $@"ReadInterface,Value,{scriptFile},Interface,pFileBox2,%Dest%", @"E:\WinPE\");

            // 14 - RadioGroup
            ReadInterface_Template(s, $@"ReadInterface,Text,{scriptFile},Interface,pRadioGroup1,%Dest%", @"pRadioGroup1");
            ReadInterface_Template(s, $@"ReadInterface,Visible,{scriptFile},Interface,pRadioGroup1,%Dest%", @"True");
            ReadInterface_Template(s, $@"ReadInterface,PosX,{scriptFile},Interface,pRadioGroup1,%Dest%", @"20");
            ReadInterface_Template(s, $@"ReadInterface,PosY,{scriptFile},Interface,pRadioGroup1,%Dest%", @"160");
            ReadInterface_Template(s, $@"ReadInterface,Width,{scriptFile},Interface,pRadioGroup1,%Dest%", @"150");
            ReadInterface_Template(s, $@"ReadInterface,Height,{scriptFile},Interface,pRadioGroup1,%Dest%", @"60");
            ReadInterface_Template(s, $@"ReadInterface,Value,{scriptFile},Interface,pRadioGroup1,%Dest%", @"0");
        }

        public void ReadInterface_Template(EngineState s, string rawCode, string comp, ErrorCheck check = ErrorCheck.Success)
        {
            EngineTests.Eval(s, rawCode, CodeType.ReadInterface, check);
            if (check == ErrorCheck.Success)
            {
                string dest = s.Variables["Dest"];
                Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
            }
        }
        #endregion

        #region WriteInterface
        [TestMethod]
        [TestCategory("Command")]
        [TestCategory("CommandInterface")]
        public void Interface_WriteInterface()
        { // WriteInterface,<Element>,<PluginFile>,<Section>,<Key>,<Value>
            // Element = Text, Visible, PosX, PosY, Width, Height, Value
            EngineState s = EngineTests.CreateEngineState();

            string scriptFile = Path.Combine("%ProjectTemp%", "WriteInterface.script");

            // 0 - TextBox
            WriteInterface_Template(s, $@"WriteInterface,Text,{scriptFile},Interface,pTextBox1,PEBakery", @"pTextBox1",
                @"PEBakery,1,0,20,20,200,21,StringValue");
            WriteInterface_Template(s, $@"WriteInterface,Visible,{scriptFile},Interface,pTextBox1,False", @"pTextBox1",
                @"Display,0,0,20,20,200,21,StringValue");
            WriteInterface_Template(s, $@"WriteInterface,PosX,{scriptFile},Interface,pTextBox1,30", @"pTextBox1",
                @"Display,1,0,30,20,200,21,StringValue");
            WriteInterface_Template(s, $@"WriteInterface,PosY,{scriptFile},Interface,pTextBox1,30", @"pTextBox1",
                @"Display,1,0,20,30,200,21,StringValue");
            WriteInterface_Template(s, $@"WriteInterface,Width,{scriptFile},Interface,pTextBox1,30", @"pTextBox1",
                @"Display,1,0,20,20,30,21,StringValue");
            WriteInterface_Template(s, $@"WriteInterface,Height,{scriptFile},Interface,pTextBox1,10", @"pTextBox1",
                @"Display,1,0,20,20,200,10,StringValue");
            WriteInterface_Template(s, $@"WriteInterface,Value,{scriptFile},Interface,pTextBox1,PEBakery", @"pTextBox1",
                @"Display,1,0,20,20,200,21,PEBakery");

            // 1 - TextLabel
            WriteInterface_Template(s, $@"WriteInterface,Value,{scriptFile},Interface,pTextLabel1,PEBakery", @"pTextLabel1", 
                null, ErrorCheck.Error);

            // 2 - NumberBox
            WriteInterface_Template(s, $@"WriteInterface,Value,{scriptFile},Interface,pNumberBox1,2", @"pNumberBox1",
                @"pNumberBox1,1,2,20,70,40,22,2,0,100,1");
            WriteInterface_Template(s, $@"WriteInterface,Value,{scriptFile},Interface,pNumberBox1,200", @"pNumberBox1",
                null, ErrorCheck.Error);
            WriteInterface_Template(s, $@"WriteInterface,Value,{scriptFile},Interface,pNumberBox1,Str", @"pNumberBox1",
                null, ErrorCheck.Error);
            
            // 3 - CheckBox
            WriteInterface_Template(s, $@"WriteInterface,Value,{scriptFile},Interface,pCheckBox1,False", @"pCheckBox1",
                @"pCheckBox1,1,3,20,100,200,18,False");

            // 4 - ComboBox
            WriteInterface_Template(s, $@"WriteInterface,Value,{scriptFile},Interface,pComboBox1,B", @"pComboBox1",
                @"B,1,4,20,130,150,21,A,B,C,D");
            WriteInterface_Template(s, $@"WriteInterface,Value,{scriptFile},Interface,pComboBox1,E", @"pComboBox1",
                null, ErrorCheck.Error);

            // 5 - Image
            WriteInterface_Template(s, $@"WriteInterface,Value,{scriptFile},Interface,pImage1,PEBakery", @"pImage1", 
                null, ErrorCheck.Error);

            // 6 - TextFile
            WriteInterface_Template(s, $@"WriteInterface,Value,{scriptFile},Interface,pTextFile1,PEBakery", @"pTextFile1",
                null, ErrorCheck.Error);

            // 8 - Button
            WriteInterface_Template(s, $@"WriteInterface,Value,{scriptFile},Interface,pButton1,PEBakery", @"pButton1", 
                null, ErrorCheck.Error);

            // 10 - WebLabel
            WriteInterface_Template(s, $@"WriteInterface,Value,{scriptFile},Interface,pWebLabel1,PEBakery", @"pWebLabel1",
                null, ErrorCheck.Error);

            // 11 - RadioButton
            WriteInterface_Template(s, $@"WriteInterface,Value,{scriptFile},Interface,pRadioButton1,True", @"pRadioButton1",
                @"pRadioButton1,1,11,250,180,100,20,True");

            // 12 - Bevel
            WriteInterface_Template(s, $@"WriteInterface,Value,{scriptFile},Interface,pBevel1,PEBakery", @"pBevel1",
                null, ErrorCheck.Error);

            // 13 - FileBox
            WriteInterface_Template(s, $@"WriteInterface,Value,{scriptFile},Interface,pFileBox1,D:\PEBakery\Launcher.exe", @"pFileBox1",
                @"D:\PEBakery\Launcher.exe,1,13,240,230,200,20,file");

            // 14 - RadioGroup
            WriteInterface_Template(s, $@"WriteInterface,Value,{scriptFile},Interface,pRadioGroup1,2", @"pRadioGroup1",
                @"pRadioGroup1,1,14,20,160,150,60,Option1,Option2,Option3,2");
            WriteInterface_Template(s, $@"WriteInterface,Value,{scriptFile},Interface,pRadioGroup1,3", @"pRadioGroup1",
                null, ErrorCheck.Error);
        }

        public void WriteInterface_Template(EngineState s, string rawCode, string key, string comp, ErrorCheck check = ErrorCheck.Success)
        {
            string srcFile = StringEscaper.Preprocess(s, Path.Combine("%ProjectDir%", TestSuite_Interface, "ReadInterface.script"));
            string destFile = StringEscaper.Preprocess(s, Path.Combine("%ProjectTemp%", "WriteInterface.script"));

            File.Copy(srcFile, destFile, true);
            try
            {
                EngineTests.Eval(s, rawCode, CodeType.WriteInterface, check);
                if (check == ErrorCheck.Success)
                {
                    string dest = Ini.GetKey(destFile, "Interface", key);
                    Assert.IsTrue(dest.Equals(comp, StringComparison.Ordinal));
                }
            }
            finally
            {
                if (File.Exists(destFile))
                    File.Delete(destFile);
            }
        }
        #endregion
    }
}
