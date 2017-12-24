# RegExport

Exports the contents of a registry key to a *.reg file.

This command has the same effect as running `REG.exe EXPORT <RegPath> /y` from Windows.

## Syntax

```pebakery
RegExport,<KeyPath>,<RegFile>
```

### Arguments

| Argument | Description |
| --- | --- |
| KeyPath | The full path of the registry key to export. |
| RegFile | The full path of the target *.reg file. If `RegFile` exists it will be overwritten. |

## Remarks

`RegImport` will export from your **local** registry, so you must ensure that `KeyPath` points to your mounted your PE hive if you intend to export from the PE registry.

## Related

[RegHiveLoad](./RegHiveLoad.md), [RegHiveUnload](./RegHiveUnload.md)

## Examples

#### Example 1

```pebakery
// Make sure RegHiveLoad,<KeyPath> matches the path in your *.reg file
RegHiveLoad,Tmp_System,%RegSystem%
RegExport,HKEY_LOCAL_MACHINE\Tmp_System\ControlSet001\Control\Network,c:\myFile.reg
RegHiveUnLoad,Tmp_System
```