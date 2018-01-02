# ReadInterface

Reads the properties of an interface control.

## Syntax

```pebakery
ReadInterface,<Property>,<PluginFile>,<Interface>,<ControlName>,<%DestVar%>
```

### Arguments

| Argument | Description |
| --- | --- |
| Property | The Property value to read:
|| Text - Text value of the control. |
|| Visible - True/False - Show or Hide the control. |
|| PosX - Horizontal Position measured from the control's top left corner. |
|| PosY - Vertical Position measured from the control's top left corner. |
|| Width - Width of the control. |
|| Height - Height of the control. |
|| Value - Value of the control. |
| PluginFile | The full path to the plugin. **Hint:** Use `%PluginFile%` to reference the current plugin. |
| Interface | The name of the section containing the interface you wish to read. |
| ControlName | The name of the control to read. |
| %DestVar% | The variable that will contain the value of the selected property. |

## Remarks

The `Value` `Property` is only supported in these controls:

| Control | Value |
| --- | --- |
| TextBox     | (String) Content of the control. |
| NumberBox   | (String) Content of the control. |
| CheckBox    | (Boolean) True/False, True if checked. |
| ComboBox    | (String) Selected item. |
| RadioButton | (Boolean) True/False, True if checked. |
| FileBox     | (String) Content of the control. |
| RadioGroup  | (Integer) Zero-Based Index of the selected item. |

Trying to read `Value` from an unsupported control will result in an error.

```pebakery
// Error! You cannot read the value of a TextLabel.
ReadInterface,Value,%PluginFile%,Interface,pTextLabel1,%Dest%
```

## Related

TODO: Structure of [Interfaces] section, [WriteInterface](./WriteInterface.md)

## Examples

### Example 1

An interactive plugin demonstrating various usage.

