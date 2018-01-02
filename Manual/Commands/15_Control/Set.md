# Set

Changes or Defines the value of a variable.

## Syntax

```pebakery
Set,<%Variable%>,<Value>[,GLOBAL | PERMANENT]
```

### Arguments

| Argument | Description |
| --- | --- |
| %Variable% | The name of our variable. |
| Value | The value of the variable. |

### Flags

The following flags are mutually exclusive.

| Flag | Description |
| --- | --- |
| GLOBAL | Store the variable in global memory for the lifetime of the build process. This will allow other plugins to reference and/or modify this value. If the `Variable` is already defined the value will be overwritten. |
| PERMANENT | Permanently stores the value of this variable by writing the definition into script.project's [Variables] section. If the `Variable` is already defined the value will be overwritten. |

## Remarks

Unless the `GLOBAL` or `PERMANENT` flag are defined the `%Variable%` scope is confined to the running plugin.

Alternately, Variables can also be defined before execution by placing the definitions in the plugin's `[Variables]` section. See Example 2 for details.

The `Set,...,PERMANENT` command should not be used to change interface control values. Use the `InterfaceWrite` command for this purpose.

## Related

[SetMacro](./SetMacro.md), [WriteInterface](../08_Interface/WriteInterface.md)

## Examples

Define variables during plugin execution.

### Example 1

```pebakery
[Main]
Title=Variables Example 1
Description=Show usage of the Set command.
Author=Homes32
Level=5
Version=1

[Variables]

[Process]
Set,%ProgramName%,"myApp"
Set,%DownloadURL%,http://mySite.net/myApp.exe
// Set %isMyAppInstalled% as a global var so other plugins can access it.
Set,%isMyAppInstalled%,True,GLOBAL
// Set %ProjectTemp% = to the value of pFileBox1 and store it permanently in script.project
Set,%BuildSource%,%pFileBox1%,PERMANENT
Echo,%BuildSource%

Echo,"Downloading %ProgramName%"
Webget,%DownloadURL%,C:\Temp


[Interface]
pFileBox1=C:\Images\,1,13,23,44,230,20,dir
pTextLabel1="Select your source directory:",1,1,23,25,230,18,8,Bold
```

### Example 2

Defining variables before execution by placing the definitions in the plugin's `[Variables]` section.

```pebakery
[Main]
Title=Variables Example 2
Description=Show usage of the variables section.
Author=Homes32
Level=5
Version=1

[Variables]
ProgramName=myApp
DownloadURL=http://mySite.net/myApp.exe

[Process]
Echo,"Downloading %ProgramName%"
Webget,%DownloadURL%,C:\Temp
```
