# ReadInterface

Read a element from interface control.

## Syntax

```pebakery
ReadInterface,<Element>,<PluginFile>,<Section>,<Key>,<%DestVar%>
```

### Arguments

| Argument | Description |
| --- | --- |
| Element | The key of the interface control to be read. Can be one of these: |
|| Text - Ex `<Display>,1,0,20,20,200,21,StringValue` |
|| Visible - Ex `Display,<1>,0,20,20,200,21,StringValue`  |
|| PosX - Ex `Display,1,0,<20>,20,200,21,StringValue`  |
|| PosY - Ex `Display,1,0,20,<20>,200,21,StringValue`  |
|| Width - Ex `Display,1,0,20,20,<200>,21,StringValue`  |
|| Height - Ex `Display,1,0,20,20,200,<21>,StringValue`  |
|| Value - Ex `Display,1,0,20,20,200,21,<StringValue>`  |
| PluginFile | The path of the plugin file to read. |
| Section | The section containing the interface. |
| Key | The key of the interface control to be read. |
| DestVar | Variable name to save read value. |

## Remarks

In `<Element>`, `Value` is only supported in these controls:

| Control | Read Value |
| --- | --- |
| TextBox     | (String) Content of the control |
| NumberBox   | (String) Content of the control |
| CheckBox    | (Boolean) True/False, True if checked |
| ComboBox    | (String) Selected item |
| RadioButton | (Boolean) True/False, True if checked |
| FileBox     | (String) Content of the control |
| RadioGroup  | (Integer) Index of selected item (Starts from 0) |

Trying to read `Value` from unsupported control will result error.

```pebakery
// Error! You cannot read value from TextLabel.
ReadInterface,Value,%ScriptFile%,Interface,pTextLabel1,%Dest%
```

## Related

TODO: Structure of [Interfaces] section.

## Examples

Let us assume a file %ScriptFile% consists of these sections:

```pebakery
[Main]
Title=ReadInterface
Author=ied206
Description=UnitTest
Version=001
Level=5
Selected=True
Mandatory=False

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

### Example 1 - Text

```pebakery
// Return "Display"
ReadInterface,Text,%ScriptFile%,Interface,pTextBox1,%Dest%
```

### Example 2 - Visible

```pebakery
// Return "True"
ReadInterface,Visible,%ScriptFile%,Interface,pTextLabel1,%Dest%
// Return "False"
ReadInterface,Visible,%ScriptFile%,Interface,pTextLabel2,%Dest%
```

### Example 3 - Value

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