```pebakery
[main]
Title=Read/Write Interface Example
Description=Show usage of ReadInterface and WriteInterface
Level=5
Version=1
Author=Homes32

[variables]

[Process]

[Toggle_Advanced_Options]
System,CURSOR,WAIT
If,%SB_CfgProfile%,Equal,"Advanced",Begin
  WriteInterface,Visible,%PluginFile%,Interface,BVL_AdvOptions,True
  WriteInterface,Visible,%PluginFile%,Interface,LBL_AdvOptions,True
  WriteInterface,Visible,%PluginFile%,Interface,CB_Adv1,True
  WriteInterface,Visible,%PluginFile%,Interface,CB_Adv2,True
  WriteInterface,Visible,%PluginFile%,Interface,BTN_SelectAll,True
  WriteInterface,Visible,%PluginFile%,Interface,BTN_SelectNone,True
  WriteInterface,Visible,%PluginFile%,Interface,LBL_Info,True
End
Else,Begin
  WriteInterface,Visible,%PluginFile%,Interface,BVL_AdvOptions,False
  WriteInterface,Visible,%PluginFile%,Interface,LBL_AdvOptions,False
  WriteInterface,Visible,%PluginFile%,Interface,CB_Adv1,False
  WriteInterface,Visible,%PluginFile%,Interface,CB_Adv2,False
  WriteInterface,Visible,%PluginFile%,Interface,BTN_SelectAll,False
  WriteInterface,Visible,%PluginFile%,Interface,BTN_SelectNone,False
  WriteInterface,Visible,%PluginFile%,Interface,LBL_Info,False
End
System,CURSOR,NORMAL

[SelectAll]
WriteInterface,Value,%PluginFile%,Interface,CB_Adv1,True
WriteInterface,Value,%PluginFile%,Interface,CB_Adv2,True
WriteInterface,Text,%PluginFile%,Interface,LBL_Info,"All Options Selected!"

[SelectNone]
WriteInterface,Value,%PluginFile%,Interface,CB_Adv1,False
WriteInterface,Value,%PluginFile%,Interface,CB_Adv2,False
WriteInterface,Text,%PluginFile%,Interface,LBL_Info,"All Options Disabled!"

[ReadValues]
// Read Visibility
ReadInterface,Visible,%PluginFile%,Interface,CB_Adv1,%value%
Message,"The Option 1 check box is Visible: %value%"

// Read value
ReadInterface,Value,%PluginFile%,Interface,CB_Adv1,%value%
Message,"The Option 1 check box is Checked: %value%"

// Read Text
ReadInterface,Text,%PluginFile%,Interface,CB_Adv1,%value%
Message,"The Option 1 check box caption is: %value%"

// Read Dimensions
ReadInterface,Height,%PluginFile%,Interface,CB_Adv1,%height%
ReadInterface,Width,%PluginFile%,Interface,CB_Adv1,%width%
Message,"The Option 1 check box dimensions are : %width%x%height%"

// Read Position
ReadInterface,PosX,%PluginFile%,Interface,CB_Adv1,%x%
ReadInterface,PosY,%PluginFile%,Interface,CB_Adv1,%y%
Message,"The Option 1 check box is located at : %x%#$c%y%"

// Text box
ReadInterface,Text,%PluginFile%,Interface,TXT_FilePath,%text%
ReadInterface,Value,%PluginFile%,Interface,TXT_FilePath,%value%
Message,"Text box name: %text% #$x Text box value: %value%"

[BumpLeft]
// Move the textbox to the left
ReadInterface,PosX,%PluginFile%,Interface,TXT_MoveMe,%x%
ReadInterface,PosY,%PluginFile%,Interface,TXT_MoveMe,%y%
StrFormat,DEC,%x%,1
WriteInterface,PosX,%PluginFile%,Interface,TXT_MoveMe,%x%
WriteInterface,Text,%PluginFile%,Interface,LBL_X,%x%
WriteInterface,Text,%PluginFile%,Interface,LBL_Y,%y%

[BumpRight]
// Move the textbox to the right
ReadInterface,PosX,%PluginFile%,Interface,TXT_MoveMe,%x%
ReadInterface,PosY,%PluginFile%,Interface,TXT_MoveMe,%y%
StrFormat,INC,%x%,1
WriteInterface,PosX,%PluginFile%,Interface,TXT_MoveMe,%x%
WriteInterface,Text,%PluginFile%,Interface,LBL_X,%x%
WriteInterface,Text,%PluginFile%,Interface,LBL_Y,%y%

[Shrink]
// Make the textbox smaller
ReadInterface,Width,%PluginFile%,Interface,TXT_MoveMe,%width%
ReadInterface,Height,%PluginFile%,Interface,TXT_MoveMe,%height%
StrFormat,DEC,%width%,1
StrFormat,DEC,%height%,1
WriteInterface,Width,%PluginFile%,Interface,TXT_MoveMe,%width%
WriteInterface,Height,%PluginFile%,Interface,TXT_MoveMe,%height%
WriteInterface,Text,%PluginFile%,Interface,LBL_WidthValue,%width%
WriteInterface,Text,%PluginFile%,Interface,LBL_HeightValue,%height%

[Grow]
// Make the textbox larger
ReadInterface,Width,%PluginFile%,Interface,TXT_MoveMe,%width%
ReadInterface,Height,%PluginFile%,Interface,TXT_MoveMe,%height%
StrFormat,INC,%width%,1
StrFormat,INC,%height%,1
WriteInterface,Width,%PluginFile%,Interface,TXT_MoveMe,%width%
WriteInterface,Height,%PluginFile%,Interface,TXT_MoveMe,%height%
WriteInterface,Text,%PluginFile%,Interface,LBL_WidthValue,%width%
WriteInterface,Text,%PluginFile%,Interface,LBL_HeightValue,%height%

[Interface]
LBL_CfgProfile="Configuration Profile:",1,1,12,16,120,20,8,Bold
SB_CfgProfile=Advanced,1,4,135,10,150,21,Simple,Advanced,_Toggle_Advanced_Options_,True
TXT_FilePath="File Path",1,0,16,78,200,21,C:\Temp
BVL_AdvOptions=pBevel1,1,12,9,114,170,88,
LBL_AdvOptions="Advanced Options:",1,1,23,120,147,18,8,Bold
CB_Adv1="Option 1",1,3,20,150,145,20,True
CB_ADV2="Option 2",1,3,20,170,135,20,True
BTN_SelectAll="Select All",1,8,94,146,80,25,SelectAll,0,True
BTN_SelectNone="Select None",1,8,94,170,80,25,SelectNone,0,True
LBL_Info="All Options Selected!",1,1,10,206,170,18,8,Bold
BTN_ReadValues="Read Values",1,8,200,135,147,48,ReadValues,0,True
BTN_BumpLeft=<<,1,8,170,276,80,25,BumpLeft,0,True
BTN_BumpRight=>>,1,8,261,276,80,25,BumpRight,0,True
TXT_MoveMe="Use buttons to change me!",1,0,154,247,200,21,abc..
BTN_Shrink=Shrink,1,8,170,303,80,25,Shrink,0,True
BTN_Grow=Grow,1,8,261,303,80,25,Grow,0,True
pBevel1=pBevel1,1,12,146,334,211,40
LBL_PosX="PosX: ",1,1,152,339,32,18,8,Normal
LBL_PosY=PosY:,1,1,151,360,33,18,8,Normal
LBL_X=154,1,1,186,339,58,18,8,Normal
LBL_Y=247,1,1,186,360,58,18,8,Normal
LBL_Width=Width:,1,1,259,339,39,18,8,Normal
LBL_Height=Height:,1,1,258,360,40,18,8,Normal
LBL_WidthValue=200,1,1,301,340,50,18,8,Normal
LBL_HeightValue=21,1,1,301,360,50,18,8,Normal
```

