# PathMove

Move a file or directory.

## Syntax

```pebakery
PathMove,<SrcPath>,<DestPath>
```

- Arguments

| Argument | Description |
| --- | --- |
| SrcPath | Path of file or directory to move. |
| DestPath | Destination Path. |

## Remarks

Turning on compatibility option `FileRename and DirMove work like PathMove` makes FileRename and DirMove identical to PathMove.

## Example

```pebakery
// A.txt will be renamed to B.txt.
PathMove,%SrcDir%\A.txt,%SrcDir%\B.txt
```

```pebakery
// %SrcDir%\A.txt will be moved into %DestDir%\B.txt.
PathMove,%SrcDir%\A.txt,%DestDir%\B.txt
```

```pebakery
// If %DestDir% exists : %SrcDir%\A will be moved into %DestDir%\A.
// If %DestDir% not exist : %SrcDir%\A will be moved into %DestDir%.
PathMove,%SrcDir%\A,%DestDir%
```
