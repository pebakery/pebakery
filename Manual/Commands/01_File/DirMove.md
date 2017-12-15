# DirMove

Move a directory.

## Syntax

```pebakery
DirMove,<SrcDir>,<DestPath>
```

- Arguments

| Argument | Description |
| --- | --- |
| SrcDir | Source directory to move. |
| DestPath | Destination directory. |

## Remarks

WinBuilder 082 allows DirMove to move file. Turning on compatibility option `FileRename and DirMove work like PathMove` makes DirMove identical to PathMove.

## Example

```pebakery
// If %DestDir% exists : %SrcDir%\A will be moved into %DestDir%\A.
// If %DestDir% not exist : %SrcDir%\A will be moved into %DestDir%.
DirMove,%SrcDir%\A,%DestDir%
```
