# SetMacro

Defines a procedure to be executed when a specific keyword is encountered.

Macros make plugin maintenance easier as the code is written only once but can be called repeatedly by any number of plugins or processes. They are ideal for repetitive tasks such as creating shortcuts or downloading and unpacking a file.

## Syntax

```pebakery
SetMacro,<MacroName>,<MacroCommand>[,GLOBAL | PERMANENT]
```

### Arguments

| Argument | Description |
| --- | --- |
| MacroName | The name of our macro. `MacroName` can only consist of following characters `a-z A-Z 0-9 _ ( ) .` Because macros are parsed before run-time using %Variables% in the macro name is not supported.|
| MacroCommand | The command to be executed when the macro is called. `MacroCommand` must be enclosed in double quotes. Setting this argument to `NIL` will delete the macro. |

### Flags

The following flags are mutually exclusive.

| Flag | Description |
| --- | --- |
| GLOBAL | Store the macro in global memory for the lifetime of the build process. This will allow other plugins to reference and/or modify this macro. If the macro is already defined it will be overwritten. |
| PERMANENT | Permanently stores the value of this variable by writing the definition into script.project's [Variables] section. If the macro is already defined it will be overwritten. |

## Remarks

Macros can be as simple as a single command or you can define a complex procedure by placing the macro in its own [Section] and calling it with the `Run` command.

Global and Permanent macros can only be deleted if the respective `GLOBAL` or `PERMANENT` flag is set.

## Related

[AddVariables](./AddVariables.md)

## Examples

### Example 1

The following example creates a macro to replicate the deprecated WebGetIfNotExist command. This allows us to call our macro multiple times within the plugin or call it from another plugin while maintaining the code in one central location.

```pebakery
[Main]
Title=SetMacro Example
Description=Show usage of the SetMacro Command
Author=Homes32
Level=5
Version=1

[Variables]

[Process]
// Define our macro 'WebGetIfNotExistEx'
SetMacro,WebGetIfNotExistEx,"Run,%PluginFile%,WebGetIfNotExistEx"

// Call our macro
WebGetIfNotExistEx,"https://zlib.net/zlib-1.2.11.tar.gz",%BaseDir%\zlib.tar.gz

// This is the section to be executed when our macro is called
[WebGetIfNotExistEx]
// Syntax: WebGetIfNotExistEx,<URL>,<DestFile>
Echo,"Checking for #2..."
If,Not,ExistFile,#2,Begin
// File doesn't exist. lets download it!
Echo,"Downloading #2..."
WebGet,#1,#2
End
Else,Begin
// File already exists on disk.
Echo,"#2 found! Skipping download."
End
```
