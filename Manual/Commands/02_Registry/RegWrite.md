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
||0x0 - (REG_NONE) Empty Key |
||0x1 - (REG_SZ) String |
||0x2 - (REG_EXPAND_SZ) Expanded String - Will expand any variable value contained inside %%. (e.g. %temp%) |
||0x3 - (REG_BINARY) Binary data - Data is specified in HEX, with each byte being specified by groups of two digits splitting each value with commas. |
||0x4 - (REG_DWORD) 32bit integer |
||0x7 - (REG_MULTI_SZ) Multiple Null Separated Strings |
||0x11 - (REG_QWORD) 64bit integer |
| KeyPath | The full path of the registry key. |
| ValueName | The name of the value. |
| Value | The value to write.<br/>Large values can be wrapped for easier reading by using the `\` character to indicate that the value continues on the next line, similar to the .reg file format. **Note:** If the `Value` to be written is the `\` character eg. `RegWrite,HKLM,0x1,Tmp_System\Setup,OsLoaderPath,"\"` be sure to wrap it in double quotes so it is not mistaken for a line continuation.  |

### Flags

| Flag | Description |
| --- | --- |
| NOWARN | **(Optional)** Suppress OverWrite messages in the log if the `Value` already exists. |

## Remarks

PEBakery does not permit use of variables in `HKey` and `ValueType`.

If you need to modify the value of an ***existing*** REG_MULTI_SZ value consider using the `RegMulti` command to insert and delete values without overwriting the entire value list.

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

// Write a large BINARY value using multiple lines for easy reading
RegWrite,HKLM,0x3,Tmp_Default\Software\Microsoft\Windows\CurrentVersion\Explorer\Streams\Desktop,TaskbarWinXP,0c,\
00,00,00,08,00,00,00,02,00,00,00,00,00,00,00,b0,e2,2b,d8,64,57,d0,11,a9,6e,00,c0,4f,d7,05,a2,22,00,1c,00,0a,10,00,00,01,00,00,00,01,00,00,00,00,00,00,\
00,00,00,00,00,00,00,00,00,4c,00,00,00,01,14,02,00,00,00,00,00,c0,00,00,00,00,00,00,46,81,01,00,00,11,00,00,00,64,54,7a,06,bd,b2,cb,01,ea,2f,16,74,ca,\
b8,cb,01,ea,2f,16,74,ca,b8,cb,01,00,10,00,00,00,00,00,00,01,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,04,02,14,00,1f,44,47,1a,03,59,72,3f,a7,44,89,\
c5,55,95,fe,6b,30,ee,7e,00,74,00,1c,00,43,46,53,46,16,00,31,00,00,00,00,00,2d,3e,57,07,12,20,41,70,70,44,61,74,61,00,00,00,74,1a,59,5e,96,df,d3,48,8d,\
67,17,33,bc,ee,28,ba,c5,cd,fa,df,9f,67,56,41,89,47,c5,c7,6b,c0,b6,7f,3c,00,08,00,04,00,ef,be,2d,3e,56,07,2d,3e,57,07,2a,00,00,00,e4,01,00,00,00,00,02,\
00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,41,00,70,00,70,00,44,00,61,00,74,00,61,00,00,00,42,00,52,00,31,00,00,00,00,00,32,3e,d3,7c,10,20,52,6f,61,\
6d,69,6e,67,00,3c,00,08,00,04,00,ef,be,2d,3e,56,07,32,3e,d3,7c,2a,00,00,00,e5,01,00,00,00,00,02,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,52,00,6f,\
00,61,00,6d,00,69,00,6e,00,67,00,00,00,16,00,58,00,31,00,00,00,00,00,32,3e,da,90,14,20,4d,49,43,52,4f,53,7e,31,00,00,40,00,08,00,04,00,ef,be,2d,3e,56,\
07,32,3e,da,90,2a,00,00,00,e6,01,00,00,00,00,02,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,4d,00,69,00,63,00,72,00,6f,00,73,00,6f,00,66,00,74,00,00,\
00,18,00,68,00,31,00,00,00,00,00,ee,3a,85,1a,10,00,49,4e,54,45,52,4e,7e,31,00,00,50,00,08,00,04,00,ef,be,2d,3e,56,07,2d,3e,56,07,2a,00,00,00,f4,01,00,\
00,00,00,02,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,49,00,6e,00,74,00,65,00,72,00,6e,00,65,00,74,00,20,00,45,00,78,00,70,00,6c,00,6f,00,72,00,65,\
00,72,00,00,00,18,00,5e,00,31,00,00,00,00,00,34,3e,40,8e,11,00,51,55,49,43,4b,4c,7e,31,00,00,46,00,08,00,04,00,ef,be,2d,3e,56,07,34,3e,40,8e,2a,00,00,\
00,f5,01,00,00,00,00,02,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,51,00,75,00,69,00,63,00,6b,00,20,00,4c,00,61,00,75,00,6e,00,63,00,68,00,00,00,18,\
00,00,00,60,00,00,00,03,00,00,a0,58,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,00,52,cb,74,30,40,c1,22,45,81,05,c7,54,dd,94,b1,\
ad,9f,09,e0,19,75,24,e0,11,89,f4,00,50,56,c0,00,08,52,cb,74,30,40,c1,22,45,81,05,c7,54,dd,94,b1,ad,9f,09,e0,19,75,24,e0,11,89,f4,00,50,56,c0,00,08,00,\
00,00,00,8d,00,00,00,40,07,00,00,00,00,00,00,1e,00,00,00,00,00,00,00,00,00,00,00,1e,00,00,00,00,00,00,00,01,00,00,00,01,00,00,00,aa,4f,28,68,48,6a,d0,\
11,8c,78,00,c0,4f,d9,18,b4,a9,04,00,00,40,0d,00,00,00,00,00,00,1e,00,00,00,00,00,00,00,00,00,00,00,1e,00,00,00,00,00,00,00,01,00,00,00

RegHiveUnLoad,Tmp_System
```

### Example 2

In cases where a registry key contains a `#` character followed by numbers PEBakery can mistakenly interpret this as a parameter passed from a `Run` command, even when this is not intended. The following example demonstrates when this behavior can occur and a workaround that can be used to ensure the intended result.

```pebakery

[Variables]
%hash%=#

[Process]
Run,%PluginFile%,Test

[Test]
RegHiveLoad,Tmp_System,%RegSystem%

// Intended Result: HKLM\Tmp_System\ControlSet001\Control\CriticalDeviceDatabase\1394#609E&10483\Service
// Due to #609 being interpreted as a parameter
// Actual Result : HKLM\Tmp_System\ControlSet001\Control\CriticalDeviceDatabase\1394E&10483\Service
RegWrite,HKLM,0x1,Tmp_System\ControlSet001\Control\CriticalDeviceDatabase\1394%hash%609E&10483,Service,sbp2port

// Workaround uses a variable to write the # character into the registry path
// Result: HKLM\Tmp_System\ControlSet001\Control\CriticalDeviceDatabase\1394#609E&10483\Service
RegWrite,HKLM,0x1,Tmp_System\ControlSet001\Control\CriticalDeviceDatabase\1394%hash%609E&10483,Service,sbp2port

RegHiveUnLoad,Tmp_System
```
