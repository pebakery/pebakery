# System,SetLocal

Starts localization of variables within a plugin.

Localized variables contain a copy of their original value and any further modifications are isolated until a matching `System,EndLocal` command is encountered or the end of the current [Section] is reached.

## Syntax

```pebakery
System,SetLocal
```

### Arguments

This command has no arguments.

### Return Values

| Return Value | Description |
| --- | --- |
| #r | When `System,EndLocal` is called the contents of the isolated variables are discarded. The `#r` token is not affected by the constraints of `System,SetLocal` and can be used to return the value of an isolated variable to the main process. `#r` is volatile so if you need to preserve the return value copy it into a local variable. |

## Remarks

This command is intended for use in "library" plugins containing collections of macros. Maintaining unique variables for many macros in a single plugin can be difficult because local variables are in the scope of the entire `%PluginFile%`. By making use of the `System,SetLocal` and `System,EndLocal` commands to isolate variables to a narrower scope you can ensure that each section's variables are protected from unwanted modification.

## Related

[System,EndLocal](./EndLocal.md)

## Examples

### Example 1

```pebakery
[Main]
Title=SetLocal/EndLocal Example
Author=Homes32
Description=Demonstrate usage of the System,SetLocal and System,EndLocal command.
Version=1
Level=5

[Interface]

[variables]

[process]
Set,%var%,"This value should never change!"
Run,%PluginFile%,mySection
Echo,"Let's verify our var1 didn't change."
Echo,"Var1 = %var%"
Echo,"R = #r"

[mySection]
System,SETLOCAL
// Thanks to System,SETLOCAL we can safely use %var% in [mySection] without changing the value of %var% in our [Process] section.
Echo,"Our isolated copy of var1: %var%"
// Lets change it!
Set,%var%,"Hello World!"
Set,#r,%var%
System,ENDLOCAL
```