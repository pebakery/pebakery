# Visible

Write a element to interface control.

## Syntax

```pebakery
WriteInterface,<Element>,<PluginFile>,<Section>,<Key>,<Value>
```

### Arguments

| Argument | Description |
| --- | --- |
| Element | The key of the interface control to be modified. Can be one of these: |
|| Text - Ex `<Display>,1,0,20,20,200,21,StringValue` |
|| Visible - Ex `Display,<1>,0,20,20,200,21,StringValue`  |
|| PosX - Ex `Display,1,0,<20>,20,200,21,StringValue`  |
|| PosY - Ex `Display,1,0,20,<20>,200,21,StringValue`  |
|| Width - Ex `Display,1,0,20,20,<200>,21,StringValue`  |
|| Height - Ex `Display,1,0,20,20,200,<21>,StringValue`  |
|| Value - Ex `Display,1,0,20,20,200,21,<StringValue>`  |
| PluginFile | The path of the plugin file to be modified. |
| Section | The section containing the interface control. |
| Key | The key of the interface control to be modified. |
| Value | The value to write. |

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

Trying to write `Value` to unsupported control will result error.

```pebakery
// Error! You cannot write value to TextLabel.
WriteInterface,Value,%ScriptFile%,Interface,pTextLabel1,PEBakery
```

Writing invalid type will also result error.

```pebakery
// Error! CheckBox accepts only True or False.
WriteInterface,Value,%ScriptFile%,Interface,pCheckBox1,Joveler
```

## Related

TODO: Structure of [Interfaces] section.

## Examples

Let us assume a file %ScriptFile% consists of these sections:

```pebakery
[Main]
Title=WriteInterface
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
// Source : pTextBox1=Display,1,0,20,20,200,21,StringValue
// Result : pTextBox1=PEBakery,1,0,20,20,200,21,StringValue
WriteInterface,Text,%ScriptFile%,Interface,pTextBox1,PEBakery
```

### Example 2 - Visible

```pebakery
// Source : pTextLabel1=Display,1,1,20,50,230,18,8,Normal
// Result : pTextLabel1=Display,0,1,20,50,230,18,8,Normal
WriteInterface,Visible,%ScriptFile%,Interface,pTextLabel1,False
```

### Example 3 - Value

```pebakery
// Source : pTextBox1=Display,1,0,20,20,200,21,StringValue
// Result : pTextBox1=Display,1,0,20,20,200,21,PEBakery
WriteInterface,Value,%ScriptFile%,Interface,pTextBox1,PEBakery

// Source : pNumberBox1=pNumberBox1,1,2,20,70,40,22,3,0,100,1
// Result : pNumberBox1=pNumberBox1,1,2,20,70,40,22,2,0,100,1
WriteInterface,Value,%ScriptFile%,Interface,pNumberBox1,2

// Source : pNumberBox1=pNumberBox1,1,2,20,70,40,22,3,0,100,1
// Result : pNumberBox1=pNumberBox1,1,2,20,70,40,22,2,0,100,1
WriteInterface,Value,%ScriptFile%,Interface,pCheckBox1,%Dest%

// Source : pComboBox1=A,1,4,20,130,150,21,A,B,C,D
// Result : pComboBox1=B,1,4,20,130,150,21,A,B,C,D
WriteInterface,Value,%ScriptFile%,Interface,pComboBox1,B

// Source : pRadioButton1=pRadioButton1,1,11,250,180,100,20,False
// Result : pRadioButton1=pRadioButton1,1,11,250,180,100,20,True
WriteInterface,Value,%ScriptFile%,Interface,pRadioButton1,True

// Source : pFileBox1=C:\Windows\notepad.exe,1,13,240,230,200,20,file
// Result : pFileBox1=D:\PEBakery\Launcher.exe,1,13,240,230,200,20,file
WriteInterface,Value,%ScriptFile%,Interface,pFileBox1,D:\PEBakery\Launcher.exe

// Source : pRadioGroup1=pRadioGroup1,1,14,20,160,150,60,Option1,Option2,Option3,2
// Result : pRadioGroup1=pRadioGroup1,1,14,20,160,150,60,Option1,Option2,Option3,0
WriteInterface,Value,%ScriptFile%,Interface,pRadioGroup1,0
```