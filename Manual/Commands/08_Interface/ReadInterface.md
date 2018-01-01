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

## Related

[WriteInterface](./WriteInterface.md)

## Examples

### Example 1

```pebakery
[main]
Title=ReadInterface Example
Description=Show usage of ReadInterface
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

[Interface]
LBL_CfgProfile="Configuration Profile:",1,1,12,16,120,20,8,Bold
SB_CfgProfile=Simple,1,4,135,10,150,21,Simple,Advanced,_Toggle_Advanced_Options_,True
TXT_FilePath="File Path",1,0,16,78,200,21,C:\Temp
BVL_AdvOptions=pBevel1,0,12,9,114,170,88,
LBL_AdvOptions="Advanced Options:",0,1,23,120,147,18,8,Bold
CB_Adv1="Option 1",0,3,20,150,145,20,True
CB_ADV2="Option 2",0,3,20,170,135,20,True
BTN_SelectAll="Select All",0,8,94,146,80,25,SelectAll,0,True
BTN_SelectNone="Select None",0,8,94,170,80,25,SelectNone,0,True
LBL_Info="All Options Selected!",0,1,10,206,170,18,8,Bold
BTN_ReadValues="Read Values",1,8,200,135,147,48,ReadValues,0,True
```
