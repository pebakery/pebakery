# FileRename

**Alias**: `FileMove`

Rename or move a file.

## Syntax

```pebakery
FileRename,<SrcPath>,<DestPath>
```

- Arguments

| Argument | Description |
| --- | --- |
| SrcPath | Path of file to rename. |
| DestPath | New path to rename file. |

## Remarks

FileRename can also be used to move file.

WinBuilder 082 allows FileRename to move directory. Turning on compatibility option `FileRename and DirMove work like PathMove` makes FileRename identical to PathMove.

## Example

```pebakery
// A.txt will be renamed to B.txt.
FileRename,%SrcDir%\A.txt,%SrcDir%\B.txt
```

```pebakery
// %SrcDir%\A.txt will be moved into %DestDir%\B.txt.
FileMove,%SrcDir%\A.txt,%DestDir%\B.txt
```
