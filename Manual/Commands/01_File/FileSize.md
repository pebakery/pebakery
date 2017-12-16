# FileSize

**Alias**: `Retrieve,FileSize`

Get size of the file.

## Syntax

```pebakery
FileSize,<FilePath>,<DestVar>
```

- Arguments

| Argument | Description |
| --- | --- |
| FilePath | Path of the file. |
| DestVar | Variable name to save file size. |

## Example

```pebakery
// %Dest% is set to "246784".
FileSize,%WindowsDir%\System32\notepad.exe,%Dest%
```
