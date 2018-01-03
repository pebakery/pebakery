# System,LoadAll

**Alias:** `System,ReScanScripts`

Scans the *Projects* directory for new/modified projects and plugins and adds them to the project tree.

## Syntax

```pebakery
System,LoadAll
```

### Arguments

This command has no arguments.

## Remarks

This command can be used to rebuild the project tree when a plugin is added or modified and performs the same operation as pressing the **Refresh** button on the main window.

## Related

[System,Load](./Load.md)

## Examples

### Example 1

```pebakery
[Main]
Title=LoadAll Example
Author=Homes32
Description=Demonstrate usage of the System,LoadAll command.
Version=1
Level=5
Selected=True

[Interface]
BTN_Clean=Cleanup,1,8,100,60,80,25,Clean,0,True,_Clean_,True

[variables]
%myProject%=%BaseDir%\Projects\myProject\script.project
%myPlugin%=%BaseDir%\Projects\myProject\myPlugin.script

[Clean]
Echo,"Removing myProject Example..."
If,EXISTDIR,%BaseDir%\Projects\myProject\,DirDelete,%BaseDir%\Projects\myProject\
System,LoadAll

[process]
If,Not,ExistFile,%myProject%,FileCreateBlank,%myProject%
If,Not,ExistFile,%myPlugin%,FileCreateBlank,%myPlugin%

// Project
IniWrite,%myProject%,Main,Title,myProject
IniWrite,%myProject%,Main,Author,Homes32
IniWrite,%myProject%,Main,Description,"A brand new project!"
IniWrite,%myProject%,Main,Version,1
IniWrite,%myProject%,Main,Level,5

TXTAddLine,%myProject%,"[Variables]",Append
TXTAddLine,%myProject%,"",Append
TXTAddLine,%myProject%,"[Process]",Append
TXTAddLine,%myProject%,"[Interface]",Append
TXTAddLine,%myProject%,"",Append

// Plugin
IniWrite,%myPlugin%,Main,Title,myPlugin
IniWrite,%myPlugin%,Main,Author,Homes32
IniWrite,%myPlugin%,Main,Description,"A brand new plugin!"
IniWrite,%myPlugin%,Main,Version,1
IniWrite,%myPlugin%,Main,Level,5
IniWrite,%myPlugin%,Main,Selected,False

TXTAddLine,%myPlugin%,"[Variables]",Append
TXTAddLine,%myPlugin%,"",Append
TXTAddLine,%myPlugin%,"[Process]",Append
TXTAddLine,%myPlugin%,"[Interface]",Append
TXTAddLine,%myPlugin%,"",Append

// Now we need to call the following command to get our new project and plugin to show up in the main window.
System,LoadAll
```