# RegWrite

Create or modify a key or value in the registry.

## Syntax

```pebakery
RegWrite,<HKey>,<ValueType>,<KeyPath>,<ValueName>,<Value>,[NOWARN]
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
| ValueType   | The type of data the value contains. Supported types are: |
||0x0 - REG_NONE - Empty Key |
||0x1 - REG_SZ - String |
||0x2 - REG_EXPAND_SZ - Expanded String - Will expand any variable value contained inside %%. (e.g. %temp%) |
||0x3 - REG_BINARY - Binary data - Data is specified in HEX, with each byte being specified by groups of two digits splitting each value with commas. |
||0x4 - REG_DWORD - 32bit integer |
||0x7 - REG_MULTI_SZ - Multiple Null Separated Strings |
||0x11 - REG_QWORD - 64bit integer |
| KeyPath | The full path of the registry key. |
| ValueName | The name of the value. |
| Value | The value to write. |

### Flags

| Flag | Description |
| --- | --- |
| NOWARN | **(Optional)** Suppress warning messages if the value already exists. |

## Remarks

PEBakery does not permit use of variables in `HKey` and `ValueType`.

If you need to modify the value of an ***existing*** REG_MULTI_SZ value consider using the `RegMulti` command to insert and delete values without overwriting the entire value list.

Default behavior is to log a warning message if the registry value exists in an effort to make troubleshooting easier in the event another plugin changes a previously set registry value. Use the `NoWarn` flag to override this behavior.

## Related

[RegHiveLoad](./RegHiveLoad.md), [RegHiveUnload](./RegHiveUnload.md), [RegMulti](./RegMulti.md)

## Examples

### Example 1

```pebakery
RegHiveLoad,Tmp_System,%RegSystem%

// Write a DWORD
RegWrite,HKLM,0x4,Tmp_System\ControlSet001\Services\VgaSave\Device0,DefaultSettings.XResolution,1024

// Write a BINARY value
RegWrite,HKLM,0x3,Tmp_System\ControlSet001\Control\Network\{4d36e975-e325-11ce-bfc1-08002be10318}\{12F2EEA2-EE86-4933-8C0B-346E5E57F332},InstallTimeStamp,d9,07,07,00,02,00,0e,00,04,00,31,00,20,00,fd,00

// Write a Multi-String value
RegWrite,HKLM,0x7,Tmp_System\ControlSet001\Control\Network,FilterClasses,ms_firewall_upper,scheduler,encryption,compression,vpn,loadbalance,failover,diagnostic,custom

RegHiveUnLoad,Tmp_System
```