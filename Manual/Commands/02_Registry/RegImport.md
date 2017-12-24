# RegImport

Imports the contents of a registry file (*.reg) to your local registry system.

This command has the same effect as running `REG.exe IMPORT <RegFile>` from Windows and is ideal for importing a large quantity of prepared registry entries into your build.

## Syntax

```pebakery
RegImport,<RegFile>
```

### Arguments

| Argument | Description |
| --- | --- |
| RegFile | The full path of the *.reg file to import. |

## Remarks

No warning messages will be logged if the registry values exist.

**Warning:**
Because `RegImport` will imports the *.reg file into your **local** registry, you must ensure that the file contains the correct path to your mounted PE hive, or you risk corrupting your local registry.

## Related

[RegHiveLoad](./RegHiveLoad.md), [RegHiveUnload](./RegHiveUnload.md)

## Examples

### Example 1

Assume we have the following *myFile.reg* file we want to import into our PE Registry. Note that we have already modifed the registry keys to point to the path where we will mount the PE's SYSTEM hive.

```pebakery
Windows Registry Editor Version 5.00

[HKEY_LOCAL_MACHINE\Tmp_System\ControlSet001\Control\Network]
"FilterClasses"=hex(7):6d,00,73,00,5f,00,66,00,69,00,72,00,65,00,77,00,61,00,\
  6c,00,6c,00,5f,00,75,00,70,00,70,00,65,00,72,00,00,00,73,00,63,00,68,00,65,\
  00,64,00,75,00,6c,00,65,00,72,00,00,00,65,00,6e,00,63,00,72,00,79,00,70,00,\
  74,00,69,00,6f,00,6e,00,00,00,63,00,6f,00,6d,00,70,00,72,00,65,00,73,00,73,\
  00,69,00,6f,00,6e,00,00,00,76,00,70,00,6e,00,00,00,6c,00,6f,00,61,00,64,00,\
  62,00,61,00,6c,00,61,00,6e,00,63,00,65,00,00,00,66,00,61,00,69,00,6c,00,6f,\
  00,76,00,65,00,72,00,00,00,64,00,69,00,61,00,67,00,6e,00,6f,00,73,00,74,00,\
  69,00,63,00,00,00,63,00,75,00,73,00,74,00,6f,00,6d,00,00,00,00,00
"IscsiSupportedProtocols"="Ndisuio,rspndr,lltdio,RasPppoe,Tcpip,Tcpip6"
"MaxNumFilters"=dword:00000008
```

```pebakery
// Make sure RegHiveLoad,<KeyPath> matches the path in your *.reg file
// or you will overwrite your local system registry!
RegHiveLoad,Tmp_System,%RegSystem%
RegImport,c:\myFile.reg
RegHiveUnLoad,Tmp_System
```