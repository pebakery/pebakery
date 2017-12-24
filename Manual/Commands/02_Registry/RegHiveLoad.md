# RegHiveLoad

Loads an external registry hive onto your local registry.

## Syntax

```pebakery
RegHiveLoad,<KeyPath>,<HiveFile>
```

### Arguments

| Argument | Description |
| --- | --- |
| KeyPath | The registry key where `HiveFile` will be loaded. |
| HiveFile | The full path of the registry hive to load. |

## Remarks

`RegHiveLoad` will always load the `KeyPath` under the local registry **HKLM** key.

A registry hive mounted with `RegHiveLoad` should be unmounted with `RegHiveUnload`.

## Related

[RegHiveUnload](./RegHiveUnload.md)

## Examples

### Example 1

Most projects will define a set of variables containing paths to the registry hives to make development easier.
Common examples of this are:

```pebakery
%RegSystem%=%TargetDir%\Windows\System32\config\System
%RegSoftware%=%TargetDir%\Windows\System32\config\Software
%RegDefault%=%TargetDir%\Windows\System32\config\Default
%RegComponents%=%TargetDir%\Windows\System32\config\Components
```

In this example we will load the project defined HKLM\Software hive onto a key called `Tmp_Software` in our local registry.

```pebakery
// Load the Software Hive (%RegSoftware% is defined by the project)
RegHiveLoad,Tmp_Software,%RegSoftware%
Echo,"Writing new values on registry hive.."
RegWrite,HKLM,0x1,"Tmp_Software\MyProgram","ProgramVersion","1.2.3"
// Always unload when we are finished.
RegHiveUnLoad,Tmp_Software
```
### Example 2

```pebakery
RegHiveLoad,Tmp_Setup,%targetdir%\I386\system32\setupreg.hiv
// Always unload when we are finished.
RegHiveUnLoad,Tmp_Setup
```