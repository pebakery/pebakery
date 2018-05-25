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
            ReadInterface_Template(s, $@"ReadInterface,ToolTip,{scriptFile},Interface,pTextBox1,%Dest%", string.Empty);

            // 1 - TextLabel
            ReadInterface_Template(s, $@"ReadInterface,Text,{scriptFile},Interface,pTextLabel1,%Dest%", @"Display");
            ReadInterface_Template(s, $@"ReadInterface,Visible,{scriptFile},Interface,pTextLabel1,%Dest%", @"True");
            ReadInterface_Template(s, $@"ReadInterface,PosX,{scriptFile},Interface,pTextLabel1,%Dest%", @"20");
            ReadInterface_Template(s, $@"ReadInterface,PosY,{scriptFile},Interface,pTextLabel1,%Dest%", @"50");
            ReadInterface_Template(s, $@"ReadInterface,Width,{scriptFile},Interface,pTextLabel1,%Dest%", @"230");
            ReadInterface_Template(s, $@"ReadInterface,Height,{scriptFile},Interface,pTextLabel1,%Dest%", @"18");
            ReadInterface_Template(s, $@"ReadInterface,Value,{scriptFile},Interface,pTextLabel1,%Dest%", null, ErrorCheck.Error);
            ReadInterface_Template(s, $@"ReadInterface,ToolTip,{scriptFile},Interface,pTextLabel1,%Dest%", string.Empty);
            ReadInterface_Template(s, $@"ReadInterface,FontSize,{scriptFile},Interface,pTextLabel1,%Dest%", @"8");
            ReadInterface_Template(s, $@"ReadInterface,FontWeight,{scriptFile},Interface,pTextLabel1,%Dest%", @"Normal");

            // 2 - NumberBox
            ReadInterface_Template(s, $@"ReadInterface,Text,{scriptFile},Interface,pNumberBox1,%Dest%", @"pNumberBox1");
            ReadInterface_Template(s, $@"ReadInterface,Visible,{scriptFile},Interface,pNumberBox1,%Dest%", @"True");
            ReadInterface_Template(s, $@"ReadInterface,PosX,{scriptFile},Interface,pNumberBox1,%Dest%", @"20");
            ReadInterface_Template(s, $@"ReadInterface,PosY,{scriptFile},Interface,pNumberBox1,%Dest%", @"70");
            ReadInterface_Template(s, $@"ReadInterface,Width,{scriptFile},Interface,pNumberBox1,%Dest%", @"40");
            ReadInterface_Template(s, $@"ReadInterface,Height,{scriptFile},Interface,pNumberBox1,%Dest%", @"22");
            ReadInterface_Template(s, $@"ReadInterface,Value,{scriptFile},Interface,pNumberBox1,%Dest%", @"3");
            ReadInterface_Template(s, $@"ReadInterface,ToolTip,{scriptFile},Interface,pNumberBox1,%Dest%", string.Empty);
            ReadInterface_Template(s, $@"ReadInterface,NumberMin,{scriptFile},Interface,pNumberBox1,%Dest%", @"0");
            ReadInterface_Template(s, $@"ReadInterface,NumberMax,{scriptFile},Interface,pNumberBox1,%Dest%", @"100");
            ReadInterface_Template(s, $@"ReadInterface,NumberTick,{scriptFile},Interface,pNumberBox1,%Dest%", @"1");

            // 3 - CheckBox
            ReadInterface_Template(s, $@"ReadInterface,Text,{scriptFile},Interface,pCheckBox1,%Dest%", @"pCheckBox1");
            ReadInterface_Template(s, $@"ReadInterface,Visible,{scriptFile},Interface,pCheckBox1,%Dest%", @"True");
            ReadInterface_Template(s, $@"ReadInterface,PosX,{scriptFile},Interface,pCheckBox1,%Dest%", @"20");
            ReadInterface_Template(s, $@"ReadInterface,PosY,{scriptFile},Interface,pCheckBox1,%Dest%", @"100");
            ReadInterface_Template(s, $@"ReadInterface,Width,{scriptFile},Interface,pCheckBox1,%Dest%", @"200");
            ReadInterface_Template(s, $@"ReadInterface,Height,{scriptFile},Interface,pCheckBox1,%Dest%", @"18");
            ReadInterface_Template(s, $@"ReadInterface,Value,{scriptFile},Interface,pCheckBox1,%Dest%", @"True");
            ReadInterface_Template(s, $@"ReadInterface,ToolTip,{scriptFile},Interface,pCheckBox1,%Dest%", string.Empty);
            ReadInterface_Template(s, $@"ReadInterface,SectionName,{scriptFile},Interface,pCheckBox1,%Dest%", string.Empty);
            ReadInterface_Template(s, $@"ReadInterface,HideProgress,{scriptFile},Interface,pCheckBox1,%Dest%", "None");

            // 4 - ComboBox
            ReadInterface_Template(s, $@"ReadInterface,Text,{scriptFile},Interface,pComboBox1,%Dest%", @"A");
            ReadInterface_Template(s, $@"ReadInterface,Visible,{scriptFile},Interface,pComboBox1,%Dest%", @"True");
            ReadInterface_Template(s, $@"ReadInterface,PosX,{scriptFile},Interface,pComboBox1,%Dest%", @"20");
            ReadInterface_Template(s, $@"ReadInterface,PosY,{scriptFile},Interface,pComboBox1,%Dest%", @"130");
            ReadInterface_Template(s, $@"ReadInterface,Width,{scriptFile},Interface,pComboBox1,%Dest%", @"150");
            ReadInterface_Template(s, $@"ReadInterface,Height,{scriptFile},Interface,pComboBox1,%Dest%", @"21");
            ReadInterface_Template(s, $@"ReadInterface,Value,{scriptFile},Interface,pComboBox1,%Dest%", @"A");
            ReadInterface_Template(s, $@"ReadInterface,ToolTip,{scriptFile},Interface,pComboBox1,%Dest%", string.Empty);
            ReadInterface_Template(s, $@"ReadInterface,Items,{scriptFile},Interface,pComboBox1,%Dest%", @"A|B|C|D");
            ReadInterface_Template(s, $@"ReadInterface,SectionName,{scriptFile},Interface,pComboBox1,%Dest%", string.Empty);
            ReadInterface_Template(s, $@"ReadInterface,HideProgress,{scriptFile},Interface,pComboBox1,%Dest%", "None");

            // 5 - Image
            ReadInterface_Template(s, $@"ReadInterface,Text,{scriptFile},Interface,pImage1,%Dest%", @"Logo.jpg");
            ReadInterface_Template(s, $@"ReadInterface,Visible,{scriptFile},Interface,pImage1,%Dest%", @"True");
            ReadInterface_Template(s, $@"ReadInterface,PosX,{scriptFile},Interface,pImage1,%Dest%", @"20");
            ReadInterface_Template(s, $@"ReadInterface,PosY,{scriptFile},Interface,pImage1,%Dest%", @"230");
            ReadInterface_Template(s, $@"ReadInterface,Width,{scriptFile},Interface,pImage1,%Dest%", @"40");
            ReadInterface_Template(s, $@"ReadInterface,Height,{scriptFile},Interface,pImage1,%Dest%", @"40");
            ReadInterface_Template(s, $@"ReadInterface,Value,{scriptFile},Interface,pImage1,%Dest%", null, ErrorCheck.Error);
            ReadInterface_Template(s, $@"ReadInterface,ToolTip,{scriptFile},Interface,pImage1,%Dest%", string.Empty);
            ReadInterface_Template(s, $@"ReadInterface,Url,{scriptFile},Interface,pImage1,%Dest%", string.Empty);

            // 6 - TextFile
            ReadInterface_Template(s, $@"ReadInterface,Text,{scriptFile},Interface,pTextFile1,%Dest%", @"HelpMsg.txt");
            ReadInterface_Template(s, $@"ReadInterface,Visible,{scriptFile},Interface,pTextFile1,%Dest%", @"True");
            ReadInterface_Template(s, $@"ReadInterface,PosX,{scriptFile},Interface,pTextFile1,%Dest%", @"240");
            ReadInterface_Template(s, $@"ReadInterface,PosY,{scriptFile},Interface,pTextFile1,%Dest%", @"20");
            ReadInterface_Template(s, $@"ReadInterface,Width,{scriptFile},Interface,pTextFile1,%Dest%", @"200");
            ReadInterface_Template(s, $@"ReadInterface,Height,{scriptFile},Interface,pTextFile1,%Dest%", @"86");
            ReadInterface_Template(s, $@"ReadInterface,Value,{scriptFile},Interface,pTextFile1,%Dest%", null, ErrorCheck.Error);
            ReadInterface_Template(s, $@"ReadInterface,ToolTip,{scriptFile},Interface,pTextFile1,%Dest%", string.Empty);

            // 8 - Button
            ReadInterface_Template(s, $@"ReadInterface,Text,{scriptFile},Interface,pButton1,%Dest%", @"ShowProgress");
            ReadInterface_Template(s, $@"ReadInterface,Text,{scriptFile},Interface,pButton2,%Dest%", @"HideProgress");
            ReadInterface_Template(s, $@"ReadInterface,Visible,{scriptFile},Interface,pButton1,%Dest%", @"True");
            ReadInterface_Template(s, $@"ReadInterface,PosX,{scriptFile},Interface,pButton1,%Dest%", @"240");
            ReadInterface_Template(s, $@"ReadInterface,PosY,{scriptFile},Interface,pButton1,%Dest%", @"115");
            ReadInterface_Template(s, $@"ReadInterface,Width,{scriptFile},Interface,pButton1,%Dest%", @"80");
            ReadInterface_Template(s, $@"ReadInterface,Height,{scriptFile},Interface,pButton1,%Dest%", @"25");
            ReadInterface_Template(s, $@"ReadInterface,Value,{scriptFile},Interface,pButton1,%Dest%", null, ErrorCheck.Error);
            ReadInterface_Template(s, $@"ReadInterface,ToolTip,{scriptFile},Interface,pButton1,%Dest%", string.Empty);
            ReadInterface_Template(s, $@"ReadInterface,SectionName,{scriptFile},Interface,pButton1,%Dest%", @"Hello");
            ReadInterface_Template(s, $@"ReadInterface,HideProgress,{scriptFile},Interface,pButton1,%Dest%", @"False");

            // 10 - WebLabel
            ReadInterface_Template(s, $@"ReadInterface,Text,{scriptFile},Interface,pWebLabel1,%Dest%", @"GitHub");
            ReadInterface_Template(s, $@"ReadInterface,Visible,{scriptFile},Interface,pWebLabel1,%Dest%", @"True");
            ReadInterface_Template(s, $@"ReadInterface,PosX,{scriptFile},Interface,pWebLabel1,%Dest%", @"250");
            ReadInterface_Template(s, $@"ReadInterface,PosY,{scriptFile},Interface,pWebLabel1,%Dest%", @"160");
            ReadInterface_Template(s, $@"ReadInterface,Width,{scriptFile},Interface,pWebLabel1,%Dest%", @"32");
            ReadInterface_Template(s, $@"ReadInterface,Height,{scriptFile},Interface,pWebLabel1,%Dest%", @"18");
            ReadInterface_Template(s, $@"ReadInterface,Value,{scriptFile},Interface,pWebLabel1,%Dest%", null, ErrorCheck.Error);
            ReadInterface_Template(s, $@"ReadInterface,ToolTip,{scriptFile},Interface,pWebLabel1,%Dest%", string.Empty);
            ReadInterface_Template(s, $@"ReadInterface,Url,{scriptFile},Interface,pWebLabel1,%Dest%", @"https://github.com/pebakery/PEBakery");

            // 11 - RadioButton
            ReadInterface_Template(s, $@"ReadInterface,Text,{scriptFile},Interface,pRadioButton1,%Dest%", @"pRadioButton1");
            ReadInterface_Template(s, $@"ReadInterface,Visible,{scriptFile},Interface,pRadioButton1,%Dest%", @"True");
            ReadInterface_Template(s, $@"ReadInterface,PosX,{scriptFile},Interface,pRadioButton1,%Dest%", @"250");
            ReadInterface_Template(s, $@"ReadInterface,PosY,{scriptFile},Interface,pRadioButton1,%Dest%", @"180");
            ReadInterface_Template(s, $@"ReadInterface,Width,{scriptFile},Interface,pRadioButton1,%Dest%", @"100");
            ReadInterface_Template(s, $@"ReadInterface,Height,{scriptFile},Interface,pRadioButton1,%Dest%", @"20");
            ReadInterface_Template(s, $@"ReadInterface,Value,{scriptFile},Interface,pRadioButton1,%Dest%", @"False");
            ReadInterface_Template(s, $@"ReadInterface,ToolTip,{scriptFile},Interface,pRadioButton1,%Dest%", string.Empty);
            ReadInterface_Template(s, $@"ReadInterface,SectionName,{scriptFile},Interface,pRadioButton1,%Dest%", string.Empty);
            ReadInterface_Template(s, $@"ReadInterface,HideProgress,{scriptFile},Interface,pRadioButton1,%Dest%", "None");

            // 12 - Bevel
            ReadInterface_Template(s, $@"ReadInterface,Text,{scriptFile},Interface,pBevel1,%Dest%", @"pBevel1");
            ReadInterface_Template(s, $@"ReadInterface,Visible,{scriptFile},Interface,pBevel1,%Dest%", @"True");
            ReadInterface_Template(s, $@"ReadInterface,PosX,{scriptFile},Interface,pBevel1,%Dest%", @"240");
            ReadInterface_Template(s, $@"ReadInterface,PosY,{scriptFile},Interface,pBevel1,%Dest%", @"150");
            ReadInterface_Template(s, $@"ReadInterface,Width,{scriptFile},Interface,pBevel1,%Dest%", @"235");
            ReadInterface_Template(s, $@"ReadInterface,Height,{scriptFile},Interface,pBevel1,%Dest%", @"60");
            ReadInterface_Template(s, $@"ReadInterface,Value,{scriptFile},Interface,pBevel1,%Dest%", null, ErrorCheck.Error);
            ReadInterface_Template(s, $@"ReadInterface,ToolTip,{scriptFile},Interface,pBevel1,%Dest%", string.Empty);

            // 13 - FileBox
            ReadInterface_Template(s, $@"ReadInterface,Text,{scriptFile},Interface,pFileBox1,%Dest%", @"C:\Windows\notepad.exe");
            ReadInterface_Template(s, $@"ReadInterface,Visible,{scriptFile},Interface,pFileBox1,%Dest%", @"True");
            ReadInterface_Template(s, $@"ReadInterface,PosX,{scriptFile},Interface,pFileBox1,%Dest%", @"240");
            ReadInterface_Template(s, $@"ReadInterface,PosY,{scriptFile},Interface,pFileBox1,%Dest%", @"230");
            ReadInterface_Template(s, $@"ReadInterface,Width,{scriptFile},Interface,pFileBox1,%Dest%", @"200");
            ReadInterface_Template(s, $@"ReadInterface,Height,{scriptFile},Interface,pFileBox1,%Dest%", @"20");
            ReadInterface_Template(s, $@"ReadInterface,Value,{scriptFile},Interface,pFileBox1,%Dest%", @"C:\Windows\notepad.exe");
            ReadInterface_Template(s, $@"ReadInterface,ToolTip,{scriptFile},Interface,pFileBox1,%Dest%", string.Empty);

            ReadInterface_Template(s, $@"ReadInterface,Text,{scriptFile},Interface,pFileBox2,%Dest%", @"E:\WinPE\");
            ReadInterface_Template(s, $@"ReadInterface,Visible,{scriptFile},Interface,pFileBox2,%Dest%", @"True");
            ReadInterface_Template(s, $@"ReadInterface,PosX,{scriptFile},Interface,pFileBox2,%Dest%", @"240");
            ReadInterface_Template(s, $@"ReadInterface,PosY,{scriptFile},Interface,pFileBox2,%Dest%", @"260");
            ReadInterface_Template(s, $@"ReadInterface,Width,{scriptFile},Interface,pFileBox2,%Dest%", @"200");
            ReadInterface_Template(s, $@"ReadInterface,Height,{scriptFile},Interface,pFileBox2,%Dest%", @"20");
            ReadInterface_Template(s, $@"ReadInterface,Value,{scriptFile},Interface,pFileBox2,%Dest%", @"E:\WinPE\");
            ReadInterface_Template(s, $@"ReadInterface,ToolTip,{scriptFile},Interface,pFileBox2,%Dest%", string.Empty);

            // 14 - RadioGroup
            ReadInterface_Template(s, $@"ReadInterface,Text,{scriptFile},Interface,pRadioGroup1,%Dest%", @"pRadioGroup1");
            ReadInterface_Template(s, $@"ReadInterface,Visible,{scriptFile},Interface,pRadioGroup1,%Dest%", @"True");
            ReadInterface_Template(s, $@"ReadInterface,PosX,{scriptFile},Interface,pRadioGroup1,%Dest%", @"20");
            ReadInterface_Template(s, $@"ReadInterface,PosY,{scriptFile},Interface,pRadioGroup1,%Dest%", @"160");
            ReadInterface_Template(s, $@"ReadInterface,Width,{scriptFile},Interface,pRadioGroup1,%Dest%", @"150");
            ReadInterface_Template(s, $@"ReadInterface,Height,{scriptFile},Interface,pRadioGroup1,%Dest%", @"60");
            ReadInterface_Template(s, $@"ReadInterface,Value,{scriptFile},Interface,pRadioGroup1,%Dest%", @"0");
            ReadInterface_Template(s, $@"ReadInterface,Items,{scriptFile},Interface,pRadioGroup1,%Dest%", @"Option1|Option2|Option3");
            ReadInterface_Template(s, $@"ReadInterface,SectionName,{scriptFile},Interface,pRadioGroup1,%Dest%", string.Empty);
            ReadInterface_Template(s, $@"ReadInterface,HideProgress,{scriptFile},Interface,pRadioGroup1,%Dest%", "None");

            // Visible - False
            ReadInterface_Template(s, $@"ReadInterface,Visible,{scriptFile},Interface,pTextLabel2,%Dest%", @"False");

            // ToolTip
            ReadInterface_Template(s, $@"ReadInterface,ToolTip,{scriptFile},Interface,pTextLabel3,%Dest%", @"PEBakery");
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
            // Element = Text, Visible, PosX, PosY, Width, Height, Value, ToolTip
            EngineState s = EngineTests.CreateEngineState();
            string scriptFile = Path.Combine("%ProjectTemp%", "WriteInterface.script");

            // Common
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
            WriteInterface_Template(s, $@"WriteInterface,ToolTip,{scriptFile},Interface,pTextBox1,PEBakery", @"pTextBox1",
                @"Display,1,0,20,20,200,21,StringValue,__PEBakery");

            // 0 - TextBox
            WriteInterface_Template(s, $@"WriteInterface,Value,{scriptFile},Interface,pTextBox1,PEBakery", @"pTextBox1",
                @"Display,1,0,20,20,200,21,PEBakery");

            // 1 - TextLabel
            WriteInterface_Template(s, $@"WriteInterface,Value,{scriptFile},Interface,pTextLabel1,PEBakery", @"pTextLabel1", 
                null, ErrorCheck.Error);
            WriteInterface_Template(s, $@"WriteInterface,FontSize,{scriptFile},Interface,pTextLabel1,10", @"pTextLabel1",
                @"Display,1,1,20,50,230,18,10,Normal");
            WriteInterface_Template(s, $@"WriteInterface,FontWeight,{scriptFile},Interface,pTextLabel1,Bold", @"pTextLabel1",
                @"Display,1,1,20,50,230,18,8,Bold");
            WriteInterface_Template(s, $@"WriteInterface,FontWeight,{scriptFile},Interface,pTextLabel1,Error", @"pTextLabel1",
                null, ErrorCheck.Error);

            // 2 - NumberBox
            WriteInterface_Template(s, $@"WriteInterface,Value,{scriptFile},Interface,pNumberBox1,2", @"pNumberBox1",
                @"pNumberBox1,1,2,20,70,40,22,2,0,100,1");
            WriteInterface_Template(s, $@"WriteInterface,Value,{scriptFile},Interface,pNumberBox1,200", @"pNumberBox1",
                null, ErrorCheck.Error);
            WriteInterface_Template(s, $@"WriteInterface,Value,{scriptFile},Interface,pNumberBox1,Str", @"pNumberBox1",
                null, ErrorCheck.Error);
            WriteInterface_Template(s, $@"WriteInterface,NumberMin,{scriptFile},Interface,pNumberBox1,10", @"pNumberBox1",
                @"pNumberBox1,1,2,20,70,40,22,10,10,100,1");
            WriteInterface_Template(s, $@"WriteInterface,NumberMax,{scriptFile},Interface,pNumberBox1,1", @"pNumberBox1",
                @"pNumberBox1,1,2,20,70,40,22,1,0,1,1");
            WriteInterface_Template(s, $@"WriteInterface,NumberTick,{scriptFile},Interface,pNumberBox1,5", @"pNumberBox1",
                @"pNumberBox1,1,2,20,70,40,22,3,0,100,5");
            WriteInterface_Template(s, $@"WriteInterface,NumberMin,{scriptFile},Interface,pNumberBox1,Error", @"pNumberBox1",
                null, ErrorCheck.Error);

            // 3 - CheckBox
            WriteInterface_Template(s, $@"WriteInterface,Value,{scriptFile},Interface,pCheckBox1,False", @"pCheckBox1",
                @"pCheckBox1,1,3,20,100,200,18,False");
            WriteInterface_Template(s, $@"WriteInterface,SectionName,{scriptFile},Interface,pCheckBox1,Hello", @"pCheckBox1",
                @"pCheckBox1,1,3,20,100,200,18,True,_Hello_,False");
            WriteInterface_Template(s, $@"WriteInterface,HideProgress,{scriptFile},Interface,pCheckBox1,None", @"pCheckBox1",
                @"pCheckBox1,1,3,20,100,200,18,True");
            WriteInterface_Template(s, $@"WriteInterface,HideProgress,{scriptFile},Interface,pCheckBox1,True", @"pCheckBox1",
                null, ErrorCheck.Error);

            // 4 - ComboBox
            WriteInterface_Template(s, $@"WriteInterface,Value,{scriptFile},Interface,pComboBox1,B", @"pComboBox1",
                @"B,1,4,20,130,150,21,A,B,C,D");
            WriteInterface_Template(s, $@"WriteInterface,Value,{scriptFile},Interface,pComboBox1,E", @"pComboBox1",
                null, ErrorCheck.Error);
            WriteInterface_Template(s, $@"WriteInterface,Items,{scriptFile},Interface,pComboBox1,X|Y|Z", @"pComboBox1",
                @"X,1,4,20,130,150,21,X,Y,Z");
            WriteInterface_Template(s, $@"WriteInterface,SectionName,{scriptFile},Interface,pComboBox1,Hello", @"pComboBox1",
                @"A,1,4,20,130,150,21,A,B,C,D,_Hello_,False");
            WriteInterface_Template(s, $@"WriteInterface,HideProgress,{scriptFile},Interface,pComboBox1,None", @"pComboBox1",
                @"A,1,4,20,130,150,21,A,B,C,D");
            WriteInterface_Template(s, $@"WriteInterface,HideProgress,{scriptFile},Interface,pComboBox1,True", @"pComboBox1",
                null, ErrorCheck.Error);

            // 5 - Image
            WriteInterface_Template(s, $@"WriteInterface,Value,{scriptFile},Interface,pImage1,PEBakery", @"pImage1", 
                null, ErrorCheck.Error);
            WriteInterface_Template(s, $@"WriteInterface,Url,{scriptFile},Interface,pImage1,https://github.com/pebakery/pebakery", @"pImage1",
                @"Logo.jpg,1,5,20,230,40,40,https://github.com/pebakery/pebakery");

            // 6 - TextFile
            WriteInterface_Template(s, $@"WriteInterface,Value,{scriptFile},Interface,pTextFile1,PEBakery", @"pTextFile1",
                null, ErrorCheck.Error);

            // 8 - Button
            WriteInterface_Template(s, $@"WriteInterface,Value,{scriptFile},Interface,pButton1,PEBakery", @"pButton1", 
                null, ErrorCheck.Error);
            WriteInterface_Template(s, $@"WriteInterface,SectionName,{scriptFile},Interface,pButton1,World", @"pButton1",
                @"ShowProgress,1,8,240,115,80,25,World,0,False");
            WriteInterface_Template(s, $@"WriteInterface,SectionName,{scriptFile},Interface,pButton1,""""", @"pButton1",
                null, ErrorCheck.Error);
            WriteInterface_Template(s, $@"WriteInterface,HideProgress,{scriptFile},Interface,pButton1,None", @"pButton1",
                null, ErrorCheck.Error);
            WriteInterface_Template(s, $@"WriteInterface,HideProgress,{scriptFile},Interface,pButton1,True", @"pButton1",
                @"ShowProgress,1,8,240,115,80,25,Hello,0,True");

            // 10 - WebLabel
            WriteInterface_Template(s, $@"WriteInterface,Value,{scriptFile},Interface,pWebLabel1,PEBakery", @"pWebLabel1",
                null, ErrorCheck.Error);
            WriteInterface_Template(s, $@"WriteInterface,Url,{scriptFile},Interface,pWebLabel1,https://github.com/pebakery", @"pWebLabel1",
                @"GitHub,1,10,250,160,32,18,https://github.com/pebakery");

            // 11 - RadioButton
            WriteInterface_Template(s, $@"WriteInterface,Value,{scriptFile},Interface,pRadioButton1,True", @"pRadioButton1",
                @"pRadioButton1,1,11,250,180,100,20,True");
            WriteInterface_Template(s, $@"WriteInterface,SectionName,{scriptFile},Interface,pRadioButton1,Hello", @"pRadioButton1",
                @"pRadioButton1,1,11,250,180,100,20,False,_Hello_,False");
            WriteInterface_Template(s, $@"WriteInterface,HideProgress,{scriptFile},Interface,pRadioButton1,None", @"pRadioButton1",
                @"pRadioButton1,1,11,250,180,100,20,False");
            WriteInterface_Template(s, $@"WriteInterface,HideProgress,{scriptFile},Interface,pRadioButton1,True", @"pRadioButton1",
                null, ErrorCheck.Error);

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
            WriteInterface_Template(s, $@"WriteInterface,Value,{scriptFile},Interface,pRadioGroup1,Items", @"pRadioGroup1",
                null, ErrorCheck.Error);
            WriteInterface_Template(s, $@"WriteInterface,Items,{scriptFile},Interface,pRadioGroup1,X|Y|Z", @"pRadioGroup1",
                @"pRadioGroup1,1,14,20,160,150,60,X,Y,Z,0");
            WriteInterface_Template(s, $@"WriteInterface,SectionName,{scriptFile},Interface,pRadioGroup1,Hello", @"pRadioGroup1",
                @"pRadioGroup1,1,14,20,160,150,60,Option1,Option2,Option3,0,_Hello_,False");
            WriteInterface_Template(s, $@"WriteInterface,HideProgress,{scriptFile},Interface,pRadioGroup1,None", @"pRadioGroup1",
                @"pRadioGroup1,1,14,20,160,150,60,Option1,Option2,Option3,0");
            WriteInterface_Template(s, $@"WriteInterface,HideProgress,{scriptFile},Interface,pRadioGroup1,True", @"pRadioGroup1",
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
        #endregion
    }
}
