# ExtractAllFiles

Extracts all files from the specified directory inside a plugin.

## Syntax

```pebakery
ExtractAllFiles,<PluginFile>,<DirName>,<DestDir>
```

### Arguments

| Argument | Description |
| --- | --- |
| PluginFile | The full path to the plugin. **Hint:** Use `%PluginFile%` to reference the current plugin. |
| DirName | The folder inside the plugin that contains the files. |
| DestDir | The full path of the target directory. If the files to be extracted already exist they will be overwritten. |

## Remarks

None.

## Related

[Encode](./Encode.md), [ExtractFile](./ExtractFile.md)

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

Extract all files contained in the *Folder* directory of the running plugin to C:\Temp.

```pebakery
ExtractAllFiles,%ScriptFile%,Folder,C:\Temp
```