# Visible

**This command has been deprecated and will be removed in a future version. It is recommended that you update your code to use `WriteInterface,Visible` as soon as possible to avoid breaking your plugin.**

Sets the visibility of a UI control.

## Syntax

```pebakery
Visible,<%Control%>,<Boolean>
```

### Arguments

| Argument | Description |
| --- | --- |
| %Control% | The variable representing the interface control to be modified. |
| Boolean | One of the following values: |
|| True - Show the control. |
|| False - Hide the control. |

## Remarks

Changes made to an control's visibility are persistent.

## Related

## Examples

### Example 1

Sample plugin that will toggle the visibility of a group of controls when the value of a scrollbox changes.

```pebakery
[main]
Title=Visibility Example
Description=Show usage of Visible command
Level=5
Version=1
Author=Homes32

[variables]

[process]

[Toggle_Advanced_Options]
System,CURSOR,WAIT
If,%SB_CfgProfile%,Equal,"Advanced",Begin
  Visible,%BVL_AdvOptions%,True
  Visible,%LBL_AdvOptions%,True
  Visible,%CB_Adv1%,True
  Visible,%CB_Adv2%,True
End
Else,Begin
  Visible,%BVL_AdvOptions%,False
  Visible,%LBL_AdvOptions%,False
  Visible,%CB_Adv1%,False
  Visible,%CB_Adv2%,False
End
System,CURSOR,NORMAL

[Interface]
LBL_CfgProfile="Configuration Profile:",1,1,12,16,120,20,8,Bold
SB_CfgProfile=Simple,1,4,135,10,150,21,Simple,Advanced,_Toggle_Advanced_Options_,True
TXT_FilePath="File Path",1,0,16,78,200,21,C:\Temp
BVL_AdvOptions=pBevel1,0,12,10,115,170,80,
LBL_AdvOptions="Advanced Options:",0,1,23,120,147,18,8,Bold
CB_Adv1="Option 1",0,3,20,150,145,20,True
CB_ADV2="Option 2",0,3,20,170,135,20,True
```