### Example 2

Let us assume a file %ScriptFile% consists of these sections:

```pebakery
[Main]
Title=ReadInterface
Author=ied206
Description=UnitTest
Version=001
Level=5
[Interface]
pTextBox1=Display,1,0,20,20,200,21,StringValue
pTextLabel1=Display,1,1,20,50,230,18,8,Normal
pNumberBox1=pNumberBox1,1,2,20,70,40,22,3,0,100,1
pCheckBox1=pCheckBox1,1,3,20,100,200,18,True
pComboBox1=A,1,4,20,130,150,21,A,B,C,D
pRadioGroup1=pRadioGroup1,1,14,20,160,150,60,Option1,Option2,Option3,0
pImage1=Logo.jpg,1,5,20,230,40,40,
pTextFile1=HelpMsg.txt,1,6,240,20,200,86
pButton1=ShowProgress,1,8,240,115,80,25,Hello,0,False,False,_Hello_,False
pButton2=HideProgress,1,8,330,115,80,25,Hello,0,True,_Hello_,True
pWebLabel1=GitHub,1,10,250,160,32,18,https://github.com/ied206/PEBakery
pRadioButton1=pRadioButton1,1,11,250,180,100,20,False
pBevel1=pBevel1,1,12,240,150,235,60
pFileBox1=C:\Windows\notepad.exe,1,13,240,230,200,20,file
pFileBox2=E:\WinPE\,1,13,240,260,200,20,dir
pTextLabel2=Hidden,0,1,20,50,280,18,8,Normal
```

#### Read Text

```pebakery
// Return "Display"
ReadInterface,Text,%ScriptFile%,Interface,pTextBox1,%Dest%
```

#### Read Visibility

```pebakery
// Return "True"
ReadInterface,Visible,%ScriptFile%,Interface,pTextLabel1,%Dest%
// Return "False"
ReadInterface,Visible,%ScriptFile%,Interface,pTextLabel2,%Dest%
```

#### Read Value

```pebakery
// Return "StringValue"
ReadInterface,Value,%ScriptFile%,Interface,pTextBox1,%Dest%

// Return "3"
ReadInterface,Value,%ScriptFile%,Interface,pNumberBox1,%Dest%

// Return "True"
ReadInterface,Value,%ScriptFile%,Interface,pCheckBox1,%Dest%

// Return "A"
ReadInterface,Value,%ScriptFile%,Interface,pComboBox1,%Dest%

// Return "False"
ReadInterface,Value,%ScriptFile%,Interface,pRadioButton1,%Dest%

// Return "C:\Windows\notepad.exe"
ReadInterface,Value,%ScriptFile%,Interface,pFileBox1,%Dest%

// Return "0"
ReadInterface,Value,%ScriptFile%,Interface,pRadioGroup1,%Dest%
```