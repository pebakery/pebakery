# ExtractFile

Extracts a single file from inside a plugin.

## Syntax

```pebakery
ExtractFile,<PluginFile>,<DirName>,<FileName>,<DestDir>
```

### Arguments

| Argument | Description |
| --- | --- |
| PluginFile | The full path to the plugin. **Hint:** Use `%PluginFile%` to reference the current plugin. |
| DirName | The folder inside the plugin that contains the file. |
| FileName | The name of the file to extract. |
| DestDir | The full path of the target directory. If `FileName` already exists it will be overwritten. |

## Remarks

None.

## Related

[Encode](./Encode.md), [ExtractAllFiles](./ExtractAllFiles.md)

## Examples

### Example 1

Simple directory structure inside a plugin.

```pebakery
root/
|--- Folder/
     |--- myApp.exe
     |--- myApp.ini
     |--- moreFiles.7z
|--- Reg/
     |--- mySettings.reg
|--- src/
     |---mySrc.au3
```

Extract mySettings.reg from the running plugin's *Reg* directory.

```pebakery
ExtractFile,%PluginFile%,Reg,mySettings.reg,c:\Temp
```