# AddInterface

Loads variables from another interface section into the local scope.

## Syntax

```pebakery
AddInterface,<PluginFile>,<Interface>,<Prefix>
```

### Arguments

| Argument | Description |
| --- | --- |
| PluginFile | The full path of the plugin containing the interface. **Hint:** Use `%PluginFile%` to reference the current plugin.|
| Interface | The name of the section containing the interface you wish to read. |
| Prefix |  Prefix for the interface variables. Prefixes protect you in the event the interface you are loading has the same component names as the main [Interface] section. Variables are loaded as `%<prefix>_<componentName>%`. |

## Remarks

The `AddInterface` command is required in order to read all the components in your plugin in the event you make use of multiple interface "pages". You can also use `AddInterface` to access the values of components in another plugin, as long as you know the component names.

## Related

## Examples

### Example 1

```pebakery
[main]
Title=AddInterface Example
Description=Show usage of the AddInterface command using multiple interface pages.
Level=5
Version=1
Author=Homes32
Interface=Interface

[variables]

[process]

// Echo will not return the value of text box %RunProg%
// because the components in [Interface-Advanced] are not in scope.
Message,"The value of text box RunProg is: %RunProg%"

// Add our advanced interface. Since we know we don't have
// duplicate component names we are going to specify a blank prefix.
AddInterface,%PluginFile%,Interface-Advanced,""

// Now we can read the value of the RunProg text box
Message,"The value of text box RunProg is: %RunProg%"

[ShowAdvanced]
// Show the Advanced interface
IniWrite,%PluginFile%,Main,Interface,Interface-Advanced
System,REFRESHINTERFACE

[ShowMain]
// Show the main interface
IniWrite,%PluginFile%,Main,Interface,Interface
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