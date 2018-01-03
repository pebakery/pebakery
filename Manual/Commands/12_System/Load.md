# System,Load

Scans the specified path for new/modified projects and plugins and adds them to the project tree.

## Syntax

```pebakery
System,Load,<FilePath>,[NOREC]
```

### Arguments

| Argument | Description |
| --- | --- |
| FilePath | The path to the plugin file to load. Wildcards (* ?) are supported and can be used to scan multiple files. |

### Flags

| Flag | Description |
| --- | --- |
| NOREC | **(Optional)** Do not recurse subdirectories. - When using wildcards all directories under `FilePath` are scanned. Use this flag to disable this behavior. |

## Remarks

This command can be used to rebuild the project tree when a plugin is added or modified and performs similar to the  `System,LoadAll` command with the exception that only the specified file(s) are loaded. This can save time if you don't need to refresh the entire project tree, which could span multiple projects and dozens of plugins.

Due to a system limitation PEBakery cannot `Load` the current plugin `%PluginFile%` during a project build. The command will take affect after the current build finishes.

## Related

[System,LoadAll](./LoadAll.md)

## Examples

### Example 1

```pebakery
[Main]
Title=Load Example
Author=Homes32
Description=Demonstrate usage of the System,Load command.
Version=1
Level=5

[Interface]
CB_Recurse="Scan inside subdirectories",1,3,15,30,200,20,True
BTN_Load=Load,1,8,15,60,80,25,Process,0,False,_Process_,False
BTN_Clean=Cleanup,1,8,100,60,80,25,Clean,0,True,_Clean_,True

[variables]
%myProject%=%BaseDir%\Projects\myProject\script.project
%myPlugin%=%BaseDir%\Projects\myProject\myPlugin.script
%mySubDir%=%BaseDir%\Projects\myProject\SubDir\folder.project
%myPlugin2%=%BaseDir%\Projects\myProject\SubDir\myPlugin2.script

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

// Plugin subdir
//IniWrite,%mySubDir%,Main,Title,myApps
//IniWrite,%mySubDir%,Main,Author,Homes32
//IniWrite,%mySubDir%,Main,Description,"A folder for myApps"
//IniWrite,%mySubDir%,Main,Version,1

// Plugin in subdir
IniWrite,%myPlugin2%,Main,Title,myPlugin2
IniWrite,%myPlugin2%,Main,Author,Homes32
IniWrite,%myPlugin2%,Main,Description,"A brand new plugin in a subdir!"
IniWrite,%myPlugin2%,Main,Version,1
IniWrite,%myPlugin2%,Main,Level,5
IniWrite,%myPlugin2%,Main,Selected,False

TXTAddLine,%myPlugin2%,"[Variables]",Append
TXTAddLine,%myPlugin2%,"",Append
TXTAddLine,%myPlugin2%,"[Process]",Append
TXTAddLine,%myPlugin2%,"[Interface]",Append
TXTAddLine,%myPlugin2%,"",Append

// Now we need to call the following command to get our new project and plugin to show up in the main window.
If,%CB_Recurse%,Equal,True,Begin
  // Scan the following directories and all subdirectories.
  Echo,"Loading all plugins under %BaseDir%\Projects\myProject\"
  System,Load,%BaseDir%\Projects\myProject\*.*
End
Else,Begin
  // Do not scan subdirectories
  Echo,"Loading only plugins located in the %BaseDir%\Projects\myProject\ directory."
  System,Load,%BaseDir%\Projects\myProject\*.*,NOREC
End
```