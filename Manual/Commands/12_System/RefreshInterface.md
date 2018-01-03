# System,RefreshInterface

Refreshes the Graphical Interface of the current plugin.

## Syntax

```pebakery
System,RefreshInterface
```

### Arguments

This command has no arguments.

## Remarks

This command can be used to refresh the interface after making changes to the plugin, such as switching to a new Interface page or changing the value of an interface control via the `Set,...,PERMANENT` command. This same behavior can be achieved manually with the "Refresh Plugin" button.

PEBakery refreshes the interface automatically when changes are made using the `WriteInterface` command.

## Related

## Examples

### Example 1

```pebakery
[main]
Title=RefreshInterface Example
Description=Show usage of the System,RefreshInterface command using multiple interface pages.
Level=5
Version=1
Author=Homes32
Interface=Interface

[variables]

[process]

[ShowAdvanced]
// Show the Advanced interface
IniWrite,%PluginFile%,Main,Interface,Interface-Advanced
// We need to refresh or we won't see the new interface
System,REFRESHINTERFACE

[ShowMain]
// Show the main interface
IniWrite,%PluginFile%,Main,Interface,Interface
// We need to refresh or we won't see the new interface
System,REFRESHINTERFACE

[Interface]
pBevel_Shortcut=pBevel1,1,12,1,25,188,116
CB_StartMenu="Start menu",1,3,11,72,122,18,True
CB_QuickLaunch=Quicklaunch,1,3,11,52,122,18,False
CB_Desktop=Desktop,1,3,11,35,120,15,False
LBL_Shortcuts=Shortcuts,1,1,4,6,75,20,8,Bold
SM_Folder="Start menu folder (. for root):",1,0,12,109,168,21,.
BTN_AdvancedOpts="Advanced Options",1,8,225,25,120,25,ShowAdvanced,0,True,_ShowAdvanced_,True

[Interface-Advanced]
LBL_AdvOpts="Advanced Options ",1,1,1,1,330,20,10,Bold
BTN_Save=Save,1,8,451,11,80,25,ShowMain,0,True,_ShowMain_,True
BVL_CustCmd=pBevel2,1,12,1,47,530,130
CB_CustomCMD="Run custom commands",1,3,11,62,220,20,False
RunProg="Command to execute",1,0,21,102,370,21,%SystemRoot%\System32\MyProgram.exe
LBL_CustomCmds="Custom Commands",1,1,5,28,125,20,8,Bold
RunFlag=@SW_SHOW,1,4,406,102,115,21,@SW_SHOW,@SW_HIDE,@SW_MINIMIZE,@SW_MAXIMIZE
RunParm=Parameters,1,0,21,147,370,21,
LBL_WinState="Window State",1,1,406,85,115,18,8,Normal
```