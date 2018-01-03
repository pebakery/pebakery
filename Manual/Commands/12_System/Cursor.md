# System,Cursor

Toggles the mouse cursor between the `WAIT` and `NORMAL` state.

## Syntax

```pebakery
System,Cursor,<State>
```

### Arguments

| Argument | Description |
| --- | --- |
| State | The following cursor states are available: |
|| NORMAL - Shows a normal cursor as defined by your operating system. (Usually and arrow)|
|| WAIT - Shows a wait cursor as defined by your operating system. (Usually an hourglass or spinner) |

## Remarks

You must return the cursor to the `NORMAL` state when you are finished or it will remain in the `WAIT` state. It's a good idea to include `System,Cursor,NORMAL` in your projects `System,OnBuildExit` function to ensure that the cursor doesn't get "stuck" in a waiting state due the user pressing the Stop button or a plugin failure.

The exact cursor icon displayed is dependent on your operating system.

## Related

## Examples

### Example 1

```pebakery
[main]
Title=Cursor Example
Description=Show usage of the System,Cursor command. Change the value of the combo box or hit "Play"
Level=5
Version=1
Author=Homes32

[variables]


[process]
System,CURSOR,WAIT
Echo,"Cursor is in WAIT state.#$xPausing for 5 seconds..."
Wait,5
System,CURSOR,NORMAL

[Toggle_Advanced_Options]
System,CURSOR,WAIT
Message,"Cursor is in WAIT state.#$xPausing for 5 seconds...",INFORMATION,5
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