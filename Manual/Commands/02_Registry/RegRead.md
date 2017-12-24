# RegRead

Reads a value from the registry.

## Syntax

```pebakery
RegRead,<HKey>,<KeyPath>,<ValueName>,<DestVar>
```

### Arguments

| Argument | Description |
| --- | --- |
| HKEY | The root key must be one of the following: |
|| HKEY_LOCAL_MACHINE or HKLM |
|| HKEY_CURRENT_CONFIG or HKCC |
|| HKEY_CLASSES_ROOT or HKCR |
|| HKEY_CURRENT_USER or HKCU |
|| HKEY_USERS or HKU |
| KeyPath | The full path of the registry key. |
| ValueName | The name of the value. |
| DestVar | The %Variable% where the value will stored. |

## Remarks

If the registry key does not exist the operation will fail.

## Related

[RegHiveLoad](./RegHiveLoad.md), [RegHiveUnload](./RegHiveUnload.md)

## Examples

#### Example 1

```pebakery
RegHiveLoad,Tmp_System,%RegSystem%
RegRead,HKLM,Tmp_System\ControlSet001\Services\VgaSave\Device0,DefaultSettings.XResolution,%myXresolution%
Echo, %myXresolution%
RegHiveUnLoad,Tmp_System
```