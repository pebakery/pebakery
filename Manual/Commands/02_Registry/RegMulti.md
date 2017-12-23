# RegMulti

Modify a multi-string value in the registry.

## Syntax

```pebakery
RegMulti,<HKey>,<KeyPath>,<ValueName>,<Action>,<Arg1>[,Arg2]
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
| ValueName | The multi-string value to modify. |
| Action | Action must be one of the following keywords: |
|| APPEND - Writes a string at the end of the specified value. |
|| PREPEND - Writes a string at the start of the specified value. |
|| BEFORE - Writes a string before the matching search string. |
|| BEHIND - Writes a string after the matching search string. |
|| PLACE - Writes a string at the specified index. |
|| DELETE - Removes the specified string from the value list. |
|| INDEX - Queries the index of the specified string. If the string does not exist, the returned value is 0. |
| Arg1 | String or Index |
| Arg2 | String or %Variable% |

## Remarks

If the `string` already exists in `ValueName`, a warning is logged and the `Action` is ignored.

## Related

[RegHiveLoad](./RegHiveLoad.md), [RegHiveUnload](./RegHiveUnload.md)

## Examples

#### Example 1

Append a string to the end of the value list.

```pebakery
RegHiveLoad,Tmp_System,%RegSystem%
// RegMulti,<HKEY>,<KeyPath>,<ValueName>,APPEND,<String>
RegMulti,HKLM,Tmp_System\ControlSet001\Control\Class\{71A27CDD-812A-11D0-BEC7-08002BE2092F},UpperFilters,APPEND,snapman
RegHiveUnLoad,Tmp_System
```

#### Example 2

Prepend a string to the beginning of the value list.

```pebakery
RegHiveLoad,Tmp_System,%RegSystem%
// RegMulti,<HKEY>,<KeyPath>,<ValueName>,PREPEND,<String>
RegMulti,HKLM,Tmp_System\ControlSet001\Control\Class\{71A27CDD-812A-11D0-BEC7-08002BE2092F},UpperFilters,PREPEND,snapman
RegHiveUnLoad,Tmp_System
```

#### Example 3

Write a string before a specified value. In this example we will write the value `snapman` before the value `rdyboost`.

```pebakery
RegHiveLoad,Tmp_System,%RegSystem%
// RegMulti,<HKEY>,<KeyPath>,<ValueName>,BEFORE,<SearchString>,<String>
RegMulti,HKLM,Tmp_System\ControlSet001\Control\Class\{71A27CDD-812A-11D0-BEC7-08002BE2092F},UpperFilters,BEFORE,rdyboost,snapman
RegHiveUnLoad,Tmp_System
```

#### Example 4

Write a string after a specified value. In this example we will write the value `snapman` after the value `rdyboost`.

```pebakery
RegHiveLoad,Tmp_System,%RegSystem%
// RegMulti,<HKEY>,<KeyPath>,<ValueName>,BEHIND,<SearchString>,<String>
RegMulti,HKLM,Tmp_System\ControlSet001\Control\Class\{71A27CDD-812A-11D0-BEC7-08002BE2092F},UpperFilters,BEHIND,rdyboost,snapman
RegHiveUnLoad,Tmp_System
```

#### Example 5

Write a string at a specific index. In this example we will write the value `snapman` at the 3rd position in the list.

```pebakery
RegHiveLoad,Tmp_System,%RegSystem%
// RegMulti,<HKEY>,<KeyPath>,<ValueName>,PLACE,<Index>,<String>
RegMulti,HKLM,Tmp_System\ControlSet001\Control\Class\{71A27CDD-812A-11D0-BEC7-08002BE2092F},UpperFilters,PLACE,3,snapman
RegHiveUnLoad,Tmp_System
```

#### Example 6

Delete a string from the list.

```pebakery
RegHiveLoad,Tmp_System,%RegSystem%
// RegMulti,<HKEY>,<KeyPath>,<ValueName>,DELETE,<String>
RegMulti,HKLM,Tmp_System\ControlSet001\Control\Class\{71A27CDD-812A-11D0-BEC7-08002BE2092F},UpperFilters,DELETE,snapman
RegHiveUnLoad,Tmp_System
```

#### Example 7

Find the position of a specified string if it exists in the list.

```pebakery
RegHiveLoad,Tmp_System,%RegSystem%
// RegMulti,<HKEY>,<KeyPath>,<ValueName>,INDEX,<String>,<DestVar>
RegMulti,HKLM,Tmp_System\ControlSet001\Control\Class\{71A27CDD-812A-11D0-BEC7-08002BE2092F},UpperFilters,INDEX,snapman,%varPos%
Echo,"snapman is located at position: %varPos%"
RegHiveUnLoad,Tmp_System
```