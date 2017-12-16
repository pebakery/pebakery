# FileVersion

**Alias**: `Retrieve,FileVersion`

Get version of the file.

## Syntax

```pebakery
FileVersion,<FilePath>,<DestVar>
```

- Arguments

| Argument | Description |
| --- | --- |
| FilePath | Path of the file. |
| DestVar | Variable name to save file version. |

## Example

```pebakery
// %Dest% is set to "10.0.16299.15".
FileVersion,%WindowsDir%\System32\notepad.exe,%Dest%
```
