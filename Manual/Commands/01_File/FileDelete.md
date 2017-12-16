# FileDelete

Delete file.

When used with wildcard, multiple files can be deleted.

## Syntax

```pebakery
FileDelete,<FilePath>,[NOWARN],[NOREC]
```

- Arguments

| Argument | Description |
| --- | --- |
| FilePath | File or files to delete.<br>Wildcard (*, ?) can be used in filename. |

- Flags

| Flag | Description |
| --- | --- |
| NOWARN | Do not log warning if file not exists. |
| NOREC | Ignore subdirectories when deleting files when using wildcard. |

## Remarks

When wildcard is used, files in subdirectories will also be deleted. To prevent this, use `NOREC` flag.

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
// %SrcDir%\AZ.txt will be deleted.
FileDelete,%SrcDir%\AZ.txt

Result
(D) %SrcDir%
|- (D) AAA - (F) A.txt
|- (D) ABB - (F) B.ini
|- (D) ACC - (F) C.txt, D.txt
|- (D) AEE
|- (F) Y.ini
```

```pebakery
// All .txt files in %SrcDir% will be deleted.
FileDelete,%SrcDir%\*.txt

Result
(D) %SrcDir%
|- (D) AAA
|- (D) ABB - (F) B.ini
|- (D) ACC
|- (D) AEE
|- (F) Y.ini
```

```pebakery
// All .ini files in %SrcDir% will be deleted, ignoring subdirectory.
FileDelete,%SrcDir%\*.ini,NOREC

Result
(D) %SrcDir%
|- (D) AAA - (F) A.txt
|- (D) ABB - (F) B.ini
|- (D) ACC - (F) C.txt, D.txt
|- (D) AEE
|- (F) AZ.txt
```
