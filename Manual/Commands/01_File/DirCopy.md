# DirCopy

Copy directory.

When used with wildcard, multiple files can be copied.

## Syntax

```pebakery
DirCopy,<SrcDir>,<DestDir>
```

- Arguments

| Argument | Description |
| --- | --- |
| SrcDir | Directory to copy.<br>Wildcard (*, ?) can be used in name. |
| DestDir | Destination directory. New directory will be created if not exists. |

## Remarks

If `DestDir` is a file, DirCopy fails.

If wildcard is used, `DestDir` copies subdirectories filtered by wildcard.

WinBuilder 082 has a bug that DirCopy works similar to FileCopy when wildcard is used. Turning on compatibility option `Simulate WinBuilder's DirCopy Asterisk Bug` emulates this bug.

## Example

Let us assume a directory `%SrcDir%` contains these files:

```pebakery
(D) %SrcDir%
|- (D) AAA - (F) A.txt
|- (D) ABB - (F) B.ini
|- (D) ACC - (F) C.txt, D.txt
|- (D) AEE
|- (F) Y.ini
|- (F) AZ.txt
```

```pebakery
// All files of %SrcDir% will be copied into %DestDir%.
DirCopy,%SrcDir%,%DestDir%

- Result
(D) %DestDir%
|- (D) AAA - (F) A.txt
|- (D) ABB - (F) B.ini
|- (D) ACC - (F) C.txt, D.txt
|- (D) AEE
|- (F) Y.ini
|- (F) AZ.txt
```

```pebakery
// All files of %SrcDir% will be copied into %DestDir%.
DirCopy,%SrcDir%\A*,%DestDir%

- Correct Result
(D) %DestDir%
|- (D) AAA - (F) A.txt
|- (D) ABB - (F) B.ini
|- (D) ACC - (F) C.txt, D.txt
|- (D) AEE

- WB082 Result (Bug)
(D) %DestDir%
|- (D) AAA - (F) A.txt
|- (D) ABB - (F) B.ini
|- (D) ACC - (F) C.txt, D.txt
|- (D) AEE
|- (F) AZ.txt
```

```pebakery
// All files of %SrcDir% will be copied into %DestDir%.
DirCopy,%SrcDir%\A*,%DestDir%

- Correct Result
(D) %DestDir%
|- (D) AAA - (F) A.txt
|- (D) ABB - (F) B.ini
|- (D) ACC - (F) C.txt, D.txt
|- (D) AEE

- WB082 Result (Bug)
(D) %DestDir%
|- (D) AAA - (F) A.txt
|- (D) ABB - (F) B.ini
|- (D) ACC - (F) C.txt, D.txt
|- (D) AEE
|- (F) AZ.txt
```
